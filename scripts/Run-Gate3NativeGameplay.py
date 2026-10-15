"""Run bounded scenarios through the common native game and its normal commands.

The input snapshot is immutable data exported by the Studio backend, optionally
retouched by its application SDK under a separate receipt. This runner does not
write authored JSON or control native Windows UI. It starts only its own pinned
game processes; captures and input are supplied by the app-level scenario hook.
"""
from pathlib import Path
import argparse
import hashlib
import importlib.util
import json
import os
import re
import subprocess
import sys
import time
import uuid

sys.dont_write_bytecode = True
SOURCE_BYTES = Path(__file__).read_bytes()
SOURCE_SHA256 = hashlib.sha256(SOURCE_BYTES).hexdigest()
HELPER_PATH = Path(__file__).parent / "tidewater" / "gate3_native_observations.py"
HELPER_SPEC = importlib.util.spec_from_file_location("native_campaign_observation_reader", HELPER_PATH)
HELPER = importlib.util.module_from_spec(HELPER_SPEC)
HELPER_SPEC.loader.exec_module(HELPER)


def require(condition, message):
    if not condition:
        raise ValueError(message)


def driver():
    path = Path(__file__).with_name("Validate-Gate3WorkingTree.py")
    pin = hashlib.sha256(path.read_bytes()).hexdigest()
    spec = importlib.util.spec_from_file_location("common_gate3_driver", path)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    require(module.sha(path) == pin, "Common driver changed while loading")
    return module


def inventory(root, shared):
    require(root.is_absolute() and root.is_dir(), f"Input directory absent: {root}")
    shared.physical_path(root, Path(root.anchor))
    files = {}
    for directory, directories, names in os.walk(root, followlinks=False):
        for name in directories + names:
            path = Path(directory) / name
            shared.physical_path(path, root)
            if path.is_file():
                files[path.relative_to(root).as_posix()] = shared.sha(path)
    return files


def recipe(profile, snapshot, screenshot, archive_sha, executable, shared):
    """Same water/cloud/building/snapshot recipe used by Studio launch_native_game."""
    content = Path(profile["game"]["content_root"])
    require(content.is_absolute() and content.is_dir(), "Configured game content root is absent")
    data = content / "data"
    for path in (data, data / "buildings" / "package.json", snapshot / "zone.json"):
        shared.physical_path(path, Path(path.anchor))
    require(Path(profile["game"]["executable"]).resolve() == executable.resolve(),
            "Configured Studio game differs from the pinned common release game")
    catalog = profile["module_assets"]
    require(Path(catalog["root"]).resolve() == (data / "scene").resolve()
            and Path(catalog["catalog"]).name == catalog["catalog"]
            and shared.sha(data / "scene" / catalog["catalog"]) == catalog["catalog_sha256"],
            "Configured scene catalog differs from the Studio profile pin")
    argv = [str(executable), str(data / "scene"), str(screenshot), "--water-spray-clouds",
            str(data / "ocean"), str(data / "shore"), str(data / "spray"), str(data / "clouds"),
            "--studio-snapshot", str(snapshot), archive_sha, "--building-package",
            str(data / "buildings"), shared.sha(data / "buildings" / "package.json"), "--walk"]
    debris = content / "content" / "debris"
    env = {name: value for name, value in os.environ.items()
           if not name.upper().startswith(("FORGE_TIDEWATER_", "FORGE_NATIVE_GAMEPLAY_"))}
    env.update({
        "FORGE_TIDEWATER_DEBRIS_ROOT": str(debris / "procedural"),
        "FORGE_TIDEWATER_SCANNED_ROOT": str(debris / "scanned"),
        "FORGE_TIDEWATER_SCANNED_PROGRAM": str(debris / "scanned.wgsl"),
        "FORGE_TIDEWATER_PEBBLE_PROGRAM_ROOT": str(debris / "pebbles"),
        "FORGE_TIDEWATER_GAME_FISH_ROOT": str(content / "content" / "fish" / "game"),
        "FORGE_TIDEWATER_FISH_PROP_ROOT": str(content / "content" / "fish" / "surface"),
    })
    return argv, env, content


