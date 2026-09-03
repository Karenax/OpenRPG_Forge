#!/usr/bin/env python3
"""Static cross-language contract tests for the governed R1-037 route."""

from __future__ import annotations

import ast
import copy
import enum
import hashlib
import json
import os
import re
import sys
import tempfile
import types
import unittest
from pathlib import Path
from unittest import mock

from gate1_renderdoc_model import (
    INPUT_SCHEMA,
    OUTPUT_SCHEMA,
    _ACTION_FIELDS,
    _CAPTURE_FIELDS,
    _ROOT_FIELDS,
    analyze_document,
)


_HERE = Path(__file__).resolve().parent
_REPOSITORY = _HERE.parents[1]
_MANIFEST_PATH = _HERE.parent / "gate1-renderdoc.v1.json"
_ADAPTER_PATH = _HERE / "gate1_renderdoc_qrenderdoc.py"
_ORCHESTRATOR_PATH = _HERE.parent / "Invoke-Gate1RenderDoc.ps1"
_RUNTIME_PATH = _REPOSITORY / "apps" / "forge_runtime" / "src" / "gate1_renderdoc_capture.rs"
_ENGINE_PATH = _REPOSITORY / "crates" / "forge_engine" / "src" / "lib.rs"


def _load_manifest() -> dict[str, object]:
    value = json.loads(_MANIFEST_PATH.read_text(encoding="utf-8"))
    if not isinstance(value, dict):
        raise AssertionError("RenderDoc manifest root must be an object")
    return value


def _python_set_assignment(source: str, name: str) -> set[str]:
    tree = ast.parse(source, filename=str(_ADAPTER_PATH))
    for statement in tree.body:
        if not isinstance(statement, ast.Assign):
            continue
        if not any(
            isinstance(target, ast.Name) and target.id == name
            for target in statement.targets
        ):
            continue
        value = ast.literal_eval(statement.value)
        if not isinstance(value, set) or any(not isinstance(item, str) for item in value):
            raise AssertionError(f"{name} must be a literal set of strings")
        return value
    raise AssertionError(f"missing Python contract set {name}")


def _rust_struct_fields(source: str, name: str) -> set[str]:
    match = re.search(
        rf"(?:pub(?:\([^)]*\))?\s+)?struct\s+{re.escape(name)}(?:<'[^>]+>)?\s*\{{(?P<body>.*?)^\}}",
        source,
        flags=re.MULTILINE | re.DOTALL,
    )
    if match is None:
        raise AssertionError(f"missing Rust struct {name}")
    return set(
        re.findall(
            r"^\s+(?:pub\s+)?([a-z][a-z0-9_]*)\s*:",
            match.group("body"),
            flags=re.MULTILINE,
        )
    )


