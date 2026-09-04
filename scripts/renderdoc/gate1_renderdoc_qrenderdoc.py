"""Governed RenderDoc 1.46 acquisition adapter for Gate 1.

This file is executed by ``qrenderdoc.exe --python``. It owns only the external
capture and replay boundary: the Rust runtime owns scene stability and the
separate pure model owns action validation and metric arithmetic.
"""

import hashlib
import json
import os
from pathlib import Path
import subprocess
import sys
import time
import traceback

import renderdoc as rd


READY_FILE = "gate1-renderdoc.ready.json"
SIGNAL_FILE = "gate1-renderdoc.capture.signal.json"
LEDGER_FILE = "gate1-renderdoc.runtime.json"
RUNTIME_FAILURE_FILE = "gate1-renderdoc.failure.txt"
ACTION_DOCUMENT_FILE = "gate1-renderdoc.actions.json"
ANALYSIS_FILE = "gate1-renderdoc.analysis.json"
ADAPTER_RECEIPT_FILE = "gate1-renderdoc.adapter.json"
ADAPTER_FAILURE_FILE = "gate1-renderdoc.adapter.failure.json"
REQUIRED_RENDERDOC_VERSION = "1.46"
NORMALIZED_ACTION_SCHEMA = "forge-gate1-renderdoc-actions/v2"
READY_TIMEOUT_SECONDS = 95.0
CAPTURE_TIMEOUT_SECONDS = 45.0

READY_FIELDS = {
    "schema",
    "schema_version",
    "status",
    "nonce",
    "process_id",
    "source_commit",
    "build_profile",
    "required_backend",
    "required_renderdoc_version",
    "ready_frame",
    "freeze_settle_frames",
    "virtual_time_paused",
    "frozen_state_sha256",
    "frozen_state",
    "files",
    "qualification",
}
FROZEN_STATE_FIELDS = {
    "sources",
    "seeds",
    "tick",
    "vsync_requested",
    "expected_weather",
    "weather_target",
    "weather_wetness_current_bits",
    "weather_wetness_target_bits",
    "quality_selection",
    "quality_startup_tier",
    "quality_current_tier",
    "quality_transition_count",
    "pipeline_total",
    "pipeline_ready",
    "camera",
    "window",
    "gpu",
    "governed_scene_hashes",
}
SOURCE_FIELDS = {
    "terrain",
    "vegetation",
    "environment",
    "water",
    "weather",
    "quality",
    "cadle_assets_used",
}
SEED_FIELDS = {"terrain", "vegetation", "water", "weather"}
TICK_FIELDS = {
    "virtual_elapsed_nanoseconds",
    "fixed_elapsed_nanoseconds",
    "fixed_timestep_nanoseconds",
    "fixed_tick",
}
CAMERA_FIELDS = {"translation_m", "forward", "vertical_fov_degrees", "active"}
WINDOW_FIELDS = {
    "physical_width",
    "physical_height",
    "scale_factor",
    "visible",
    "resizable",
}
GPU_FIELDS = {
    "name",
    "vendor",
    "device",
    "device_type",
    "driver",
    "driver_info",
    "backend",
    "max_texture_dimension_2d",
    "max_bind_groups",
    "max_uniform_buffer_binding_size",
    "max_storage_buffer_binding_size",
}
GOVERNED_SCENE_HASH_FIELDS = {
    "visual_valley",
    "shadow",
    "vegetation",
    "scenery",
    "environment",
    "water_static",
    "weather_static",
}
FILE_CONTRACT_FIELDS = {"ready", "signal", "ledger", "failure"}
QUALIFICATION_FIELDS = {
    "runtime_state_qualified",
    "external_capture_observed_by_runtime",
    "renderdoc_analysis_completed_by_runtime",
    "gate1_route_qualified",
    "limitation",
}
SIGNAL_FIELDS = {
    "schema",
    "schema_version",
    "nonce",
    "process_id",
    "source_commit",
    "ready_sha256",
    "capture_id",
    "renderdoc",
}
RENDERDOC_SIGNAL_FIELDS = {
    "capture_triggered",
    "target_control_ident",
    "target_api",
    "tool_version",
    "qrenderdoc_sha256",
    "renderdoccmd_sha256",
    "renderdoc_dll_sha256",
}
RUNTIME_LEDGER_FIELDS = {
    "schema",
    "schema_version",
    "status",
    "nonce",
    "process_id",
    "source_commit",
    "build_profile",
    "ready_frame",
    "signal_frame",
    "capture_window_frames",
    "ready_sha256",
    "capture_id",
    "renderdoc",
    "frozen_state_sha256",
    "frozen_state_initial",
    "frozen_state_final",
    "frozen_state_stable",
    "files",
    "qualification",
}
ADAPTER_RECEIPT_SCHEMA = "forge-gate1-renderdoc-adapter/v1"
ADAPTER_RECEIPT_FIELDS = {
    "schema",
    "status",
    "source_commit",
    "nonce",
    "weather",
    "capture_id",
    "target",
    "capture",
    "files",
    "qualification",
}
ADAPTER_TARGET_FIELDS = {"process_id", "target_control_ident", "api"}
ADAPTER_CAPTURE_FIELDS = {
    "path",
    "sha256",
    "renderdoc_capture_id",
    "frame_number",
    "byte_size",
    "captured_frame_count",
}
ADAPTER_FILE_FIELDS = {
    READY_FILE,
    SIGNAL_FILE,
    LEDGER_FILE,
    ACTION_DOCUMENT_FILE,
    ANALYSIS_FILE,
}
ADAPTER_QUALIFICATION_FIELDS = {
    "runtime_state_qualified",
    "external_capture_observed",
    "renderdoc_analysis_completed",
    "renderdoc_metrics_are_fps_evidence",
}


