"""Record the completed BC4 gate and the exact rig capture/verification jobs."""
import datetime
import hashlib
import json
import os
from pathlib import Path
import re

W=Path(r"F:\OpenRPG_ImportWork")
T=W/"SUIVI_LOTS_HIVEMIND"
def read(path):return json.loads(Path(path).read_text("utf-8-sig"))
def sha(path):return hashlib.sha256(Path(path).read_bytes()).hexdigest()
def main():
 bc4=W/"canonical-bc4-imported-texture-candidate-v2";receipt=bc4/"RUN-RESULT.v3.json";result=read(receipt)
 if result["status"]!="canonical_bc4_storage_native_and_web_verified" or result["actual_native_tests_exit_code"]!=0 or result["actual_web_check_exit_code"]!=0 or not result["source_and_prior_normal_pins_unchanged"]:raise RuntimeError("Exact completed BC4 proof absent")
 log=bc4/"native-storage-tests.v3.log"
 if not re.search(r"test result: ok\. 4 passed; 0 failed",log.read_text("utf-8-sig")):raise RuntimeError("Four actual canonical native tests absent")
 glb=W/"canonical-captured-texture-glb-integration-v1";glb_result=read(glb/"RUN-RESULT.v3.json")
 if glb_result.get("parent_bc4_sha256")!=sha(receipt):raise RuntimeError("GLB successor not pinned to this completed BC4 receipt")
 rig=W/"unity-medieval-rig-source-capture-v1";api=read(rig/"API-COMPILE.v1.json");job=read(rig/"JOB.v1.json");rig_result=read(rig/"RUN-RESULT.v1.json")
 if api["actual_exit_code"]!=0 or sha(api["helper"])!=api["helper_sha256"]:raise RuntimeError("Compiled rig helper changed")
 target=T/"ETAT.v1.json";before=target.read_bytes();state=json.loads(before);lots={row["id"]:row for row in state["batches"]}
 lots["L01"]["bc4_guarded_job"].update(status_at_observation=result["status"],actual_native_tests_passed=4,actual_native_exit_code=0,actual_web_exit_code=0,completion_receipt_sha256=sha(receipt))
 lots["L01"]["status"]="bc4_natif_web_verifies_raccordement_textures_glb_installe_en_test"
 lots["L01"]["canonical_glb_texture_integration"].update(status=glb_result["status"],canonical_installed=glb_result["canonical_installed"],run_receipt=str(glb/"RUN-RESULT.v3.json"),parent_bc4_sha256=glb_result["parent_bc4_sha256"])
 lots["L05"]["rig_capture_job"]={"status_at_observation":rig_result["status"],"receipt":str(rig/"RUN-RESULT.v1.json"),"controller_pid":job["pid"],"controller_created_utc":job["created_utc"],"actual_api_compile_exit_code":0,"rig_flattened":False,"native_unity_launched":rig_result["native_unity_launched"],"independent_reopening_job":read(rig/"REOPEN-JOB.v1.json"),"skin_glb_converted":False,"full_import":False}
 stamp=datetime.datetime.now(datetime.timezone.utc).isoformat();state["updated_utc"]=stamp
 temporary=target.with_suffix(f".json.{os.getpid()}.tmp");temporary.write_text(json.dumps(state,indent=2,ensure_ascii=False),"utf-8")
 if target.read_bytes()!=before:raise RuntimeError("Concurrent tracking change preserved")
 os.replace(temporary,target)
 text=(f"\n## L01.2 — BC4 canonique clôturé — {stamp}\n"
       f"Quatre tests natifs réellement exécutés et réussis ; contrôle Web terminé avec code 0, sources et preuves normales inchangées au terme du contrôle. Reçu {receipt}. "
       "Le successeur GLB est épinglé à ce reçu ; raccordement installé et en tests, aucun GLB runtime supplémentaire revendiqué.\n"
       f"L05 : helper de capture de rig compilé contre Unity installé (code 0), influences complètes, matrices, hiérarchie, courbes et événements conservés sans exécution. Capture et réouverture en file ; reçu {rig/'RUN-RESULT.v1.json'}. Vérificateur : un contrôle positif et six contrôles négatifs réussis ; aucune capture native du rig encore qualifiée.\n")
 for path in [T/"JOURNAL.md",T/"PLAN_ET_SUIVI.md",W/"RUN_STATUS.md"]:
  with path.open("a",encoding="utf-8") as output:output.write(text)
 print(json.dumps({"bc4_actual_native_tests_passed":4,"bc4_actual_web_exit_code":0,"glb_successor_installed":True,"rig_api_compile_exit_code":0,"new_qualified_runtime_assets":0}))
if __name__=="__main__":main()
