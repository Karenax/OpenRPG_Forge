"""Reopen House host/surface evidence and publish an honest lot checkpoint."""
from pathlib import Path
import datetime
import hashlib
import json

W = Path('F:/OpenRPG_ImportWork')
OUT = W / 'house-host-interface-checkpoint-v659'

def read(path):
    return json.loads(path.read_text('utf-8-sig'))

def sha(path):
    with Path(path).open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()

def main():
    assert not OUT.exists()
    vertex_path = W / 'house-bevy-vertex-stages-v657/RESULT.v1.json'
    surface_path = W / 'house-native-surface-interfaces-v658/RESULT.v1.json'
    vertex, surface = read(vertex_path), read(surface_path)
    assert not vertex['failures'] and not surface['failures']
    assert vertex['native_vertex_programs_composed'] == surface['native_surface_interfaces_composed'] == 6
    pins = {str(p): sha(p) for p in [vertex_path, surface_path, Path(__file__).resolve(), Path(__file__).with_name('build_house_bevy_vertex_stages_v656.py')]}
    for receipt in [vertex, surface]:
        for path, digest in receipt['input_pins'].items():
            assert sha(path) == digest, path
            pins[path] = digest
    rows = []
    for row in surface['rows']:
        path = Path(row['native'])
        assert sha(path) == row['native_sha256']
        text = path.read_text('utf-8-sig')
        assert 'depth_offset:f32' in text
        assert 'output.inverse_object_xz_scale=vec2<f32>(length(' in text
        assert 'output.inverse_object_xz_scale=source_graph.source_scale_mip_min.xy;' not in text
        assert text.count('fn evaluate_original_source_surface(') == 1
        assert text.count('out.source_uv0=') == text.count('out.source_uv1=') == 2
        assert len(row['actual_compositions']) == 4
        for check in row['actual_compositions']:
            assert check['actual_exit_code'] == 0
            for suffix in ['stdout', 'stderr']:
                log = path.parent / (check['permutation'] + '.' + suffix + '.log')
                assert sha(log) == check[suffix + '_sha256']
                pins[str(log)] = sha(log)
        pins[str(path)] = sha(path)
        rows.append(dict(material_guid=row['material_guid'], native=str(path), sha256=sha(path), depth_terminal_preserved=True, actual_compositions=4))
    OUT.mkdir()
    result = dict(schema='hivemind.house-host-interface-checkpoint.v659', utc=datetime.datetime.now(datetime.timezone.utc).isoformat(), material_count=6, vertex_compositions=24, surface_compositions=24, rows=rows, input_pins=pins, renderer_installed=False, prefab_render_parity=False, studio_verified=False, full_import=False, full_import_increment=0)
    (OUT / 'RESULT.v1.json').write_text(json.dumps(result, indent=2), 'utf-8')
    note = '''## House — interfaces hôtes et surfaces vérifiées (v659)

- Six matériaux : 24 compositions vertex Bevy et 24 compositions surface réussies ; réouverture des SHA et journaux PASS.
- UV0/UV1 conservés ; échelle POM calculée depuis la transformation réelle de chaque instance. Terminal DepthOffset source présent.
- Ces contrôles prouvent la composition des interfaces, pas encore la profondeur rasterisée ni l'éclairage complet.
- Suite : contrats de rendu source et éclairage/prepass avec profondeur POM, GLB autonomes, rendus des 24 prefabs, images/recherche/placement Studio.
- Preuves : F:/OpenRPG_ImportWork/house-host-interface-checkpoint-v659/RESULT.v1.json. **Incrément de conversion complète : 0**.

'''
    (OUT / 'LOT.md').write_text(note, 'utf-8')
    for name in ['AVANCEMENT_ACTUEL.md', 'POINT_DE_REPRISE.md']:
        target = W / 'SUIVI_LOTS_HIVEMIND' / name
        old = target.read_text('utf-8-sig')
        assert 'interfaces hôtes et surfaces vérifiées (v659)' not in old
        target.write_text(note + old, 'utf-8')
    print(json.dumps(dict(materials=6, compositions=48, full_import_increment=0, receipt=str(OUT / 'RESULT.v1.json'))))

if __name__ == '__main__':
    main()
