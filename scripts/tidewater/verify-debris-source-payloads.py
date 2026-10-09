"""Verify retained CPU Debris acquisition; this is not runtime qualification."""
import argparse
import hashlib
import json
import math
import struct
from pathlib import Path


def sha(data):
    return hashlib.sha256(data).hexdigest()


def verify(root, baseline):
    manifest = json.loads((root / "payload-manifest.json").read_text())
    inventory = json.loads((root / "inventory.json").read_text())
    assert manifest["schema"] == "forge.tidewater.debris-source-payloads"
    assert manifest["version"] == 1 and manifest["byte_order"] == "little-endian"
    assert manifest["source_commit"] == inventory["source_commit"]
    assert (root / "inventory.json").read_bytes() == (baseline / "inventory.json").read_bytes()
    payloads = {}
    for name, entry in manifest["payloads"].items():
        assert Path(name).name == name, "Unsafe payload path"
        data = (root / name).read_bytes()
        assert len(data) == entry["bytes"] and sha(data) == entry["sha256"], name
        payloads[name] = data
    vertices = indices = darkened = 0
    for batch, description in inventory["batches"].items():
        count = description["vertices"]
        vertices += count
        for attribute, width in {"pos": 3, "nrm": 3, "uv": 2, "tint": 3, "data": 4}.items():
            data = payloads[f"{batch}.{attribute}.f64.bin"]
            assert len(data) == count * width * 8
            assert sha(data) == description["attributes"][attribute]
            assert all(math.isfinite(v[0]) for v in struct.iter_unpack("<d", data))
        data = payloads[f"{batch}.indices.u32.bin"]
        assert len(data) == description["indices"] * 4 and len(data) % 12 == 0
        assert sha(data) == description["indices_sha256"]
        assert all(v[0] < count for v in struct.iter_unpack("<I", data))
        indices += description["indices"]
        before = payloads[f"{batch}.tint.f64.bin"]
        after = payloads[f"{batch}.contact-tint.f64.bin"]
        assert len(after) == len(before)
        for (raw,), (contact,) in zip(struct.iter_unpack("<d", before), struct.iter_unpack("<d", after)):
            assert math.isfinite(contact)
            assert raw >= 0 and raw * 0.62 - 1e-12 <= contact <= raw + 1e-12
            darkened += contact < raw
    mask = manifest["payloads"]["pebble-mask.rgba8.bin"]
    assert mask["resolution"] == 1024 and mask["components"] == 4
    assert mask["bytes"] == 1024 * 1024 * 4 and mask["texel_m"] > 0
    assert any(payloads["pebble-mask.rgba8.bin"])
    colliders = json.loads(payloads["colliders.json"])
    collider_count = len(colliders["boxes"]) + len(colliders["cylinders"])
    assert collider_count == inventory["new_colliders"]
    assert darkened > 0
    result = {"vertices": vertices, "triangles": indices // 3, "colliders": collider_count,
              "darkened_tint_components": darkened, "payloads": len(payloads)}
    if (root / "ownership.json").exists():
        ownership = json.loads((root / "ownership.json").read_text())
        assert ownership["schema"] == "forge.tidewater.debris-source-ownership"
        assert ownership["version"] == 1 and ownership["source_commit"] == inventory["source_commit"]
        cursor = {batch: [0, 0] for batch in inventory["batches"]}
        ids = set()
        for owner in ownership["owners"]:
            assert owner["source_id"] not in ids
            ids.add(owner["source_id"])
            bounds_min = [math.inf] * 3
            bounds_max = [-math.inf] * 3
            for batch, part in owner["parts"].items():
                assert [part["vertex_start"], part["index_start"]] == cursor[batch]
                assert part["vertices"] > 0 and part["indices"] > 0
                low, high = part["vertex_start"], part["vertex_start"] + part["vertices"]
                data = payloads[f"{batch}.indices.u32.bin"]
                begin, end = part["index_start"] * 4, (part["index_start"] + part["indices"]) * 4
                assert all(low <= v[0] < high for v in struct.iter_unpack("<I", data[begin:end]))
                positions = payloads[f"{batch}.pos.f64.bin"][low * 24:high * 24]
                for xyz in struct.iter_unpack("<ddd", positions):
                    for axis, value in enumerate(xyz):
                        bounds_min[axis] = min(bounds_min[axis], value)
                        bounds_max[axis] = max(bounds_max[axis], value)
                cursor[batch] = [high, part["index_start"] + part["indices"]]
            assert owner["bounds_world"] == {"min": bounds_min, "max": bounds_max}
        for batch, description in inventory["batches"].items():
            assert cursor[batch] == [description["vertices"], description["indices"]]
        scanned = ownership["scanned_instances"]
        assert [{k: v for k, v in item.items() if k != "source_id"} for item in scanned] == inventory["scanned_instances"]
        for item in scanned:
            assert item["source_id"] not in ids
            ids.add(item["source_id"])
        assert sum(owner["kind"] == "coveStone" for owner in ownership["owners"]) == inventory["counts"]["coveStones"]
        for kind, anchor in [("group:crates", [34.4, -114.4]), ("group:barrels", [47.6, -114.8])]:
            assert any(owner["kind"] == kind and owner["source_anchor_xz"] == anchor for owner in ownership["owners"])
        result.update(geometry_owners=len(ownership["owners"]), scanned_owners=len(scanned),
                      ownership_ranges_complete=True)
    return result


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("first", type=Path)
    parser.add_argument("repeat", type=Path)
    parser.add_argument("baseline", type=Path)
    parser.add_argument("receipt", type=Path)
    args = parser.parse_args()
    first = verify(args.first, args.baseline)
    assert first == verify(args.repeat, args.baseline)
    first_files = {p.name: sha(p.read_bytes()) for p in args.first.iterdir() if p.is_file()}
    repeat_files = {p.name: sha(p.read_bytes()) for p in args.repeat.iterdir() if p.is_file()}
    assert first_files == repeat_files, "Independent acquisitions differ"
    assert not args.receipt.exists(), "Preserve existing evidence"
    receipt = {"schema": "forge.tidewater.debris-source-payload-verification", "version": 1,
               "status": "PASS", "scope": "CPU retained payloads only", **first,
               "files": first_files, "runtime_qualified": False, "visual_parity": False}
    args.receipt.write_text(json.dumps(receipt, indent=2) + "\n", encoding="utf-8")
    print(json.dumps({key: value for key, value in receipt.items() if key != "files"}))


if __name__ == "__main__":
    main()
