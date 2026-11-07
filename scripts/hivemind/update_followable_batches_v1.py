"""Publish a compact dashboard from existing batch proofs, without upgrading them."""
from pathlib import Path
from datetime import datetime, timezone
import json

w = Path('F:/OpenRPG_ImportWork')
t = w / 'SUIVI_LOTS_HIVEMIND'
failed = w / 'townsmith-door-source-uv-derivatives-v24/RUN-RESULT.v1.json'
running = w / 'townsmith-door-source-uv-derivatives-v25/RUN-RESULT.v1.json'
assert json.loads(failed.read_text())['actual_exit_code'] == 1
live = json.loads(running.read_text())
now = datetime.now(timezone.utc).isoformat()
body = f'''# Hivemind — suivi lisible par lots

Mis à jour : {now}. Les fichiers définitifs sont dans **F:\\OpenRPG_AssetVault** ; les conversions, captures et preuves dans **F:\\OpenRPG_ImportWork**.

## Travail actif

**L03 · Townsmith · matériau de la porte : UV et échantillonnage.** Le contrôle UV v24 a échoué au chargement du shader privé. Sa preuve est conservée. Le contrôle corrigé v25 a été lancé ; état observé lors de cette mise à jour : `{live['status']}`.

Dernier sous-lot vérifié : comparaison causale de l'anisotropie Unity/Forge. La couverture et la profondeur ont aussi été comparées indépendamment sur 34 445 pixels. La fidélité complète du matériau reste ouverte. **Aucun nouvel asset complet qualifié par ces contrôles.**

## Découpage et ordre

| Lot | Contenu | Tranche de travail | État actuel |
|---|---|---|---|
| L00 | Sources, licences, stockage sur F | Sources de chaque livraison, SHA et dépendances | Preuves historiques conservées ; chaque livraison contrôlée |
| L01 | Chargement, matériaux et rendu commun | Une fonction commune et ses contrôles GPU | Partiel ; éclairage et ombres restent à qualifier |
| L02 | Conversion Dungeon | 5 prefabs, puis contrôle hiérarchie/matériaux/collisions | Partiel |
| L03 | Conversion Townsmith, House, Villa | Un matériau ; prefabs associés ; House 16 objets ; Villa 5 scènes | Actif : Townsmith |
| L04 | Végétation et shaders | Une famille : alpha, normales, vent, ombres, LOD | Partiel |
| L05 | Personnages, rigs, animations | Un squelette avec ses clips | À terminer |
| L06 | Effets FirePit, Blood, Dust | Un effet : état initial, évolution, extinction | Partiel ; comparaison temporelle/runtime ouverte |
| L07 | Sprites, terrains, audio | Une famille de formats et ses dépendances | À traiter |
| L08 | Bibliothèque : images, métadonnées, recherche | 8 fiches et 16 images contrôlées individuellement | HOUSE.05 livré au lecteur natif ; 918 fiches source sans image |
| L09 | Vérification finale dans Studio | Recherche → filtres → aperçu → placement → édition → sauvegarde → réouverture → runtime | Ouvert ; réconciliation des 48 packs/variantes restante |

## Livraison de chaque tranche

Chaque tranche inscrit son ID, les éléments concernés, le résultat, les chemins sur F, la preuve et la prochaine action dans le journal. Un échec est conservé et reste ouvert. Une tranche indépendante peut avancer sans attendre sa résolution.

Les états sont séparés : **sources conservées → conversion → contrôles techniques → images et indexation → usage Studio → runtime qualifié**. Les contrôles GPU et les fiches source ne comptent pas comme des assets complets.

Prochaines livraisons : contrôle UV Townsmith, correction/comparaison du matériau, assemblage des prefabs, puis usage Studio. Les autres familles suivent leurs tranches ci-dessus ; leurs refus restent suivis séparément.

[Plan détaillé](PLAN_LOTS_A_SUIVRE.md) · [Journal des preuves](JOURNAL.md) · [Dernière comparaison vérifiée](../townsmith-door-sampling-controls-reopen-v23/RESULT.v1.json) · [Travail actif](../townsmith-door-source-uv-derivatives-v25/RUN-RESULT.v1.json).
'''
path = t / 'SUIVI_LISIBLE.md'
assert not path.exists()
path.write_text(body, encoding='utf-8')
p = t / 'SUIVI_SIMPLE.v1.json'
data = json.loads(p.read_text(encoding='utf-8-sig'))
snapshot = t / 'SUIVI_SIMPLE.before-dashboard-v1.json'
assert not snapshot.exists()
snapshot.write_bytes(p.read_bytes())
data.update(updated_utc=now, active_sub_lot='L03.TOWNSMITH.MATERIAUX.DERIVEES',
            current_work_receipt=str(running), state=live['status'],
            readable_dashboard=str(path), full_import=False, editor_visual_verified=False)
temporary = p.with_suffix('.dashboard.tmp')
temporary.write_text(json.dumps(data, ensure_ascii=False, indent=2), encoding='utf-8')
temporary.replace(p)
with (t / 'JOURNAL.md').open('a', encoding='utf-8') as journal:
    journal.write(f'\n- {now} — **Suivi par lots actualisé** : [tableau lisible](SUIVI_LISIBLE.md). UV v24 refusé au chargement du shader ; v25 lancé avec inclusion Common HDRP et erreurs détaillées. Aucun nouvel asset complet qualifié.\n')
print(json.dumps(dict(dashboard=str(path), active_receipt=str(running), status=live['status'])))
