"""Record only the completed canonical native/Web texture registration gate."""
import datetime
import hashlib
import json
import os
from pathlib import Path

W = Path(r"F:\OpenRPG_ImportWork")
T = W / "SUIVI_LOTS_HIVEMIND"
ROOT = W / "canonical-captured-texture-glb-integration-v1"

def main():
    receipt = ROOT / "RUN-RESULT.v3.json"
    proof = json.loads(receipt.read_text("utf-8-sig"))
    if proof["status"] != "canonical_captured_texture_glb_contract_native_and_web_verified":
        raise RuntimeError("Terminal native/Web proof required")
    if any(proof[k] != 0 for k in ["contract_tests_exit_code", "native_tests_exit_code", "web_check_exit_code"]):
        raise RuntimeError("Native/Web gate failed")
    if not proof["installed_source_pins_unchanged"]:
        raise RuntimeError("Final source guard absent")
    for name, count in [("contract-tests.v3.log", 13), ("native-registration-tests.v3.log", 3)]:
        if f"{count} passed; 0 failed" not in (ROOT / name).read_text("utf-8-sig"):
            raise RuntimeError("Actual test count absent")
    state_path = T / "ETAT.v1.json"
    before = state_path.read_bytes()
    state = json.loads(before)
    lot = next(row for row in state["batches"] if row["id"] == "L01")
    lot["status"] = "textures_glb_contrat_natif_web_verifies_integration_scene_studio_restante"
    lot["canonical_glb_texture_integration"].update(
        status=proof["status"], compiled_in_canonical_engine=True,
        native_tests_executed=True, actual_native_tests_passed=3,
        native_tests_exit_code=0, web_check_exit_code=0,
        installed_source_pins_unchanged=True, finished_utc=proof["finished_utc"],
        receipt_sha256=hashlib.sha256(receipt.read_bytes()).hexdigest(),
        studio_verified=False, full_import=False)
    stamp = datetime.datetime.now(datetime.timezone.utc).isoformat()
    state["updated_utc"] = stamp
    temporary = state_path.with_name(f"ETAT.v1.{os.getpid()}.tmp")
    temporary.write_text(json.dumps(state, ensure_ascii=False, indent=2), "utf-8")
    if state_path.read_bytes() != before:
        raise RuntimeError("Concurrent state update preserved")
    os.replace(temporary, state_path)
    message = (f"\n## L01.3 — contrat et raccordement textures GLB terminés — {stamp}\n"
               "13 tests de contrat et trois tests natifs réellement réussis ; compilation Web code 0. "
               "Sources installées inchangées au contrôle final. Le chargement des scènes, le rendu, "
               "le placement et la sauvegarde dans Studio restent à vérifier. "
               f"Reçu : `{receipt}`. Aucun nouvel asset complet qualifié.\n")
    for path in [T / "PLAN_ET_SUIVI.md", T / "JOURNAL.md", W / "RUN_STATUS.md"]:
        with path.open("a", encoding="utf-8") as stream:
            stream.write(message)
    print(json.dumps({"recorded": proof["status"], "actual_native_tests_passed": 3,
                      "web_check_exit_code": 0, "full_import": False}))

if __name__ == "__main__":
    main()
