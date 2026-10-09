"""Combine acquired scanned resources into a bounded standalone input package.

Usage: GEOMETRY ATLASES MATERIAL PROGRAM COLLISIONS NEW_OUTPUT
Does not qualify renderer adoption, editing, licence review or visual parity.
"""
import hashlib
import json
from pathlib import Path
import sys

PIN = "1438b1abfcaee3267092b75573014f4d9b4a983c"


def sha(raw):
    return hashlib.sha256(raw).hexdigest()


def main():
    geometry, atlases, material, program, collisions, output = [Path(arg).resolve() for arg in sys.argv[1:]]
    assert not output.exists()
    assert all(not output.is_relative_to(root) for root in [geometry, atlases, material, program, collisions])
    payloads, descriptors = {}, {}

    def descriptor(root, file, folder, expected):
        raw = (root / file).read_bytes()
        assert len(raw) <= 1024 * 1024 and sha(raw) == expected
        index = json.loads(raw)
        assert index["source_commit"] == PIN and index["version"] == 1
        payloads[folder + "/" + file] = raw
        descriptors[folder] = {"file": folder + "/" + file, "bytes": len(raw), "sha256": sha(raw)}
        return index

    def acquire(root, folder, file, ref):
        assert Path(file).name == file
        path = (root / file).resolve()
        assert path.parent == root
        assert ref["bytes"] <= 24 * 1024 * 1024
        assert path.stat().st_size == ref["bytes"]
        raw = path.read_bytes()
        assert sha(raw) == ref["sha256"]
        payloads[folder + "/" + file] = raw

    geo = descriptor(geometry, "scanned-geometry.json", "geometry", "666bbe16969d9289aee14ca721d380d9772dfec59335e933901853971987875d")
    atlas = descriptor(atlases, "scanned-atlases.json", "atlases", "276999b1e6d4188f76a7796b82e2443597d12f6f7707b49358bf53be52fd0c0e")
    mat = descriptor(material, "scanned-material.json", "material", "d8aac6e8e5887f3b1ad194f550b619232c745966769c2b13a9873dd38ce6bfd5")
    program_raw = (program / "program.json").read_bytes()
    assert len(program_raw) <= 65536
    prog = json.loads(program_raw)
    assert prog["schema"] == "forge.tidewater.debris-scanned-program" and prog["version"] == 1 and prog["source_commit"] == PIN
    assert prog["shader"]["sha256"] == "f1870e89558c87bf103d192fe179c27c4df901bd2d4e8a96d57ee9a8a566b0c6"
    assert prog["material_sha256"] == descriptors["material"]["sha256"]
    assert prog["atlas_descriptor_sha256"] == descriptors["atlases"]["sha256"] == mat["atlas_descriptor_sha256"]
    assert geo["source_descriptor_sha256"] == atlas["retained_descriptor_sha256"]
    assert geo["source_inputs"]["scanned_contract"] == atlas["source_inputs"]["src/world/debris/ScannedDebris.js"]
    payloads["program/program.json"] = program_raw
    descriptors["program"] = {"file": "program/program.json", "bytes": len(program_raw), "sha256": sha(program_raw)}
    for root, folder, index in [(geometry, "geometry", geo), (atlases, "atlases", atlas), (material, "material", mat)]:
        for name, ref in index["files"].items():
            acquire(root, folder, name, ref)
    acquire(program, "program", prog["shader"]["file"], prog["shader"])
    col = descriptor(collisions, "collision-frames.json", "collisions", "8f46a42c3c94fdea04fba2174bc7c85a26c0f56c61887b1c7b8a017c185c7ac2")
    assert col["scanned_geometry_sha256"] == descriptors["geometry"]["sha256"]
    owners = {owner["source_id"]: owner for owner in col["owners"] if owner["category"] == "scanned"}
    assert len(owners) == len(geo["instances"]) == 141
    assert len(geo["meshes"]) == 12
    for instance in geo["instances"]:
        owner = owners[instance["source_id"]]
        assert max(abs(a-b) for a, b in zip(owner["source_matrix_column_major"], instance["matrix_column_major"], strict=True)) < 1e-12
        assert owner["source_frame"]["rotation_xyzw"] == instance["quaternion_xyzw"]
    assert sum(len(owner["colliders"]) for owner in owners.values()) == 60
    assert payloads["geometry/LICENSE-Tidewater.txt"] == payloads["atlases/LICENSE"] == payloads["material/LICENSE"]
    assert payloads["geometry/CREDITS-scanned.md"] == payloads["atlases/CREDITS.md"] == payloads["material/CREDITS.md"]
    assert len(payloads) < 100 and sum(map(len, payloads.values())) < 80 * 1024 * 1024
    manifest = {"schema": "forge.tidewater.debris-scanned-package", "version": 1, "source_commit": PIN,
                "descriptors": descriptors,
                "files": {name: {"bytes": len(raw), "sha256": sha(raw)} for name, raw in sorted(payloads.items())},
                "counts": {"assets": 4, "lod_meshes": 12, "instances": 141, "scanned_colliders": 60, "atlas_mips": 36},
                "preparation_sha256": sha(Path(__file__).read_bytes()),
                "limits": "Standalone acquired resources only; Rust loading, LOD, caster routing, physics contacts, Studio and visual parity remain unqualified."}
    raw = json.dumps(manifest, indent=2).encode("utf-8")
    output.mkdir()
    for name, data in payloads.items():
        path = output / name
        path.parent.mkdir(exist_ok=True)
        with path.open("xb") as file:
            file.write(data)
        assert path.read_bytes() == data
    with (output / "package.json").open("xb") as file:
        file.write(raw)
    print(json.dumps({"status": "PASS", "files": len(payloads) + 1, "bytes": sum(map(len, payloads.values())) + len(raw), "package_sha256": sha(raw)}))


if __name__ == "__main__":
    main()
