"""Run the proven WGSL emitter against the 25 newly closed material topologies."""
import hashlib
import json
from pathlib import Path
import subprocess
import sys

W = Path(r"F:\OpenRPG_ImportWork")
ROOT = W / "nested-prefab-delta-material-wgsl-controller-v1"
READER = Path(__file__).resolve().with_name("generate_nested_material_wgsl.py")


def sha(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def main():
    topology = W / "nested-prefab-delta-material-topology-controller-v1/RUN-RESULT.v1.json"
    proof = json.loads(topology.read_text("utf-8"))
    if proof["status"] != "delta_material_active_topology_verified" or proof["compiled_materials"] != 25:
        raise RuntimeError("Closed twenty-five-material topology required")
    expected = "1c2cd685c978ed2b922e0b84a5187f5cfc4216e4909dd921fad2377e847b49c9"
    if sha(READER) != expected:
        raise RuntimeError("Original WGSL generator changed")
    if ROOT.exists() or (W / "nested-prefab-delta-material-wgsl-v1").exists():
        raise RuntimeError("Previous WGSL evidence preserved")
    original = READER.read_text("utf-8")
    replacements = [
        ("nested-prefab-material-wgsl-v1", "nested-prefab-delta-material-wgsl-v1"),
        ("nested-prefab-material-active-ir-v1/RESULT.v1.json", "nested-prefab-delta-material-active-ir-v1/RESULT.v1.json"),
        ('batch["compiled_materials"] != 16', 'batch["compiled_materials"] != 25'),
        ("Closed sixteen-material active topology required", "Closed twenty-five-material active topology required"),
        ('"total": 16', '"total": 25'),
        ('"requested_materials": 16', '"requested_materials": 25'),
    ]
    adapted = original
    for before, after in replacements:
        if adapted.count(before) != 1:
            raise RuntimeError("WGSL generator configuration differs")
        adapted = adapted.replace(before, after)
    restored = adapted
    for before, after in reversed(replacements):
        restored = restored.replace(after, before)
    if restored != original:
        raise RuntimeError("WGSL generator body changed")
    pins = {str(p): sha(p) for p in [topology, READER, Path(__file__).resolve()]}
    ROOT.mkdir()
    child = ROOT / READER.name
    child.write_text(adapted, "utf-8")
    pins[str(child)] = sha(child)
    with (ROOT / "stdout.log").open("w", encoding="utf-8") as out, (ROOT / "stderr.log").open("w", encoding="utf-8") as err:
        run = subprocess.run([sys.executable, "-u", str(child)], stdout=out, stderr=err)
    unchanged = all(sha(p) == expected_pin for p, expected_pin in pins.items())
    result_path = W / "nested-prefab-delta-material-wgsl-v1/RESULT.v1.json"
    result = json.loads(result_path.read_text("utf-8")) if result_path.exists() else {}
    closed = (run.returncode == 0 and unchanged and result.get("requested_materials") == 25
              and result.get("original_sources_and_helpers_unchanged"))
    guard = {"status": "delta_wgsl_generator_closed" if closed else "refused_or_partial",
             "exit_code": run.returncode, "all_input_pins_unchanged": unchanged,
             "generator_body_unchanged_except_paths_and_batch_count": True,
             "configuration_replacements": replacements, "source_pins": pins,
             "naga_validated_materials": result.get("naga_validated_materials"),
             "failed_materials": result.get("failed_materials"),
             "all_materials_converted": result.get("failed_materials") == 0 if closed else False,
             "gpu_rendered": False, "studio_verified": False, "full_import": False}
    (ROOT / "RUN-RESULT.v1.json").write_text(json.dumps(guard, indent=2), "utf-8")
    print(json.dumps({k: v for k, v in guard.items() if k != "source_pins"}))
    if not closed:
        raise RuntimeError("WGSL generation did not close; diagnostics retained")


if __name__ == "__main__":
    main()
