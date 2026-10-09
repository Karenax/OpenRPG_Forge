"""Persist the observed intermediate conversion stage and its actual controls."""
import datetime
import hashlib
import json
import os
from pathlib import Path
import subprocess
import sys

W = Path(r"F:\OpenRPG_ImportWork")
ROOT = W / "nested-prefab-captured-geometry-derivatives-v1"
T = W / "SUIVI_LOTS_HIVEMIND"
SCRIPTS = Path(__file__).parent

def read(p):
    return json.loads(p.read_text("utf-8-sig"))

def sha(p):
    return hashlib.sha256(p.read_bytes()).hexdigest()

def main():
    proof_path = ROOT / "REOPENING-CONTROLS.v1.json"
    checks = []
    for script, argument, key in [
        (SCRIPTS / "convert_closed_nested_geometry.py", "self-test", "reopening_controls_passed"),
        (SCRIPTS / "test_closed_nested_geometry.py", None, "additional_reopening_controls_passed")]:
        expected = sha(script)
        command = [sys.executable, str(script)] + ([argument] if argument else [])
        process = subprocess.run(command, capture_output=True, text=True, check=True,
                                 creationflags=subprocess.CREATE_NO_WINDOW)
        if sha(script) != expected:
            raise RuntimeError("Control helper changed during execution")
        output = json.loads(process.stdout)
        checks.append({"command": command, "actual_exit_code": process.returncode,
                       "helper_sha256": expected, "passed": output[key],
                       "stdout": process.stdout, "stderr": process.stderr})
    if sum(len(row["passed"]) for row in checks) != 10:
        raise RuntimeError("Ten actual independent reopening controls absent")
    controls = {"checks": checks, "full_import": False}
    if proof_path.exists():
        if read(proof_path) != controls:
            raise RuntimeError("Existing controls preserved")
    else:
        proof_path.write_text(json.dumps(controls, indent=2), "utf-8")
    receipt = ROOT / "RUN-RESULT.v1.json"
    result = read(receipt)
    if result["meshes_reopened"] != len(result["rows"]):
        raise RuntimeError("Intermediate row count differs")
    observed = {"status": result["status"], "receipt": str(receipt),
                "meshes_with_derivative_streams_reopened": result["meshes_reopened"],
                "pack_stream_batches_processed": result["packs_reopened"],
                "source_pins_final_recheck_completed": result.get("original_sources_unchanged", False),
                "independent_reopening_controls_passed": 10,
                "reopening_controls_receipt": str(proof_path),
                "glb_created": False, "studio_verified": False, "full_import": False}
    state_path = T / "ETAT.v1.json"
    before = state_path.read_bytes()
    state = json.loads(before)
    lot = next(row for row in state["batches"] if row["id"] == "L03")
    lot["captured_geometry_derivatives"] = observed
    stamp = datetime.datetime.now(datetime.timezone.utc).isoformat()
    state["updated_utc"] = stamp
    temp = state_path.with_name(state_path.name + f".{os.getpid()}.tmp")
    temp.write_text(json.dumps(state, indent=2, ensure_ascii=False), "utf-8")
    if state_path.read_bytes() != before:
        raise RuntimeError("Concurrent tracking change preserved")
    os.replace(temp, state_path)
    message = (f"\n## L03 — conversion des géométries capturées — {stamp}\n"
               f"{result['meshes_reopened']} dossiers de flux dérivés réouverts, {result['packs_reopened']} séries de pack traitées ; état {result['status']}. "
               "Convertisseur Dungeon existant réutilisé sans modification (10 contrôles conservés), réouverture indépendante : 10 contrôles réellement réussis. "
               "Normales/tangentes/UV/indices et identités source conservés ; garde finale des sources encore requise si le traitement est actif. "
               f"Reçu {receipt}. Assemblage GLB, matériaux et Studio restants ; aucun nouvel asset runtime qualifié.\n")
    for path in [T / "JOURNAL.md", T / "PLAN_ET_SUIVI.md", W / "RUN_STATUS.md"]:
        with path.open("a", encoding="utf-8") as output:
            output.write(message)
    print(json.dumps(observed))

if __name__ == "__main__":
    main()
