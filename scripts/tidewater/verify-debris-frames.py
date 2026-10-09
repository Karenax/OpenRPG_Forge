"""Reopen portable Debris frames and compare every part with acquired source data."""
import hashlib
import json
from pathlib import Path
import sys

import numpy as np


def sha(raw):
    return hashlib.sha256(raw).hexdigest()


def main():
    first, repeat, acquisition, assembly, receipt = map(Path, sys.argv[1:])
    assert not receipt.exists()
    manifest = json.loads((first / "package.json").read_bytes())
    assert manifest["schema"] == "forge.tidewater.debris-package" and manifest["version"] == 1
    files = {p.name: sha(p.read_bytes()) for p in first.iterdir() if p.is_file()}
    assert files == {p.name: sha(p.read_bytes()) for p in repeat.iterdir() if p.is_file()}
    references = set()

    def read(reference, dtype=None):
        name = reference["file"]
        assert Path(name).name == name and name == reference["sha256"] + ".bin"
        assert manifest["files"][name] == {key: reference[key] for key in ["bytes", "sha256"]}
        raw = (first / name).read_bytes()
        assert len(raw) == reference["bytes"] and sha(raw) == reference["sha256"]
        references.add(name)
        return np.frombuffer(raw, dtype) if dtype else raw

    def visit(value):
        if isinstance(value, dict):
            if "file" in value:
                read(value)
            for child in value.values():
                visit(child)
        elif isinstance(value, list):
            for child in value:
                visit(child)

    frames = json.loads(read(manifest["frames"]))
    ownership_bytes = (acquisition / "ownership.json").read_bytes()
    ownership = json.loads(ownership_bytes)
    assert frames["schema"] == "forge.tidewater.debris-frames" and frames["version"] == 1
    assert frames["source_ownership_sha256"] == sha(ownership_bytes)
    assert frames["source_assembly_sha256"] == sha((assembly / "assembly.json").read_bytes())
    assert frames["source_commit"] == manifest["source_commit"] == ownership["source_commit"]
    assert frames["scanned_instances"] == ownership["scanned_instances"]
    assert len(frames["props"]) == len(ownership["owners"])
    triangles = shadow_triangles = 0
    max_error = 0.0
    for obj, original in zip(frames["props"], ownership["owners"]):
        assert obj["source_id"] == original["source_id"] and obj["kind"] == original["kind"]
        assert obj["source_anchor_xz"] == original["source_anchor_xz"]
        assert len(obj["parts"]) == len(original["parts"])
        frame = obj["source_frame"]
        anchor = np.array(frame["translation_m"])
        assert np.isfinite(anchor).all() and frame["rotation_xyzw"] == [0, 0, 0, 1] and frame["scale"] == [1, 1, 1]
        local_points = []
        for part, (batch, interval) in zip(obj["parts"], original["parts"].items()):
            assert part["source_material"] == batch
            low, high = interval["vertex_start"], interval["vertex_start"] + interval["vertices"]
            attributes = {}
            for key, width in {"pos": 3, "nrm": 3, "uv": 2, "tint": 3, "data": 4}.items():
                raw_key = "contact-tint" if key == "tint" else key
                attributes[key] = np.frombuffer((acquisition / f"{batch}.{raw_key}.f64.bin").read_bytes(), "<f8").reshape(-1, width)[low:high].copy()
            material = {"rope": "hard", "net": "fabric"}.get(batch, batch)
            assert part["material"] == material and part["vertices"] == high - low
            if batch == "rope":
                attributes["data"][:, 3] = 2 + attributes["data"][:, 1]
                attributes["data"][:, 1:3] = 0
            elif batch == "net":
                attributes["data"][:, 2] = np.maximum(.02, attributes["data"][:, 2])
                attributes["data"][:, 3] = 0
            reference_position = read(part["reference_position"], "<f4").reshape(-1, 3)
            assert np.array_equal(reference_position, attributes["pos"].astype("<f4"))
            local = read(part["attributes"]["pos"], "<f4").reshape(-1, 3)
            error = float(np.max(np.abs(local.astype(np.float64) + anchor - reference_position)))
            assert error < .0001 and error == part["roundtrip_position_max_m"]
            max_error = max(max_error, error)
            local_points.append(local)
            for key in ["nrm", "uv", "tint", "data"]:
                actual = read(part["attributes"][key], "<f4").reshape(attributes[key].shape)
                assert np.array_equal(actual, attributes[key].astype("<f4"))
            begin, end = interval["index_start"], interval["index_start"] + interval["indices"]
            expected_indices = np.frombuffer((acquisition / f"{batch}.indices.u32.bin").read_bytes(), "<u4")[begin:end].astype(np.int64) - low
            indices = read(part["indices"], "<u4").reshape(-1, 3)
            assert np.array_equal(indices.ravel(), expected_indices)
            if material == "nature":
                d = attributes["data"][indices[:, 0]]
                cast = ((d[:, 1] == 0) & (d[:, 3] >= .2)) | ((d[:, 1] == 6) & (d[:, 3] >= .1)) | (
                    (d[:, 1] == 4) & (d[:, 3] >= .15) & (d[:, 2] >= 2))
            else:
                cast = np.full(len(indices), material in ["wood", "hard"], dtype=bool)
            shadow = read(part["shadow_indices"], "<u4").reshape(-1, 3)
            assert np.array_equal(shadow, indices[cast])
            triangles += len(indices)
            shadow_triangles += len(shadow)
        points = np.concatenate(local_points)
        assert obj["local_bounds"] == {"min": points.min(axis=0).tolist(), "max": points.max(axis=0).tolist()}
    assert triangles == frames["source_triangles"] and shadow_triangles == frames["shadow_triangles"]
    assert max_error == frames["roundtrip_position_max_m"]
    assembled = json.loads((assembly / "assembly.json").read_bytes())
    materials = json.loads(read(manifest["materials"]))
    pebble = json.loads(read(manifest["pebble_field"]))

    def canonical(value):
        if isinstance(value, dict):
            result = {key: canonical(child) for key, child in value.items()}
            if "file" in result:
                result["file"] = result["sha256"] + ".bin"
            return result
        return [canonical(child) for child in value] if isinstance(value, list) else value

    assert materials["source_contracts"] == canonical(assembled["materials"])
    assert pebble == canonical(assembled["pebble"])
    visit(materials)
    visit(pebble)
    assert b"MIT License" in read(manifest["license"])
    assert read(manifest["credits"]) == (acquisition / "CREDITS-scanned.md").read_bytes()
    assert read(manifest["unattached_source_colliders"]) == (acquisition / "colliders.json").read_bytes()
    assert references == set(manifest["files"]), "Reference closure incomplete"
    result = {"schema": "forge.tidewater.debris-frame-verification", "version": 1, "status": "PASS",
              "scope": "portable intermediate CPU geometry", "props": len(frames["props"]),
              "parts": sum(len(obj["parts"]) for obj in frames["props"]), "triangles": triangles,
              "shadow_triangles": shadow_triangles, "position_roundtrip_max_m": max_error,
              "reference_closure": True, "identical_files": len(files), "rendered": False,
              "runtime_qualified": False, "visual_parity": False, "package_sha256": files["package.json"]}
    receipt.write_text(json.dumps(result, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(result))


if __name__ == "__main__":
    main()
