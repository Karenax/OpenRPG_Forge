"""Record one exact isolated Unity importer migration; originals stay pinned."""
import datetime
import hashlib
import json
from pathlib import Path
import sys

sys.path.insert(0, r"F:\OpenRPG_AssetVault\cache\skill-dev-python")
import yaml

W = Path(r"F:\OpenRPG_ImportWork")
ROOT = W / "native-village-isolated-importer-migration-v1"
CAPTURE = W / "unity-nested-prefab-static-geometry-capture-v1/native-american-village"

def read(p):
    return json.loads(p.read_text("utf-8-sig"))

def sha(p):
    return hashlib.sha256(Path(p).read_bytes()).hexdigest()

def flatten(value, prefix=""):
    result = {}
    if isinstance(value, dict):
        for key, child in value.items():
            result.update(flatten(child, f"{prefix}.{key}" if prefix else str(key)))
    else:
        result[prefix] = value
    return result

def main():
    if ROOT.exists():
        raise RuntimeError("Existing migration evidence preserved")
    prepared_path = CAPTURE / "PREPARED.normalized-before-native.v2.json"
    prepared = read(prepared_path)
    selected = [x for x in prepared["copies"] if "88c6743bda53a7943a953a3a72605430" in x["project_meta_copy"]]
    if len(selected) != 1:
        raise RuntimeError("Exact migration source ambiguous")
    copy = selected[0]
    before_path, after_path = Path(copy["original_meta"]), Path(copy["project_meta_copy"])
    for key, hashkey in [("original", "original_sha256"), ("original_meta", "original_meta_sha256"),
                         ("project_copy", "project_copy_sha256")]:
        if sha(copy[key]) != copy[hashkey]:
            raise RuntimeError("Original source or copied FBX changed")
    native_path = CAPTURE / "EXPORT-REOPEN.normalized-before-native.v2.json"
    native = read(native_path)
    if native["changed_isolated_importer_meta"] != [str(after_path)] or not native["original_sources_unchanged"]:
        raise RuntimeError("Native verification did not record this exact isolated migration")
    before = flatten(yaml.safe_load(before_path.read_text("utf-8-sig")))
    after = flatten(yaml.safe_load(after_path.read_text("utf-8-sig")))
    additions = {key: after[key] for key in after.keys() - before.keys()}
    deletions = {key: before[key] for key in before.keys() - after.keys()}
    changes = {key: {"before": before[key], "after": after[key]} for key in before.keys() & after.keys() if before[key] != after[key]}
    expected_additions = {
        "ModelImporter.materials.searchTexturesGlobally": 1,
        "ModelImporter.meshes.importPhysicalCameras": 1,
        "ModelImporter.meshes.generateMeshLods": 0,
        "ModelImporter.meshes.meshLodGenerationFlags": 0,
        "ModelImporter.meshes.maximumMeshLod": -1,
        "ModelImporter.meshes.importUVs": -1,
        "ModelImporter.meshes.importVertexColors": 1,
        "ModelImporter.meshes.strictVertexDataChecks": 0,
        "ModelImporter.tangentSpace.calculateBlendshapeNormalsDeltaFromImportedNormals": 1,
        "ModelImporter.importBlendShapeDeformPercent": 0,
    }
    if additions != expected_additions or deletions != {"ModelImporter.animations.rigImportErrors": None, "ModelImporter.animations.rigImportWarnings": None}:
        raise RuntimeError(f"Different migration fields: additions={additions}; deletions={deletions}")
    if changes != {"ModelImporter.serializedVersion": {"before": 21300, "after": 24501}}:
        raise RuntimeError(f"Preexisting importer setting changed: {changes}")
    format_path = CAPTURE / "SOURCE-FORMAT-REOPEN.normalized-before-native.v2.json"
    fmt = read(format_path)
    if not fmt["source_hierarchy_mesh_formats_match"] or fmt["geometry_reopen_sha256"] != sha(native_path):
        raise RuntimeError("Exact native source identity/format verification absent")
    ROOT.mkdir()
    # Keep both exact serialized representations, not an edited synthetic meta.
    (ROOT / "original-source.meta").write_bytes(before_path.read_bytes())
    (ROOT / "isolated-after-unity.meta").write_bytes(after_path.read_bytes())
    proof = {"status": "exact_isolated_importer_migration_recorded", "checked_utc": datetime.datetime.now(datetime.timezone.utc).isoformat(),
             "copy": copy, "isolated_meta_after_sha256": sha(after_path), "added_fields": additions,
             "removed_empty_diagnostics": deletions, "changed_serialization_version": changes,
             "native_geometry_reopening_sha256": sha(native_path), "source_format_reopening_sha256": sha(format_path),
             "prepared_capture_sha256": sha(prepared_path), "review_helper_sha256": sha(Path(__file__)),
             "original_sources_unchanged": True, "preexisting_geometry_settings_unchanged": True,
             "new_field_defaults_equivalent_to_original_unity_version": False,
             "original_importer_settings_parity_qualified": False, "render_parity_qualified": False,
             "scope": "Geometry captured with the explicitly recorded upgraded isolated importer; no original project/importer/render parity inferred",
             "full_import": False}
    (ROOT / "MIGRATION-REOPEN.v1.json").write_text(json.dumps(proof, indent=2, ensure_ascii=False), "utf-8")
    print(json.dumps({"status": proof["status"], "added_fields": len(additions), "changed_existing_fields": len(changes),
                      "original_sources_unchanged": True, "original_importer_settings_parity_qualified": False}))

if __name__ == "__main__":
    main()
