"""Reopen source-only UV captures; never qualify native fidelity from them."""
from pathlib import Path
from datetime import datetime, timezone
import hashlib
import json
import numpy as np

w = Path('F:/OpenRPG_ImportWork')
source = w / 'townsmith-door-source-uv-derivatives-v25'
out = w / 'townsmith-door-source-uv-reopen-v25'
assert not out.exists()
out.mkdir()
def read(p): return json.loads(p.read_text(encoding='utf-8-sig'))
def sha(p):
    with p.open('rb') as stream: return hashlib.file_digest(stream, 'sha256').hexdigest()
pins = {}
result = dict(full_import=False, native_uv_comparison_verified=False, editor_visual_verified=False)
try:
    run = read(source / 'RUN-RESULT.v1.json')
    assert run['actual_exit_code'] == 0 and run['neutral_reference_byte_equal'] and run['source_inputs_unchanged']
    prep = read(source / 'PREPARED.v1.json')
    for key in ['input_pins', 'private_before_pins']:
        for p, expected in prep[key].items():
            assert sha(Path(p)) == expected, p
            pins[p] = expected
    rows = []
    for case in ['door-neutral', 'door-nopost-linear', 'door-noaniso-nopost-linear']:
        values = []
        for index in range(8):
            raw = source / f'{case}.uv-control-{index}.rgba32f'
            meta = source / f'{case}.uv-control-{index}.json'
            record = read(meta)
            assert record['mode'] == index % 2 and record['format'] == 'R32G32B32A32_SFloat'
            a = np.fromfile(raw, dtype='<f4').reshape(512, 512, 4)
            assert np.isfinite(a).all()
            values.append(a)
            pins[str(raw)] = sha(raw)
            pins[str(meta)] = sha(meta)
        assert np.array_equal(values[4], values[6]) and np.array_equal(values[5], values[7])
        depth = source / f'{case}.depth-3.r32f'
        mask = np.fromfile(depth, dtype='<f4').reshape(512, 512) > 0
        uv_mask = values[6][:, :, 3] == 1
        assert np.array_equal(mask, uv_mask), f'{case}: independent depth and UV coverage differ'
        assert int(mask.sum()) == 34445
        pins[str(depth)] = sha(depth)
        rows.append(dict(case=case, captures=8, last_two_per_mode_byte_equal=True,
                         depth_coverage_identical=True, covered_pixels=int(mask.sum()),
                         uv_min=values[6][mask, :2].min(axis=0).tolist(),
                         uv_max=values[6][mask, :2].max(axis=0).tolist(),
                         derivative_min=values[7][mask].min(axis=0).tolist(),
                         derivative_max=values[7][mask].max(axis=0).tolist()))
    for p in [source/'RUN-RESULT.v1.json', source/'NATIVE-RESULT.v1.json', Path(__file__)]: pins[str(p)] = sha(p)
    result.update(status='source_uv_and_derivatives_captured_reopened_and_depth_coverage_verified',
                  rows=rows, checked_pins=len(pins), input_pins=pins,
                  scope='Private original LOD0 submesh0 draw inside source camera; original reference unchanged. Native derivatives not yet compared.')
except Exception as exc:
    result.update(status='source_uv_reopen_refused', error=repr(exc), checked_pins=len(pins))
result['finished_utc'] = datetime.now(timezone.utc).isoformat()
(out/'RESULT.v1.json').write_text(json.dumps(result, indent=2), encoding='utf-8')
print(json.dumps({k:v for k,v in result.items() if k not in ['input_pins','rows']}))
if result['status'].endswith('refused'): raise SystemExit(1)
t = w/'SUIVI_LOTS_HIVEMIND'
message = f"\n- {result['finished_utc']} — **L03.TOWNSMITH.MATERIAUX.UV.SOURCE vérifié** : 24 captures float32, répétitions finales identiques et couverture UV/profondeur identique sur 34 445 pixels dans trois cas ; {len(pins)} épingles recontrôlées. Référence Unity originale inchangée. Comparaison des dérivées Forge restante ; aucun nouvel asset complet qualifié. [Preuve](../townsmith-door-source-uv-reopen-v25/RESULT.v1.json).\n"
for name in ['JOURNAL.md','AVANCEMENT_ACTUEL.md','SUIVI_LISIBLE.md']:
    with (t/name).open('a', encoding='utf-8') as stream: stream.write(message)
p = t/'SUIVI_SIMPLE.v1.json'
data = read(p)
data.update(updated_utc=result['finished_utc'], state=result['status'],
            last_technical_batch='L03.TOWNSMITH.MATERIAUX.UV.SOURCE', last_technical_proof=str(out/'RESULT.v1.json'),
            next_action='Capture native UV and derivatives, compare to the source without fitting thresholds; then finish material, prefab and Studio verification.')
tmp=p.with_suffix('.uv.tmp')
tmp.write_text(json.dumps(data,ensure_ascii=False,indent=2),encoding='utf-8')
tmp.replace(p)
