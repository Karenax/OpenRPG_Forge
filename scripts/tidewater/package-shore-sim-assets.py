"""Package pinned Tidewater ShoreSim inputs, excluding generated GPU state.

Usage: package-shore-sim-assets.py FULL_PREVIEW_BUNDLE BUNDLE_SHA256 UPSTREAM NEW_OUTPUT
"""
import hashlib
import json
from pathlib import Path
import sys

COMMIT = "1438b1abfcaee3267092b75573014f4d9b4a983c"
SOURCE_SHORE_SHA256 = "21c130a70abaeba0a6ef323140edd3278c62397283962afd32b1527d9a171662"
FILES = (
    "shore-sim.json", "kernel.wgsl", "heights.f32.bin",
    "shore-field.rgba32.bin", "shore-direction.rgba32.bin",
    "shore-params.bin", "simulation-params.bin", "terrain-params.bin",
    *(f"lace-m{mip}.rgba8.bin" for mip in range(10)),
)


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
    for name in FILES:
        key = "shore/" + name
        entry = preview["files"][key]
        data = (bundle / key).read_bytes()
        assert len(data) == entry["bytes"] and digest(data) == entry["sha256"], key
        payloads[name] = data
    assert digest(payloads["shore-sim.json"]) == SOURCE_SHORE_SHA256
    shore = json.loads(payloads["shore-sim.json"])
    assert shore["schema"] == "forge.tidewater.shore-sim"
    assert shore["version"] == 1 and shore["source_commit"] == COMMIT
    assert shore["resolution"] == 768 and shore["initial_state"] == "zero"
    assert shore["direction_mode"] == "original_terrain_fallback"
    assert shore["deposit_schedule"] == []
    for name in FILES[1:]:
        assert shore["files"][name] == digest(payloads[name]), name
    assert len(payloads["heights.f32.bin"]) == 2048 * 2048 * 4
    assert len(payloads["shore-field.rgba32.bin"]) == 512 * 512 * 16
    assert len(payloads["shore-direction.rgba32.bin"]) == 16
    for name, size in (("shore-params.bin", 64), ("simulation-params.bin", 48),
                       ("terrain-params.bin", 32)):
        assert len(payloads[name]) == size, name
    for mip in range(10):
        name = f"lace-m{mip}.rgba8.bin"
        assert len(payloads[name]) == (512 >> mip) ** 2 * 4, name
    license_bytes = (upstream / "LICENSE").read_bytes()
    assert b"MIT License" in license_bytes and b"Permission is hereby granted" in license_bytes
    payloads["LICENSE-Tidewater.txt"] = license_bytes
    manifest = {
        "schema": "forge.tidewater.shore-sim-input-package", "version": 1,
        "source_commit": COMMIT, "source_bundle_sha256": expected,
        "source_shore_sha256": SOURCE_SHORE_SHA256,
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
