"""Read authenticated Gate 3 gameplay contracts; never build or start the game.

Headless coverage is deliberately distinct from observations of the native game.
The shared validator owns source, log and binary provenance; this reader only
checks that the behavioural contracts actually ran in that exact native suite.
"""

from pathlib import Path
import argparse
import hashlib
import importlib.util
import json
import re
import sys

sys.dont_write_bytecode = True
READER_SHA256 = hashlib.sha256(Path(__file__).read_bytes()).hexdigest()
OBSERVATION_HELPER_PATH = Path(__file__).parent / "tidewater" / "gate3_native_observations.py"
OBSERVATION_SPEC = importlib.util.spec_from_file_location("native_observation_reader", OBSERVATION_HELPER_PATH)
OBSERVATION_READER = importlib.util.module_from_spec(OBSERVATION_SPEC)
OBSERVATION_SPEC.loader.exec_module(OBSERVATION_READER)
require_observations = (*OBSERVATION_READER.CAPABILITIES, "save_new_process_resume",
                        "p01_three_places", "p01_two_approaches", "p01_completed_loot_quest", "p04_fresh_resume_isolation",
                        "compatible_retouch_preserved_state")

# These are existing behavioural tests, not newly generated copies of gameplay.
# A listed test must be executed successfully, rather than filtered or ignored.
CONTRACTS = {
    "forge_engine::lib": (
        "native_gameplay::tests::native_archive_load_is_generic_and_preserves_authored_bytes",
        "native_gameplay::tests::native_save_is_separate_and_resume_requires_an_explicit_request",
        "native_gameplay::tests::native_resume_rejects_a_changed_save_after_the_request",
        "native_gameplay::tests::native_action_candidates_follow_current_positions_and_queue_readiness",
        "native_gameplay::tests::native_compatible_resume_remains_explicit_and_rejects_functional_changes",
    ),
    "action_contract": (
        "pickup_is_atomic_and_exact_retry_cannot_duplicate_inventory",
        "blocked_or_distant_pickup_leaves_the_entire_state_unchanged",
        "reused_command_identity_with_new_parameters_is_refused",
        "save_reopens_consumed_item_and_keeps_the_source_immutable",
    ),
    "document_contract": (
        "duplicate_actor_and_cross_category_ids_are_rejected",
        "nonexistent_dialogue_is_an_actionable_reference_error",
        "valid_authored_document_round_trips_without_engine_handles",
    ),
    "environment_contract": (
        "support_break_releases_charge_and_preserves_initiator_through_impact_and_resume",
        "fire_damage_and_visible_danger_stop_after_observed_water_extinguishes_source",
        "compatible_resume_requires_current_geometry_before_actions_or_save",
        "resting_contact_is_observable_without_damage_and_stale_frames_are_atomic",
        "duplicate_contacts_and_cascades_over_quota_reject_entire_frame",
        "saved_last_frame_must_match_its_source_even_after_rehash",
    ),
    "first_loop": (
        "first_loop_accepts_quest_picks_up_seal_and_turns_it_in",
        "exact_turn_in_retry_is_idempotent_and_cannot_duplicate_reward",
        "exhausted_stamina_refuses_attack_atomically_and_survives_resume",
        "full_inventory_rolls_back_pickup_and_keeps_the_quest_object",
        "save_resume_uses_a_new_session_and_keeps_authored_source_immutable",
        "text_and_door_pose_edits_require_explicit_compatibility_acceptance",
        "removed_references_and_functional_changes_are_rejected",
        "resume_rejects_a_save_with_an_unsettled_physical_object",
    ),
    "save_integrity": (
        "nonzero_sequence_without_receipts_is_rejected_after_rehashed_tamper",
        "zero_tick_event_count_must_match_its_retained_actions",
        "receipt_sequence_gap_is_rejected_after_rehashed_tamper",
        "receipt_expected_sequence_gap_is_rejected_after_rehashed_tamper",
        "destroyed_flag_on_a_non_destructible_object_is_rejected",
        "released_flag_without_throwable_or_supporting_physics_is_rejected",
        "fleeing_flag_on_an_actor_that_does_not_flee_is_rejected",
        "contact_receipt_with_an_unresolved_target_is_rejected",
        "non_normalized_object_rotation_is_rejected_after_rehashed_tamper",
        "settled_throw_impact_save_preserves_exact_float_energy_on_resume",
    ),
    "gameplay_physics": (
        "falling_object_uses_real_collision_damage_and_supported_object_stays_fixed",
        "real_rapier_support_break_releases_offset_child",
        "angular_velocity_rotation_round_trips_through_a_new_app",
        "player_motion_blocks_save_until_kcc_stops_and_lands",
        "blocked_kcc_command_does_not_repeat_damage_from_stationary_object",
        "physical_regression_capsule_resync_cannot_reuse_previous_contact_for_push",
        "resumed_player_spawn_keeps_initial_camera_direction_and_cannot_move_live_player",
    ),
    "gameplay_danger_motion": (
        "danger_motion_uses_real_los_kcc_obstacle_and_saves_actor_pose",
        "npc_mesh_capsule_profile_is_one_point_nine_metres",
    ),
    "gameplay_navigation": (
        "changing_source_while_a_real_job_is_in_flight_rejects_its_old_result",
        "disconnected_destination_never_falls_back_to_a_straight_kcc_command",
        "authored_patrol_uses_real_kcc_ground_detour_and_connected_tile_seams",
        "room_door_and_low_overhead_clearance_use_the_current_190cm_kcc_profile",
        "changing_installed_door_state_invalidates_only_affected_tiles",
        "living_idle_actor_prewarms_then_physically_escapes_an_authored_heat_danger",
        "public_navigation_feedback_is_the_completed_current_kcc_step_and_clears_when_paused",
        "blocked_primary_heat_escape_selects_a_reachable_goal_and_moves_through_real_kcc",
        "enclosed_heat_escape_caches_failed_queries_and_never_requests_horizontal_fallback",
        "selected_escape_goal_survives_source_warmup_without_actuating_a_stale_route",
    ),
    "zone_gameplay": (
        "gameplay_uses_common_preview_apply_retry_undo_redo_and_all_archive_codecs",
        "legacy_zone_roundtrip_preserves_bytes_and_does_not_add_gameplay",
    ),
    "zone_gameplay_boundaries": (
        "wrong_area_id_is_rejected_before_authoring_mutation",
        "positions_and_patrol_points_outside_terrain_are_rejected_atomically",
        "invalid_references_in_forged_commands_do_not_mutate_the_authoring_session",
        "gameplay_removal_survives_save_reopen_undo_redo_on_all_three_codecs",
        "playtest_snapshot_clones_gameplay_and_binds_the_head_hash_without_source_mutation",
    ),
    "gameplay_module_ownership": (
        "managed_state_uses_workspace_module_and_rejects_mismatch_atomically",
        "managed_gameplay_with_workspace_module_is_accepted",
        "standalone_gameplay_module_id_is_stable_across_reopen",
    ),
}

