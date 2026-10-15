"""Adversarial checks for reading executed contracts, without Cargo or GPU."""
from pathlib import Path
import importlib.util
import copy
import json
import tempfile
import unittest
import hashlib
import argparse
import contextlib
import io
import os
import subprocess
from unittest import mock

PATH = Path(__file__).resolve().parents[2] / "Validate-Gate3NativeGameplay.py"
SPEC = importlib.util.spec_from_file_location("native_gameplay_reader", PATH)
READER = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(READER)
RUNNER_PATH = Path(__file__).resolve().parents[2] / "Run-Gate3NativeGameplay.py"
RUNNER_SPEC = importlib.util.spec_from_file_location("native_gameplay_runner", RUNNER_PATH)
RUNNER = importlib.util.module_from_spec(RUNNER_SPEC)
RUNNER_SPEC.loader.exec_module(RUNNER)


def block(status="ok", completed=True, source="tests/environment_contract.rs"):
    summary = "test result: ok. 1 passed; 0 failed; 0 ignored;" if completed else ""
    return (f"     Running {source} (E:/cache/environment_contract-abcd.exe)\n"
            f"test real_water_extinguishes ... {status}\n{summary}\n")


class ContractCoverageTests(unittest.TestCase):
    def setUp(self):
        self.required = {"environment_contract": ("real_water_extinguishes",)}

    def test_completed_executed_test_has_its_binary_identity(self):
        result = READER.contract_coverage(block(source="tests\\environment_contract.rs"), self.required)
        self.assertEqual(result[0]["binary"], "E:/cache/environment_contract-abcd.exe")
        self.assertEqual(result[0]["passed_tests"], ["real_water_extinguishes"])

    def test_ignored_or_failed_required_test_is_refused(self):
        for status in ("ignored", "FAILED", "ignored, private fixture unavailable"):
            with self.subTest(status=status), self.assertRaises(ValueError):
                READER.contract_coverage(block(status), self.required)

    def test_partial_run_cannot_be_adopted(self):
        with self.assertRaises(ValueError):
            READER.contract_coverage(block(completed=False), self.required)

    def test_same_name_in_another_suite_cannot_close_the_contract(self):
        with self.assertRaises(ValueError):
            READER.contract_coverage(block(source="tests/unrelated.rs"), self.required)

    def test_duplicate_suite_or_duplicate_test_is_refused(self):
        for text in (block() + block(), block().replace("test result:",
                     "test real_water_extinguishes ... ok\ntest result:")):
            with self.subTest(text=text), self.assertRaises(ValueError):
                READER.contract_coverage(text, self.required)

    def test_bare_pass_without_cargo_execution_is_refused(self):
        with self.assertRaises(ValueError):
            READER.contract_coverage("test real_water_extinguishes ... ok\n", self.required)

    def test_summary_cannot_claim_unobserved_test_results(self):
        for text in (block().replace("1 passed", "0 passed"),
                     block().replace("1 passed", "2 passed")):
            with self.subTest(text=text), self.assertRaises(ValueError):
                READER.contract_coverage(text, self.required)

    def test_library_contract_requires_its_crate_binary(self):
        required = {"forge_engine::lib": ("real_water_extinguishes",)}
        text = block(source="unittests src/lib.rs").replace(
            "environment_contract-abcd.exe", "forge_engine-abcd.exe")
        self.assertEqual(READER.contract_coverage(text, required)[0]["suite"], "forge_engine::lib")
        with self.assertRaises(ValueError):
            READER.contract_coverage(text.replace("forge_engine-abcd", "forge_gameplay-abcd"), required)


