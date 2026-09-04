"""Offline RenderDoc 1.46 extraction of already-qualified native High frames.

No runtime, capture, performance measurement, registry write, or route verdict.
The launcher owns the disposable QRenderDoc profile and checks all input hashes.
"""

import hashlib
import json
import math
import os
from pathlib import Path
import sys
import traceback

# Set before qrenderdoc imports PySide2/shiboken2: the embedded interpreter
# does not honor the launcher's PYTHONDONTWRITEBYTECODE environment variable.
sys.dont_write_bytecode = True

import qrenderdoc
import renderdoc as rd


def sha256(path):
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1048576), b""):
            digest.update(chunk)
    return digest.hexdigest()


def primitive_fields(obj):
    """Only actual scalar values, never SWIG addresses or unstable repr strings."""
    result = {}
    for key in dir(obj):
        if key.startswith("_") or key in ("this", "thisown"):
            continue
        value = getattr(obj, key)
        if isinstance(value, (str, int, float, bool)):
            result[key] = value
    return result


def descriptor(value):
    return {
        "resource": str(value.resource),
        "view": str(value.view),
        "byte_offset": int(value.byteOffset),
        "byte_size": int(value.byteSize),
        "first_mip": int(value.firstMip),
        "num_mips": int(value.numMips),
        "first_slice": int(value.firstSlice),
        "num_slices": int(value.numSlices),
    }


def variable(value):
    if value.members:
        return {"name": str(value.name), "members": [variable(x) for x in value.members]}
    storage = {rd.VarType.Float: "f32v", rd.VarType.UInt: "u32v", rd.VarType.SInt: "s32v"}.get(value.type)
    if storage is None:
        raise ValueError("Unsupported constant type: " + str(value.type))
    return {
        "name": str(value.name), "type": str(value.type),
        "rows": int(value.rows), "columns": int(value.columns),
        # Projection/light buffers may legitimately contain infinity. Preserve
        # its spelling, not a fabricated finite value; raw_hex is authoritative.
        "values": [str(x) if isinstance(x, float) and not math.isfinite(x) else x
                   for x in list(getattr(value.value, storage))[:value.rows * value.columns]],
    }


def layout(value):
    return {
        "name": str(value.name), "byte_offset": int(value.byteOffset),
        "rows": int(value.type.rows), "columns": int(value.type.columns),
        "members": [layout(x) for x in value.type.members],
    }


def classify(reflection, vertex):
    blocks = reflection.constantBlocks
    # These layouts identify candidates in this pinned Bevy 0.19.1 build.
    # The report additionally maps their captured SPIR-V instructions to WGSL.
    has_forward_lights = any(b.byteSize == 4576 for b in blocks)
    if has_forward_lights:
        for block in blocks:
            if block.fixedBindSetOrSpace == 3 and block.fixedBindNumber == 13:
                return {80: "water", 112: "terrain", 160: "vegetation"}.get(block.byteSize)
    if reflection.entryPoint in ("downsample_first", "downsample", "upsample"):
        return "bloom"
    if (vertex and vertex.entryPoint == "fullscreen_vertex_shader"
            and len(blocks) == 1 and blocks[0].byteSize == 784
            and blocks[0].fixedBindSetOrSpace == 0 and blocks[0].fixedBindNumber == 0):
        return "tonemapping"
    return None


