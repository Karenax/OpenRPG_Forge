"""Verify repeatable boardwalk extraction against exact source index ranges."""
from pathlib import Path
import hashlib
import json
import math
import sys

import numpy as np


first, second, source, buildings_root, report_path = map(Path, sys.argv[1:])
assert not report_path.exists()
files = sorted(path.name for path in first.iterdir())
assert files == sorted(path.name for path in second.iterdir())
assert all((first / name).read_bytes() == (second / name).read_bytes() for name in files)
boardwalks = json.loads((first / "boardwalks.json").read_bytes())
buildings = json.loads((buildings_root / "buildings.json").read_bytes())
render = json.loads((source / "village/render.json").read_bytes())
assert boardwalks["schema"] == "forge.tidewater.boardwalk-parts" and boardwalks["version"] == 1
assert boardwalks["source_commit"] == buildings["source_commit"]
assert boardwalks["unchanged_merged_scene"] and boardwalks["unchanged_terrain_and_colliders"]
assert len(boardwalks["boardwalks"]) == 3 and buildings["version"] == 3


def read(root, descriptor, dtype, key="file"):
    name = descriptor[key]
    assert Path(name).name == name or key == "path"
    path = (root / name).resolve()
    assert path.is_relative_to(root.resolve())
    raw = path.read_bytes()
    assert len(raw) == descriptor["bytes"] and hashlib.sha256(raw).hexdigest() == descriptor["sha256"]
    return np.frombuffer(raw, dtype)


groups = {
    "wood": ["wood", "glass"],
    "hard": ["hard", "rope"],
    "roofMetal": ["roofMetal"],
    "thatch": ["thatch"],
    "stone": ["stone"],
    "fabric": ["fabric", "cloth", "flag"],
    "nets": ["net"],
}
offsets = {}
base = 0
for target, keys in groups.items():
    vertex_position = index_position = 0
    for key in keys:
        offsets[key] = (target, (base if target not in ("fabric", "nets") else 0) + vertex_position, index_position)
        batch = buildings["assembly_batches"].get(key, {"vertices": 0, "indices": 0})
        vertex_position += batch["vertices"]
        index_position += batch["indices"]
    if target not in ("fabric", "nets"):
        base += vertex_position

source_indices = {}
for target in groups:
    node = next(node for node in render["nodes"] if node["name"] == f"village_{target}")
    geometry = render["geometries"][node["geometry"]]
    source_indices[target] = read(source, geometry["indices"], "<u4", "path")

ranges = {target: [] for target in groups}
for owner in buildings["buildings"]:
    for key, part in owner["parts"].items():
        target, _, index_base = offsets[key]
        start = index_base + part["start"]["indices"]
        end = start + part["indices"]["bytes"] // 4
        ranges[target].append((start, end, owner["source_id"]))

inventory = []
for ordinal, record in enumerate(boardwalks["boardwalks"]):
    assert record["source_id"].endswith(f":boardwalk:{ordinal}")
    assert record["role"] == ("pier-to-plaza" if ordinal == 0 else "side-path")
    assert 2 <= len(record["points"]) <= 8 and len(record["layout"]["centerline"]) >= 2
    assert math.isfinite(record["layout"]["length"]) and record["layout"]["length"] > 0
    assert math.isfinite(record["layout"]["width"]) and record["layout"]["width"] > 0
    for row in record["layout"]["centerline"]:
        assert len(row) == 3 and all(math.isfinite(value) for value in row)
    assert record["colliders"]["boxes"]
    for box in record["colliders"]["boxes"]:
        assert box["tag"] == "boardwalk" and box["walkable"] and box["solid"]
        assert all(math.isfinite(value) for value in (*box["center"], *box["half"], box["yaw"]))
        assert all(value > 0 for value in box["half"])
    vertices = triangles = 0
    for key, part in record["parts"].items():
        target, vertex_base, index_base = offsets[key]
        local = read(first, part["indices"], "<u4")
        assert len(local) > 0 and len(local) % 3 == 0
        assert int(local.max()) < part["vertices"]
        for name, width in (("pos", 3), ("nrm", 3), ("uv", 2), ("tint", 3), ("data", 4)):
            values = read(first, part["attributes"][name], "<f4")
            assert len(values) == part["vertices"] * width and np.isfinite(values).all()
        original_data = read(first, part["source_vdata"], "<f8")
        assert len(original_data) == part["vertices"] * 4 and np.isfinite(original_data).all()
        assert np.array_equal(original_data.astype("<f4"), read(first, part["attributes"]["data"], "<f4"))
        start = index_base + part["start"]["indices"]
        end = start + len(local)
        assert start % 3 == end % 3 == 0 and end <= len(source_indices[target])
        expected = local.astype("<u8") + vertex_base + part["start"]["vertices"]
        assert np.array_equal(source_indices[target][start:end], expected), (ordinal, key)
        for other_start, other_end, owner in ranges[target]:
            assert end <= other_start or start >= other_end, (record["source_id"], owner)
        ranges[target].append((start, end, record["source_id"]))
        vertices += part["vertices"]
        triangles += len(local) // 3
    inventory.append({"source_id": record["source_id"], "role": record["role"],
                      "control_points": len(record["points"]),
                      "centerline_samples": len(record["layout"]["centerline"]),
                      "vertices": vertices, "triangles": triangles,
                      "collider_boxes": len(record["colliders"]["boxes"]),
                      "collider_cylinders": len(record["colliders"]["cylinders"]),
                      "lights": len(record["lights"])})

report = {"schema": "forge.tidewater.boardwalk-parts-proof", "version": 1,
          "source_commit": boardwalks["source_commit"], "files_identical": len(files),
          "source_scene_digest": boardwalks["source_scene_digest"],
          "source_index_ranges_exact": True, "building_ranges_disjoint": True,
          "scene_and_colliders_unchanged": True, "inventory": inventory,
          "total_triangles": sum(item["triangles"] for item in inventory),
          "total_collider_boxes": sum(item["collider_boxes"] for item in inventory),
          "limits": ["pre-folding material keys", "source-space geometry", "not yet a Forge asset or Studio rendering proof"]}
report_path.write_text(json.dumps(report, indent=2), encoding="utf-8")
print(json.dumps({key: report[key] for key in ("files_identical", "source_index_ranges_exact",
                                           "building_ranges_disjoint", "total_triangles",
                                           "total_collider_boxes")}))
