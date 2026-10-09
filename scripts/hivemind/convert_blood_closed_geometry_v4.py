"""Convert only three Blood meshes with their individually recorded upgrades."""
import importlib.util
import json
from pathlib import Path

W = Path(r"F:\OpenRPG_ImportWork")
ROOT = W / "blood-captured-static-geometry-derivatives-v4"
HELPER = Path(__file__).with_name("convert_closed_nested_geometry.py")
MIGRATIONS = W / "blood-mesh-isolated-importer-migration-v1"
SNAPSHOT = W / "SUIVI_LOTS_HIVEMIND/L03_CAPTURE_DELTA_INPUTS.v2.json"

def main():
    if ROOT.exists():
        raise RuntimeError("Existing Blood geometry bank preserved")
    spec = importlib.util.spec_from_file_location("original_geometry_converter", HELPER)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    proofs = [module.read(MIGRATIONS / guid / "MIGRATION-REOPEN.v1.json") for guid in
              ["4f53a4b089ccdea4694e276e175e7035", "2e950722dbb0c35458af3c7c26e64d9c", "0aec518654f551040b523632868a90bf"]]
    exceptions = {}
    pins = {}
    for proof in proofs:
        if proof["status"] != "exact_isolated_importer_migration_recorded" or not proof["preexisting_geometry_settings_unchanged"]:
            raise RuntimeError("Exact private migration proof absent")
        for path, expected in proof["source_pins"].items():
            module.pin(pins, path, expected)
        copy = proof["copy"]
        exceptions[Path(copy["project_meta_copy"])] = (copy["project_meta_copy_sha256"], proof["isolated_meta_after_sha256"])
    selected = [row for row in module.read(SNAPSHOT) if row["pack_id"] == "hivemind.realistic-blood-vfx"]
    if len(selected) != 1 or selected[0]["requested_meshes"] != 3:
        raise RuntimeError("Blood selection differs")
    selection_path = MIGRATIONS / "GEOMETRY-SELECTION.v4.json"
    if selection_path.exists():
        if module.read(selection_path) != selected:
            raise RuntimeError("Different existing geometry selection")
    else:
        selection_path.write_text(json.dumps(selected, indent=2), "utf-8")
    for path in [HELPER, Path(__file__).resolve(), SNAPSHOT, selection_path]:
        module.pin(pins, path)
    original_pin = module.pin
    used = []
    def exact_pin(target, path, expected=None):
        entry = exceptions.get(Path(path))
        if entry:
            old, new = entry
            if expected != old:
                raise RuntimeError("Unexpected private metadata before-hash")
            used.append(str(path))
            return original_pin(target, path, new)
        return original_pin(target, path, expected)
    module.ROOT, module.RUNS, module.pin = ROOT, selection_path, exact_pin
    module.run()
    if set(used) != {str(path) for path in exceptions} or len(used) != 3:
        raise RuntimeError("Exact private migration exception coverage differs")
    for path, expected in pins.items():
        original_pin({}, path, expected)
    result = module.read(ROOT / "RUN-RESULT.v1.json")
    report = {"status": "blood_three_captured_geometries_with_recorded_importer_upgrades_reopened",
              "meshes_reopened": result["meshes_reopened"], "source_pins": pins,
              "exact_isolated_meta_exceptions": used, "original_importer_settings_parity_qualified": False,
              "previous_80_mesh_derivatives_not_repeated": True,
              "glb_created": False, "studio_verified": False, "full_import": False}
    (ROOT / "MIGRATION-BOUND-REOPEN.v4.json").write_text(json.dumps(report, indent=2), "utf-8")
    print(json.dumps({k: v for k, v in report.items() if k not in {"source_pins", "exact_isolated_meta_exceptions"}}))

if __name__ == "__main__":
    main()
