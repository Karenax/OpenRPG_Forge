"""Verify repeated pier extraction and source mesh ranges, including sign/lanterns."""
from pathlib import Path
import hashlib
import json
import sys

import numpy as np


first, second, source, buildings_root, report_path = map(Path, sys.argv[1:])
assert not report_path.exists()
files = sorted(p.name for p in first.iterdir())
assert files == sorted(p.name for p in second.iterdir())
assert all((first / name).read_bytes() == (second / name).read_bytes() for name in files)
pier = json.loads((first / "pier.json").read_bytes())
buildings = json.loads((buildings_root / "buildings.json").read_bytes())
render = json.loads((source / "village/render.json").read_bytes())
assert pier["schema"] == "forge.tidewater.pier-parts" and pier["version"] == 1
assert pier["source_commit"] == buildings["source_commit"]
assert pier["unchanged_merged_scene"] and pier["unchanged_terrain_and_colliders"]
assert pier["pier"]["source_id"] == f"tidewater:{pier['source_commit']}:pier:0"


def read(root, descriptor, dtype, key="file"):
    name = descriptor[key]
    location = (root / name).resolve()
    assert location.is_relative_to(root.resolve())
    raw = location.read_bytes()
    assert len(raw) == descriptor["bytes"]
    assert hashlib.sha256(raw).hexdigest() == descriptor["sha256"]
    return np.frombuffer(raw, dtype)


nodes = render["nodes"]


def source_mesh(name, ordinal=0):
    node = [n for n in nodes if n["name"] == name][ordinal]
    geometry = render["geometries"][node["geometry"]]
    attributes = {key: read(source, value, "<f4", "path")
                  for key, value in geometry["attributes"].items()}
    indices = read(source, geometry["indices"], "<u4", "path")
    return node, attributes, indices


widths = {"pos": ("position", 3), "nrm": ("normal", 3), "uv": ("uv", 2),
          "tint": ("tint", 3), "data": ("vdata", 4)}
counts = buildings["assembly_batches"]
opaque_keys = ("wood", "hard", "roofMetal", "thatch", "stone")
opaque_vertex_base = {}
opaque_index_base = {}
vertices = indices = 0
for key in opaque_keys:
    opaque_vertex_base[key] = vertices
    opaque_index_base[key] = indices
    vertices += counts[key]["vertices"] + (counts["glass"]["vertices"] if key == "wood" else
                                       counts["rope"]["vertices"] if key == "hard" else 0)
    indices += counts[key]["indices"] + (counts["glass"]["indices"] if key == "wood" else
                                      counts["rope"]["indices"] if key == "hard" else 0)

targets = {"wood": "wood", "hard": "hard", "rope": "hard", "net": "nets"}
matched_static = 0
static_inventory = {}
for key, part in pier["pier"]["static"].items():
    target = targets[key]
    _, source_attrs, source_indices = source_mesh(f"village_{target}")
    vertex_base = (opaque_vertex_base[target] if target != "nets" else 0)
    # The acquired render descriptor materializes each draw range as its own
    # index buffer, while the opaque materials still share global vertices.
    index_base = 0
    if key == "rope":
        vertex_base += counts["hard"]["vertices"]
        index_base += counts["hard"]["indices"]
    vertex_base += part["start"]["vertices"]
    index_base += part["start"]["indices"]
    local_indices = read(first, part["indices"], "<u4")
    assert len(local_indices) and len(local_indices) % 3 == 0
    assert int(local_indices.max()) < part["vertices"]
    assert np.array_equal(source_indices[index_base:index_base + len(local_indices)],
                          local_indices + vertex_base), key
    for attr, (source_name, width) in widths.items():
        actual = read(first, part["attributes"][attr], "<f4")
        assert len(actual) == part["vertices"] * width
        assert np.isfinite(actual).all()
        if attr != "data" or key in ("wood", "hard"):
            start = vertex_base * width
            assert np.array_equal(source_attrs[source_name][start:start + len(actual)], actual), (key, attr)
    matched_static += len(local_indices) // 3
    static_inventory[key] = {"vertices": part["vertices"], "triangles": len(local_indices) // 3}


def check_dynamic(parts, name, ordinal, pivot):
    checked = 0
    for key, part in parts.items():
        if key == "glass":
            target = "wood"
            base = parts.get("wood", {}).get("vertices", 0)
        else:
            target = key
            base = 0
        node, attrs, source_indices = source_mesh(f"village_{name}_{target}", ordinal)
        assert np.allclose(np.asarray(node["matrix_world"])[[12, 13, 14]], pivot, atol=1e-6)
        local_indices = read(first, part["indices"], "<u4")
        index_base = (len(read(first, parts["wood"]["indices"], "<u4"))
                      if key == "glass" and "wood" in parts else 0)
        assert np.array_equal(source_indices[index_base:index_base + len(local_indices)],
                              local_indices + base), (name, ordinal, key, "indices")
        for attr, (source_name, width) in widths.items():
            if attr == "data" and key == "glass":
                continue
            actual = read(first, part["attributes"][attr], "<f4").reshape(-1, width)
            expected = attrs[source_name][base * width:(base + part["vertices"]) * width].reshape(-1, width)
            if attr == "pos":
                actual = np.asarray(actual, dtype=np.float64) - np.asarray(pivot)
                assert np.allclose(actual, expected, atol=1e-5), (name, ordinal, key, attr)
            else:
                assert np.array_equal(actual, expected), (name, ordinal, key, attr)
        checked += len(local_indices) // 3
    return checked


sign_triangles = check_dynamic(pier["pier"]["sign"], "sign", 0,
                               pier["pier"]["sign_pivot"])
lantern_triangles = sum(check_dynamic(item["parts"], "lantern", index, item["pivot"])
                        for index, item in enumerate(pier["pier"]["lanterns"]))
assert len(pier["pier"]["lanterns"]) == 11
assert len(pier["pier"]["colliders"]["boxes"]) == 122
assert len(pier["pier"]["colliders"]["cylinders"]) == 93
report = {"schema": "forge.tidewater.pier-parts-proof", "version": 1,
          "source_commit": pier["source_commit"], "identical_files": len(files),
          "source_scene_digest": pier["source_scene_digest"], "unchanged_merged_scene": True,
          "source_static_indices_and_attributes_exact": True,
          "source_dynamic_rest_pose_exact": True,
          "static_inventory": static_inventory, "static_triangles": matched_static,
          "sign_triangles": sign_triangles, "lantern_triangles": lantern_triangles,
          "collider_boxes": len(pier["pier"]["colliders"]["boxes"]),
          "collider_cylinders": len(pier["pier"]["colliders"]["cylinders"]),
          "limits": ["raw source-space mesh proof; material folding and Studio rendering pending",
                     "dynamic wind animation not reproduced"]}
report_path.write_text(json.dumps(report, indent=2), encoding="utf-8")
print(json.dumps({key: report[key] for key in ("identical_files", "static_triangles",
                                                 "sign_triangles", "lantern_triangles",
                                                 "collider_boxes", "collider_cylinders")}))