def _adapter_validation_namespace() -> dict[str, object]:
    source = _ADAPTER_PATH.read_text(encoding="utf-8")
    tree = ast.parse(source, filename=str(_ADAPTER_PATH))
    assignments = {
        "READY_FILE",
        "SIGNAL_FILE",
        "LEDGER_FILE",
        "RUNTIME_FAILURE_FILE",
        "ACTION_DOCUMENT_FILE",
        "ANALYSIS_FILE",
        "REQUIRED_RENDERDOC_VERSION",
        "READY_FIELDS",
        "FROZEN_STATE_FIELDS",
        "SOURCE_FIELDS",
        "SEED_FIELDS",
        "TICK_FIELDS",
        "CAMERA_FIELDS",
        "WINDOW_FIELDS",
        "GPU_FIELDS",
        "GOVERNED_SCENE_HASH_FIELDS",
        "FILE_CONTRACT_FIELDS",
        "QUALIFICATION_FIELDS",
        "RUNTIME_LEDGER_FIELDS",
        "ADAPTER_RECEIPT_SCHEMA",
        "ADAPTER_RECEIPT_FIELDS",
        "ADAPTER_TARGET_FIELDS",
        "ADAPTER_CAPTURE_FIELDS",
        "ADAPTER_FILE_FIELDS",
        "ADAPTER_QUALIFICATION_FIELDS",
    }
    definitions = {
        "expect_exact_fields",
        "expect_nonempty_string",
        "expect_uint64",
        "expect_finite_number",
        "validate_sha256",
        "validate_source_commit",
        "required_environment",
        "sha256_file",
        "read_json",
        "validate_automation_profile",
        "validate_ready",
        "validate_runtime_ledger",
    }
    selected: list[ast.stmt] = []
    for statement in tree.body:
        if isinstance(statement, ast.Assign) and any(
            isinstance(target, ast.Name) and target.id in assignments
            for target in statement.targets
        ):
            selected.append(statement)
        elif isinstance(statement, ast.ClassDef) and statement.name == "AdapterError":
            selected.append(statement)
        elif isinstance(statement, ast.FunctionDef) and statement.name in definitions:
            selected.append(statement)
    namespace: dict[str, object] = {
        "hashlib": hashlib,
        "json": json,
        "os": os,
        "Path": Path,
    }
    module = ast.fix_missing_locations(ast.Module(body=selected, type_ignores=[]))
    exec(compile(module, str(_ADAPTER_PATH), "exec"), namespace)
    return namespace


def _adapter_action_namespace() -> dict[str, object]:
    source = _ADAPTER_PATH.read_text(encoding="utf-8")
    tree = ast.parse(source, filename=str(_ADAPTER_PATH))
    definitions = {
        "has_flag",
        "action_flags",
        "topology_name",
        "active_shader_stages",
        "normalize_action",
        "count_present_actions",
    }
    selected: list[ast.stmt] = []
    for statement in tree.body:
        if isinstance(statement, ast.ClassDef) and statement.name == "AdapterError":
            selected.append(statement)
        elif isinstance(statement, ast.FunctionDef) and statement.name in definitions:
            selected.append(statement)

    class ActionFlags(enum.IntFlag):
        NoFlags = 0
        Drawcall = 1 << 0
        Instanced = 1 << 1
        Present = 1 << 2
        Indirect = 1 << 3
        MultiAction = 1 << 4

    class Topology(enum.Enum):
        TriangleList = 1
        LineList = 2
        PointList = 3

    class ShaderStage(enum.Enum):
        Vertex = 1
        Pixel = 2

    class RenderDocStub:
        pass

    RenderDocStub.ActionFlags = ActionFlags
    RenderDocStub.Topology = Topology
    RenderDocStub.ShaderStage = ShaderStage
    namespace: dict[str, object] = {"rd": RenderDocStub}
    module = ast.fix_missing_locations(ast.Module(body=selected, type_ignores=[]))
    exec(compile(module, str(_ADAPTER_PATH), "exec"), namespace)
    return namespace


