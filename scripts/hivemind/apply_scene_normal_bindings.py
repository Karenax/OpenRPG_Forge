"""Create separate GLB derivatives with captured normal modes; preserve every BIN byte.

Builtin constant normals are explicitly deferred, not replaced by a texture.
"""
import copy
import datetime
import hashlib
import json
import os
from pathlib import Path
import struct
import subprocess
import sys

W = Path(r"F:\OpenRPG_ImportWork")
ROOT = W / "source-graph-scene-normal-bound-glb-v1"
PLAN = W / "source-graph-scene-normal-bindings-v2/PLAN.v1.json"
PROBE = W / "source-graph-imported-normal-decoder-gpu-probe-v1"

def read(path):
    return json.loads(Path(path).read_text("utf-8-sig"))

def sha(path):
    with Path(path).open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()

def now():
    return datetime.datetime.now(datetime.timezone.utc).isoformat()

def save(name, value):
    path = ROOT / name
    temporary = path.with_suffix(f".json.{os.getpid()}.tmp")
    temporary.write_text(json.dumps(value, indent=2), "utf-8")
    os.replace(temporary, path)

def change_document(document, row, texture_evidence):
    before = copy.deepcopy(document)
    edited = copy.deepcopy(document)
    edits = []
    for item in row["materials"]:
        material = edited["materials"][item["material_index"]]["extensions"]["OPENRPG_source_graph"]
        modes = material.get("normal_decode_modes")
        if not isinstance(modes, list) or len(modes) != 8:
            raise RuntimeError("Exact existing normal mode ABI required")
        for slot_row in item["normal_slots"]:
            slot = slot_row["slot"]
            if slot_row["proposed_mode"] != "rg_or_ag" or slot >= len(material["textures"]) or slot >= 8:
                raise RuntimeError("Unresolved or builtin normal mode remains deferred")
            texture = material["textures"][slot]
            original_meta = slot_row["matched_original_meta_sha256"]
            native = texture_evidence[original_meta]
            if texture["source_meta_sha256"] != original_meta or native["unity_guid"] != slot_row["captured_texture_guid"] or native["gpu_format"] != "Bc3RgbaUnorm" or native["unity_graphics_format"] != "RGBA_DXT5_UNorm":
                raise RuntimeError("Exact captured normal importer/storage identity required")
            if modes[slot] != slot_row["current_mode"]:
                raise RuntimeError("Normal binding changed after planning")
            edits.append({"material_index": item["material_index"], "slot": slot, "before": modes[slot], "after": "rg_or_ag", "original_meta_sha256": original_meta, "unity_texture_guid": native["unity_guid"]})
            modes[slot] = "rg_or_ag"
    # Reverse only the intended edits: all other geometry/material declarations stay exact.
    restored = copy.deepcopy(edited)
    for edit in edits:
        restored["materials"][edit["material_index"]]["extensions"]["OPENRPG_source_graph"]["normal_decode_modes"][edit["slot"]] = edit["before"]
    if restored != before or not edits:
        raise RuntimeError("Changes exceeded explicit normal bindings")
    return edited, edits

def convert(row, texture_evidence):
    source_path = Path(row["glb"])
    output_dir = ROOT / "jobs" / row["asset_id"]
    output_dir.mkdir(parents=True, exist_ok=False)
    temporary = output_dir / "model.glb.unverified"
    old_digest = hashlib.sha256()
    binary_digest = hashlib.sha256()
    with source_path.open("rb") as source, temporary.open("xb") as output:
        header = source.read(20)
        old_digest.update(header)
        total = struct.unpack_from("<I", header, 8)[0]
        size = struct.unpack_from("<I", header, 12)[0]
        if header[:4] != b"glTF" or header[16:20] != b"JSON" or size > 16 * 1024 * 1024 or total != source_path.stat().st_size:
            raise RuntimeError("Invalid original GLB header")
        original_json = source.read(size)
        old_digest.update(original_json)
        document, edits = change_document(json.loads(original_json), row, texture_evidence)
        serialized = json.dumps(document, separators=(",", ":"), ensure_ascii=False, allow_nan=False).encode("utf-8")
        serialized += b" " * (-len(serialized) % 4)
        output.write(b"glTF" + struct.pack("<III", 2, total + len(serialized) - size, len(serialized)) + b"JSON")
        output.write(serialized)
        copied = 0
        while chunk := source.read(1024 * 1024):
            old_digest.update(chunk)
            binary_digest.update(chunk)
            output.write(chunk)
            copied += len(chunk)
        if copied != total - 20 - size or old_digest.hexdigest() != row["expected_original_glb_sha256"]:
            raise RuntimeError("Original GLB changed; derivative stays unverified")
    final = output_dir / "model.glb"
    # Reopen the derivative, hash it, and compare every byte after the JSON chunk.
    new_hash = sha(temporary)
    with temporary.open("rb") as result:
        result.seek(20 + len(serialized))
        if hashlib.file_digest(result, "sha256").hexdigest() != binary_digest.hexdigest():
            raise RuntimeError("Embedded shader/texture/geometry BIN changed")
    os.replace(temporary, final)
    return {"asset_id": row["asset_id"], "glb": str(final), "sha256": new_hash, "source_glb": str(source_path), "source_glb_sha256": old_digest.hexdigest(), "unchanged_embedded_tail_sha256": binary_digest.hexdigest(), "normal_binding_edits": edits, "binary_reopened_equal": True, "geometry_native_verified": False, "texture_upload_qualified": False, "gpu_rendered": False, "studio_verified": False, "full_import": False}

