"""Launch the shared Studio against isolated data; observe its real saved output.

This integration driver never invokes a UI approval endpoint, provider API or
source mutation. Generation, review, approval and save occur through Studio UI.
It passes process-local native settings without changing the owner's sidecar.
"""
import argparse
import datetime
import hashlib
import json
import os
from pathlib import Path
import re
import stat
import subprocess
import time


def utc():
    return datetime.datetime.now(datetime.timezone.utc).isoformat()


def load_zone(root):
    workspace = json.loads((root / "workspace-v1/authoring.json").read_bytes())
    reference = workspace["head"]["projects"][0]["modules"][0]["zones"][0]
    archive = json.loads((root / "workspace-v1/objects" / (reference["archive_hash"] + ".json")).read_bytes())
    return reference, archive


def applied_job(root):
    """Read bounded durable ledgers; never mutate authoring data."""
    applied = []
    group = root / "assistant-jobs"
    if getattr(group.lstat(), "st_file_attributes", 0) & 0x400 or group.is_symlink():
        raise ValueError("Job links are not followed")
    directories = list(group.iterdir())
    if len(directories) > 128:
        raise ValueError("Unexpected qualification job count")
    for directory in directories:
        metadata = directory.lstat()
        if stat.S_ISLNK(metadata.st_mode) or getattr(metadata, "st_file_attributes", 0) & 0x400:
            raise ValueError("Job links are not followed")
        if not directory.is_dir():
            raise ValueError("Unexpected job entry")
        path = directory / "authoring.json"
        metadata = path.lstat()
        if stat.S_ISLNK(metadata.st_mode) or getattr(metadata, "st_file_attributes", 0) & 0x400:
            raise ValueError("Job links are not followed")
        if metadata.st_size > 8 * 1024**2:
            raise ValueError("Unexpected journal size")
        journal = json.loads(path.read_bytes())
        if any(event["event"] == "applied" for event in journal["events"]):
            applied.append(journal)
    assert len(applied) == 1
    return applied[0]


def verify(root, before, reference, after, recipe):
    assert after["head"]["base"] == before["head"]["base"]
    for key in before["head"]["corrections"].keys() | after["head"]["corrections"].keys():
        if key not in ({"placements"} if recipe == "assets" else {"gameplay", "placements"}):
            assert after["head"]["corrections"].get(key) == before["head"]["corrections"].get(key), key
    job = applied_job(root)
    staged = [event for event in job["events"] if event["event"] == "staged"]
    promoted = [event for event in job["events"] if event["event"] == "applied"]
    assert len(staged) == len(promoted) == 1
    result = staged[0]["result"]
    if recipe == "assets":
        additions = verify_asset(before, after, result)
    else:
        additions = verify_content(before, after, result)
    assert promoted[0]["resulting_hash"] == reference["head_hash"]
    receipts = [receipt for receipt in after["receipts"] if receipt["request"]["command_id"] == job["job_id"]]
    assert len(receipts) == 1 and receipts[0]["audit"]["actor"]["kind"] == "ai"
    assert len(after["receipts"]) == len(before["receipts"]) + 1
    return {"head_hash": reference["head_hash"], "command_id": job["job_id"],
            "additions": additions, "provider_model": result["model"], "usage": result["usage"],
            "recipe": recipe, "previous_source_preserved": True, "single_source_receipt": True,
            "single_staged_and_promoted_result": True}


def verify_asset(before, after, result):
    additions = verify_added_placements(before, after, result)
    assert len(additions) == 1
    assert len(result["proposal"]["operations"]) == 1
    return additions


def verify_added_placements(before, after, result):
    original = before["head"]["corrections"].get("placements") or []
    current = after["head"]["corrections"].get("placements") or []
    assert all(item in current for item in original)
    additions = [item for item in current if item["id"] not in {old["id"] for old in original}]
    operations = [operation for operation in result["proposal"]["operations"] if operation["operation"] == "place_asset"]
    assert len(operations) == len(additions)
    for operation in operations:
        matching = [item for item in additions if item["name"] == operation["name"] and item["asset_id"] == operation["asset_id"] and item["transform"]["translation_m"] == operation["position"]]
        assert len(matching) == 1
    return additions


