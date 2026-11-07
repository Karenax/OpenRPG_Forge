"""Publish a reviewable batch plan, reopen the GPU receipt, start eight new cards."""
from pathlib import Path
from datetime import datetime, timezone
import hashlib
import json
import subprocess
import sys

S = Path(__file__).parent
W = Path('F:/OpenRPG_ImportWork')
T = W / 'SUIVI_LOTS_HIVEMIND'

def read(p):
    return json.loads(p.read_text(encoding='utf-8-sig'))

def sha(p):
    with p.open('rb') as f:
        return hashlib.file_digest(f, 'sha256').hexdigest()

def write(p, value):
    p.write_text(json.dumps(value, indent=2, ensure_ascii=False), encoding='utf-8')

pipeline = W / 'townsmith-s-paint-bevy-pipeline-v3'
run = read(pipeline / 'RUN-RESULT.v1.json')
native = read(pipeline / 'NATIVE-RESULT.v1.json')
assert run['actual_exit_code'] == 0
assert run['pipelines_created'] == native['pipelines_created'] == 7
assert run['native_receipt_sha256'] == sha(pipeline / 'NATIVE-RESULT.v1.json')
assert not native['gpu_draw_executed'] and not native['actual_resource_bind_groups_created']
seal = W / 'townsmith-s-paint-bevy-pipeline-reopen-v3'
assert not seal.exists()
pins = {str(pipeline / n): sha(pipeline / n) for n in
        ['RUN-RESULT.v1.json', 'NATIVE-RESULT.v1.json', 'INPUT.v1.json']}
inputs = read(pipeline / 'INPUT.v1.json')
for case in inputs['cases']:
    assert sha(Path(case['export'])) == case['export_sha256']
    pins[case['export']] = case['export_sha256']
seal.mkdir()
write(seal / 'RESULT.v1.json', dict(
    status='seven_actual_bevy_material_pipeline_receipts_reopened',
    finished_utc=datetime.now(timezone.utc).isoformat(), pipelines_verified=7,
    actual_resource_bind_groups_created=False, gpu_draw_executed=False,
    source_render_parity_qualified=False, forge_editor_verified=False,
    complete_assets_delivered_increment=0, full_import=False, input_pins=pins))

