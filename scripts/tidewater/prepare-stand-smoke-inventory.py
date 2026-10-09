"""Independent fixture/package inventory for the public common-object smoke.

Reads the pinned source descriptors, never a candidate UI inventory as an oracle.
Does not create or edit placements. UUID recipes mirror the documented Forge
identity contract, not the renderer or source-object projection implementation.
"""
import argparse
import hashlib
import json
from pathlib import Path
import uuid

COMMIT = "1438b1abfcaee3267092b75573014f4d9b4a983c"
FIXTURE = "50bfd655a7fb4f5a61e4593240ea86dc8d13136d82d558d34737a41b4c6c8720"
NAMESPACE = uuid.UUID("9e65f3d9-f8e2-5f10-8d1e-3e0b6e4d7c21")
DEBRIS_NATIVE = "63e9abf5736fa043765366c78e7eea3688b62ce641b43c7a9db3bed34f528216"
DEBRIS_GEOMETRY = "0a84592f5ccaa0b8077849b6cf054935f6ecb33f41aaf04a19cec3b42255189a"


def identity(domain, key):
    return str(uuid.uuid5(NAMESPACE, f"openrpg-forge:{domain}:{key}"))


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def verified_payload(root, name, entry):
    if (not name or "\\" in name or ":" in name
            or any(part in ("", ".", "..") for part in name.split("/"))):
        raise ValueError("Unsafe acquired payload path")
    path = (root / name).resolve()
    if (not path.is_relative_to(root.resolve()) or not path.is_file()
            or path.stat().st_size != entry["bytes"] or digest(path) != entry["sha256"]):
        raise ValueError(f"Changed acquired payload: {name}")
    return path


