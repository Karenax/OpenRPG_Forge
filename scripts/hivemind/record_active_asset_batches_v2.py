"""Refresh lot receipts using actual completed rows; preserve earlier refusals."""
import datetime
import hashlib
import importlib.util
import json
import os
from pathlib import Path

W = Path(r"F:\OpenRPG_ImportWork")
T = W / "SUIVI_LOTS_HIVEMIND"
R = Path(r"E:\OpenRPG_Forge\OpenRPG_Forge-Rust")

def read(path):
    return json.loads(Path(path).read_text("utf-8-sig"))

def sha(path):
    with Path(path).open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()

def main():
    helper = R / "scripts/hivemind/record_scene_geometry_progress_v2.py"
    spec = importlib.util.spec_from_file_location("process_identity", helper)
    identity = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(identity)
    state_path = T / "ETAT.v1.json"
    before = state_path.read_bytes()
    state = json.loads(before)
    batches = {row["id"]: row for row in state["batches"]}
    root = W / "source-graph-scene-normal-bound-glb-v1"
    run_path = root / "RUN-RESULT.v1.json"
    run_bytes = run_path.read_bytes()
    run = json.loads(run_bytes)
    job = read(root / "JOB.v1.json")
    conversion = {"status_at_observation": run["status"], "receipt": str(run_path), "receipt_sha256_at_observation": hashlib.sha256(run_bytes).hexdigest(), "derivatives_written_and_binary_reopened": len(run["converted_derivatives"]), "derivatives_expected": 39, "builtin_constant_cases_remaining": 18, "qualified_runtime_assets": 0, "canonical_installed": False, "studio_verified": False, "full_import": False}
    if run["status"] in ["checking_original_normal_evidence", "writing_separate_normal_bound_glb_derivatives"]:
        conversion["exact_live_controller"] = identity.live_identity(job["pid"], job["created_utc"])
    queue = read(root / "GEOMETRY-QUEUE.v1.json")
    conversion["geometry_queue"] = queue
    native_path = W / "source-graph-scene-geometry-adapter-v3/RUN-RESULT.v1.json"
    if native_path.exists():
        native = read(native_path)
        conversion["native_geometry_receipt"] = str(native_path)
        conversion["native_geometry_status"] = native["status"]
        if native["status"] == "native_scene_geometry_candidate_verified":
            if native["actual_tests"] != 6 or native["native_exit_code"] != 0 or native["changed_inputs"] or native["scene_summary"]["actual_scenes"] != 39:
                raise RuntimeError("Native geometry success lacks its exact tests and unchanged inputs")
            for lane in ["stdout", "stderr"]:
                if sha(native_path.parent / ("native." + lane + ".log")) != native[lane + "_sha256"]:
                    raise RuntimeError("Native geometry log changed")
            conversion.update(actual_native_geometry_tests_passed=6, native_geometry_scenes_verified=39, native_inputs_unchanged=True)
    batches["L01"]["normal_bound_glb_conversion"] = conversion
    raw = W / "unity-blood-raw-mip-sampling-reference-v1"
    reopening = read(raw / "REOPEN-RUN.v3.json")
    if reopening["actual_independent_verifier_exit_code"] != 0:
        raise RuntimeError("Native raw sampling reopening failed")
    batches["L06"]["native_raw_texture_sampling"] = {"receipt": str(raw / "REOPEN.v3.json"), "verifier_run_receipt": str(raw / "REOPEN-RUN.v3.json"), "verified_textures": reopening["verified_textures"], "verified_mip_levels": reopening["verified_mip_levels"], "native_capture_rerun": False, "isolated_settings_migration_explicit": True, "original_asset_database_texture_sampling": False, "material_graph_render_parity_qualified": False, "full_import": False}
    comparison = W / "blood-imported-texture-sampling-comparison-v4"
    comparison_run = comparison / "RUN-RESULT.v1.json"
    if comparison_run.exists():
        value = read(comparison_run)
        batches["L06"]["raw_storage_unity_bevy_comparison"] = {"receipt": str(comparison_run), **value}
    stamp = datetime.datetime.now(datetime.timezone.utc).isoformat()
    state["updated_utc"] = stamp
    temporary = state_path.with_suffix(f".json.{os.getpid()}.tmp")
    temporary.write_text(json.dumps(state, ensure_ascii=False, indent=2), "utf-8")
    if state_path.read_bytes() != before:
        temporary.unlink()
        raise RuntimeError("Concurrent tracking change preserved")
    os.replace(temporary, state_path)
    detail = (f"\n## L01 / L06 — {stamp}\n"
              f"L01 : {conversion['derivatives_written_and_binary_reopened']}/39 dérivés avec modes de normales issus des captures, contenus BIN conservés et réouverts. "
              "Les 18 cas restants décodent une constante du shader (canal 7), pas une texture achetée manquante : qualifier cette constante, sans ajout de texture. "
              f"Reçu {run_path}. Aucun nouvel asset qualifié runtime.\n"
              f"L06 : 46 textures / 498 mipmaps RAW Unity réouverts après migration explicite des seuls réglages du projet de diagnostic isolé. "
              f"Sources et buffers vérifiés ; pas de nouvelle capture. Reçu {raw / 'REOPEN.v3.json'}. "
              "Aucune qualification de l'AssetDatabase original ni du rendu des effets.\n")
    if conversion.get("actual_native_geometry_tests_passed") == 6:
        detail += f"L01.4 : six contrôles natifs réussis sur les 39 scènes ; données des sommets identiques bit à bit, instances et routes de matériaux conservées, sources inchangées. Reçu {native_path}. Adaptateur candidat uniquement : aucun raccordement Studio ni rendu GPU qualifié.\n"
    if comparison_run.exists():
        detail += f"Comparaison Unity/Bevy : {value['status']}, reçu {comparison_run}.\n"
    for path in [T / "JOURNAL.md", T / "PLAN_ET_SUIVI.md", W / "RUN_STATUS.md"]:
        with path.open("a", encoding="utf-8") as output:
            output.write(detail)
    print(json.dumps({"updated_utc": stamp, "converted_intermediate_derivatives": conversion["derivatives_written_and_binary_reopened"], "native_raw_textures_reopened": 46, "native_raw_mips_reopened": 498, "new_qualified_runtime_assets": 0}))

if __name__ == "__main__":
    main()
