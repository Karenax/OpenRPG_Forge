"""Inventory the shared checkout without copying or mutating build inputs."""
import argparse
import datetime
import hashlib
import json
from pathlib import Path
import subprocess


def git(root, *args):
    return subprocess.check_output(["git", "-C", str(root), *args])


def capture(root):
    names = sorted(set(git(root, "ls-files", "-z", "--cached", "--others",
                           "--exclude-standard").decode("utf-8").rstrip("\0").split("\0")))
    files = {}
    for name in names:
        path = root / name
        try:
            present = path.exists()
        except PermissionError:
            # Historical evidence links may be inaccessible; never call them deleted.
            if name.split("/", 1)[0] in {"crates", "apps", "tools", "third_party", "examples", ".cargo"}:
                raise
            files[name] = {"unreadable": True, "reason": "permission_denied"}
            continue
        if not present:
            files[name] = {"deleted": True}
            continue
        if not path.is_file() or path.is_symlink():
            raise ValueError(f"Unsupported input: {name}")
        with path.open("rb") as stream:
            digest = hashlib.file_digest(stream, "sha256").hexdigest()
        files[name] = {"sha256": digest, "bytes": path.stat().st_size}
    return files


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--repository", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    root = args.repository.resolve(strict=True)
    first = capture(root)
    second = capture(root)
    if first != second:
        raise ValueError("Inputs changed during inventory; retry after concurrent edits")
    result = {
        "schema": "forge.gate5.source-inputs.v1",
        "utc": datetime.datetime.now(datetime.timezone.utc).isoformat(),
        "repository": str(root),
        "head": git(root, "rev-parse", "HEAD").decode().strip(),
        "branch": git(root, "branch", "--show-current").decode().strip(),
        "status": git(root, "status", "--porcelain=v1").decode("utf-8"),
        "files": first,
    }
    args.output.parent.mkdir(parents=True, exist_ok=True)
    with args.output.open("x", encoding="utf-8") as stream:
        json.dump(result, stream, ensure_ascii=False, indent=2)
        stream.write("\n")
    print(json.dumps({"manifest": str(args.output), "entries": len(first),
                      "head": result["head"]}))


if __name__ == "__main__":
    main()