def verify_content(before, after, result):
    original = before["head"]["corrections"]["gameplay"]
    current = after["head"]["corrections"]["gameplay"]
    collections = {"actors", "items", "objects", "places", "dialogues", "quests"}
    for key in original.keys() | current.keys():
        if key not in collections | {"narrative"}:
            assert current.get(key) == original.get(key), key
    assert [operation["operation"] for operation in result["proposal"]["operations"]].count("create_fetch_quest") == 1
    operations = [operation["operation"] for operation in result["proposal"]["operations"]]
    assert operations.count("create_places") + operations.count("add_content") == 1
    assert set(operations) <= {"add_content", "create_places", "create_fetch_quest", "place_asset"}
    additions = {}
    for key in collections:
        previous = original.get(key) or []
        actual = current.get(key) or []
        assert all(item in actual for item in previous), key
        additions[key] = [item for item in actual if item["id"] not in {old["id"] for old in previous}]
    assert len(additions["quests"]) == 1
    assert len(additions["places"]) == 3
    narrative = current["narrative"]
    if original.get("narrative"):
        for key, value in original["narrative"].items():
            if isinstance(value, list):
                assert all(item in narrative.get(key, []) for item in value), key
            else:
                assert narrative.get(key) == value, key
    flows = [flow for flow in narrative["quests"] if flow["quest"] == additions["quests"][0]["id"]]
    assert len(flows) == 1
    assert len(flows[0]["solutions"]) == 2
    additions["placements"] = verify_added_placements(before, after, result)
    return {key: [item["id"] for item in value] for key, value in additions.items()}


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--executable", type=Path, required=True)
    parser.add_argument("--root", type=Path, required=True)
    parser.add_argument("--evidence", type=Path, required=True)
    parser.add_argument("--run-id", required=True)
    parser.add_argument("--provider", required=True)
    parser.add_argument("--model", required=True)
    parser.add_argument("--vault", type=Path, required=True)
    parser.add_argument("--recipe", choices=("assets", "content"), default="assets")
    args = parser.parse_args()
    if not re.fullmatch(r"[0-9]{3}", args.run_id):
        raise ValueError("Use a three-digit evidence ID")
    root = args.root.resolve(strict=True)
    if not re.fullmatch(r"gate6-" + args.recipe + r"-studio-ui-[0-9]{3}", root.name):
        raise ValueError("Use isolated qualification documents matching the recipe")
    executable = args.executable.resolve(strict=True)
    evidence = args.evidence.resolve(strict=True)
    vault = args.vault.resolve(strict=True)
    _, before = load_zone(root)
    settings = {"base_url": args.provider, "model": args.model, "api_kind": "chat_completions",
                "enable_reasoning": True, "api_key_environment": None,
                "limits": {"maximum_attempts": 2, "maximum_input_tokens": 100_000,
                           "maximum_output_tokens": 16_384 if args.recipe == "content" else 4096, "maximum_micro_usd": 0,
                           "prices": {"input_per_million": 0, "output_per_million": 0}}}
    environment = os.environ.copy()
    environment.update(FORGE_AUTHORING_ROOT=str(root), OPENRPG_ASSET_VAULT=str(vault),
                       FORGE_ASSISTANT_CONFIGURATION=json.dumps(settings))
    prefix = "assistant-" + args.recipe + "-studio-client-" + args.run_id
    with (evidence / (prefix + ".jsonl")).open("x", encoding="utf-8") as transcript, \
            (evidence / (prefix + ".stderr.log")).open("xb") as errors:
        def record(kind, **fields):
            transcript.write(json.dumps({"utc": utc(), "kind": kind, **fields}, ensure_ascii=False) + "\n")
            transcript.flush()

        process = subprocess.Popen([str(executable)], cwd=executable.parent, env=environment,
                                   stdout=subprocess.DEVNULL, stderr=errors,
                                   creationflags=subprocess.CREATE_NO_WINDOW if os.name == "nt" else 0)
        try:
            record("started", pid=process.pid, executable_sha256=hashlib.sha256(executable.read_bytes()).hexdigest(),
                   root=str(root), settings=settings, approvals="Studio UI only")
            print(json.dumps({"phase": "waiting_for_ui_" + args.recipe + "_plan_apply_save", "pid": process.pid}), flush=True)
            deadline = time.monotonic() + 1800
            while time.monotonic() < deadline:
                reference, after = load_zone(root)
                field = "placements" if args.recipe == "assets" else "gameplay"
                if after["head"]["corrections"].get(field) != before["head"]["corrections"].get(field):
                    report = verify(root, before, reference, after, args.recipe)
                    record("saved_source_verified", **report)
                    print(json.dumps({"phase": "source_verified_close_studio", **report}), flush=True)
                    break
                if process.poll() is not None:
                    raise RuntimeError("Studio closed before the recipe was applied and saved")
                time.sleep(2)
            else:
                raise TimeoutError("Plan recipe did not finish within thirty minutes")
            exit_code = process.wait(timeout=600)
            assert exit_code == 0
            report.update(schema="forge.gate6.studio-client.v2", utc=utc(), exit_code=exit_code,
                          reasoning_requested=True, gate_closed=False)
            with (evidence / (prefix + ".result.json")).open("x", encoding="utf-8") as output:
                json.dump(report, output, ensure_ascii=False, indent=2)
                output.write("\n")
        except BaseException as error:
            record("failed", error=str(error))
            raise
        finally:
            if process.poll() is None:
                process.terminate()
                try:
                    process.wait(timeout=10)
                except subprocess.TimeoutExpired:
                    process.kill()
                    process.wait(timeout=10)


if __name__ == "__main__":
    main()
