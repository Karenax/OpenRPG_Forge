"""Verify independent source runs without conflating different tonemapping controls."""
from pathlib import Path
from datetime import datetime, timezone
import hashlib
import json

W = Path('F:/OpenRPG_ImportWork')
OUT = W / 'townsmith-door-source-sky-repeat-reopen-v9'
assert not OUT.exists()


def read(path):
    return json.loads(path.read_text(encoding='utf-8-sig'))


def sha(path):
    with Path(path).open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


pins = {}
for version in (8, 9):
    root = W / f'townsmith-door-source-sky-capture-v{version}'
    state = read(root / 'RUN-RESULT.v1.json')
    assert state['actual_exit_code'] == 0 and state['source_inputs_unchanged'] and state['neutral_reference_byte_equal']
    assert sha(root / 'NATIVE-RESULT.v1.json') == state['native_receipt_sha256']
    prepared = read(root / 'PREPARED.v1.json')
    pins.update(prepared['input_pins'])
    pins.update(prepared['private_before_pins'])
    for path in root.iterdir():
        if path.is_file() and path.suffix in ('.json', '.rgba32f', '.rgba16f'):
            pins[str(path)] = sha(path)
rows = []
for case in ('door-neutral', 'door-linear', 'door-fixedbackground-linear'):
    hashes = []
    for mip in range(9):
        first = W / f'townsmith-door-source-sky-capture-v8/{case}.sky-mip-{mip}.rgba32f'
        second = W / f'townsmith-door-source-sky-capture-v9/{case}.sky-mip-{mip}.rgba32f'
        assert sha(first) == sha(second), f'Independent source run differs: {case}, mip {mip}'
        hashes.append(sha(first))
    assert sha(W / f'townsmith-door-source-sky-capture-v8/{case}.rgba16f') == sha(W / f'townsmith-door-source-sky-capture-v9/{case}.rgba16f')
    rows.append(dict(case=case, mip_hashes=hashes, all_nine_mips_byte_equal_across_runs=True, rendered_reference_byte_equal_across_runs=True))
pins[str(Path(__file__))] = sha(Path(__file__))
for path, expected in pins.items():
    assert sha(path) == expected, path
OUT.mkdir()
now = datetime.now(timezone.utc).isoformat()
result = dict(status='three_source_controls_reproduced_byte_equal_in_independent_runs', finished_utc=now,
              lot='L03.TOWNSMITH.DESSIN.02.CIEL.REPETITION', input_pins=pins, rehashed_pins=len(pins), rows=rows,
              same_case_independent_reproduction_verified=True, different_cases_cube_equivalence_verified=False,
              between_neutral_and_linear_variation_cause_qualified=False,
              source_face_orientation_verified=False, forge_reflection_application_verified=False,
              source_render_parity_qualified=False, forge_editor_verified=False, full_import=False,
              complete_assets_delivered_increment=0)
(OUT / 'RESULT.v1.json').write_text(json.dumps(result, indent=2), encoding='utf-8')
T = W / 'SUIVI_LOTS_HIVEMIND'
for name in ('AVANCEMENT_ACTUEL.md', 'PLAN_LOTS_A_SUIVRE.md', 'JOURNAL.md'):
    with (T / name).open('a', encoding='utf-8') as stream:
        stream.write(f'\n- {now} — **DESSIN.02.CIEL.REPETITION vérifié** : les neuf mipmaps et images de chaque contrôle sont byte-equal entre deux exécutions Unity indépendantes. Les cinq différences entre Neutral et linéaire sont reproductibles ; leur cause reste ouverte. [Preuve](../townsmith-door-source-sky-repeat-reopen-v9/RESULT.v1.json). Mode HDRP/BRDF en cours de capture avant application des reflets.\n')
print(json.dumps({'status': result['status'], 'rehashed_pins': len(pins), 'full_import': False}))