class AdapterError(RuntimeError):
    """A fail-closed acquisition or replay error."""


def expect_exact_fields(value, expected, label):
    if not isinstance(value, dict):
        raise AdapterError("{} is not an object".format(label))
    actual = set(value)
    if actual != expected:
        missing = sorted(expected - actual)
        unexpected = sorted(actual - expected)
        raise AdapterError(
            "{} field set mismatch: missing={} unexpected={}".format(
                label, missing, unexpected
            )
        )
    return value


def expect_nonempty_string(value, label):
    if not isinstance(value, str) or not value.strip():
        raise AdapterError("{} is not a non-empty string".format(label))
    return value


def expect_uint64(value, label):
    if (
        isinstance(value, bool)
        or not isinstance(value, int)
        or value < 0
        or value > (1 << 64) - 1
    ):
        raise AdapterError("{} is not an unsigned 64-bit integer".format(label))
    return value


def expect_finite_number(value, label):
    if isinstance(value, bool) or not isinstance(value, (int, float)):
        raise AdapterError("{} is not a finite number".format(label))
    if isinstance(value, float) and (value != value or value in (float("inf"), float("-inf"))):
        raise AdapterError("{} is not a finite number".format(label))
    return value


def validate_sha256(value, label):
    expect_nonempty_string(value, label)
    if len(value) != 64 or any(character not in "0123456789abcdef" for character in value):
        raise AdapterError("{} is not a lowercase SHA-256".format(label))


def validate_source_commit(value, label):
    expect_nonempty_string(value, label)
    if len(value) != 40 or any(character not in "0123456789abcdef" for character in value):
        raise AdapterError("{} is not a lowercase 40-character Git object id".format(label))


def validate_capture_id(value):
    expect_nonempty_string(value, "capture id")
    allowed = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-_.:"
    if len(value) > 96 or any(character not in allowed for character in value):
        raise AdapterError("capture id is too long or contains unsupported characters")


def required_environment(name):
    value = os.environ.get(name, "")
    if not value:
        raise AdapterError("required environment variable {} is absent".format(name))
    return value


def sha256_file(path):
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        while True:
            chunk = handle.read(1024 * 1024)
            if not chunk:
                break
            digest.update(chunk)
    return digest.hexdigest()


def write_json_atomic(path, document):
    encoded = (json.dumps(document, indent=2, sort_keys=True) + "\n").encode("utf-8")
    temporary = path.with_name(".{}.{}.tmp".format(path.name, os.getpid()))
    descriptor = os.open(str(temporary), os.O_WRONLY | os.O_CREAT | os.O_EXCL, 0o600)
    try:
        with os.fdopen(descriptor, "wb") as handle:
            handle.write(encoded)
            handle.flush()
            os.fsync(handle.fileno())
        os.link(str(temporary), str(path))
    finally:
        try:
            temporary.unlink()
        except FileNotFoundError:
            pass


def read_json(path):
    with path.open("r", encoding="utf-8") as handle:
        return json.load(handle)


