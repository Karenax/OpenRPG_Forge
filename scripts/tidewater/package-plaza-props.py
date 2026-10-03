"""Fold verified source plaza props into local frames and a portable payload.

Usage: RAW_PARTS SOURCE_RENDER BUILDING_PACKAGE UPSTREAM NEW_OUTPUT
"""
from collections import Counter
from pathlib import Path
import hashlib
import json
import sys

import numpy as np


parts_root, source, buildings, upstream, output = map(Path, sys.argv[1:])
assert not output.exists()
source_bytes = (parts_root / "plaza-props.json").read_bytes()
parts = json.loads(source_bytes)
assert parts["schema"] == "forge.tidewater.plaza-parts" and parts["version"] == 1
assert len(parts["props"]) == 5
building_manifest = json.loads((buildings / "package.json").read_bytes())
assert building_manifest["schema"] == "forge.tidewater.building-package"
assert building_manifest["source_commit"] == parts["source_commit"]
materials_raw = (buildings / "materials.json").read_bytes()
materials_sha = hashlib.sha256(materials_raw).hexdigest()
assert building_manifest["material_library"]["sha256"] == materials_sha
license_raw = (upstream / "LICENSE").read_bytes()
assert b"MIT License" in license_raw and b"Permission is hereby granted" in license_raw
render = json.loads((source / "village/render.json").read_bytes())

mapping = {"wood": "wood", "glass": "wood", "hard": "hard"}
names = {"pos": "position", "nrm": "normal", "uv": "uv", "tint": "tint", "data": "vdata"}
widths = {"pos": 3, "nrm": 3, "uv": 2, "tint": 3, "data": 4}


def read(root, descriptor, dtype, key="file"):
    path = (root / descriptor[key]).resolve()
    assert path.is_relative_to(root.resolve())
    raw = path.read_bytes()
    assert len(raw) == descriptor["bytes"]
    assert hashlib.sha256(raw).hexdigest() == descriptor["sha256"]
    return np.frombuffer(raw, dtype)


available = {}
material_ids = {}
for target in sorted(set(mapping.values())):
    node = next(node for node in render["nodes"] if node["name"] == f"village_{target}")
    geometry = render["geometries"][node["geometry"]]
    vertices = np.concatenate([
        read(source, geometry["attributes"][names[name]], "<f4", "path").reshape(-1, width)
        for name, width in widths.items()
    ], axis=1)
    indices = read(source, geometry["indices"], "<u4", "path").reshape(-1, 3)
    available[target] = Counter(triangle.tobytes() for triangle in vertices[indices])
    material_ids[target] = node["material"]

files = {"LICENSE-Tidewater.txt": license_raw}


def save(raw):
    digest = hashlib.sha256(raw).hexdigest()
    name = digest + ".bin"
    files.setdefault(name, raw)
    assert files[name] == raw
    return {"file": name, "bytes": len(raw), "sha256": digest}


records = []
matched = 0
max_error = 0.0
for ordinal, prop in enumerate(parts["props"]):
    assert prop["source_id"] == f"tidewater:{parts['source_commit']}:plaza-prop:{ordinal}"
    assert prop["kind"] in ("bench", "lampPost", "barrel")
    args = prop["args"]
    anchor = np.array(args[1:4] if prop["kind"] == "barrel" else args[0:3], dtype=np.float64)
    assert anchor.shape == (3,) and np.isfinite(anchor).all()
    prepared = []
    bounds = []
    for key, part in prop["parts"].items():
        assert key in mapping
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
        attrs["data"] = folded_data.astype("<f4")
        indices = read(parts_root, part["indices"], "<u4").reshape(-1, 3)
        assert indices.size and int(indices.max()) < part["vertices"]
        vertices = np.concatenate(list(attrs.values()), axis=1)
        for triangle in vertices[indices]:
            signature = triangle.tobytes()
            assert available[target][signature] > 0, (prop["source_id"], key)
            available[target][signature] -= 1
            matched += 1
        positions = attrs["pos"]
        local = (positions.astype(np.float64) - anchor).astype("<f4")
        restored = local.astype(np.float64) + anchor
        error = float(np.max(np.abs(restored - positions)))
        assert error < 0.0001, "Local conversion exceeds 0.1 mm"
        max_error = max(max_error, error)
        local_attrs = {name: save(values.tobytes()) for name, values in attrs.items()}
        local_attrs["pos"] = save(local.tobytes())
        prepared.append({"source_material": key, "material": material_ids[target],
                         "target": target, "vertices": part["vertices"],
                         "attributes": local_attrs, "indices": save(indices.tobytes()),
                         "reference_position": save(positions.tobytes()),
                         "reference_normal": save(attrs["nrm"].tobytes()),
                         "roundtrip_position_max_m": error})
        bounds.append(local)
    boxes = [box | {"center": (np.array(box["center"], dtype=np.float64) - anchor).tolist()}
             for box in prop["colliders"]["boxes"]]
    cylinders = [c | {"x": c["x"] - anchor[0], "z": c["z"] - anchor[2],
                      "yMin": c["yMin"] - anchor[1], "yMax": c["yMax"] - anchor[1]}
                 for c in prop["colliders"]["cylinders"]]
    lights = [light | {"position": (np.array(light["position"], dtype=np.float64) - anchor).tolist()}
              for light in prop["lights"]]
    points = np.concatenate(bounds)
    records.append({"source_id": prop["source_id"], "kind": prop["kind"],
                    "source_args": args,
                    "source_frame": {"translation_m": anchor.tolist(),
                                     "rotation_xyzw": [0, 0, 0, 1], "scale": [1, 1, 1]},
                    "pivot_basis": "source prop origin at terrain height",
                    "local_bounds": {"min": points.min(axis=0).tolist(),
                                     "max": points.max(axis=0).tolist()},
                    "colliders": {"boxes": boxes, "cylinders": cylinders},
                    "lights": lights, "parts": prepared})

assert matched == 1207
descriptor = {"schema": "forge.tidewater.plaza-frames", "version": 1,
              "source_commit": parts["source_commit"],
              "source_descriptor_sha256": hashlib.sha256(source_bytes).hexdigest(),
              "matched_source_triangles": matched,
              "roundtrip_position_max_m": max_error,
              "props": records,
              "limits": ["source rest-pose geometry and collisions",
                         "lantern light metadata retained; runtime staging still required"]}
descriptor_raw = json.dumps(descriptor, indent=2).encode("utf-8")
files["plaza-props.json"] = descriptor_raw


def entry(raw):
    return {"bytes": len(raw), "sha256": hashlib.sha256(raw).hexdigest()}


manifest = {"schema": "forge.tidewater.plaza-package", "version": 1,
            "source_commit": parts["source_commit"],
            "building_material_library_sha256": materials_sha,
            "props": {"file": "plaza-props.json", **entry(descriptor_raw)},
            "license": {"file": "LICENSE-Tidewater.txt", **entry(license_raw)},
            "files": {name: entry(raw) for name, raw in sorted(files.items())},
            "qualification": "verified portable payload; no Studio rendering or authoring proof"}
output.mkdir()
for name, raw in files.items():
    (output / name).write_bytes(raw)
(output / "package.json").write_text(json.dumps(manifest, indent=2), encoding="utf-8")
for name, expected in manifest["files"].items():
    assert entry((output / name).read_bytes()) == expected
print(json.dumps({"props": len(records), "parts": sum(len(p["parts"]) for p in records),
                  "matched_source_triangles": matched, "payloads": len(files),
                  "roundtrip_position_max_m": max_error,
                  "package_sha256": hashlib.sha256((output / "package.json").read_bytes()).hexdigest()}))
