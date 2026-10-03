"""Package the exact pinned pier as a verified, portable Forge asset."""
from pathlib import Path
import hashlib
import json
import sys


frames, buildings, upstream, output = map(Path, sys.argv[1:])
assert not output.exists()
descriptor = (frames / "pier.json").read_bytes()
records = json.loads(descriptor)
assert records["schema"] == "forge.tidewater.pier-frames" and records["version"] == 1
assert records["source_commit"] == "1438b1abfcaee3267092b75573014f4d9b4a983c"
assert records["triangles"] == 36194 and len(records["pier"]["lanterns"]) == 11
building_manifest = json.loads((buildings / "package.json").read_bytes())
assert building_manifest["schema"] == "forge.tidewater.building-package"
assert building_manifest["source_commit"] == records["source_commit"]
materials_raw = (buildings / "materials.json").read_bytes()
materials_sha = hashlib.sha256(materials_raw).hexdigest()
assert building_manifest["material_library"]["sha256"] == materials_sha
license_raw = (upstream / "LICENSE").read_bytes()
assert b"MIT License" in license_raw and b"Permission is hereby granted" in license_raw
files = {"pier.json": descriptor, "LICENSE-Tidewater.txt": license_raw}


def include(reference):
    name = reference["file"]
    assert Path(name).name == name and name.endswith(".bin") and len(name) == 68
    location = (frames / name).resolve()
    assert location.is_relative_to(frames.resolve())
    raw = location.read_bytes()
    assert len(raw) == reference["bytes"]
    assert hashlib.sha256(raw).hexdigest() == reference["sha256"]
    assert name == reference["sha256"] + ".bin"
    files.setdefault(name, raw)
    assert files[name] == raw


parts = (records["pier"]["static_parts"] + records["pier"]["sign"]["parts"]
         + [part for lantern in records["pier"]["lanterns"] for part in lantern["parts"]])
assert len(parts) == 28
assert {part["material"] for part in parts} == {"material-0", "material-1", "material-6"}
for part in parts:
    for reference in (*part["attributes"].values(), part["indices"],
                      part["reference_position"], part["reference_normal"]):
        include(reference)


def file_entry(raw):
    return {"bytes": len(raw), "sha256": hashlib.sha256(raw).hexdigest()}


manifest = {"schema": "forge.tidewater.pier-package", "version": 1,
            "source_commit": records["source_commit"],
            "building_material_library_sha256": materials_sha,
            "pier": {"file": "pier.json", **file_entry(descriptor)},
            "license": {"file": "LICENSE-Tidewater.txt", **file_entry(license_raw)},
            "files": {name: file_entry(raw) for name, raw in sorted(files.items())},
            "qualification": "verified source geometry and portable payload; Studio rendering pending"}
output.mkdir()
for name, raw in files.items():
    (output / name).write_bytes(raw)
(output / "package.json").write_text(json.dumps(manifest, indent=2), encoding="utf-8")
for name, entry in manifest["files"].items():
    assert file_entry((output / name).read_bytes()) == entry
print(json.dumps({"parts": len(parts), "payloads": len(files),
                  "bytes": sum(len(raw) for raw in files.values()),
                  "package_sha256": hashlib.sha256((output / "package.json").read_bytes()).hexdigest()}))
