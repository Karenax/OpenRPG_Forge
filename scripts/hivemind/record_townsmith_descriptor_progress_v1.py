from pathlib import Path
from datetime import datetime, timezone
import json, hashlib
T=Path("F:/OpenRPG_ImportWork/SUIVI_LOTS_HIVEMIND")
P=T.parent/"townsmith-descriptor-slot-controls-reopen-v2/DESCRIPTOR-RESULT.v1.json"
proof=json.loads(P.read_text())
assert proof["descriptors_verified"]==44
p=T/"PLAN_EXECUTION_LOTS_20261009.v1.json"
data=json.loads(p.read_text(encoding="utf-8"))
batch="L03.TOWNSMITH.DESSINS.DESCRIPTEURS"
assert not any(r["id"]==batch for r in data["finished_batches"])
data["active_batch"]=batch
data["updated_utc"]=datetime.now(timezone.utc).isoformat()
data["next_action"]="Isoler clone complet et passes d ombres source ; verifier ensuite les affectations converties dans Forge"
data["script"]=str(Path(__file__).with_name("reopen_townsmith_descriptor_controls_v2.py"))
data["script_sha256"]=hashlib.sha256(Path(data["script"]).read_bytes()).hexdigest()
data["finished_batches"].append({"id":batch,"proof":str(P),"descriptors_verified":44,"captures":16,"door_differences_open":True,"complete_assets_delivered_increment":0})
p.write_text(json.dumps(data,indent=2,ensure_ascii=False),encoding="utf-8")
p=T/"PLAN_EXECUTION_LOTS_20261009.md"
text=p.read_text(encoding="utf-8")
text+="\n- L03.TOWNSMITH.DESSINS.DESCRIPTEURS : 44 descripteurs et attributs verifies, seize nouvelles captures ; ecarts portes inchanges (3/2/4 pixels). Reconstruction des indices non explicative ; prochain controle : passes d ombres et clone complet. [Preuve](../townsmith-descriptor-slot-controls-reopen-v2/DESCRIPTOR-RESULT.v1.json).\n"
p.write_text(text,encoding="utf-8")
from record_batch_progress_v1 import main
main()