def read_plan(path, archive_sha, head_sha, shared):
    require(path.is_absolute(), "Scenario input must be absolute")
    shared.physical_path(path, Path(path.anchor))
    require(path.stat().st_size <= 1024 * 1024, "Scenario plan exceeds 1 MiB")
    raw = path.read_bytes()
    plan = json.loads(raw)
    require(plan.get("schema") == "forge.native-gameplay.scenario" and plan.get("version") == 1
            and plan.get("source_hash") == head_sha and plan.get("archive_hash") == archive_sha,
            "Scenario does not bind the independently prepared snapshot")
    require(type(plan.get("max_duration_s")) is int and 0 < plan["max_duration_s"] <= 600,
            "Scenario duration is invalid or exceeds 600 s")
    steps = plan.get("steps")
    require(isinstance(steps, list) and 1 <= len(steps) <= 128 and all(isinstance(step, dict) for step in steps)
            and steps[-1].get("kind") == "exit"
            and all(step.get("kind") != "exit" for step in steps[:-1]),
            "Scenario steps are missing, excessive, or have no final Exit")
    require(steps[0].get("kind") == "wait_ready", "Scenario must wait for canonical readiness before acting")
    allowed = {"wait_ready", "wait_ticks", "move", "move_to", "action", "save",
               "resume_next_launch", "capture", "menu", "exit"}
    require(all(isinstance(step, dict) and step.get("kind") in allowed for step in steps),
            "Scenario contains an unsupported operation")
    return raw, hashlib.sha256(raw).hexdigest(), plan


def write_new(path, value):
    with path.open("x", encoding="utf-8") as stream:
        json.dump(value, stream, indent=2, ensure_ascii=False)
        stream.write("\n")


def owned_runtime_path(path, root, shared):
    """Bind Rust's canonical Windows drive alias to the same owned physical file."""
    require(path.is_absolute() and root.is_absolute() and ".." not in path.parts and ".." not in root.parts,
            "Owned runtime paths must be absolute without traversal")

    def ordinary(value):
        text = str(value)
        if os.name == "nt":
            require(not text.startswith("\\\\.\\"), "Windows device namespace refused")
            if text.startswith("\\\\?\\"):
                require(re.match(r"^[A-Za-z]:\\", text[4:]) is not None, "Unsupported Windows extended namespace")
                return Path(text[4:])
        return value

    original, boundary = ordinary(path), ordinary(root)
    resolved, resolved_boundary = original.resolve(strict=True), boundary.resolve(strict=True)
    require(resolved.is_relative_to(resolved_boundary), "Native runtime file escapes its owned directory")
    shared.physical_path(original, boundary)
    shared.physical_path(resolved, resolved_boundary)
    return resolved


def fixture_provenance(path, snapshot, archive_sha, head_sha, files, shared):
    proof = json.loads(path.read_bytes())
    require(proof.get("schema") == "forge.native-gameplay.fixture" and proof.get("version") == 1
            and proof.get("status") == "DATA_ONLY_APPLICATION_FIXTURE"
            and proof.get("source_unchanged") is True and proof.get("copied_product_code") is False
            and proof.get("studio_ui_verified") is False and proof.get("runtime_verified") is False
            and proof.get("gate_qualified") is False, "Fixture provenance does not describe isolated application data")
    require(Path(proof["snapshot"]).resolve() == snapshot.resolve() and proof.get("archive_sha256") == archive_sha
            and proof.get("head_sha256") == head_sha, "Fixture provenance differs from the requested snapshot")
    pins = proof.get("files")
    require(isinstance(pins, list) and all(isinstance(pin, dict) for pin in pins), "Fixture file inventory is absent")
    expected = {}
    for pin in pins:
        name = shared.safe_relative(pin["path"])
        require(name not in expected and type(pin.get("bytes")) is int and pin["bytes"] >= 0,
                "Duplicate or invalid fixture input")
        require((snapshot / name).stat().st_size == pin["bytes"], "Fixture input size differs")
        expected[name] = pin["sha256"]
    if path.resolve().is_relative_to(snapshot.resolve()):
        expected[path.relative_to(snapshot).as_posix()] = shared.sha(path)
    require(files == expected, "Fixture inventory has changed or contains unexpected files")
    source = Path(proof["source_snapshot"])
    require(source.is_absolute(), "Fixture source snapshot is not absolute")
    shared.physical_path(source / "zone.json", Path(source.anchor))
    require(shared.sha(source / "zone.json") == proof["source_archive_sha256"], "Original Studio export changed")
    return proof