def main(output):
    expected_profile = Path(os.environ["FORGE_GATE1_RENDERDOC_PROFILE_CONFIG"]).resolve(strict=True)
    actual_profile = Path(str(qrenderdoc.ConfigFilePath("UI.config"))).resolve(strict=True)
    if expected_profile != actual_profile or json.loads(actual_profile.read_text())["Analytics_TotalOptOut"] is not True:
        raise ValueError("Disposable opt-out profile mismatch")
    path = Path(os.environ["FORGE_MATERIAL_CAPTURE"]).resolve(strict=True)
    parent_path = Path(os.environ["FORGE_MATERIAL_PARENT_RECEIPT"]).resolve(strict=True)
    parent = json.loads(parent_path.read_text(encoding="utf-8-sig"))
    capture_hash = sha256(path)
    members = [r for r in parent["results"] if r["capture_sha256"] == capture_hash
               and (parent_path.parent / r["capture_file"]).resolve(strict=True) == path]
    if parent["status"] != "PASS" or len(members) != 1:
        raise ValueError("Capture is not in the passing R1-037 receipt")
    ledger_path = path.parent / "gate1-renderdoc.runtime.json"
    if sha256(ledger_path) != members[0]["ledger_sha256"]:
        raise ValueError("Runtime ledger hash mismatch")
    ledger = json.loads(ledger_path.read_text(encoding="utf-8-sig"))
    frozen = ledger["frozen_state_initial"]
    capture = rd.OpenCaptureFile()
    controller = None
    try:
        result = capture.OpenFile(str(path), "", None)
        if result.code != rd.ResultCode.Succeeded or capture.LocalReplaySupport() != rd.ReplaySupport.Supported:
            raise ValueError("Capture cannot be replayed locally: " + str(result))
        result, controller = capture.OpenCapture(rd.ReplayOptions(), None)
        if result.code != rd.ResultCode.Succeeded or controller is None:
            raise ValueError("Replay failed: " + str(result))
        api = controller.GetAPIProperties()
        if api.pipelineType != rd.GraphicsAPI.Vulkan or api.degraded:
            raise ValueError("Expected non-degraded Vulkan replay")
        document = {
            "schema": "forge-gate1-material-replay/v1",
            "status": "extracted_pending_bound_state_validation",
            "capture": {"file": str(path), "sha256": capture_hash,
                        "run": members[0]["run"], "weather": members[0]["weather"],
                        "quality": frozen["quality_current_tier"], "window": frozen["window"],
                        "runtime_ledger_sha256": members[0]["ledger_sha256"],
                        "parent_receipt_sha256": sha256(parent_path),
                        "runtime_source_commit": parent["source_commit"],
                        "pipeline": "Vulkan", "degraded": False},
            "draws": [], "candidate_events": {}, "textures": [],
            "qualification": {"pixel_debug_performed": False, "human_review_performed": False,
                              "same_process_weather_reversal": False, "browser_observed": False,
                              "routes_closed": [], "gate1_transition_authorized": False},
        }
        shader_cache = {}
        selected_roles = set()
        referenced_textures = set()
        def visit(action):
            if action.flags & rd.ActionFlags.Drawcall and not action.children:
                controller.SetFrameEvent(action.eventId, True)
                pipe = controller.GetPipelineState()
                pixel = pipe.GetShaderReflection(rd.ShaderStage.Pixel)
                vertex = pipe.GetShaderReflection(rd.ShaderStage.Vertex)
                role = classify(pixel, vertex) if pixel else None
                if role:
                    document["candidate_events"].setdefault(role, []).append(int(action.eventId))
                if role and (role == "bloom" or role not in selected_roles):
                    selected_roles.add(role)
                    pid = pipe.GetGraphicsPipelineObject()
                    shader_id = str(pixel.resourceId)
                    if shader_id not in shader_cache:
                        disassembly = controller.DisassembleShader(pid, pixel, "")
                        shader_file = "shader-" + shader_id.replace("ResourceId::", "") + ".txt"
                        (output / shader_file).write_text(disassembly, encoding="utf-8")
                        shader_cache[shader_id] = {"file": shader_file, "sha256": sha256(output / shader_file)}
                    draw = {"role": role, "event_id": int(action.eventId), "pipeline": str(pid),
                            "shader_id": shader_id, "shader": shader_cache[shader_id],
                            "entry": str(pixel.entryPoint), "indices": int(action.numIndices),
                            "instances": int(action.numInstances), "constant_blocks": [],
                            "resources": [], "outputs": [], "blend": {}}
                    for index, block in enumerate(pixel.constantBlocks):
                        used = pipe.GetConstantBlock(rd.ShaderStage.Pixel, index, 0)
                        bound = used.descriptor
                        if not block.bufferBacked or used.access.staticallyUnused:
                            raise ValueError("Unexpected unbacked or unused constant block")
                        raw = bytes(controller.GetBufferData(bound.resource, bound.byteOffset, block.byteSize))
                        if len(raw) != block.byteSize:
                            raise ValueError("Truncated constant buffer")
                        values = controller.GetCBufferVariableContents(pid, pixel.resourceId, rd.ShaderStage.Pixel,
                            pixel.entryPoint, index, bound.resource, bound.byteOffset, block.byteSize)
                        draw["constant_blocks"].append({"index": index, "set": int(block.fixedBindSetOrSpace),
                            "binding": int(block.fixedBindNumber), "size": int(block.byteSize),
                            "bound": descriptor(bound), "raw_hex": raw.hex(),
                            "layout": [layout(x) for x in block.variables], "variables": [variable(x) for x in values]})
                    for used in pipe.GetReadOnlyResources(rd.ShaderStage.Pixel, True):
                        reflected = pixel.readOnlyResources[used.access.index]
                        item = {"set": int(reflected.fixedBindSetOrSpace), "binding": int(reflected.fixedBindNumber),
                                "array_index": int(used.access.arrayElement), "statically_unused": bool(used.access.staticallyUnused),
                                "bound": descriptor(used.descriptor)}
                        draw["resources"].append(item)
                        referenced_textures.add(item["bound"]["resource"])
                    draw["outputs"] = [descriptor(x) for x in pipe.GetOutputTargets() if str(x.resource) != "ResourceId::0"]
                    referenced_textures.update(x["resource"] for x in draw["outputs"])
                    blend = controller.GetVulkanPipelineState().colorBlend
                    draw["blend"] = {"factor": list(blend.blendFactor), "attachments": [
                        {"state": primitive_fields(x), "color": primitive_fields(x.colorBlend),
                         "alpha": primitive_fields(x.alphaBlend)} for x in blend.blends]}
                    document["draws"].append(draw)
            for child in action.children:
                visit(child)
        for root in controller.GetRootActions():
            visit(root)
        for texture in controller.GetTextures():
            if str(texture.resourceId) in referenced_textures:
                document["textures"].append({"resource": str(texture.resourceId), "width": int(texture.width),
                    "height": int(texture.height), "depth": int(texture.depth), "mips": int(texture.mips),
                    "array_size": int(texture.arraysize), "cubemap": bool(texture.cubemap),
                    "format": texture.format.Name(), "usage": [
                        {"event_id": int(u.eventId), "usage": str(u.usage)} for u in controller.GetUsage(texture.resourceId)]})
        (output / "material-replay.json").write_text(json.dumps(document, indent=2, allow_nan=False), encoding="utf-8")
    finally:
        if controller is not None:
            controller.Shutdown()
        capture.Shutdown()


output_path = Path(os.environ["FORGE_MATERIAL_OUTPUT"])
exit_code = 0
try:
    main(output_path)
except Exception:
    exit_code = 1
    (output_path / "failure.txt").write_text(traceback.format_exc(), encoding="utf-8")
sys.exit(exit_code)
