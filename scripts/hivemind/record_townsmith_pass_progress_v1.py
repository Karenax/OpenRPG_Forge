from pathlib import Path
from datetime import datetime,timezone
import hashlib,json
T=Path("F:/OpenRPG_ImportWork/SUIVI_LOTS_HIVEMIND")
proofs=[T.parent/"townsmith-door-pass-controls-reopen-v1/RESULT.v1.json",T.parent/"townsmith-explicit-shadow-controls-reopen-v1/RESULT.v1.json"]
observations=[r for p in proofs for r in json.loads(p.read_text())["comparisons"]]
assert len(observations)==15 and all(r["source_bytes_identical"] for r in observations)
p=T/"PLAN_EXECUTION_LOTS_20261009.v1.json";d=json.loads(p.read_text(encoding="utf-8"))
batch="L03.TOWNSMITH.DESSINS.PASSES"
assert not any(r["id"]==batch for r in d["finished_batches"])
d["active_batch"]=batch;d["updated_utc"]=datetime.now(timezone.utc).isoformat()
d["next_action"]="Isoler mode shadow casting et etat reel des ombres de la lumiere ; ne pas attribuer les ecarts aux triangles caches"
d["script"]=str(Path(__file__).with_name("reopen_townsmith_explicit_shadow_controls_v1.py"));d["script_sha256"]=hashlib.sha256(Path(d["script"]).read_bytes()).hexdigest()
d["finished_batches"].append({"id":batch,"proofs":list(map(str,proofs)),"byte_identical_captures":15,"hidden_shadow_triangles_causal_qualification":False,"complete_assets_delivered_increment":0})
p.write_text(json.dumps(d,indent=2,ensure_ascii=False),encoding="utf-8")
p=T/"PLAN_EXECUTION_LOTS_20261009.md"
text=p.read_text(encoding="utf-8")
text+="\n- L03.TOWNSMITH.DESSINS.PASSES : quinze nouvelles captures bit-exactes. Clone et contexte enfant exacts ; separation visible/shadow caster exacte, mais deux geometries d ombres indiscernables. La causalite des triangles caches n est pas prouvee. Prochain controle : mode shadow casting et lumiere. [Renderer](../townsmith-door-pass-controls-reopen-v1/RESULT.v1.json) / [Ombres explicites](../townsmith-explicit-shadow-controls-reopen-v1/RESULT.v1.json).\n"
p.write_text(text,encoding="utf-8")
from record_batch_progress_v1 import main
main()
