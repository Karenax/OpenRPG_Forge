"""Run the unchanged numeric comparison using the explicitly reopened native evidence."""
import datetime
import hashlib
import json
from pathlib import Path
import subprocess
import sys

W = Path(r"F:\OpenRPG_ImportWork")
ROOT = W / "blood-imported-texture-sampling-comparison-v4"
PREVIOUS = W / "blood-imported-texture-sampling-comparison-v3/compare.py"
U = W / "unity-blood-raw-mip-sampling-reference-v1"

def sha(path):
    with Path(path).open("rb") as source:
        return hashlib.file_digest(source, "sha256").hexdigest()

def main():
    ROOT.mkdir(exist_ok=False)
    reopen_run = json.loads((U / "REOPEN-RUN.v3.json").read_text("utf-8"))
    if reopen_run["actual_independent_verifier_exit_code"] != 0 or reopen_run["verified_textures"] != 46 or reopen_run["verified_mip_levels"] != 498:
        raise RuntimeError("Native raw capture reopening incomplete")
    text = PREVIOUS.read_text("utf-8")
    text = text.replace("U/'REOPEN.v1.json'", "U/'REOPEN.v3.json'")
    text = text.replace("us['actual_verifier_exit_code']", "load(U/'REOPEN-RUN.v3.json')['actual_independent_verifier_exit_code']")
    text = text.replace("pins={str(p):sha(p) for p in (gp,up,", "pins={str(p):sha(p) for p in (U/'REOPEN-RUN.v3.json',U/'VERSION-NORMALIZATION.v3.json',U/'PREPARED.v3.json',R/'ADAPTATION.v1.json',gp,up,")
    with (ROOT / "compare.py").open("x", encoding="utf-8") as output:
        output.write(text)
    adaptation = {"previous_verifier": str(PREVIOUS), "previous_verifier_sha256": sha(PREVIOUS), "adapted_verifier_sha256": sha(ROOT / "compare.py"), "numeric_comparison_and_tolerance_unchanged": True, "native_capture_rerun": False, "reopening_scope": "captured raw storage only; isolated Unity settings changes explicit", "helper_sha256": sha(Path(__file__))}
    with (ROOT / "ADAPTATION.v1.json").open("x", encoding="utf-8") as output:
        json.dump(adaptation, output, indent=2)
    result = {"started_utc": datetime.datetime.now(datetime.timezone.utc).isoformat(), "full_import": False}
    for name, args in [("controls", ["--controls"]), ("comparison", [])]:
        with (ROOT / (name + ".stdout.log")).open("x", encoding="utf-8") as out, (ROOT / (name + ".stderr.log")).open("x", encoding="utf-8") as err:
            code = subprocess.run([sys.executable, str(ROOT / "compare.py"), *args], cwd=ROOT, stdout=out, stderr=err, check=False).returncode
        result[name + "_exit_code"] = code
        if code:
            result["status"] = "refused"
            break
    else:
        report = json.loads((ROOT / "RESULT.v1.json").read_text("utf-8"))
        result.update(status="captured_raw_storage_sampling_comparison_verified", verified_textures=report["verified_textures"], verified_mip_levels=report["verified_mip_levels"], maximum_abs_rgba_difference=report["maximum_abs_rgba_difference"], original_asset_database_texture_sampling=False, material_graph_render_parity_qualified=False)
    result["finished_utc"] = datetime.datetime.now(datetime.timezone.utc).isoformat()
    with (ROOT / "RUN-RESULT.v1.json").open("x", encoding="utf-8") as output:
        json.dump(result, output, indent=2)
    print(json.dumps(result))
    raise SystemExit(1 if result["status"] == "refused" else 0)

if __name__ == "__main__":
    main()
