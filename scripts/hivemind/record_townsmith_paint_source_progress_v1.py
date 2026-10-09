from pathlib import Path
from datetime import datetime,timezone
import hashlib,json
T=Path("F:/OpenRPG_ImportWork/SUIVI_LOTS_HIVEMIND")
P=T.parent/"townsmith-s-paint-source-program-reopen-v1/RESULT.v1.json"
proof=json.loads(P.read_text());assert proof["active_nodes_reopened"]==134 and proof["input_slots_reopened"]==255
p=T/"PLAN_EXECUTION_LOTS_20261009.v1.json";d=json.loads(p.read_text(encoding="utf-8"));batch=proof["sub_lot"]
assert not any(r["id"]==batch for r in d["finished_batches"])
d["active_batch"]=batch;d["updated_utc"]=datetime.now(timezone.utc).isoformat();d["next_action"]=proof["next_action"]
d["script"]=str(Path(__file__).with_name("reopen_townsmith_s_paint_source_program_v1.py"));d["script_sha256"]=hashlib.sha256(Path(d["script"]).read_bytes()).hexdigest()
d["finished_batches"].append({"id":batch,"proof":str(P),"active_nodes_reopened":134,"input_slots_reopened":255,"wgsl_generated":False,"forge_material_render_qualified":False,"complete_assets_delivered_increment":0})
p.write_text(json.dumps(d,indent=2,ensure_ascii=False),encoding="utf-8")
p=T/"PLAN_EXECUTION_LOTS_20261009.md";text=p.read_text(encoding="utf-8")
text+="\n- L03.TOWNSMITH.SHADER.S_PAINT.SOURCE : 134 operations et 255 ports reouverts contre deux graphes originaux, sept branches et deux instances de sous-graphe ; code Unity genere exporte. Entrees vertex implicites originales identifiees, non converties en zeros. Traduction WGSL et fidelite GPU Forge restantes. [Preuve](../townsmith-s-paint-source-program-reopen-v1/RESULT.v1.json).\n"
p.write_text(text,encoding="utf-8")
from record_batch_progress_v1 import main
main()
