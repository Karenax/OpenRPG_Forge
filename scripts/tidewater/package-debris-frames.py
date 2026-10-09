"""Prepare portable Debris frames from verified source payloads and assembly.

This intermediate package preserves custom material data; it is not GLB or
runtime qualification. Usage: ACQUISITION ASSEMBLY NEW_OUTPUT.
"""
import hashlib
import importlib.util
import json
from pathlib import Path
import sys

import numpy as np


def sha(raw):
    return hashlib.sha256(raw).hexdigest()


def main():
    acquisition, assembly, output = map(Path, sys.argv[1:])
    assert not output.exists()
    assert all(not output.resolve().is_relative_to(root.resolve()) for root in [acquisition, assembly])
    spec = importlib.util.spec_from_file_location("debris_assembly_proof", Path(__file__).with_name("verify-debris-assembly.py"))
    proof = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(proof)
    assembly_proof = proof.verify(assembly, acquisition)
    owner_bytes = (acquisition / "ownership.json").read_bytes()
    ownership = json.loads(owner_bytes)
    assert ownership["schema"] == "forge.tidewater.debris-source-ownership" and ownership["version"] == 1
    input_manifest = json.loads((acquisition / "payload-manifest.json").read_bytes())
    assembled = json.loads((assembly / "assembly.json").read_bytes())
    assert assembled["source_commit"] == ownership["source_commit"]
    inputs = input_manifest["payloads"]
    pending = {}

    def save(raw):
        digest = sha(raw)
        name = digest + ".bin"
        assert name not in pending or pending[name] == raw
        pending[name] = raw
        return {"file": name, "bytes": len(raw), "sha256": digest}

    widths = {"pos": 3, "nrm": 3, "uv": 2, "tint": 3, "data": 4}
    raw = {}
    for batch in ["wood", "hard", "nature", "net", "rope"]:
        attrs = {}
        for key, width in widths.items():
            name = f"{batch}.{'contact-tint' if key == 'tint' else key}.f64.bin"
            attrs[key] = np.frombuffer(proof.checked(acquisition, name, inputs), "<f8").reshape(-1, width)
        indices = np.frombuffer(proof.checked(acquisition, f"{batch}.indices.u32.bin", inputs), "<u4")
        raw[batch] = attrs, indices
    cursor = {name: [0, 0] for name in raw}
    records, max_error, triangle_count, shadow_count = [], 0.0, 0, 0
    ids = set()
    for owner in ownership["owners"]:
        assert owner["source_id"] not in ids
        ids.add(owner["source_id"])
        bounds = owner["bounds_world"]
        anchor = (np.array(bounds["min"]) + np.array(bounds["max"])) * 0.5
        anchor[1] = bounds["min"][1]
        parts, local_points = [], []
        for batch, interval in owner["parts"].items():
            assert [interval["vertex_start"], interval["index_start"]] == cursor[batch]
            low, high = interval["vertex_start"], interval["vertex_start"] + interval["vertices"]
            begin, end = interval["index_start"], interval["index_start"] + interval["indices"]
            attrs = {key: values[low:high].copy() for key, values in raw[batch][0].items()}
            indices = raw[batch][1][begin:end].astype(np.int64) - low
            assert len(indices) % 3 == 0 and len(indices) > 0 and indices.min() >= 0 and indices.max() < high - low
            cursor[batch] = [high, end]
            material = batch
            if batch == "rope":
                attrs["data"][:, 3] = 2 + attrs["data"][:, 1]
                attrs["data"][:, 1:3] = 0
                material = "hard"
            elif batch == "net":
                attrs["data"][:, 2] = np.maximum(0.02, attrs["data"][:, 2])
                attrs["data"][:, 3] = 0
                material = "fabric"
            if material == "nature":
                d = attrs["data"][indices.reshape(-1, 3)[:, 0]]
                cast = ((d[:, 1] == 0) & (d[:, 3] >= .2)) | ((d[:, 1] == 6) & (d[:, 3] >= .1)) | (
                    (d[:, 1] == 4) & (d[:, 3] >= .15) & (d[:, 2] >= 2))
            else:
                cast = np.full(len(indices) // 3, material in ["wood", "hard"], dtype=bool)
            shadow_indices = indices.reshape(-1, 3)[cast].astype("<u4")
            source_position = attrs["pos"].astype("<f4")
            local = (source_position.astype(np.float64) - anchor).astype("<f4")
            error = float(np.max(np.abs(local.astype(np.float64) + anchor - source_position)))
            assert error < .0001
            max_error = max(max_error, error)
            attr_refs = {key: save(values.astype("<f4").tobytes()) for key, values in attrs.items()}
            attr_refs["pos"] = save(local.tobytes())
            parts.append({"source_material": batch, "material": material, "vertices": high - low,
                          "attributes": attr_refs, "indices": save(indices.astype("<u4").tobytes()),
                          "shadow_indices": save(shadow_indices.tobytes()),
                          "reference_position": save(source_position.tobytes()),
                          "roundtrip_position_max_m": error})
            triangle_count += len(indices) // 3
            shadow_count += len(shadow_indices)
            local_points.append(local)
        points = np.concatenate(local_points)
        records.append({"source_id": owner["source_id"], "kind": owner["kind"],
                        "source_anchor_xz": owner["source_anchor_xz"],
                        "source_frame": {"translation_m": anchor.tolist(), "rotation_xyzw": [0, 0, 0, 1], "scale": [1, 1, 1]},
                        "pivot_basis": "measured rest-pose bounds centre XZ, minimum Y; identity axes",
                        "local_bounds": {"min": points.min(axis=0).tolist(), "max": points.max(axis=0).tolist()},
                        "parts": parts})
    for batch, (attrs, indices) in raw.items():
        assert cursor[batch] == [len(attrs["pos"]), len(indices)]
    assert triangle_count == assembly_proof["matched_triangles"]
    assert shadow_count == assembly_proof["shadow_triangles"]
    # Retain generated contracts and dependencies without pretending that textures
    # or shader adaptation are already runtime-ready.
    material_refs = {name: save(proof.checked(assembly, name, assembled["files"])) for name in assembled["files"] if name.endswith(".wgsl")}
    def remap_references(value, refs):
        if isinstance(value, dict):
            if value.get("file") in refs:
                return {**value, **refs[value["file"]]}
            return {key: remap_references(item, refs) for key, item in value.items()}
        if isinstance(value, list):
            return [remap_references(item, refs) for item in value]
        return value

    materials = {"source_contracts": remap_references(assembled["materials"], material_refs),
                 "runtime_adapted": False, "texture_pixels_acquired": False}
    material_descriptor = save(json.dumps(materials, indent=2).encode())
    pebble_names = set()

    def collect_files(value):
        if isinstance(value, dict):
            if "file" in value:
                pebble_names.add(value["file"])
            for child in value.values():
                collect_files(child)
        elif isinstance(value, list):
            for child in value:
                collect_files(child)

    collect_files(assembled["pebble"])
    pebble_refs = {name: save(proof.checked(assembly, name, assembled["files"])) for name in sorted(pebble_names)}
    pebble_descriptor = save(json.dumps(remap_references(assembled["pebble"], pebble_refs), indent=2).encode())
    description = {"schema": "forge.tidewater.debris-frames", "version": 1,
                   "source_commit": ownership["source_commit"], "source_ownership_sha256": sha(owner_bytes),
                   "source_assembly_sha256": sha((assembly / "assembly.json").read_bytes()),
                   "props": records, "scanned_instances": ownership["scanned_instances"],
                   "source_triangles": triangle_count, "shadow_triangles": shadow_count,
                   "roundtrip_position_max_m": max_error,
                   "limits": ["Intermediate portable CPU geometry, not qualified GLB/runtime assets.",
                              "Collisions remain separate until exact source ownership is attached.",
                              "Use reference_position for source wind phase; local positions change that phase.",
                              "Scanned models, terrain texture bindings, pebbles and Studio integration remain pending."]}
    frames = save(json.dumps(description, indent=2).encode())
    license_ref = save((acquisition / "LICENSE-Tidewater.txt").read_bytes())
    credits_ref = save((acquisition / "CREDITS-scanned.md").read_bytes())
    collision_ref = save(proof.checked(acquisition, "colliders.json", inputs))
    manifest = {"schema": "forge.tidewater.debris-package", "version": 1,
                "source_commit": ownership["source_commit"], "frames": frames, "materials": material_descriptor,
                "pebble_field": pebble_descriptor,
                "license": license_ref, "credits": credits_ref, "unattached_source_colliders": collision_ref,
                "files": {name: {"bytes": len(data), "sha256": sha(data)} for name, data in sorted(pending.items())},
                "qualification": "intermediate portable frames; no runtime/Studio/GLB qualification"}
    output.mkdir()
    for name, data in pending.items():
        (output / name).write_bytes(data)
    (output / "package.json").write_text(json.dumps(manifest, indent=2), encoding="utf-8")
    for name in manifest["files"]:
        proof.checked(output, name, manifest["files"])
    print(json.dumps({"props": len(records), "parts": sum(len(obj["parts"]) for obj in records),
                      "triangles": triangle_count, "shadow_triangles": shadow_count,
                      "roundtrip_position_max_m": max_error, "payloads": len(pending)}))


if __name__ == "__main__":
    main()
