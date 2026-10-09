from pathlib import Path
import json, hashlib
from datetime import datetime, timezone

W = Path("F:/OpenRPG_ImportWork")
T = W / "SUIVI_LOTS_HIVEMIND"
P = W / "townsmith-explicit-slot-controls-reopen-v1/RESULT.v1.json"
proof = json.loads(P.read_text(encoding="utf-8"))
assert proof["cases_reopened"] == 16 and proof["byte_identical_controls"] == 10
p = T / "PLAN_EXECUTION_LOTS_20261009.v1.json"
data = json.loads(p.read_text(encoding="utf-8"))
assert not any(b["id"] == proof["sub_lot"] for b in data["finished_batches"])
data["active_batch"] = proof["sub_lot"]
data["updated_utc"] = datetime.now(timezone.utc).isoformat()
data["script"] = str(Path(__file__).parent / "reopen_townsmith_explicit_slot_controls_v1.py")
data["script_sha256"] = hashlib.sha256(Path(data["script"]).read_bytes()).hexdigest()
data["finished_batches"].append({"id": proof["sub_lot"], "proof": str(P), "controls_executed": 16, "byte_identical_controls": 10, "door_differences_open": True, "complete_assets_delivered_increment": 0})
data["next_action"] = "Isoler les 2 a 4 pixels differents des portes, puis verifier les affectations et materiaux convertis dans Forge"
p.write_text(json.dumps(data, ensure_ascii=False, indent=2), encoding="utf-8")
p = T / "PLAN_EXECUTION_LOTS_20261009.md"
text = p.read_text(encoding="utf-8")
text = text.replace("2. Comparaison des affectations sous-maillage/materiau : a executer. Ne pas deduire les dessins du seul inventaire.", "2. Comparaison des affectations sous-maillage/materiau : 16 controles executes ; dix vues lit/etagere bit-exactes ; portes : 2 a 4 pixels differents, a isoler. Les passes repetees de materiaux identiques restent visuellement indiscernables.")
text += "\n- L03.TOWNSMITH.DESSINS.CONTROLES_EXPLICITES : seize comparaisons reouvertes, dix bit-exactes ; portes non qualifiees. [Recu](../townsmith-explicit-slot-controls-reopen-v1/RESULT.v1.json). Aucun nouvel asset complet.\n"
p.write_text(text, encoding="utf-8")
from record_batch_progress_v1 import main
main()
