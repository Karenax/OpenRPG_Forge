"""Reopen the source lighting batch and publish its scoped lot receipt."""
from pathlib import Path
from datetime import datetime, timezone
import hashlib
import json
import numpy as np

W = Path('F:/OpenRPG_ImportWork')
ROOT = W / 'townsmith-door-source-direct-controls-v18'
OUT = W / 'townsmith-door-source-direct-controls-reopen-v18'
T = W / 'SUIVI_LOTS_HIVEMIND'


def read(path):
    return json.loads(path.read_text(encoding='utf-8-sig'))


def sha(path):
    with Path(path).open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


assert not OUT.exists()
run = read(ROOT / 'RUN-RESULT.v1.json')
assert run['actual_exit_code'] == 0 and run['cases_captured'] == 21
assert run['neutral_reference_byte_equal'] and run['source_inputs_unchanged']
assert sha(ROOT / 'NATIVE-RESULT.v1.json') == run['native_receipt_sha256']
prepared = read(ROOT / 'PREPARED.v1.json')
pins = dict(prepared['input_pins'])
pins.update(prepared['private_before_pins'])
for path in ROOT.iterdir():
    if path.is_file() and path.suffix in ('.json', '.rgba16f', '.rgba32f'):
        pins[str(path)] = sha(path)
for path, expected in pins.items():
    assert sha(path) == expected, path
prior = W / 'townsmith-door-source-bloom-controls-v17'
assert sha(ROOT / 'door-linear.rgba16f') == sha(prior / 'door-linear.rgba16f')
pins[str(prior / 'door-linear.rgba16f')] = sha(prior / 'door-linear.rgba16f')
rows = []
for mode in ('DirectDiffuseLighting', 'DirectSpecularLighting', 'IndirectDiffuseLighting', 'ReflectionLighting'):
    case = 'door-debug-' + mode + '-nopost-linear'
    context = read(ROOT / (case + '.context.json'))
    assert not context['actual_postprocess_enabled']
    assert context['actual_debug_lighting_mode'] == mode
    assert context['actual_exposure_control_enabled']
    assert context['exposure_texture_present'] == [False, True, True]
    assert context['exposure_texture_rgba'][4] == context['exposure_texture_rgba'][8] == 0.0032552082557231188
    path = ROOT / (case + '.rgba16f')
    assert path.stat().st_size == 512 * 512 * 8
    pixels = np.fromfile(path, dtype='<f2').reshape(512, 512, 4)
    assert np.isfinite(pixels).all()
    rows.append(dict(case=case, sha256=sha(path), postprocess_enabled=False,
                     source_gpu_exposure=context['exposure_texture_rgba'][4],
                     source_color_buffer_format=context['actual_color_buffer_format']))
assert len({row['sha256'] for row in rows}) == 4
pins[str(Path(__file__))] = sha(Path(__file__))
now = datetime.now(timezone.utc).isoformat()
result = dict(status='four_native_lighting_components_without_postprocess_reopened',
              lot='L03.TOWNSMITH.DESSIN.02.TRANSPORT.DIRECT.CAPTURE', finished_utc=now,
              input_pins=pins, rehashed_pins=len(pins), rows=rows,
              source_inputs_unchanged=True, original_linear_reference_byte_equal=True,
              native_debug_restored_by_helper=True, full_material_fidelity_qualified=False,
              source_render_parity_qualified=False, forge_editor_verified=False,
              full_import=False, complete_assets_delivered_increment=0,
              next_action='Compare native direct lighting with these source controls; qualify intermediate precision, BRDF and occlusion separately from bloom.')
OUT.mkdir()
(OUT / 'RESULT.v1.json').write_text(json.dumps(result, indent=2, ensure_ascii=False), encoding='utf-8')
summary = 'Quatre composantes HDRP sans post-traitement capturées et réouvertes ; exposition GPU identique, référence originale et sources inchangées. Comparaison Forge, bloom, prefabs et Studio restent ouverts.'
for name in ('AVANCEMENT_ACTUEL.md', 'PLAN_LOTS_A_SUIVRE.md', 'JOURNAL.md'):
    with (T / name).open('a', encoding='utf-8') as stream:
        stream.write(f'\n- {now} — **TRANSPORT.DIRECT.CAPTURE : contrôle source vérifié**. {summary} [Preuve](../{OUT.name}/RESULT.v1.json). Aucun nouvel asset complet livré.\n')
for name in ('PLAN_LOTS_A_SUIVRE.v1.json', 'SUIVI_SIMPLE.v1.json'):
    path = T / name
    data = read(path)
    data.update(updated_utc=now, last_technical_batch=result['lot'], last_technical_proof=str(OUT / 'RESULT.v1.json'),
                current_work_sub_lot='L03.TOWNSMITH.DESSIN.02.TRANSPORT.DIRECT.COMPARAISON', current_work_state=summary,
                next_action=result['next_action'], full_import=False)
    path.write_text(json.dumps(data, indent=2, ensure_ascii=False) + '\n', encoding='utf-8')
print(json.dumps(dict(status=result['status'], pins=len(pins), complete_assets_delivered_increment=0)))
