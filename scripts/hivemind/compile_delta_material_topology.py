"""Compile delta material topology using the existing reader without body edits."""
import hashlib
import json
from pathlib import Path
import subprocess
import sys

W = Path(r"F:\OpenRPG_ImportWork")
ROOT = W / "nested-prefab-delta-material-topology-controller-v1"
READER = Path(__file__).resolve().with_name("compile_nested_material_active_ir.py")


def sha(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def main():
    binding_path = W / "nested-prefab-delta-graph-property-reader-v1/RUN-RESULT.v1.json"
    binding = json.loads(binding_path.read_text("utf-8"))
    if binding["status"] != "delta_graph_property_reader_verified" or binding["exit_code"] != 0:
        raise RuntimeError("Delta graph property closing proof required")
    old_receipt = json.loads((W / "nested-prefab-material-active-ir-v1/RESULT.v1.json").read_text("utf-8"))
    expected = old_receipt["source_pins"][str(READER)]
    if sha(READER) != expected:
        raise RuntimeError("Existing topology reader differs from proven source")
    if ROOT.exists() or (W / "nested-prefab-delta-material-active-ir-v1").exists():
        raise RuntimeError("Previous topology output preserved")
    replacements = [
        ("nested-prefab-material-active-ir-v1", "nested-prefab-delta-material-active-ir-v1"),
        ("nested-prefab-graph-property-bindings-v1/RESULT.v1.json", "nested-prefab-delta-graph-property-bindings-v1/RESULT.v1.json"),
        ("nested-prefab-material-source-ir-v1/RESULT.v1.json", "nested-prefab-delta-material-source-ir-v1/RESULT.v1.json"),
    ]
    original = READER.read_text("utf-8")
    adapted = original
    for before, after in replacements:
        if adapted.count(before) != 1:
            raise RuntimeError("Reader configuration differs")
        adapted = adapted.replace(before, after)
    restored = adapted
    for before, after in reversed(replacements):
        restored = restored.replace(after, before)
    if restored != original:
        raise RuntimeError("Topology reader body changed")
    pins = {str(p): sha(p) for p in [binding_path, READER, Path(__file__).resolve()]}
    compiler = W / "dungeon-original-material-family-closure-v1/compile_source_material_ir.py"
    if sha(compiler) != old_receipt["original_source_compiler_sha256"]:
        raise RuntimeError("Original topology compiler changed")
    pins[str(compiler)] = sha(compiler)
    ROOT.mkdir()
    child = ROOT / READER.name
    child.write_text(adapted, "utf-8")
    pins[str(child)] = sha(child)
    with (ROOT / "stdout.log").open("w", encoding="utf-8") as out, (ROOT / "stderr.log").open("w", encoding="utf-8") as err:
        run = subprocess.run([sys.executable, "-u", str(child)], stdout=out, stderr=err)
    unchanged = all(sha(p) == expected_pin for p, expected_pin in pins.items())
    result_path = W / "nested-prefab-delta-material-active-ir-v1/RESULT.v1.json"
    result = json.loads(result_path.read_text("utf-8")) if result_path.exists() else {}
    completed = (run.returncode == 0 and unchanged and result.get("all_input_pins_unchanged")
                 and result.get("materials") == 25 and result.get("compiled_materials") == 25)
    guard = {"status": "delta_material_active_topology_verified" if completed else "refused_or_partial",
             "exit_code": run.returncode, "all_input_pins_unchanged": unchanged,
             "reader_body_unchanged": True, "configuration_replacements": replacements,
             "materials": result.get("materials"), "compiled_materials": result.get("compiled_materials"),
             "source_pins": pins, "full_import": False, "studio_verified": False}
    (ROOT / "RUN-RESULT.v1.json").write_text(json.dumps(guard, indent=2), "utf-8")
    print(json.dumps({k: v for k, v in guard.items() if k != "source_pins"}))
    if not completed:
        raise RuntimeError("Topology conversion remains partial; retained diagnostics")


if __name__ == "__main__":
    main()
