"""Retain the observed sky variation instead of declaring stable reflection parity."""
from pathlib import Path
from datetime import datetime, timezone
import hashlib
import json
import numpy as np

W = Path('F:/OpenRPG_ImportWork')
SOURCE = W / 'townsmith-door-source-sky-capture-v8'
OUT = W / 'townsmith-door-source-sky-capture-reopen-v8/CONTROL-DIFFERENCES.v1.json'
assert not OUT.exists()
rows = []
pins = {}
for case in ('door-neutral', 'door-fixedbackground-linear'):
    for mip in range(9):
        paths = [SOURCE / f'{name}.sky-mip-{mip}.rgba32f' for name in ('door-linear', case)]
        first, second = [np.fromfile(path, dtype='<f4') for path in paths]
        delta = np.abs(first - second)
        rows.append(dict(case=case, mip=mip, different_channels=int(np.count_nonzero(delta)), max_error=float(delta.max()), mean_error=float(delta.mean())))
        for path in paths:
            with path.open('rb') as stream:
                pins[str(path)] = hashlib.file_digest(stream, 'sha256').hexdigest()
now = datetime.now(timezone.utc).isoformat()
OUT.write_text(json.dumps(dict(status='five_channels_in_neutral_control_cube_differ_stability_unqualified', finished_utc=now,
                              input_pins=pins, rows=rows, stability_qualified=False, thresholds_relaxed=False,
                              next_action='Repeat actual source cube captures to distinguish recomputation variation from acquisition timing before qualifying reflection parity.',
                              full_import=False), indent=2), encoding='utf-8')
T = W / 'SUIVI_LOTS_HIVEMIND'
with (T / 'JOURNAL.md').open('a', encoding='utf-8') as stream:
    stream.write(f'\n- {now} — Contrôle ciel : cinq canaux diffèrent dans les mips 2/3 du contrôle Neutral (écart max 0,5) ; les deux captures linéaires sont byte-equal. Stabilité intercontrôles non qualifiée, aucun seuil ajusté. [Diagnostic](../townsmith-door-source-sky-capture-reopen-v8/CONTROL-DIFFERENCES.v1.json). Répéter la capture source avant de qualifier les reflets.\n')
with (T / 'AVANCEMENT_ACTUEL.md').open('a', encoding='utf-8') as stream:
    stream.write('\nContrôle de stabilité du ciel encore ouvert : cinq canaux diffèrent entre Neutral et linéaire aux mips 2/3 ; les captures linéaires sont identiques. Prochaine action : répétition source, puis orientations et application des reflets.\n')
print(json.dumps({'status': 'sky_control_variation_recorded', 'stability_qualified': False}))
