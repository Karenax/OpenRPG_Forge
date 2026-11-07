"""Gate 7 source manifest; use the existing bounded physical inventory reader."""
import argparse
import datetime
import importlib.util
import json
from pathlib import Path
import subprocess


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--repository", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--verify", type=Path)
    args = parser.parse_args()
    root = args.repository.resolve(strict=True)
    spec = importlib.util.spec_from_file_location("gate6_inventory", root / "scripts/gate6/capture_inputs.py")
    reader = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(reader)
    reader.ROOTS = (*reader.ROOTS, "scripts/gate7")
    files = reader.capture(root)
    if reader.capture(root) != files:
        raise ValueError("Inputs changed during inventory")
    previous = json.loads(args.verify.read_text("utf-8-sig"))["files"] if args.verify else files
    changed = sorted(name for name in previous.keys() | files.keys() if previous.get(name) != files.get(name))
    def git(*command):
        return subprocess.check_output(["git", "-C", str(root), *command], encoding="utf-8").strip()
    report = {
        "schema": "forge.gate7.physical-source-inputs.v1",
        "utc": datetime.datetime.now(datetime.timezone.utc).isoformat(),
        "repository": str(root), "head": git("rev-parse", "HEAD"),
        "branch": git("branch", "--show-current"),
        "tracked_status": git("status", "--porcelain=v1", "--untracked-files=no"),
        "roots": reader.ROOTS, "excluded_directory_names": sorted(reader.EXCLUDED),
        "files": files, "bytes": sum(item["bytes"] for item in files.values()),
        "changed_inputs": changed, "source_unchanged": not changed,
        "gate_closed": False,
    }
    # The owner-designated central evidence entry point may itself be a junction.
    # It is an explicit destination, never a recursively scanned input root.
    args.output.parent.mkdir(parents=True, exist_ok=True)
    with args.output.open("x", encoding="utf-8") as stream:
        json.dump(report, stream, ensure_ascii=False, indent=2)
        stream.write("\n")
    print(json.dumps({"manifest": str(args.output), "entries": len(files), "bytes": report["bytes"], "changed_inputs": changed}))
    if changed:
        raise SystemExit(1)


if __name__ == "__main__":
    main()
