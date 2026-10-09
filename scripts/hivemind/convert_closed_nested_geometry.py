"""Reuse the proven geometry converter for closed native mesh captures on F:."""
import datetime
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import struct
import sys

W = Path(r"F:\OpenRPG_ImportWork")
ROOT = W / "nested-prefab-captured-geometry-derivatives-v1"
CONVERTER = W / "dungeon-captured-geometry-converter-v1/convert_geometry_batch_v2.py"
CONTROLS = W / "dungeon-captured-geometry-converter-v1/CONTROLS.v2.json"
RUNS = W / "unity-nested-prefab-static-geometry-remaining-v2/PROJECT-CAPTURE-RUNS.v3.json"

def read(path):
    return json.loads(Path(path).read_text("utf-8-sig"))

def sha(path):
    h = hashlib.sha256()
    with Path(path).open("rb") as f:
        for block in iter(lambda: f.read(1024 * 1024), b""):
            h.update(block)
    return h.hexdigest()

def pin(pins, path, expected=None):
    path = Path(path)
    actual = sha(path)
    if expected is not None and actual != expected:
        raise ValueError(f"Changed pinned input: {path}")
    if str(path) in pins and pins[str(path)] != actual:
        raise ValueError(f"Conflicting input hash: {path}")
    pins[str(path)] = actual
    return actual

def save(name, data):
    path = ROOT / name
    temp = path.with_name(path.name + f".{os.getpid()}.tmp")
    temp.write_text(json.dumps(data, indent=2, ensure_ascii=False, allow_nan=False), "utf-8")
    os.replace(temp, path)

def reflection_bits(raw, width, columns):
    if len(raw) % (width * 4):
        raise ValueError("Partial source element")
    result = bytearray(raw)
    for offset in range(0, len(raw), width * 4):
        for column in columns:
            result[offset + column * 4 + 3] ^= 0x80
    return bytes(result)

def reopen_streams(source, target, metadata):
    """Independent byte-level inspection, without calling converter functions."""
    expected = {"positions.target.f32": reflection_bits(source["positions.f32"], 3, [0])}
    if metadata["normals_count"]:
        expected["normals.target.f32"] = reflection_bits(source["normals.f32"], 3, [0])
    if metadata["tangents_count"]:
        expected["tangents.target.f32"] = reflection_bits(source["tangents.f32"], 4, [0])
        expected["tangents.source_basis.f32"] = reflection_bits(source["tangents.f32"], 4, [0, 3])
    if metadata["colors_count"]:
        expected["colors.target.f32"] = source["colors.f32"]
    for channel, count in enumerate(metadata["uv_counts"]):
        if count:
            raw = source[f"uv{channel}.f32"]
            expected[f"uv{channel}.source.f32"] = raw
            output = bytearray()
            for offset in range(0, len(raw), 16):
                output.extend(raw[offset:offset + 4])
                v = struct.unpack_from("<f", raw, offset + 4)[0]
                output.extend(struct.pack("<f", 1.0 - v))
            expected[f"uv{channel}.target.f32"] = bytes(output)
    for sub in range(metadata["submeshes"]):
        raw = source[f"submesh-{sub}.i32"]
        if len(raw) % 12:
            raise ValueError("Partial source triangle")
        output = b"".join(raw[i:i + 4] + raw[i + 8:i + 12] + raw[i + 4:i + 8]
                          for i in range(0, len(raw), 12))
        expected[f"submesh-{sub}.target.u32"] = output
    if set(expected) != set(target):
        raise ValueError("Missing or unexpected derived attribute/submesh")
    for name, data in expected.items():
        if target[name] != data:
            raise ValueError(f"Derived bytes differ from source transformation: {name}")
    return len(expected)