def validate_automation_profile():
    import qrenderdoc

    config_environment = "FORGE_GATE1_RENDERDOC_PROFILE_CONFIG"
    expected_path = Path(required_environment(config_environment)).resolve(strict=True)
    actual_path = Path(str(qrenderdoc.ConfigFilePath("UI.config"))).resolve(strict=True)
    if os.path.normcase(str(actual_path)) != os.path.normcase(str(expected_path)):
        raise AdapterError("QRenderDoc did not load the governed disposable UI.config")
    # QRenderDoc expands and saves its configuration before executing Python.
    # The host records the seed hash; here validate the loaded path and policy,
    # not byte equality with the pre-launch seed.
    config = read_json(actual_path)
    if (
        not isinstance(config, dict)
        or type(config.get("rdocConfigData")) is not int
        or config.get("rdocConfigData") != 1
        or config.get("Analytics_TotalOptOut") is not True
    ):
        raise AdapterError("QRenderDoc disposable UI.config does not enforce analytics opt-out")
    os.environ.pop(config_environment, None)


def result_succeeded(result):
    return getattr(result, "code", result) == rd.ResultCode.Succeeded


def result_description(result):
    message = getattr(result, "Message", None)
    return str(message()) if callable(message) else str(result)


def has_flag(flags, flag):
    return bool(flags & flag)


