"""Pinned plant rendering resources. Canonical populations remain in the workspace."""
import argparse
import hashlib
import json
import re
from pathlib import Path

COMMIT = "1438b1abfcaee3267092b75573014f4d9b4a983c"
SCHEMA = "forge.tidewater.plant-package"
BASE = {"vegetation-scene.json", "grass.json", "plant.wgsl", "detail.bin", "leaf.bin",
        "impostor-a.bin", "impostor-b.bin", "LICENSE-Tidewater.txt"}
META = "plant-textures.json"
LIMIT = 64 * 1024 * 1024
TOTAL = 128 * 1024 * 1024


def digest(data):
    return hashlib.sha256(data).hexdigest()


def read(root, name, limit=LIMIT):
    if not isinstance(name, str) or not re.fullmatch(r"[A-Za-z0-9][A-Za-z0-9_.-]{0,127}", name):
        raise ValueError("Invalid flat payload name")
    if root.is_symlink() or root.is_junction() or not root.is_dir():
        raise ValueError("Root must be a real directory")
    path = root / name
    if path.is_symlink() or path.is_junction() or not path.is_file():
        raise ValueError("Payload must be a regular file")
    with path.open("rb") as stream:
        data = stream.read(limit + 1)
    if not data or len(data) > limit:
        raise ValueError("Payload size limit")
    return data


def dependencies(vegetation, grass):
    if (vegetation["schema"] != "forge.tidewater.vegetation-scene-probe"
            or vegetation["version"] != 1 or len(vegetation["groups"]) != 6
            or sum(g["count"] for g in vegetation["groups"]) != 65383
            or grass["schema"] != "forge.tidewater.grass-probe"
            or grass["version"] != 1 or grass["source_commit"] != COMMIT
            or len(grass["levels"]) != 3):
        raise ValueError("Unsupported plant descriptions")
    names = BASE | {grass["flags"], grass["min_y"], grass["max_y"], grass["mask"]["file"]}
    levels = list(grass["levels"])
    for group in vegetation["groups"]:
        if not 1 <= len(group["levels"]) <= 2:
            raise ValueError("Invalid plant LOD count")
        levels.extend(group["levels"])
    for level in levels:
        names.update(level["attributes"].values())
        names.add(level["indices"])
    if len(names) > 255:
        raise ValueError("Resource count limit")
    return names


def verify(root, expected):
    raw = read(root, "package.json", 256 * 1024)
    if digest(raw) != expected:
        raise ValueError("Package manifest pin differs")
    manifest = json.loads(raw)
    if (set(manifest) != {"schema", "version", "source_commit", "source_fixture_sha256", "files"}
            or manifest["schema"] != SCHEMA or manifest["version"] != 1
            or manifest["source_commit"] != COMMIT
            or not re.fullmatch(r"[0-9a-f]{64}", manifest["source_fixture_sha256"])):
        raise ValueError("Package manifest contract differs")
    names = dependencies(json.loads(read(root, "vegetation-scene.json", 1024 * 1024)),
                         json.loads(read(root, "grass.json", 1024 * 1024))) | {META}
    if set(manifest["files"]) != names or {p.name for p in root.iterdir()} != names | {"package.json"}:
        raise ValueError("Package dependency closure differs")
    total = 0
    for name in sorted(names):
        data = read(root, name)
        total += len(data)
        if total > TOTAL or manifest["files"][name] != {"sha256": digest(data), "bytes": len(data)}:
            raise ValueError("Payload pin, length or total differs")
    meta = json.loads(read(root, META, 16384))
    if meta != {"schema": "forge.tidewater.plant-textures", "version": 1,
                "leaf": {"width": 1024, "height": 1024, "mips": 11},
                "impostor": {"width": 3840, "height": 768, "mips": 12}}:
        raise ValueError("Texture metadata differs")
    return {"manifest_sha256": expected, "payload_files": len(names), "payload_bytes": total}


def build(scene, expected, output):
    if output.exists() or output.is_symlink() or output.is_junction():
        raise ValueError("Output already exists")
    raw = read(scene, "fixture.json", 4 * 1024 * 1024)
    if digest(raw) != expected:
        raise ValueError("Fixture pin differs")
    fixture = json.loads(raw)
    if (fixture["schema"] != "forge.tidewater.plant-probe" or fixture["version"] != 2
            or fixture["source"]["commit"] != COMMIT):
        raise ValueError("Source fixture differs")
    descriptions = {}
    for name in ("vegetation-scene.json", "grass.json"):
        data = read(scene, name, 1024 * 1024)
        if fixture["files"].get(name) != digest(data):
            raise ValueError("Description pin differs")
        descriptions[name] = json.loads(data)
    names = dependencies(descriptions["vegetation-scene.json"], descriptions["grass.json"])
    payloads = {}
    for name in sorted(names):
        data = read(scene, name)
        if fixture["files"].get(name) != digest(data):
            raise ValueError("Source payload pin differs")
        payloads[name] = data
    payloads[META] = (json.dumps({"schema": "forge.tidewater.plant-textures", "version": 1,
                                "leaf": fixture["leaf_texture"], "impostor": fixture["impostor_texture"]},
                               indent=2, sort_keys=True) + "\n").encode("utf-8")
    if sum(map(len, payloads.values())) > TOTAL:
        raise ValueError("Package total limit")
    manifest = {"schema": SCHEMA, "version": 1, "source_commit": COMMIT,
                "source_fixture_sha256": expected,
                "files": {n: {"sha256": digest(b), "bytes": len(b)} for n, b in sorted(payloads.items())}}
    encoded = (json.dumps(manifest, indent=2, sort_keys=True) + "\n").encode("utf-8")
    output.mkdir()
    for name, data in {**payloads, "package.json": encoded}.items():
        with (output / name).open("xb") as stream:
            stream.write(data)
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
    result = (build(args.scene, args.expected_fixture_sha256, args.output) if args.mode == "build"
              else verify(args.root, args.expected_manifest_sha256))
    print(json.dumps(result, indent=2))


if __name__ == "__main__":
    main()
