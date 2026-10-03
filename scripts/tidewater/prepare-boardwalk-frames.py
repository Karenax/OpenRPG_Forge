"""Derive movable boardwalk frames while keeping exact source reference buffers."""
from pathlib import Path
import hashlib
import json
import math
import sys

import numpy as np


def main():
    root, output = map(Path, sys.argv[1:])
    assert not output.exists()
    source_bytes = (root / "boardwalks.json").read_bytes()
    source = json.loads(source_bytes)
    assert source["schema"] == "forge.tidewater.boardwalk-materials" and source["version"] == 1
    assert len(source["boardwalks"]) == 3
    output.mkdir()

    def read(descriptor):
        path = (root / descriptor["file"]).resolve()
        assert path.is_relative_to(root.resolve())
        raw = path.read_bytes()
        assert len(raw) == descriptor["bytes"]
        assert hashlib.sha256(raw).hexdigest() == descriptor["sha256"]
        return raw

    def save(raw):
        digest = hashlib.sha256(raw).hexdigest()
        name = digest + ".bin"
        if not (output / name).exists():
            (output / name).write_bytes(raw)
        return {"file": name, "bytes": len(raw), "sha256": digest}

    boardwalks = []
    max_error = 0.0
    for record in source["boardwalks"]:
        anchor = np.array([record["points"][0][0], 0, record["points"][0][1]], dtype=np.float64)
        assert np.isfinite(anchor).all()
        pieces = []
        bounds = []
        for part in record["parts"]:
            attrs = {key: read(value) for key, value in part["attributes"].items()}
            positions = np.frombuffer(attrs["pos"], "<f4").reshape(-1, 3)
            normals = np.frombuffer(attrs["nrm"], "<f4").reshape(-1, 3)
            assert len(positions) == part["vertices"] and np.isfinite(positions).all()
            assert np.isfinite(normals).all()
            local = (positions.astype(np.float64) - anchor).astype("<f4")
            restored = local.astype(np.float64) + anchor
            error = float(np.max(np.abs(restored - positions)))
            assert error < 0.0001, "Local conversion exceeds 0.1 mm"
            max_error = max(max_error, error)
            local_attrs = {key: save(raw) for key, raw in attrs.items()}
            local_attrs["pos"] = save(local.tobytes())
            indices = read(part["indices"])
            assert int(np.frombuffer(indices, "<u4").max()) < len(local)
            pieces.append({key: part[key] for key in ("source_material", "material", "target", "vertices")}
                          | {"attributes": local_attrs, "indices": save(indices),
                             "reference_position": save(attrs["pos"]),
                             "reference_normal": save(attrs["nrm"]),
                             "roundtrip_position_max_m": error})
            bounds.append(local)
        collider_boxes = []
        for box in record["colliders"]["boxes"]:
            center = (np.array(box["center"], dtype=np.float64) - anchor).tolist()
            collider_boxes.append(box | {"center": center})
        collider_cylinders = [c | {"x": c["x"] - anchor[0], "z": c["z"] - anchor[2]}
                              for c in record["colliders"]["cylinders"]]
        centerline = [[x - anchor[0], y, z - anchor[2]]
                      for x, y, z in record["layout"]["centerline"]]
        local_points = [[x - anchor[0], z - anchor[2]] for x, z in record["points"]]
        lights = [light | {"position": (np.array(light["position"], dtype=np.float64) - anchor).tolist()}
                  for light in record["lights"]]
        points = np.concatenate(bounds)
        assert np.isfinite(points).all() and all(math.isfinite(v) for p in centerline for v in p)
        boardwalks.append({
            "source_id": record["source_id"], "kind": "boardwalk",
            "role": record["role"], "source_frame": {
                "translation_m": anchor.tolist(), "rotation_xyzw": [0, 0, 0, 1], "scale": [1, 1, 1]},
            "pivot_basis": "first source control point; source-space y preserved",
            "source_control_points_xz": record["points"],
            "local_control_points_xz": local_points,
            "source_options": record["options"],
            "local_centerline": centerline,
            "length_m": record["layout"]["length"], "width_m": record["layout"]["width"],
            "colliders": {"boxes": collider_boxes, "cylinders": collider_cylinders},
            "lights": lights, "local_bounds": {"min": points.min(axis=0).tolist(),
                                                "max": points.max(axis=0).tolist()},
            "parts": pieces,
        })
    result = {"schema": "forge.tidewater.boardwalk-frames", "version": 1,
              "source_commit": source["source_commit"],
              "source_descriptor_sha256": hashlib.sha256(source_bytes).hexdigest(),
              "boardwalks": boardwalks, "roundtrip_position_max_m": max_error,
              "limits": ["rigid movable segments; spline editing not yet exposed in Studio",
                         "source colliders and path lights retained as metadata, not yet staged"]}
    (output / "boardwalks.json").write_text(json.dumps(result, indent=2), encoding="utf-8")
    print(json.dumps({"boardwalks": len(boardwalks),
                      "vertices": sum(part["vertices"] for record in boardwalks for part in record["parts"]),
                      "roundtrip_position_max_m": max_error}))


if __name__ == "__main__":
    main()
