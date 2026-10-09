from pathlib import Path
from datetime import datetime,timezone
import hashlib,json
T=Path("F:/OpenRPG_ImportWork/SUIVI_LOTS_HIVEMIND")
P=T.parent/"townsmith-verified-door-draw-recipes-v1/DRAW_BINDINGS.v1.json"
proof=json.loads(P.read_text(encoding="utf-8"))
assert proof["bindings_converted"]==15 and proof["unique_meshes"]==3
p=T/"PLAN_EXECUTION_LOTS_20261009.v1.json";d=json.loads(p.read_text(encoding="utf-8"));batch=proof["sub_lot"]
assert not any(r["id"]==batch for r in d["finished_batches"])
d["active_batch"]=batch;d["updated_utc"]=datetime.now(timezone.utc).isoformat();d["next_action"]=proof["next_action"]
d["script"]=str(Path(__file__).with_name("convert_townsmith_verified_door_draw_recipes_v1.py"));d["script_sha256"]=hashlib.sha256(Path(d["script"]).read_bytes()).hexdigest()
d["finished_batches"].append({"id":batch,"proof":str(P),"bindings_converted":15,"unique_meshes":3,"forge_material_program_qualified":False,"complete_assets_delivered_increment":0})
p.write_text(json.dumps(d,indent=2,ensure_ascii=False),encoding="utf-8")
p=T/"PLAN_EXECUTION_LOTS_20261009.md";text=p.read_text(encoding="utf-8")
text=text.replace("3. Conversion Forge : apres qualification des affectations, avec dependances et metadonnees.","3. Conversion Forge : quinze affectations de couleur des portes converties en recettes, geometrie complete conservee. Conversion du shader source, integration des prefabs et rendu Forge restants.")
text+="\n- L03.TOWNSMITH.CONVERSION.AFFECTATIONS_PORTES : quinze recettes couleur / trois meshes converties et reouvertes ; 39 epingles verifiees. Correspondance de geometrie visible dans les captures source avec et sans ombres. Shader Forge, politique des ombres, prefabs complets et usage editeur non qualifies. [Recettes](../townsmith-verified-door-draw-recipes-v1/DRAW_BINDINGS.v1.json).\n"
p.write_text(text,encoding="utf-8")
from record_batch_progress_v1 import main
main()
