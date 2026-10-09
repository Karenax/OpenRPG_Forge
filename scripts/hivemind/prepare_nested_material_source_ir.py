"""Preserve typed Unity material properties and distinguish rendering references."""
import datetime
import hashlib
import json
from pathlib import Path
import re
import sys

sys.path.insert(0, r"F:\OpenRPG_AssetVault\cache\skill-dev-python")
import yaml

W = Path(r"F:\OpenRPG_ImportWork")
ROOT = W / "nested-prefab-material-source-ir-v1"

def sha(p):
    return hashlib.sha256(p.read_bytes()).hexdigest()

def main():
    if ROOT.exists():
        raise RuntimeError("Existing typed material source evidence preserved")
    inventory_path = W / "nested-prefab-material-dependency-inventory-v1/RESULT.v1.json"
    inventory = json.loads(inventory_path.read_text("utf-8-sig"))
    if not inventory["all_input_pins_unchanged"]:
        raise RuntimeError("Closed inventory required")
    pins = {str(p): sha(p) for p in [inventory_path, Path(__file__).resolve()]}
    rows, wanted = [], set()
    for row in inventory["rows"]:
        native = row["material"]
        dep = next(d for d in row["dependencies"] if d["guid"] == native["guid"])
        path = Path(next(f["path"] for f in dep["verified_files"] if f["path"].endswith(".mat")))
        if sha(path) != inventory["source_pins"][str(path)]:
            raise RuntimeError("Original material source changed")
        pins[str(path)] = inventory["source_pins"][str(path)]
        original = path.read_text("utf-8-sig")
        if not re.search(r"^--- !u!21 &" + re.escape(str(native["file_id"])) + r"\s*$", original, re.M):
            raise RuntimeError("Material local fileID differs from native renderer reference")
        plain = re.sub(r"^%.*\n", "", original, flags=re.M)
        plain = re.sub(r"^--- !u!\d+ &-?\d+.*$", "---", plain, flags=re.M)
        documents = list(yaml.safe_load_all(plain))
        material_docs = [doc["Material"] for doc in documents if isinstance(doc, dict) and "Material" in doc]
        if len(material_docs) != 1 or material_docs[0]["m_Name"] != native["name"]:
            raise RuntimeError("Native material name/document identity differs")
        material = material_docs[0]
        render_refs = [{"role": "shader", "property": "m_Shader", "reference": material["m_Shader"]}]
        for entry in material["m_SavedProperties"].get("m_TexEnvs", []):
            for name, properties in entry.items():
                ref = properties["m_Texture"]
                if ref.get("fileID") != 0:
                    render_refs.append({"role": "texture", "property": name, "reference": ref,
                                        "scale": properties["m_Scale"], "offset": properties["m_Offset"]})
        resolved = {d["guid"]: d for d in row["dependencies"]}
        missing = []
        for ref in render_refs:
            guid = ref["reference"].get("guid")
            if guid is None or guid not in resolved:
                missing.append(ref)
                if guid:
                    wanted.add(guid)
        metadata_refs = [doc["MonoBehaviour"].get("m_Script") for doc in documents
                         if isinstance(doc, dict) and "MonoBehaviour" in doc]
        rows.append({"pack_id": row["pack_id"], "material_identity": native,
                     "source_material": {"path": str(path), "sha256": pins[str(path)]},
                     "source_material_properties": material, "rendering_references": render_refs,
                     "unresolved_rendering_references": missing, "metadata_script_references": metadata_refs,
                     "metadata_scripts_executed": False, "uses": row["uses"],
                     "rendering_references_are_serialized_not_shader_liveness": True,
                     "program_qualified": False, "render_qualified": False})
    matches = {guid: [] for guid in sorted(wanted)}
    for manifest_path in sorted((W / "hivemind-run").glob("*/source.v1.json")):
        manifest_bytes = manifest_path.read_bytes()
        manifest = json.loads(manifest_bytes)
        pins[str(manifest_path)] = hashlib.sha256(manifest_bytes).hexdigest()
        for guid in wanted:
            if guid in manifest["guid_paths"]:
                matches[guid].append({"manifest": str(manifest_path), "asset_path": manifest["guid_paths"][guid],
                                      "automatic_cross_pack_substitution": False})
    for path, expected in pins.items():
        if sha(Path(path)) != expected:
            raise RuntimeError("Input changed during typed material preparation")
    result = {"status": "typed_material_source_properties_reopened", "checked_utc": datetime.datetime.now(datetime.timezone.utc).isoformat(),
              "materials": len(rows), "materials_with_unresolved_serialized_rendering_references": sum(bool(r["unresolved_rendering_references"]) for r in rows),
              "unique_unresolved_rendering_guids": sorted(wanted), "cross_pack_manifest_matches": matches,
              "rows": rows, "source_pins": pins, "all_input_pins_unchanged": True,
              "material_programs_qualified": False, "glb_created": False, "studio_verified": False, "full_import": False}
    ROOT.mkdir()
    (ROOT / "RESULT.v1.json").write_text(json.dumps(result, indent=2, ensure_ascii=False, allow_nan=False), "utf-8")
    print(json.dumps({k: v for k, v in result.items() if k not in {"rows", "source_pins", "cross_pack_manifest_matches"}}))

if __name__ == "__main__":
    main()
