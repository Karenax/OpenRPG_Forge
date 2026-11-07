"""Reopen composed House fragments and retain explicit raster qualification gaps."""
from pathlib import Path
import datetime
import hashlib
import json

W = Path('F:/OpenRPG_ImportWork')
O = W / 'house-native-lit-checkpoint-v663'
assert not O.exists()

def read(path):
    return json.loads(Path(path).read_text('utf-8-sig'))

def sha(path):
    with Path(path).open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()

proof_path = W / 'house-native-lit-fragments-v662/RESULT.v1.json'
contracts_path = W / 'house-render-contracts-v661/RESULT.v1.json'
proof, contracts = read(proof_path), read(contracts_path)
assert proof['native_lit_programs_composed'] == 6 and not proof['failures']
assert proof['actual_positive_compositions'] == 42
pins = {str(p): sha(p) for p in [proof_path, contracts_path, Path(__file__).resolve()]}
for receipt in [proof, contracts]:
    for path, digest in receipt['input_pins'].items():
        assert sha(path) == digest, path
        pins[path] = digest
by_id = {r['material_guid']: r for r in contracts['rows']}
for row in proof['rows']:
    contract = by_id[row['material_guid']]
    assert contract['source_depth_offset_enabled']
    assert '_DEPTHOFFSET_ON' in contract['source_valid_keywords']
    assert not contract['source_keyword_alpha_test'] and not contract['source_keyword_transparent']
    for stage in ['forward', 'prepass']:
        path = Path(row[stage])
        assert sha(path) == row[stage + '_sha256']
        text = path.read_text('utf-8-sig')
        assert 'out.position=source_graph_depth_position(in,surface.depth_offset);' in text
        assert 'out.position=source_graph_depth_position(in,0.0);' not in text
        assert '@builtin(frag_depth) frag_depth:f32,' in text
        assert 'out.frag_depth=surface.position.device_depth;' in text
        if stage == 'forward':
            assert 'p.frag_coord.z=surface.position.device_depth;' in text
        pins[str(path)] = sha(path)
    assert len(row['actual_compositions']) == 7
    for check in row['actual_compositions']:
        assert check['actual_exit_code'] == 0
        for lane in ['stdout', 'stderr']:
            path = Path(row['forward']).parent / (check['permutation'] + '.' + lane + '.log')
            assert sha(path) == check[lane + '_sha256']
            pins[str(path)] = sha(path)
O.mkdir()
result = dict(schema='hivemind.house-native-lit-checkpoint.v663', utc=datetime.datetime.now(datetime.timezone.utc).isoformat(), materials=6, actual_compositions=42, rows=proof['rows'], input_pins=pins, original_depth_keyword_and_terminal_preserved=True, forward_and_prepass_depth_output_present=True, pipeline_state_admitted=False, depth_raster_parity=False, orthographic_depth_parity=False, unity_light_transport_parity=False, renderer_installed=False, studio_verified=False, full_import=False, full_import_increment=0)
(O / 'RESULT.v1.json').write_text(json.dumps(result, indent=2), 'utf-8')
note = '''## House — éclairage et sorties de profondeur composés (v663)

- Six contrats HDRP réouverts depuis les sources : opaques, sans alpha test, DepthOffset activé pour tous.
- Six paires de shaders couleur/prépass : 42 compositions Bevy réussies, empreintes et journaux réouverts.
- La valeur POM source atteint les positions et les sorties frag_depth couleur/prépass. Aucun remplacement du décalage par zéro.
- Restent : admission des états de pipeline et comparaison raster de profondeur (perspective, orthographique, ombre), GLB autonomes et rendus des 24 prefabs, bibliothèque/Studio.
- Preuves : F:/OpenRPG_ImportWork/house-native-lit-checkpoint-v663/RESULT.v1.json. **Incrément de conversion complète : 0**.

'''
(O / 'LOT.md').write_text(note, 'utf-8')
for name in ['AVANCEMENT_ACTUEL.md', 'POINT_DE_REPRISE.md']:
    path = W / 'SUIVI_LOTS_HIVEMIND' / name
    before = path.read_bytes()
    (O / (name + '.before')).write_bytes(before)
    path.write_bytes(note.encode('utf-8') + before)
print(json.dumps(dict(materials=6, compositions=42, full_import_increment=0, receipt=str(O / 'RESULT.v1.json'))))