def prepare():
    ROOT.mkdir(exist_ok=False)
    save("PREPARED.v1.json", {"plan_sha256": sha(PLAN), "helper_sha256": sha(Path(__file__)), "created_utc": now()})
    with (ROOT / "controller.log").open("xb") as log:
        process = subprocess.Popen([sys.executable, str(Path(__file__)), "run"], cwd=ROOT, stdout=log, stderr=log, stdin=subprocess.DEVNULL, creationflags=subprocess.CREATE_NO_WINDOW)
    save("JOB.v1.json", {"pid": process.pid, "created_utc": now()})
    print(json.dumps({"controller_pid": process.pid, "root": str(ROOT)}))

def self_test():
    document = {"materials": [{"extensions": {"OPENRPG_source_graph": {"normal_decode_modes": [None] * 8, "textures": [{"source_meta_sha256": "original-meta"}]}}}], "meshes": [{"unchanged": True}]}
    row = {"materials": [{"material_index": 0, "normal_slots": [{"slot": 0, "proposed_mode": "rg_or_ag", "current_mode": None, "matched_original_meta_sha256": "original-meta", "captured_texture_guid": "original-guid"}]}]}
    evidence = {"original-meta": {"unity_guid": "original-guid", "gpu_format": "Bc3RgbaUnorm", "unity_graphics_format": "RGBA_DXT5_UNorm"}}
    modified, edits = change_document(document, row, evidence)
    assert document["materials"][0]["extensions"]["OPENRPG_source_graph"]["normal_decode_modes"] == [None] * 8
    assert modified["meshes"] == document["meshes"] and len(edits) == 1
    for category in ["builtin", "wrong_guid", "wrong_format", "changed_mode", "unresolved"]:
        d, r, e = copy.deepcopy(document), copy.deepcopy(row), copy.deepcopy(evidence)
        if category == "builtin": r["materials"][0]["normal_slots"][0]["slot"] = 7
        if category == "wrong_guid": e["original-meta"]["unity_guid"] = "different"
        if category == "wrong_format": e["original-meta"]["gpu_format"] = "Rgba8Unorm"
        if category == "changed_mode": d["materials"][0]["extensions"]["OPENRPG_source_graph"]["normal_decode_modes"][0] = "rgb"
        if category == "unresolved": r["materials"][0]["normal_slots"][0]["proposed_mode"] = None
        try:
            change_document(d, r, e)
        except RuntimeError:
            continue
        raise AssertionError("Unsafe binding accepted: " + category)
    print(json.dumps({"positive_copy_and_geometry_preservation_control": True, "negative_binding_controls_passed": 5, "full_import": False}))

def run():
    state = {"status": "checking_original_normal_evidence", "started_utc": now(), "converted_derivatives": [], "deferred_cases": [], "full_import": False, "new_qualified_assets": 0}
    save("RUN-RESULT.v1.json", state)
    try:
        prepared = read(ROOT / "PREPARED.v1.json")
        if sha(PLAN) != prepared["plan_sha256"] or sha(Path(__file__)) != prepared["helper_sha256"]:
            raise RuntimeError("Prepared helper or plan changed")
        plan = read(PLAN)
        for path, expected in plan["input_pins"].items():
            if sha(path) != expected:
                raise RuntimeError("Normal planning input changed: " + path)
        parity = read(PROBE / "DECODER-PARITY.v1.json")
        if not parity["candidate_mode2_matches_float_rgorag_gpu_reference"] or not parity["all_match_builtin_unpack_reference"]:
            raise RuntimeError("Original normal comparison absent")
        for path, expected in parity["proof_pins"].items():
            if sha(path) != expected:
                raise RuntimeError("Normal native comparison proof changed: " + path)
        passing = {item["asset_id"] for item in parity["rows"] if item["matches_float_rgorag_reference"] and item["matches_builtin_unpack_reference"]}
        evidence = {}
        for row in read(PROBE / "INPUT.v1.json")["rows"]:
            if row["asset_id"] not in passing:
                continue
            for field in ["original_png", "original_meta", "storage"]:
                if sha(row[field]) != row[field + "_sha256"]:
                    raise RuntimeError("Original captured normal source changed")
            evidence[row["original_meta_sha256"]] = row
        save("EVIDENCE-REOPEN.v1.json", {"checked_utc": now(), "comparison_proof_files_rehashed": len(parity["proof_pins"]), "native_normal_sources_rehashed": len(evidence), "normal_modes_admitted_for_captured_inputs_only": True, "original_shadergraph_render_parity_qualified": False})
        state["status"] = "writing_separate_normal_bound_glb_derivatives"
        for row in plan["rows"]:
            unresolved = [item for material in row["materials"] for item in material["normal_slots"] if item["proposed_mode"] is None]
            if unresolved:
                state["deferred_cases"].append({"asset_id": row["asset_id"], "normal_reads": unresolved, "next_action": "qualify_original_builtin_constant_normal_decode", "source_preserved": True})
            else:
                state["converted_derivatives"].append(convert(row, evidence))
            save("RUN-RESULT.v1.json", state)
        if len(state["converted_derivatives"]) != 39 or len(state["deferred_cases"]) != 18:
            raise RuntimeError("Expected 39 captured-input cases and 18 builtin cases")
        save("INPUTS.v1.json", [{key: row[key] for key in ["asset_id", "glb", "sha256"]} for row in state["converted_derivatives"]])
        state["status"] = "39_derivative_glb_normal_bindings_reopened_18_builtin_cases_deferred"
    except Exception as error:
        state.update(status="refused", error=str(error))
    state["finished_utc"] = now()
    save("RUN-RESULT.v1.json", state)
    print(json.dumps({key: value if not isinstance(value, list) else len(value) for key, value in state.items()}))

if __name__ == "__main__":
    {"prepare": prepare, "run": run, "self-test": self_test}[sys.argv[1]]()
