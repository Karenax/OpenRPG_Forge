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
READY_TIMEOUT_SECONDS = 95.0
CAPTURE_TIMEOUT_SECONDS = 45.0


class AdapterError(RuntimeError):
    """A fail-closed acquisition or replay error."""


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


def result_succeeded(result):
    return getattr(result, "code", result) == rd.ResultCode.Succeeded


def result_description(result):
    message = getattr(result, "Message", None)
    return str(message()) if callable(message) else str(result)


def has_flag(flags, flag):
    return bool(flags & flag)


def validate_ready(document, nonce, source_commit, target_pid):
    required = {
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
    if set(document) != required:
        raise AdapterError("runtime ready receipt has an unexpected field set")
    if document["schema"] != "forge.gate1-renderdoc-ready":
        raise AdapterError("runtime ready receipt schema mismatch")
    if (
        document["schema_version"] != 1
        or document["status"]
        != "ready_frozen_pending_external_capture_signal"
    ):
        raise AdapterError("runtime ready receipt status or schema version mismatch")
    if document["nonce"] != nonce:
        raise AdapterError("runtime ready nonce does not match the acquisition nonce")
    if document["process_id"] != target_pid:
        raise AdapterError("runtime ready PID does not match RenderDoc target control")
    if document["source_commit"] != source_commit:
        raise AdapterError("runtime ready commit does not match the requested exact commit")
    if document["build_profile"] != "release":
        raise AdapterError("runtime ready receipt is not from a release build")
    if document["required_backend"] != "Vulkan":
        raise AdapterError("runtime ready receipt does not require Vulkan")
    if document["required_renderdoc_version"] != REQUIRED_RENDERDOC_VERSION:
        raise AdapterError("runtime ready receipt requires a different RenderDoc version")
    if document["freeze_settle_frames"] != 30 or not document["virtual_time_paused"]:
        raise AdapterError("runtime ready receipt does not prove the governed frozen settle")
    frozen = document["frozen_state"]
    if not isinstance(frozen, dict):
        raise AdapterError("runtime ready frozen_state is not an object")
    if frozen.get("gpu", {}).get("backend") != "Vulkan":
        raise AdapterError("runtime ready GPU identity is not Vulkan")
    if frozen.get("sources", {}).get("cadle_assets_used") is not False:
        raise AdapterError("runtime ready receipt does not exclude Cadle assets")
    qualification = document["qualification"]
    for field in (
        "runtime_state_qualified",
        "external_capture_observed_by_runtime",
        "renderdoc_analysis_completed_by_runtime",
        "gate1_route_qualified",
    ):
        if qualification.get(field) is not False:
            raise AdapterError("runtime ready receipt crosses its qualification boundary")


def wait_for_ready(output_dir, target, nonce, source_commit):
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
            validate_ready(document, nonce, source_commit, int(target.GetPID()))
            return ready_path, document
        if not target.Connected():
            raise AdapterError("RenderDoc target disconnected before runtime readiness")
        message = target.ReceiveMessage(None)
        if message.type == rd.TargetControlMessageType.NewCapture:
            raise AdapterError("RenderDoc produced a capture before the governed signal")
        time.sleep(0.02)
    raise AdapterError("runtime ready receipt timed out")


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


def normalize_action(controller, structured_file, action):
    children = [
        normalize_action(controller, structured_file, child) for child in action.children
    ]
    flags = action_flags(action)
    is_draw = "Drawcall" in flags
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
        "indirect": is_indirect,
        "indirect_resolved": indirect_resolved,
        "active_stages": stages,
        "children": children,
    }


def count_present_events(actions):
    present_events = set()

    def visit(action):
        if has_flag(action.flags, rd.ActionFlags.Present):
            present_events.add(int(action.eventId))
        for child in action.children:
            visit(child)

    for action in actions:
        visit(action)
    return len(present_events)


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
            "schema": "forge-gate1-renderdoc-actions/v1",
            "capture": {
                "captured_frame_count": 1,
                "present_count": count_present_events(roots),
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


def validate_runtime_ledger(document, nonce, source_commit, ready_sha256, capture_id):
    if document.get("schema") != "forge.gate1-renderdoc-runtime":
        raise AdapterError("runtime ledger schema mismatch")
    if (
        document.get("schema_version") != 1
        or document.get("status")
        != "stable_capture_window_completed_pending_external_capture_analysis"
    ):
        raise AdapterError("runtime ledger status or schema version mismatch")
    if document.get("nonce") != nonce or document.get("source_commit") != source_commit:
        raise AdapterError("runtime ledger nonce or commit mismatch")
    if document.get("ready_sha256") != ready_sha256:
        raise AdapterError("runtime ledger ready SHA-256 mismatch")
    if document.get("capture_id") != capture_id:
        raise AdapterError("runtime ledger capture identifier mismatch")
    if document.get("capture_window_frames") != 120:
        raise AdapterError("runtime ledger did not preserve the full capture window")
    if document.get("frozen_state_stable") is not True:
        raise AdapterError("runtime ledger reports an unstable frozen state")
    qualification = document.get("qualification", {})
    if qualification.get("runtime_state_qualified") is not True:
        raise AdapterError("runtime ledger did not qualify its owned state")
    if qualification.get("gate1_route_qualified") is not False:
        raise AdapterError("runtime ledger improperly claims external qualification")


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
    capture_id = required_environment("FORGE_GATE1_RENDERDOC_CAPTURE_ID")
    if weather not in ("clear", "rain"):
        raise AdapterError("weather must be clear or rain")
    if len(nonce) != 32 or any(character not in "0123456789abcdef" for character in nonce):
        raise AdapterError("nonce must be 32 lowercase hexadecimal characters")

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
        ready_path, _ = wait_for_ready(
            output_dir, target, nonce, source_commit
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
            "process_id": int(target.GetPID()),
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
            },
        }
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

    ledger = read_json(ledger_path)
    validate_runtime_ledger(ledger, nonce, source_commit, ready_sha256, capture_id)
    action_document, analysis = analyze_capture(
        capture_path, repository_root / "scripts" / "renderdoc"
    )
    action_path = output_dir / ACTION_DOCUMENT_FILE
    analysis_path = output_dir / ANALYSIS_FILE
    write_json_atomic(action_path, action_document)
    write_json_atomic(analysis_path, analysis)

    receipt = {
        "schema": "forge-gate1-renderdoc-adapter/v1",
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
