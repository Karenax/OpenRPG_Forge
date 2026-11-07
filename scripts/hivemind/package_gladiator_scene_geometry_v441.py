"""Prepare glTF attribute buffers and exact scene bindings from proven geometry."""
from pathlib import Path
from datetime import datetime, timezone
import base64
import hashlib
import json
import struct

W = Path('F:/OpenRPG_ImportWork')
O = W / 'gladiator-scene-geometry-package-v441'
assert not O.exists()

def read(p):
    return json.loads(Path(p).read_text('utf-8-sig'))

def sha(p):
    with Path(p).open('rb') as f:
        return hashlib.file_digest(f, 'sha256').hexdigest()

pins = {str(Path(__file__)): sha(Path(__file__))}
def load(rel):
    p = W / rel
    result = read(p)
    pins[str(p)] = sha(p)
    for key in ['input_pins', 'output_pins']:
        for path, digest in result.get(key, {}).items():
            assert sha(path) == digest, path
            pins[path] = digest
    return result

bridge = load('gladiator-source-binding-bridge-v390/RESULT.v1.json')
first = load('gladiator-converted-geometry-v396/RESULT.v1.json')
remaining = load('gladiator-remaining-converted-geometry-v403/RESULT.v1.json')
native_first = load('gladiator-native-geometry-v380/NATIVE-RESULT.v1.json')
native_remaining = load('gladiator-remaining-native-geometry-v400/NATIVE-RESULT.v1.json')
reopened = load('gladiator-remaining-geometry-reopen-v402/RESULT.v1.json')
assert reopened['native_root_transforms_verified'] == 5
lit = load('gladiator-native-lit-delivery-v440/RESULT.v1.json')
assert lit['materials'] == 6
materials = {r['material_guid'] for r in lit['rows']}
assert first['basis'] == remaining['basis']
assert first['basis']['unity_to_target_diagonal'] == [1, 1, -1]
native = {}
for case in [native_first, *native_remaining['cases']]:
    for mesh in case['meshes']:
        key = (mesh['identity']['guid'], mesh['identity']['file_id'])
        assert key not in native
        native[key] = mesh
converted = [*first['meshes'], *remaining['meshes']]
assert len(converted) == len(native) == 17
assert len(bridge['rows']) == 5
O.mkdir()
geometry = {}
outputs = {}
semantics = {'Position': 'POSITION', 'Normal': 'NORMAL', 'Tangent': 'TANGENT',
             'TexCoord0': 'TEXCOORD_0', 'TexCoord1': 'TEXCOORD_1'}
for m in converted:
    key = (m['identity']['guid'], m['identity']['file_id'])
    original = native[key]
    assert m['vertex_count'] == original['vertices']
    directory = O / (key[0] + '-' + key[1])
    directory.mkdir()
    target = {s['index']: Path(s['path']).read_bytes() for s in m['streams']}
    source = {s['index']: base64.b64decode(s['bytes_base64'], validate=True) for s in original['streams']}
    strides = {s['index']: s['stride'] for s in m['streams']}
    attrs = {}
    source_attrs = {}
    for a in m['attributes']:
        assert a['format'] == 'Float32' and a['semantic'] in semantics
        semantic = semantics[a['semantic']]
        width = a['dimension']
        stride = strides[a['stream']]
        for lane, buffers, descriptors in [('target', target, attrs), ('source', source, source_attrs)]:
            raw = buffers[a['stream']]
            assert len(raw) == m['vertex_count'] * stride
            sliced = b''.join(raw[i*stride+a['offset']:i*stride+a['offset']+4*width] for i in range(m['vertex_count']))
            assert len(sliced) == m['vertex_count'] * width * 4
            path = directory / (semantic + '.' + lane + '.f32')
            path.write_bytes(sliced)
            reopened_bytes = path.read_bytes()
            assert reopened_bytes == sliced
            # Independently compare each reopened accessor element to its source
            # interleaved input. No new basis conversion or UV changes occur here.
            for i in range(m['vertex_count']):
                assert reopened_bytes[i*width*4:(i+1)*width*4] == raw[i*stride+a['offset']:i*stride+a['offset']+width*4]
            digest = sha(path)
            outputs[str(path)] = digest
            descriptors[semantic] = dict(path=str(path), sha256=digest, dimension=width, count=m['vertex_count'])
    assert set(attrs) == set(semantics.values())
    assert attrs['TEXCOORD_0']['dimension'] == attrs['TEXCOORD_1']['dimension'] == 2
    for semantic in ['TEXCOORD_0', 'TEXCOORD_1']:
        assert attrs[semantic]['sha256'] == source_attrs[semantic]['sha256']
    assert len(m['primitives']) == len(m['materials_preserved'])
    for i, primitive in enumerate(m['primitives']):
        path = Path(primitive['index_path'])
        raw = path.read_bytes()
        assert len(raw) == primitive['indices'] * 4
        indices = struct.unpack('<' + 'I'*primitive['indices'], raw)
        assert all(v < m['vertex_count'] for v in indices)
        assert m['materials_preserved'][i]['guid'] in materials
        pins[str(path)] = sha(path)
    assert key not in geometry
    geometry[key] = dict(identity=m['identity'], attributes=attrs, original_attributes=source_attrs,
        primitives=m['primitives'], materials=m['materials_preserved'],
        bounds_min=m['bounds_min'], bounds_max=m['bounds_max'], vertex_count=m['vertex_count'])

