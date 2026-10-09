"""Independently close the preserved 80 meshes; never rerun conversion."""
import datetime
import importlib.util
import json
from pathlib import Path

W = Path(r"F:\OpenRPG_ImportWork")
ROOT = W / "nested-prefab-preserved-geometry-reopen-v1"
BANK = W / "nested-prefab-captured-geometry-derivatives-v3"
HELPER = Path(__file__).with_name("convert_closed_nested_geometry.py")
MIGRATION = W / "native-village-isolated-importer-migration-v1/MIGRATION-REOPEN.v1.json"

def main():
    if ROOT.exists():
        raise RuntimeError("Existing preserved-geometry proof retained")
    spec = importlib.util.spec_from_file_location("geometry_reopening", HELPER)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    prior_path = BANK / "RUN-RESULT.v1.json"
    prior = module.read(prior_path)
    if prior["status"] != "refused_or_partial" or prior["meshes_reopened"] != 80 or len(prior["rows"]) != 80:
        raise RuntimeError("Expected preserved 80-mesh bank absent")
    selected = [p for p in module.read(BANK / "INPUTS.v1.json")["closed_packs"]
                if p["pack_id"] in {"hivemind.native-american-village", "hivemind.pirate-island"}]
    if len(selected) != 2 or sum(p["requested_meshes"] for p in selected) != 80:
        raise RuntimeError("Preserved pack selection differs")
    migration = module.read(MIGRATION)
    if migration["status"] != "exact_isolated_importer_migration_recorded" or not migration["original_sources_unchanged"] or not migration["preexisting_geometry_settings_unchanged"]:
        raise RuntimeError("Exact village private-meta proof absent")
    exception = Path(migration["copy"]["project_meta_copy"])
    pins = {}
    for p in [prior_path, BANK / "INPUTS.v1.json", MIGRATION, HELPER, Path(__file__).resolve(), module.CONVERTER, module.CONTROLS]:
        module.pin(pins, p)
    controls = module.read(module.CONTROLS)
    if controls["converter_sha256"] != module.sha(module.CONVERTER) or len(controls["passed_controls"]) != 10:
        raise RuntimeError("Existing exact converter controls absent")
    reopened = []
    used_exception = False
    ROOT.mkdir()
    try:
        for pack in selected:
            if not pack["verified"] or pack["unity_exit_code"] != 0 or pack["independent_verifier_exit_code"] != 0:
                raise RuntimeError("Native pack proof incomplete")
            capture = Path(pack["root"])
            fmt_path = capture / "SOURCE-FORMAT-REOPEN.normalized-before-native.v2.json"
            native_path = capture / "EXPORT-REOPEN.normalized-before-native.v2.json"
            prepared_path = capture / "PREPARED.normalized-before-native.v2.json"
            fmt, native, prepared = map(module.read, [fmt_path, native_path, prepared_path])
            if fmt["pack_id"] != pack["pack_id"] or not fmt["source_hierarchy_mesh_formats_match"] or not fmt["original_sources_unchanged"] or not native["original_sources_unchanged"]:
                raise RuntimeError("Source format/source guards absent")
            module.pin(pins, native_path, fmt["geometry_reopen_sha256"])
            module.pin(pins, fmt_path)
            module.pin(pins, prepared_path)
            manifest_path, export_path = capture / "manifest.json", capture / "export/RESULT.json"
            module.pin(pins, manifest_path, native["manifest_sha256"])
            module.pin(pins, export_path, native["unity_result_sha256"])
            manifest, export = map(module.read, [manifest_path, export_path])
            count = pack["requested_meshes"]
            if not (len(manifest["cases"]) == len(export["cases"]) == len(native["rows"]) == count == fmt["verified_source_meshes"]) or export["failed"] or export["matched"] != count:
                raise RuntimeError("Native geometry count differs")
            for copy in prepared["copies"]:
                for pk, hk in [("original", "original_sha256"), ("original_meta", "original_meta_sha256"), ("project_copy", "project_copy_sha256"), ("project_meta_copy", "project_meta_copy_sha256")]:
                    expected = copy[hk]
                    if Path(copy[pk]) == exception:
                        if expected != migration["copy"]["project_meta_copy_sha256"] or copy != migration["copy"]:
                            raise RuntimeError("Different private meta migration")
                        expected = migration["isolated_meta_after_sha256"]
                        used_exception = True
                    module.pin(pins, copy[pk], expected)
            for ordinal, (request, metadata, source_receipt) in enumerate(zip(manifest["cases"], export["cases"], native["rows"])):
                matching = [r for r in prior["rows"] if r["pack_id"] == pack["pack_id"] and r["capture_ordinal"] == ordinal]
                if len(matching) != 1:
                    raise RuntimeError("Preserved mesh ordinal ambiguous")
                row = matching[0]
                target = BANK / pack["pack_id"] / f"{ordinal:03d}"
                if Path(row["derivative"]) != target or not (row["asset_id"] == request["asset_id"] == metadata["asset_id"] == source_receipt["asset_id"]):
                    raise RuntimeError("Preserved mesh source identity differs")
                module.pin(pins, target / "GEOMETRY.v1.json", row["geometry_receipt_sha256"])
                report = module.read(target / "GEOMETRY.v1.json")
                expected_identity = {"guid": request["expected_guid"], "file_id": request["expected_file_id"]}
                if report["metadata"] != metadata or report["expected_identity"] != expected_identity or row["source_mesh_identity"] != expected_identity or report["converter_sha256"] != controls["converter_sha256"]:
                    raise RuntimeError("Preserved geometry/native metadata identity differs")
                if metadata["bind_poses"] or metadata["blend_shapes"] or report["geometry"]["generated_authored_streams"]:
                    raise RuntimeError("Rig/morph/generated attributes cannot be static closure")
                source = capture / "export" / f"{ordinal:03d}"
                raw = {}
                for name, expected_hash in source_receipt["buffers"].items():
                    module.pin(pins, source / name, expected_hash)
                    raw[name] = (source / name).read_bytes()
                expected_stream_pins = {str(source / name): h for name, h in source_receipt["buffers"].items()}
                if report["source_stream_pins"] != expected_stream_pins:
                    raise RuntimeError("Preserved source-stream pin set differs")
                output = {}
                for name, details in report["derived_streams"].items():
                    module.pin(pins, target / name, details["sha256"])
                    output[name] = (target / name).read_bytes()
                    if len(output[name]) != details["bytes"]:
                        raise RuntimeError("Preserved output length differs")
                checked = module.reopen_streams(raw, output, metadata)
                if checked != row["derived_streams_reopened"] or row["prefab_instance_aliases"] != prepared["inventory_rows"][ordinal]["aliases"]:
                    raise RuntimeError("Preserved attributes or native prefab aliases differ")
                reopened.append(row)
            print(json.dumps({"pack_reopened": pack["pack_id"], "meshes_reopened": len(reopened)}), flush=True)
        if not used_exception:
            raise RuntimeError("Expected exact isolated migration not consumed")
        for path, expected in pins.items():
            if module.sha(path) != expected:
                raise RuntimeError(f"Input changed during reopening: {path}")
        result = {"status": "captured_geometry_derivatives_reopened", "meshes_reopened": len(reopened), "packs_reopened": 2,
                  "rows": reopened, "source_pins": pins, "original_sources_unchanged": True,
                  "converter_rerun": False, "exact_isolated_meta_exception": str(exception),
                  "original_importer_settings_parity_qualified": False, "finished_utc": datetime.datetime.now(datetime.timezone.utc).isoformat(),
                  "glb_created": False, "studio_verified": False, "full_import": False}
    except Exception as error:
        result = {"status": "preserved_geometry_reopening_refused", "meshes_reopened": len(reopened), "error": str(error), "full_import": False}
        (ROOT / "RUN-RESULT.v1.json").write_text(json.dumps(result, indent=2), "utf-8")
        raise
    (ROOT / "RUN-RESULT.v1.json").write_text(json.dumps(result, indent=2), "utf-8")
    print(json.dumps({k: v for k, v in result.items() if k not in {"rows", "source_pins"}}))

if __name__ == "__main__":
    main()