NATIVE_OBSERVATIONS_REQUIRED = (
    "Authored archive exported by Studio and loaded by the separate game",
    "Interaction, dialogue, quest, inventory and direct combat through runtime actions",
    "Push, throw, collision impact, support destruction and released falling charge",
    "Ignition, causal damage, NPC danger reaction and actual grounded KCC displacement",
    "Observed water contact and extinction with stopped heat damage",
    "Door closed NoPath then Open/Reached with current navigation revision",
    "Stable Save and Resume in a new native process with durable consequences",
    "Compatible dialogue/door retouch and explicit incompatible-resume refusal",
    "Invalid quest diagnostics, selected replay state and authoring-source isolation",
    "Actual GPU presentation and current-profile performance observations",
)


def require(condition, message):
    if not condition:
        raise ValueError(message)


def validation_driver():
    path = Path(__file__).with_name("Validate-Gate3WorkingTree.py")
    before = hashlib.sha256(path.read_bytes()).hexdigest()
    specification = importlib.util.spec_from_file_location("gate3_shared_validator", path)
    module = importlib.util.module_from_spec(specification)
    specification.loader.exec_module(module)
    require(hashlib.sha256(path.read_bytes()).hexdigest() == before,
            "Shared validation driver changed while loading")
    return module


def contract_coverage(log_text, requirements=CONTRACTS):
    """Require named passes inside completed Cargo test-suite blocks."""
    blocks = []
    current = None
    for line in log_text.splitlines():
        header = re.match(r"^\s*Running\s+(.+?)\s+\((.+)\)\s*$", line)
        if header:
            current = {"source": header[1].replace("\\", "/"), "binary": header[2],
                       "tests": {}, "complete": False}
            blocks.append(current)
            continue
        if current is None:
            continue
        result = re.match(r"^test (\S+) \.\.\. (.+)$", line)
        if result:
            require(result[1] not in current["tests"], "Duplicate test result in Cargo block")
            current["tests"][result[1]] = result[2]
        summary = re.match(r"^test result: ok\. (\d+) passed; (\d+) failed; (\d+) ignored;", line)
        if summary:
            require(not current["complete"] and int(summary[2]) == 0,
                    "Repeated or failed test-suite summary")
            passed = sum(status == "ok" for status in current["tests"].values())
            ignored = sum(status.startswith("ignored") for status in current["tests"].values())
            require(passed == int(summary[1]) and ignored == int(summary[3]),
                    "Cargo suite summary differs from observed individual test results")
            current["complete"] = True
    coverage = []
    for suite, names in requirements.items():
        def belongs(block):
            if suite.endswith("::lib"):
                crate = suite.removesuffix("::lib")
                binary_name = block["binary"].replace("\\", "/").rsplit("/", 1)[-1]
                return (block["source"] == "unittests src/lib.rs"
                        and binary_name.startswith(crate + "-"))
            return block["source"] == f"tests/{suite}.rs"

        matches = [block for block in blocks
                   if belongs(block)
                   and all(block["tests"].get(name) == "ok" for name in names)]
        require(len(matches) == 1 and matches[0]["complete"],
                f"Required gameplay suite missing, incomplete, duplicated or not executed: {suite}")
        coverage.append({"suite": suite, "binary": matches[0]["binary"], "passed_tests": list(names)})
    return coverage


