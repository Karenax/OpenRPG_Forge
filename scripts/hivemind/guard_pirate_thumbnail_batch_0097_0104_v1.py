from pathlib import Path
from datetime import datetime,timezone
import json,hashlib,re
W=Path("F:/OpenRPG_ImportWork");CAP=W/"pirate-source-thumbnail-batch-0097-0104-v1";O=W/"pirate-source-thumbnail-batch-0097-0104-source-guard-v1"
pins={}
def read(p):
 raw=Path(p).read_bytes();pins[str(p)]=hashlib.sha256(raw).hexdigest();return json.loads(raw.decode("utf-8-sig"))
def pin(p,h=None):
 raw=Path(p).read_bytes();actual=hashlib.sha256(raw).hexdigest()
 if h is not None:assert actual==h,str(p)
 pins[str(p)]=actual;return raw
assert not O.exists()
run=read(CAP/"RUN-RESULT.v1.json");prep=read(CAP/"PREPARED.v1.json");batch=read(CAP/"NATIVE-RESULT.v1.json")
assert run["actual_exit_code"]==0 and run["source_inputs_unchanged"]
assert pins[str(CAP/"NATIVE-RESULT.v1.json")]==run["native_receipt_sha256"]
assert len(prep["cases"])==8 and [r["id"] for r in prep["cases"]]==[r["id"] for r in batch["cases"]]
# The frozen capture controller performed the complete before/after SHA checks.
# This guard checks the new captures and historical material identities itself.
keyword_guids=set()
for version in range(1,6):
 proof=W/f"pirate-private-material-migrations-reopen-v{version}/RESULT.v1.json"
 if not proof.is_file():continue
 history=read(proof)
 for material in history.get("material_changes",[]):
  if material.get("alpha_to_mask_keyword_moved_to_invalid"):
   meta=Path(material["path"]).with_suffix(".mat.meta")
   text=pin(meta).decode("utf-8-sig");match=re.search(r"^guid: ([a-f0-9]{32})$",text,re.M);assert match
   keyword_guids.add(match.group(1))
refused=[];admitted=[]
for case in batch["cases"]:
 if not case["captured"]:continue
 native=read(case["receipt"]);assert native["id"]==case["id"] and native["source_object_unchanged"] and native["preview_scene_isolated"]
 assert native["pipeline"]=="UnityEngine.Rendering.HighDefinition.HDRenderPipelineAsset" and native["gpu"]!="Null"
 used={t["material"].split(":")[0] for t in native["textures"]}
 for renderer in native["renderers"]:
  for material in renderer.get("materials",[]):
   if isinstance(material,str):used.add(material.split(":")[0])
   elif isinstance(material,dict):used.add(material.get("identity","").split(":")[0])
 for image in native["images"]:pin(Path(image["path"]),image["sha256"])
 if used & keyword_guids:refused.append({"id":case["id"],"stage":"historical_source_alpha_keyword_parity","material_guids":sorted(used & keyword_guids),"reason":"Dedicated original alpha control required"})
 else:admitted.append(case["id"])
pin(Path(__file__));pin(Path("E:/OpenRPG_Forge/OpenRPG_Forge-Rust/scripts/hivemind/queue_house_thumbnail_batch_v1.py"))
O.mkdir();result={"status":"current_capture_source_pins_and_historical_alpha_dependencies_verified","finished_utc":datetime.now(timezone.utc).isoformat(),"lot":"L08.IMAGE.PIRATE.05","source_inputs_unchanged":True,"source_pin_verification_performed_by_capture_controller":True,"inherited_input_pin_count":len(prep["input_pins"]),"capture_controller_receipt_sha256":pins[str(CAP/"RUN-RESULT.v1.json")],"alpha_keyword_changes_not_used_by_admitted_cases":True,"cases_refused_historical_alpha_keyword_change":refused,"admitted_ids":admitted,"input_pins":pins,"source_version_parity_qualified":False,"full_import":False}
(O/"RESULT.v1.json").write_text(json.dumps(result,indent=2),encoding="utf-8");print(json.dumps({"admitted":len(admitted),"alpha_refused":len(refused),"proof":str(O/"RESULT.v1.json")}))
