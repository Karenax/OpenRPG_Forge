"""Bounded physical source inventory; no junctions, work copies or cache traversal."""
import argparse
import datetime
import hashlib
import json
import os
from pathlib import Path
import stat
import subprocess

ROOTS = ("apps", "crates", "tools", "third_party", "examples", ".cargo", "scripts/gate6")
EXCLUDED = {".git", "target", "node_modules", "dist", "gen", "__pycache__", ".trunk", ".stage"}
EXPLICIT = ("Cargo.toml", "Cargo.lock", "rust-toolchain.toml", "scripts/Build-NavigationWorker.ps1", "scripts/Stage-StudioLocalAssets.ps1")


def capture(root):
    pending = [root / name for name in (*ROOTS, *EXPLICIT)]
    files = {}
    total = 0
    while pending:
        path = pending.pop()
        metadata = path.lstat()
        if stat.S_ISLNK(metadata.st_mode) or getattr(metadata, "st_file_attributes", 0) & 0x400:
            raise ValueError(f"Source link is not traversed: {path}")
        if stat.S_ISDIR(metadata.st_mode):
            with os.scandir(path) as entries:
                pending.extend(Path(entry.path) for entry in entries if entry.name not in EXCLUDED)
            continue
        if not stat.S_ISREG(metadata.st_mode):
            raise ValueError(f"Source is not a regular file: {path}")
        relative = path.relative_to(root).as_posix()
        if relative in files:
            continue
        total += metadata.st_size
        if len(files) >= 50_000 or total > 20 * 1024**3:
            raise ValueError("Source inventory exceeds its bounded 20 GiB / 50,000-file scope")
        with path.open("rb") as stream:
            digest = hashlib.file_digest(stream, "sha256").hexdigest()
        after = path.stat(follow_symlinks=False)
        if (metadata.st_size, metadata.st_mtime_ns) != (after.st_size, after.st_mtime_ns):
            raise ValueError(f"Source changed while hashing: {relative}")
        files[relative] = {"sha256": digest, "bytes": metadata.st_size}
    return dict(sorted(files.items()))


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--repository", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--verify", type=Path)
    args = parser.parse_args()
    root = args.repository.resolve(strict=True)
    first = capture(root)
    if capture(root) != first:
        raise ValueError("Source inputs changed during inventory")
    previous = json.loads(args.verify.read_text("utf-8-sig"))["files"] if args.verify else first
    changed = sorted(name for name in previous.keys() | first.keys() if previous.get(name) != first.get(name))
    git = lambda *command: subprocess.check_output(["git", "-C", str(root), *command]).decode("utf-8").strip()
    report = {"schema": "forge.gate6.physical-source-inputs.v1", "utc": datetime.datetime.now(datetime.timezone.utc).isoformat(),
              "repository": str(root), "head": git("rev-parse", "HEAD"), "branch": git("branch", "--show-current"),
              "status": git("status", "--porcelain=v1", "--untracked-files=no"), "roots": ROOTS, "excluded_directory_names": sorted(EXCLUDED),
              "files": first, "bytes": sum(entry["bytes"] for entry in first.values()), "changed_inputs": changed,
              "source_unchanged": not changed, "gate_closed": False}
    with args.output.open("x", encoding="utf-8") as stream:
        json.dump(report, stream, ensure_ascii=False, indent=2)
        stream.write("\n")
    print(json.dumps({"manifest": str(args.output), "entries": len(first), "bytes": report["bytes"], "changed_inputs": changed}))
    if changed:
        raise SystemExit(1)


if __name__ == "__main__":
    main()
