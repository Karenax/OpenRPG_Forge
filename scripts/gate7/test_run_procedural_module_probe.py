"""CPU-only tests for the qualification runner; no application or GPU is launched."""
import contextlib
import copy
import hashlib
import io
import json
from pathlib import Path
import struct
import tempfile
from types import SimpleNamespace
import unittest
from unittest import mock
import zlib

import run_procedural_module_probe as probe


HEAD = "a" * 64
ARCHIVE = "b" * 64
TERRAIN = "c" * 64
SCENARIO_HASH = "d" * 64
FUNCTIONAL = "e" * 64
PINS = {"head": HEAD, "archive": ARCHIVE, "terrain": TERRAIN,
        "module_id": "module", "area": "area"}


def plan():
    return {"schema": "forge.native-gameplay.scenario", "version": 1,
            "source_hash": HEAD, "archive_hash": ARCHIVE, "max_duration_s": 120,
            "steps": [
                {"kind": "wait_ready", "timeout_s": 30},
                {"kind": "capture", "name": "start.png", "timeout_s": 30},
                {"kind": "move_to", "position_xz": [20.0, 0.0], "tolerance_m": 1.0, "timeout_s": 30},
                {"kind": "capture", "name": "outward.png", "timeout_s": 30},
                {"kind": "move_to", "position_xz": [0.0, 0.0], "tolerance_m": 1.0, "timeout_s": 30},
                {"kind": "capture", "name": "return.png", "timeout_s": 30},
                {"kind": "exit"},
            ]}


def build_receipt():
    return {"schema": "forge.central-cargo-receipt.v1", "exit_code": 0,
            "arguments": ["build", "-p", "forge_runtime", "--bin", "forge-module",
                          "--no-default-features", "--features", "qualification"]}


def evidence():
    terrain = {"document_hash": TERRAIN, "dimensions": [8, 8], "maximum_cells": 64,
               "maximum_cpu_mesh_bytes": 10000, "maximum_staging_cpu_mesh_bytes": 10000}
    actual = {"identity": terrain, "resident_cells": 9, "detailed_cells": 3,
              "coarse_cells": 6, "cpu_mesh_bytes": 500, "resident_triangles": 100,
              "committed_transitions": 3, "pending": False, "physics_ready": True,
              "source_synchronized": True, "error": None,
              "hlod": {"tiles": 10, "covered_base_cells": 55, "cpu_mesh_bytes": 250,
                       "resident_triangles": 30, "peak_staging_mesh_bytes": 750}}
    static = {"enabled": True, "pending": False, "error": None, "rendered_batches": 2,
              "replaced_primitives": 3, "gpu_upload_ready": True,
              "source_objects": 3, "source_primitives": 3, "source_triangles": 40}
    return {"schema": "forge.module-rpg-runtime-report", "version": 2,
            "runtime_exit_success": True, "projection_ready": True,
            "expected": {"source_hash": HEAD, "module_id": "module", "area_id": "area",
                         "procedural_streaming": terrain},
            "final_observation": {"procedural_streaming": actual, "static_hlod": static}}


def rows():
    return [
        {"frame_ms": 16.0, "projection_ready": True, "player_position": [0.0, 0.0, 0.0]},
        {"frame_ms": 80.0, "projection_ready": False, "player_position": [1.0, 0.0, 0.0]},
        {"frame_ms": 20.0, "projection_ready": True, "player_position": [20.0, 0.0, 0.0]},
        {"frame_ms": 18.0, "projection_ready": True, "player_position": [0.1, 0.0, 0.0]},
    ]


def timing_document():
    samples = copy.deepcopy(rows())
    for index, row in enumerate(samples):
        row.update(frame=index + 1, tick=index)
    return {"schema": "forge.native-main-frame-intervals.v2", "pid": 42, "source_hash": HEAD,
            "maximum_samples": 20000, "maximum_metadata_snapshots_per_domain": 512,
            "truncated": False, "graphics_observations": [], "imported_observations": [],
            "samples": samples}


