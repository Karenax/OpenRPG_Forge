"""Point the batch tracker at the tested replacement without claiming completion."""
import datetime
import json
import os
from pathlib import Path

W = Path(r"F:\OpenRPG_ImportWork")
ROOT = W / "library-locked-integrity-reader-integration-v1"
T = W / "SUIVI_LOTS_HIVEMIND"

def read(p):
    return json.loads(p.read_text("utf-8-sig"))

def main():
    tests = read(ROOT / "RUN-RESULT.v1.json")
    build = read(ROOT / "BUILD-RESULT.v1.json")
    replacement = read(ROOT / "AUDIT-REPLACEMENT.v1.json")
    audit = read(ROOT / "AUDIT-RUN.v1.json")
    if tests["actual_tests_passed"] != 50 or build["actual_build_exit_code"] != 0 or not replacement["prior_process_termination_confirmed"]:
        raise RuntimeError("Actual tested replacement evidence absent")
    path = T / "ETAT.v1.json"
    before = path.read_bytes()
    state = json.loads(before)
    lot = next(row for row in state["batches"] if row["id"] == "L08")
    lot["locked_native_audit"] = {"status_at_observation": audit["status"],
        "receipt": str(ROOT / "AUDIT-RUN.v1.json"), "job": read(ROOT / "AUDIT-JOB.v1.json"),
        "controller_job": read(ROOT / "AUDIT-CONTROLLER-JOB.v1.json"),
        "actual_reader_tests_passed": 50, "actual_build_exit_code": 0,
        "immutable_executable_sha256": build["executable_sha256"],
        "prior_audit_superseded": True, "prior_audit_pid": replacement["prior_pid"],
        "scope_reduced": False, "actual_ui_visual_review": False, "full_import": False}
    stamp = datetime.datetime.now(datetime.timezone.utc).isoformat()
    state["updated_utc"] = stamp
    temporary = path.with_name(path.name + f".{os.getpid()}.tmp")
    temporary.write_text(json.dumps(state, indent=2, ensure_ascii=False), "utf-8")
    if path.read_bytes() != before:
        raise RuntimeError("Concurrent tracking edit preserved")
    os.replace(temporary, path)
    message = (f"\n## L08 — audit natif avec protection des fichiers partagés — {stamp}\n"
        "Cinquante contrôles du lecteur réussis, exécutable construit avec code 0 puis copie immuable vérifiée. "
        "L'ancien processus d'audit a été remplacé après vérification de son chemin, SHA et date de création ; ses preuves restent conservées. "
        f"Nouvel audit de même portée : {ROOT / 'AUDIT-RUN.v1.json'}, état {audit['status']}. "
        "Un fichier partagé est haché une fois sous verrou de lecture Windows ; les vérifications interactives ordinaires restent fraîches. Aucun verdict intégral ni validation visuelle de Studio revendiqué.\n")
    for target in [T / "JOURNAL.md", T / "PLAN_ET_SUIVI.md", W / "RUN_STATUS.md"]:
        with target.open("a", encoding="utf-8") as out:
            out.write(message)
    print(json.dumps(lot["locked_native_audit"]))

if __name__ == "__main__":
    main()
