"""Publish verified batch progress without claiming complete conversion or GUI QA."""
from pathlib import Path
from datetime import datetime, timezone
import hashlib
import json

W=Path('F:/OpenRPG_ImportWork');T=W/'SUIVI_LOTS_HIVEMIND'
proof=W/'house-source-thumbnail-batch-0033-0040-delivery-reopen-v1/RESULT.v1.json'
def read(p):return json.loads(p.read_text(encoding='utf-8-sig'))
def sha(p):
    with p.open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
d=read(proof)
assert d['cards_updated']==8 and d['source_images_installed']==16
assert d['search_pack_availability_filters_details_and_primary_image_verified']
assert d['missing_primary_images_before']==918 and d['missing_primary_images_after']==910
assert not d['actual_editor_visual_verified'] and not d['full_import']
for p,h in d['input_pins'].items():assert sha(Path(p))==h,p
now=datetime.now(timezone.utc).isoformat()
p=T/'SUIVI_LISIBLE.md';q=p.read_text(encoding='utf-8');a=q.index('## Travail actif');b=q.index('## Découpage et ordre',a)
q=q[:a]+'''## Dernières livraisons et prochaine tranche

**L08 · HOUSE.06 livré : huit fiches / seize images installées et lues par le produit.**

Deux plats en terre cuite, un râteau, quatre variantes de rochers et une bêche ont maintenant leurs deux vues source sur F. Les objets utilisent EV8 ; les rochers EV4. Les captures initiales trop sombres et les contrôles écrêtés sont conservés. Les seize images finales ont été revues individuellement ; sources, licences, retouches, IDs et disponibilité restent préservés.

La recherche, les filtres de pack/disponibilité, les détails et les PNG ont été vérifiés par le lecteur natif actuel. Les demandes de modèle restent refusées pour ces fiches source non plaçables. **Fiches source sans image : 918 → 910.** La vérification visuelle dans Studio reste ouverte.

**L03 · export UV1 Townsmith livré techniquement.** La règle d’entrée UV implicite conserve le canal déclaré. Les 134 opérations régénérées ont été réouvertes dans les deux stages ; UV1 float4 conservé dans les trois LOD. Le graphe corrigé a été comparé sur GPU. Le filtrage fin UV1 ne change aucun pixel et reste diagnostique.

**Prochaine tranche : L03.TOWNSMITH.MATERIAUX.SCENE**, écarts restants du matériau éclairé, occlusion, bloom et fond source ; puis assemblage des prefabs et parcours Studio. Les autres familles gardent les lots ci-dessous.

Ces livraisons sont des preuves d’export et de bibliothèque. **Aucun nouvel asset 3D complet qualifié ; l’objectif global reste ouvert.**

[Preuve HOUSE.06](../house-source-thumbnail-batch-0033-0040-delivery-reopen-v1/RESULT.v1.json) · [Images revues](../house-source-thumbnail-batch-0033-0040-final-review-v1/CANDIDATES.v1.json) · [Export UV1](TRANCHE_EXPORT_UV1_V38.v1.json).

'''+q[b:]
q=q.replace('HOUSE.05 livré au lecteur natif ; 918 fiches source sans image','HOUSE.06 livré au lecteur natif ; 910 fiches source sans image')
p.write_text(q,encoding='utf-8')
message=f'\n- {now} — **L08.IMAGE.HOUSE.06 livré** : huit fiches / seize PNG revus, installés sur F et vérifiés par la recherche, les filtres, les détails et le lecteur natif. Sources, licences, IDs, retouches et autres fiches préservés ; delta Vault recontrôlé. Fiches sans image : 918 → 910. Éditeur visuel et conversion runtime non qualifiés. [Preuve](../house-source-thumbnail-batch-0033-0040-delivery-reopen-v1/RESULT.v1.json). Aucun asset 3D complet supplémentaire qualifié.\n'
for name in ['PLAN_LOTS_A_SUIVRE.md','AVANCEMENT_ACTUEL.md','JOURNAL.md']:
    with (T/name).open('a',encoding='utf-8') as f:f.write(message)
for name in ['SUIVI_SIMPLE.v1.json','PLAN_LOTS_A_SUIVRE.v1.json']:
    p=T/name;j=read(p)
    j.update(updated_utc=now,state=d['status'],last_technical_batch='L08.IMAGE.HOUSE.06',last_technical_proof=str(proof),active_sub_lot='L03.TOWNSMITH.MATERIAUX.SCENE',current_work_sub_lot='L03.TOWNSMITH.MATERIAUX.SCENE',current_work_state='HOUSE.06 livré ; prochains écarts : matériau éclairé et scène Townsmith.',current_work_root='',current_work_receipt=str(proof),missing_primary_images=910,complete_assets_delivered_increment=0,editor_visual_verified=False,full_import=False,next_action='Inspect remaining whole-material errors and qualify source/native AO, bloom and background; continue prefabs, remaining families and actual Studio workflow.')
    p.write_text(json.dumps(j,indent=2,ensure_ascii=False)+'\n',encoding='utf-8')
print(json.dumps(dict(lot='L08.IMAGE.HOUSE.06',cards=8,images=16,missing_primary_images=910,next_sub_lot='L03.TOWNSMITH.MATERIAUX.SCENE',full_import=False)))