def _valid_ready_document() -> dict[str, object]:
    digest = "0" * 64
    sources = {
        "terrain": digest,
        "vegetation": digest,
        "environment": digest,
        "water": digest,
        "weather": digest,
        "quality": digest,
        "cadle_assets_used": False,
    }
    frozen = {
        "sources": sources,
        "seeds": {"terrain": 1, "vegetation": 2, "water": 3, "weather": 4},
        "tick": {
            "virtual_elapsed_nanoseconds": 0,
            "fixed_elapsed_nanoseconds": 0,
            "fixed_timestep_nanoseconds": 16_666_667,
            "fixed_tick": 0,
        },
        "vsync_requested": False,
        "expected_weather": "clear",
        "weather_target": "Clear",
        "weather_wetness_current_bits": 0,
        "weather_wetness_target_bits": 0,
        "quality_selection": "Fixed(High)",
        "quality_startup_tier": "High",
        "quality_current_tier": "High",
        "quality_transition_count": 0,
        "pipeline_total": 12,
        "pipeline_ready": 12,
        "camera": {
            "translation_m": [0.0, 10.0, 20.0],
            "forward": [0.0, 0.0, -1.0],
            "vertical_fov_degrees": 60.0,
            "active": True,
        },
        "window": {
            "physical_width": 1920,
            "physical_height": 1080,
            "scale_factor": 1.0,
            "visible": True,
            "resizable": False,
        },
        "gpu": {
            "name": "Synthetic Vulkan GPU",
            "vendor": 1,
            "device": 2,
            "device_type": "DiscreteGpu",
            "driver": "synthetic",
            "driver_info": "1.0",
            "backend": "Vulkan",
            "max_texture_dimension_2d": 16384,
            "max_bind_groups": 8,
            "max_uniform_buffer_binding_size": 65536,
            "max_storage_buffer_binding_size": 134217728,
        },
        "governed_scene_hashes": {
            "visual_valley": digest,
            "shadow": digest,
            "vegetation": digest,
            "scenery": digest,
            "environment": digest,
            "water_static": digest,
            "weather_static": digest,
        },
    }
    return {
        "schema": "forge.gate1-renderdoc-ready",
        "schema_version": 1,
        "status": "ready_frozen_pending_external_capture_signal",
        "nonce": "0123456789abcdef0123456789abcdef",
        "process_id": 1234,
        "source_commit": "a" * 40,
        "build_profile": "release",
        "required_backend": "Vulkan",
        "required_renderdoc_version": "1.46",
        "ready_frame": 100,
        "freeze_settle_frames": 30,
        "virtual_time_paused": True,
        "frozen_state_sha256": digest,
        "frozen_state": frozen,
        "files": {
            "ready": "gate1-renderdoc.ready.json",
            "signal": "gate1-renderdoc.capture.signal.json",
            "ledger": "gate1-renderdoc.runtime.json",
            "failure": "gate1-renderdoc.failure.txt",
        },
        "qualification": {
            "runtime_state_qualified": False,
            "external_capture_observed_by_runtime": False,
            "renderdoc_analysis_completed_by_runtime": False,
            "gate1_route_qualified": False,
            "limitation": "external analysis pending",
        },
    }


