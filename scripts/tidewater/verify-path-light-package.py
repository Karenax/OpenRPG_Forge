"""Verify a path-light conversion independently against captured source bytes.

Usage: RAW_PARTS PACKAGE EXPECTED_PACKAGE_SHA256 NEW_REPORT
This proves payload/pose fidelity, not a Studio or GPU execution.
"""
from pathlib import Path
import hashlib
import json
import sys

import numpy as np


def digest(raw):
    return hashlib.sha256(raw).hexdigest()


def read(root, pin):
    name = pin["file"]
    assert Path(name).name == name, "Payload must be a direct package member"
    raw = (root / name).read_bytes()
    assert len(raw) == pin["bytes"] and digest(raw) == pin["sha256"]
    return raw


def array(root, pin, dtype, width):
    values = np.frombuffer(read(root, pin), dtype).reshape(-1, width)
    assert values.size and np.isfinite(values).all()
    return values


def main():
    raw_root, package, expected, report = sys.argv[1:]
    raw_root, package, report = map(Path, (raw_root, package, report))
    assert not report.exists(), "Fresh report required"
    manifest_raw = (package / "package.json").read_bytes()
    assert digest(manifest_raw) == expected
    manifest = json.loads(manifest_raw)
    assert manifest["schema"] == "forge.tidewater.path-light-package"
    assert manifest["version"] == 1
    assert {p.name for p in package.iterdir()} == set(manifest["files"]) | {"package.json"}
    for name, pin in manifest["files"].items():
        read(package, {"file": name, **pin})
    license_raw = read(package, manifest["license"])
    assert license_raw == (raw_root / "originals/LICENSE").read_bytes()
    assert b"MIT License" in license_raw
    source_raw = (raw_root / "path-lights.json").read_bytes()
    source = json.loads(source_raw)
    converted = json.loads(read(package, manifest["props"]))
    assert converted["source_descriptor_sha256"] == digest(source_raw)
    assert converted["schema"] == "forge.tidewater.path-light-frames"
    assert converted["source_commit"] == manifest["source_commit"] == source["source_commit"]
    assert len(source["props"]) == len(converted["props"]) == 3
    total = 0
    max_error = 0.0
    rows = []
    for original, asset in zip(source["props"], converted["props"], strict=True):
        assert asset["source_id"] == original["source_id"]
        assert asset["kind"] == original["kind"] == "pathLight"
        frame = asset["source_frame"]
        assert frame["rotation_xyzw"] == [0, 0, 0, 1] and frame["scale"] == [1, 1, 1]
        anchor = np.array(frame["translation_m"], dtype=np.float64)
        assert np.array_equal(anchor, original["args"][:3])
        assert len(asset["parts"]) == len(original["parts"]) == 3
        points = []
        for part in asset["parts"]:
            original_part = original["parts"][part["source_material"]]
            assert part["vertices"] == original_part["vertices"]
            before = array(raw_root, original_part["attributes"]["pos"], "<f4", 3)
            after = array(package, part["attributes"]["pos"], "<f4", 3)
            assert len(after) == part["vertices"]
            error = float(np.max(np.abs(after.astype(np.float64) + anchor - before)))
            assert error <= 0.0001 and error == part["roundtrip_position_max_m"]
            max_error = max(max_error, error)
            points.append(after)
            for name in ("nrm", "uv", "tint"):
                assert read(package, part["attributes"][name]) == read(raw_root, original_part["attributes"][name])
            original_data = array(raw_root, original_part["source_vdata"], "<f8", 4).copy()
            if part["source_material"] == "glass":
                original_data[:, 3] = original_data[:, 2]
                original_data[:, 2] = 9
            assert np.array_equal(array(package, part["attributes"]["data"], "<f4", 4), original_data.astype("<f4"))
            assert read(package, part["indices"]) == read(raw_root, original_part["indices"])
            indices = array(package, part["indices"], "<u4", 3)
            assert int(indices.max()) < len(after)
            total += len(indices)
        points = np.concatenate(points)
        assert np.array_equal(points.min(axis=0), asset["local_bounds"]["min"])
        assert np.array_equal(points.max(axis=0), asset["local_bounds"]["max"])
        assert not asset["colliders"]["boxes"] and len(asset["colliders"]["cylinders"]) == 1
        collider = asset["colliders"]["cylinders"][0]
        before = original["colliders"]["cylinders"][0]
        assert collider["radius"] == before["radius"] and collider["tag"] == before["tag"]
        for key, axis in (("x", 0), ("z", 2), ("yMin", 1), ("yMax", 1)):
            assert abs(collider[key] + anchor[axis] - before[key]) <= 1e-12
        assert len(asset["lights"]) == 1
        light, before = asset["lights"][0], original["lights"][0]
        assert np.max(np.abs(np.array(light["position"]) + anchor - before["position"])) <= 1e-12
        assert all(light[key] == before[key] for key in ("color", "intensity", "kind"))
        rows.append({"source_id": asset["source_id"], "position_max_error_m": max_error})
    assert total == converted["matched_source_triangles"] == 312
    assert max_error == converted["roundtrip_position_max_m"]
    result = {"schema": "forge.path-light-package-fidelity-proof.v1", "package_sha256": expected,
              "verifier_sha256": digest(Path(__file__).read_bytes()), "payloads": len(manifest["files"]),
              "triangles": total, "max_position_error_m": max_error, "objects": rows,
              "source_attributes_colliders_lights_preserved": True, "studio_executed": False,
              "gpu_draw_executed": False, "visual_parity": False}
    report.write_text(json.dumps(result, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(result))


if __name__ == "__main__":
    main()
