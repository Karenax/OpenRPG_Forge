"""Refresh the current heading while preserving the chronological evidence below."""
from pathlib import Path
from datetime import datetime, timezone
import json
w=Path('F:/OpenRPG_ImportWork')
t=w/'SUIVI_LOTS_HIVEMIND'
source=w/'townsmith-door-source-fine-uv-v28/RUN-RESULT.v1.json'
native=w/'townsmith-native-fine-uv-batch-v28/BATCH.v1.json'
proof=w/'townsmith-depth-bound-uv-comparison-v27/RESULT.v1.json'
def read(p): return json.loads(p.read_text(encoding='utf-8-sig'))
verified=read(proof)
assert verified['status']=='native_uv_and_derivatives_compared_to_source'
source_state=read(source);native_state=read(native)
p=t/'SUIVI_LISIBLE.md'
text=p.read_text(encoding='utf-8')
start=text.index('## Travail actif')
end=text.index('## Découpage et ordre')
section=f'''## Travail actif

**L03 · Townsmith · matériau de la porte : dérivées UV et échantillonnage.**

Dernière comparaison vérifiée : UV source attachés à la profondeur opaque réelle et six canaux Forge, 1 005 épingles recontrôlées. Écart moyen U : {verified['rows'][0]['mean_absolute_error']:.9g} ; V : {verified['rows'][1]['mean_absolute_error']:.9g}. La référence source originale est conservée. Le diagnostic antérieur sans profondeur est conservé comme témoin ; il ne mesure pas toujours la face visible.

Sous-lot suivant : dérivées fines explicites dans les deux backends, pour isoler le choix du quad après inversion verticale de la projection. Unity : `{source_state['status']}`. Forge : `{native_state['status']}`, {len(native_state['completed_channels'])}/4 canaux terminés au moment de cette mise à jour. Les contrôles diagnostics ne changent pas les échantillonnages implicites du matériau original.

**Matériau complet, assemblage des prefabs et usage Studio ouverts. Aucun nouvel asset complet qualifié par ces contrôles.**

[Dernière preuve](../townsmith-depth-bound-uv-comparison-v27/RESULT.v1.json) · [Capture Unity en cours](../townsmith-door-source-fine-uv-v28/RUN-RESULT.v1.json) · [Contrôles Forge](../townsmith-native-fine-uv-batch-v28/BATCH.v1.json).

'''
text=text[:start]+section+text[end:]
p.write_text(text,encoding='utf-8')
p=t/'SUIVI_SIMPLE.v1.json'
data=read(p)
data.update(updated_utc=datetime.now(timezone.utc).isoformat(),active_sub_lot='L03.TOWNSMITH.MATERIAUX.UV.DERIVEES.FINES',
            current_work_receipt=str(native),current_source_work_receipt=str(source),
            state=native_state['status'],next_action='Compare explicit fine derivatives after depth-bound source capture; preserve original material sampling until causal evidence supports a correction.')
tmp=p.with_suffix('.uv28.tmp');tmp.write_text(json.dumps(data,ensure_ascii=False,indent=2),encoding='utf-8');tmp.replace(p)
print(json.dumps(dict(source=source_state['status'],native=native_state['status'],completed_native_channels=len(native_state['completed_channels']))))
