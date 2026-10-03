"""Package Tidewater's pinned, fixed-sun ground-bounce source for Studio.

Usage: package-ground-bounce-assets.py FULL_PREVIEW_BUNDLE BUNDLE_SHA256 UPSTREAM NEW_OUTPUT
"""
import hashlib
import json
from pathlib import Path
import sys

COMMIT = "1438b1abfcaee3267092b75573014f4d9b4a983c"
DESCRIPTOR_SHA256 = "db19ec968d1dcd8dd0c9d48d23bb189dcccfe24e1b435fb61fc3326e2bcf5c9b"
MAP_SHA256 = "70e99b2c01613f1f8b064d462a88e5ff8717d557b9248810efe4ae089a561843"
SOURCE_FILES = ("ground-bounce.json", "ground-bounce.rgba16.bin")


def digest(data):
    return hashlib.sha256(data).hexdigest()


def package(bundle, expected, upstream, output):
    assert not output.exists(), "Choose a new output directory"
    raw = (bundle / "preview-scene.json").read_bytes()
    assert digest(raw) == expected, "Preview bundle pin mismatch"
    preview = json.loads(raw)
    assert preview["schema"] == "forge.tidewater.preview-scene"
    assert preview["version"] == 1 and preview["source_commit"] == COMMIT
    payloads = {}
    for name in SOURCE_FILES:
        key = "scene/" + name
        entry = preview["files"][key]
        data = (bundle / key).read_bytes()
        assert len(data) == entry["bytes"] and digest(data) == entry["sha256"], key
        payloads[name] = data
    assert digest(payloads["ground-bounce.json"]) == DESCRIPTOR_SHA256
    assert digest(payloads["ground-bounce.rgba16.bin"]) == MAP_SHA256
    descriptor = json.loads(payloads["ground-bounce.json"])
    assert descriptor["schema"] == "forge.tidewater.ground-bounce-probe"
    assert descriptor["version"] == 1 and descriptor["source_commit"] == COMMIT
    assert descriptor["width"] == descriptor["height"] == 512
    assert descriptor["format"] == "rgba16float"
    assert descriptor["files"]["ground-bounce.rgba16.bin"] == MAP_SHA256
    assert len(payloads["ground-bounce.rgba16.bin"]) == 512 * 512 * 8
    license_bytes = (upstream / "LICENSE").read_bytes()
    assert b"MIT License" in license_bytes and b"Permission is hereby granted" in license_bytes
    payloads["LICENSE-Tidewater.txt"] = license_bytes
    manifest = {
        "schema": "forge.tidewater.ground-bounce-input-package", "version": 1,
        "source_commit": COMMIT, "source_bundle_sha256": expected,
        "source_descriptor_sha256": DESCRIPTOR_SHA256,
        "files": {name: {"sha256": digest(data), "bytes": len(data)}
                  for name, data in sorted(payloads.items())},
    }
    encoded = json.dumps(manifest, indent=2).encode()
    output.mkdir()
    for name, data in payloads.items():
        (output / name).write_bytes(data)
    (output / "package.json").write_bytes(encoded)
    return {"files": len(payloads), "bytes": sum(map(len, payloads.values())),
            "sha256": digest(encoded)}


if __name__ == "__main__":
    assert len(sys.argv) == 5, __doc__
    print(json.dumps(package(Path(sys.argv[1]), sys.argv[2], Path(sys.argv[3]), Path(sys.argv[4]))))
