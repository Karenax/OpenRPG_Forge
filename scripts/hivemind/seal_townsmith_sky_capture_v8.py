"""Reopen all actual GPU cube layers; half conversion must roundtrip exactly."""
from pathlib import Path
from datetime import datetime, timezone
import hashlib
import json
import numpy as np

W = Path('F:/OpenRPG_ImportWork')
SOURCE = W / 'townsmith-door-source-sky-capture-v8'
OUT = W / 'townsmith-door-source-sky-capture-reopen-v8'
assert not OUT.exists()


def read(path):
    return json.loads(path.read_text(encoding='utf-8-sig'))


def sha(path):
    with Path(path).open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


run = read(SOURCE / 'RUN-RESULT.v1.json')
assert run['actual_exit_code'] == 0 and run['source_inputs_unchanged'] and run['neutral_reference_byte_equal']
assert sha(SOURCE / 'NATIVE-RESULT.v1.json') == run['native_receipt_sha256']
prepared = read(SOURCE / 'PREPARED.v1.json')
pins = dict(prepared['input_pins'])
pins.update(prepared['private_before_pins'])
for path, expected in pins.items():
    assert sha(path) == expected, path
native = read(SOURCE / 'door-linear.context.json')
assert native['actual_camera_history'] == 'CustomUserHistory0-existing'
assert native['sky_cubemap_count'] == 1 and native['sky_mip_count'] == 9
assert native['sky_source_format'] == 'RGBAHalf'
half_chunks = []
rows = []
controls_equal = True
for mip in range(9):
    side = max(1, 256 >> mip)
    path = SOURCE / f'door-linear.sky-mip-{mip}.rgba32f'
    assert path.stat().st_size == 6 * side * side * 16
    assert native['sky_layer_counts'][mip] == 6
    assert native['sky_layer_bytes'][mip] == side * side * 16
    values = np.fromfile(path, dtype='<f4')
    assert np.isfinite(values).all()
    half = values.astype('<f2')
    assert np.array_equal(half.astype('<f4'), values), 'Source half roundtrip differs'
    half_chunks.append(half.tobytes())
    rows.append(dict(mip=mip, width=side, layers=6, rgba32f_bytes=path.stat().st_size, min=float(values.min()), max=float(values.max()), sha256=sha(path)))
    for case in ('door-neutral', 'door-linear', 'door-fixedbackground-linear'):
        control = SOURCE / f'{case}.sky-mip-{mip}.rgba32f'
        pins[str(control)] = sha(control)
        controls_equal &= sha(control) == sha(path)
for case in ('door-neutral', 'door-linear', 'door-fixedbackground-linear'):
    for suffix in ('.context.json', '.rgba16f'):
        path = SOURCE / f'{case}{suffix}'
        pins[str(path)] = sha(path)
assert sha(SOURCE / 'door-linear.rgba16f') == sha(W / 'townsmith-door-source-render-context-v7/door-linear.rgba16f')
for name in ('RUN-RESULT.v1.json', 'NATIVE-RESULT.v1.json', 'PREPARED.v1.json'):
    path = SOURCE / name
    pins[str(path)] = sha(path)
pins[str(Path(__file__))] = sha(Path(__file__))
OUT.mkdir()
storage = OUT / 'actual-source-sky.mip-major.rgba16f'
storage.write_bytes(b''.join(half_chunks))
assert storage.stat().st_size == 8388576 // 2
pins[str(storage)] = sha(storage)
now = datetime.now(timezone.utc).isoformat()
result = dict(status='actual_hdrp_sky_cube_all_layers_mips_reopened', lot='L03.TOWNSMITH.DESSIN.02.CIEL.CAPTURE', finished_utc=now,
              input_pins=pins, rehashed_source_pins=len(prepared['input_pins']) + len(prepared['private_before_pins']),
              actual_camera_history='CustomUserHistory0-existing', cube_count=1, width=256, mip_count=9,
              faces_per_mip=6, captured_files=27, source_format='RGBAHalf', storage=str(storage), storage_sha256=sha(storage),
              storage_bytes=storage.stat().st_size, mips=rows, lossless_float32_to_native_half_roundtrip=True,
              three_controls_cube_byte_equal=bool(controls_equal), source_render_reference_unchanged=True,
              source_face_orientation_verified=False, actual_shader_binding_timing_independently_verified=False,
              source_background_sky_resource_equivalence_verified=False,
              forge_sky_application_verified=False, source_render_parity_qualified=False,
              forge_editor_verified=False, full_import=False, complete_assets_delivered_increment=0)
(OUT / 'RESULT.v1.json').write_text(json.dumps(result, indent=2, ensure_ascii=False) + '\n', encoding='utf-8')
T = W / 'SUIVI_LOTS_HIVEMIND'
for name in ('AVANCEMENT_ACTUEL.md', 'PLAN_LOTS_A_SUIVRE.md', 'JOURNAL.md'):
    with (T / name).open('a', encoding='utf-8') as stream:
        stream.write(f'\n- {now} — **DESSIN.02.CIEL.CAPTURE vérifié** : six faces et neuf mipmaps du cubemap GPU HDRP réel ; stockage natif half réversible de {storage.stat().st_size:,} octets. Référence et sources inchangées. Suite : vérifier les orientations et raccorder les reflets ; le ciel visible doit être vérifié séparément. [Preuve](../townsmith-door-source-sky-capture-reopen-v8/RESULT.v1.json). Aucun asset complet supplémentaire.\n')
for name in ('PLAN_LOTS_A_SUIVRE.v1.json', 'SUIVI_SIMPLE.v1.json'):
    path = T / name
    data = read(path)
    data.update(updated_utc=now, last_technical_batch='L03.TOWNSMITH.DESSIN.02.CIEL.CAPTURE', last_technical_proof=str(OUT / 'RESULT.v1.json'),
                current_work_sub_lot='L03.TOWNSMITH.DESSIN.02.REFLETS',
                current_work_state='Ambiant GPU appliqué et cubemap HDRP réel capturé : six faces, neuf mipmaps. Orientations, reflets, ciel visible et fidélité ouverts.',
                next_action='Vérifier les orientations et le raccordement des reflets, identifier la ressource du ciel visible puis comparer de nouveau.', full_import=False)
    path.write_text(json.dumps(data, indent=2, ensure_ascii=False) + '\n', encoding='utf-8')
print(json.dumps({key: value for key, value in result.items() if key not in ('input_pins', 'mips')}))
