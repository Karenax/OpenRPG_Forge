"""Prepare material dependencies for the eleven newly closed prefab bindings.

Reuse the proven readers with only their input/output paths changed. Never
modify the pinned original helpers or reprocess the previous 32 prefabs.
"""
import datetime
import hashlib
import json
from pathlib import Path
import subprocess
import sys

W = Path(r"F:\OpenRPG_ImportWork")
ROOT = W / "nested-prefab-delta-material-preparation-v1"
HERE = Path(__file__).resolve().parent


def sha(path):
    digest = hashlib.sha256()
    with Path(path).open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def read(path):
    return json.loads(Path(path).read_text("utf-8-sig"))


def write(path, value):
    path.write_text(json.dumps(value, ensure_ascii=False, indent=2), "utf-8")


def main():
    if ROOT.exists():
        raise RuntimeError("Previous delta preparation preserved")
    plan_path = W / "nested-prefab-scene-bindings-v2/RESULT.v1.json"
    reopen_path = plan_path.parent / "SOURCE-REOPEN.v1.json"
    plan, reopen = read(plan_path), read(reopen_path)
    if (reopen["status"] != "selected_prefab_hierarchy_and_geometry_bindings_reopened"
            or sha(plan_path) != reopen["plan_sha256"] or plan["prefabs"] != 11
            or not reopen["source_and_assembly_pins_unchanged"]
            or not reopen["assembly_controls_passed"]
            or reopen["actual_controls_exit_code"] != 0):
        raise RuntimeError("Closed eleven-prefab plan required")
    pins = {str(p): sha(p) for p in [plan_path, reopen_path, Path(__file__).resolve()]}
    for p, expected in plan["source_pins"].items():
        if sha(p) != expected:
            raise RuntimeError(f"Closed prefab source changed: {p}")
        pins[p] = expected
    configurations = [
        ("inventory_nested_material_dependencies.py",
         "c7785d3fe6af85b045be2803338d608308c209c4d19096b70a6f22db2f2324bd",
         [("nested-prefab-material-dependency-inventory-v1", "nested-prefab-delta-material-dependency-inventory-v1"),
          ("nested-prefab-scene-bindings-v1/RESULT.v1.json", "nested-prefab-scene-bindings-v2/RESULT.v1.json")]),
        ("prepare_nested_material_source_ir.py",
         "9b747d63d093908ca3b51dd55bf18e56d289878c6255ea6c252ed02e624e9046",
         [("nested-prefab-material-source-ir-v1", "nested-prefab-delta-material-source-ir-v1"),
          ("nested-prefab-material-dependency-inventory-v1/RESULT.v1.json", "nested-prefab-delta-material-dependency-inventory-v1/RESULT.v1.json")]),
    ]
    prepared = []
    for name, expected, replacements in configurations:
        original_path = HERE / name
        if sha(original_path) != expected:
            raise RuntimeError(f"Original reader changed: {name}")
        pins[str(original_path)] = expected
        original = original_path.read_text("utf-8")
        adapted = original
        for before, after in replacements:
            if adapted.count(before) != 1:
                raise RuntimeError("Configuration replacement is not unique")
            adapted = adapted.replace(before, after)
        restored = adapted
        for before, after in reversed(replacements):
            restored = restored.replace(after, before)
        if restored != original:
            raise RuntimeError("Reader body changed")
        compile(adapted, name, "exec")
        prepared.append((name, adapted, replacements))
    for output in ["nested-prefab-delta-material-dependency-inventory-v1", "nested-prefab-delta-material-source-ir-v1"]:
        if (W / output).exists():
            raise RuntimeError("Existing delta output preserved")
    ROOT.mkdir()
    result = {"status": "preparing_delta_material_sources", "prefabs": 11,
              "started_utc": datetime.datetime.now(datetime.timezone.utc).isoformat(),
              "steps": [], "source_pins": pins, "previous_prefabs_reprocessed": False,
              "material_programs_qualified": False, "studio_verified": False, "full_import": False}
    receipt = ROOT / "RUN-RESULT.v1.json"
    write(receipt, result)
    try:
        for name, adapted, replacements in prepared:
            child = ROOT / name
            child.write_text(adapted, "utf-8")
            pins[str(child)] = sha(child)
            run = subprocess.run([sys.executable, "-u", str(child)], capture_output=True, text=True)
            (ROOT / (name + ".stdout.log")).write_text(run.stdout, "utf-8")
            (ROOT / (name + ".stderr.log")).write_text(run.stderr, "utf-8")
            result["steps"].append({"reader": name, "exit_code": run.returncode,
                                    "configuration_replacements": replacements,
                                    "reader_body_unchanged": True})
            write(receipt, result)
            if run.returncode != 0:
                raise RuntimeError(f"Delta material reader failed: {name}")
        inventory_path = W / "nested-prefab-delta-material-dependency-inventory-v1/RESULT.v1.json"
        typed_path = W / "nested-prefab-delta-material-source-ir-v1/RESULT.v1.json"
        inventory, typed = read(inventory_path), read(typed_path)
        if not inventory["all_input_pins_unchanged"] or not typed["all_input_pins_unchanged"]:
            raise RuntimeError("Material reader final source guard absent")
        if inventory["materials"] != typed["materials"]:
            raise RuntimeError("Material identity count differs")
        for child_result in [inventory, typed]:
            pins.update(child_result["source_pins"])
        for path in [inventory_path, typed_path]:
            pins[str(path)] = sha(path)
        for path, expected in pins.items():
            if sha(path) != expected:
                raise RuntimeError(f"Delta material input changed: {path}")
        result.update(status="delta_prefab_material_sources_reopened",
                      materials=typed["materials"],
                      unique_payloads_and_meta_reopened=inventory["unique_payloads_and_meta_reopened"],
                      unresolved_serialized_rendering_guids=typed["unique_unresolved_rendering_guids"],
                      materials_with_unresolved_serialized_rendering_references=typed["materials_with_unresolved_serialized_rendering_references"],
                      all_input_pins_unchanged=True,
                      finished_utc=datetime.datetime.now(datetime.timezone.utc).isoformat())
        write(receipt, result)
        print(json.dumps({k: v for k, v in result.items() if k != "source_pins"}))
    except Exception as error:
        result.update(status="refused_or_partial", error=str(error))
        write(receipt, result)
        raise


if __name__ == "__main__":
    main()
