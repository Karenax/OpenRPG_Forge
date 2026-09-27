"""Replay extraction only; no launch, injection, or gate qualification.

Adapted from the historical Forge Rust RenderDoc adapter; see README.md.
The RenderDoc API is injected so extraction and cleanup can be tested without GPU.
"""

from gate1_renderdoc_model import INPUT_SCHEMA, analyze_document

NORMALIZED_ACTION_SCHEMA = INPUT_SCHEMA


class AdapterError(RuntimeError):
    pass


def result_succeeded(rd, result):
    return getattr(result, "code", result) == rd.ResultCode.Succeeded


def result_description(result):
    message = getattr(result, "Message", None)
    return str(message()) if callable(message) else str(result)


def has_flag(flags, flag):
    return bool(flags & flag)


def action_flags(rd, action):
    names = []
    for name in (
        "Clear",
        "Drawcall",
        "Dispatch",
        "MeshDispatch",
        "CmdList",
        "SetMarker",
        "PushMarker",
        "PopMarker",
        "Present",
        "MultiAction",
        "Copy",
        "Resolve",
        "GenMips",
        "PassBoundary",
        "DispatchRay",
        "BuildAccStruct",
        "Indexed",
        "Instanced",
        "Auto",
        "Indirect",
        "ClearColor",
        "ClearDepthStencil",
        "BeginPass",
        "EndPass",
        "CommandBufferBoundary",
    ):
        flag = getattr(rd.ActionFlags, name, None)
        if flag is not None and has_flag(action.flags, flag):
            names.append(name)
    return names


def topology_name(rd, topology):
    for name in ("TriangleList", "LineList", "PointList"):
        if topology == getattr(rd.Topology, name):
            return name
    text = str(topology)
    return text.rsplit(".", 1)[-1]


def active_shader_stages(rd, pipeline):
    stages = []
    candidates = (
        ("Vertex", "Vertex"),
        ("Hull", "TessellationControl"),
        ("Domain", "TessellationEvaluation"),
        ("Geometry", "Geometry"),
        ("Pixel", "Fragment"),
        ("Compute", "Compute"),
        ("Task", "Task"),
        ("Mesh", "Mesh"),
        ("RayGen", "RayGeneration"),
        ("Intersection", "Intersection"),
        ("AnyHit", "AnyHit"),
        ("ClosestHit", "ClosestHit"),
        ("Miss", "Miss"),
        ("Callable", "Callable"),
    )
    for renderdoc_name, normalized_name in candidates:
        stage = getattr(rd.ShaderStage, renderdoc_name, None)
        if stage is not None and pipeline.GetShaderReflection(stage) is not None:
            stages.append(normalized_name)
    return stages


def normalize_action(rd, controller, structured_file, action):
    children = [
        normalize_action(rd, controller, structured_file, child) for child in action.children
    ]
    flags = action_flags(rd, action)
    is_draw = "Drawcall" in flags
    is_instanced = "Instanced" in flags
    is_indirect = "Indirect" in flags
    if "MultiAction" in flags and not children:
        raise AdapterError(
            "RenderDoc left MultiAction event {} as a leaf".format(int(action.eventId))
        )

    topology = None
    stages = []
    if is_draw and not children:
        controller.SetFrameEvent(action.eventId, True)
        pipeline = controller.GetPipelineState()
        topology = topology_name(rd, pipeline.GetPrimitiveTopology())
        stages = active_shader_stages(rd, pipeline)

    num_indices = int(action.numIndices)
    num_instances = int(action.numInstances)
    indirect_resolved = not is_indirect
    if is_indirect and is_draw and not children:
        # A zero value can also mean "not used" in ActionDescription. For an indirect
        # call both fields must therefore be concrete and non-zero before the adapter
        # claims that replay resolved the arguments.
        indirect_resolved = num_indices > 0 and num_instances > 0

    return {
        "event_id": int(action.eventId),
        "name": str(action.GetName(structured_file)),
        "flags": flags,
        "topology": topology,
        "num_indices": num_indices,
        "num_instances": num_instances,
        "instanced": is_instanced,
        "indirect": is_indirect,
        "indirect_resolved": indirect_resolved,
        "active_stages": stages,
        "children": children,
    }


def count_present_actions(rd, actions):
    present_count = 0

    def visit(action):
        nonlocal present_count
        if has_flag(action.flags, rd.ActionFlags.Present):
            present_count += 1
        for child in action.children:
            visit(child)

    for action in actions:
        visit(action)
    return present_count


