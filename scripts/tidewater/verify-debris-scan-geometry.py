"""Verify acquired scan geometry, atlas coordinates and independent instance matrices."""
import hashlib
import json
import math
from pathlib import Path
import sys

import numpy as np


def sha(data):
    return hashlib.sha256(data).hexdigest()


def verify(root, source_root):
    source_bytes = (source_root / "scanned-source.json").read_bytes()
    source = json.loads(source_bytes)
    description = json.loads((root / "scanned-geometry.json").read_bytes())
    assert description["schema"] == "forge.tidewater.debris-scanned-geometry" and description["version"] == 1
    assert description["source_commit"] == source["source_commit"]
    assert description["source_descriptor_sha256"] == sha(source_bytes)
    references = set()

    def read(reference, dtype=None):
        name = reference["file"]
        assert Path(name).name == name
        entry = description["files"][name]
        assert entry == {key: reference[key] for key in ["bytes", "sha256"]}
        data = (root / name).read_bytes()
        assert len(data) == entry["bytes"] and sha(data) == entry["sha256"]
        references.add(name)
        return np.frombuffer(data, dtype) if dtype else data

    assert len(description["meshes"]) == 12
    seen, totals = set(), [0, 0, 0]
    for mesh in description["meshes"]:
        asset, lod = mesh["asset"], mesh["lod"]
        assert (asset, lod) not in seen and 0 <= asset < 4 and 0 <= lod < 3
        seen.add((asset, lod))
        assert mesh["source_name"] == source["assets"][asset]["source_name"]
        assert mesh["node"].startswith(f"LOD{lod}") and mesh["node_transforms_applied_by_source_parser"]
        positions = read(mesh["attributes"]["position"], "<f4").reshape(-1, 3)
        normals = read(mesh["attributes"]["normal"], "<f4").reshape(-1, 3)
        uv = read(mesh["attributes"]["uv"], "<f4").reshape(-1, 2)
        assert len(positions) == len(normals) == len(uv) > 0
        assert all(np.isfinite(values).all() for values in [positions, normals, uv])
        indices = read(mesh["indices"], "<u4").reshape(-1, 3)
        assert len(indices) == mesh["triangles"] and indices.max() < len(positions)
        totals[lod] += len(indices)
        atlas = read(mesh["atlas_uv"], "<f4").reshape(-1, 2)
        expected = np.clip(uv.astype(np.float64), .002, .998) * .5
        expected += np.array([(asset % 2) * .5, (asset // 2) * .5])
        assert np.array_equal(atlas, expected.astype("<f4"))
    assert len(description["instances"]) == len(source["instances"]) == 141
    max_matrix_error = 0.0
    for ordinal, (instance, raw) in enumerate(zip(description["instances"], source["instances"])):
        assert instance["source"] == raw
        assert instance["source_id"] == f"tidewater:{source['source_commit']}:debris-scanned:{ordinal}"
        # Independent matrix multiplication, rather than copying the quaternion formula.
        x, y, z = raw.get("roll", 0), raw["yaw"], raw.get("pitch", 0)
        cx, sx, cy, sy, cz, sz = math.cos(x), math.sin(x), math.cos(y), math.sin(y), math.cos(z), math.sin(z)
        rx = np.array([[1, 0, 0], [0, cx, -sx], [0, sx, cx]])
        ry = np.array([[cy, 0, sy], [0, 1, 0], [-sy, 0, cy]])
        rz = np.array([[cz, -sz, 0], [sz, cz, 0], [0, 0, 1]])
        matrix = np.eye(4)
        matrix[:3, :3] = ry @ rx @ rz @ np.diag([raw["sx"], raw["sy"], raw["sz"]])
        matrix[:3, 3] = [raw["x"], raw["y"], raw["z"]]
        acquired = np.array(instance["matrix_column_major"]).reshape(4, 4, order="F")
        assert np.isfinite(acquired).all()
        error = float(np.max(np.abs(acquired - matrix)))
        assert error < 1e-12
        max_matrix_error = max(error, max_matrix_error)
        quaternion = np.array(instance["quaternion_xyzw"])
        assert abs(float(np.dot(quaternion, quaternion)) - 1) < 1e-12
        size = np.array(source["assets"][raw["asset"]]["source_declared_size_m"])
        radius = float(np.linalg.norm(size * [raw["sx"], raw["sy"], raw["sz"]]) / 2)
        assert abs(instance["radius_m"] - radius) < 1e-12
    for name in ["LICENSE-Tidewater.txt", "CREDITS-scanned.md"]:
        read({"file": name, **description["files"][name]})
    assert references == set(description["files"])
    return {"meshes": 12, "instances": 141, "lod_triangles": totals,
            "matrix_max_error": max_matrix_error, "reference_closure": True}


def main():
    first, repeat, source, receipt = map(Path, sys.argv[1:])
    measured = verify(first, source)
    assert measured == verify(repeat, source)
    files = {p.name: sha(p.read_bytes()) for p in first.iterdir() if p.is_file()}
    assert files == {p.name: sha(p.read_bytes()) for p in repeat.iterdir() if p.is_file()}
    assert not receipt.exists()
    result = {"schema": "forge.tidewater.debris-scan-geometry-verification", "version": 1,
              "status": "PASS", "scope": "source CPU geometry and independent matrix/atlas checks",
              **measured, "files": files, "rendered": False, "visual_parity": False}
    receipt.write_text(json.dumps(result, indent=2) + "\n", encoding="utf-8")
    print(json.dumps({key: value for key, value in result.items() if key != "files"}))


if __name__ == "__main__":
    main()
