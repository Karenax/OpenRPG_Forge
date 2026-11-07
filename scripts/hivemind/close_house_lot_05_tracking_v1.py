"""Record the completed source-image lot without claiming asset/runtime delivery."""
from pathlib import Path
import json
import subprocess
import sys
from datetime import datetime, timezone

W = Path('F:/OpenRPG_ImportWork')
T = W / 'SUIVI_LOTS_HIVEMIND'
S = Path(__file__).parent
proof = W / 'house-source-thumbnail-batch-0025-0032-delivery-reopen-v1/RESULT.v1.json'
result = json.loads(proof.read_text(encoding='utf-8'))
assert result['cards_updated'] == 8 and result['source_images_installed'] == 16
assert result['missing_primary_images_after'] == 918 and not result['full_import']
p = T / 'PLAN_LOTS_A_SUIVRE.md'
text = p.read_text(encoding='utf-8')
text = text.replace('**L08.IMAGE.HOUSE.05 — prochain lot lancé**', '**L08.IMAGE.HOUSE.05 — terminé pour les images et le lecteur natif**')
text = text.replace('huit fiches encore sans image, seize vues originales. Vérifier chaque vue avant d\'indexer ; conserver les refus précédents.', 'huit fiches mises à jour, seize vues originales revues et indexées ; recherche, filtres, détails et PNG vérifiés. Les refus précédents restent conservés.')
text = text.replace('tranche HOUSE.04 ; 926', 'tranche HOUSE.05 ; 918')
text = text.replace('Actif : huit fiches House', 'HOUSE.05 livré : huit fiches / seize images')
text += '\n[Livraison HOUSE.05](../house-source-thumbnail-batch-0025-0032-delivery-reopen-v1/RESULT.v1.json) : 926 → 918 fiches source sans image. Prochain lot de conversion : **L03.TOWNSMITH.RESSOURCES**.\n'
p.write_text(text, encoding='utf-8')
data = json.loads((T / 'PLAN_LOTS_A_SUIVRE.v1.json').read_text(encoding='utf-8'))
data.update(updated_utc=datetime.now(timezone.utc).isoformat(), current_batch='L03.TOWNSMITH.RESSOURCES',
            last_delivered_batch='L08.IMAGE.HOUSE.05', last_delivery_proof=str(proof), missing_source_primary_images=918)
(T / 'PLAN_LOTS_A_SUIVRE.v1.json').write_text(json.dumps(data, indent=2, ensure_ascii=False), encoding='utf-8')
with (T / 'JOURNAL.md').open('a', encoding='utf-8') as f:
    f.write('\n- L08.IMAGE.HOUSE.05 livré : huit fiches, seize images ; sources, licences et autres fiches inchangées. Lecteur natif PASS. 926 → 918 fiches source sans image. Aucun nouvel asset complet qualifié runtime ; contrôle visuel Studio restant.\n')
subprocess.run([sys.executable, '-B', str(S / 'record_batch_progress_v1.py')], check=True, stdout=subprocess.DEVNULL)
subprocess.run([sys.executable, '-B', str(S / 'seal_townsmith_material_packet_v1.py'), 'refresh'], check=True)
print(json.dumps(dict(lot='L08.IMAGE.HOUSE.05', cards=8, images=16, missing_primary_images=918, next='L03.TOWNSMITH.RESSOURCES')))
