"""Package pinned Tidewater water-surface inputs, excluding generated GPU results.

Usage: package-water-surface-assets.py FULL_PREVIEW_BUNDLE BUNDLE_SHA256 UPSTREAM NEW_OUTPUT
"""
import hashlib
import json
from pathlib import Path
import sys

COMMIT = "1438b1abfcaee3267092b75573014f4d9b4a983c"
FILES = (
    "ocean-geometry.wgsl", "water-optics.wgsl", "water-frame.bin",
    "uniform-waterSurface.bin", "uniform-mat.bin", "uniform-skyParams.bin",
    "uniform-whaleMarks.bin", "uniform-shoreP.bin", "shore-field.rgba32.bin",
    "uniform-shoreSimP.bin", "sea-detail-noise.rgba16.bin",
    "uniform-seaDetailParams.bin",
    *(f"foam-m{mip}.rgba16.bin" for mip in range(11)),
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
        path = "scene/" + name
        entry = preview["files"][path]
        data = (bundle / path).read_bytes()
        assert len(data) == entry["bytes"] and digest(data) == entry["sha256"], path
        payloads[name] = data
    assert len(payloads["water-frame.bin"]) == 720
    assert len(payloads["uniform-waterSurface.bin"]) == 32
    assert len(payloads["shore-field.rgba32.bin"]) == 512 * 512 * 16
    assert len(payloads["sea-detail-noise.rgba16.bin"]) == 256 * 256 * 8
    for mip in range(11):
        assert len(payloads[f"foam-m{mip}.rgba16.bin"]) == (1024 >> mip) ** 2 * 8
    for mip in range(10):
        assert len(payloads[f"lace-m{mip}.rgba8.bin"]) == (512 >> mip) ** 2 * 4
    license_bytes = (upstream / "LICENSE").read_bytes()
    assert b"MIT License" in license_bytes and b"Permission is hereby granted" in license_bytes
    payloads["LICENSE-Tidewater.txt"] = license_bytes
    manifest = {
        "schema": "forge.tidewater.water-surface-package", "version": 1,
        "source_commit": COMMIT, "source_bundle_sha256": expected,
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