def validate_ready(document, nonce, source_commit, target_pid, weather):
    expect_exact_fields(document, READY_FIELDS, "runtime ready receipt")
    if document["schema"] != "forge.gate1-renderdoc-ready":
        raise AdapterError("runtime ready receipt schema mismatch")
    expect_uint64(document["schema_version"], "runtime ready schema version")
    if (
        document["schema_version"] != 1
        or document["status"]
        != "ready_frozen_pending_external_capture_signal"
    ):
        raise AdapterError("runtime ready receipt status or schema version mismatch")
    if document["nonce"] != nonce:
        raise AdapterError("runtime ready nonce does not match the acquisition nonce")
    expect_uint64(document["process_id"], "runtime ready process id")
    if document["process_id"] != target_pid:
        raise AdapterError("runtime ready PID does not match RenderDoc target control")
    if document["source_commit"] != source_commit:
        raise AdapterError("runtime ready commit does not match the requested exact commit")
    validate_source_commit(document["source_commit"], "runtime ready source commit")
    if document["build_profile"] != "release":
        raise AdapterError("runtime ready receipt is not from a release build")
    if document["required_backend"] != "Vulkan":
        raise AdapterError("runtime ready receipt does not require Vulkan")
    if document["required_renderdoc_version"] != REQUIRED_RENDERDOC_VERSION:
        raise AdapterError("runtime ready receipt requires a different RenderDoc version")
    expect_uint64(document["freeze_settle_frames"], "runtime freeze settle frames")
    if document["freeze_settle_frames"] != 30 or document["virtual_time_paused"] is not True:
        raise AdapterError("runtime ready receipt does not prove the governed frozen settle")
    expect_uint64(document["ready_frame"], "runtime ready frame")
    validate_sha256(document["frozen_state_sha256"], "runtime frozen-state hash")

    frozen = expect_exact_fields(
        document["frozen_state"], FROZEN_STATE_FIELDS, "runtime frozen state"
    )
    sources = expect_exact_fields(
        frozen["sources"], SOURCE_FIELDS, "runtime frozen-state sources"
    )
    for name in SOURCE_FIELDS - {"cadle_assets_used"}:
        validate_sha256(sources[name], "runtime source hash {}".format(name))
    if sources["cadle_assets_used"] is not False:
        raise AdapterError("runtime ready receipt does not exclude Cadle assets")

    seeds = expect_exact_fields(
        frozen["seeds"], SEED_FIELDS, "runtime frozen-state seeds"
    )
    for name in SEED_FIELDS:
        expect_uint64(seeds[name], "runtime source seed {}".format(name))

    tick = expect_exact_fields(frozen["tick"], TICK_FIELDS, "runtime frozen-state tick")
    for name in TICK_FIELDS:
        expect_uint64(tick[name], "runtime frozen-state tick {}".format(name))
    if (
        tick["virtual_elapsed_nanoseconds"] != 0
        or tick["fixed_elapsed_nanoseconds"] != 0
        or tick["fixed_tick"] != 0
        or tick["fixed_timestep_nanoseconds"] == 0
    ):
        raise AdapterError("runtime ready receipt is not frozen at deterministic tick zero")
    if frozen["vsync_requested"] is not False:
        raise AdapterError("runtime ready receipt does not prove the AutoNoVsync request")
    if frozen["expected_weather"] != weather:
        raise AdapterError("runtime ready weather does not match the requested weather")
    expected_weather_target = "Clear" if weather == "clear" else "Rain"
    if frozen["weather_target"] != expected_weather_target:
        raise AdapterError("runtime ready weather target does not match the request")
    for name in ("weather_wetness_current_bits", "weather_wetness_target_bits"):
        expect_uint64(frozen[name], "runtime frozen-state {}".format(name))
    if frozen["weather_wetness_current_bits"] != frozen["weather_wetness_target_bits"]:
        raise AdapterError("runtime ready weather transition is not settled")
    if (
        frozen["quality_selection"] != "Fixed(High)"
        or frozen["quality_startup_tier"] != "High"
        or frozen["quality_current_tier"] != "High"
        or frozen["quality_transition_count"] != 0
    ):
        raise AdapterError("runtime ready quality is not fixed High")
    expect_uint64(
        frozen["quality_transition_count"], "runtime frozen-state quality transition count"
    )
    pipeline_total = expect_uint64(
        frozen["pipeline_total"], "runtime frozen-state pipeline_total"
    )
    pipeline_ready = expect_uint64(
        frozen["pipeline_ready"], "runtime frozen-state pipeline_ready"
    )
    if pipeline_total == 0 or pipeline_ready != pipeline_total:
        raise AdapterError("runtime ready pipelines are not completely ready")

    camera = expect_exact_fields(
        frozen["camera"], CAMERA_FIELDS, "runtime frozen-state camera"
    )
    for vector_name in ("translation_m", "forward"):
        vector = camera[vector_name]
        if not isinstance(vector, list) or len(vector) != 3:
            raise AdapterError("runtime camera {} is not a three-vector".format(vector_name))
        for index, component in enumerate(vector):
            expect_finite_number(
                component, "runtime camera {}[{}]".format(vector_name, index)
            )
    if camera["active"] is not True:
        raise AdapterError("runtime ready camera is not active")
    if expect_finite_number(camera["vertical_fov_degrees"], "runtime camera FOV") <= 0:
        raise AdapterError("runtime ready camera FOV is not positive")

    window = expect_exact_fields(
        frozen["window"], WINDOW_FIELDS, "runtime frozen-state window"
    )
    expect_uint64(window["physical_width"], "runtime window physical width")
    expect_uint64(window["physical_height"], "runtime window physical height")
    if (
        window["physical_width"] != 1920
        or window["physical_height"] != 1080
        or window["visible"] is not True
    ):
        raise AdapterError("runtime ready window is not visible 1920x1080")
    if expect_finite_number(window["scale_factor"], "runtime window scale factor") <= 0:
        raise AdapterError("runtime window scale factor is not positive")
    if not isinstance(window["visible"], bool) or not isinstance(window["resizable"], bool):
        raise AdapterError("runtime window visibility/resizable state is not boolean")

    gpu = expect_exact_fields(frozen["gpu"], GPU_FIELDS, "runtime frozen-state GPU")
    if gpu["backend"] != "Vulkan":
        raise AdapterError("runtime ready GPU identity is not Vulkan")
    for name in ("name", "device_type"):
        expect_nonempty_string(gpu[name], "runtime GPU {}".format(name))
    for name in ("driver", "driver_info"):
        if not isinstance(gpu[name], str):
            raise AdapterError("runtime GPU {} is not a string".format(name))
    if not gpu["driver"] and not gpu["driver_info"]:
        raise AdapterError("runtime ready GPU identity has no driver information")
    for name in GPU_FIELDS - {
        "name",
        "device_type",
        "driver",
        "driver_info",
        "backend",
    }:
        expect_uint64(gpu[name], "runtime GPU {}".format(name))

    scene_hashes = expect_exact_fields(
        frozen["governed_scene_hashes"],
        GOVERNED_SCENE_HASH_FIELDS,
        "runtime governed-scene hashes",
    )
    for name in GOVERNED_SCENE_HASH_FIELDS:
        validate_sha256(scene_hashes[name], "runtime governed-scene hash {}".format(name))

    files = expect_exact_fields(document["files"], FILE_CONTRACT_FIELDS, "runtime file contract")
    expected_files = {
        "ready": READY_FILE,
        "signal": SIGNAL_FILE,
        "ledger": LEDGER_FILE,
        "failure": RUNTIME_FAILURE_FILE,
    }
    if files != expected_files:
        raise AdapterError("runtime ready file contract mismatch")

    qualification = expect_exact_fields(
        document["qualification"], QUALIFICATION_FIELDS, "runtime ready qualification"
    )
    for field in (
        "runtime_state_qualified",
        "external_capture_observed_by_runtime",
        "renderdoc_analysis_completed_by_runtime",
        "gate1_route_qualified",
    ):
        if qualification.get(field) is not False:
            raise AdapterError("runtime ready receipt crosses its qualification boundary")
    expect_nonempty_string(qualification["limitation"], "runtime qualification limitation")