class Gate1RenderDocContractChainTests(unittest.TestCase):
    def test_adapter_emits_instanced_and_counts_present_actions_without_deduplication(
        self,
    ) -> None:
        namespace = _adapter_action_namespace()
        flags = namespace["rd"].ActionFlags
        topology = namespace["rd"].Topology

        class Action:
            def __init__(
                self,
                event_id: int,
                action_flags: object,
                *,
                num_indices: int = 0,
                num_instances: int = 0,
                children: list[object] | None = None,
            ) -> None:
                self.eventId = event_id
                self.flags = action_flags
                self.numIndices = num_indices
                self.numInstances = num_instances
                self.children = [] if children is None else children

            def GetName(self, _structured_file: object) -> str:
                return f"action-{self.eventId}"

        class Pipeline:
            @staticmethod
            def GetPrimitiveTopology() -> object:
                return topology.TriangleList

            @staticmethod
            def GetShaderReflection(stage: object) -> object | None:
                return object() if stage in tuple(namespace["rd"].ShaderStage) else None

        class Controller:
            def __init__(self) -> None:
                self.events: list[int] = []

            def SetFrameEvent(self, event_id: int, force: bool) -> None:
                if not force:
                    raise AssertionError("adapter must force the exact frame event")
                self.events.append(event_id)

            @staticmethod
            def GetPipelineState() -> Pipeline:
                return Pipeline()

        controller = Controller()
        normalize = namespace["normalize_action"]
        non_instanced = Action(10, flags.Drawcall, num_indices=3)
        instanced = Action(
            11,
            flags.Drawcall | flags.Instanced,
            num_indices=6,
            num_instances=4,
        )
        self.assertFalse(normalize(controller, object(), non_instanced)["instanced"])
        self.assertTrue(normalize(controller, object(), instanced)["instanced"])

        first_present = Action(20, flags.Present)
        second_present_same_event = Action(20, flags.Present)
        count_present = namespace["count_present_actions"]
        self.assertEqual(count_present([first_present]), 1)
        self.assertEqual(count_present([first_present, second_present_same_event]), 2)

    def test_runtime_adapter_manifest_and_orchestrator_share_one_exact_contract(self) -> None:
        manifest = _load_manifest()
        workload = manifest["workload"]
        capture = manifest["capture"]
        normalized = manifest["normalized_input"]
        analysis = manifest["analysis"]
        adapter = _ADAPTER_PATH.read_text(encoding="utf-8")
        runtime = _RUNTIME_PATH.read_text(encoding="utf-8")
        engine = _ENGINE_PATH.read_text(encoding="utf-8")
        orchestrator = _ORCHESTRATOR_PATH.read_text(encoding="utf-8")

        self.assertEqual(INPUT_SCHEMA, normalized["schema"])
        self.assertEqual(OUTPUT_SCHEMA, analysis["output_schema"])
        self.assertIn(f'NORMALIZED_ACTION_SCHEMA = "{INPUT_SCHEMA}"', adapter)
        self.assertNotIn("forge-gate1-renderdoc-actions/v1", adapter)
        self.assertNotIn("forge-gate1-renderdoc-analysis/v1", orchestrator)
        self.assertIn(f'$requiredActionSchema = "{INPUT_SCHEMA}"', orchestrator)
        self.assertIn(f'$requiredAnalysisSchema = "{OUTPUT_SCHEMA}"', orchestrator)

        runtime_contracts = (
            ("ReadyReceipt", workload["ready_receipt_required_fields"], "READY_FIELDS"),
            (
                "FrozenStateEvidence",
                workload["ready_receipt_frozen_state_fields"],
                "FROZEN_STATE_FIELDS",
            ),
            ("SourceHashes", workload["ready_receipt_source_fields"], "SOURCE_FIELDS"),
            ("SourceSeeds", workload["ready_receipt_seed_fields"], "SEED_FIELDS"),
            ("ClockTickEvidence", workload["ready_receipt_tick_fields"], "TICK_FIELDS"),
            ("CameraEvidence", workload["ready_receipt_camera_fields"], "CAMERA_FIELDS"),
            ("WindowEvidence", workload["ready_receipt_window_fields"], "WINDOW_FIELDS"),
            (
                "GovernedSceneHashes",
                workload["ready_receipt_governed_scene_hash_fields"],
                "GOVERNED_SCENE_HASH_FIELDS",
            ),
            ("FileContract", workload["ready_receipt_file_fields"], "FILE_CONTRACT_FIELDS"),
            (
                "QualificationBoundary",
                workload["ready_receipt_qualification_fields"],
                "QUALIFICATION_FIELDS",
            ),
            ("CaptureSignal", capture["signal_fields"], "SIGNAL_FIELDS"),
            ("RenderDocSignal", capture["signal_renderdoc_fields"], "RENDERDOC_SIGNAL_FIELDS"),
            ("RuntimeLedger", capture["runtime_ledger_fields"], "RUNTIME_LEDGER_FIELDS"),
        )
        for rust_name, manifest_fields, adapter_name in runtime_contracts:
            with self.subTest(contract=rust_name):
                expected = set(manifest_fields)
                self.assertEqual(expected, _rust_struct_fields(runtime, rust_name))
                self.assertEqual(expected, _python_set_assignment(adapter, adapter_name))

        gpu_fields = set(workload["ready_receipt_gpu_fields"])
        self.assertEqual(gpu_fields, _rust_struct_fields(engine, "GpuDiagnostic"))
        self.assertEqual(gpu_fields, _python_set_assignment(adapter, "GPU_FIELDS"))

        self.assertEqual(
            set(normalized["root_fields"]), set(_ROOT_FIELDS)
        )
        self.assertEqual(set(normalized["capture_fields"]), set(_CAPTURE_FIELDS))
        self.assertEqual(set(normalized["action_fields"]), set(_ACTION_FIELDS))
        self.assertIn('is_instanced = "Instanced" in flags', adapter)
        self.assertIn('"instanced": is_instanced', adapter)
        nominal = json.loads(
            (_HERE / "fixtures" / "nominal_nested_actions.json").read_text(encoding="utf-8")
        )
        nominal_analysis = analyze_document(nominal)
        self.assertEqual(set(analysis["output_fields"]), set(nominal_analysis))
        self.assertEqual(set(analysis["metric_fields"]), set(nominal_analysis["metrics"]))
        self.assertIn('"present_actions"', orchestrator)
        self.assertIn("$Analysis.metrics.present_actions -eq 1", orchestrator)
        self.assertIn("Measure-PresentActions", orchestrator)
        self.assertEqual(
            ["draws", "instances", "triangles"],
            analysis["required_positive_metrics"],
        )
        self.assertIn("$requiredPositiveMetrics", orchestrator)
        self.assertIn("$Analysis.metrics.$field -gt 0", orchestrator)
        self.assertNotIn("draw_calls_observed = $true", orchestrator)
        self.assertNotIn("instances_observed = $true", orchestrator)
        self.assertNotIn("triangles_observed = $true", orchestrator)
        self.assertIn("PresentMode::AutoNoVsync", runtime)
        self.assertIn("vsync_requested -is [bool]", orchestrator)

        self.assertEqual(
            {"name": "forge.gate1-renderdoc-ready", "version": 1},
            workload["ready_receipt_schema"],
        )
        self.assertEqual(
            {"name": "forge.gate1-renderdoc-capture-signal", "version": 1},
            capture["signal_schema"],
        )
        self.assertEqual(
            {"name": "forge.gate1-renderdoc-runtime", "version": 1},
            capture["runtime_ledger_schema"],
        )
        self.assertEqual(
            "forge-gate1-qrenderdoc-<nonce>.exe --python", capture["launcher"]
        )
        automation_profile = capture["automation_profile"]
        self.assertEqual("5.15.2.0", manifest["tool"]["embedded_qt_core_file_version"])
        self.assertEqual(72, manifest["tool"]["bundle_inventory"]["file_count"])
        self.assertEqual(
            "C2F3D1C4F477F3DDB8B80C4AC2AB9BC72F881F0E95B612EAB40A59B3F3E300B1",
            manifest["tool"]["bundle_inventory"]["sha256"],
        )
        self.assertEqual(
            "full_bundle_mirror_unique_executable_basename",
            automation_profile["strategy"],
        )
        self.assertTrue(automation_profile["analytics_total_opt_out"])
        self.assertTrue(automation_profile["adapter_config_path_check_before_injection"])
        self.assertIn("validate_automation_profile()", adapter)
        self.assertIn("Remove-GovernedDirectoryTree", orchestrator)
        self.assertIn("Assert-RenderDocBundleInventory", orchestrator)
        self.assertIn("$governedBuildTimeoutSeconds = 2700", orchestrator)
        for disjoint_call in (
            'Assert-DisjointRoots -First $worktree -Second $outputPath',
            'Assert-DisjointRoots -First $worktree -Second $cargoTargetPath',
            'Assert-DisjointRoots -First $worktree -Second $renderDocPath',
            'Assert-DisjointRoots -First $outputPath -Second $cargoTargetPath',
            'Assert-DisjointRoots -First $renderDocPath -Second $outputPath',
            'Assert-DisjointRoots -First $renderDocPath -Second $cargoTargetPath',
        ):
            self.assertIn(disjoint_call, orchestrator)
        self.assertIn("$process.WaitForExit(10000)", orchestrator)
        self.assertNotIn("$process.WaitForExit()", orchestrator)
        self.assertIn("$actualBundleRelativePath", orchestrator)
        self.assertIn("$actualInventoryRelativePath", orchestrator)
        self.assertIn("$executionViewOwned = $true", orchestrator)
        self.assertIn("$activeProfileOwned = $true", orchestrator)
        self.assertIn("FORGE_GATE1_RENDERDOC_RENDERDOC_DLL_SHA256", adapter)
        self.assertIn("FORGE_GATE1_RENDERDOC_RENDERDOC_DLL_SHA256", orchestrator)
        adapter_namespace = _adapter_validation_namespace()
        self.assertEqual(
            capture["adapter_receipt_schema"],
            adapter_namespace["ADAPTER_RECEIPT_SCHEMA"],
        )
        for manifest_name, adapter_name in (
            ("adapter_receipt_fields", "ADAPTER_RECEIPT_FIELDS"),
            ("adapter_target_fields", "ADAPTER_TARGET_FIELDS"),
            ("adapter_capture_fields", "ADAPTER_CAPTURE_FIELDS"),
            ("adapter_file_fields", "ADAPTER_FILE_FIELDS"),
            ("adapter_qualification_fields", "ADAPTER_QUALIFICATION_FIELDS"),
        ):
            self.assertEqual(
                set(capture[manifest_name]),
                adapter_namespace[adapter_name],
            )
            self.assertIn("manifest.capture.{}".format(manifest_name), orchestrator)

    def test_adapter_profile_guard_is_fail_closed(self) -> None:
        namespace = _adapter_validation_namespace()
        validate = namespace["validate_automation_profile"]
        adapter_error = namespace["AdapterError"]
        with tempfile.TemporaryDirectory() as directory:
            config_path = Path(directory) / "UI.config"
            encoded = b'{"rdocConfigData":1,"Analytics_TotalOptOut":true}\n'
            config_path.write_bytes(encoded)
            environment = {
                "FORGE_GATE1_RENDERDOC_PROFILE_CONFIG": str(config_path),
                "FORGE_GATE1_RENDERDOC_PROFILE_SEED_SHA256": hashlib.sha256(
                    encoded
                ).hexdigest(),
            }
            module = types.SimpleNamespace(ConfigFilePath=lambda _name: str(config_path))
            with mock.patch.dict(sys.modules, {"qrenderdoc": module}), mock.patch.dict(
                os.environ, environment, clear=False
            ):
                validate()
                self.assertNotIn("FORGE_GATE1_RENDERDOC_PROFILE_CONFIG", os.environ)
                self.assertNotIn("FORGE_GATE1_RENDERDOC_PROFILE_SEED_SHA256", os.environ)

            config_path.write_text(
                '{"rdocConfigData":1,"Analytics_TotalOptOut":false}\n',
                encoding="utf-8",
            )
            with mock.patch.dict(sys.modules, {"qrenderdoc": module}), mock.patch.dict(
                os.environ, environment, clear=False
            ):
                with self.assertRaises(adapter_error):
                    validate()

    def test_ready_validator_rejects_shape_tick_seed_and_binding_drift(self) -> None:
        namespace = _adapter_validation_namespace()
        validate_ready = namespace["validate_ready"]
        error = namespace["AdapterError"]
        ready = _valid_ready_document()
        arguments = (
            ready["nonce"],
            ready["source_commit"],
            ready["process_id"],
            "clear",
        )

        validate_ready(copy.deepcopy(ready), *arguments)
        mutations = []
        missing_top_level = copy.deepcopy(ready)
        del missing_top_level["frozen_state"]
        mutations.append(missing_top_level)
        extra_top_level = copy.deepcopy(ready)
        extra_top_level["unexpected"] = True
        mutations.append(extra_top_level)
        missing_nested = copy.deepcopy(ready)
        del missing_nested["frozen_state"]["seeds"]["terrain"]
        mutations.append(missing_nested)
        nonzero_tick = copy.deepcopy(ready)
        nonzero_tick["frozen_state"]["tick"]["fixed_tick"] = 1
        mutations.append(nonzero_tick)
        boolean_seed = copy.deepcopy(ready)
        boolean_seed["frozen_state"]["seeds"]["terrain"] = True
        mutations.append(boolean_seed)
        vsync_requested = copy.deepcopy(ready)
        vsync_requested["frozen_state"]["vsync_requested"] = True
        mutations.append(vsync_requested)
        wrong_pid = copy.deepcopy(ready)
        wrong_pid["process_id"] += 1
        mutations.append(wrong_pid)
        boolean_pid = copy.deepcopy(ready)
        boolean_pid["process_id"] = True
        mutations.append(boolean_pid)

        for mutation in mutations:
            with self.subTest(mutation=mutation):
                with self.assertRaises(error):
                    validate_ready(mutation, *arguments)

    def test_runtime_ledger_validator_rejects_shape_and_frozen_state_drift(self) -> None:
        namespace = _adapter_validation_namespace()
        validate_ledger = namespace["validate_runtime_ledger"]
        error = namespace["AdapterError"]
        ready = _valid_ready_document()
        digest = "0" * 64
        renderdoc = {
            "capture_triggered": True,
            "target_control_ident": 42,
            "target_api": "Vulkan",
            "tool_version": "1.46",
            "qrenderdoc_sha256": "1" * 64,
            "renderdoccmd_sha256": "2" * 64,
            "renderdoc_dll_sha256": "3" * 64,
        }
        signal = {"renderdoc": renderdoc}
        ledger = {
            "schema": "forge.gate1-renderdoc-runtime",
            "schema_version": 1,
            "status": "stable_capture_window_completed_pending_external_capture_analysis",
            "nonce": ready["nonce"],
            "process_id": ready["process_id"],
            "source_commit": ready["source_commit"],
            "build_profile": "release",
            "ready_frame": ready["ready_frame"],
            "signal_frame": ready["ready_frame"] + 1,
            "capture_window_frames": 120,
            "ready_sha256": digest,
            "capture_id": "gate1-renderdoc-clear-01",
            "renderdoc": renderdoc,
            "frozen_state_sha256": ready["frozen_state_sha256"],
            "frozen_state_initial": copy.deepcopy(ready["frozen_state"]),
            "frozen_state_final": copy.deepcopy(ready["frozen_state"]),
            "frozen_state_stable": True,
            "files": copy.deepcopy(ready["files"]),
            "qualification": {
                "runtime_state_qualified": True,
                "external_capture_observed_by_runtime": False,
                "renderdoc_analysis_completed_by_runtime": False,
                "gate1_route_qualified": False,
                "limitation": "external analysis pending",
            },
        }
        arguments = (
            ready["nonce"],
            ready["source_commit"],
            digest,
            ledger["capture_id"],
            ready["process_id"],
            ready,
            signal,
        )

        validate_ledger(copy.deepcopy(ledger), *arguments)
        mutations = []
        missing_field = copy.deepcopy(ledger)
        del missing_field["renderdoc"]
        mutations.append(missing_field)
        extra_field = copy.deepcopy(ledger)
        extra_field["unexpected"] = True
        mutations.append(extra_field)
        changed_final = copy.deepcopy(ledger)
        changed_final["frozen_state_final"]["vsync_requested"] = True
        mutations.append(changed_final)
        changed_renderdoc = copy.deepcopy(ledger)
        changed_renderdoc["renderdoc"]["renderdoc_dll_sha256"] = "4" * 64
        mutations.append(changed_renderdoc)
        early_signal = copy.deepcopy(ledger)
        early_signal["signal_frame"] = ready["ready_frame"] - 1
        mutations.append(early_signal)
        boolean_schema = copy.deepcopy(ledger)
        boolean_schema["schema_version"] = True
        mutations.append(boolean_schema)

        for mutation in mutations:
            with self.subTest(mutation=mutation):
                with self.assertRaises(error):
                    validate_ledger(mutation, *arguments)


if __name__ == "__main__":
    unittest.main()
