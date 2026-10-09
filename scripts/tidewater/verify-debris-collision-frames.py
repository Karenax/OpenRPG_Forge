"""Reopen collision frames and independently reconstruct source primitives.

Usage: FIRST REPEAT DEBRIS_PACKAGE SCAN_GEOMETRY NEW_RECEIPT
"""
from collections import Counter
import hashlib
import itertools
import json
from pathlib import Path
import sys

import numpy as np


def sha(raw):
    return hashlib.sha256(raw).hexdigest()


def main():
    first, repeat, package_root, scans_root, receipt = map(Path, sys.argv[1:])
    assert not receipt.exists()
    files = lambda root: {path.name: sha(path.read_bytes()) for path in root.iterdir() if path.is_file()}
    assert files(first) == files(repeat)
    raw = (first / "collision-frames.json").read_bytes()
    data = json.loads(raw)
    colliders_raw = (first / "source-colliders.json").read_bytes()
    ownership_raw = (first / "source-ownership.json").read_bytes()
    colliders, ownership = json.loads(colliders_raw), json.loads(ownership_raw)
    assert data["schema"] == "forge.tidewater.debris-collision-frames" and data["version"] == 1
    assert data["source_colliders_sha256"] == sha(colliders_raw) and data["source_ownership_sha256"] == sha(ownership_raw)
    package_raw = (package_root / "package.json").read_bytes()
    package = json.loads(package_raw)
    assert data["debris_package_sha256"] == sha(package_raw) == "0a84592f5ccaa0b8077849b6cf054935f6ecb33f41aaf04a19cec3b42255189a"
    scans_raw = (scans_root / "scanned-geometry.json").read_bytes()
    scans = json.loads(scans_raw)
    assert data["scanned_geometry_sha256"] == sha(scans_raw)
    frames = json.loads((package_root / package["frames"]["file"]).read_bytes())
    source_frames = {row["source_id"]: row["source_frame"] for row in frames["props"]}
    for row in scans["instances"]:
        source = row["source"]
        source_frames[row["source_id"]] = {"translation_m": [source[key] for key in ["x","y","z"]],
            "rotation_xyzw": row["quaternion_xyzw"], "scale": [source[key] for key in ["sx","sy","sz"]]}
    expected_owners = {row["source_id"]: row for row in ownership["owners"]}
    assert len(data["owners"]) == len(source_frames) == len(expected_owners) == 946
    corners = np.array(list(itertools.product([-1.0,1.0], repeat=3)))
    seen, primitive_ids, kinds, max_error = set(), set(), Counter(), 0.0
    for row in data["owners"]:
        identity = row["source_id"]
        assert identity not in seen and row["source_frame"] == source_frames[identity]
        seen.add(identity)
        source_owner = expected_owners[identity]
        assert row["category"] == source_owner["category"]
        assert len(row["colliders"]) == len(source_owner["collisions"])
        frame = row["source_frame"]
        x,y,z,w = frame["rotation_xyzw"]
        rotation = np.array([[1-2*(y*y+z*z),2*(x*y-z*w),2*(x*z+y*w)],
                             [2*(x*y+z*w),1-2*(x*x+z*z),2*(y*z-x*w)],
                             [2*(x*z-y*w),2*(y*z+x*w),1-2*(x*x+y*y)]])
        matrix = np.eye(4); matrix[:3,:3] = rotation @ np.diag(frame["scale"]); matrix[:3,3] = frame["translation_m"]
        assert np.max(np.abs(matrix - np.array(row["source_matrix_column_major"]).reshape(4,4,order="F"))) < 1e-12
        for collider, event in zip(row["colliders"], source_owner["collisions"]):
            shape, index = collider["source_shape"], collider["source_index"]
            assert [shape,index] == [event["shape"],event["index"]]
            assert (shape,index) not in primitive_ids
            primitive_ids.add((shape,index))
            source = colliders["boxes" if shape == "box" else "cylinders"][index]
            local = collider["local"]; kinds[local["shape"]] += 1
            assert collider["tag"] == source["tag"]
            if shape == "box":
                center = np.array([source["center"][axis] for axis in ["x","y","z"]])
                half = np.array([source["half"][axis] for axis in ["x","y","z"]])
                yaw = source["rotY"]; c,s = np.cos(yaw),np.sin(yaw)
                box_rotation = np.array([[c,0,s],[0,1,0],[-s,0,c]])
                world = corners * half @ box_rotation.T + center
                assert local["solid"] == source["solid"] and local["walkable"] == source["walkable"]
                if row["category"] == "procedural":
                    assert local["shape"] == "box" and local["half_m"] == half.tolist() and local["rotation_y_rad"] == yaw
                    vertices = corners * np.array(local["half_m"], dtype="<f4") @ box_rotation.T + np.array(local["translation_m"], dtype="<f4")
                else:
                    assert local["shape"] == "convex_box"
                    vertices = np.array(local["vertices"])
                    assert vertices.shape == (8,3) and np.isfinite(vertices).all()
                    assert np.array_equal(vertices,vertices.astype("<f4"))
                    indices = np.array(local["indices"]).reshape(-1,3)
                    assert indices.shape == (12,3) and set(indices.flatten()) == set(range(8))
                    edges = Counter(tuple(sorted((int(a),int(b)))) for triangle in indices
                                    for a,b in zip(triangle,np.roll(triangle,-1)))
                    assert all(count == 2 for count in edges.values())
                    volume = sum(np.dot(vertices[a],np.cross(vertices[b],vertices[c])) for a,b,c in indices)/6
                    assert volume > 0
                rebuilt = (np.c_[vertices,np.ones(8)] @ matrix.T)[:,:3]
                error = float(np.linalg.norm(rebuilt-world,axis=1).max())
            else:
                assert row["category"] == "procedural" and local["shape"] == "cylinder"
                assert local["radius_m"] == source["radius"]
                assert local["half_height_m"] == (source["yMax"]-source["yMin"])/2
                center = np.array([source["x"],(source["yMin"]+source["yMax"])/2,source["z"]])
                rebuilt = matrix[:3,3] + np.array(local["translation_m"],dtype="<f4")
                error = float(np.linalg.norm(rebuilt-center)) + abs(float(np.float32(local["radius_m"]))-source["radius"])
                error += abs(float(np.float32(local["half_height_m"]))-local["half_height_m"])
            assert error < 0.0001
            max_error = max(max_error,error)
    assert seen == set(source_frames)
    assert primitive_ids == ({("box",i) for i in range(len(colliders["boxes"]))}
                            | {("cylinder",i) for i in range(len(colliders["cylinders"]))})
    assert dict(kinds) == data["counts"] == {"box":26,"cylinder":25,"convex_box":60}
    assert abs(max_error-data["max_float32_local_reconstruction_error_m"]) < 1e-12
    for key,name in [("license","LICENSE-Tidewater.txt"),("credits","CREDITS-scanned.md")]:
        assert sha((first/name).read_bytes()) == package[key]["sha256"]
    result = {"status":"passed","owners":len(seen),"counts":dict(kinds),"repeat_identical":True,
              "collision_frames_sha256":sha(raw),"max_float32_local_reconstruction_error_m":max_error,
              "limits":["Local collider reconstruction only; no Bevy/Rapier upload, contacts, controller or Studio qualification"]}
    receipt.write_text(json.dumps(result,indent=2),encoding="utf-8"); print(json.dumps(result))


if __name__ == "__main__":
    main()