def wait_for_ready(output_dir, target, nonce, source_commit, weather):
    ready_path = output_dir / READY_FILE
    failure_path = output_dir / RUNTIME_FAILURE_FILE
    deadline = time.monotonic() + READY_TIMEOUT_SECONDS
    while time.monotonic() < deadline:
        if failure_path.is_file():
            raise AdapterError(
                "runtime failed before readiness: {}".format(
                    failure_path.read_text(encoding="utf-8", errors="replace").strip()
                )
            )
        if ready_path.is_file():
            document = read_json(ready_path)
            validate_ready(document, nonce, source_commit, int(target.GetPID()), weather)
            # RegisterAPI is delivered asynchronously. GetAPI remains empty until
            # ReceiveMessage processes the first presenting API notification.
            target_api = str(target.GetAPI())
            if target_api == "Vulkan":
                return ready_path, document
            if target_api:
                raise AdapterError("RenderDoc target API is not Vulkan: {}".format(target_api))
        if not target.Connected():
            raise AdapterError("RenderDoc target disconnected before runtime readiness")
        message = target.ReceiveMessage(None)
        if message.type == rd.TargetControlMessageType.NewCapture:
            raise AdapterError("RenderDoc produced a capture before the governed signal")
        time.sleep(0.02)
    raise AdapterError("runtime ready receipt or presenting Vulkan API timed out")


def action_flags(action):
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


def topology_name(topology):
    for name in ("TriangleList", "LineList", "PointList"):
        if topology == getattr(rd.Topology, name):
            return name
    text = str(topology)
    return text.rsplit(".", 1)[-1]


def active_shader_stages(pipeline):
    stages = []
    # This is called only for draw leaves. Vulkan retains a separately bound
    # compute pipeline; its reflection does not describe stages of this draw.
    candidates = (
        ("Vertex", "Vertex"),
        ("Hull", "TessellationControl"),
        ("Domain", "TessellationEvaluation"),
        ("Geometry", "Geometry"),
        ("Pixel", "Fragment"),
        ("Task", "Task"),
        ("Mesh", "Mesh"),
    )
    for renderdoc_name, normalized_name in candidates:
        stage = getattr(rd.ShaderStage, renderdoc_name, None)
        if stage is not None and pipeline.GetShaderReflection(stage) is not None:
            stages.append(normalized_name)
    return stages


def normalize_action(controller, structured_file, action):
    children = [
        normalize_action(controller, structured_file, child) for child in action.children
    ]
    flags = action_flags(action)
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
        topology = topology_name(pipeline.GetPrimitiveTopology())
        stages = active_shader_stages(pipeline)

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


def count_present_actions(actions):
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


def analyze_capture(capture_path, model_directory):
    capture = rd.OpenCaptureFile()
    controller = None
    try:
        opened = capture.OpenFile(str(capture_path), "", None)
        if not result_succeeded(opened):
            raise AdapterError(
                "RenderDoc could not open capture: {}".format(result_description(opened))
            )
        support = capture.LocalReplaySupport()
        if support != rd.ReplaySupport.Supported:
            raise AdapterError("capture local replay support is {}".format(support))
        replay_result, controller = capture.OpenCapture(rd.ReplayOptions(), None)
        if not result_succeeded(replay_result) or controller is None:
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
                "present_count": count_present_actions(roots),
                "local_replay_support": "Supported",
                "pipeline_type": "Vulkan",
                "degraded": False,
            },
            "actions": [
                normalize_action(controller, structured, action) for action in roots
            ],
        }
    finally:
        if controller is not None:
            controller.Shutdown()
        capture.Shutdown()

    sys.path.insert(0, str(model_directory))
    try:
        from gate1_renderdoc_model import analyze_document

        analysis = analyze_document(document)
    finally:
        del sys.path[0]
    return document, analysis


