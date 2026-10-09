"""Publish current bounded receipts without promoting candidates to delivered assets."""
import datetime
import hashlib
import json
import os
from pathlib import Path

W = Path(r"F:\OpenRPG_ImportWork")
T = W / "SUIVI_LOTS_HIVEMIND"
R = Path(r"E:\OpenRPG_Forge\OpenRPG_Forge-Rust")

def read(path):
    return json.loads(Path(path).read_text("utf-8-sig"))

def digest(path):
    with Path(path).open("rb") as source:
        return hashlib.file_digest(source, "sha256").hexdigest()

def main():
    target = T / "ETAT.v1.json"
    before = target.read_bytes()
    state = json.loads(before)
    lots = {row["id"]: row for row in state["batches"]}
    geometry = W / "source-graph-scene-geometry-adapter-v2"
    result = read(geometry / "RUN-RESULT.v1.json")
    stdout = (geometry / "native.stdout.log").read_text("utf-8")
    stderr = (geometry / "native.stderr.log").read_text("utf-8")
    if result["status"] != "refused" or result["native_exit_code"] != 101 or "0 passed; 6 failed" not in stdout or "UnqualifiedNormalDecode" not in stderr:
        raise RuntimeError("Exact scene failure no longer matches; reinspect before recording")
    lots["L01"]["scene_geometry_candidate"] = {"status": "refused_before_geometry_due_to_unqualified_normal_decode", "receipt": str(geometry / "RUN-RESULT.v1.json"), "actual_exit_code": 101, "actual_tests_passed": 0, "actual_tests_failed": 6, "stdout_sha256": digest(geometry / "native.stdout.log"), "stderr_sha256": digest(geometry / "native.stderr.log"), "canonical_installed": False, "new_qualified_prefabs": 0, "next_action": "repair_explicit_normal_bindings_from_original_texture_evidence_before_new_geometry_run"}
    hdrp = W / "unity-dungeon-original-shadergraph-hdrp-context-v3"
    api = read(hdrp / "API-COMPILE.v3.json")
    job = read(hdrp / "JOB.v3.json")
    run = read(hdrp / "RUN-RESULT.v3.json")
    lots["L02"]["hdrp_context_capture"] = {"status_at_observation": run["status"], "receipt": str(hdrp / "RUN-RESULT.v3.json"), "actual_api_compile_exit_code": api["actual_api_compile_exit_code"], "controller_pid": job["controller_pid"], "controller_created_utc": job["created_utc"], "native_unity_launched_at_observation": run["native_unity_launched"], "full_import": False}
    source = R / "scripts/hivemind/candidates/shared_source_integrity_plan.v1.json"
    shared = read(source)
    if shared["conflicting_hash_paths"] or shared["entries"] != 12516:
        raise RuntimeError("Shared source inventory changed")
    # Catalog pin only; this does not rehash thousands of source leaves.
    if digest(shared["catalog_path"]) != shared["catalog_sha256"]:
        raise RuntimeError("Shared source catalog changed")
    destination = T / "L08_FICHIERS_PARTAGES_PLAN.v1.json"
    with destination.open("xb") as output:
        output.write(source.read_bytes())
    lots["L08"]["shared_source_audit_plan"] = {"receipt": str(destination), "entries": shared["entries"], "unique_sources": shared["unique_sources"], "unique_sources_and_meta": shared["unique_sources_and_meta"], "physical_file_integrity_checked": False, "interactive_hash_guards_changed": False}
    normal_path = W / "source-graph-scene-normal-bindings-v2/PLAN.v1.json"
    normal = read(normal_path) if normal_path.exists() else None
    if normal:
        lots["L01"]["normal_binding_conversion_plan"] = {"receipt": str(normal_path), "scenes_inspected": normal["scenes_inspected"], "resolved_reads": normal["normal_reads_resolved"], "unresolved_reads": normal["unresolved_normal_reads"], "glb_modified": False}
    stamp = datetime.datetime.now(datetime.timezone.utc).isoformat()
    state["updated_utc"] = stamp
    temporary = target.with_suffix(f".json.{os.getpid()}.tmp")
    temporary.write_text(json.dumps(state, indent=2, ensure_ascii=False), "utf-8")
    if target.read_bytes() != before:
        temporary.unlink()
        raise RuntimeError("Concurrent tracking update preserved")
    os.replace(temporary, target)
    detail = (f"\n## Sous-lots L01/L02/L08 — {stamp}\n"
              "L01.4 : six tests réellement exécutés, zéro réussi : le lecteur refuse les contrats de normales non qualifiés avant la géométrie. "
              "Aucune relance identique ; réparer les contrats depuis les preuves source. "
              f"Plan : {normal_path} (préparation seulement).\n"
              f"L02 : compilation API HDRP réelle réussie ; contrôle natif en file, reçu {hdrp / 'RUN-RESULT.v3.json'}. "
              "Il attend les contrôleurs Unity antérieurs ; aucun nouveau matériau livré revendiqué.\n"
              f"L08 : 12 516 fiches partagent 3 026 sources / 5 870 chemins source et meta, aucun hash déclaré contradictoire. Plan {destination}. "
              "Le plan ne remplace pas la vérification physique et ne change pas les contrôles interactifs.\n")
    if normal:
        detail += f"L01 : {normal['scenes_inspected']} scènes inspectées ; {normal['normal_reads_resolved']} lectures normales résolues, {normal['unresolved_normal_reads']} encore refusées. Aucun GLB modifié.\n"
    for name in [T / "JOURNAL.md", W / "RUN_STATUS.md"]:
        with name.open("a", encoding="utf-8") as output:
            output.write(detail)
    plan_path = T / "PLAN_ET_SUIVI.md"
    plan_before = plan_path.read_text("utf-8")
    plan = plan_before.replace("son log attend le verrou du répertoire de compilation", "la compilation canonique reste sans verdict terminal dans le reçu relu")
    plan += detail
    if plan_path.read_text("utf-8") != plan_before:
        raise RuntimeError("Concurrent plan update preserved")
    plan_path.write_text(plan, "utf-8")
    print(json.dumps({"updated_utc": stamp, "batches_recorded": ["L01", "L02", "L08"], "new_qualified_assets": 0, "full_import": False}))

if __name__ == "__main__":
    main()
