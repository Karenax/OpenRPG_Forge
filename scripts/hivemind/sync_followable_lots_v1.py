"""Synchronize the followable plan with receipts without promoting qualification."""
from pathlib import Path
from datetime import datetime, timezone
import json

W = Path('F:/OpenRPG_ImportWork')
T = W / 'SUIVI_LOTS_HIVEMIND'
path = T / 'PLAN_LOTS_A_SUIVRE.v1.json'
original = path.read_bytes()
snapshot = T / 'PLAN_LOTS_A_SUIVRE.before-draw-01.json.saved'
if not snapshot.exists():
    snapshot.write_bytes(original)


def repair(value):
    if isinstance(value, list):
        return [repair(item) for item in value]
    if isinstance(value, dict):
        return {key: repair(item) for key, item in value.items()}
    if isinstance(value, str) and ('\u00c3' in value or '\u00c2' in value):
        try:
            return value.encode('latin1').decode('utf-8')
        except (UnicodeEncodeError, UnicodeDecodeError):
            return value
    return value


plan = repair(json.loads(original.decode('utf-8-sig')))
receipt = W / 'townsmith-paint-door-native-draw-reopen-v1/RESULT.v1.json'
assert receipt.exists()
plan.update(updated_utc=datetime.now(timezone.utc).isoformat(),
            last_technical_batch='L03.TOWNSMITH.DESSIN.01',
            last_technical_proof=str(receipt),
            current_batch='L03.TOWNSMITH.DESSIN.02',
            next_conversion='L03.TOWNSMITH.PREFABS.01', full_import=False,
            current_work_receipt=str(W / 'townsmith-door-source-render-context-v7/RUN-RESULT.v1.json'))
path.write_text(json.dumps(plan, indent=2, ensure_ascii=False) + '\n', encoding='utf-8')
current = T / 'AVANCEMENT_ACTUEL.md'
text = current.read_text(encoding='utf-8')
marker = '\n## Sous-lot actif et prochaine livraison\n'
text = text.split(marker)[0]
state = json.loads((W / 'townsmith-door-source-render-context-v7/RUN-RESULT.v1.json').read_text(encoding='utf-8-sig'))
text += marker + '''
**L03.TOWNSMITH.DESSIN.01 est clos techniquement** : dessin réel, trois captures GPU identiques et 266 épingles réouvertes. La fidélité Unity, les prefabs et l'éditeur restent ouverts.

**L03.TOWNSMITH.DESSIN.02 est actif** : capture du buffer GPU ambiant dans l'historique réel de la caméra HDRP, puis comparaison de scène sans ajuster les seuils. État du dernier travail Unity : `''' + state['status'] + '''`.

[Preuve DESSIN.01](../townsmith-paint-door-native-draw-reopen-v1/RESULT.v1.json) · [Travail DESSIN.02](../townsmith-door-source-render-context-v7/RUN-RESULT.v1.json) · [Comparaison diagnostique](../townsmith-paint-door-native-draw-comparison-v2/comparison.png).

Aucun nouvel asset complet qualifié par ces contrôles. Les lots d'effets L06 et le parcours Studio L09 restent à terminer.
'''
current.write_text(text, encoding='utf-8')
print(json.dumps({'current_batch': plan['current_batch'], 'unity_state': state['status'], 'full_import': False}))