def png():
    def chunk(kind, data):
        return struct.pack(">I", len(data)) + kind + data + struct.pack(">I", zlib.crc32(kind + data))
    return (probe.PNG + chunk(b"IHDR", struct.pack(">IIBBBBB", 1, 1, 8, 6, 0, 0, 0))
            + chunk(b"IDAT", zlib.compress(b"\x00\x00\x00\x00\xff")) + chunk(b"IEND", b""))


class ProceduralRunnerTests(unittest.TestCase):
    def test_scenario_requires_pins_and_captures_around_both_moves(self):
        good = plan()
        self.assertEqual(probe.check_plan(good, HEAD, ARCHIVE),
                         ["start.png", "outward.png", "return.png"])
        wrong = copy.deepcopy(good)
        wrong["source_hash"] = "e" * 64
        with self.assertRaises(ValueError):
            probe.check_plan(wrong, HEAD, ARCHIVE)
        wrong = copy.deepcopy(good)
        wrong["steps"][3]["name"] = "../elsewhere.png"
        with self.assertRaises(ValueError):
            probe.check_plan(wrong, HEAD, ARCHIVE)
        wrong = copy.deepcopy(good)
        wrong["steps"][3], wrong["steps"][1] = wrong["steps"][1], wrong["steps"][3]
        wrong["steps"][3], wrong["steps"][4] = wrong["steps"][4], wrong["steps"][3]
        with self.assertRaises(ValueError):
            probe.check_plan(wrong, HEAD, ARCHIVE)

    def test_build_receipt_does_not_accept_missing_qualification(self):
        probe.check_build_receipt(build_receipt())
        wrong = build_receipt()
        wrong["arguments"][-1] = "other"
        with self.assertRaises(ValueError):
            probe.check_build_receipt(wrong)
        wrong = build_receipt()
        wrong["exit_code"] = 1
        with self.assertRaises(ValueError):
            probe.check_build_receipt(wrong)

    def test_recorded_nonready_intervals_are_kept_and_route_returns(self):
        result = probe.analyze(evidence(), rows(), "hierarchy", PINS, 10.0, 1.0, True)
        self.assertEqual(result["recorded_frames"]["samples"], 4)
        self.assertEqual(result["recorded_ready_frames"]["samples"], 3)
        self.assertEqual(result["recorded_not_ready_frames"]["samples"], 1)
        self.assertEqual(result["recorded_not_ready_frames"]["p95_ms"], 80.0)
        self.assertTrue(result["static_hlod_replacement_exercised"])
        self.assertAlmostEqual(result["farthest_distance_m"], 20.0)
        with self.assertRaises(ValueError):
            probe.analyze(evidence(), rows()[:-1], "hierarchy", PINS, 10.0, 1.0, True)

    def test_static_witness_and_terrain_are_not_inferred(self):
        report = evidence()
        report["final_observation"]["static_hlod"]["gpu_upload_ready"] = False
        result = probe.analyze(report, rows(), "hierarchy", PINS, 10.0, 1.0, False)
        self.assertFalse(result["static_hlod_replacement_exercised"])
        with self.assertRaises(ValueError):
            probe.analyze(report, rows(), "hierarchy", PINS, 10.0, 1.0, True)
        for key, value in (("pending", True), ("physics_ready", False), ("source_synchronized", False)):
            bad = evidence()
            bad["final_observation"]["procedural_streaming"][key] = value
            with self.subTest(key=key), self.assertRaises(ValueError):
                probe.analyze(bad, rows(), "hierarchy", PINS, 10.0, 1.0, False)
        bad = evidence()
        bad["expected"]["procedural_streaming"]["document_hash"] = "f" * 64
        with self.assertRaises(ValueError):
            probe.analyze(bad, rows(), "hierarchy", PINS, 10.0, 1.0, False)

    def test_detail_control_must_not_replace_primitives(self):
        report = evidence()
        static = report["final_observation"]["static_hlod"]
        static.update(enabled=False, rendered_batches=0, replaced_primitives=0)
        probe.analyze(report, rows(), "detail", PINS, 10.0, 1.0, False)
        static["replaced_primitives"] = 1
        with self.assertRaises(ValueError):
            probe.analyze(report, rows(), "detail", PINS, 10.0, 1.0, False)

    def test_timings_check_hash_identity_count_order_and_truncation(self):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "frame-timings.json"
            document = timing_document()
            def write_and_read():
                path.write_text(json.dumps(document), encoding="utf-8")
                summary = {"enabled": True, "artifact": path.name, "truncated": False,
                           "recorded": len(document["samples"]), "sha256": probe.digest(path)}
                return probe.read_timings(path, {"frame_timings": summary}, PINS, 42)
            self.assertEqual(len(write_and_read()), 4)
            document["schema"] = "forge.native-main-frame-intervals.v3"
            with self.assertRaises(ValueError):
                write_and_read()
            path.write_text(json.dumps(document), encoding="utf-8")
            summary = {"enabled": True, "artifact": path.name, "truncated": False,
                       "recorded": len(document["samples"]), "sha256": probe.digest(path)}
            self.assertEqual(len(probe.read_timings(path, {"frame_timings": summary}, PINS, 42,
                                 "forge.native-main-frame-intervals.v3")), 4)
            document["schema"] = "forge.native-main-frame-intervals.v2"
            document["pid"] = 43
            with self.assertRaises(ValueError):
                write_and_read()
            document["pid"] = 42
            document["truncated"] = True
            with self.assertRaises(ValueError):
                write_and_read()
            document["truncated"] = False
            document["samples"][2]["frame"] = document["samples"][1]["frame"]
            with self.assertRaises(ValueError):
                write_and_read()

    def test_scenario_events_are_complete_and_match_the_pinned_plan(self):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "observations.jsonl"
            events = [{"schema": "forge.native-gameplay.observation", "version": 1,
                       "qualification": "observations_only", "kind": "scenario", "pid": 42,
                       "source_hash": HEAD, "archive_hash": ARCHIVE, "plan_hash": SCENARIO_HASH,
                       "functional_hash": FUNCTIONAL,
                       "step": index, "status": "completed", "detail": step}
                      for index, step in enumerate(plan()["steps"])]
            def check():
                path.write_text("".join(json.dumps(event) + "\n" for event in events), encoding="utf-8")
                probe.check_events(path, plan(), PINS, SCENARIO_HASH, FUNCTIONAL, 42)
            check()
            events[2]["detail"] = {"kind": "exit"}
            with self.assertRaises(ValueError):
                check()
            events[2]["detail"] = plan()["steps"][2]
            del events[3]
            with self.assertRaises(ValueError):
                check()

    def test_game_report_requires_matching_functional_identity_and_clean_exit(self):
        report = {"schema": "forge.native-gameplay.report", "version": 1, "pid": 42,
                  "source_hash": HEAD, "archive_hash": ARCHIVE, "functional_hash": FUNCTIONAL,
                  "exit_success": True, "ready": True, "last_command_error": None,
                  "simulation_error": None}
        probe.check_game_report(report, PINS, FUNCTIONAL, 42)
        report["functional_hash"] = "f" * 64
        with self.assertRaises(ValueError):
            probe.check_game_report(report, PINS, FUNCTIONAL, 42)
        report["functional_hash"] = FUNCTIONAL
        report["simulation_error"] = "backend unavailable"
        with self.assertRaises(ValueError):
            probe.check_game_report(report, PINS, FUNCTIONAL, 42)

    def test_capture_crc_and_file_completeness_are_checked_without_claiming_visual_review(self):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "capture.png"
            contents = png()
            path.write_bytes(contents)
            capture = probe.capture_info(path)
            self.assertEqual((capture["width"], capture["height"]), (1, 1))
            self.assertFalse(capture["pixel_decode_verified"])
            self.assertFalse(capture["human_reviewed"])
            damaged = bytearray(contents)
            damaged[20] ^= 1
            path.write_bytes(damaged)
            with self.assertRaises(ValueError):
                probe.capture_info(path)
            path.write_bytes(contents[:-1])
            with self.assertRaises(ValueError):
                probe.capture_info(path)

    def test_describe_checks_pins_without_creating_outputs_or_starting_process(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            binary, package, scenario = root / "binary", root / "package", root / "scenario.json"
            receipt, sources = root / "receipt.json", root / "source-inputs.json"
            binary.write_bytes(b"inert binary")
            package.write_bytes(b"inert package")
            scenario.write_text(json.dumps(plan()), encoding="utf-8")
            receipt.write_text(json.dumps(build_receipt()), encoding="utf-8")
            sources.write_text("{}", encoding="utf-8")
            args = SimpleNamespace(binary=binary, package=package, scenario=scenario,
                                   build_receipt=receipt, source_manifest=sources,
                                   module_id="module", area="area", head=HEAD, archive=ARCHIVE,
                                   transport="e" * 64, terrain=TERRAIN, output=root / "evidence",
                                   work_root=root / "work", quality="high", backend="vulkan",
                                   minimum_distance_m=10.0, return_tolerance_m=1.0,
                                   require_static_replacement=False, describe=True)
            for name in ("binary", "package", "scenario", "build_receipt", "source_manifest"):
                setattr(args, name + "_sha256", hashlib.sha256(getattr(args, name).read_bytes()).hexdigest())
            output = io.StringIO()
            with mock.patch.object(probe.subprocess, "Popen") as spawn, contextlib.redirect_stdout(output):
                probe.run(args)
            spawn.assert_not_called()
            self.assertFalse(args.output.exists())
            self.assertFalse(args.work_root.exists())
            self.assertFalse(json.loads(output.getvalue())["build_source_link_verified"])
            package.write_bytes(b"changed")
            with self.assertRaises(ValueError):
                probe.run(args)

    def test_full_two_run_orchestration_with_inert_process_fixtures(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            values = {"binary": b"inert binary", "package": b"inert package",
                      "scenario": json.dumps(plan()).encode(),
                      "build_receipt": json.dumps(build_receipt()).encode(), "source_manifest": b"{}"}
            args = SimpleNamespace(module_id="module", area="area", head=HEAD, archive=ARCHIVE,
                                   transport="f" * 64, terrain=TERRAIN, output=root / "evidence",
                                   work_root=root / "work", quality="high", backend="vulkan",
                                   minimum_distance_m=10.0, return_tolerance_m=1.0,
                                   require_static_replacement=True, describe=False)
            for name, data in values.items():
                path = root / name
                path.write_bytes(data)
                setattr(args, name, path)
                setattr(args, name + "_sha256", hashlib.sha256(data).hexdigest())
            modes = []
            def simulate(command, cwd, env, stdout, stderr):
                mode = env["FORGE_GAME_STATIC_HLOD"]
                modes.append(mode)
                pid = 100 + len(modes)
                result_dir = Path(env["FORGE_GAME_REPORT_PATH"]).parent
                document = timing_document()
                document["pid"] = pid
                timing_path = result_dir / "frame-timings.json"
                timing_path.write_text(json.dumps(document), encoding="utf-8")
                report = evidence()
                report["pid"] = pid
                report["expected"]["functional_hash"] = FUNCTIONAL
                report["frame_timings"] = {"enabled": True, "artifact": timing_path.name,
                                           "truncated": False, "recorded": len(document["samples"]),
                                           "sha256": probe.digest(timing_path)}
                if mode == "detail":
                    report["final_observation"]["static_hlod"].update(enabled=False, rendered_batches=0,
                                                                       replaced_primitives=0)
                (result_dir / "module-runtime-report.json").write_text(json.dumps(report), encoding="utf-8")
                game = {"schema": "forge.native-gameplay.report", "version": 1, "pid": pid,
                        "source_hash": HEAD, "archive_hash": ARCHIVE, "functional_hash": FUNCTIONAL,
                        "exit_success": True, "ready": True, "last_command_error": None,
                        "simulation_error": None}
                (result_dir / "report.json").write_text(json.dumps(game), encoding="utf-8")
                events = [{"schema": "forge.native-gameplay.observation", "version": 1,
                           "qualification": "observations_only", "kind": "scenario", "pid": pid,
                           "source_hash": HEAD, "archive_hash": ARCHIVE,
                           "functional_hash": FUNCTIONAL, "plan_hash": args.scenario_sha256,
                           "step": index, "status": "completed", "detail": step}
                          for index, step in enumerate(plan()["steps"])]
                (result_dir / "observations.jsonl").write_text(
                    "".join(json.dumps(event) + "\n" for event in events), encoding="utf-8")
                for name in ("start.png", "outward.png", "return.png"):
                    (result_dir / name).write_bytes(png())
                return SimpleNamespace(pid=pid, wait=lambda timeout: 0)
            with mock.patch.object(probe.subprocess, "Popen", side_effect=simulate):
                probe.run(args)
            assessment = json.loads((args.output / "assessment.json").read_text(encoding="utf-8"))
            self.assertEqual(modes, ["hierarchy", "detail"])
            self.assertTrue(assessment["procedural_probe_passed"])
            self.assertFalse(assessment["performance_qualified"])
            self.assertFalse(assessment["tidewater_comparison"])
            self.assertFalse(assessment["residency_transition_causality_verified"])
            self.assertEqual(assessment["runs"][0]["recorded_not_ready_frames"]["samples"], 1)
            self.assertTrue(assessment["runs"][0]["static_hlod_replacement_exercised"])


    def test_terrain_comparison_requires_mode_and_explicit_bounded_limits(self):
        args = SimpleNamespace(comparison="terrain", max_run_seconds=150,
                               max_route_deviation_m=2.0, require_static_replacement=False)
        self.assertTrue(probe.check_terrain_comparison(args, plan()))
        for key, value in (("comparison", None), ("max_run_seconds", None),
                           ("max_run_seconds", 149), ("max_run_seconds", 631),
                           ("max_route_deviation_m", None), ("max_route_deviation_m", float("nan")),
                           ("require_static_replacement", True)):
            bad = copy.copy(args)
            setattr(bad, key, value)
            with self.subTest(key=key, value=value), self.assertRaises(ValueError):
                probe.check_terrain_comparison(bad, plan())

    def test_terrain_route_comparison_rejects_spatial_divergence(self):
        self.assertEqual(probe.route_deviation(rows(), rows(), 0.1), 0.0)
        divergent = copy.deepcopy(rows())
        divergent[2]["player_position"][2] = 20.0
        with self.assertRaises(ValueError):
            probe.route_deviation(rows(), divergent, 1.0)

    def test_complete_mode_requires_absent_hlod_and_correct_source(self):
        report = evidence()
        report["version"] = 1
        del report["expected"]["procedural_streaming"]
        del report["final_observation"]["procedural_streaming"]
        del report["final_observation"]["static_hlod"]
        result = probe.analyze_complete(report, rows(), PINS, 10.0, 1.0)
        self.assertIsNone(result["static_hlod"])
        report["expected"]["streamed_terrain"] = {"invented": True}
        with self.assertRaises(ValueError):
            probe.analyze_complete(report, rows(), PINS, 10.0, 1.0)

    def test_timeout_cleanup_is_bounded_and_reports_a_surviving_child(self):
        child = mock.Mock(pid=123)
        timeout = probe.subprocess.TimeoutExpired("fixture", 1)
        child.wait.side_effect = [timeout, timeout, timeout]
        with self.assertRaisesRegex(ValueError, "123 remained alive"):
            probe.wait_bounded(child, 1, "complete")
        child.terminate.assert_called_once()
        child.kill.assert_called_once()
        self.assertEqual([call.kwargs["timeout"] for call in child.wait.call_args_list], [1, 10, 10])

    def test_terrain_comparison_uses_same_pins_and_real_absence_with_mocked_processes(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            values = {"binary": b"inert binary", "package": b"inert package",
                      "scenario": json.dumps(plan()).encode(),
                      "build_receipt": json.dumps(build_receipt()).encode(), "source_manifest": b"{}"}
            args = SimpleNamespace(module_id="module", area="area", head=HEAD, archive=ARCHIVE,
                                   transport="f" * 64, terrain=TERRAIN, output=root / "evidence",
                                   work_root=root / "work", quality="high", backend="vulkan",
                                   minimum_distance_m=10.0, return_tolerance_m=1.0,
                                   require_static_replacement=False, describe=False,
                                   comparison="terrain", max_run_seconds=150, max_route_deviation_m=1.0)
            for name, data in values.items():
                path = root / name
                path.write_bytes(data)
                setattr(args, name, path)
                setattr(args, name + "_sha256", hashlib.sha256(data).hexdigest())
            calls = []
            def simulate(command, cwd, env, stdout, stderr):
                streamed = env.get("FORGE_QUALIFY_PROCEDURAL_STREAMING") == "1"
                calls.append((streamed, command, dict(env)))
                pid = 100 + len(calls)
                result_dir = Path(env["FORGE_GAME_REPORT_PATH"]).parent
                document = timing_document()
                document["pid"] = pid
                for row in document["samples"]:
                    row["simulation_ready"] = True
                if not streamed:
                    document["schema"] = "forge.native-main-frame-intervals.v3"
                timing_path = result_dir / "frame-timings.json"
                timing_path.write_text(json.dumps(document), encoding="utf-8")
                report = evidence()
                report["pid"] = pid
                report["expected"]["functional_hash"] = FUNCTIONAL
                report["frame_timings"] = {"enabled": True, "artifact": timing_path.name,
                                           "truncated": False, "recorded": len(document["samples"]),
                                           "sha256": probe.digest(timing_path)}
                report["final_observation"]["graphics"] = {
                    "requested": "high", "applied": "high", "ready": True,
                    "identity": {"head": HEAD, "policy": "f" * 64},
                    "error": None, "preference_error": None}
                if streamed:
                    report["final_observation"]["static_hlod"].update(
                        enabled=False, rendered_batches=0, replaced_primitives=0)
                else:
                    report["version"] = 1
                    del report["expected"]["procedural_streaming"]
                    del report["final_observation"]["procedural_streaming"]
                    del report["final_observation"]["static_hlod"]
                (result_dir / "module-runtime-report.json").write_text(json.dumps(report), encoding="utf-8")
                game = {"schema": "forge.native-gameplay.report", "version": 1, "pid": pid,
                        "source_hash": HEAD, "archive_hash": ARCHIVE, "functional_hash": FUNCTIONAL,
                        "exit_success": True, "ready": True, "last_command_error": None,
                        "simulation_error": None}
                (result_dir / "report.json").write_text(json.dumps(game), encoding="utf-8")
                events = [{"schema": "forge.native-gameplay.observation", "version": 1,
                           "qualification": "observations_only", "kind": "scenario", "pid": pid,
                           "source_hash": HEAD, "archive_hash": ARCHIVE,
                           "functional_hash": FUNCTIONAL, "plan_hash": args.scenario_sha256,
                           "step": index, "status": "completed", "detail": step}
                          for index, step in enumerate(plan()["steps"])]
                (result_dir / "observations.jsonl").write_text(
                    "".join(json.dumps(event) + "\n" for event in events), encoding="utf-8")
                for name in ("start.png", "outward.png", "return.png"):
                    (result_dir / name).write_bytes(png())
                return SimpleNamespace(pid=pid, wait=lambda timeout: 0)
            with mock.patch.object(probe.subprocess, "Popen", side_effect=simulate):
                probe.run(args)
            assessment = json.loads((args.output / "assessment.json").read_text(encoding="utf-8"))
            self.assertEqual([call[0] for call in calls], [False, True])
            self.assertEqual([call[2]["FORGE_QUALIFY_PROCEDURAL_STREAMING"] for call in calls],
                             ["0", "1"])
            self.assertEqual([run["mode"] for run in assessment["runs"]], ["complete", "streamed"])
            self.assertIsNone(assessment["runs"][0]["static_hlod"])
            self.assertEqual(assessment["route_max_deviation_m"], 0.0)
            self.assertFalse(assessment["tidewater_comparison"])
            self.assertFalse(assessment["performance_qualified"])
            self.assertEqual(calls[0][1][0], calls[1][1][0])
            for flag in ("--module", "--zone", "--expected-head", "--expected-archive-sha256",
                         "--expected-package-manifest-sha256", "--default-quality"):
                self.assertEqual(*(call[1][call[1].index(flag) + 1] for call in calls))
            self.assertEqual(calls[0][2]["WGPU_BACKEND"], calls[1][2]["WGPU_BACKEND"])
            self.assertNotEqual(calls[0][2]["TEMP"], calls[1][2]["TEMP"])


if __name__ == "__main__":
    unittest.main()