lots = [
 ('L00', 'Sources, licences et stockage', 'Contrôle historique conservé', 'SHA de chaque nouvelle livraison ; sources intactes'),
 ('L01', 'Chargement et rendu commun', 'Partiel', 'Textures, uniformes, dessin GPU, comparaison Unity ; ombres séparées'),
 ('L02', 'Conversion Dungeon', 'Partiel', '5 prefabs par tranche ; hiérarchie, matériaux et collisions'),
 ('L03', 'Conversion Townsmith / House / Villa', 'Actif : Townsmith', 'Un matériau puis les prefabs associés ; House par 16 objets, Villa par 5 scènes'),
 ('L04', 'Matériaux et végétation', 'Partiel', 'Une famille de shaders ; alpha, normales, vent, ombres et LOD'),
 ('L05', 'Rigs et animations', 'À terminer', 'Un squelette et ses clips ; poids, poses et lecture runtime'),
 ('L06', 'Effets FirePit / Blood / Dust', 'Partiel', 'Un effet par tranche ; état initial, évolution et extinction'),
 ('L07', 'Sprites, terrains et audio', 'À traiter par format', 'Dépendances, conversion, lecture et métadonnées'),
 ('L08', 'Images et recherche dans la bibliothèque', 'Actif : huit fiches House', '8 fiches / 16 images ; revue individuelle, indexation, lecteur produit'),
 ('L09', 'Vérification finale dans Studio', 'Ouvert', 'Recherche, filtres, aperçu, placement, sauvegarde, réouverture, runtime ; IDs des 48 packs/variantes'),
]
text = '# Hivemind — lots et livraisons à suivre\n\n'
text += 'Fichiers définitifs : `F:\\OpenRPG_AssetVault`. Travail, captures, caches et preuves : `F:\\OpenRPG_ImportWork`.\n\n'
text += '| Lot | Travail | État | Taille / critère |\n|---|---|---|---|\n'
text += ''.join('| ' + ' | '.join(row) + ' |\n' for row in lots)
text += '''
## Ordre immédiat

1. **L03.TOWNSMITH.PIPELINE — terminé techniquement** : sept variantes créent leur pipeline avec les véritables layouts Bevy. Aucun dessin ni asset complet validé à ce stade.
2. **L08.IMAGE.HOUSE.05 — prochain lot lancé** : huit fiches encore sans image, seize vues originales. Vérifier chaque vue avant d'indexer ; conserver les refus précédents.
3. **L03.TOWNSMITH.RESSOURCES** : charger les dix textures natives du GLB et les uniformes dans le chemin commun du moteur. Critère : création des vrais groupes de ressources sans substitution de textures.
4. **L03.TOWNSMITH.DESSIN** : dessiner la porte avec ses états source, comparer au rendu Unity, puis assembler les prefabs. Les trois LOD transportés ne prouvent pas encore leur comportement runtime.
5. Reprendre les autres familles statiques, les matériaux, rigs et effets selon les lots ci-dessus ; terminer par le parcours réel dans Studio.

## Règle de clôture

Chaque livraison porte un ID de lot, les IDs des éléments, les fichiers sur F, les contrôles exécutés, les refus et la prochaine action. Les états sont distincts : source conservée → conversion → vérification technique → image et indexation → vérification Studio → runtime qualifié.

Une fiche avec image ne devient pas un asset 3D plaçable. Une compilation ou un pipeline GPU ne devient pas une validation visuelle. Un échec reste ouvert avec sa preuve ; le prochain lot indépendant continue.

## Dernière livraison bibliothèque

Huit fiches et seize images validées dans la tranche HOUSE.04 ; 926 fiches source restent sans image. Recherche, filtre de pack/disponibilité, détails et lecture PNG vérifiés par le lecteur natif. Le parcours visuel réel dans Studio reste à vérifier.

[Avancement courant](AVANCEMENT_ACTUEL.md) · [Journal](JOURNAL.md) · [Preuve GPU](../townsmith-s-paint-bevy-pipeline-reopen-v3/RESULT.v1.json)
'''
(T / 'PLAN_LOTS_A_SUIVRE.md').write_text(text, encoding='utf-8')
write(T / 'PLAN_LOTS_A_SUIVRE.v1.json', dict(
    updated_utc=datetime.now(timezone.utc).isoformat(), lots=lots,
    current_batch='L08.IMAGE.HOUSE.05', next_conversion='L03.TOWNSMITH.RESSOURCES',
    full_import=False, pipeline_receipt=str(seal / 'RESULT.v1.json')))
with (T / 'JOURNAL.md').open('a', encoding='utf-8') as f:
    f.write('\n- Plan par lots publié : PLAN_LOTS_A_SUIVRE.md. Sept pipelines Bevy réouverts ; ressources et dessin restent ouverts. Lot suivant : HOUSE.05, huit fiches / seize images.\n')
subprocess.run([sys.executable, '-B', str(S / 'seal_townsmith_material_packet_v1.py'), 'refresh'], check=True)
target = S / 'queue_house_thumbnail_batch_0025_0032_v1.py'
assert not target.exists()
code = (S / 'queue_house_thumbnail_batch_0017_0024_v1.py').read_text(encoding='utf-8')
code = code.replace('house-source-thumbnail-batch-0017-0024', 'house-source-thumbnail-batch-0025-0032')
code = code.replace('L08.IMAGE.HOUSE.04', 'L08.IMAGE.HOUSE.05')
target.write_text(code, encoding='utf-8')
subprocess.run([sys.executable, '-B', str(target)], check=True)
