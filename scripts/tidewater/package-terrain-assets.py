"""Pinned terrain rendering resources; canonical height rasters remain in the workspace."""
import argparse
import hashlib
import json
from pathlib import Path

COMMIT = "1438b1abfcaee3267092b75573014f4d9b4a983c"
LIMITS = {"terrain.wgsl": 1_000_000, "terrain-lod.wgsl": 1_000_000,
          "terrain-normal.bin": 22369620, "terrain-splat.bin": 22369620,
          "terrain-detail.bin": 1398100, "LICENSE-Tidewater.txt": 32768}
LIMITS_V2 = {**LIMITS, "hill-shadow.wgsl": 1_000_000}
SCHEMA = "forge.tidewater.terrain-package"


def digest(data):
    return hashlib.sha256(data).hexdigest()


def read(root, name, limit):
    path = root / name
    if root.is_symlink() or root.is_junction() or not root.is_dir():
        raise ValueError("Package root must be a real directory")
    if path.is_symlink() or path.is_junction() or not path.is_file():
        raise ValueError("Payload must be a regular file")
    with path.open("rb") as stream:
        data = stream.read(limit + 1)
    if not data or len(data) > limit:
        raise ValueError("Payload size limit exceeded")
    return data


def shape(name, data):
    if name.endswith(".bin") and len(data) != LIMITS[name]:
        raise ValueError("Terrain mip chain size differs")
    if not name.endswith(".bin"):
        data.decode("utf-8")


def verify(root, expected):
    raw = read(root, "package.json", 16384)
    if digest(raw) != expected:
        raise ValueError("Terrain manifest pin differs")
    manifest = json.loads(raw)
    if set(manifest) != {"schema", "version", "source_commit", "files"}:
        raise ValueError("Unknown terrain manifest fields")
    limits = LIMITS if manifest["version"] == 1 else LIMITS_V2
    if (manifest["schema"] != SCHEMA or manifest["version"] not in (1, 2)
            or manifest["source_commit"] != COMMIT
            or set(manifest["files"]) != set(limits)):
        raise ValueError("Terrain manifest contract differs")
    if {p.name for p in root.iterdir()} != set(limits) | {"package.json"}:
        raise ValueError("Unexpected package inventory")
    total = 0
    for name, entry in manifest["files"].items():
        if set(entry) != {"sha256", "bytes"}:
            raise ValueError("Unknown payload fields")
        data = read(root, name, limits[name])
        shape(name, data)
        if entry != {"sha256": digest(data), "bytes": len(data)}:
            raise ValueError("Terrain payload pin or length differs")
        total += len(data)
    return {"manifest_sha256": expected, "payload_files": len(limits),
            "payload_bytes": total}


def build(scene, expected, output):
    if output.exists() or output.is_symlink() or output.is_junction():
        raise ValueError("Output already exists")
    raw = read(scene, "fixture.json", 4 * 1024 * 1024)
    if digest(raw) != expected:
        raise ValueError("Source fixture pin differs")
    fixture = json.loads(raw)
    if (fixture["schema"] != "forge.tidewater.plant-probe"
            or fixture["version"] != 2 or fixture["source"]["commit"] != COMMIT):
        raise ValueError("Source fixture contract differs")
    payloads = {}
    for name, limit in LIMITS_V2.items():
        data = read(scene, name, limit)
        shape(name, data)
        if fixture["files"].get(name) != digest(data):
            raise ValueError("Source terrain payload pin differs")
        payloads[name] = data
    manifest = {"schema": SCHEMA, "version": 2, "source_commit": COMMIT,
                "files": {name: {"sha256": digest(data), "bytes": len(data)}
                          for name, data in sorted(payloads.items())}}
    encoded = (json.dumps(manifest, indent=2, sort_keys=True) + "\n").encode("utf-8")
    output.mkdir()
    for name, data in payloads.items():
        with (output / name).open("xb") as stream:
            stream.write(data)
    with (output / "package.json").open("xb") as stream:
        stream.write(encoded)
    return verify(output, digest(encoded))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    modes = parser.add_subparsers(dest="mode", required=True)
    create = modes.add_parser("build")
    create.add_argument("scene", type=Path)
    create.add_argument("expected_fixture_sha256")
    create.add_argument("output", type=Path)
    check = modes.add_parser("verify")
    check.add_argument("root", type=Path)
    check.add_argument("expected_manifest_sha256")
    args = parser.parse_args()
    result = (build(args.scene, args.expected_fixture_sha256, args.output)
              if args.mode == "build" else verify(args.root, args.expected_manifest_sha256))
    print(json.dumps(result, indent=2))


if __name__ == "__main__":
    main()
