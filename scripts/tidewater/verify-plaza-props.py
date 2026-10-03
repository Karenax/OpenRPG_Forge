"""Verify repeatable plaza extraction against the pinned merged village mesh."""
from pathlib import Path
import hashlib
import json
import math
import sys

import numpy as np


first, second, source, buildings_root, boardwalk_root, pier_root, report_path = map(Path, sys.argv[1:])
assert not report_path.exists()
files = sorted(path.name for path in first.iterdir())
assert files == sorted(path.name for path in second.iterdir())
assert all((first / name).read_bytes() == (second / name).read_bytes() for name in files)
plaza = json.loads((first / "plaza-props.json").read_bytes())
buildings = json.loads((buildings_root / "buildings.json").read_bytes())
boardwalk = json.loads((boardwalk_root / "boardwalks.json").read_bytes())
pier = json.loads((pier_root / "pier.json").read_bytes())
render = json.loads((source / "village/render.json").read_bytes())
assert plaza["schema"] == "forge.tidewater.plaza-parts" and plaza["version"] == 1
assert all(p["source_commit"] == plaza["source_commit"] for p in (buildings, boardwalk, pier))
assert plaza["source_scene_digest"] == boardwalk["source_scene_digest"] == pier["source_scene_digest"]
assert plaza["unchanged_merged_scene"] and plaza["unchanged_terrain_and_colliders"]
assert [p["kind"] for p in plaza["props"]] == ["bench", "bench", "lampPost", "barrel", "barrel"]


def read(root, descriptor, dtype, key="file"):
    location = (root / descriptor[key]).resolve()
    assert location.is_relative_to(root.resolve())
    raw = location.read_bytes()
    assert len(raw) == descriptor["bytes"]
    assert hashlib.sha256(raw).hexdigest() == descriptor["sha256"]
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
        offsets[key] = (target, (base if target not in ("fabric", "nets") else 0) + vertex_position,
                        index_position)
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


def add_existing(record, label):
    for key, part in record.items():
        target, _, index_base = offsets[key]
        start = index_base + part["start"]["indices"]
        end = start + part["indices"]["bytes"] // 4
        ranges[target].append((start, end, label))


for item in buildings["buildings"]:
    add_existing(item["parts"], item["source_id"])
for item in boardwalk["boardwalks"]:
    add_existing(item["parts"], item["source_id"])
add_existing(pier["pier"]["static"], pier["pier"]["source_id"])

inventory = []
for ordinal, prop in enumerate(plaza["props"]):
    assert prop["source_id"] == f"tidewater:{plaza['source_commit']}:plaza-prop:{ordinal}"
    x = prop["args"][1] if prop["kind"] == "barrel" else prop["args"][0]
    z = prop["args"][3] if prop["kind"] == "barrel" else prop["args"][2]
    assert all(math.isfinite(value) for value in (x, z))
    colliders = prop["colliders"]
    assert len(colliders["boxes"]) + len(colliders["cylinders"]) == 1
    collider = (colliders["boxes"] + colliders["cylinders"])[0]
    assert collider["tag"] == prop["kind"]
    assert len(prop["lights"]) == (1 if ordinal == 2 else 0)
    if prop["lights"]:
        light = prop["lights"][0]
        assert light["kind"] == "lantern" and light["intensity"] == 5
        assert all(math.isfinite(value) for value in (*light["position"], *light["color"]))
    vertices = triangles = 0
    for key, part in prop["parts"].items():
        target, vertex_base, index_base = offsets[key]
        local = read(first, part["indices"], "<u4")
        assert len(local) > 0 and len(local) % 3 == 0
        assert int(local.max()) < part["vertices"]
        for name, width in (("pos", 3), ("nrm", 3), ("uv", 2), ("tint", 3), ("data", 4)):
            values = read(first, part["attributes"][name], "<f4")
            assert len(values) == part["vertices"] * width and np.isfinite(values).all()
        original_data = read(first, part["source_vdata"], "<f8")
        assert len(original_data) == part["vertices"] * 4 and np.isfinite(original_data).all()
        assert np.array_equal(original_data.astype("<f4"),
                              read(first, part["attributes"]["data"], "<f4"))
        start = index_base + part["start"]["indices"]
        end = start + len(local)
        assert start % 3 == end % 3 == 0 and end <= len(source_indices[target])
        expected = local.astype("<u8") + vertex_base + part["start"]["vertices"]
        assert np.array_equal(source_indices[target][start:end], expected), (ordinal, key)
        for other_start, other_end, owner in ranges[target]:
            assert end <= other_start or start >= other_end, (prop["source_id"], owner)
        ranges[target].append((start, end, prop["source_id"]))
        vertices += part["vertices"]
        triangles += len(local) // 3
    inventory.append({"source_id": prop["source_id"], "kind": prop["kind"],
                      "x": x, "z": z, "vertices": vertices, "triangles": triangles,
                      "collider_boxes": len(colliders["boxes"]),
                      "collider_cylinders": len(colliders["cylinders"])})

report = {"schema": "forge.tidewater.plaza-parts-proof", "version": 1,
          "source_commit": plaza["source_commit"], "identical_files": len(files),
          "source_scene_digest": plaza["source_scene_digest"],
          "source_index_ranges_exact": True,
          "building_boardwalk_pier_ranges_disjoint": True,
          "scene_and_colliders_unchanged": True,
          "inventory": inventory,
          "total_triangles": sum(item["triangles"] for item in inventory),
          "limits": ["source-space meshes; not yet material-folded portable assets",
                     "no Studio rendering, editing or playtest proof yet"]}
report_path.write_text(json.dumps(report, indent=2), encoding="utf-8")
print(json.dumps({key: report[key] for key in ("identical_files", "source_index_ranges_exact",
                                           "building_boardwalk_pier_ranges_disjoint", "total_triangles")}))
