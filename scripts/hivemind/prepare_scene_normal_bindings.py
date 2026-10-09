"""Resolve missing scene normal-mode declarations against existing captured evidence.

This is a conversion plan, not an admission to rendering or a relaxed reader.
"""
import datetime
import hashlib
import json
import os
from pathlib import Path
import re
import struct

W = Path(r"F:\OpenRPG_ImportWork")
ROOT = W / "source-graph-scene-normal-bindings-v2"
GEOMETRY = W / "source-graph-scene-geometry-adapter-v2"
PROBE = W / "source-graph-imported-normal-decoder-gpu-probe-v1"

def read(path):
    return json.loads(Path(path).read_text("utf-8-sig"))

def sha(path):
    with Path(path).open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()

def inspect(case, evidence):
    with Path(case["glb"]).open("rb") as source:
        header = source.read(20)
        if header[:4] != b"glTF" or header[16:20] != b"JSON":
            raise RuntimeError("Invalid GLB header")
        json_length = struct.unpack_from("<I", header, 12)[0]
        if json_length > 16 * 1024 * 1024:
            raise RuntimeError("Oversized GLB JSON")
        doc = json.loads(source.read(json_length))
        binary_header = source.read(8)
        if binary_header[4:] != b"BIN\x00":
            raise RuntimeError("Missing embedded source data")
        binary_start = 28 + json_length
        output = []
        for material_index, material in enumerate(doc["materials"]):
            contract = material.get("extensions", {}).get("OPENRPG_source_graph")
            if contract is None:
                continue
            slots = set()
            for stage in [contract["forward"], contract["prepass"], *contract["libraries"]]:
                view = doc["bufferViews"][stage["buffer_view"]]
                if view["byteLength"] > 2 * 1024 * 1024:
                    raise RuntimeError("Oversized program view")
                source.seek(binary_start + view.get("byteOffset", 0))
                data = source.read(view["byteLength"])
                if hashlib.sha256(data).hexdigest() != stage["sha256"]:
                    raise RuntimeError("Changed embedded source program")
                slots.update(int(group) * 4 + "xyzw".index(channel) for group, channel in re.findall(r"source_graph\.normal_decode_modes\[(\d+)\]\.([xyzw])", data.decode("utf-8")))
            proposed = []
            for slot in sorted(slots):
                if slot >= len(contract["textures"]) or slot >= 8:
                    proposed.append({"slot": slot, "proposed_mode": None, "reason": "normal_uniform_has_no_matching_texture_slot", "texture_count": len(contract["textures"]), "requires_conversion_repair": True})
                    continue
                texture = contract["textures"][slot]
                captured = evidence.get(texture["source_meta_sha256"])
                proposed.append({"slot": slot, "current_mode": contract.get("normal_decode_modes", [None] * 8)[slot], "proposed_mode": "rg_or_ag" if captured else None, "captured_texture_guid": captured["unity_guid"] if captured else None, "matched_original_meta_sha256": texture["source_meta_sha256"], "requires_full_texture_storage_and_source_pin_reopen_before_apply": True})
            output.append({"material_index": material_index, "normal_slots": proposed})
    return {"asset_id": case["asset_id"], "glb": case["glb"], "expected_original_glb_sha256": case["sha256"], "full_glb_hash_reopened_this_preparation": False, "materials": output}

def main():
    ROOT.mkdir(exist_ok=False)
    paths = [GEOMETRY / "INPUTS.v1.json", PROBE / "INPUT.v1.json", PROBE / "DECODER-PARITY.v1.json", PROBE / "RUN-RESULT.v1.json", Path(__file__)]
    pins = {str(path): sha(path) for path in paths}
    parity = read(PROBE / "DECODER-PARITY.v1.json")
    run = read(PROBE / "RUN-RESULT.v1.json")
    if not parity["candidate_mode2_matches_float_rgorag_gpu_reference"] or not parity["all_match_builtin_unpack_reference"] or run["native_exit_code"] != 0 or run["comparison_exit_code"] != 0:
        raise RuntimeError("Existing decoder comparison absent")
    passing = {row["asset_id"] for row in parity["rows"] if row["matches_float_rgorag_reference"] and row["matches_builtin_unpack_reference"]}
    evidence = {}
    for row in read(PROBE / "INPUT.v1.json")["rows"]:
        if row["asset_id"] in passing:
            key = row["original_meta_sha256"]
            if key in evidence and evidence[key]["storage_sha256"] != row["storage_sha256"]:
                raise RuntimeError("Ambiguous captured storage for original metadata")
            evidence[key] = row
    cases = read(GEOMETRY / "INPUTS.v1.json")
    rows = [inspect(case, evidence) for case in cases]
    slots = [slot for row in rows for material in row["materials"] for slot in material["normal_slots"]]
    missing = [slot for slot in slots if slot["proposed_mode"] is None]
    if any(sha(path) != expected for path, expected in pins.items()):
        raise RuntimeError("Evidence changed during planning")
    result = {"schema": "openrpg.scene-normal-binding-conversion-plan.v1", "created_utc": datetime.datetime.now(datetime.timezone.utc).isoformat(), "scenes_inspected": len(rows), "normal_reads_resolved": len(slots) - len(missing), "unresolved_normal_reads": len(missing), "input_pins": pins, "rows": rows, "glb_modified": False, "reader_guard_relaxed": False, "original_shadergraph_render_parity_qualified": False, "full_import": False}
    with (ROOT / "PLAN.v1.json").open("x", encoding="utf-8") as output:
        json.dump(result, output, indent=2)
    print(json.dumps({key: value for key, value in result.items() if key not in ["rows", "input_pins"]}))

if __name__ == "__main__":
    main()
