"""Reuse the unchanged normal reader on the verified native delivery inventory."""
import hashlib
import importlib.util
import json
from pathlib import Path

W = Path(r"F:\OpenRPG_ImportWork")
PLAN_ROOT = W / "villa-native-scene-delivery-plan-v1"
HELPER = Path(__file__).with_name("prepare_scene_normal_bindings.py")


def sha(p):
    with Path(p).open("rb") as f:
        return hashlib.file_digest(f, "sha256").hexdigest()


def main():
    plan = json.loads((PLAN_ROOT / "PLAN.v1.json").read_text("utf-8-sig"))
    if plan["counts"]["native_scenes_inspected"] != 226:
        raise RuntimeError("Native scene plan coverage differs")
    for path, expected in plan["input_pins"].items():
        if sha(path) != expected:
            raise RuntimeError("Native plan input changed")
    if sha(HELPER) != "7fccff179b0aa69a83c107ab179052fffd986ec162a0371cd17adfae6fd3a332":
        raise RuntimeError("Normal reader identity changed")
    inputs = PLAN_ROOT / "INPUTS.v1.json"
    if inputs.exists():
        raise RuntimeError("Previous native normal input preserved")
    inputs.write_text(json.dumps([{"asset_id": r["asset_id"], "glb": r["glb"], "sha256": r["expected_glb_sha256"]}
                                 for r in plan["rows"]], indent=2), "utf-8")
    spec = importlib.util.spec_from_file_location("original_normal_reader", HELPER)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    module.ROOT = W / "villa-native-scene-normal-bindings-v1"
    module.GEOMETRY = PLAN_ROOT
    module.main()
    result_path = module.ROOT / "PLAN.v1.json"
    result = json.loads(result_path.read_text("utf-8"))
    result["input_pins"][str(Path(__file__).resolve())] = sha(Path(__file__).resolve())
    result["input_pins"][str(PLAN_ROOT / "PLAN.v1.json")] = sha(PLAN_ROOT / "PLAN.v1.json")
    result["geometry_source_is_native_captured_batch"] = True
    result["prior_geometry_was_not_reconverted"] = True
    result_path.write_text(json.dumps(result, indent=2), "utf-8")


if __name__ == "__main__":
    main()
