"""Record measured scene application without closing fidelity or editor checks."""
from pathlib import Path
from datetime import datetime, timezone
import json

W = Path('F:/OpenRPG_ImportWork')
T = W / 'SUIVI_LOTS_HIVEMIND'
proof = W / 'townsmith-paint-door-native-ambient-comparison-v1/RESULT.v1.json'
result = json.loads(proof.read_text(encoding='utf-8-sig'))
assert result['measured_gpu_ambient_applied'] and result['stable_native_readbacks'] >= 3
assert result['rehashed_pins'] == 412 and not result['full_import']
now = datetime.now(timezone.utc).isoformat()
key = 'L03.TOWNSMITH.DESSIN.02.APPLICATION'
marker = T / 'LAST_TOWNSMITH_AMBIENT_APPLICATION.v1.json'
assert not marker.exists()
summary = 'Buffer ambiant GPU source et LUT BRDF appliqués par le moteur commun ; trois captures identiques, 412 épingles contrôlées. Erreur RGB moyenne de la région fixe : 0,0488 -> 0,0313. Ciel, reflets, variante BRDF source et fidélité complète ouverts.'
for name in ('PLAN_LOTS_A_SUIVRE.v1.json', 'SUIVI_SIMPLE.v1.json'):
    path = T / name
    data = json.loads(path.read_text(encoding='utf-8-sig'))
    data.update(updated_utc=now, last_technical_batch=key, last_technical_proof=str(proof),
                current_work_sub_lot='L03.TOWNSMITH.DESSIN.02.CIEL',
                current_work_state=summary, current_work_receipt=str(W / 'townsmith-door-source-sky-capture-v8/RUN-RESULT.v1.json'),
                next_action='Capturer et réouvrir les faces et mips du cubemap HDRP réel, puis vérifier son application et reprendre la comparaison.',
                full_import=False)
    path.write_text(json.dumps(data, indent=2, ensure_ascii=False) + '\n', encoding='utf-8')
for name in ('AVANCEMENT_ACTUEL.md', 'PLAN_LOTS_A_SUIVRE.md', 'SUIVI_SIMPLE.md', 'JOURNAL.md'):
    with (T / name).open('a', encoding='utf-8') as stream:
        stream.write(f'\n- {now} — **{key} : application technique vérifiée**. {summary} Sous-lot actif : **DESSIN.02.CIEL**, capture Unity lancée. [Preuve](../townsmith-paint-door-native-ambient-comparison-v1/RESULT.v1.json) · [Comparaison](../townsmith-paint-door-native-ambient-comparison-v1/comparison.png). Aucun nouvel asset complet livré ; Studio non vérifié.\n')
marker.write_text(json.dumps({'lot': key, 'proof': str(proof), 'recorded_utc': now, 'full_import': False}, indent=2), encoding='utf-8')
print(json.dumps({'recorded': key, 'next': 'DESSIN.02.CIEL'}))
