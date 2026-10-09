"""Assemble only the newly closed 83 geometries using the unchanged binder."""
import importlib.util
import json
from pathlib import Path
import subprocess
import sys

W = Path(r"F:\OpenRPG_ImportWork")
ROOT = W / "nested-prefab-scene-bindings-v2"
UNION = W / "nested-prefab-closed-geometry-selection-v4"
HELPER = Path(__file__).with_name("assemble_nested_prefab_bindings.py")

def main():
    if ROOT.exists() or UNION.exists():
        raise RuntimeError("Existing next-batch evidence preserved")
    spec = importlib.util.spec_from_file_location("unchanged_prefab_binder", HELPER)
    binder = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(binder)
    inputs = [W / "nested-prefab-preserved-geometry-reopen-v1/RUN-RESULT.v1.json",
              W / "blood-captured-static-geometry-derivatives-v4/RUN-RESULT.v1.json"]
    pins = {str(p): binder.sha(p) for p in [HELPER, Path(__file__).resolve(), *inputs]}
    rows = []
    for path in inputs:
        bank = binder.read(path)
        if bank["status"] != "captured_geometry_derivatives_reopened" or not bank["original_sources_unchanged"]:
            raise RuntimeError("Closed geometry bank absent")
        for p, expected in bank["source_pins"].items():
            if p in pins and pins[p] != expected:
                raise RuntimeError("Conflicting source pin between banks")
            if binder.sha(p) != expected:
                raise RuntimeError("Closed bank source changed")
            pins[p] = expected
        rows.extend(bank["rows"])
    if len(rows) != 83 or len({(r["pack_id"], r["source_mesh_identity"]["guid"], r["source_mesh_identity"]["file_id"]) for r in rows}) != 83:
        raise RuntimeError("Duplicate or incomplete new geometry selection")
    blood_guard = W / "blood-captured-static-geometry-derivatives-v4/MIGRATION-BOUND-REOPEN.v4.json"
    guard = binder.read(blood_guard)
    if guard["meshes_reopened"] != 3:
        raise RuntimeError("Blood migration-bound reopening absent")
    pins[str(blood_guard)] = binder.sha(blood_guard)
    for p, expected in guard["source_pins"].items():
        if binder.sha(p) != expected:
            raise RuntimeError("Blood private migration source changed")
        if p in pins and pins[p] != expected:
            raise RuntimeError("Conflicting Blood proof pin")
        pins[p] = expected
    UNION.mkdir()
    selection = {"status": "captured_geometry_derivatives_reopened", "meshes_reopened": 83,
                 "packs_reopened": 3, "rows": rows, "source_pins": pins,
                 "original_sources_unchanged": True, "glb_created": False, "full_import": False}
    (UNION / "RUN-RESULT.v1.json").write_text(json.dumps(selection, indent=2), "utf-8")
    binder.GEOMETRY, binder.ROOT = UNION, ROOT
    binder.run()
    plan_path = ROOT / "RESULT.v1.json"
    plan = binder.read(plan_path)
    if plan["errors"]:
        raise RuntimeError("Unresolved native binding retained in new assembly receipt")
    controls = subprocess.run([sys.executable, str(HELPER), "self-test"], capture_output=True, text=True,
                              check=True, creationflags=subprocess.CREATE_NO_WINDOW)
    passed = json.loads(controls.stdout)["assembly_controls_passed"]
    if len(passed) != 7:
        raise RuntimeError("Seven actual binding controls absent")
    hierarchy_path = W / "hivemind-prefab-instance-source-closure-v1/HIERARCHY-REOPEN.v1.json"
    hierarchy = binder.read(hierarchy_path)
    slugs = {r["pack_id"].removeprefix("hivemind.") for r in rows}
    dependencies = {}
    for p, expected in hierarchy["proof_pins"].items():
        normalized = p.replace("\\", "/")
        if any(f"/{slug}/" in normalized or f"/{slug}." in normalized for slug in slugs):
            if binder.sha(p) != expected:
                raise RuntimeError("Original prefab hierarchy dependency changed")
            dependencies[p] = expected
    if not dependencies:
        raise RuntimeError("No original prefab source dependencies")
    for p, expected in {**pins, **plan["source_pins"], **dependencies}.items():
        if binder.sha(p) != expected:
            raise RuntimeError("Input changed during next-batch assembly")
    result = {"status": "selected_prefab_hierarchy_and_geometry_bindings_reopened",
              "plan_sha256": binder.sha(plan_path), "prefabs": plan["prefabs"],
              "static_renderer_bindings": plan["static_renderer_bindings"],
              "deferred_nonstatic_renderers": plan["deferred_nonstatic_renderers"],
              "source_dependency_pins": dependencies, "source_and_assembly_pins_unchanged": True,
              "assembly_controls_passed": passed, "actual_controls_exit_code": controls.returncode,
              "helper_sha256": pins[str(HELPER)], "prior_32_prefabs_not_repeated": True,
              "material_programs_qualified": False, "glb_created": False, "studio_verified": False, "full_import": False}
    (ROOT / "SOURCE-REOPEN.v1.json").write_text(json.dumps(result, indent=2), "utf-8")
    print(json.dumps({k: v for k, v in result.items() if k not in {"source_dependency_pins", "assembly_controls_passed"}}))

if __name__ == "__main__":
    main()
