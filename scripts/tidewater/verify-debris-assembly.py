"""Compare actual source assembly against retained pre-assembly CPU payloads."""
import argparse
import hashlib
import json
from pathlib import Path

import numpy as np


def sha(raw):
    return hashlib.sha256(raw).hexdigest()


def checked(root, name, entries):
    assert Path(name).name == name
    raw = (root / name).read_bytes()
    assert len(raw) == entries[name]["bytes"] and sha(raw) == entries[name]["sha256"]
    return raw


def verify(root, acquisition):
    descriptor = json.loads((root / "assembly.json").read_bytes())
    assert descriptor["schema"] == "forge.tidewater.debris-source-assembly" and descriptor["version"] == 1
    inventory_bytes = (acquisition / "inventory.json").read_bytes()
    inventory = json.loads(inventory_bytes)
    manifest_bytes = (acquisition / "payload-manifest.json").read_bytes()
    inputs = json.loads(manifest_bytes)["payloads"]
    assert descriptor["source_commit"] == inventory["source_commit"]
    assert descriptor["source_inventory_sha256"] == sha(inventory_bytes)
    assert descriptor["source_payload_manifest_sha256"] == sha(manifest_bytes)
    for name in descriptor["files"]:
        checked(root, name, descriptor["files"])
    raw = {}
    widths = {"pos": 3, "nrm": 3, "uv": 2, "tint": 3, "data": 4}
    for batch in inventory["batches"]:
        attributes = {}
        for key, width in widths.items():
            filename = f"{batch}.{'contact-tint' if key == 'tint' else key}.f64.bin"
            values = np.frombuffer(checked(acquisition, filename, inputs), "<f8").reshape(-1, width)
            assert np.isfinite(values).all()
            attributes[key] = values.copy()
        indices = np.frombuffer(checked(acquisition, f"{batch}.indices.u32.bin", inputs), "<u4").reshape(-1, 3)
        raw[batch] = attributes, indices
    # Independent reconstruction of the documented source folding rules.
    rope, rope_indices = raw["rope"]
    rope["data"][:, 3] = 2 + rope["data"][:, 1]
    rope["data"][:, 1:3] = 0
    hard, hard_indices = raw["hard"]
    hard_vertices = len(hard["pos"])
    raw["hard"] = ({key: np.concatenate([hard[key], rope[key]]) for key in widths},
                   np.concatenate([hard_indices, rope_indices + hard_vertices]))
    net, net_indices = raw["net"]
    net["data"][:, 2] = np.maximum(0.02, net["data"][:, 2])
    net["data"][:, 3] = 0
    nature, nature_indices = raw["nature"]
    first = nature["data"][nature_indices[:, 0]]
    caster = ((first[:, 1] == 0) & (first[:, 3] >= 0.2)) | ((first[:, 1] == 6) & (first[:, 3] >= 0.1)) | (
        (first[:, 1] == 4) & (first[:, 3] >= 0.15) & (first[:, 2] >= 2))
    nature_indices = np.concatenate([nature_indices[caster], nature_indices[~caster]])
    raw["nature"] = nature, nature_indices
    opaque_attributes = {key: np.concatenate([raw[name][0][key] for name in ["wood", "hard", "nature"]]).astype("<f4")
                         for key in widths}
    opaque_indices, offset, ranges, start = [], 0, {}, 0
    for name in ["wood", "hard", "nature"]:
        attributes, indices = raw[name]
        opaque_indices.append(indices + offset)
        ranges[f"debris_{name}"] = {"start": start, "count": indices.size}
        start += indices.size
        offset += len(attributes["pos"])
    opaque_indices = np.concatenate(opaque_indices).astype("<u4")
    names = {"pos": "position", "nrm": "normal", "uv": "uv", "tint": "tint", "data": "vdata"}
    assert {mesh["name"] for mesh in descriptor["meshes"]} == {"debris_wood", "debris_hard", "debris_nature", "debris_fabric"}
    for mesh in descriptor["meshes"]:
        fabric = mesh["name"] == "debris_fabric"
        expected_attributes = {key: values.astype("<f4") for key, values in net.items()} if fabric else opaque_attributes
        expected_indices = net_indices if fabric else opaque_indices
        geometry = mesh["geometry"]
        for key, values in expected_attributes.items():
            entry = geometry["attributes"][names[key]]
            actual = np.frombuffer(checked(root, entry["file"], descriptor["files"]), "<f4").reshape(-1, widths[key])
            assert np.array_equal(actual, values), (mesh["name"], key)
        entry = geometry["indices"]
        dtype = {"Uint16Array": "<u2", "Uint32Array": "<u4"}[entry["scalar"]]
        actual = np.frombuffer(checked(root, entry["file"], descriptor["files"]), dtype).reshape(-1, 3)
        assert np.array_equal(actual, expected_indices)
        if not fabric:
            assert mesh["draw_range"] == ranges[mesh["name"]]
        assert mesh["cast_shadow"] == (mesh["name"] == "debris_wood")
    assert descriptor["nature_caster_triangles"] == int(caster.sum())
    assert descriptor["shadow_triangles"] == (raw["wood"][1].size + raw["hard"][1].size) // 3 + int(caster.sum())
    pebble = descriptor["pebble"]
    flags = np.frombuffer(checked(root, pebble["cell_flags"]["file"], descriptor["files"]), "u1")
    assert len(flags) == pebble["cells_per_side"] ** 2 and set(np.unique(flags)) <= {0, 1}
    assert int(flags.sum()) == pebble["occupied_cells"]
    assert checked(root, pebble["mask"]["file"], descriptor["files"]) == checked(acquisition, "pebble-mask.rgba8.bin", inputs)
    assert set(descriptor["materials"]) == {"wood", "hard", "fabric", "nature", "pebbles"}
    return {"matched_triangles": sum(inventory["batches"][name]["indices"] // 3 for name in inventory["batches"]),
            "nature_caster_triangles": int(caster.sum()), "shadow_triangles": descriptor["shadow_triangles"],
            "occupied_pebble_cells": pebble["occupied_cells"]}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ["first", "repeat", "acquisition", "receipt"]:
        parser.add_argument(name, type=Path)
    args = parser.parse_args()
    measured = verify(args.first, args.acquisition)
    assert measured == verify(args.repeat, args.acquisition)
    first = {p.name: sha(p.read_bytes()) for p in args.first.iterdir() if p.is_file()}
    repeat = {p.name: sha(p.read_bytes()) for p in args.repeat.iterdir() if p.is_file()}
    assert first == repeat, "Independent source assemblies differ"
    assert not args.receipt.exists()
    receipt = {"schema": "forge.tidewater.debris-assembly-verification", "version": 1,
               "status": "PASS", "scope": "CPU assembly and generated source material contracts",
               **measured, "files": first, "rendered": False, "visual_parity": False}
    args.receipt.write_text(json.dumps(receipt, indent=2) + "\n", encoding="utf-8")
    print(json.dumps({key: value for key, value in receipt.items() if key != "files"}))


if __name__ == "__main__":
    main()
