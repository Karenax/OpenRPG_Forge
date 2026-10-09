from pathlib import Path
from datetime import datetime,timezone
import hashlib,json
T=Path("F:/OpenRPG_ImportWork/SUIVI_LOTS_HIVEMIND")
P=T.parent/"townsmith-s-paint-wgsl-reopen-v1/RESULT.v1.json"
proof=json.loads(P.read_text());assert proof["operations_reopened"]==134 and proof["shader_fragment_type_validation"]
p=T/"PLAN_EXECUTION_LOTS_20261009.v1.json";d=json.loads(p.read_text(encoding="utf-8"));batch=proof["sub_lot"]
assert not any(r["id"]==batch for r in d["finished_batches"])
d["active_batch"]=batch;d["updated_utc"]=datetime.now(timezone.utc).isoformat();d["next_action"]=proof["next_action"]
d["script"]=str(Path(__file__).with_name("reopen_townsmith_paint_wgsl_v1.py"));d["script_sha256"]=hashlib.sha256(Path(d["script"]).read_bytes()).hexdigest()
d["finished_batches"].append({"id":batch,"proof":str(P),"operations_reopened":134,"texture_bindings_required":10,"source_numeric_gpu_equivalence_qualified":False,"complete_assets_delivered_increment":0})
p.write_text(json.dumps(d,indent=2,ensure_ascii=False),encoding="utf-8")
p=T/"PLAN_EXECUTION_LOTS_20261009.md";text=p.read_text(encoding="utf-8")
text+="\n- L03.TOWNSMITH.SHADER.S_PAINT.WGSL : 134 operations generees et types fragment valides ; dix textures et trois normal maps. Contrat commun a etendre de sept a dix textures sans collision des bindings de scene. Import textures, fidelite GPU Forge et usage editeur restent ouverts. [Preuve](../townsmith-s-paint-wgsl-reopen-v1/RESULT.v1.json).\n"
p.write_text(text,encoding="utf-8")
from record_batch_progress_v1 import main
main()