def receive_capture_and_ledger(target, output_dir):
    runtime_failure = output_dir / RUNTIME_FAILURE_FILE
    ledger_path = output_dir / LEDGER_FILE
    captures = []
    deadline = time.monotonic() + CAPTURE_TIMEOUT_SECONDS
    while time.monotonic() < deadline:
        if runtime_failure.is_file():
            raise AdapterError(
                "runtime failed during capture: {}".format(
                    runtime_failure.read_text(encoding="utf-8", errors="replace").strip()
                )
            )
        if target.Connected():
            message = target.ReceiveMessage(None)
            if message.type == rd.TargetControlMessageType.NewCapture:
                capture = message.newCapture
                # Copy SWIG-backed values while the target-control connection is
                # alive.  The returned document must remain valid after Shutdown.
                captures.append(
                    {
                        "capture_id": int(capture.captureId),
                        "frame_number": int(capture.frameNumber),
                        "byte_size": int(capture.byteSize),
                        "path": str(capture.path),
                        "api": str(capture.api),
                        "local": bool(capture.local),
                    }
                )
                if len(captures) > 1:
                    raise AdapterError("RenderDoc produced more than one capture")
            elif message.type == rd.TargetControlMessageType.Noop:
                time.sleep(0.01)
        elif not ledger_path.is_file():
            raise AdapterError("RenderDoc target disconnected before the runtime ledger")
        if ledger_path.is_file() and len(captures) == 1:
            return captures[0], ledger_path
    raise AdapterError("capture message or runtime ledger timed out")


def validate_runtime_ledger(
    document,
    nonce,
    source_commit,
    ready_sha256,
    capture_id,
    target_pid,
    ready_document,
    signal_document,
):
    expect_exact_fields(document, RUNTIME_LEDGER_FIELDS, "runtime ledger")
    if document.get("schema") != "forge.gate1-renderdoc-runtime":
        raise AdapterError("runtime ledger schema mismatch")
    expect_uint64(document.get("schema_version"), "runtime ledger schema version")
    if (
        document.get("schema_version") != 1
        or document.get("status")
        != "stable_capture_window_completed_pending_external_capture_analysis"
    ):
        raise AdapterError("runtime ledger status or schema version mismatch")
    if document.get("nonce") != nonce or document.get("source_commit") != source_commit:
        raise AdapterError("runtime ledger nonce or commit mismatch")
    expect_uint64(document.get("process_id"), "runtime ledger process id")
    if document.get("process_id") != target_pid:
        raise AdapterError("runtime ledger PID does not match RenderDoc target control")
    if document.get("build_profile") != "release":
        raise AdapterError("runtime ledger is not from a release build")
    expect_uint64(document.get("ready_frame"), "runtime ledger ready frame")
    if document.get("ready_frame") != ready_document["ready_frame"]:
        raise AdapterError("runtime ledger ready frame changed")
    expect_uint64(document.get("signal_frame"), "runtime ledger signal frame")
    if document["signal_frame"] < document["ready_frame"]:
        raise AdapterError("runtime ledger signal frame precedes the ready frame")
    if document.get("ready_sha256") != ready_sha256:
        raise AdapterError("runtime ledger ready SHA-256 mismatch")
    if document.get("capture_id") != capture_id:
        raise AdapterError("runtime ledger capture identifier mismatch")
    expect_uint64(
        document.get("capture_window_frames"), "runtime ledger capture window frames"
    )
    if document.get("capture_window_frames") != 120:
        raise AdapterError("runtime ledger did not preserve the full capture window")
    if document.get("frozen_state_stable") is not True:
        raise AdapterError("runtime ledger reports an unstable frozen state")
    if document.get("renderdoc") != signal_document["renderdoc"]:
        raise AdapterError("runtime ledger RenderDoc signal changed")
    if document.get("frozen_state_sha256") != ready_document["frozen_state_sha256"]:
        raise AdapterError("runtime ledger frozen-state SHA-256 changed")
    if document.get("frozen_state_initial") != ready_document["frozen_state"]:
        raise AdapterError("runtime ledger initial frozen state changed")
    if document.get("frozen_state_final") != ready_document["frozen_state"]:
        raise AdapterError("runtime ledger final frozen state changed")
    if document.get("files") != ready_document["files"]:
        raise AdapterError("runtime ledger file contract changed")
    qualification = expect_exact_fields(
        document["qualification"], QUALIFICATION_FIELDS, "runtime ledger qualification"
    )
    if qualification.get("runtime_state_qualified") is not True:
        raise AdapterError("runtime ledger did not qualify its owned state")
    for field in (
        "external_capture_observed_by_runtime",
        "renderdoc_analysis_completed_by_runtime",
        "gate1_route_qualified",
    ):
        if qualification.get(field) is not False:
            raise AdapterError("runtime ledger improperly claims external qualification")
    expect_nonempty_string(qualification["limitation"], "runtime qualification limitation")


