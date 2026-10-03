"""Fold source pier materials and place all parts in one movable local frame."""
from pathlib import Path
import hashlib
import json
import sys

import numpy as np


source_root, raw_root, output = map(Path, sys.argv[1:])
assert not output.exists()
raw_descriptor = (raw_root / "pier.json").read_bytes()
source = json.loads(raw_descriptor)
assert source["schema"] == "forge.tidewater.pier-parts" and source["version"] == 1
assert source["unchanged_merged_scene"] and source["unchanged_terrain_and_colliders"]
render = json.loads((source_root / "village/render.json").read_bytes())
material_ids = {n["name"]: n["material"] for n in render["nodes"]}
targets = {"wood": "wood", "glass": "wood", "hard": "hard", "rope": "hard", "net": "nets"}
widths = {"pos": 3, "nrm": 3, "uv": 2, "tint": 3, "data": 4}
anchor = np.array([source["pier"]["source_layout"]["x"], 0,
                   source["pier"]["source_layout"]["zStart"]], dtype=np.float64)
assert np.isfinite(anchor).all() and np.array_equal(anchor, [55.0, 0.0, -64.0])
output.mkdir()


def read(descriptor, dtype):
    location = (raw_root / descriptor["file"]).resolve()
    assert location.is_relative_to(raw_root.resolve())
    raw = location.read_bytes()
    assert len(raw) == descriptor["bytes"]
    assert hashlib.sha256(raw).hexdigest() == descriptor["sha256"]
    return np.frombuffer(raw, dtype)


def save(raw):
    digest = hashlib.sha256(raw).hexdigest()
    name = digest + ".bin"
    if not (output / name).exists():
        (output / name).write_bytes(raw)
    return {"file": name, "bytes": len(raw), "sha256": digest}


max_error = 0.0
triangles = 0


def pieces(parts, source_role):
    global max_error, triangles
    result = []
    for key, part in parts.items():
        attrs = {name: read(part["attributes"][name], "<f4").reshape(-1, width)
                 for name, width in widths.items()}
        count = part["vertices"]
        assert count > 0 and all(len(values) == count and np.isfinite(values).all()
                                 for values in attrs.values())
        original_data = read(part["source_vdata"], "<f8").reshape(-1, 4)
        assert np.array_equal(original_data.astype("<f4"), attrs["data"])
        folded = original_data.copy()
        if key == "glass":
            folded[:, 2] = 9
            folded[:, 3] = original_data[:, 2]
        elif key == "rope":
            folded[:, 1:3] = 0
            folded[:, 3] = 2 + original_data[:, 1]
        elif key == "net":
            folded[:, 2] = np.maximum(0.02, original_data[:, 2])
            folded[:, 3] = 0
        attrs["data"] = folded.astype("<f4")
        world = attrs["pos"]
        local = (world.astype(np.float64) - anchor).astype("<f4")
        error = float(np.max(np.abs(local.astype(np.float64) + anchor - world)))
        assert error < 0.0001
        max_error = max(max_error, error)
        attrs["pos"] = local
        indices = read(part["indices"], "<u4")
        assert len(indices) % 3 == 0 and int(indices.max()) < count
        triangles += len(indices) // 3
        target = targets[key]
        material = material_ids[f"village_{target}"]
        result.append({"source_role": source_role, "source_material": key,
                       "material": material, "vertices": count,
                       "attributes": {name: save(values.tobytes()) for name, values in attrs.items()},
                       "indices": save(indices.tobytes()),
                       "reference_position": save(world.tobytes()),
                       "reference_normal": save(attrs["nrm"].tobytes()),
                       "roundtrip_position_max_m": error})
    return result


def local_point(point):
    values = np.asarray(point, dtype=np.float64) - anchor
    assert values.shape == (3,) and np.isfinite(values).all()
    return values.tolist()


pier = source["pier"]
boxes = [box | {"center": local_point(box["center"])} for box in pier["colliders"]["boxes"]]
cylinders = [c | {"x": c["x"] - anchor[0], "z": c["z"] - anchor[2]}
             for c in pier["colliders"]["cylinders"]]
record = {
    "source_id": pier["source_id"], "kind": "pier",
    "source_frame": {"translation_m": anchor.tolist(), "rotation_xyzw": [0, 0, 0, 1],
                     "scale": [1, 1, 1]},
    "source_layout": pier["source_layout"],
    "static_parts": pieces(pier["static"], "static"),
    "sign": {"pivot": local_point(pier["sign_pivot"]),
             "parts": pieces(pier["sign"], "swinging-sign")},
    "lanterns": [{"pivot": local_point(item["pivot"]),
                  "rest": local_point(item["rest"]),
                  "parts": pieces(item["parts"], "hanging-lantern")}
                 for item in pier["lanterns"]],
    "colliders": {"boxes": boxes, "cylinders": cylinders},
    "lights": [light | {"position": local_point(light["position"])}
               for light in pier["lights"]],
    "instanced_props": pier["instanced_props"],
    "step_foot": local_point(pier["step_foot"]),
}
result = {"schema": "forge.tidewater.pier-frames", "version": 1,
          "source_commit": source["source_commit"],
          "source_descriptor_sha256": hashlib.sha256(raw_descriptor).hexdigest(),
          "pier": record, "triangles": triangles,
          "roundtrip_position_max_m": max_error,
          "limits": ["rigid editable pier frame; swinging sign and lanterns retained as separate parts",
                     "wind animation, Studio staging and playable collisions pending"]}
(output / "pier.json").write_text(json.dumps(result, indent=2), encoding="utf-8")
print(json.dumps({"parts": len(record["static_parts"]) + len(record["sign"]["parts"])
                           + sum(len(item["parts"]) for item in record["lanterns"]),
                  "triangles": triangles, "roundtrip_position_max_m": max_error}))
