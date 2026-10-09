"""Retain bounded observations from the real Studio window, not full qualification."""
import datetime, hashlib, json, os
from pathlib import Path
WORK=Path(r"F:\OpenRPG_ImportWork")
TRACK=WORK/"SUIVI_LOTS_HIVEMIND"
now=datetime.datetime.now(datetime.timezone.utc).isoformat()
prepared=json.loads((WORK/"asset-library-editor-frontend-review-v2"/"PREPARED.v1.json").read_bytes())
exe=prepared["executable"]
if hashlib.sha256(Path(exe["path"]).read_bytes()).hexdigest()!=exe["sha256"]:
    raise RuntimeError("Studio executable changed; observation identity not retained")
receipt={"observed_utc":now,"status":"real_studio_library_partial_review", "window_id":13833474,"window_title":"OpenRPG Forge Studio · Édition locale","process_pid_at_launch":47616,"executable":exe,"collection":r"F:\OpenRPG_AssetVault","indexed_entries_visible":26735,"images_visible":True,"details_visible":True,"details_observed":{"name":"Accessoire de donjon · Metal Corner Con A · LOD 0","dimensions_m":[0.30,0.30,0.30],"triangles":12,"subtype":"dungeon-prop","qualification_limit_visible":"Placement et comportement dans le jeu restent à qualifier."},"search":{"text":"Metal Corner Con A","result_count_visible":14,"images_visible":True},"source_support_nonplaceable_label_visible":True,"filter_options_visible":["Tous","Modèle GLB utilisable","Source conservée"],"filter_application_verified":False,"filter_observation_failure":"Computer Use capture/activation timeout; owned WebView dropdown point rejected", "evidence_kind":"agent observations of actual native screenshots retained in conversation; no standalone screenshot persisted", "frontend_server_used_by_window_verified":False,"current_source_build_qualified":False,"placement_verified":False,"save_reopen_verified":False,"runtime_verified":False,"human_visual_approval":False,"new_qualified_assets":0,"full_import":False}
path=TRACK/"L08_CONTROLE_EDITEUR_PARTIEL.v1.json"
if path.exists(): raise RuntimeError("Receipt already exists; preserve it")
path.write_text(json.dumps(receipt,ensure_ascii=False,indent=2),encoding="utf-8")
statepath=TRACK/"ETAT.v1.json"
original=statepath.read_bytes()
state=json.loads(original)
lot=next(x for x in state["batches"] if x["id"]=="L08")
lot["status"]="images_fiche_recherche_reelles_verifiees_filtres_placement_runtime_restants"
lot["partial_editor_receipt"]=str(path)
lot["new_qualified_assets"]=0
state["updated_utc"]=now
tmp=statepath.with_suffix(f".json.{os.getpid()}.tmp")
tmp.write_text(json.dumps(state,ensure_ascii=False,indent=2),encoding="utf-8")
if statepath.read_bytes()!=original:
    tmp.unlink(); raise RuntimeError("Tracking changed concurrently")
os.replace(tmp,statepath)
entry=f"\n## L08 — contrôle réel partiel — {now}\nStudio réel : 26 735 entrées affichées, images visibles, fiche Dungeon avec dimensions et limite de qualification ; recherche Metal Corner Con A : 14 résultats. Options de disponibilité visibles, application du filtre non vérifiée à cause de délais Computer Use. Placement/sauvegarde/réouverture/runtime restent à faire. Aucun nouvel asset livré. Reçu : {path}\n"
for target in [TRACK/"JOURNAL.md",WORK/"RUN_STATUS.md"]:
    with target.open("a",encoding="utf-8") as out: out.write(entry)
plan=TRACK/"PLAN_ET_SUIVI.md"
text=plan.read_text(encoding="utf-8")
text=text.replace("contrôle visuel réel encore à faire", "contrôle réel partiel : 26 735 entrées et images affichées, fiche Dungeon et recherche de nom (14 résultats) vérifiées ; filtres, placement, sauvegarde et runtime restants")
plan.write_text(text,encoding="utf-8")
print(json.dumps(receipt,ensure_ascii=False))