def main():
    runtime_path = Path(required_environment("FORGE_GATE1_RENDERDOC_RUNTIME")).resolve(
        strict=True
    )
    repository_root = Path(
        required_environment("FORGE_GATE1_RENDERDOC_REPO_ROOT")
    ).resolve(strict=True)
    output_dir = Path(required_environment("FORGE_GATE1_RENDERDOC_OUTPUT_DIR")).resolve(
        strict=True
    )
    nonce = required_environment("FORGE_GATE1_RENDERDOC_NONCE")
    weather = required_environment("FORGE_GATE1_RENDERDOC_WEATHER")
    source_commit = required_environment("FORGE_GATE1_RENDERDOC_SOURCE_COMMIT")
    qrenderdoc_sha256 = required_environment("FORGE_GATE1_RENDERDOC_QRENDERDOC_SHA256")
    renderdoccmd_sha256 = required_environment(
        "FORGE_GATE1_RENDERDOC_RENDERDOCCMD_SHA256"
    )
    renderdoc_dll_sha256 = required_environment(
        "FORGE_GATE1_RENDERDOC_RENDERDOC_DLL_SHA256"
    )
    capture_id = required_environment("FORGE_GATE1_RENDERDOC_CAPTURE_ID")
    if weather not in ("clear", "rain"):
        raise AdapterError("weather must be clear or rain")
    if len(nonce) != 32 or any(character not in "0123456789abcdef" for character in nonce):
        raise AdapterError("nonce must be 32 lowercase hexadecimal characters")
    validate_source_commit(source_commit, "requested source commit")
    validate_capture_id(capture_id)
    validate_sha256(qrenderdoc_sha256, "qrenderdoc SHA-256")
    validate_sha256(renderdoccmd_sha256, "renderdoccmd SHA-256")
    validate_sha256(renderdoc_dll_sha256, "renderdoc.dll SHA-256")
    validate_automation_profile()

    command_line = subprocess.list2cmdline(
        [
            "--visual-valley",
            "--quality",
            "high",
            "--weather",
            weather,
            "--gate1-renderdoc-capture",
            str(output_dir),
            "--gate1-renderdoc-nonce",
            nonce,
            "--no-vsync",
        ]
    )
    options = rd.CaptureOptions()
    options.allowVSync = False
    options.allowFullscreen = False
    options.apiValidation = False
    options.captureCallstacks = False
    options.hookIntoChildren = False
    options.refAllResources = False
    launched = rd.ExecuteAndInject(
        str(runtime_path),
        str(repository_root),
        command_line,
        [],
        str(output_dir / "gate1-renderdoc-frame"),
        options,
        False,
    )
    if not result_succeeded(launched.result) or int(launched.ident) == 0:
        raise AdapterError(
            "RenderDoc launch/injection failed: {}".format(
                result_description(launched.result)
            )
        )

    target = rd.CreateTargetControl(
        "", int(launched.ident), "OpenRPGForgeGate1", True
    )
    if target is None:
        raise AdapterError("RenderDoc target control connection failed")
    try:
        target_pid = int(target.GetPID())
        ready_path, ready_document = wait_for_ready(
            output_dir, target, nonce, source_commit, weather
        )
        target_api = str(target.GetAPI())
        if target_api != "Vulkan":
            raise AdapterError("RenderDoc target API is not Vulkan: {}".format(target_api))
        ready_sha256 = sha256_file(ready_path)

        target.TriggerCapture(1)
        signal = {
            "schema": "forge.gate1-renderdoc-capture-signal",
            "schema_version": 1,
            "nonce": nonce,
            "process_id": target_pid,
            "source_commit": source_commit,
            "ready_sha256": ready_sha256,
            "capture_id": capture_id,
            "renderdoc": {
                "capture_triggered": True,
                "target_control_ident": int(launched.ident),
                "target_api": "Vulkan",
                "tool_version": REQUIRED_RENDERDOC_VERSION,
                "qrenderdoc_sha256": qrenderdoc_sha256,
                "renderdoccmd_sha256": renderdoccmd_sha256,
                "renderdoc_dll_sha256": renderdoc_dll_sha256,
            },
        }
        expect_exact_fields(signal, SIGNAL_FIELDS, "capture signal")
        expect_exact_fields(
            signal["renderdoc"], RENDERDOC_SIGNAL_FIELDS, "capture signal RenderDoc identity"
        )
        signal_path = output_dir / SIGNAL_FILE
        write_json_atomic(signal_path, signal)
        capture_message, ledger_path = receive_capture_and_ledger(target, output_dir)
    finally:
        target.Shutdown()

    if not capture_message["local"]:
        raise AdapterError("RenderDoc capture is not local")
    if capture_message["api"] not in ("", "Vulkan"):
        raise AdapterError(
            "RenderDoc capture message API is not Vulkan: {}".format(
                capture_message["api"]
            )
        )
    capture_path = Path(capture_message["path"]).resolve(strict=True)
    try:
        capture_path.relative_to(output_dir)
    except ValueError as error:
        raise AdapterError("RenderDoc capture escaped the governed output directory") from error
    capture_files = list(output_dir.glob("*.rdc"))
    if len(capture_files) != 1 or capture_files[0].resolve() != capture_path:
        raise AdapterError("governed output does not contain exactly the announced capture")
    if capture_path.stat().st_size != capture_message["byte_size"]:
        raise AdapterError("RenderDoc capture byte size does not match the announced capture")

    ledger = read_json(ledger_path)
    validate_runtime_ledger(
        ledger,
        nonce,
        source_commit,
        ready_sha256,
        capture_id,
        target_pid,
        ready_document,
        signal,
    )
    action_document, analysis = analyze_capture(
        capture_path, repository_root / "scripts" / "renderdoc"
    )
    action_path = output_dir / ACTION_DOCUMENT_FILE
    analysis_path = output_dir / ANALYSIS_FILE
    write_json_atomic(action_path, action_document)
    write_json_atomic(analysis_path, analysis)

    receipt = {
        "schema": ADAPTER_RECEIPT_SCHEMA,
        "status": "PASS",
        "source_commit": source_commit,
        "nonce": nonce,
        "weather": weather,
        "capture_id": capture_id,
        "target": {
            "process_id": int(ledger["process_id"]),
            "target_control_ident": int(launched.ident),
            "api": "Vulkan",
        },
        "capture": {
            "path": capture_path.name,
            "sha256": sha256_file(capture_path),
            "renderdoc_capture_id": capture_message["capture_id"],
            "frame_number": capture_message["frame_number"],
            "byte_size": capture_message["byte_size"],
            "captured_frame_count": 1,
        },
        "files": {
            READY_FILE: sha256_file(ready_path),
            SIGNAL_FILE: sha256_file(output_dir / SIGNAL_FILE),
            LEDGER_FILE: sha256_file(ledger_path),
            ACTION_DOCUMENT_FILE: sha256_file(action_path),
            ANALYSIS_FILE: sha256_file(analysis_path),
        },
        "qualification": {
            "runtime_state_qualified": True,
            "external_capture_observed": True,
            "renderdoc_analysis_completed": True,
            "renderdoc_metrics_are_fps_evidence": False,
        },
    }
    expect_exact_fields(receipt, ADAPTER_RECEIPT_FIELDS, "adapter receipt")
    expect_exact_fields(receipt["target"], ADAPTER_TARGET_FIELDS, "adapter receipt target")
    expect_exact_fields(receipt["capture"], ADAPTER_CAPTURE_FIELDS, "adapter receipt capture")
    expect_exact_fields(receipt["files"], ADAPTER_FILE_FIELDS, "adapter receipt files")
    expect_exact_fields(
        receipt["qualification"],
        ADAPTER_QUALIFICATION_FIELDS,
        "adapter receipt qualification",
    )
    write_json_atomic(output_dir / ADAPTER_RECEIPT_FILE, receipt)


exit_code = 0
try:
    main()
except Exception as error:  # qrenderdoc converts SystemExit into process termination.
    exit_code = 1
    traceback.print_exc()
    output_value = os.environ.get("FORGE_GATE1_RENDERDOC_OUTPUT_DIR", "")
    if output_value:
        try:
            failure_path = Path(output_value) / ADAPTER_FAILURE_FILE
            write_json_atomic(
                failure_path,
                {
                    "schema": "forge-gate1-renderdoc-adapter-failure/v1",
                    "status": "FAIL",
                    "error_type": type(error).__name__,
                    "message": str(error),
                },
            )
        except Exception:
            traceback.print_exc()

raise SystemExit(exit_code)