def retain_saves(observation, destination, save_root, shared):
    """Copy only committed game-save bytes between sessions, never edit them."""
    require(observation.is_file(), "Native session produced no gameplay observations")
    captures, copied = {}, {}
    rows = [json.loads(line) for line in observation.read_bytes().splitlines()]
    for row in rows:
        if row.get("kind") == "scenario" and row.get("status") == "save_copied":
            detail = row["detail"]
            path = owned_runtime_path(Path(detail["path"]), destination.parent / "native-output", shared)
            require(path.is_file() and path.stat().st_size <= 4 * 1024 * 1024
                    and shared.sha(path) == detail["sha256"], "Scenario save copy is missing or changed")
            copied[detail["sha256"]] = path
    for row in rows:
        if row.get("kind") != "command" or row.get("accepted") is not True or row.get("saved") is None:
            continue
        if row["command"].get("kind") not in ("save", "resume_next_launch"):
            continue
        meta = row["saved"]
        original = Path(meta["path"])
        path = owned_runtime_path(original, save_root, shared)
        path = copied.get(meta["sha256"], path)
        shared.physical_path(path, Path(path.anchor))
        require(path.is_file() and path.stat().st_size <= 4 * 1024 * 1024,
                "Recorded save is absent or over quota")
        raw = path.read_bytes()
        pin = hashlib.sha256(raw).hexdigest()
        require(pin == meta["sha256"], "A committed save changed before it could be retained")
        if pin not in captures:
            captured = destination / (pin + ".json")
            with captured.open("xb") as stream:
                stream.write(raw)
            captures[pin] = {"path": str(captured), "sha256": pin, "original_path": str(original)}
    return list(captures.values())


def copy_resume_source(result_path, destination, game_pin, source_fingerprint, fixture, shared):
    """Adopt only the prior campaign's exact saved bytes and explicit request."""
    require(result_path.is_absolute(), "Resume campaign receipt must be absolute")
    shared.physical_path(result_path, Path(result_path.anchor))
    previous = HELPER.strict_json(result_path.read_text(encoding="utf-8"))
    inputs_path = result_path.parent / "inputs.json"
    shared.physical_path(inputs_path, Path(inputs_path.anchor))
    inputs = HELPER.strict_json(inputs_path.read_text(encoding="utf-8"))
    require(previous.get("schema") == "forge.gate3.native-gameplay-campaign.v1"
            and previous.get("status") == "NATIVE_PROCESSES_CLOSED" and previous.get("source_unchanged") is True
            and inputs.get("native_game") == game_pin
            and inputs.get("source_fingerprint_sha256") == source_fingerprint
            and previous.get("runs") and all(run.get("executable") == game_pin and run.get("exit_code") == 0
                and run.get("forced_stop") is False for run in previous["runs"]),
            "Resume source was not a normally closed campaign of the same common executable/sources")
    source_root = result_path.parent / "user-data" / "game-saves"
    before = inventory(source_root, shared)
    module, area = fixture["module_id"], fixture["area_id"]
    require(str(uuid.UUID(module)) == module and str(uuid.UUID(area)) == area,
            "Resume module/area identities are not canonical")
    source = source_root / module / area
    require(not destination.exists(), "Resume destination must be fresh")
    saved = (source / "authoring.json").read_bytes()
    request_bytes = (source / "resume.json").read_bytes()
    saved_pin = hashlib.sha256(saved).hexdigest()
    require(len(request_bytes) <= 4096 and len(saved) <= 4 * 1024 * 1024, "Copied save/request exceed storage limits")
    request, envelope = HELPER.strict_json(request_bytes.decode("utf-8")), HELPER.strict_json(saved.decode("utf-8"))
    require(request.get("schema") == "forge.native-game-resume" and request.get("version") == 1
            and request.get("module") == module and request.get("area") == area
            and request.get("saved_hash") == saved_pin
            and envelope.get("schema") == "forge.game-save" and envelope.get("version") == 1
            and envelope.get("module_id") == module and envelope.get("area_id") == area
            and envelope.get("functional_hash") == fixture["functional_hash"]
            and type(request.get("allow_compatible_edits")) is bool
            and (envelope.get("source_hash") == fixture["head_sha256"] or request["allow_compatible_edits"]),
            "Resume request/save do not bind this module, saved bytes and functional identity")
    observed_save = observed_request = False
    for run in previous["runs"]:
        observation = owned_runtime_path(Path(run["observation"]), result_path.parent, shared)
        rows, _ = HELPER.read_rows(observation, inputs["snapshot_files"]["zone.json"],
                                  inputs["head_sha256"], run["scenario_sha256"])
        require(rows and rows[0].get("kind") == "boot" and rows[-1].get("kind") == "exit"
                and rows[-1].get("exit_success") is True
                and all(row.get("pid") == run["pid"] for row in rows), "Prior native process receipt is incoherent")
        report_pin = run["game_report"]
        report_path = owned_runtime_path(Path(report_pin["path"]), result_path.parent, shared)
        require(shared.sha(report_path) == report_pin["sha256"],
                "Prior final game report changed")
        read_report(report_path, run["pid"], inputs["head_sha256"],
                    inputs["snapshot_files"]["zone.json"], fixture["functional_hash"], shared)
        for row in rows:
            if row.get("kind") != "command" or row.get("accepted") is not True \
                    or (row.get("saved") or {}).get("sha256") != saved_pin:
                continue
            command = row["command"]
            observed_save |= command.get("kind") == "save" and row.get("source_hash") == envelope["source_hash"]
            observed_request |= command.get("kind") == "resume_next_launch" \
                and command.get("allow_compatible_edits") == request["allow_compatible_edits"]
    require(observed_save and observed_request, "Copied save/request lack real accepted Save and explicit Resume observations")
    copied = []
    for name, raw in (("authoring.json", saved), ("resume.json", request_bytes)):
        relative = f"{module}/{area}/{name}"
        require(before.get(relative) == hashlib.sha256(raw).hexdigest(), "Resume source changed during inspection")
        target = destination / module / area / name
        target.parent.mkdir(parents=True, exist_ok=True)
        with target.open("xb") as stream:
            stream.write(raw)
        copied.append({"path": str(target), "sha256": hashlib.sha256(raw).hexdigest()})
    require(inventory(source_root, shared) == before, "Prior campaign saves changed while copying")
    return {"source_result": str(result_path), "source_result_sha256": shared.sha(result_path),
            "source_inputs_sha256": shared.sha(inputs_path), "source_root": str(source_root),
            "source_files": before, "copied": copied, "synthesized_save_or_request": False}