def self_test():
    # Signed zero and tangent handedness must survive; UV Z/W stay source bytes.
    source = {"positions.f32": struct.pack("<fff", -0.0, 2.0, 3.0),
              "normals.f32": b"", "tangents.f32": struct.pack("<ffff", 1., 0., 0., -1.),
              "colors.f32": b"", "uv0.f32": struct.pack("<ffff", .25, .75, 8., 9.),
              "submesh-0.i32": struct.pack("<iii", 0, 0, 0)}
    meta = {"normals_count": 0, "tangents_count": 1, "colors_count": 0,
            "uv_counts": [1, 0, 0, 0, 0, 0, 0, 0], "submeshes": 1}
    good = {"positions.target.f32": struct.pack("<fff", 0., 2., 3.),
            "tangents.target.f32": struct.pack("<ffff", -1., 0., 0., -1.),
            "tangents.source_basis.f32": struct.pack("<ffff", -1., 0., 0., 1.),
            "uv0.source.f32": source["uv0.f32"], "uv0.target.f32": struct.pack("<ff", .25, .25),
            "submesh-0.target.u32": source["submesh-0.i32"]}
    reopen_streams(source, good, meta)
    controls = ["signed_zero_full_source_uv_and_both_tangent_frames"]
    for label, broken in [
        ("changed_tangent_handedness", {**good, "tangents.source_basis.f32": good["tangents.target.f32"]}),
        ("lost_uv_zw", {**good, "uv0.source.f32": good["uv0.source.f32"][:8]}),
        ("changed_position", {**good, "positions.target.f32": struct.pack("<fff", -0., 2., 3.)}),
        ("missing_indices", {k: v for k, v in good.items() if not k.startswith("submesh")}),
        ("generated_normals", {**good, "normals.target.f32": struct.pack("<fff", 0., 1., 0.)}),
    ]:
        try:
            reopen_streams(source, broken, meta)
        except ValueError:
            controls.append(label)
        else:
            raise RuntimeError(f"Bad control accepted: {label}")
    print(json.dumps({"reopening_controls_passed": controls}))