class NativeObservationTests(unittest.TestCase):
    def setUp(self):
        self.reader = READER.OBSERVATION_READER
        self.common = {"schema": "forge.native-gameplay.observation", "version": 1,
                       "qualification": "observations_only", "pid": 41,
                       "archive_hash": "a" * 64, "source_hash": "b" * 64,
                       "functional_hash": "c" * 64}
        self.object = {"position": {"x": 0, "y": 1, "z": 0}, "released": False,
                       "in_motion": False, "burning": False, "destroyed": False, "initiator": None}
        self.state = {"tick": 0, "sequence": 0, "actors": {},
                      "objects": {"box": copy.deepcopy(self.object)}, "quests": {}}
        self.boot = {**self.common, "kind": "boot", "resumed": False, "saved": None,
                     "state": copy.deepcopy(self.state)}
        self.frame = {**self.common, "kind": "frame", "tick": 1, "sequence": 0,
                      "elapsed_ms": 10, "ready": True, "error": None,
                      "actors": {}, "objects": {"box": copy.deepcopy(self.object)}, "quests": {},
                      "last_action_receipt": None, "last_events": [], "navigation": [], "npc_motion": [],
                      "last_frame": {"frame": {"source_hash": "b" * 64, "tick": 1,
                                                "contacts": [], "heat": [], "danger": []}, "events": []}}
        self.exit = {**self.common, "kind": "exit", "tick": 1, "sequence": 0,
                     "elapsed_ms": 20, "exit_success": True,
                     "observation_error": None, "simulation_error": None}

    def read(self, rows):
        with tempfile.TemporaryDirectory() as folder:
            path = Path(folder) / "observations.jsonl"
            path.write_text("".join(json.dumps(row) + "\n" for row in rows), encoding="utf-8")
            return self.reader.read_rows(path, "a" * 64, "b" * 64)[0]

    def test_valid_stream_is_only_an_observation_and_has_no_capability_by_readiness(self):
        result = self.reader.assess(self.read([self.boot, self.frame, self.exit]))
        self.assertEqual(result["ready_frames"], 1)
        self.assertFalse(any(result["checks"].values()))

    def test_truncated_or_missing_terminal_stream_is_refused(self):
        with self.assertRaises(ValueError):
            self.read([self.boot, self.frame])
        with tempfile.TemporaryDirectory() as folder:
            path = Path(folder) / "truncated.jsonl"
            path.write_text(json.dumps(self.boot), encoding="utf-8")
            with self.assertRaises(ValueError):
                self.reader.read_rows(path, "a" * 64, "b" * 64)

    def test_quota_failure_or_simulation_failure_is_refused(self):
        for field in ("observation_error", "simulation_error"):
            failed = {**self.exit, field: "recording incomplete"}
            with self.subTest(field=field), self.assertRaises(ValueError):
                self.read([self.boot, self.frame, failed])

    def test_a_ready_frame_with_a_stale_physics_source_is_refused(self):
        frame = copy.deepcopy(self.frame)
        frame["last_frame"]["frame"]["source_hash"] = "d" * 64
        with self.assertRaises(ValueError):
            self.read([self.boot, frame, self.exit])

    def test_pid_or_archive_identity_cannot_change_within_a_stream(self):
        for change in ({"pid": 42}, {"archive_hash": "d" * 64}, {"source_hash": "d" * 64}):
            with self.subTest(change=change), self.assertRaises(ValueError):
                self.read([self.boot, {**self.frame, **change}, self.exit])

    def test_a_recorded_frame_cannot_move_backwards_or_repeat(self):
        for update in ({"tick": 0}, {"elapsed_ms": 9}, {}):
            with self.subTest(update=update), self.assertRaises(ValueError):
                self.read([self.boot, self.frame, {**self.frame, **update}, self.exit])

    def test_non_finite_or_duplicate_json_is_refused(self):
        for data in ('{"ready":true,"ready":false}', '{"elapsed_ms":NaN}',
                     '{"elapsed_ms":Infinity}', '{"elapsed_ms":1e999}'):
            with self.subTest(data=data), self.assertRaises(ValueError):
                self.reader.strict_json(data)

    def test_accepting_a_queued_throw_is_not_a_committed_throw(self):
        command = {**self.common, "kind": "command", "tick": 1, "sequence": 0,
                   "elapsed_ms": 15, "accepted": True, "error": None,
                   "storage_observation_error": None, "saved": None,
                   "command": {"kind": "action", "action": {"kind": "throw", "object": "box"}}}
        result = self.reader.assess(self.read([self.boot, self.frame, command, self.exit]))
        self.assertFalse(result["checks"]["throw"])

    def test_throw_requires_impulse_and_physical_movement_and_initiator(self):
        frame = copy.deepcopy(self.frame)
        frame["sequence"] = 1
        frame["objects"]["box"].update(released=True, initiator="player",
                                         position={"x": 0.2, "y": 1, "z": 0})
        frame["last_action_receipt"] = {"tick": 1, "sequence": 1, "events": [
            {"kind": "impulse", "object": "box", "impulse": {"x": 2, "y": 0, "z": 0}}],
            "request": {"command_id": "one", "expected_sequence": 0, "actor": "player",
                        "action": {"kind": "throw", "object": "box"}}}
        result = self.reader.assess([self.boot, frame, {**self.exit, "sequence": 1}])
        self.assertTrue(result["checks"]["throw"])
        frame["objects"]["box"]["initiator"] = "other_actor"
        result = self.reader.assess([self.boot, frame, {**self.exit, "sequence": 1}])
        self.assertFalse(result["checks"]["throw"])

    def test_an_unready_stream_cannot_claim_gameplay(self):
        frame = {**self.frame, "ready": False}
        rows = self.read([self.boot, frame, self.exit])
        with self.assertRaises(ValueError):
            self.reader.assess(rows)

    def test_resume_requires_saved_identity(self):
        boot = {**self.boot, "resumed": True}
        with self.assertRaises(ValueError):
            self.read([boot, self.frame, self.exit])

    def test_saved_bytes_resume_exactly_in_a_new_process_after_explicit_request(self):
        with tempfile.TemporaryDirectory() as folder:
            path = Path(folder) / "authoring.json"
            envelope = {"schema": "forge.game-save", "version": 1, "state_hash": "d" * 64,
                        "source_hash": "b" * 64, "functional_hash": "c" * 64,
                        "state": copy.deepcopy(self.state)}
            raw = json.dumps(envelope).encode("utf-8")
            path.write_bytes(raw)
            meta = {key: envelope[key] for key in ("state_hash", "source_hash", "functional_hash")}
            meta.update(path=str(path), sha256=hashlib.sha256(raw).hexdigest())
            save = {**self.common, "kind": "command", "accepted": True, "tick": 0, "sequence": 0,
                    "saved": meta, "command": {"kind": "save"}}
            request = {**save, "command": {"kind": "resume_next_launch"}}
            resumed = {**self.boot, "pid": 42, "resumed": True, "saved": meta}
            result = self.reader.verify_saved_observations(
                [[self.boot, save, request], [resumed]], [path], lambda p: None)
            self.assertTrue(result["save_new_process_resume"])
            self.assertEqual(result["new_process_resume_pairs"][0]["resumed_pid"], 42)
            changed = copy.deepcopy(resumed)
            changed["state"]["sequence"] = 9
            with self.assertRaises(ValueError):
                self.reader.verify_saved_observations(
                    [[self.boot, save, request], [changed]], [path], lambda p: None)

    def test_an_overwritten_save_without_a_retained_capture_is_refused(self):
        with tempfile.TemporaryDirectory() as folder:
            path = Path(folder) / "authoring.json"
            path.write_text("changed saved bytes", encoding="utf-8")
            saved = {"path": str(path), "sha256": "e" * 64, "state_hash": "d" * 64,
                     "source_hash": "b" * 64, "functional_hash": "c" * 64}
            row = {**self.common, "kind": "command", "accepted": True, "saved": saved,
                   "command": {"kind": "save"}}
            with self.assertRaises(ValueError):
                self.reader.verify_saved_observations([[self.boot, row]], [], lambda p: None)

    def test_pinned_scenario_requires_all_steps_and_normal_terminal_exit(self):
        marker = {**self.common, "kind": "scenario", "plan_hash": "f" * 64,
                  "step": 0, "status": "prepared", "elapsed_ms": 1, "detail": {"steps": 2}}
        completed = {**marker, "status": "completed", "elapsed_ms": 11, "detail": {"kind": "wait_ready"}}
        exited = {**completed, "step": 1, "elapsed_ms": 12, "detail": {"kind": "exit"}}
        self.read([self.boot, marker, self.frame, completed, exited, self.exit])
        with self.assertRaises(ValueError):
            self.read([self.boot, marker, self.frame, completed, self.exit])
        with self.assertRaises(ValueError):
            self.read([self.boot, marker, self.frame, {**completed, "status": "failed"}, self.exit])

    def guard_rows(self, ack_ms=900, ack_sequence=7, movement=0.1):
        command = {"kind": "command", "accepted": True, "sequence": 0, "elapsed_ms": 0,
                   "command": {"kind": "action", "action": {"kind": "ignite", "object": "fire"}}}
        frame = {"kind": "frame", "ready": True, "elapsed_ms": 10, "tick": 1,
                 "actors": {"player": {"position": {"x": 1, "y": 0, "z": 0}, "vitality": 99}},
                 "objects": {"fire": {"position": {"x": 0, "y": 0, "z": 0}, "burning": True}},
                 "last_action_receipt": {"request": {"command_id": "ignite-one", "expected_sequence": 0,
                    "actor": "player", "action": {"kind": "ignite", "object": "fire"}},
                    "events": [{"kind": "burning", "object": "fire", "active": True}]},
                 "last_frame": {"frame": {"heat": [{"object": "fire", "actor": "player", "line_of_sight": True}]},
                                "events": [{"kind": "damage", "target": "player", "contact": False, "amount": 1}]}}
        submitted = {"kind": "scenario", "status": "input_submitted", "elapsed_ms": 20,
                     "detail": {"submitted_sequence": 7, "move_axis": [1, 0]}}
        ack = {**copy.deepcopy(frame), "elapsed_ms": ack_ms, "tick": 2,
               "player": {"last_applied_sequence": ack_sequence, "effective_translation": [movement, 0, 0]}}
        return [command, frame, submitted, ack]

    def test_fire_guard_requires_actual_sequence_ack_and_away_movement(self):
        self.assertTrue(self.reader.fire_retreat_guards(self.guard_rows())[0]["within_budget"])
        for change in ({"ack_sequence": 8}, {"movement": 0}, {"movement": -0.1}):
            with self.subTest(change=change):
                result = self.reader.fire_retreat_guards(self.guard_rows(**change))[0]
                self.assertFalse(result["native_input_ack_observed"])
                self.assertFalse(result["within_budget"])

    def test_submitted_input_without_any_native_ack_cannot_close_the_fire_guard(self):
        result = self.reader.fire_retreat_guards(self.guard_rows()[:-1])[0]
        self.assertFalse(result["native_input_ack_observed"])
        self.assertFalse(result["within_budget"])

    def test_fire_guard_does_not_relax_1000ms_by_using_a_later_ignition_observation(self):
        rows = self.guard_rows(ack_ms=1100)
        rows[1]["elapsed_ms"] = 300
        result = self.reader.fire_retreat_guards(rows)[0]
        self.assertEqual(result["delay_from_observed_ignition_ms"], 800)
        self.assertEqual(result["upper_delay_ms"], 1100)
        self.assertFalse(result["within_budget"])

    def test_queued_ignition_without_committed_burning_is_not_an_ignition(self):
        rows = self.guard_rows()
        rows[1]["last_action_receipt"]["events"] = []
        rows[3]["last_action_receipt"]["events"] = []
        self.assertEqual(self.reader.fire_retreat_guards(rows), [])

    def test_missing_accepted_command_start_keeps_temporal_evidence_incomplete(self):
        result = self.reader.fire_retreat_guards(self.guard_rows()[1:])[0]
        self.assertFalse(result["within_budget"])


    def test_fire_ai_requires_heat_and_damage_from_the_same_burning_danger_source(self):
        boot, frame = copy.deepcopy(self.boot), copy.deepcopy(self.frame)
        boot["state"]["actors"]["watcher"] = {"position": {"x": 0, "y": 0, "z": 0}, "vitality": 100}
        boot["state"]["objects"]["fire"] = {**self.object, "position": {"x": 1, "y": 0, "z": 0}}
        frame["actors"]["watcher"] = {"position": {"x": -.1, "y": 0, "z": 0}, "vitality": 99, "fleeing": True}
        frame["objects"]["fire"] = {**boot["state"]["objects"]["fire"], "burning": True}
        frame["sequence"] = 1
        frame["npc_motion"] = [{"actor": "watcher", "grounded": True, "feedback_current": True, "effective_translation": [-.1, 0, 0]}]
        frame["navigation"] = [{"actor": "watcher", "grounded": True, "feedback_tick": 1}]
        exposure = {"actor": "watcher", "object": "fire", "line_of_sight": True}
        frame["last_frame"]["frame"]["danger"] = [exposure]
        frame["last_action_receipt"] = {"request": {"command_id": "ignite", "expected_sequence": 0, "actor": "player",
            "action": {"kind": "ignite", "object": "fire"}}, "sequence": 1, "tick": 1,
            "events": [{"kind": "burning", "object": "fire", "active": True}]}
        checks = self.reader.assess([boot, frame])["checks"]
        self.assertTrue(checks["danger_ai"])
        self.assertFalse(checks["fire_danger_ai"])
        frame["last_frame"]["frame"]["heat"] = [exposure]
        frame["last_frame"]["events"] = [{"kind": "damage", "target": "watcher", "contact": False, "amount": 1}]
        self.assertTrue(self.reader.assess([boot, frame])["checks"]["fire_danger_ai"])
        frame["last_frame"]["frame"]["heat"] = [{**exposure, "object": "another-fire"}]
        self.assertFalse(self.reader.assess([boot, frame])["checks"]["fire_danger_ai"])