def read_report(path, pid, source_hash, archive_hash, functional_hash, shared):
    shared.physical_path(path, Path(path.anchor))
    require(path.is_file() and path.stat().st_size <= 64 * 1024, "Native game did not retain its bounded final report")
    report = HELPER.strict_json(path.read_text(encoding="utf-8"))
    fields = {"schema", "version", "pid", "source_hash", "archive_hash", "functional_hash", "exit_success", "ready",
              "tick", "sequence", "quests", "inventory_total", "health", "stamina", "feedback", "saved",
              "last_command_error", "simulation_error", "resume_refused"}
    require(set(report) == fields and all(type(report[name]) is int and 0 <= report[name] < 2**64
            for name in ("tick", "sequence", "inventory_total", "health", "stamina", "pid"))
            and report["pid"] > 0 and report["health"] < 2**32 and report["stamina"] < 2**32
            and isinstance(report["feedback"], str) and len(report["feedback"].encode("utf-8")) <= 1024
            and isinstance(report["quests"], list) and len(report["quests"]) <= 1024
            and all(isinstance(q, dict) and set(q) == {"id", "status"} and isinstance(q["id"], str)
                    and q["status"] in ("available", "active", "completed") for q in report["quests"])
            and len({q["id"] for q in report["quests"]}) == len(report["quests"])
            and (report["saved"] is None or isinstance(report["saved"], dict)
                 and set(report["saved"]) == {"path", "sha256", "state_hash", "source_hash", "functional_hash"}
                 and all(isinstance(v, str) for v in report["saved"].values())), "Native report contract is incomplete or malformed")
    require(report.get("schema") == "forge.native-gameplay.report" and type(report.get("version")) is int
            and report["version"] == 1 and report.get("pid") == pid
            and report.get("source_hash") == source_hash and report.get("archive_hash") == archive_hash
            and report.get("functional_hash") == functional_hash and report.get("exit_success") is True
            and report.get("ready") is True
            and all(report.get(name) is None for name in ("last_command_error", "simulation_error", "resume_refused")),
            "Native report does not attest the pinned ready game and clean result")
    return {"path": str(path), "sha256": shared.sha(path)}


