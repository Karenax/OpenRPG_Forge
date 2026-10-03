"""Package the pinned Tidewater FFT inputs for Studio, excluding probe GPU outputs.

Usage: package-ocean-assets.py FULL_PREVIEW_BUNDLE BUNDLE_SHA256 UPSTREAM NEW_OUTPUT
"""
import hashlib
import json
from pathlib import Path
import sys

COMMIT = "1438b1abfcaee3267092b75573014f4d9b4a983c"
KERNELS = (
    "initSpectrum", "conjugate", "copyH0", "row", "column",
    "mipA-0", "mipA-1", "mipB-0", "mipB-1",
)
FILES = ("ocean.json", "params-initial.bin", "frame-120-frame.bin",
         "frame-120-params.bin", *(name + ".wgsl" for name in KERNELS))


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
        path = "ocean/" + name
        entry = preview["files"][path]
        data = (bundle / path).read_bytes()
        assert len(data) == entry["bytes"] and digest(data) == entry["sha256"], path
        payloads[name] = data
    metadata = json.loads(payloads["ocean.json"])
    assert metadata["schema"] == "forge.tidewater.ocean-probe"
    assert metadata["version"] == 1 and metadata["source_commit"] == COMMIT
    assert metadata["fft_size"] == 256 and metadata["cascades"] == 4
    for name in FILES[1:]:
        assert metadata["files"][name] == digest(payloads[name]), name
    license_bytes = (upstream / "LICENSE").read_bytes()
    assert b"MIT License" in license_bytes and b"Permission is hereby granted" in license_bytes
    payloads["LICENSE-Tidewater.txt"] = license_bytes
    manifest = {
        "schema": "forge.tidewater.ocean-input-package", "version": 1,
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