def verify_headless(evidence, driver=None):
    driver = driver or validation_driver()
    driver_pin = driver.sha(Path(driver.__file__))
    require(evidence.is_absolute(), "Evidence path must be absolute")
    driver.physical_path(evidence, Path(evidence.anchor))
    evidence = evidence.resolve()
    current = driver.source_inputs()
    manifest, native = driver.completed_receipt(evidence, "native", current)
    rows = [row for row in native["stages"] if row["stage"] == "tests"]
    require(len(rows) == 1, "Native test stage is missing or duplicated")
    test_log = evidence / "tests.log"
    log_pin = driver.sha(test_log)
    coverage = contract_coverage(test_log.read_text(encoding="utf-8", errors="strict"))
    binaries = {str(Path(pin["path"]).resolve()): pin for pin in rows[0]["test_binaries"]}
    require(len(binaries) == len(rows[0]["test_binaries"]), "Duplicate native test binary pin")
    target = Path(manifest["target_dir"]).resolve()
    for suite in coverage:
        path = Path(suite["binary"])
        if not path.is_absolute():
            path = driver.SOURCE / path
        pin = binaries.get(str(path.resolve()))
        require(pin is not None, f"Executed gameplay test binary is not pinned: {path}")
        driver.verify_binary(pin, target)
        suite["binary"] = pin
    require(driver.source_inputs() == current and driver.sha(test_log) == log_pin,
            "Shared source or native test log changed during gameplay contract reading")
    driver.completed_receipt(evidence, "native", current)
    require(driver.sha(Path(driver.__file__)) == driver_pin
            and driver.sha(Path(__file__)) == READER_SHA256,
            "A gameplay evidence reader changed during verification")
    return {
        "schema": "forge.gate3.native-gameplay-contracts.v1",
        "status": "HEADLESS_CONTRACTS_PASS", "source": str(driver.SOURCE),
        "base_commit": manifest["base_commit"],
        "source_fingerprint_sha256": manifest["fingerprint_sha256"],
        "source_manifest_sha256": driver.sha(evidence / "native-source-manifest.json"),
        "native_result_sha256": driver.sha(evidence / "native-result.json"),
        "tests_log_sha256": log_pin, "reader_sha256": READER_SHA256,
        "contract_suites": coverage,
        "required_tests_passed": sum(len(suite["passed_tests"]) for suite in coverage),
        "native_observations_required": list(NATIVE_OBSERVATIONS_REQUIRED),
        "runtime_verified": False, "gpu_verified": False, "gate_qualified": False,
    }


