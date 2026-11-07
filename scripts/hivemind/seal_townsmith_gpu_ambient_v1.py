"""Close the measured ambient capture, not visual equivalence or scene delivery."""
from pathlib import Path
from datetime import datetime, timezone
import hashlib
import json
import math

W = Path('F:/OpenRPG_ImportWork')
ROOT = W / 'townsmith-door-source-render-context-v7'
OUT = W / 'townsmith-door-source-gpu-ambient-reopen-v1'
assert not OUT.exists()


def read(path):
    return json.loads(path.read_text(encoding='utf-8-sig'))


def sha(path):
    with Path(path).open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


run = read(ROOT / 'RUN-RESULT.v1.json')
assert run['actual_exit_code'] == 0 and run['neutral_reference_byte_equal'] and run['source_inputs_unchanged']
assert sha(ROOT / 'NATIVE-RESULT.v1.json') == run['native_receipt_sha256']
prepared = read(ROOT / 'PREPARED.v1.json')
pins = dict(prepared['input_pins'])
pins.update(prepared['private_before_pins'])
for path in ROOT.iterdir():
    if path.is_file() and path.suffix in ('.json', '.rgba16f', '.png'):
        pins[str(path)] = sha(path)
pins[str(Path(__file__))] = sha(Path(__file__))
for path, expected in pins.items():
    assert sha(path) == expected, path
contexts = [read(ROOT / f'{case}.context.json') for case in ('door-neutral', 'door-linear', 'door-fixedbackground-linear')]
coefficients = contexts[0]['actual_gpu_ambient_coefficients']
assert len(coefficients) == 28 and all(math.isfinite(value) for value in coefficients)
assert any(value != 0 for value in coefficients)
for context in contexts:
    assert context['actual_camera_history'] == 'CustomUserHistory0-existing'
    assert context['ambient_buffer_count'] == 7 and context['ambient_buffer_stride'] == 16
    assert context['actual_gpu_ambient_coefficients'] == coefficients
    assert all(value == 0 for value in context['ambient_probe'])
assert sha(ROOT / 'door-linear.rgba16f') == sha(W / 'townsmith-door-source-render-context-v6/door-linear.rgba16f')
OUT.mkdir()
now = datetime.now(timezone.utc).isoformat()
result = {
    'status': 'actual_hdrp_ambient_gpu_buffer_captured_and_reopened',
    'lot': 'L03.TOWNSMITH.DESSIN.02.CAPTURE', 'finished_utc': now,
    'input_pins': pins, 'rehashed_pins': len(pins),
    'source_reference_unchanged': True, 'source_inputs_unchanged': True,
    'buffer_count': 7, 'buffer_stride': 16, 'coefficients': coefficients,
    'three_source_controls_same_gpu_buffer': True,
    'cpu_camera_probe_zero_gpu_buffer_nonzero': True,
    'actual_gpu_ambient_buffer_captured': True,
    'actual_shader_binding_timing_independently_verified': False,
    'actual_sky_pixels_captured': False,
    'forge_application_verified': False, 'source_render_parity_qualified': False,
    'forge_editor_verified': False, 'full_import': False,
    'complete_assets_delivered_increment': 0,
    'next_action': 'Use the measured GPU ambient data in the common scene path, capture the sky resources and compare without fitted thresholds.',
}
(OUT / 'RESULT.v1.json').write_text(json.dumps(result, indent=2, ensure_ascii=False) + '\n', encoding='utf-8')
tracking = W / 'SUIVI_LOTS_HIVEMIND'
with (tracking / 'JOURNAL.md').open('a', encoding='utf-8') as stream:
    stream.write(f'\n- {now} — L03.TOWNSMITH.DESSIN.02.CAPTURE : 28 coefficients GPU non nuls identiques pour les trois contrôles, sonde CPU nulle ; sources et référence inchangées. Capture close, application Forge et fidélité ouvertes. [Preuve](../townsmith-door-source-gpu-ambient-reopen-v1/RESULT.v1.json).\n')
for name in ('AVANCEMENT_ACTUEL.md', 'PLAN_LOTS_A_SUIVRE.md'):
    with (tracking / name).open('a', encoding='utf-8') as stream:
        stream.write('\nDESSIN.02.CAPTURE vérifié : buffer ambiant GPU de 28 coefficients non nuls, identique dans trois contrôles ; la sonde CPU est nulle. Prochaine tranche : application des données GPU dans Forge et capture du ciel. [Preuve](../townsmith-door-source-gpu-ambient-reopen-v1/RESULT.v1.json). La fidélité et Studio restent ouverts.\n')
print(json.dumps({'status': result['status'], 'rehashed_pins': len(pins), 'full_import': False}))