def run(args):
    shared = driver()
    for path in (args.evidence, args.profile, args.snapshot, args.snapshot_provenance, args.output):
        require(path.is_absolute(), "All native campaign paths must be absolute")
        shared.physical_path(path.parent, Path(path.anchor))
    shared.physical_path(args.profile, Path(args.profile.anchor))
    shared.physical_path(args.snapshot_provenance, Path(args.snapshot_provenance.anchor))
    require(not args.output.exists(), "Previous campaign evidence must be preserved")
    require(re.fullmatch(r"[0-9a-f]{64}", args.archive_sha256) is not None
            and re.fullmatch(r"[0-9a-f]{64}", args.head_sha256) is not None,
            "Snapshot archive/head pins are invalid")
    current = shared.source_inputs()
    manifest, release = shared.completed_receipt(args.evidence.resolve(), "release", current)
    executable = shared.release_artifact_paths(Path(manifest["target_dir"]))["native-game-release"]
    game_pin = shared.release_artifact_pin(executable, Path(manifest["target_dir"]))
    require(game_pin in release["artifacts"], "Native game is not in the common release receipt")
    profile_pin = shared.sha(args.profile)
    profile = json.loads(args.profile.read_bytes())
    provenance_pin = shared.sha(args.snapshot_provenance)
    snapshot_before = inventory(args.snapshot, shared)
    require(snapshot_before.get("zone.json") == args.archive_sha256, "Snapshot archive pin differs")
    fixture = fixture_provenance(args.snapshot_provenance, args.snapshot, args.archive_sha256,
                                 args.head_sha256, snapshot_before, shared)
    source_snapshot = Path(fixture["source_snapshot"])
    source_snapshot_before = inventory(source_snapshot, shared)
    authoring_root = Path(profile["authoring_root"])
    authored_before = inventory(authoring_root, shared)
    plans = [read_plan(path, args.archive_sha256, args.head_sha256, shared) for path in args.scenario]
    args.output.mkdir()
    write_new(args.output / "inputs.json", {"schema": "forge.gate3.native-gameplay-campaign-inputs.v1",
        "source_fingerprint_sha256": manifest["fingerprint_sha256"], "native_game": game_pin,
        "profile": {"path": str(args.profile), "sha256": profile_pin},
        "snapshot": str(args.snapshot), "snapshot_files": snapshot_before,
        "snapshot_provenance": {"path": str(args.snapshot_provenance), "sha256": provenance_pin},
        "original_studio_snapshot": str(source_snapshot), "original_snapshot_files": source_snapshot_before,
        "head_sha256": args.head_sha256, "authoring_root": str(authoring_root),
        "authored_files": authored_before, "runner_sha256": SOURCE_SHA256})
    save_root = args.output / "user-data" / "game-saves"
    resume_copy = None
    if args.resume_from:
        resume_copy = copy_resume_source(args.resume_from, save_root, game_pin,
                                          manifest["fingerprint_sha256"], fixture, shared)
        write_new(args.output / "resume-source.json", resume_copy)
    results, captures, failure = [], [], None
    try:
        for index, (path, prepared) in enumerate(zip(args.scenario, plans), 1):
            raw, plan_pin, plan = prepared
            require(shared.source_inputs() == current and shared.sha(args.profile) == profile_pin
                    and shared.sha(path) == plan_pin, "Campaign inputs changed before a native launch")
            shared.completed_receipt(args.evidence.resolve(), "release", current)
            folder = args.output / f"run-{index}"
            folder.mkdir()
            native_output = folder / "native-output"
            native_output.mkdir()
            shared.physical_path(native_output, folder)
            scenario = folder / "scenario.json"
            scenario.write_bytes(raw)
            observation = folder / "gameplay-observations.jsonl"
            argv, env, cwd = recipe(profile, args.snapshot, folder / "game.png", args.archive_sha256, executable, shared)
            env.update(FORGE_GAME_SAVE_ROOT=str(save_root), FORGE_GAME_OBSERVATIONS=str(observation),
                       FORGE_GAME_REPORT_PATH=str(folder / "game-report.json"),
                       FORGE_NATIVE_GAMEPLAY_SCENARIO_PATH=str(scenario),
                       FORGE_NATIVE_GAMEPLAY_SCENARIO_SHA256=plan_pin,
                       FORGE_NATIVE_GAMEPLAY_OUTPUT_ROOT=str(native_output))
            started = time.time()
            with (folder / "game.log").open("xb") as log:
                process = None
                row = {"run": index, "executable": game_pin, "argv": argv, "cwd": str(cwd),
                       "started_unix": started, "scenario_sha256": plan_pin, "forced_stop": False}
                try:
                    process = subprocess.Popen(argv, cwd=cwd, env=env, stdout=log, stderr=subprocess.STDOUT)
                    row["pid"] = process.pid
                    results.append(row)
                    write_new(folder / "process-started.json", row)
                    print(f"Native gameplay run {index} started, PID {process.pid}", flush=True)
                    deadline = time.monotonic() + plan["max_duration_s"] + 120
                    while process.poll() is None:
                        if time.monotonic() >= deadline:
                            raise TimeoutError("Bounded native scenario exceeded its startup/plan allowance")
                        try:
                            process.wait(timeout=10)
                        except subprocess.TimeoutExpired:
                            print(f"Native gameplay run {index}, PID {process.pid}, still running", flush=True)
                finally:
                    if process is not None and process.poll() is None:
                        row["forced_stop"] = True
                        process.terminate()  # exact Popen handle owned by this test
                        try:
                            process.wait(timeout=15)
                        except subprocess.TimeoutExpired:
                            process.kill()
                            process.wait(timeout=15)
                row.update(exit_code=process.returncode, ended_unix=time.time(),
                           observation=str(observation), log=str(folder / "game.log"))
                write_new(folder / "process-closed.json", row)
                require(process.returncode == 0 and not row["forced_stop"], "Native scenario did not exit normally")
            saved_folder = folder / "saved-captures"
            saved_folder.mkdir()
            captures.extend(retain_saves(observation, saved_folder, save_root, shared))
            report_path = folder / "game-report.json"
            row["game_report"] = read_report(report_path, process.pid, args.head_sha256,
                                               args.archive_sha256, fixture["functional_hash"], shared)
            require(inventory(args.snapshot, shared) == snapshot_before
                    and inventory(source_snapshot, shared) == source_snapshot_before
                    and inventory(authoring_root, shared) == authored_before,
                    "Native play changed snapshot or authored source files")
            print(f"Native gameplay run {index} exited normally", flush=True)
    except (OSError, ValueError, KeyError, TypeError, subprocess.SubprocessError, TimeoutError) as error:
        failure = str(error)
    try:
        unchanged = (inventory(args.snapshot, shared) == snapshot_before
                     and inventory(source_snapshot, shared) == source_snapshot_before
                     and inventory(authoring_root, shared) == authored_before
                     and shared.sha(args.profile) == profile_pin
                     and shared.sha(args.snapshot_provenance) == provenance_pin
                     and shared.source_inputs() == current
                     and shared.sha(Path(__file__)) == SOURCE_SHA256)
        if resume_copy:
            unchanged = unchanged and inventory(Path(resume_copy["source_root"]), shared) == resume_copy["source_files"]
    except (OSError, ValueError, KeyError, TypeError) as error:
        unchanged = False
        failure = failure or str(error)
    if not unchanged and failure is None:
        failure = "Campaign source/profile/snapshot inputs changed"
    outcome = {"schema": "forge.gate3.native-gameplay-campaign.v1",
               "status": "FAILED_CLOSED" if failure else "NATIVE_PROCESSES_CLOSED",
               "failure": failure, "source_unchanged": unchanged, "runs": results,
               "saved_captures": captures, "gameplay_observations_verified": False,
               "resume_source": resume_copy,
               "gpu_verified": False, "gate_qualified": False,
               "limits": ["Normal process exit is not gameplay qualification; run the strict observation reader.",
                          "Native screenshots need visual inspection and are not a performance measurement.",
                          "SDK-authored fixtures do not by themselves prove the Studio authoring UI."]}
    write_new(args.output / "result.json", outcome)
    print(json.dumps({"status": outcome["status"], "failure": failure, "output": str(args.output)}, ensure_ascii=False))
    return 1 if failure else 0


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ("evidence", "profile", "snapshot", "snapshot-provenance", "output"):
        parser.add_argument("--" + name, type=Path, required=True)
    parser.add_argument("--archive-sha256", required=True)
    parser.add_argument("--head-sha256", required=True)
    parser.add_argument("--scenario", type=Path, required=True, nargs="+")
    parser.add_argument("--resume-from", type=Path)
    args = parser.parse_args()
    try:
        return run(args)
    except (OSError, ValueError, KeyError, TypeError) as error:
        print(f"Native campaign refused: {error}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    sys.exit(main())
