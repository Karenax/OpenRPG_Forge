"""Snapshot the shared checkout without copying product sources or changing Git."""
import argparse
from datetime import datetime, timezone
import importlib.util
import json
from pathlib import Path
import subprocess

ROOT = Path(__file__).resolve().parents[1]


def snapshot():
    spec = importlib.util.spec_from_file_location("working_tree_validator", ROOT / "scripts/Validate-Gate3WorkingTree.py")
    validator = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(validator)
    build, checkout = validator.source_inventories()
    def git(*args):
        return subprocess.check_output(["git", *args], cwd=ROOT)
    tracked = set(filter(None, git("ls-files", "--cached", "-z").decode("utf-8").split("\0")))
    changed = set(filter(None, git("diff", "HEAD", "--name-only", "-z").decode("utf-8").split("\0")))
    return {
        "schema": "forge.shared-checkout-inputs.v1",
        "timestamp_utc": datetime.now(timezone.utc).isoformat(),
        "head": git("rev-parse", "HEAD").decode().strip(),
        "branch": git("branch", "--show-current").decode().strip(),
        "build_inputs": build,
        "checkout_files": {name: {"sha256": pin, "tracked": name in tracked, "changed_from_head": name in changed or name not in tracked} for name, pin in checkout.items()},
        "informative_entries": validator.working_tree_inventory(checkout),
    }


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--compare", type=Path)
    args = parser.parse_args()
    if args.output.exists():
        raise SystemExit("Output already exists; select a fresh receipt path")
    data = snapshot()
    changed = []
    identity_changed = False
    if args.compare:
        baseline = json.loads(args.compare.read_text(encoding="utf-8-sig"))
        previous = baseline["build_inputs"]
        current = data["build_inputs"]
        changed = sorted(name for name in previous.keys() | current.keys() if previous.get(name) != current.get(name) or (name in previous) != (name in current))
        identity_changed = any(baseline[key] != data[key] for key in ("head", "branch"))
        data["comparison"] = {"baseline": str(args.compare), "changed_build_inputs": changed, "git_identity_changed": identity_changed, "passed": not changed and not identity_changed}
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(data, indent=2, ensure_ascii=False) + "\n", encoding="utf-8", newline="\n")
    print(f"Build inputs: {len(data['build_inputs'])}; checkout entries: {len(data['checkout_files'])}; changed: {len(changed)}")
    if changed or identity_changed:
        print("\n".join(changed))
        if identity_changed:
            print("HEAD or branch changed during validation")
        raise SystemExit(1)


if __name__ == "__main__":
    main()
