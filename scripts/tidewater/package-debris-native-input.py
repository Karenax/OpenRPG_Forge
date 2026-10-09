"""Assemble bounded native inputs; preserves source packages and never qualifies pixels.
Usage: DEBRIS_PACKAGE COLLISION_FRAMES NATURE_PROGRAM NEW_OUTPUT
"""
import hashlib
import json
from pathlib import Path
import sys


def sha(raw):
    return hashlib.sha256(raw).hexdigest()


def main():
    geometry, collisions, nature, output = [Path(v).resolve() for v in sys.argv[1:]]
    assert not output.exists()
    assert all(not output.is_relative_to(root) for root in (geometry, collisions, nature))
    manifest_raw = (geometry / "package.json").read_bytes()
    assert sha(manifest_raw) == "0a84592f5ccaa0b8077849b6cf054935f6ecb33f41aaf04a19cec3b42255189a"
    manifest = json.loads(manifest_raw)
    payloads = {"geometry/package.json": manifest_raw}
    for name, ref in manifest["files"].items():
        assert Path(name).name == name
        raw = (geometry / name).read_bytes()
        assert len(raw) == ref["bytes"] and sha(raw) == ref["sha256"]
        payloads["geometry/" + name] = raw
    collision_raw = (collisions / "collision-frames.json").read_bytes()
    assert sha(collision_raw) == "8f46a42c3c94fdea04fba2174bc7c85a26c0f56c61887b1c7b8a017c185c7ac2"
    shader = (nature / "nature.wgsl").read_bytes()
    assert sha(shader) == "ee10cf3391d20a3ba9422c4014d022c69b3b49999a4418ecad2ddc965032cab0"
    payloads["collision-frames.json"] = collision_raw
    payloads["nature.wgsl"] = shader
    payloads["program.json"] = (nature / "program.json").read_bytes()
    payloads["CREDITS-scanned.md"] = (collisions / "CREDITS-scanned.md").read_bytes()
    assert sum(map(len, payloads.values())) < 32 * 1024 * 1024
    description = {
        "schema": "forge.tidewater.debris-native-input", "version": 1,
        "source_commit": manifest["source_commit"],
        "files": {name: {"bytes": len(raw), "sha256": sha(raw)} for name, raw in sorted(payloads.items())},
        "limits": "Procedural Debris only; scanned geometry and camera-following pebbles are separate. No runtime draw or visual qualification.",
    }
    output.mkdir()
    for name, raw in payloads.items():
        path = output / name
        path.parent.mkdir(exist_ok=True)
        path.write_bytes(raw)
        assert path.read_bytes() == raw
    descriptor = json.dumps(description, indent=2).encode()
    (output / "debris-native.json").write_bytes(descriptor)
    print(json.dumps({"files": len(payloads) + 1, "bytes": sum(map(len, payloads.values())) + len(descriptor),
                      "descriptor_sha256": sha(descriptor), "output": str(output)}))


if __name__ == "__main__":
    main()
