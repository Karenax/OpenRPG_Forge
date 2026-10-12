"""Install a delivery into explicit, fresh test roots; retain evidence and user files."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import subprocess


def call(cli, *args, env):
    result = subprocess.run([str(cli), *map(str, args)], env=env, capture_output=True, text=True, encoding="utf-8")
    if result.returncode:
        raise RuntimeError(result.stderr[-4000:])
    return json.loads(result.stdout)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--delivery", type=Path, required=True)
    parser.add_argument("--evidence", type=Path, required=True)
    args = parser.parse_args()
    args.evidence = args.evidence.absolute()
    args.evidence.mkdir(parents=True, exist_ok=False)
    root, user = args.evidence / "installation", args.evidence / "user-data"
    env = dict(os.environ, FORGE_DELIVERY_USER_ROOT=str(user))
    cli = args.delivery / "forge-delivery.exe"
    index = json.loads((args.delivery / "delivery.json").read_bytes())
    packages = []
    for package in index["packages"]:
        path = args.delivery / package["file"]
        with path.open("rb") as stream:
            if hashlib.file_digest(stream, "sha256").hexdigest() != package["archive_sha256"]:
                raise ValueError("Archive index mismatch")
        packages.append(path)
    plan = call(cli, "plan", root, *packages, env=env)
    if root.exists() or user.exists():
        raise ValueError("Prepare unexpectedly wrote test directories")
    plan_path = args.evidence / "install-plan.json"
    plan_path.write_text(json.dumps(plan, ensure_ascii=False), encoding="utf-8")
    applied = call(cli, "apply", plan_path, env=env)
    before = (root / "installation.json").read_bytes()
    replay = call(cli, "apply", plan_path, env=env)
    if applied != replay or before != (root / "installation.json").read_bytes():
        raise ValueError("Apply replay was not idempotent")
    state = call(cli, "verify", root, env=env)
    workspace = user / "workspaces/openrpg-forge"
    if not (workspace / "workspace-v1").is_dir():
        raise ValueError("Editable workspace seed missing")
    report = dict(schema="forge.delivery.validation.v1", status="PASS_LOCAL_INSTALLER", root=str(root), user_root=str(user),
                  validation_script_sha256=hashlib.sha256(Path(__file__).read_bytes()).hexdigest(),
                  packages=len(packages), prepare_read_only=True, apply_replay_idempotent=True, full_file_verification=True,
                  workspace_seed_files=sum(1 for p in workspace.rglob("*") if p.is_file()), revision=state["revision"],
                  visual_acceptance=False, game_rpg_qualified=False, gate3_closed=False, release_qualified=False)
    (args.evidence / "verification.json").write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(report, ensure_ascii=False), flush=True)


if __name__ == "__main__":
    main()
