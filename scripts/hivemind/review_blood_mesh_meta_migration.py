"""Reopen the exact Blood isolated importer upgrade, retaining original bytes."""
import datetime
import importlib.util
import json
from pathlib import Path
import sys

W = Path(r"F:\OpenRPG_ImportWork")
GUID = sys.argv[1] if len(sys.argv) == 2 else "4f53a4b089ccdea4694e276e175e7035"
if GUID not in {"4f53a4b089ccdea4694e276e175e7035", "2e950722dbb0c35458af3c7c26e64d9c", "0aec518654f551040b523632868a90bf"}:
    raise RuntimeError("Unreviewed Blood mesh identity")
ROOT = W / "blood-mesh-isolated-importer-migration-v1" / GUID
CAPTURE = W / "unity-nested-prefab-static-geometry-capture-v1/realistic-blood-vfx"
HELPER = Path(__file__).with_name("review_native_village_meta_migration.py")

def main():
    if ROOT.exists():
        raise RuntimeError("Existing Blood migration evidence preserved")
    spec = importlib.util.spec_from_file_location("prior_migration_review", HELPER)
    prior = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(prior)
    prepared_path = CAPTURE / "PREPARED.normalized-before-native.v2.json"
    prepared = prior.read(prepared_path)
    selected = [c for c in prepared["copies"] if GUID in c["project_meta_copy"]]
    if len(selected) != 1:
        raise RuntimeError("Exact Blood mesh migration ambiguous")
    copy = selected[0]
    for key, hashkey in [("original", "original_sha256"), ("original_meta", "original_meta_sha256"), ("project_copy", "project_copy_sha256")]:
        if prior.sha(copy[key]) != copy[hashkey]:
            raise RuntimeError("Original source or copied FBX changed")
    native_path = CAPTURE / "EXPORT-REOPEN.normalized-before-native.v2.json"
    format_path = CAPTURE / "SOURCE-FORMAT-REOPEN.normalized-before-native.v2.json"
    native, fmt = prior.read(native_path), prior.read(format_path)
    before_path, after_path = Path(copy["original_meta"]), Path(copy["project_meta_copy"])
    expected_changed = {c["project_meta_copy"] for c in prepared["copies"]}
    if set(native["changed_isolated_importer_meta"]) != expected_changed or str(after_path) not in expected_changed or not native["original_sources_unchanged"]:
        raise RuntimeError("Native proof does not identify exactly this isolated migration")
    if not fmt["source_hierarchy_mesh_formats_match"] or fmt["geometry_reopen_sha256"] != prior.sha(native_path):
        raise RuntimeError("Native source geometry/format proof absent")
    before = prior.flatten(prior.yaml.safe_load(before_path.read_text("utf-8-sig")))
    after = prior.flatten(prior.yaml.safe_load(after_path.read_text("utf-8-sig")))
    added = {k: after[k] for k in after.keys() - before.keys()}
    removed = {k: before[k] for k in before.keys() - after.keys()}
    changes = {k: {"before": before[k], "after": after[k]} for k in before.keys() & after.keys() if before[k] != after[k]}
    allowed = {"ModelImporter.materials.searchTexturesGlobally": 1,
               "ModelImporter.meshes.importPhysicalCameras": 1, "ModelImporter.meshes.generateMeshLods": 0,
               "ModelImporter.meshes.meshLodGenerationFlags": 0, "ModelImporter.meshes.maximumMeshLod": -1,
               "ModelImporter.meshes.importUVs": -1, "ModelImporter.meshes.importVertexColors": 1,
               "ModelImporter.meshes.strictVertexDataChecks": 0,
               "ModelImporter.tangentSpace.calculateBlendshapeNormalsDeltaFromImportedNormals": 1,
               "ModelImporter.importBlendShapeDeformPercent": 0}
    if added != {k: v for k, v in allowed.items() if k not in before}:
        raise RuntimeError(f"Unexpected added importer fields: {added}")
    if removed != {"ModelImporter.animations.rigImportErrors": None, "ModelImporter.animations.rigImportWarnings": None}:
        raise RuntimeError(f"Unexpected removed fields: {removed}")
    if changes != {"ModelImporter.serializedVersion": {"before": 22200, "after": 24501}}:
        raise RuntimeError(f"Preexisting importer setting changed: {changes}")
    pins = {str(p): prior.sha(p) for p in [HELPER, Path(__file__).resolve(), prepared_path, native_path, format_path, before_path, after_path,
                                         Path(copy["original"]), Path(copy["project_copy"])]}
    for path, expected in pins.items():
        if prior.sha(path) != expected:
            raise RuntimeError("Migration input changed during review")
    ROOT.mkdir(parents=True)
    (ROOT / "original-source.meta").write_bytes(before_path.read_bytes())
    (ROOT / "isolated-after-unity.meta").write_bytes(after_path.read_bytes())
    proof = {"status": "exact_isolated_importer_migration_recorded", "checked_utc": datetime.datetime.now(datetime.timezone.utc).isoformat(),
             "copy": copy, "isolated_meta_after_sha256": prior.sha(after_path), "added_fields": added,
             "removed_empty_diagnostics": removed, "changed_serialization_version": changes, "source_pins": pins,
             "original_sources_unchanged": True, "preexisting_geometry_settings_unchanged": True,
             "new_field_defaults_equivalent_to_original_unity_version": False, "original_importer_settings_parity_qualified": False,
             "scope": "Native geometry captured with the recorded upgraded isolated importer; no original importer or render parity inferred",
             "render_parity_qualified": False, "full_import": False}
    (ROOT / "MIGRATION-REOPEN.v1.json").write_text(json.dumps(proof, indent=2), "utf-8")
    print(json.dumps({"status": proof["status"], "added_fields": len(added), "changed_existing_fields": len(changes), "full_import": False}))

if __name__ == "__main__":
    main()
