"""Bundle the existing native preview inputs without changing authored documents."""
import argparse
import hashlib
import json
from pathlib import Path
import uuid

FOLDERS = {"scene", "workspace", "buildings", "ocean", "shore", "spray", "clouds"}
COMMIT = "1438b1abfcaee3267092b75573014f4d9b4a983c"
MAX_FILE = 128 * 1024 * 1024
MAX_TOTAL = 1024 * 1024 * 1024
MAX_FILES = 10000


def digest(data):
    return hashlib.sha256(data).hexdigest()


def files(root):
    if root.is_symlink() or root.is_junction() or not root.is_dir():
        raise ValueError("input must be a real directory")
    for item in sorted(root.iterdir()):
        if item.is_symlink() or item.is_junction():
            raise ValueError("links are not accepted")
        if item.is_dir():
            yield from files(item)
        elif item.is_file():
            yield item
        else:
            raise ValueError("nonregular input")


def read(path):
    with path.open("rb") as stream:
        data = stream.read(MAX_FILE + 1)
    if len(data) > MAX_FILE:
        raise ValueError("file quota exceeded")
    return data


def verify(root, expected):
    with (root / "preview-scene.json").open("rb") as stream:
        raw = stream.read(4 * 1024 * 1024 + 1)
    if len(raw) > 4 * 1024 * 1024 or digest(raw) != expected:
        raise ValueError("manifest quota or pin mismatch")
    manifest = json.loads(raw)
    if set(manifest) != {"schema", "version", "source_commit", "area_id", "building_package_sha256", "files", "scope"} or manifest["source_commit"] != COMMIT:
        raise ValueError("unknown manifest fields or source")
    uuid.UUID(manifest["area_id"])
    if manifest["schema"] != "forge.tidewater.preview-scene" or manifest["version"] != 1:
        raise ValueError("unknown bundle schema")
    entries = manifest["files"]
    if not 1 <= len(entries) <= MAX_FILES:
        raise ValueError("file count quota")
    if {name.split("/")[0] for name in entries} != FOLDERS:
        raise ValueError("missing input group")
    actual = {p.relative_to(root).as_posix(): p for p in files(root)}
    if set(actual) != set(entries) | {"preview-scene.json"}:
        raise ValueError("incomplete bundle or unexpected file")
    total = 0
    for name, record in entries.items():
        data = read(actual[name])
        total += len(data)
        if total > MAX_TOTAL or record != {"bytes": len(data), "sha256": digest(data)}:
            raise ValueError("bundle size or digest mismatch: " + name)
    if digest(read(root / "buildings/package.json")) != manifest["building_package_sha256"]:
        raise ValueError("building package pin differs")
    return {"manifest_sha256": digest(read(root / "preview-scene.json")), "files": len(entries), "bytes": total}


def bundle(config_path, output):
    config = json.loads(config_path.read_text(encoding="utf-8"))
    if set(config) != {"folders", "source_commit", "area_id", "building_package_sha256"}:
        raise ValueError("unknown configuration fields")
    if set(config["folders"]) != FOLDERS or config["source_commit"] != COMMIT:
        raise ValueError("bundle inputs or source differ")
    uuid.UUID(config["area_id"])
    if output.exists():
        raise ValueError("output exists; choose a new directory")
    entries, sources, total = {}, {}, 0
    for label, source in sorted(config["folders"].items()):
        root = Path(source)
        for path in files(root):
            name = label + "/" + path.relative_to(root).as_posix()
            data = read(path)
            entries[name] = {"bytes": len(data), "sha256": digest(data)}
            sources[name] = path
            total += len(data)
            if len(entries) > MAX_FILES or total > MAX_TOTAL:
                raise ValueError("bundle quota exceeded")
    if entries["buildings/package.json"]["sha256"] != config["building_package_sha256"]:
        raise ValueError("building package pin differs")
    # Full preflight precedes any output. Recheck source bytes during copying.
    output.mkdir(parents=True)
    for name, source in sources.items():
        data = read(source)
        if entries[name] != {"bytes": len(data), "sha256": digest(data)}:
            raise ValueError("source changed during packaging")
        target = output / name
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_bytes(data)
    manifest = {"schema": "forge.tidewater.preview-scene", "version": 1,
                "source_commit": COMMIT, "area_id": config["area_id"],
                "building_package_sha256": config["building_package_sha256"], "files": entries,
                "scope": "Native fixed-view prototype only; not a distributable module or complete Studio map"}
    (output / "preview-scene.json").write_text(json.dumps(manifest, sort_keys=True, indent=2) + "\n", encoding="utf-8")
    return verify(output, digest(read(output / "preview-scene.json")))


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--config", type=Path)
    parser.add_argument("--output", type=Path)
    parser.add_argument("--verify", type=Path)
    parser.add_argument("--expected-sha256")
    args = parser.parse_args()
    if args.verify and args.expected_sha256 and not args.config and not args.output:
        result = verify(args.verify, args.expected_sha256)
    elif args.config and args.output and not args.verify and not args.expected_sha256:
        result = bundle(args.config, args.output)
    else:
        parser.error("use --config CONFIG --output NEW_DIRECTORY, or --verify DIRECTORY --expected-sha256 PIN")
    print(json.dumps(result, sort_keys=True))
