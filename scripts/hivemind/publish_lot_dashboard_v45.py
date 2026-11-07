from pathlib import Path
from datetime import datetime,timezone
import hashlib,json
W=Path('F:/OpenRPG_ImportWork');T=W/'SUIVI_LOTS_HIVEMIND'
def read(p):return json.loads(p.read_text(encoding='utf-8-sig'))
def sha(p):
 with p.open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
proofs=[W/'house-source-thumbnail-batch-0033-0040-delivery-reopen-v1/RESULT.v1.json',T/'TRANCHE_EXPORT_UV1_V38.v1.json',T/'TRANCHE_MATERIAU_SCENE_V41.v1.json',W/'townsmith-lighting-diagnostic-v44/DRAW-DIAGNOSTIC.v1.json']
for p in proofs:read(p)
old=read(proofs[-1]);assert old['pending']==1 and old['thresholds_unchanged'] and not old['gpu_validation_errors']
D=W/'townsmith-lighting-diagnostic-v45';run=read(D/'RUN-RESULT.v1.json')
now=datetime.now(timezone.utc).isoformat()
state='Diagnostic éclairage en cours' if 'finished_utc' not in run else 'Diagnostic éclairage terminé ; résultat conservé'
backup=T/'dashboard-before-v45';backup.mkdir(exist_ok=False)
for name in ['SUIVI_LISIBLE.md','PLAN_LOTS_A_SUIVRE.md','AVANCEMENT_ACTUEL.md','JOURNAL.md','SUIVI_SIMPLE.v1.json','PLAN_LOTS_A_SUIVRE.v1.json']:
 (backup/name).write_bytes((T/name).read_bytes())
head=f"""# Hivemind — suivi par lots

Mis à jour : {now}. Fichiers définitifs : **F:\OpenRPG_AssetVault**. Travail, caches, captures et preuves : **F:\OpenRPG_ImportWork**.

## Avancement actuel

| Tranche | Résultat livré | Reste à faire |
|---|---|---|
| L08 · HOUSE.06 | 8 fiches, 16 images revues et installées ; recherche, filtres, métadonnées et lecture PNG vérifiés par le lecteur produit | Contrôle visuel dans Studio ; 910 fiches source restent sans image |
| L03 · Townsmith UV1 | Export corrigé et régénéré ; normales et rugosité contrôlées ; erreur RGB du matériau éclairé réduite de 71 % | Fidélité complète du matériau, assemblage des prefabs |
| L03 · Valeurs matériau et occlusion | Couleur brute et paramètres comparés au graphe Unity ; occlusion seule insuffisante pour expliquer l'écart | Séparer et comparer éclairage direct, ambiant et reflets |
| L03 · Diagnostic éclairage | {state} ; V44 refusé après 90 s avec un pipeline encore en attente, sans erreur de validation GPU | V45 identifie les états en attente ; conserver les refus et les seuils |

**Aucun nouvel asset 3D complet qualifié dans ces tranches.** Les images, conversions techniques et validations runtime ont des compteurs distincts. Le parcours réel recherche → placement → édition → sauvegarde → réouverture reste ouvert.

Prochaine livraison attendue : **L03.TOWNSMITH.MATERIAUX.SCENE.ECLAIRAGE**, puis **PREFABS.01** et **STUDIO.01**. Les autres familles suivent les lots ci-dessous ; une tranche indépendante peut continuer si un contrôle est refusé.

[Livraison HOUSE.06](../house-source-thumbnail-batch-0033-0040-delivery-reopen-v1/RESULT.v1.json) · [Export UV1](TRANCHE_EXPORT_UV1_V38.v1.json) · [Contrôles matériau](TRANCHE_MATERIAU_SCENE_V41.v1.json) · [Diagnostic courant](../townsmith-lighting-diagnostic-v45/RUN-RESULT.v1.json).

"""
p=T/'SUIVI_LISIBLE.md';text=p.read_text(encoding='utf-8-sig');assert text.count('## Découpage et ordre')==1;p.write_text(head+text[text.index('## Découpage et ordre'):],encoding='utf-8')
p=T/'PLAN_LOTS_A_SUIVRE.md';text=p.read_text(encoding='utf-8-sig');cut=text.index('## Townsmith : tranches');first=text[:cut].replace('HOUSE.05','HOUSE.06').replace('918 fiches','910 fiches').replace('926 → 918','918 → 910').replace('batch-0025-0032','batch-0033-0040');p.write_text(first+text[cut:],encoding='utf-8')
message=f"\n- {now} — **Suivi synchronisé par lots** : HOUSE.06 livré (8 fiches / 16 images ; 910 fiches source sans image), correction UV1 et contrôles matériau livrés techniquement. V44 terminé en refus : 1 pipeline en attente après 90 s, aucune erreur de validation GPU ; V45 ajoute les états de pipelines, mêmes données et seuils. État V45 : {run['status']}. Fidélité complète, prefabs, Studio et runtime restent ouverts.\n"
for name in ['PLAN_LOTS_A_SUIVRE.md','AVANCEMENT_ACTUEL.md','JOURNAL.md']:
 with (T/name).open('a',encoding='utf-8') as f:f.write(message)
for name in ['SUIVI_SIMPLE.v1.json','PLAN_LOTS_A_SUIVRE.v1.json']:
 p=T/name;j=read(p);j.update(updated_utc=now,active_sub_lot='L03.TOWNSMITH.MATERIAUX.SCENE.ECLAIRAGE',current_work_sub_lot='L03.TOWNSMITH.MATERIAUX.SCENE.ECLAIRAGE',current_work_state=run['status'],current_work_root=str(D),current_work_receipt=str(D/'RUN-RESULT.v1.json'),missing_source_primary_images=910,missing_primary_images=910,full_import=False,editor_visual_verified=False,next_action='Identifier le pipeline en attente, comparer éclairage direct/ambiant/reflets ; puis assemblage des prefabs et parcours Studio.')
 if 'lots' in j:
  for lot in j['lots']:
   if lot[0]=='L08':lot[2]='HOUSE.06 livré : huit fiches / seize images ; 910 fiches source sans image'
 p.write_text(json.dumps(j,indent=2,ensure_ascii=False)+'\n',encoding='utf-8')
receipt=T/'DASHBOARD_SYNC_V45.v1.json';assert not receipt.exists()
receipt.write_text(json.dumps(dict(updated_utc=now,input_pins={str(p):sha(p) for p in [Path(__file__),*proofs,D/'RUN-RESULT.v1.json']},backup=str(backup),full_import=False,complete_assets_delivered_increment=0),indent=2),encoding='utf-8')
print(json.dumps(dict(dashboard=str(T/'SUIVI_LISIBLE.md'),current_state=run['status'],missing_source_primary_images=910)))
