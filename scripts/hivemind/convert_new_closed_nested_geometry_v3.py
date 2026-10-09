"""Resume the new packs in a fresh bank with one explicitly reopened meta migration."""
import hashlib
import importlib.util
import json
from pathlib import Path

W = Path(r"F:\OpenRPG_ImportWork")
ROOT = W / "nested-prefab-captured-geometry-derivatives-v3"
HELPER = Path(__file__).with_name("convert_closed_nested_geometry.py")
MIGRATION = W / "native-village-isolated-importer-migration-v1/MIGRATION-REOPEN.v1.json"
SNAPSHOT = W / "SUIVI_LOTS_HIVEMIND/L03_CAPTURE_DELTA_INPUTS.v2.json"
PRIOR = W / "nested-prefab-captured-geometry-derivatives-v1/RUN-RESULT.v1.json"
REFUSAL = W / "nested-prefab-captured-geometry-derivatives-v2/RUN-RESULT.v1.json"

def read(p):
    return json.loads(p.read_text("utf-8-sig"))

def sha(p):
    return hashlib.sha256(Path(p).read_bytes()).hexdigest()

def main():
    if ROOT.exists():
        raise RuntimeError("Existing corrected bank preserved")
    proof = read(MIGRATION)
    if proof["status"] != "exact_isolated_importer_migration_recorded" or not proof["original_sources_unchanged"] or not proof["preexisting_geometry_settings_unchanged"]:
        raise RuntimeError("Explicit original/isolated migration reopening absent")
    old = read(PRIOR)
    if old["status"] != "captured_geometry_derivatives_reopened" or not old["original_sources_unchanged"]:
        raise RuntimeError("Prior 116-mesh bank not closed")
    skipped = {row["pack_id"] for row in old["rows"]}
    selected = read(SNAPSHOT)
    if any(row["pack_id"] in skipped for row in selected):
        raise RuntimeError("Prior pack unexpectedly repeated")
    pins = {str(p): sha(p) for p in [MIGRATION, SNAPSHOT, PRIOR, REFUSAL, HELPER, Path(__file__).resolve()]}
    spec = importlib.util.spec_from_file_location("unchanged_nested_converter", HELPER)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    original_pin = module.pin
    exception_path = Path(proof["copy"]["project_meta_copy"])
    old_expected = proof["copy"]["project_meta_copy_sha256"]
    after_expected = proof["isolated_meta_after_sha256"]
    exceptions = []

    def pin_exact_migration(target_pins, path, expected=None):
        if Path(path) == exception_path:
            if expected != old_expected or sha(path) != after_expected:
                raise RuntimeError("Different isolated-meta state; exact migration exception refused")
            exceptions.append(str(path))
            return original_pin(target_pins, path, after_expected)
        return original_pin(target_pins, path, expected)

    module.ROOT = ROOT
    module.RUNS = SNAPSHOT
    module.pin = pin_exact_migration
    module.run()
    if exceptions != [str(exception_path)] or any(sha(Path(p)) != expected for p, expected in pins.items()):
        raise RuntimeError("Migration exception/selection inputs changed; qualification withheld")
    result = read(ROOT / "RUN-RESULT.v1.json")
    report = {"status": "new_captured_geometry_with_explicit_isolated_meta_migration_reopened",
              "new_meshes_reopened": result["meshes_reopened"], "new_packs_reopened": result["packs_reopened"],
              "prior_meshes_not_repeated": old["meshes_reopened"], "source_pins": pins,
              "derivative_receipt_sha256": sha(ROOT / "RUN-RESULT.v1.json"),
              "exact_private_meta_exception": exceptions,
              "scope": proof["scope"], "original_importer_settings_parity_qualified": False,
              "glb_created": False, "studio_verified": False, "full_import": False}
    (ROOT / "DELTA-REOPEN.v3.json").write_text(json.dumps(report, indent=2), "utf-8")
    print(json.dumps({k: v for k, v in report.items() if k != "source_pins"}))

if __name__ == "__main__":
    main()