def verify_runtime(evidence, candidate, observations, archives, heads, captures=(), required=(), scenarios=(),
                   campaigns=(), retouch_fixture=None):
    driver = validation_driver()
    contracts = verify_headless(evidence, driver)
    current = driver.source_inputs()
    manifest, release = driver.completed_receipt(evidence.resolve(), "release", current)
    expected = driver.release_artifact_paths(Path(manifest["target_dir"]))["native-game-release"]
    require(candidate.is_absolute() and candidate.resolve() == expected.resolve(),
            "Native gameplay candidate is not the common release game")
    game = driver.release_artifact_pin(candidate, Path(manifest["target_dir"]))
    require(game in release["artifacts"], "Native gameplay executable is not pinned by release validation")
    require(observations and len(observations) == len(archives) == len(heads),
            "Each native observation requires its archive and independently exported head hash")
    require(not scenarios or len(scenarios) == len(observations), "Each scenario needs its own native observation")

    def physical(path):
        require(path.is_absolute(), "Native observation/archive/save paths must be absolute")
        driver.physical_path(path, Path(path.anchor))

    runs, assessments, input_pins, archive_documents = [], [], [], []
    for index, (observation, archive, head) in enumerate(zip(observations, archives, heads)):
        physical(observation)
        physical(archive)
        require(re.fullmatch(r"[0-9a-f]{64}", head) is not None, "Invalid exported native head hash")
        archive_pin = driver.sha(archive)
        archive_documents.append(OBSERVATION_READER.strict_json(archive.read_text(encoding="utf-8")))
        plan_pin = None
        if scenarios:
            physical(scenarios[index])
            plan_pin = driver.sha(scenarios[index])
            input_pins.append({"path": str(scenarios[index]), "sha256": plan_pin})
        rows, observation_pin = OBSERVATION_READER.read_rows(observation, archive_pin, head, plan_pin)
        runs.append(rows)
        assessments.append(OBSERVATION_READER.assess(rows))
        input_pins.extend(({"path": str(archive), "sha256": archive_pin},
                           {"path": str(observation), "sha256": observation_pin}))
    require(len({assessment["pid"] for assessment in assessments}) == len(assessments),
            "Native sessions reuse the same process identity")
    require(campaigns, "Native evidence requires independent normal process campaigns and final report pins")
    process_runs = {}
    for campaign in campaigns:
        physical(campaign)
        result = OBSERVATION_READER.strict_json(campaign.read_text(encoding="utf-8"))
        inputs_path = campaign.parent / "inputs.json"
        physical(inputs_path)
        inputs = OBSERVATION_READER.strict_json(inputs_path.read_text(encoding="utf-8"))
        require(result.get("schema") == "forge.gate3.native-gameplay-campaign.v1"
                and result.get("status") == "NATIVE_PROCESSES_CLOSED" and result.get("source_unchanged") is True
                and inputs.get("native_game") == game and inputs.get("source_fingerprint_sha256") == manifest["fingerprint_sha256"],
                "Native campaign does not bind this common source and executable")
        input_pins.extend(({"path": str(campaign), "sha256": driver.sha(campaign)},
                           {"path": str(inputs_path), "sha256": driver.sha(inputs_path)}))
        for process in result["runs"]:
            require(process["pid"] not in process_runs and process.get("executable") == game
                    and process.get("exit_code") == 0 and process.get("forced_stop") is False,
                    "Native campaign process was reused, forced or failed")
            report_pin = process["game_report"]
            report_path = Path(report_pin["path"])
            physical(report_path)
            require(report_path.is_relative_to(campaign.parent) and driver.sha(report_path) == report_pin["sha256"],
                    "Native final report changed or escapes campaign")
            # Reuse the runner's complete report contract rather than a weaker second schema.
            runner_path = Path(__file__).with_name("Run-Gate3NativeGameplay.py")
            spec = importlib.util.spec_from_file_location("native_campaign_report_contract", runner_path)
            runner = importlib.util.module_from_spec(spec)
            spec.loader.exec_module(runner)
            runner.read_report(report_path, process["pid"], inputs["head_sha256"],
                               inputs["snapshot_files"]["zone.json"], runs[0][0]["functional_hash"], driver)
            input_pins.append(report_pin)
            process_runs[process["pid"]] = (process, OBSERVATION_READER.strict_json(report_path.read_text(encoding="utf-8")))
    for observation, rows in zip(observations, runs):
        require(rows[0]["pid"] in process_runs, "Native observation has no independent process monitor")
        process, report = process_runs[rows[0]["pid"]]
        require(Path(process["observation"]).resolve() == observation.resolve()
                and report["tick"] == rows[-1]["tick"] and report["sequence"] == rows[-1]["sequence"]
                and report["source_hash"] == rows[0]["source_hash"] and report["archive_hash"] == rows[0]["archive_hash"],
                "Final native report/process differ from observations")
    if retouch_fixture is None:
        require(len({(a["source_hash"], a["archive_hash"], a["functional_hash"]) for a in assessments}) == 1,
                "Native capability campaign must use one exact authored source/archive/rules identity")
    persistence = OBSERVATION_READER.verify_saved_observations(runs, captures, physical)
    checks = {name: any(assessment["checks"][name] for assessment in assessments)
              for name in OBSERVATION_READER.CAPABILITIES}
    checks["save_new_process_resume"] = persistence["save_new_process_resume"]
    document_assessment = None
    compatible = None
    if retouch_fixture is None:
        document_assessment = OBSERVATION_READER.assess_p01_p04(
            runs, OBSERVATION_READER.gameplay_document(archive_documents[0]), persistence)
        checks.update(document_assessment["checks"])
        checks["compatible_retouch_preserved_state"] = False
    else:
        require(len(runs) == 2 and required == ["compatible_retouch_preserved_state"],
                "P02 requires exactly old/new observations and its dedicated required assertion")
        physical(retouch_fixture)
        fixture_pin = driver.sha(retouch_fixture)
        compatible = OBSERVATION_READER.verify_compatible_retouch(runs[0], runs[1], archive_documents[0],
            archive_documents[1], OBSERVATION_READER.strict_json(retouch_fixture.read_text(encoding="utf-8")), captures, physical)
        checks = {"compatible_retouch_preserved_state": compatible["compatible_retouch_preserved_state"]}
        input_pins.append({"path": str(retouch_fixture), "sha256": fixture_pin})
    input_pins.extend(persistence["save_capture_pins"])
    for pin in input_pins:
        require(driver.sha(Path(pin["path"])) == pin["sha256"], "Native input changed while reading its evidence")
    require(driver.source_inputs() == current and driver.sha(OBSERVATION_HELPER_PATH) == OBSERVATION_READER.SOURCE_SHA256
            and driver.sha(Path(__file__)) == READER_SHA256, "Shared sources or gameplay evidence reader changed")
    driver.completed_receipt(evidence.resolve(), "release", current)
    missing = [name for name in required if not checks[name]]
    return {
        "schema": "forge.gate3.native-gameplay-observations.v1",
        "status": "FAILED_REQUIRED_OBSERVATIONS" if missing else "NATIVE_OBSERVATIONS_READ",
        "source_fingerprint_sha256": contracts["source_fingerprint_sha256"],
        "native_result_sha256": contracts["native_result_sha256"],
        "release_result_sha256": driver.sha(evidence / "release-result.json"),
        "candidate": game, "reader_sha256": READER_SHA256,
        "observation_reader_sha256": OBSERVATION_READER.SOURCE_SHA256,
        "input_pins": input_pins, "sessions": assessments, "checks": checks,
        "required_observations": list(required), "missing_required_observations": missing,
        "persistence": persistence, "document_assessment": document_assessment, "compatible_retouch": compatible,
        "gpu_verified": False, "gate_qualified": False,
        "limitations": ["PIDs and exit markers are supplied by the pinned runtime; retain an independent process monitor.",
                        "SDK diagnostic P03, Studio authoring/replay/revision UI and performance need separate evidence.",
                        "No human acceptance, GPU presentation, CI result or whole-gate qualification is inferred."]}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("mode", choices=("headless", "runtime", "retouch"))
    parser.add_argument("--evidence", type=Path, required=True)
    parser.add_argument("--output", type=Path)
    parser.add_argument("--candidate", type=Path)
    parser.add_argument("--observations", type=Path, nargs="+")
    parser.add_argument("--archive", type=Path, nargs="+")
    parser.add_argument("--expected-head", nargs="+")
    parser.add_argument("--saved-capture", type=Path, nargs="+", default=[])
    parser.add_argument("--scenario", type=Path, nargs="+", default=[])
    parser.add_argument("--campaign", type=Path, nargs="+", default=[])
    parser.add_argument("--retouch-fixture", type=Path)
    parser.add_argument("--require", choices=require_observations, nargs="+", default=[])
    args = parser.parse_args()
    try:
        if args.mode == "headless":
            result = verify_headless(args.evidence)
        else:
            require(args.candidate is not None and args.observations and args.archive and args.expected_head,
                    "Runtime mode requires candidate, observations, archives and independently exported heads")
            require((args.mode == "retouch") == (args.retouch_fixture is not None),
                    "Only the dedicated retouch mode can aggregate distinct authored revisions")
            result = verify_runtime(args.evidence, args.candidate, args.observations, args.archive,
                                    args.expected_head, args.saved_capture, args.require, args.scenario,
                                    args.campaign, args.retouch_fixture)
        rendered = json.dumps(result, indent=2, ensure_ascii=False)
        if args.output:
            require(args.output.is_absolute(), "Output path must be absolute")
            driver = validation_driver()
            driver.physical_path(args.output.parent, Path(args.output.anchor))
            # Existing evidence remains unchanged, including failures.
            with args.output.open("x", encoding="utf-8") as stream:
                stream.write(rendered + "\n")
        print(rendered)
        return 1 if result["status"] == "FAILED_REQUIRED_OBSERVATIONS" else 0
    except (ValueError, KeyError, TypeError, OSError) as error:
        print(f"Native gameplay contract evidence refused: {error}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    sys.exit(main())
