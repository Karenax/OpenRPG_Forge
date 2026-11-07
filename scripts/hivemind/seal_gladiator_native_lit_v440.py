"""Reopen full native fragments and logs; do not claim material render parity."""
from pathlib import Path
from datetime import datetime, timezone
import hashlib
import json

W = Path('F:/OpenRPG_ImportWork')
S = Path(__file__).resolve().parent
O = W / 'gladiator-native-lit-delivery-v440'
assert not O.exists()

def read(p):
    return json.loads(Path(p).read_text('utf-8-sig'))

def sha(p):
    with Path(p).open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()

p = W / 'gladiator-native-lit-fragments-v439/RESULT.v1.json'
proof = read(p)
assert proof['native_lit_programs_composed'] == 6 and not proof['failures']
assert proof['actual_positive_compositions'] == 46
pins = {str(p): sha(p), str(Path(__file__)): sha(Path(__file__))}
for path, digest in proof['input_pins'].items():
    assert sha(path) == digest, path
    pins[path] = digest
contracts = read(W / 'gladiator-render-contracts-v432/RESULT.v1.json')
by_id = {r['material_guid']: r for r in contracts['rows']}
for row in proof['rows']:
    c = by_id[row['material_guid']]
    assert row['original_alpha_to_mask'] == bool(c['native_resolved_state']['_AlphaCutoffEnable'])
    assert not row['original_alpha_to_mask_forward'] and not row['original_alpha_to_mask_shadow']
    for stage in ['forward', 'prepass']:
        path = Path(row[stage])
        assert sha(path) == row[stage + '_sha256']
        pins[str(path)] = row[stage + '_sha256']
        text = path.read_text('utf-8')
        assert 'out.position=source_graph_depth_position(in,0.0);' in text
        assert ('if surface.alpha < surface.alpha_clip_threshold { discard; }' in text) == c['source_keyword_alpha_test']
    expected = 8 if row['original_alpha_to_mask'] else 7
    assert len(row['actual_compositions']) == expected
    for run in row['actual_compositions']:
        assert run['actual_exit_code'] == 0
        for lane in ['stdout', 'stderr']:
            path = Path(row['forward']).parent / (run['permutation'] + '.' + lane + '.log')
            assert sha(path) == run[lane + '_sha256']
            pins[str(path)] = sha(path)
O.mkdir()
result = dict(status='six_full_native_lit_fragment_pairs_composed_and_reopened',
    observed_utc=datetime.now(timezone.utc).isoformat(), rows=proof['rows'], input_pins=pins,
    materials=6, actual_positive_compositions=46, depth_alpha_to_coverage_materials=4,
    forward_alpha_to_coverage_materials=0, shadow_alpha_to_coverage_materials=0,
    original_equations_and_backface_constants_preserved=True,
    renderer_installed=False, gpu_rendered=False, full_import=False,
    complete_asset_conversion_increment=0,
    next_work='Assemble autonomous native GLBs with geometry, hierarchy, imported texture mip storage and original pass state; verify canonical admission and actual mesh draws.')
(O / 'RESULT.v1.json').write_text(json.dumps(result, indent=2), 'utf-8')
note = '''## Gladiator — fragments Lit complets composés, 10 octobre 2026

6 paires de fragments couleur/profondeur : 46 compositions Bevy réussies et journaux réouverts. Les normales arrière utilisent les constantes originales [1,1,-1]. La couverture alpha des quatre matériaux de drapeaux est conservée pour la profondeur ; les passes couleur et ombre n'activent pas cette couverture dans la source générée. Aucun terminal de déplacement de profondeur n'est inventé.

Preuve : F:\\OpenRPG_ImportWork\\gladiator-native-lit-delivery-v440\\RESULT.v1.json
Prochaine étape : GLB autonomes, admission des états dans le moteur, dessins GPU comparés, puis bibliothèque/éditeur. Ces compositions ne prouvent pas encore le rendu des matériaux. Aucun nouvel asset complet qualifié.

'''
(O / 'LOT.md').write_text(note, 'utf-8')
for name in ['AVANCEMENT_ACTUEL.md', 'POINT_DE_REPRISE.md']:
    path = W / 'SUIVI_LOTS_HIVEMIND' / name
    before = path.read_bytes()
    (O / (name + '.before')).write_bytes(before)
    path.write_bytes(note.encode('utf-8') + before)
print(json.dumps(dict(status=result['status'], materials=6, compositions=46)))
