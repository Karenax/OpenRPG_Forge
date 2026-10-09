"""Publish independently reopened Townsmith evidence without closing draw fidelity."""
from pathlib import Path
import json, hashlib, shutil
from datetime import datetime, timezone

S = Path(__file__).parent
W = Path("F:/OpenRPG_ImportWork")
T = W / "SUIVI_LOTS_HIVEMIND"
P = W / "townsmith-hdrp-slot-contexts-reopen-v1/RESULT.v1.json"
BACKUP = W / "townsmith-context-tracker-before-v1"

def main():
    proof = json.loads(P.read_text(encoding="utf-8-sig"))
    assert proof["cases_reopened"] == 8 and proof["affected_bindings"] == 37
    assert not proof["native_draw_mapping_qualified"]
    assert not BACKUP.exists()
    BACKUP.mkdir()
    names = ["record_batch_progress_v1.py", "render_lot_dashboard_v1.py"]
    for name in names:
        shutil.copy2(S / name, BACKUP / name)
    for name in ["PLAN_EXECUTION_LOTS_20261009.md", "PLAN_EXECUTION_LOTS_20261009.v1.json"]:
        shutil.copy2(T / name, BACKUP / name)
    path = S / names[0]
    text = path.read_text(encoding="utf-8")
    anchor = " active_lot=data.get('current_work_sub_lot',active_lot);active_state=data.get('current_work_state',active_state)"
    assert text.count(anchor) == 1
    block = """ town_context=read(W/'townsmith-hdrp-slot-contexts-reopen-v1/RESULT.v1.json')
 if town_context.get('status')=='eight_source_hdrp_slot_contexts_reopened_private_serialization_scoped_draw_mapping_unobserved':
  data['current_work_sub_lot']='L03.TOWNSMITH.DESSINS.CONTEXTE_HDRP'
  data['current_work_state']='8 configurations HDRP source reouvertes ; 144 passes compilees ; 8 images identiques entre deux executions ; 7 copies privees reserialisees avec proprietes de rendu preservees ; 37 bindings a qualifier ; usage Forge non verifie'
  data['townsmith_hdrp_slot_contexts']={'proof':str(W/'townsmith-hdrp-slot-contexts-reopen-v1/RESULT.v1.json'),'cases_reopened':8,'affected_bindings':37,'native_draw_mapping_qualified':False,'complete_assets_delivered_increment':0}
  data['next_action']='Comparer les huit rendus source aux controles explicites de sous-maillages et slots, puis convertir et verifier dans Forge ; conserver les refus de rendu ponctuel ouverts'
  text=text.replace('| L01.LOD.01 |','| L03.TOWNSMITH.DESSINS.CONTEXTE_HDRP | Huit configurations source | controle technique verifie ; dessins ouverts | 8 vues / 144 passes ; sources preservees ; aucun usage Forge acquis | [Preuve](../townsmith-hdrp-slot-contexts-reopen-v1/RESULT.v1.json) |\\\\n| L01.LOD.01 |')
  key+=':'+town_context['status']
"""
    text = text.replace(anchor, block + anchor)
    old = "('**Lot en cours : L01 — éclairage et rendu des assets source.**')"
    assert text.count(old) == 1
    text = text.replace(old, "('**Lot en cours : '+active_lot.split('.')[0]+' — '+('conversions statiques Townsmith.' if active_lot.startswith('L03.') else 'éclairage et rendu des assets source.')+'**')")
    # Generated row must contain a real newline, not the literal characters backslash-n.
    text = text.replace("acquis | [Preuve](../townsmith-hdrp-slot-contexts-reopen-v1/RESULT.v1.json) |\\\\n|", "acquis | [Preuve](../townsmith-hdrp-slot-contexts-reopen-v1/RESULT.v1.json) |\\n|")
    compile(text, str(path), "exec")
    path.write_text(text, encoding="utf-8")
    path = S / names[1]
    text = path.read_text(encoding="utf-8")
    anchor = "    output += 'Prochaine action enregistrée : '"
    assert text.count(anchor) == 1
    block = """    town_context_path=W/'townsmith-hdrp-slot-contexts-reopen-v1/RESULT.v1.json'
    if town_context_path.exists():
        output += '**L03.TOWNSMITH.DESSINS.CONTEXTE_HDRP : huit configurations source reouvertes.** 144 passes compilees ; huit images identiques entre deux executions. Les sept copies privees de materiaux ont ete reserialisees par HDRP, avec proprietes de rendu preservees ; les originaux et licences restent conserves. Le FrameDebugger est indisponible en batch : aucun dessin natif observe ne sera revendique. Les 37 bindings, le rendu Forge et l usage Studio restent a qualifier. [Preuve](../townsmith-hdrp-slot-contexts-reopen-v1/RESULT.v1.json). Zero asset complet ajoute.\\\\n\\\\n'
"""
    text = text.replace(anchor, block + anchor)
    anchor = "+ [tent5_taps_path,tent5_bridge_path,town_scope_path]"
    assert text.count(anchor) == 1
    text = text.replace(anchor, anchor + " + ([town_context_path] if town_context_path.exists() else [])")
    compile(text, str(path), "exec")
    path.write_text(text, encoding="utf-8")
    plan = T / "PLAN_EXECUTION_LOTS_20261009.md"
    text = plan.read_text(encoding="utf-8")
    start = text.index("## Tranche qui demarre")
    end = text.index("## Comment suivre", start)
    text = text[:start] + """## Tranche actuelle

**L03.TOWNSMITH.DESSINS — conversions statiques, huit configurations et 37 bindings.**

1. Reference HDRP source : terminee pour les huit configurations ; preuves et originaux conserves.
2. Comparaison des affectations sous-maillage/materiau : a executer. Ne pas deduire les dessins du seul inventaire.
3. Conversion Forge : apres qualification des affectations, avec dependances et metadonnees.
4. Usage editeur : recherche, image, apercu, placement, sauvegarde/reouverture puis runtime.

L01 reste ouvert : cause locale PCF5 identifiee ; raster, rendu de scene et usage Studio non qualifies. Cette tranche independante de conversion permet de poursuivre sans masquer ce refus.

""" + text[end:]
    text = text.replace("| L01 | Moteur, rendu, chargement | En cours |", "| L01 | Moteur, rendu, chargement | Ouvert : rendu a qualifier |")
    text = text.replace("| L03 | Conversions statiques | Partiel |", "| L03 | Conversions statiques | En cours : Townsmith |")
    text += "\n- L03.TOWNSMITH.DESSINS.CONTEXTE_HDRP verifie : huit configurations, 144 passes et huit images identiques entre deux executions. Dessins et usage Forge non qualifies. [Recu](../townsmith-hdrp-slot-contexts-reopen-v1/RESULT.v1.json).\n"
    plan.write_text(text, encoding="utf-8")
    path = T / "PLAN_EXECUTION_LOTS_20261009.v1.json"
    data = json.loads(path.read_text(encoding="utf-8"))
    data["active_batch"] = proof["sub_lot"]
    data["next_action"] = "Comparer huit configurations source aux controles explicites de sous-maillages et slots"
    data["script"] = str(S / "reopen_source_townsmith_slot_contexts_v1.py")
    data["script_sha256"] = hashlib.sha256(Path(data["script"]).read_bytes()).hexdigest()
    data["updated_utc"] = datetime.now(timezone.utc).isoformat()
    data.setdefault("finished_batches", []).append({"id": proof["sub_lot"], "proof": str(P), "cases_reopened": 8, "complete_assets_delivered_increment": 0})
    for lot in data["lots"]:
        if lot["id"] == "L03": lot["status"] = "En cours : Townsmith"
        if lot["id"] == "L01": lot["status"] = "Ouvert : rendu a qualifier"
    path.write_text(json.dumps(data, ensure_ascii=False, indent=2), encoding="utf-8")
    from record_batch_progress_v1 import main as regenerate
    regenerate()

if __name__ == "__main__":
    main()
