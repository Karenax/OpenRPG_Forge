"""Record partial native mesh verification and the queued constant-normal proof."""
import datetime
import hashlib
import json
import os
from pathlib import Path

W=Path(r"F:\OpenRPG_ImportWork")
T=W/"SUIVI_LOTS_HIVEMIND"

def read(path):return json.loads(Path(path).read_text("utf-8-sig"))
def sha(path):return hashlib.sha256(Path(path).read_bytes()).hexdigest()

def main():
 partial_path=W/"unity-nested-prefab-static-geometry-capture-v1/medieval-kingdom/PARTIAL-STATIC-SOURCE-REOPEN.v1.json"
 partial=read(partial_path)
 if partial["verified_static_source_meshes"]!=13 or partial["deferred_rig_meshes"]!=1 or partial["geometry_converted"]:raise RuntimeError("Partial source receipt changed")
 state_path=T/"ETAT.v1.json";before=state_path.read_bytes();state=json.loads(before);lots={row["id"]:row for row in state["batches"]}
 lots["L03"]["medieval_static_partial_capture"]={"receipt":str(partial_path),"receipt_sha256":sha(partial_path),"verified_source_meshes":13,"geometry_converted":False,"rig_meshes_deferred_to_L05":1,"native_capture_rerun":False,"full_import":False}
 lots["L05"]["medieval_rig_source"]={"receipt":str(partial_path),"source":partial["deferred"][0],"native_weights_and_bind_matrices_captured":False,"skin_glb_converted":False,"rig_flattened":False,"full_import":False}
 lots["L05"]["status"]="rig_medieval_identifie_capture_complete_et_conversion_skin_restantes"
 probe=W/"unity-builtin-normal-constant-reference-v1";job=read(probe/"JOB.v1.json");run=read(probe/"RUN-RESULT.v1.json")
 lots["L01"]["builtin_constant_normal_probe"]={"receipt":str(probe/"RUN-RESULT.v1.json"),"controller_pid":job["pid"],"controller_created_utc":job["created_utc"],"status_at_observation":run["status"],"native_unity_launched":run["native_unity_launched"],"self_contained_include_receipt":str(probe/"INCLUDE-PATH-REPAIR.v2.json"),"new_qualified_assets":0}
 stamp=datetime.datetime.now(datetime.timezone.utc).isoformat();state["updated_utc"]=stamp
 temporary=state_path.with_suffix(f".json.{os.getpid()}.tmp");temporary.write_text(json.dumps(state,indent=2,ensure_ascii=False),"utf-8")
 if state_path.read_bytes()!=before:raise RuntimeError("Concurrent tracking change preserved")
 os.replace(temporary,state_path)
 text=(f"\n## L03 / L05 — Medieval Kingdom — {stamp}\n"
       f"13 captures statiques réouvertes avec le vérificateur existant inchangé ; sources, formats, GUID/fileID et buffers contrôlés. Reçu : {partial_path}. "
       "La capture complète de 14 reste refusée : SKM_MarketCloth_01 contient un rig (1 bind pose, 1 024 sommets), routé vers L05 sans aplatissement. Aucune nouvelle capture et aucun nouveau GLB livré.\n"
       f"L01 : contrôle GPU de la normale blanche constante en file après les tâches Unity antérieures ; shader original et 7 includes Unity épinglés, chemins seuls adaptés. Reçu : {probe/'RUN-RESULT.v1.json'}. Aucun verdict GPU encore revendiqué.\n")
 for path in [T/"JOURNAL.md",T/"PLAN_ET_SUIVI.md",W/"RUN_STATUS.md"]:
  with path.open("a",encoding="utf-8") as output:output.write(text)
 print(json.dumps({"verified_additional_static_source_meshes":13,"rig_routed_to_L05":1,"new_qualified_runtime_assets":0}))

if __name__=="__main__":main()