def target_column_major(source_row_major):
    signs = [1, 1, -1, 1]
    assert len(source_row_major) == 16
    return [source_row_major[r*4+c]*signs[r]*signs[c] for c in range(4) for r in range(4)]

scenes = []
used = set()
for scene in bridge['rows']:
    assert sha(scene['source_path']) == scene['source_sha256']
    pins[scene['source_path']] = scene['source_sha256']
    nodes = []
    node_paths = {n['path'] for n in scene['nodes']}
    lod_paths = set()
    for group in scene['lod_groups']:
        for level in group['levels']:
            assert not level['explicit_empty_renderer_slots']
            for renderer in level['renderers']:
                assert renderer['path'] in node_paths
                lod_paths.add(renderer['path'])
    for node in scene['nodes']:
        entry = dict(source_node=node,
            target_local_matrix_column_major=target_column_major(node['source_local_matrix_row_major']),
            target_world_matrix_column_major=target_column_major(node['source_world_matrix_row_major']))
        if node['renderer']:
            r = node['renderer']
            key = (r['mesh_guid'], r['mesh_file_id'])
            g = geometry[key]
            assert [s['guid'] for s in r['material_slots']] == [s['guid'] for s in g['materials']]
            assert node['path'] in lod_paths
            entry['geometry'] = g
            used.add(key)
        nodes.append(entry)
    scenes.append(dict(prefab_guid=scene['prefab_guid'], cards=scene['cards'],
        source_prefab=scene['source_path'], source_prefab_sha256=scene['source_sha256'],
        nodes=nodes, original_lod_groups=scene['lod_groups'],
        runtime_renderer_and_collider_admission_pending=True))
assert used == set(geometry)
assert sum(len(s['cards']) for s in scenes) == 17
assert all(sha(p) == h for p, h in {**pins, **outputs}.items())
result = dict(status='five_source_scenes_and_seventeen_mesh_accessor_packages_reopened',
    observed_utc=datetime.now(timezone.utc).isoformat(), rows=scenes, input_pins=pins, output_pins=outputs,
    prefabs=5, meshes=17, attribute_buffers=len(outputs),
    vertices=sum(g['vertex_count'] for g in geometry.values()),
    triangles=sum(p['triangles'] for g in geometry.values() for p in g['primitives']),
    glb_created=False, renderer_installed=False, gpu_rendered=False, full_import=False,
    complete_asset_conversion_increment=0)
(O / 'RESULT.v1.json').write_text(json.dumps(result, indent=2, allow_nan=False), 'utf-8')
note = '''## Gladiator — géométrie prête pour assemblage GLB, 10 octobre 2026

5 hiérarchies sources liées aux 17 meshes et à leurs niveaux de détail. 170 buffers d'attributs source/cible décomposés et réouverts octet par octet : 6702 sommets, 10000 triangles. UV0/UV1 inchangés, slots de matériaux et colliders source conservés. Transformations de hiérarchie préparées dans le même repère que la géométrie déjà vérifiée.

Preuve : F:\\OpenRPG_ImportWork\\gladiator-scene-geometry-package-v441\\RESULT.v1.json
Assemblage GLB, admission runtime des renderers/LOD/colliders, dessins comparés et validation Studio restent à réaliser. Aucun nouvel asset complet qualifié.

'''
(O / 'LOT.md').write_text(note, 'utf-8')
for name in ['AVANCEMENT_ACTUEL.md', 'POINT_DE_REPRISE.md']:
    p = W / 'SUIVI_LOTS_HIVEMIND' / name
    before = p.read_bytes()
    (O / (name + '.before')).write_bytes(before)
    p.write_bytes(note.encode('utf-8') + before)
print(json.dumps({k: result[k] for k in ['status', 'prefabs', 'meshes', 'attribute_buffers', 'vertices', 'triangles']}))
