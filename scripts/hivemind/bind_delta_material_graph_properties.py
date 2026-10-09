"""Reuse the explicit blackboard reader for the new 25 source materials."""
import hashlib
import json
from pathlib import Path
import subprocess
import sys

W = Path(r"F:\OpenRPG_ImportWork")
ROOT = W / "nested-prefab-delta-graph-property-reader-v1"
READER = Path(__file__).resolve().with_name("prepare_nested_graph_property_bindings.py")


def sha(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def main():
    receipt_path = W / "nested-prefab-delta-material-preparation-v1/RUN-RESULT.v1.json"
    receipt = json.loads(receipt_path.read_text("utf-8"))
    if receipt["status"] != "delta_prefab_material_sources_reopened" or not receipt["all_input_pins_unchanged"]:
        raise RuntimeError("Closed delta material preparation required")
    if ROOT.exists() or (W / "nested-prefab-delta-graph-property-bindings-v1").exists():
        raise RuntimeError("Existing blackboard evidence preserved")
    expected = "9ddbe47c877f32a3cd9d5510b017d4950d8523f524112331d6c46c81484c6fa5"
    if sha(READER) != expected:
        raise RuntimeError("Original reader changed")
    original = READER.read_text("utf-8")
    replacements = [
        ("nested-prefab-graph-property-bindings-v1", "nested-prefab-delta-graph-property-bindings-v1"),
        ("nested-prefab-material-source-ir-v1/RESULT.v1.json", "nested-prefab-delta-material-source-ir-v1/RESULT.v1.json"),
        ("nested-prefab-material-dependency-inventory-v1/RESULT.v1.json", "nested-prefab-delta-material-dependency-inventory-v1/RESULT.v1.json"),
    ]
    adapted = original
    for before, after in replacements:
        if adapted.count(before) != 1:
            raise RuntimeError("Nonunique configuration replacement")
        adapted = adapted.replace(before, after)
    restored = adapted
    for before, after in reversed(replacements):
        restored = restored.replace(after, before)
    if restored != original:
        raise RuntimeError("Reader body changed")
    pins = {str(p): sha(p) for p in [READER, receipt_path, Path(__file__).resolve()]}
    for name in ["nested-prefab-delta-material-source-ir-v1", "nested-prefab-delta-material-dependency-inventory-v1"]:
        p = W / name / "RESULT.v1.json"
        pins[str(p)] = sha(p)
        result = json.loads(p.read_text("utf-8"))
        for source, expected_source in result["source_pins"].items():
            if sha(source) != expected_source:
                raise RuntimeError(f"Material preparation source changed: {source}")
            pins[source] = expected_source
    ROOT.mkdir()
    child = ROOT / READER.name
    child.write_text(adapted, "utf-8")
    pins[str(child)] = sha(child)
    run = subprocess.run([sys.executable, "-u", str(child)], capture_output=True, text=True)
    (ROOT / "stdout.log").write_text(run.stdout, "utf-8")
    (ROOT / "stderr.log").write_text(run.stderr, "utf-8")
    unchanged = all(sha(p) == expected_pin for p, expected_pin in pins.items())
    guard = {"status": "delta_graph_property_reader_verified" if run.returncode == 0 and unchanged else "refused_or_partial",
             "exit_code": run.returncode, "all_input_pins_unchanged": unchanged,
             "reader_body_unchanged": True, "configuration_replacements": replacements,
             "source_pins": pins, "full_import": False, "studio_verified": False}
    (ROOT / "RUN-RESULT.v1.json").write_text(json.dumps(guard, indent=2), "utf-8")
    if run.returncode != 0 or not unchanged:
        raise RuntimeError("Graph property binding or final guard failed")
    print(run.stdout)


if __name__ == "__main__":
    main()