class NativeRunnerInputTests(unittest.TestCase):
    class Shared:
        @staticmethod
        def physical_path(path, root):
            if path.is_symlink():
                raise ValueError("Link refused")

        @staticmethod
        def sha(path):
            return hashlib.sha256(path.read_bytes()).hexdigest()

    def setUp(self):
        self.shared = self.Shared()

    def plan(self):
        return {"schema": "forge.native-gameplay.scenario", "version": 1,
                "archive_hash": "a" * 64, "source_hash": "b" * 64, "max_duration_s": 60,
                "steps": [{"kind": "wait_ready", "timeout_s": 30}, {"kind": "exit"}]}

    def read(self, plan):
        with tempfile.TemporaryDirectory() as folder:
            path = Path(folder) / "scenario.json"
            path.write_text(json.dumps(plan), encoding="utf-8")
            return RUNNER.read_plan(path, "a" * 64, "b" * 64, self.shared)

    def test_valid_plan_retains_exact_bytes_and_hash(self):
        raw, pin, plan = self.read(self.plan())
        self.assertEqual(pin, hashlib.sha256(raw).hexdigest())
        self.assertEqual(plan["max_duration_s"], 60)

    def test_real_menu_operation_is_allowed(self):
        plan = self.plan()
        plan["steps"].insert(1, {"kind": "menu", "open": True, "timeout_s": 30})
        self.assertEqual(self.read(plan)[2]["steps"][1]["kind"], "menu")

    def report(self):
        return {"schema": "forge.native-gameplay.report", "version": 1, "pid": 41,
                "source_hash": "b" * 64, "archive_hash": "a" * 64, "functional_hash": "c" * 64,
                "exit_success": True, "ready": True, "tick": 1, "sequence": 0, "quests": [],
                "inventory_total": 0, "health": 100, "stamina": 100, "feedback": "Prêt",
                "saved": None, "last_command_error": None, "simulation_error": None, "resume_refused": None}

    def test_report_requires_complete_pinned_bounded_contract(self):
        with tempfile.TemporaryDirectory() as folder:
            path = Path(folder) / "report.json"
            valid = self.report()
            path.write_text(json.dumps(valid), encoding="utf-8")
            pin = RUNNER.read_report(path, 41, "b" * 64, "a" * 64, "c" * 64, self.shared)
            self.assertEqual(pin["sha256"], self.shared.sha(path))
            malformed = [{k: v for k, v in valid.items() if k != "simulation_error"},
                         {**valid, "pid": 42}, {**valid, "resume_refused": "source changed"},
                         {**valid, "health": True}, {**valid, "feedback": "é" * 513}]
            for report in malformed:
                path.write_text(json.dumps(report), encoding="utf-8")
                with self.subTest(report=report), self.assertRaises(ValueError):
                    RUNNER.read_report(path, 41, "b" * 64, "a" * 64, "c" * 64, self.shared)
            path.write_text(json.dumps(valid)[:-1] + ',"ready":true}', encoding="utf-8")
            with self.assertRaises(ValueError):
                RUNNER.read_report(path, 41, "b" * 64, "a" * 64, "c" * 64, self.shared)

    def test_resume_copy_preserves_exact_bytes_and_only_owns_new_destination(self):
        base = NativeObservationTests()
        base.setUp()
        module, area = "0439cbbd-65bb-55d4-aeb4-b735f22504a7", "e620dd19-ead9-5913-a6d6-0f93109148c0"
        game = {"key": "native-game-release", "sha256": "d" * 64}
        fixture = {"module_id": module, "area_id": area, "functional_hash": "c" * 64, "head_sha256": "f" * 64}
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            prior = root / "prior"
            saved_root = prior / "user-data/game-saves" / module / area
            saved_root.mkdir(parents=True)
            envelope = {"schema": "forge.game-save", "version": 1, "module_id": module, "area_id": area,
                        "source_hash": "b" * 64, "functional_hash": "c" * 64, "state_hash": "e" * 64, "state": base.state}
            saved = json.dumps(envelope).encode()
            pin = hashlib.sha256(saved).hexdigest()
            request = {"schema": "forge.native-game-resume", "version": 1, "module": module, "area": area,
                       "saved_hash": pin, "allow_compatible_edits": True}
            (saved_root / "authoring.json").write_bytes(saved)
            (saved_root / "resume.json").write_text(json.dumps(request), encoding="utf-8")
            (saved_root / "authoring.lock").write_bytes(b"do not copy")
            meta = {"path": str(saved_root / "authoring.json"), "sha256": pin, "state_hash": "e" * 64,
                    "source_hash": "b" * 64, "functional_hash": "c" * 64}
            save = {**base.common, "kind": "command", "accepted": True, "error": None,
                    "storage_observation_error": None, "tick": 0, "sequence": 0, "elapsed_ms": 1,
                    "saved": meta, "command": {"kind": "save"}}
            resume = {**save, "elapsed_ms": 2, "command": {"kind": "resume_next_launch", "allow_compatible_edits": True}}
            prepared = {**base.common, "kind": "scenario", "plan_hash": "f" * 64, "step": 0,
                        "status": "prepared", "elapsed_ms": 0, "detail": {"steps": 2}}
            complete = {**prepared, "status": "completed", "elapsed_ms": 11, "detail": {"kind": "wait_ready"}}
            exited = {**complete, "step": 1, "elapsed_ms": 12, "detail": {"kind": "exit"}}
            observation = prior / "observations.jsonl"
            observation.write_text("".join(json.dumps(row) + "\n" for row in
                [base.boot, prepared, save, resume, base.frame, complete, exited, base.exit]), encoding="utf-8")
            report_path = prior / "report.json"
            report_path.write_text(json.dumps(self.report()), encoding="utf-8")
            report_pin = {"path": str(report_path), "sha256": self.shared.sha(report_path)}
            result = {"schema": "forge.gate3.native-gameplay-campaign.v1", "status": "NATIVE_PROCESSES_CLOSED",
                      "source_unchanged": True, "runs": [{"pid": 41, "executable": game, "exit_code": 0,
                      "forced_stop": False, "observation": str(observation), "scenario_sha256": "f" * 64, "game_report": report_pin}]}
            result_path = prior / "result.json"
            result_path.write_text(json.dumps(result), encoding="utf-8")
            (prior / "inputs.json").write_text(json.dumps({"native_game": game, "source_fingerprint_sha256": "source-one",
                "head_sha256": "b" * 64, "snapshot_files": {"zone.json": "a" * 64}}), encoding="utf-8")
            copied = RUNNER.copy_resume_source(result_path, root / "new", game, "source-one", fixture, self.shared)
            self.assertFalse(copied["synthesized_save_or_request"])
            self.assertEqual((root / "new" / module / area / "authoring.json").read_bytes(), saved)
            self.assertEqual(len(list((root / "new").rglob("*.*"))), 2)
            self.assertTrue((saved_root / "authoring.lock").exists())
            if os.name == "nt":
                result["runs"][0]["observation"] = "\\\\?\\" + str(observation)
                result["runs"][0]["game_report"]["path"] = "\\\\?\\" + str(report_path)
                result_path.write_text(json.dumps(result), encoding="utf-8")
                copied_alias = RUNNER.copy_resume_source(Path("\\\\?\\" + str(result_path)), root / "alias-copy",
                    game, "source-one", fixture, self.shared)
                self.assertFalse(copied_alias["synthesized_save_or_request"])
                self.assertEqual((root / "alias-copy" / module / area / "authoring.json").read_bytes(), saved)
            with self.assertRaises(ValueError):
                RUNNER.copy_resume_source(result_path, root / "wrong-build", game, "source-two", fixture, self.shared)
            observation.write_text("".join(json.dumps(row) + "\n" for row in
                [base.boot, prepared, save, base.frame, complete, exited, base.exit]), encoding="utf-8")
            with self.assertRaises(ValueError):
                RUNNER.copy_resume_source(result_path, root / "missing-request", game, "source-one", fixture, self.shared)

    def test_different_snapshot_or_unbounded_plan_is_refused_before_launch(self):
        for change in ({"source_hash": "c" * 64}, {"max_duration_s": 601}, {"max_duration_s": True}):
            with self.subTest(change=change), self.assertRaises(ValueError):
                self.read({**self.plan(), **change})

    def test_missing_ready_or_exit_or_unsupported_operation_is_refused(self):
        for steps in ([{"kind": "exit"}], [{"kind": "wait_ready"}],
                      [{"kind": "wait_ready"}, {"kind": "shell", "cmd": "ignored"}, {"kind": "exit"}]):
            with self.subTest(steps=steps), self.assertRaises(ValueError):
                self.read({**self.plan(), "steps": steps})

    def test_recipe_uses_snapshot_and_pinned_buildings_in_normal_walk(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            data = root / "data"
            (data / "scene").mkdir(parents=True)
            (data / "buildings").mkdir()
            (data / "scene" / "static-assets.json").write_bytes(b"{}")
            (data / "buildings" / "package.json").write_bytes(b"{}")
            snapshot = root / "snapshot"
            snapshot.mkdir()
            (snapshot / "zone.json").write_bytes(b"test data only")
            executable = root / "tidewater_plant_probe.exe"
            profile = {"game": {"content_root": str(root), "executable": str(executable)},
                       "module_assets": {"root": str(data / "scene"), "catalog": "static-assets.json",
                                         "catalog_sha256": self.shared.sha(data / "scene" / "static-assets.json")}}
            args, env, cwd = RUNNER.recipe(profile, snapshot, root / "game.png", "a" * 64,
                                          executable, self.shared)
            self.assertEqual(args[-1], "--walk")
            self.assertEqual(args[8:11], ["--studio-snapshot", str(snapshot), "a" * 64])
            self.assertEqual(args[11:14], ["--building-package", str(data / "buildings"),
                                         self.shared.sha(data / "buildings" / "package.json")])
            self.assertEqual(cwd, root)
            self.assertEqual(env["FORGE_TIDEWATER_GAME_FISH_ROOT"], str(root / "content" / "fish" / "game"))
            profile["game"]["executable"] = str(root / "another.exe")
            with self.assertRaises(ValueError):
                RUNNER.recipe(profile, snapshot, root / "game.png", "a" * 64, executable, self.shared)

    @unittest.skipUnless(os.name == "nt", "Real Windows extended-path regression")
    def test_capture_and_saved_rust_aliases_preserve_exact_bytes_and_metadata(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            run = root / "run-1"
            native = run / "native-output"
            native.mkdir(parents=True)
            save_root = root / "user-data/game-saves"
            original = save_root / "module/area/authoring.json"
            original.parent.mkdir(parents=True)
            original.write_bytes(b"a newer committed Save")
            raw = b'{"schema":"forge.game-save","state":{"sequence":12}}'
            pin = hashlib.sha256(raw).hexdigest()
            retained = native / ("saved-" + pin + ".json")
            retained.write_bytes(raw)
            destination = run / "saved-captures"
            destination.mkdir()
            original_path = "\\\\?\\" + str(original)
            retained_path = "\\\\?\\" + str(retained)
            self.assertTrue(original.samefile(Path(original_path)))
            self.assertFalse(Path(original_path).resolve().is_relative_to(save_root.resolve()))
            rows = [{"kind": "scenario", "status": "save_copied", "detail": {"path": retained_path, "sha256": pin}},
                    {"kind": "command", "accepted": True, "command": {"kind": "save"},
                     "saved": {"path": original_path, "sha256": pin}}]
            observation = run / "observations.jsonl"
            observation.write_text("".join(json.dumps(row) + "\n" for row in rows), encoding="utf-8")
            result = RUNNER.retain_saves(observation, destination, save_root, self.shared)
            self.assertEqual(Path(result[0]["path"]).read_bytes(), raw)
            self.assertEqual(result[0]["original_path"], original_path)
            self.assertEqual(original.read_bytes(), b"a newer committed Save")

    def test_owned_runtime_paths_refuse_sibling_traversal_and_physical_links(self):
        with tempfile.TemporaryDirectory() as folder:
            base = Path(folder)
            root, sibling = base / "owned", base / "owned-sibling"
            root.mkdir()
            sibling.mkdir()
            file = sibling / "saved.json"
            file.write_bytes(b"outside")
            refused = [file, root / ".." / sibling.name / file.name]
            if os.name == "nt":
                refused += [Path("\\\\?\\" + str(file)), Path("\\\\?\\GLOBALROOT\\Device\\HarddiskVolume1\\saved.json"),
                            Path("\\\\.\\GLOBALROOT\\Device\\HarddiskVolume1\\saved.json")]
            for path in refused:
                with self.subTest(path=str(path)), self.assertRaises(ValueError):
                    RUNNER.owned_runtime_path(path, root, self.shared)
            internal = root / "owned.json"
            internal.write_bytes(b"owned")
            physical = mock.Mock(side_effect=ValueError("reparse rejected"))
            shared = argparse.Namespace(physical_path=physical)
            with self.assertRaises(ValueError):
                RUNNER.owned_runtime_path(internal, root, shared)
            physical.assert_called_once_with(internal, root)

    def test_retained_capture_and_saved_paths_cannot_escape_or_adopt_changed_bytes(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            native = root / "run-1/native-output"
            native.mkdir(parents=True)
            destination = root / "run-1/captures"
            destination.mkdir()
            save_root = root / "game-saves"
            save_root.mkdir()
            outside = root / "outside.json"
            outside.write_bytes(b"outside")
            owned = save_root / "authoring.json"
            owned.write_bytes(b"changed saved bytes")
            observation = root / "run-1/observations.jsonl"
            for rows in ([{"kind": "scenario", "status": "save_copied", "detail": {"path": str(outside), "sha256": "a" * 64}}],
                         [{"kind": "command", "accepted": True, "command": {"kind": "save"},
                           "saved": {"path": str(outside), "sha256": "a" * 64}}],
                         [{"kind": "command", "accepted": True, "command": {"kind": "save"},
                           "saved": {"path": str(owned), "sha256": "a" * 64}}]):
                observation.write_text("".join(json.dumps(row) + "\n" for row in rows), encoding="utf-8")
                with self.subTest(rows=rows), self.assertRaises(ValueError):
                    RUNNER.retain_saves(observation, destination, save_root, self.shared)
                self.assertEqual(list(destination.iterdir()), [])


class NativeRunnerPreparationTests(unittest.TestCase):
    class Shared(NativeRunnerInputTests.Shared):
        def __init__(self, target, executable):
            self.target, self.executable = target, executable

        @staticmethod
        def source_inputs():
            return {"synthetic_unit_validation": "fixture-only"}

        def release_artifact_paths(self, target):
            return {"native-game-release": self.executable}

        def release_artifact_pin(self, path, target):
            return {"path": str(path), "sha256": self.sha(path), "bytes": path.stat().st_size}

        def completed_receipt(self, evidence, mode, current):
            if mode != "release" or current != self.source_inputs():
                raise ValueError("Unit fixture release differs")
            return ({"target_dir": str(self.target), "fingerprint_sha256": "f" * 64},
                    {"artifacts": [self.release_artifact_pin(self.executable, self.target)]})

    def exercise(self, root, registration_error=None, graceful_timeout=False):
        for name in ("snapshot", "original", "authoring", "evidence"):
            (root / name).mkdir()
        archive = root / "snapshot/zone.json"
        archive.write_bytes(b"synthetic unit data, not a Studio archive")
        (root / "original/zone.json").write_bytes(archive.read_bytes())
        (root / "authoring/workspace.json").write_bytes(b"unit authoring source")
        candidate = root / "target/release/examples/tidewater_plant_probe.exe"
        candidate.parent.mkdir(parents=True)
        candidate.write_bytes(b"unit pin, never executable")
        profile = root / "profile.json"
        profile.write_text(json.dumps({"authoring_root": str(root / "authoring"),
            "game": {"executable": str(candidate)}}), encoding="utf-8")
        provenance = root / "provenance.json"
        provenance.write_bytes(b"{}")
        shared = self.Shared(root / "target", candidate)
        archive_pin = shared.sha(archive)
        plan = {"schema": "forge.native-gameplay.scenario", "version": 1,
                "archive_hash": archive_pin, "source_hash": "b" * 64, "max_duration_s": 60,
                "steps": [{"kind": "wait_ready", "timeout_s": 30}, {"kind": "exit"}]}
        scenario = root / "scenario.json"
        scenario.write_text(json.dumps(plan), encoding="utf-8")
        args = argparse.Namespace(evidence=root / "evidence", profile=profile, snapshot=root / "snapshot",
            snapshot_provenance=provenance, output=root / "campaign", archive_sha256=archive_pin,
            head_sha256="b" * 64, scenario=[scenario], resume_from=None)
        fixture = {"source_snapshot": str(root / "original"), "functional_hash": "c" * 64}
        test = self
        children = []

        class FakePopen:
            def __init__(self, argv, **kwargs):
                env = kwargs["env"]
                output = Path(env["FORGE_NATIVE_GAMEPLAY_OUTPUT_ROOT"])
                # Assert the real runner's preparation before emitting any fake output.
                test.assertTrue(output.is_absolute() and output.is_dir())
                test.assertEqual(output.parent, args.output / "run-1")
                test.assertEqual(list(output.iterdir()), [])
                test.assertEqual(shared.sha(Path(env["FORGE_NATIVE_GAMEPLAY_SCENARIO_PATH"])),
                                 env["FORGE_NATIVE_GAMEPLAY_SCENARIO_SHA256"])
                self.pid = 41
                self.returncode = None if registration_error else 0
                self.terminate_calls = self.kill_calls = self.wait_calls = 0
                children.append(self)
                base = NativeObservationTests()
                base.setUp()
                rows = [base.boot, base.frame, base.exit]
                for row in rows:
                    row["archive_hash"] = archive_pin
                Path(env["FORGE_GAME_OBSERVATIONS"]).write_text(
                    "".join(json.dumps(row) + "\n" for row in rows), encoding="utf-8")
                report_test = NativeRunnerInputTests()
                report = report_test.report()
                report["archive_hash"] = archive_pin
                Path(env["FORGE_GAME_REPORT_PATH"]).write_text(json.dumps(report), encoding="utf-8")

            def poll(self):
                return self.returncode

            def terminate(self):
                self.terminate_calls += 1

            def kill(self):
                self.kill_calls += 1
                self.returncode = -9

            def wait(self, timeout=None):
                self.wait_calls += 1
                if graceful_timeout and not self.kill_calls:
                    raise subprocess.TimeoutExpired("fake child", timeout)
                self.returncode = self.returncode if self.returncode is not None else -15
                return self.returncode

        original_write = RUNNER.write_new

        def record(path, value):
            if registration_error and path.name == "process-started.json":
                raise registration_error("unit recording fault after Popen")
            original_write(path, value)

        with mock.patch.object(RUNNER, "driver", return_value=shared), \
             mock.patch.object(RUNNER, "fixture_provenance", return_value=fixture), \
             mock.patch.object(RUNNER, "recipe", return_value=([str(candidate)], {}, root)), \
             mock.patch.object(RUNNER.subprocess, "Popen", FakePopen), \
             mock.patch.object(RUNNER, "write_new", side_effect=record), \
             contextlib.redirect_stdout(io.StringIO()):
            if registration_error is KeyboardInterrupt:
                with self.assertRaises(KeyboardInterrupt):
                    RUNNER.run(args)
                result = None
            else:
                expected = 1 if registration_error else 0
                self.assertEqual(RUNNER.run(args), expected)
                result = json.loads((args.output / "result.json").read_bytes())
        self.assertEqual(len(children), 1)
        return result, children[0]

    def test_real_runner_creates_empty_native_output_before_spawn_and_keeps_source_invariants(self):
        with tempfile.TemporaryDirectory() as folder:
            result, child = self.exercise(Path(folder))
            self.assertEqual(result["status"], "NATIVE_PROCESSES_CLOSED")
            self.assertTrue(result["source_unchanged"])
            self.assertEqual(result["runs"][0]["pid"], 41)
            self.assertFalse(result["runs"][0]["forced_stop"])
            self.assertFalse(result["gameplay_observations_verified"])
            self.assertFalse(result["gpu_verified"])
            self.assertFalse(result["gate_qualified"])
            self.assertEqual(child.terminate_calls, 0)

    def test_failed_process_record_or_interrupt_closes_the_exact_owned_popen(self):
        for error in (OSError, KeyboardInterrupt):
            for graceful_timeout in (False, True):
                with self.subTest(error=error.__name__, kill_fallback=graceful_timeout), tempfile.TemporaryDirectory() as folder:
                    result, child = self.exercise(Path(folder), error, graceful_timeout)
                    self.assertEqual(child.terminate_calls, 1)
                    self.assertEqual(child.kill_calls, int(graceful_timeout))
                    self.assertEqual(child.wait_calls, 1 + int(graceful_timeout))
                    self.assertIsNotNone(child.poll())
                    if result is not None:
                        self.assertEqual(result["status"], "FAILED_CLOSED")
                        self.assertTrue(result["runs"][0]["forced_stop"])
                        self.assertFalse(result["gate_qualified"])


class NativeDocumentProofTests(unittest.TestCase):
    def setUp(self):
        self.reader = READER.OBSERVATION_READER
        point = {"x": 0, "y": 0, "z": 0}
        self.document = {"schema": "forge.gameplay", "version": 1, "module_id": "module", "area_id": "area",
            "player": "player", "profile": {"max_stamina": 100}, "actors": [
                {"id": "player", "role": "player", "position": point, "max_vitality": 100},
                {"id": "enemy", "role": "enemy", "position": point, "max_vitality": 100}],
            "objects": [{"id": "loot", "position": point, "kind": {"kind": "pickup", "item": "seal"}},
                        {"id": "door", "position": point, "kind": {"kind": "door", "initially_open": False}}],
            "quests": [{"id": "quest", "required_item": "seal", "reward_item": None, "reward_quantity": 0}],
            "places": [{"id": str(i), "links": [str((i+1) % 3)],
                        "position": {"x": 10*i, "y": 0, "z": 0}} for i in range(3)],
            "dialogues": [{"id": "dialogue", "nodes": [{"id": "node", "text": "Old text", "choices": []}]}]}

    def walkthrough(self, pid, environmental=False):
        state = self.reader.authored_initial_state(self.document)
        boot = {"pid": pid, "kind": "boot", "state": state, "resumed": False}
        rows = [boot]
        for i, place in enumerate(self.document["places"], 1):
            frame = {"kind": "frame", "ready": True, "tick": i, "player": {"grounded": True},
                     "actors": copy.deepcopy(state["actors"]), "objects": copy.deepcopy(state["objects"]),
                     "quests": {"quest": "completed"}, "last_action_receipt": None,
                     "last_frame": {"frame": {"contacts": [], "heat": []}, "events": []}}
            frame["actors"]["player"]["position"] = place["position"]
            frame["actors"]["enemy"]["vitality"] = 0
            frame["objects"]["loot"]["consumed"] = True
            damage = {"kind": "damage", "target": "enemy", "amount": 100, "contact": True}
            if environmental:
                frame["last_frame"] = {"frame": {"contacts": [{"target": "enemy"}], "heat": []}, "events": [damage]}
            else:
                frame["last_action_receipt"] = {"request": {"command_id": "attack", "action": {"kind": "attack", "target": "enemy"}},
                                                "events": [damage]}
            rows.append(frame)
        return rows

    def test_three_places_and_two_approaches_bind_the_same_authored_enemy_and_consumed_quest_loot(self):
        result = self.reader.assess_p01_p04([self.walkthrough(1), self.walkthrough(2, True)], self.document,
                                          {"save_new_process_resume": True})
        self.assertTrue(all(result["checks"].values()))
        self.assertFalse(result["studio_replay_selector_verified"])
        changed = self.walkthrough(2, True)
        changed[-1]["actors"]["player"]["inventory"]["seal"] = 1
        result = self.reader.assess_p01_p04([self.walkthrough(1), changed], self.document,
                                          {"save_new_process_resume": True})
        self.assertFalse(result["checks"]["p01_two_approaches"])

    def test_fresh_requires_every_initial_quest_object_and_journal_and_missing_place_is_not_accepted(self):
        for mutate in (lambda s: s["quests"].update(quest="completed"),
                       lambda s: s["objects"]["door"].update(open=True),
                       lambda s: s["objects"]["loot"].update(consumed=True),
                       lambda s: s["receipts"].append({"historical": True}),
                       lambda s: s["actors"]["player"].update(stamina=50)):
            rows = self.walkthrough(1)
            mutate(rows[0]["state"])
            self.assertFalse(self.reader.assess_p01_p04([rows], self.document,
                {"save_new_process_resume": True})["checks"]["p04_fresh_resume_isolation"])
        rows = self.walkthrough(1)[:3]
        self.assertFalse(self.reader.assess_p01_p04([rows], self.document,
            {"save_new_process_resume": True})["checks"]["p01_three_places"])

    def test_compatible_resume_only_rebinds_documented_fields_and_requires_explicit_request(self):
        with tempfile.TemporaryDirectory() as folder:
            path = Path(folder) / "saved.json"
            state = self.reader.authored_initial_state(self.document)
            state.update(sequence=9, tick=50, last_frame={"historical": True}, tick_events_used=3)
            state["quests"]["quest"] = "completed"
            state["objects"]["door"]["open"] = True
            envelope = {"schema": "forge.game-save", "version": 1, "source_hash": "old-head", "functional_hash": "same",
                        "state_hash": "state", "state": state}
            path.write_text(json.dumps(envelope), encoding="utf-8")
            meta = {"path": str(path), "sha256": hashlib.sha256(path.read_bytes()).hexdigest(), "state_hash": "state",
                    "source_hash": "old-head", "functional_hash": "same"}
            old = {"head": {"revision": 7, "corrections": {"gameplay": self.document}}}
            new_document = copy.deepcopy(self.document)
            new_document["dialogues"][0]["nodes"][0]["text"] = "Retouched text"
            new_document["objects"][1]["position"]["x"] = .05
            new = {"head": {"revision": 8, "corrections": {"gameplay": new_document}}}
            common = {"source_hash": "old-head", "archive_hash": "old-archive", "functional_hash": "same", "pid": 1}
            save = {**common, "kind": "command", "accepted": True, "command": {"kind": "save"},
                    "saved": meta, "tick": 50, "sequence": 9}
            request = {**save, "command": {"kind": "resume_next_launch", "allow_compatible_edits": True}}
            initial = [{**common, "kind": "boot", "resumed": False, "state": state}, save, request]
            expected = copy.deepcopy(state)
            expected.update(last_frame=None, tick_events_used=0, requires_physics_revalidation=True)
            expected["objects"]["door"]["position"] = new_document["objects"][1]["position"]
            boot = {**common, "source_hash": "new-head", "archive_hash": "new-archive", "pid": 2,
                    "kind": "boot", "resumed": True, "saved": meta, "state": expected}
            resumed = [boot, {"kind": "frame", "ready": True, "last_frame": {"frame": {"source_hash": "new-head"}}}]
            fixture = {"schema": "forge.native-gameplay.fixture", "version": 1, "status": "DATA_ONLY_APPLICATION_FIXTURE",
                       "compatible_retouch": True, "application_commands_used": True, "source_unchanged": True,
                       "copied_product_code": False, "studio_ui_verified": False, "archive_sha256": "new-archive",
                       "head_sha256": "new-head", "source_archive_sha256": "old-archive", "source_head_sha256": "old-head",
                       "functional_hash": "same", "authoring_receipt": {"applied": new["head"]["corrections"],
                        "request": {"expected_hash": "old-head"}, "resulting_hash": "new-head", "resulting_revision": 8}}
            def verify(a=initial, b=resumed):
                return self.reader.verify_compatible_retouch(a, b, old, new, fixture, [path], lambda p: None)
            self.assertTrue(verify()["compatible_retouch_preserved_state"])
            changed = copy.deepcopy(resumed)
            changed[0]["state"]["quests"]["quest"] = "available"
            with self.assertRaises(ValueError):
                verify(b=changed)
            refused = copy.deepcopy(initial)
            refused[-1]["command"]["allow_compatible_edits"] = False
            with self.assertRaises(ValueError):
                verify(a=refused)


if __name__ == "__main__":
    unittest.main()
