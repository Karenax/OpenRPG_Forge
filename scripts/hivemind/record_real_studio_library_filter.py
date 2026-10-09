"""Retain actual filter and non-placeable detail observations without promoting runtime qualification."""
import datetime,hashlib,json,os
from pathlib import Path
W=Path(r"F:\OpenRPG_ImportWork"); T=W/"SUIVI_LOTS_HIVEMIND"
previous=T/"L08_CONTROLE_EDITEUR_PARTIEL.v1.json"; raw=previous.read_bytes(); receipt=json.loads(raw)
now=datetime.datetime.now(datetime.timezone.utc).isoformat()
receipt.update(observed_utc=now,previous_receipt=str(previous),previous_receipt_sha256=hashlib.sha256(raw).hexdigest(),filter_application_verified=True,filter_observation_failure_recovered=True,collection_path_visible=True)
receipt["filter_observations"]=[{"availability":"Source conservée","search":"Metal Corner Con A","results_visible":0},{"availability":"Source conservée","search":"","results_visible":12516}]
receipt["source_support_detail"]={"name":"Air Conditioner 01a","thumbnail_visible":True,"thumbnail_scope":"geometry preview only; material color-space unresolved", "preview3d_button_disabled":True,"place_button_disabled":True,"reason_visible":"Le réglage Gamma/Linear du projet source manque, donc le matériau Unity n'est pas converti."}
receipt["other_source_support_cards_show_placeholder_icons"]=True
receipt["all_asset_images_verified"]=False
receipt["placement_verified"]=False;receipt["runtime_verified"]=False;receipt["new_qualified_assets"]=0
path=T/"L08_CONTROLE_EDITEUR_PARTIEL.v2.json"
with path.open("x",encoding="utf-8") as out: json.dump(receipt,out,ensure_ascii=False,indent=2)
statepath=T/"ETAT.v1.json";original=statepath.read_bytes();state=json.loads(original)
lot=next(b for b in state["batches"] if b["id"]=="L08")
lot.update(status="images_fiche_recherche_filtre_disponibilite_reels_verifies_placement_runtime_restants",partial_editor_receipt=str(path),availability_filter_verified=True,nonplaceable_actions_disabled_verified=True)
state["updated_utc"]=now;tmp=statepath.with_suffix(f".json.{os.getpid()}.tmp");tmp.write_text(json.dumps(state,ensure_ascii=False,indent=2),encoding="utf-8")
if statepath.read_bytes()!=original:tmp.unlink();raise RuntimeError("Concurrent tracking edit")
os.replace(tmp,statepath)
entry=f"\n## L08 — filtre et fiche non plaçable — {now}\nFiltre Source conservée appliqué réellement : recherche précédente 14 modèles → 0 résultat ; sans mot-clé → 12 516 sources conservées. Fiche Air Conditioner 01a : image de géométrie visible, raison Gamma/Linear explicite, Aperçu 3D et Placer sur la carte désactivés. Chemin de collection F visible. Plusieurs autres sources ont seulement une icône générique ; toutes les images ne sont pas qualifiées. Placement/sauvegarde/runtime restent à prouver. Reçu : {path}\n"
for target in [T/"JOURNAL.md",W/"RUN_STATUS.md"]:
    with target.open("a",encoding="utf-8") as out:out.write(entry)
plan=T/"PLAN_ET_SUIVI.md";content=plan.read_text(encoding="utf-8").replace("filtres, placement, sauvegarde et runtime restants","filtre de disponibilité appliqué (12 516 sources), fiche non plaçable et actions désactivées vérifiés ; autres filtres, placement, sauvegarde et runtime restants")
plan.write_text(content,encoding="utf-8")
print(json.dumps({"receipt":str(path),"filter_verified":True,"sources_visible":12516,"new_qualified_assets":0}))
