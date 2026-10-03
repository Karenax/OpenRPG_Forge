"""Fold the three observed boardwalks into verified source village materials."""
from collections import Counter
from pathlib import Path
import hashlib
import json
import sys

import numpy as np


source, parts_root, output = map(Path, sys.argv[1:])
assert not output.exists()
parts = json.loads((parts_root / "boardwalks.json").read_bytes())
assert parts["schema"] == "forge.tidewater.boardwalk-parts" and parts["version"] == 1
render = json.loads((source / "village/render.json").read_bytes())
mapping = {"wood": "wood", "glass": "wood", "hard": "hard", "rope": "hard",
           "roofMetal": "roofMetal", "thatch": "thatch", "stone": "stone",
           "fabric": "fabric", "cloth": "fabric", "flag": "fabric", "net": "nets"}
names = {"pos": "position", "nrm": "normal", "uv": "uv", "tint": "tint", "data": "vdata"}
widths = {"pos": 3, "nrm": 3, "uv": 2, "tint": 3, "data": 4}


def read(root, descriptor, dtype, key="file"):
    name = descriptor[key]
    path = (root / name).resolve()
    assert path.is_relative_to(root.resolve())
    raw = path.read_bytes()
    assert len(raw) == descriptor["bytes"] and hashlib.sha256(raw).hexdigest() == descriptor["sha256"]
    return np.frombuffer(raw, dtype)


available = {}
material_ids = {}
for target in sorted({mapping[key] for boardwalk in parts["boardwalks"] for key in boardwalk["parts"]}):
    node = next(node for node in render["nodes"] if node["name"] == f"village_{target}")
    geometry = render["geometries"][node["geometry"]]
    vertices = np.concatenate([
        read(source, geometry["attributes"][names[name]], "<f4", "path").reshape(-1, width)
        for name, width in widths.items()
    ], axis=1)
    indices = read(source, geometry["indices"], "<u4", "path").reshape(-1, 3)
    available[target] = Counter(triangle.tobytes() for triangle in vertices[indices])
    material_ids[target] = node["material"]

output.mkdir()


def save(array):
    raw = array.tobytes()
    digest = hashlib.sha256(raw).hexdigest()
    name = digest + ".bin"
    if not (output / name).exists():
        (output / name).write_bytes(raw)
    return {"file": name, "bytes": len(raw), "sha256": digest}


records = []
matched = 0
for boardwalk in parts["boardwalks"]:
    folded_parts = []
    for key, part in boardwalk["parts"].items():
        target = mapping[key]
        attrs = {name: read(parts_root, part["attributes"][name], "<f4").reshape(-1, width)
                 for name, width in widths.items()}
        original_data = read(parts_root, part["source_vdata"], "<f8").reshape(-1, 4)
        assert np.isfinite(original_data).all()
        assert np.array_equal(original_data.astype("<f4"), attrs["data"])
        folded_data = original_data.copy()
        if key == "glass":
            folded_data[:, 2] = 9
            folded_data[:, 3] = original_data[:, 2]
        elif key == "rope":
            folded_data[:, 1:3] = 0
            folded_data[:, 3] = 2 + original_data[:, 1]
        elif key == "cloth":
            folded_data[:, 2:] = 0
        elif key == "net":
            folded_data[:, 2] = np.maximum(0.02, original_data[:, 2])
            folded_data[:, 3] = 0
        elif key == "flag":
            folded_data[:, 2] = original_data[:, 2] + 10000
        attrs["data"] = folded_data.astype("<f4")
        indices = read(parts_root, part["indices"], "<u4").reshape(-1, 3)
        assert indices.size and int(indices.max()) < part["vertices"]
        vertices = np.concatenate(list(attrs.values()), axis=1)
        for triangle in vertices[indices]:
            signature = triangle.tobytes()
            assert available[target][signature] > 0, (boardwalk["source_id"], key)
            available[target][signature] -= 1
            matched += 1
        folded_parts.append({"source_material": key, "material": material_ids[target],
                             "target": target, "vertices": part["vertices"],
                             "attributes": {name: save(values) for name, values in attrs.items()},
                             "indices": save(indices)})
    records.append({key: boardwalk[key] for key in ("source_id", "role", "points", "options", "layout", "colliders", "lights", "checks")}
                   | {"parts": folded_parts})

manifest = {"schema": "forge.tidewater.boardwalk-materials", "version": 1,
            "source_commit": parts["source_commit"],
            "boardwalk_parts_sha256": hashlib.sha256((parts_root / "boardwalks.json").read_bytes()).hexdigest(),
            "boardwalks": records, "matched_source_triangles": matched,
            "proof": "Every triangle has identical ordered source position, normal, UV, tint and folded shader data.",
            "limits": ["world-space geometry", "not yet a governed Forge asset or Studio Playtest proof"]}
(output / "boardwalks.json").write_text(json.dumps(manifest, indent=2), encoding="utf-8")
print(json.dumps({"boardwalks": len(records), "matched_source_triangles": matched,
                  "materials": sorted({part["material"] for record in records for part in record["parts"]})}))