def prepare(frontend, archive):
    if digest(archive) != FIXTURE:
        raise ValueError("Unreviewed source fixture")
    head = json.loads(archive.read_bytes())["head"]
    area = head["base"]["terrain"]["area_id"]
    imports = frontend / "forge-imports"
    index_path = imports / "buildings.json"
    index = json.loads(index_path.read_bytes())
    entries = [dict(id=p["id"], label=p["name"], asset_id=p["asset_id"],
                    asset_hash=p["asset_hash"], origin="accepted_manual")
               for p in head["corrections"]["placements"]]
    if len(entries) != 23:
        raise ValueError("Expected 23 pinned accepted placements")
    pins = {}

    def add(key, label, asset, pin):
        entries.append(dict(id=identity("entity", f"imported-object:{area}:{asset}:{key}"),
                            label=label, asset_id=asset, asset_hash=pin, origin="inherited_source"))

    recipes = [
        ("boardwalk", "boardwalks.json", "boardwalks", 3, "tidewater/boardwalk/{commit}/{ordinal}"),
        ("pier", "pier.json", None, 1, "tidewater/pier/{commit}/{ordinal}"),
        ("plaza", "plaza-props.json", "props", 5, "tidewater/plaza/{commit}/{ordinal}"),
        ("village_props", "village-props.json", "props", 27, "tidewater:{commit}:village-prop:{ordinal}"),
        ("beach_props", "beach-props.json", "props", 43, "tidewater:{commit}:beach-prop:{ordinal}"),
        ("fish_props", "geometry/fish-props.json", "instances", 78, "tidewater:{commit}:fish-prop:{ordinal}"),
    ]
    for family, descriptor, array, count, seed in recipes:
        package_pin = index[family + "_sha256"]
        package = imports / package_pin
        manifest_path = package / "package.json"
        if digest(manifest_path) != package_pin:
            raise ValueError(f"Changed {family} package manifest")
        manifest = json.loads(manifest_path.read_bytes())
        pin = manifest["files"][descriptor]
        descriptor_path = package / descriptor
        if digest(descriptor_path) != pin["sha256"] or descriptor_path.stat().st_size != pin["bytes"]:
            raise ValueError(f"Changed {family} source descriptor")
        source = json.loads(descriptor_path.read_bytes())
        if source["source_commit"] != COMMIT:
            raise ValueError("Source commit differs")
        records = source[array] if array else [source["pier"]]
        if len(records) != count:
            raise ValueError(f"Unexpected {family} source count")
        pins[f"forge-imports/{package_pin}/package.json"] = package_pin
        pins[f"forge-imports/{package_pin}/{descriptor}"] = pin["sha256"]
        for ordinal, record in enumerate(records):
            key = record["source_id"]
            asset = identity("asset", seed.format(commit=COMMIT, ordinal=ordinal))
            add(key, key, asset, package_pin)
    stand = identity("asset", "tidewater/fish-stand/v1")
    if stand != "941b533a-8d35-54ce-9fff-96d07b6b4439":
        raise ValueError("Forge UUID contract differs")
    add("source", "Échoppe", stand, index["fish_stand_bundle_sha256"])
    debris_count = 0
    if "debris_native_sha256" in index:
        if index["debris_native_sha256"] != DEBRIS_NATIVE:
            raise ValueError("Unreviewed Debris acquisition")
        native_root = imports / DEBRIS_NATIVE
        native_path = native_root / "debris-native.json"
        if digest(native_path) != DEBRIS_NATIVE:
            raise ValueError("Changed Debris native descriptor")
        native = json.loads(native_path.read_bytes())
        if (native["schema"] != "forge.tidewater.debris-native-input"
                or native["version"] != 1 or native["source_commit"] != COMMIT
                or len(native["files"]) != 4909):
            raise ValueError("Unexpected Debris native input")
        for name, entry in native["files"].items():
            verified_payload(native_root, name, entry)
        geometry_root = native_root / "geometry"
        geometry_path = geometry_root / "package.json"
        if digest(geometry_path) != DEBRIS_GEOMETRY:
            raise ValueError("Changed Debris geometry manifest")
        geometry = json.loads(geometry_path.read_bytes())
        frame_entry = geometry["frames"]
        frame_path = verified_payload(geometry_root, frame_entry["file"], frame_entry)
        frames = json.loads(frame_path.read_bytes())
        if (frames["schema"] != "forge.tidewater.debris-frames" or frames["version"] != 1
                or frames["source_commit"] != COMMIT or len(frames["props"]) != 805):
            raise ValueError("Unexpected Debris frames")
        pins[f"forge-imports/{DEBRIS_NATIVE}/debris-native.json"] = DEBRIS_NATIVE
        pins[f"forge-imports/{DEBRIS_NATIVE}/geometry/package.json"] = DEBRIS_GEOMETRY
        pins[f"forge-imports/{DEBRIS_NATIVE}/geometry/{frame_entry['file']}"] = frame_entry["sha256"]
        for ordinal, record in enumerate(frames["props"]):
            asset = identity("asset", f"tidewater:{COMMIT}:debris-cpu-owner:{ordinal}")
            add(record["source_id"], record["kind"], asset, DEBRIS_GEOMETRY)
        debris_count = 805
    entries.sort(key=lambda entry: entry["id"])
    if len(entries) != 181 + debris_count or len({entry["id"] for entry in entries}) != len(entries):
        raise ValueError("Source identity collision or missing object")
    return dict(schema="forge.tidewater.stand-smoke-inventory", version=1,
                source_commit=COMMIT, fixture_sha256=FIXTURE, area_id=area,
                building_index_sha256=digest(index_path), resource_pins=pins,
                accepted_manual=23, inherited_sources=158 + debris_count, entries=entries,
                ui_observed=False, gpu_observed=False, visual_parity=False)


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--frontend", required=True, type=Path)
    parser.add_argument("--archive", required=True, type=Path)
    parser.add_argument("--output", required=True, type=Path)
    args = parser.parse_args()
    inventory = prepare(args.frontend, args.archive)
    with args.output.open("x", encoding="utf-8") as output:
        json.dump(inventory, output, indent=2, ensure_ascii=False)
