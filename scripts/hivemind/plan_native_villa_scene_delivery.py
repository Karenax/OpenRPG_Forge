"""Use verified native geometry, not older calculated transport fixtures."""
import datetime
import hashlib
import json
from pathlib import Path
import struct

W = Path(r"F:\OpenRPG_ImportWork")
ROOT = W / "villa-native-scene-delivery-plan-v1"
MANIFEST = W / "villa-unity-captured-vertex-glb-batch-v6/RESULT.v1.json"
REOPEN = W / "villa-unity-captured-vertex-glb-batch-v6/REOPEN-RESULT.v1.json"
TEXTURES = W / "villa-unity-imported-texture-storage-v1/RESULT.v1.json"


def read(p):
    return json.loads(Path(p).read_text("utf-8-sig"))


def sha(p):
    with Path(p).open("rb") as f:
        return hashlib.file_digest(f, "sha256").hexdigest()


def main():
    if ROOT.exists():
        raise RuntimeError("Previous native delivery plan preserved")
    manifest, reopened, textures = read(MANIFEST), read(REOPEN), read(TEXTURES)
    if manifest["total"] != 226 or reopened["verified"] != 226 or reopened["batch_manifest_sha256"] != sha(MANIFEST):
        raise RuntimeError("Closed native geometry receipt required")
    if not textures["exact_mip_storage_copied_and_reopened"]:
        raise RuntimeError("Captured texture storage receipt required")
    pins = {str(p): sha(p) for p in [MANIFEST, REOPEN, TEXTURES, Path(__file__).resolve()]}
    texture_map = {}
    for item in textures["rows"]:
        key = item["original_meta_sha256"]
        if key in texture_map and texture_map[key] != item:
            raise RuntimeError("Ambiguous captured importer identity")
        texture_map[key] = item
    rows = []
    for row in manifest["rows"]:
        with Path(row["glb"]).open("rb") as f:
            header = f.read(20)
            size = struct.unpack_from("<I", header, 12)[0]
            if header[:4] != b"glTF" or header[16:20] != b"JSON" or size > 16 * 1024 * 1024:
                raise RuntimeError("Native derivative header invalid")
            raw_json = f.read(size)
            doc = json.loads(raw_json)
        capture = doc.get("extras", {}).get("unity_captured_vertices")
        if not capture or capture["position_basis"] != "Unity mesh local reflected X to Forge right handed; meters; identity mesh child frame":
            raise RuntimeError("Native captured mesh basis declaration absent")
        materials = []
        for index, material in enumerate(doc.get("materials", [])):
            contract = material.get("extensions", {}).get("OPENRPG_source_graph")
            if contract is None:
                materials.append({"material_index": index, "route": "ordinary_gltf"})
                continue
            bindings = []
            for slot, texture in enumerate(contract["textures"]):
                found = texture_map.get(texture["source_meta_sha256"])
                exact = found is not None and found["original_png_sha256"] == texture["png"]["sha256"]
                bindings.append({"slot": slot, "source_meta_sha256": texture["source_meta_sha256"],
                    "original_png_sha256": texture["png"]["sha256"], "captured_storage_identity_matched": exact,
                    "native_storage": found["storage"] if exact else None,
                    "native_storage_sha256": found["storage_sha256"] if exact else None,
                    "full_source_storage_reopen_required": True})
            materials.append({"material_index": index, "route": "original_source_graph", "schema_version": contract["schema_version"],
                "textures": bindings, "normal_decode_modes": contract["normal_decode_modes"],
                "original_render_queue": contract["state"]["render_queue"], "disabled_passes": contract["state"]["disabled_passes"],
                "next_action": "embed_captured_storage_and_qualified_normal_modes_without_changing_native_geometry_or_source_programs"})
        rows.append({"asset_id": row["asset_id"], "glb": row["glb"], "expected_glb_sha256": row["glb_sha256"],
            "header_json_sha256": hashlib.sha256(raw_json).hexdigest(), "native_geometry_prior_receipt": str(REOPEN),
            "full_glb_rehashed_this_plan": False, "unity_mesh_guid": capture["unity_guid"], "unity_mesh_file_id": capture["unity_file_id"],
            "native_position_basis_declaration": capture["position_basis"], "materials": materials,
            "source_frame_math_reopened": False, "gpu_rendered": False, "studio_verified": False})
    if len(rows) != 226 or len({r["asset_id"] for r in rows}) != 226:
        raise RuntimeError("Native scene coverage differs")
    if any(sha(p) != expected for p, expected in pins.items()):
        raise RuntimeError("Plan inputs changed")
    counts = {"native_scenes_inspected": len(rows), "source_material_instances": sum(m["route"] == "original_source_graph" for r in rows for m in r["materials"]),
        "texture_bindings": sum(len(m.get("textures", [])) for r in rows for m in r["materials"]),
        "captured_texture_bindings_matched": sum(t["captured_storage_identity_matched"] for r in rows for m in r["materials"] for t in m.get("textures", []))}
    ROOT.mkdir()
    (ROOT / "PLAN.v1.json").write_text(json.dumps({"status": "native_scene_headers_and_storage_identity_links_prepared", "created_utc": datetime.datetime.now(datetime.timezone.utc).isoformat(),
        "counts": counts, "input_pins": pins, "rows": rows, "older_39_scenes_are_transport_fixtures": True,
        "new_derivatives_written": 0, "full_import": False}, indent=2), "utf-8")
    print(json.dumps(counts))


if __name__ == "__main__":
    main()
