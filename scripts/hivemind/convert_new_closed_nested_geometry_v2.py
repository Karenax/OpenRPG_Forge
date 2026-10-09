"""Convert only newly closed packs, preserving the completed 116-mesh bank."""
import datetime
import hashlib
import importlib.util
import json
from pathlib import Path
import sys

W = Path(r"F:\OpenRPG_ImportWork")
ROOT = W / "nested-prefab-captured-geometry-derivatives-v2"
PRIOR = W / "nested-prefab-captured-geometry-derivatives-v1/RUN-RESULT.v1.json"
SNAPSHOT = W / "SUIVI_LOTS_HIVEMIND/L03_CAPTURE_DELTA_INPUTS.v2.json"
HELPER = Path(__file__).with_name("convert_closed_nested_geometry.py")

def read(p):
    return json.loads(p.read_text("utf-8-sig"))

def sha(p):
    return hashlib.sha256(p.read_bytes()).hexdigest()

def main():
    if ROOT.exists() or SNAPSHOT.exists():
        raise RuntimeError("Existing delta bank retained; inspect its exact process")
    prior = read(PRIOR)
    if prior["status"] != "captured_geometry_derivatives_reopened" or not prior["original_sources_unchanged"]:
        raise RuntimeError("Prior bank not closed")
    prior_sha = sha(PRIOR)
    completed = {row["pack_id"] for row in prior["rows"]}
    current = read(W / "unity-nested-prefab-static-geometry-remaining-v2/PROJECT-CAPTURE-RUNS.v3.json")
    selected = [row for row in current if row.get("verified") and row["pack_id"] not in completed]
    if not selected:
        raise RuntimeError("No new closed pack; old packs not repeated")
    for row in selected:
        if row["unity_exit_code"] or row["independent_verifier_exit_code"]:
            raise RuntimeError("Incomplete source capture refused")
    SNAPSHOT.write_text(json.dumps(selected, indent=2), "utf-8")
    pins = {str(p): sha(p) for p in [PRIOR, SNAPSHOT, HELPER, Path(__file__).resolve()]}
    spec = importlib.util.spec_from_file_location("unchanged_nested_geometry_converter", HELPER)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    module.ROOT = ROOT
    module.RUNS = SNAPSHOT
    module.run()
    if any(sha(Path(p)) != expected for p, expected in pins.items()):
        raise RuntimeError("Delta selection/parent/helper changed; delta qualification withheld")
    result = read(ROOT / "RUN-RESULT.v1.json")
    if {row["pack_id"] for row in result["rows"]} & completed:
        raise RuntimeError("Prior pack unexpectedly repeated")
    report = {"status": "new_closed_pack_geometry_delta_reopened", "checked_utc": datetime.datetime.now(datetime.timezone.utc).isoformat(),
              "prior_bank_sha256": prior_sha, "prior_meshes_not_repeated": prior["meshes_reopened"],
              "new_meshes_reopened": result["meshes_reopened"], "new_packs_reopened": result["packs_reopened"],
              "source_pins": pins, "derivative_bank_receipt_sha256": sha(ROOT / "RUN-RESULT.v1.json"),
              "glb_created": False, "studio_verified": False, "full_import": False}
    (ROOT / "DELTA-REOPEN.v2.json").write_text(json.dumps(report, indent=2), "utf-8")
    print(json.dumps({k: v for k, v in report.items() if k != "source_pins"}))

if __name__ == "__main__":
    main()
