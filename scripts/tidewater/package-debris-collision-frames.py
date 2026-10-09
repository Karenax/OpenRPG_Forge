"""Preserve source collider ownership in portable procedural/scanned frames.

Usage: ACQUISITION DEBRIS_PACKAGE SCAN_GEOMETRY OWNERSHIP_RECEIPT NEW_OUTPUT
Scanned log boxes become exact local convex geometry: inverse nonuniform TRS
does not generally preserve a primitive box plus rigid local pose.
"""
import hashlib
import itertools
import json
from pathlib import Path
import sys

import numpy as np


def sha(raw):
    return hashlib.sha256(raw).hexdigest()


def main():
    acquisition, package_root, scans_root, receipt_path, output = map(Path, sys.argv[1:])
    assert not output.exists() and all(not output.resolve().is_relative_to(root.resolve())
                                     for root in [acquisition, package_root, scans_root])
    owner_bytes = (acquisition / "collision-ownership.json").read_bytes()
    collider_bytes = (acquisition / "colliders.json").read_bytes()
    owners, colliders = json.loads(owner_bytes), json.loads(collider_bytes)
    receipt_bytes = receipt_path.read_bytes()
    receipt = json.loads(receipt_bytes)
    assert receipt["status"] == "passed" and receipt["source_payloads_unchanged"] and receipt["repeat_identical"]
    assert receipt["collision_ownership_sha256"] == sha(owner_bytes) and receipt["colliders_sha256"] == sha(collider_bytes)
    package_bytes = (package_root / "package.json").read_bytes()
    package = json.loads(package_bytes)
    assert sha(package_bytes) == "0a84592f5ccaa0b8077849b6cf054935f6ecb33f41aaf04a19cec3b42255189a"

    def read(reference):
        name = reference["file"]
        assert Path(name).name == name
        assert package["files"][name] == {key: reference[key] for key in ["bytes", "sha256"]}
        raw = (package_root / name).read_bytes()
        assert len(raw) == reference["bytes"] and sha(raw) == reference["sha256"]
        return raw

    frames = json.loads(read(package["frames"]))
    scans_bytes = (scans_root / "scanned-geometry.json").read_bytes()
    scans = json.loads(scans_bytes)
    assert owners["source_commit"] == frames["source_commit"] == scans["source_commit"]
    assert scans["schema"] == "forge.tidewater.debris-scanned-geometry"
    procedural = {prop["source_id"]: prop["source_frame"] for prop in frames["props"]}
    scanned = {instance["source_id"]: instance for instance in scans["instances"]}
    assert len(procedural) == 805 and len(scanned) == 141
    assert {owner["source_id"] for owner in owners["owners"]} == set(procedural) | set(scanned)
    signs = np.array(list(itertools.product([-1.0, 1.0], repeat=3)))
    indices = [0,1,3,0,3,2,4,6,7,4,7,5,0,4,5,0,5,1,
               2,3,7,2,7,6,0,2,6,0,6,4,1,5,7,1,7,3]
    rows, max_error, counts = [], 0.0, {"box": 0, "cylinder": 0, "convex_box": 0}
    for owner in owners["owners"]:
        identity = owner["source_id"]
        if owner["category"] == "procedural":
            frame = procedural[identity]
            assert frame["rotation_xyzw"] == [0,0,0,1] and frame["scale"] == [1,1,1]
            anchor = np.array(frame["translation_m"])
            matrix = np.eye(4); matrix[:3,3] = anchor
        else:
            instance = scanned[identity]
            source = instance["source"]
            frame = {"translation_m": [source[key] for key in ["x","y","z"]],
                     "rotation_xyzw": instance["quaternion_xyzw"],
                     "scale": [source[key] for key in ["sx","sy","sz"]]}
            matrix = np.array(instance["matrix_column_major"]).reshape(4,4,order="F")
            assert np.isfinite(matrix).all() and np.linalg.det(matrix[:3,:3]) > 0
        primitives = []
        for event in owner["collisions"]:
            shape, index = event["shape"], event["index"]
            value = colliders["boxes" if shape == "box" else "cylinders"][index]
            if shape == "box":
                center = np.array([value["center"][axis] for axis in ["x","y","z"]])
                half = np.array([value["half"][axis] for axis in ["x","y","z"]])
                c, s = np.cos(value["rotY"]), np.sin(value["rotY"])
                rotation = np.array([[c,0,s],[0,1,0],[-s,0,c]])
                world = (signs * half) @ rotation.T + center
                if owner["category"] == "procedural":
                    local = {"shape": "box", "translation_m": (center-anchor).tolist(),
                             "half_m": half.tolist(), "rotation_y_rad": value["rotY"],
                             "solid": value["solid"], "walkable": value["walkable"]}
                    rebuilt = (signs * half.astype("<f4")) @ rotation.T + np.array(local["translation_m"], dtype="<f4") + anchor
                else:
                    points = np.c_[world, np.ones(8)] @ np.linalg.inv(matrix).T
                    vertices = points[:,:3].astype("<f4")
                    local = {"shape": "convex_box", "vertices": vertices.tolist(), "indices": indices,
                             "solid": value["solid"], "walkable": value["walkable"]}
                    rebuilt = (np.c_[vertices, np.ones(8)] @ matrix.T)[:,:3]
                error = float(np.linalg.norm(rebuilt-world, axis=1).max())
            else:
                assert owner["category"] == "procedural"
                center = np.array([value["x"], (value["yMin"]+value["yMax"])/2, value["z"]])
                local = {"shape": "cylinder", "translation_m": (center-anchor).tolist(),
                         "radius_m": value["radius"], "half_height_m": (value["yMax"]-value["yMin"])/2}
                center_error = float(np.linalg.norm(np.array(local["translation_m"], dtype="<f4") + anchor - center))
                error = center_error + abs(float(np.float32(local["radius_m"]))-local["radius_m"])
                error += abs(float(np.float32(local["half_height_m"]))-local["half_height_m"])
            assert error < 0.0001
            max_error = max(max_error, error)
            counts[local["shape"]] += 1
            primitives.append({"source_shape": shape, "source_index": index, "tag": value["tag"], "local": local})
        rows.append({"source_id": identity, "category": owner["category"], "source_frame": frame,
                     "source_matrix_column_major": matrix.reshape(16,order="F").tolist(), "colliders": primitives})
    assert sum(counts.values()) == 111
    document = {"schema": "forge.tidewater.debris-collision-frames", "version": 1,
                "source_commit": owners["source_commit"], "debris_package_sha256": sha(package_bytes),
                "source_ownership_sha256": sha(owner_bytes), "source_colliders_sha256": sha(collider_bytes),
                "scanned_geometry_sha256": sha(scans_bytes), "ownership_receipt_sha256": sha(receipt_bytes),
                "preparation_sha256": sha(Path(__file__).read_bytes()), "owners": rows, "counts": counts,
                "max_float32_local_reconstruction_error_m": max_error,
                "limits": ["Intermediate collision frames only; not GLB/Vault qualification or Rapier registration",
                           "Reconstruction uses source matrices in f64 and f32 local geometry; actual Bevy/Rapier TRS remains unqualified",
                           "Source capsule/step behaviour and authored nonuniform scales require runtime comparison"]}
    output.mkdir()
    (output / "collision-frames.json").write_bytes(json.dumps(document, indent=2).encode())
    (output / "source-colliders.json").write_bytes(collider_bytes)
    (output / "source-ownership.json").write_bytes(owner_bytes)
    (output / "LICENSE-Tidewater.txt").write_bytes(read(package["license"]))
    (output / "CREDITS-scanned.md").write_bytes(read(package["credits"]))
    print(json.dumps({"output": str(output), "owners": len(rows), "counts": counts,
                      "max_float32_local_reconstruction_error_m": max_error, "status": "prepared_not_registered"}))


if __name__ == "__main__":
    main()