def inspect_draw_events(rd, controller, roots, event_ids):
    """Bounded replay metadata/disassembly, not automatic identification of a world object."""
    import hashlib
    if not event_ids or len(event_ids) > 16 or len(set(event_ids)) != len(event_ids) or any(e <= 0 for e in event_ids):
        raise AdapterError('Inspect requires 1..16 unique positive event IDs')
    actions = {}
    def visit(action):
        actions[int(action.eventId)] = action
        for child in action.children:
            visit(child)
    for root in roots:
        visit(root)
    for event in event_ids:
        action = actions.get(event)
        if action is None or action.children or not has_flag(action.flags, rd.ActionFlags.Drawcall):
            raise AdapterError('Inspect event is not a draw leaf: ' + str(event))
    textures = {str(t.resourceId): t for t in controller.GetTextures()}
    def target(descriptor):
        resource = str(descriptor.resource)
        texture = textures.get(resource)
        return {'resource': resource, 'first_slice': int(descriptor.firstSlice),
                'first_mip': int(descriptor.firstMip),
                'texture': None if texture is None else {
                    'width': int(texture.width), 'height': int(texture.height),
                    'array_size': int(texture.arraysize), 'format': str(texture.format.Name())}}
    shaders, draws = {}, []
    for event in event_ids:
        controller.SetFrameEvent(event, True)
        pipeline = controller.GetPipelineState()
        stages = {}
        for name in ('Vertex', 'Pixel'):
            stage = getattr(rd.ShaderStage, name)
            reflection = pipeline.GetShaderReflection(stage)
            if reflection is None:
                continue
            shader = str(pipeline.GetShader(stage))
            entry = pipeline.GetShaderEntryPoint(stage)
            key = shader + ':' + entry
            stages[name] = key
            if key not in shaders:
                disassembly = controller.DisassembleShader(pipeline.GetGraphicsPipelineObject(), reflection, '')
                if not disassembly or len(disassembly) > 2 * 1024 * 1024:
                    raise AdapterError('Shader disassembly missing or exceeds inspection bound')
                shaders[key] = {'resource': shader, 'entry': entry,
                                'encoding': str(reflection.encoding),
                                'raw_sha256': hashlib.sha256(bytes(reflection.rawBytes)).hexdigest(),
                                'disassembly': disassembly}
        draws.append({'event_id': event, 'num_indices': int(actions[event].numIndices),
                      'num_instances': int(actions[event].numInstances), 'shaders': stages,
                      'depth_target': target(pipeline.GetDepthTarget()),
                      'color_targets': [target(t) for t in pipeline.GetOutputTargets()]})
    return {'scope': 'selected_gpu_draws_not_world_caster_identity', 'draws': draws, 'shaders': shaders}


def analyze_capture(rd, capture_path, gpu_durations=False, inspect_events=None):
    capture = rd.OpenCaptureFile()
    controller = None
    try:
        opened = capture.OpenFile(str(capture_path), "", None)
        if not result_succeeded(rd, opened):
            raise AdapterError(
                "RenderDoc could not open capture: {}".format(result_description(opened))
            )
        support = capture.LocalReplaySupport()
        if support != rd.ReplaySupport.Supported:
            raise AdapterError("capture local replay support is {}".format(support))
        replay_result, controller = capture.OpenCapture(rd.ReplayOptions(), None)
        if not result_succeeded(rd, replay_result) or controller is None:
            raise AdapterError(
                "RenderDoc could not replay capture: {}".format(
                    result_description(replay_result)
                )
            )
        properties = controller.GetAPIProperties()
        if properties.pipelineType != rd.GraphicsAPI.Vulkan:
            raise AdapterError(
                "capture pipeline is not Vulkan: {}".format(properties.pipelineType)
            )
        if bool(properties.degraded):
            raise AdapterError("capture replay is degraded")

        roots = controller.GetRootActions()
        structured = controller.GetStructuredFile()
        document = {
            "schema": NORMALIZED_ACTION_SCHEMA,
            "capture": {
                "captured_frame_count": 1,
                "present_count": count_present_actions(rd, roots),
                "local_replay_support": "Supported",
                "pipeline_type": "Vulkan",
                "degraded": False,
            },
            "actions": [
                normalize_action(rd, controller, structured, action) for action in roots
            ],
        }
        timing = None
        inspection = inspect_draw_events(rd, controller, roots, inspect_events) if inspect_events else None
        if gpu_durations:
            from gate1_renderdoc_timing import fetch_durations
            timing = fetch_durations(rd, controller, roots, structured)
    finally:
        if controller is not None:
            controller.Shutdown()
        capture.Shutdown()

    analysis = analyze_document(document)
    if timing is not None:
        analysis['gpu_durations'] = timing
    if inspection is not None:
        analysis['draw_inspection'] = inspection
    return document, analysis