def run():
    if ROOT.exists():
        raise RuntimeError("Existing bank preserved; inspect receipts before any resume")
    converter_hash = sha(CONVERTER)
    controls = read(CONTROLS)
    if controls["converter_sha256"] != converter_hash or len(controls["passed_controls"]) != 10:
        raise RuntimeError("Ten controls for this exact converter are required")
    spec = importlib.util.spec_from_file_location("closed_geometry_converter", CONVERTER)
    converter = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(converter)
    # Immutable snapshot: only packs whose independent verification is terminal.
    runs = read(RUNS)
    packs = [p for p in runs if p.get("verified")]
    for p in packs:
        if p["unity_exit_code"] != 0 or p["independent_verifier_exit_code"] != 0:
            raise RuntimeError("Inconsistent completed pack")
    if not packs:
        raise RuntimeError("No closed source capture available")
    ROOT.mkdir()
    save("INPUTS.v1.json", {"closed_packs": packs, "converter_sha256": converter_hash,
                           "controls_sha256": sha(CONTROLS), "full_import": False})
    pins = {}
    for path in [CONVERTER, CONTROLS, Path(__file__).resolve()]:
        pin(pins, path)
    result = {"status": "converting_closed_captured_geometry", "started_utc": datetime.datetime.now(datetime.timezone.utc).isoformat(),
              "meshes_reopened": 0, "packs_reopened": 0, "rows": [], "glb_created": False,
              "material_render_qualified": False, "studio_verified": False, "full_import": False}
    save("RUN-RESULT.v1.json", result)
    try:
        for pack in packs:
            root = Path(pack["root"])
            fmt_path = root / "SOURCE-FORMAT-REOPEN.normalized-before-native.v2.json"
            geometry_path = root / "EXPORT-REOPEN.normalized-before-native.v2.json"
            prepared_path = root / "PREPARED.normalized-before-native.v2.json"
            fmt, geometry, prepared = read(fmt_path), read(geometry_path), read(prepared_path)
            if fmt["pack_id"] != pack["pack_id"] or not fmt["source_hierarchy_mesh_formats_match"] or not fmt["original_sources_unchanged"]:
                raise RuntimeError("Source format verification absent")
            pin(pins, geometry_path, fmt["geometry_reopen_sha256"])
            for path in [fmt_path, prepared_path]:
                pin(pins, path)
            manifest_path = root / "manifest.json"
            export_path = root / "export/RESULT.json"
            pin(pins, manifest_path, geometry["manifest_sha256"])
            pin(pins, export_path, geometry["unity_result_sha256"])
            manifest, export = read(manifest_path), read(export_path)
            expected_count = pack["requested_meshes"]
            if not (len(manifest["cases"]) == len(export["cases"]) == len(geometry["rows"]) == expected_count == fmt["verified_source_meshes"]):
                raise RuntimeError("Source capture counts differ")
            if export["failed"] or export["matched"] != expected_count:
                raise RuntimeError("Native capture incomplete")
            for copy in prepared["copies"]:
                for path_key, hash_key in [("original", "original_sha256"), ("original_meta", "original_meta_sha256"),
                                           ("project_copy", "project_copy_sha256"), ("project_meta_copy", "project_meta_copy_sha256")]:
                    pin(pins, copy[path_key], copy[hash_key])
            for ordinal, (request, metadata, receipt) in enumerate(zip(manifest["cases"], export["cases"], geometry["rows"])):
                if not (request["asset_id"] == metadata["asset_id"] == receipt["asset_id"]):
                    raise RuntimeError("Native mesh ordinal differs")
                source = root / "export" / f"{ordinal:03d}"
                raw = {}
                for name, expected_hash in receipt["buffers"].items():
                    pin(pins, source / name, expected_hash)
                    raw[name] = (source / name).read_bytes()
                expected = {"guid": request["expected_guid"], "file_id": request["expected_file_id"]}
                destination = ROOT / pack["pack_id"] / f"{ordinal:03d}"
                report = converter.convert_closed_case(source, metadata, expected, destination)
                outputs = {name: (destination / name).read_bytes() for name in report["derived_streams"]}
                checked = reopen_streams(raw, outputs, metadata)
                for name, details in report["derived_streams"].items():
                    if sha(destination / name) != details["sha256"]:
                        raise RuntimeError("Derivative receipt hash differs")
                case = {"asset_id": request["asset_id"], "source_mesh_identity": expected,
                        "pack_id": pack["pack_id"], "capture_ordinal": ordinal,
                        "derivative": str(destination), "derived_streams_reopened": checked,
                        "vertices": metadata["vertices"], "submeshes": metadata["submeshes"],
                        "geometry_receipt_sha256": sha(destination / "GEOMETRY.v1.json"),
                        "source_format_receipt_sha256": pins[str(fmt_path)],
                        "source_geometry_receipt_sha256": pins[str(geometry_path)],
                        "native_capture_terminal_verified": True,
                        "prefab_instance_aliases": prepared["inventory_rows"][ordinal]["aliases"],
                        "glb_created": False, "full_import": False}
                (destination / "REOPEN.v1.json").write_text(json.dumps(case, indent=2, ensure_ascii=False, allow_nan=False), "utf-8")
                result["rows"].append(case)
                result["meshes_reopened"] += 1
                save("RUN-RESULT.v1.json", result)
            result["packs_reopened"] += 1
            save("RUN-RESULT.v1.json", result)
        changed = [p for p, h in pins.items() if sha(p) != h]
        if changed:
            raise RuntimeError(f"Inputs changed during conversion: {changed}")
        result.update(status="captured_geometry_derivatives_reopened", source_pins=pins, original_sources_unchanged=True)
    except Exception as error:
        result.update(status="refused_or_partial", error=str(error))
    result["finished_utc"] = datetime.datetime.now(datetime.timezone.utc).isoformat()
    save("RUN-RESULT.v1.json", result)
    print(json.dumps({k: v for k, v in result.items() if k not in ("rows", "source_pins")}))
    if result["status"] != "captured_geometry_derivatives_reopened":
        raise SystemExit(1)

if __name__ == "__main__":
    {"self-test": self_test, "run": run}[sys.argv[1]]()
