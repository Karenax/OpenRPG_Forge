"""Reopen source hierarchy and bind verified geometry/IR without admitting runtime."""
from pathlib import Path
from datetime import datetime, timezone
import hashlib
import json
import struct
import copy

W = Path('F:/OpenRPG_ImportWork')
O = W / 'medieval-torch-source-assembly-v166'
PINS = {}

def sha(path):
    with Path(path).open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()

def read(path):
    PINS[str(path)] = sha(path)
    return json.loads(Path(path).read_text('utf-8-sig'))

def flip(value):
    bits = struct.unpack('<I', struct.pack('<f', value))[0] ^ 0x80000000
    return struct.unpack('<f', struct.pack('<I', bits))[0]

def same(a, b):
    return struct.pack('<f', a) == struct.pack('<f', b)

def matrix(source):
    return {f'e{i}{j}': flip(source[f'e{i}{j}']) if (i == 2) != (j == 2) else source[f'e{i}{j}'] for i in range(4) for j in range(4)}

def main():
    assert not O.exists()
    native = read(W / 'medieval-torch-native-prefab-import-v137/NATIVE-RESULT.v1.json')
    geometry = read(W / 'medieval-torch-converted-geometry-v152/RESULT.v1.json')
    ir = read(W / 'medieval-torch-active-material-ir-v154/RESULT.v1.json')
    guidmap = read(W / 'medieval-torch-private-prefab-clone-v134/RESULT.v1.json')['guid_mapping']
    assert native['original_prefab_components_unchanged']
    assert geometry['all_vertex_bytes_reopened_and_reverse_compared'] and geometry['all_indices_reopened_and_reverse_compared']
    for path, digest in geometry['output_pins'].items():
        assert sha(path) == digest, path
        PINS[path] = digest
    materials = {}
    for row in ir['rows']:
        path = Path(row['path'])
        assert sha(path) == row['sha256']
        PINS[str(path)] = row['sha256']
        materials[guidmap[row['guid']]] = dict(original_guid=row['guid'], private_guid=guidmap[row['guid']], active_ir_path=str(path), active_ir_sha256=row['sha256'], shader_execution_qualified=False)
    assert len(materials) == 3
    meshes = {m['binding']['path']: m for m in geometry['meshes'] if m['binding']['role'] == 'mesh_filter'}
    colliders = {m['binding']['path']: m for m in geometry['meshes'] if m['binding']['role'] == 'mesh_collider'}
    assert len(meshes) == 4 and len(colliders) == 1
    paths = {n['path'] for n in native['nodes']}
    assert len(paths) == len(native['nodes']) == 8 and 'root' in paths
    nodes = []
    for source in native['nodes']:
        path = source['path']
        parent = path.rsplit('/', 1)[0] if path != 'root' else None
        assert parent is None or parent in paths
        position = dict(source['local_position']); position['z'] = flip(position['z'])
        rotation = dict(source['local_rotation'])
        for axis in 'xy': rotation[axis] = flip(rotation[axis])
        world = matrix(source['local_to_world'])
        assert all(same(v, matrix(world)[k]) for k, v in source['local_to_world'].items())
        assert same(flip(position['z']), source['local_position']['z'])
        assert all(same(flip(rotation[axis]), source['local_rotation'][axis]) for axis in 'xy')
        row = dict(path=path, parent=parent, name=source['name'], position=position, rotation=rotation, scale=source['local_scale'], world_matrix=world, original_node=source, render_mesh=None, collider_mesh=None, materials=[], source_components_pending_runtime=[])
        for component in source['components']:
            kind = component['identity']['type']
            payload = json.loads(component['serialized_json'])
            if kind == 'UnityEngine.MeshRenderer':
                mesh = meshes[path]
                slots = payload['MeshRenderer']['m_Materials']
                assert len(slots) == len(mesh['primitives']) == 3
                row['render_mesh'] = mesh
                row['renderer_identity'] = component['identity']
                row['materials'] = [dict(slot=i, **materials[slot['guid']]) for i, slot in enumerate(slots)]
            elif kind == 'UnityEngine.MeshCollider':
                mesh = colliders[path]
                identity = payload['MeshCollider']['m_Mesh']
                assert str(identity['fileID']) == mesh['identity']['file_id'] and identity['guid'] == mesh['identity']['guid']
                row['collider_mesh'] = mesh
                row['collider_settings_source'] = payload['MeshCollider']
            elif kind not in ['UnityEngine.Transform', 'UnityEngine.MeshFilter', 'UnityEngine.LODGroup']:
                row['source_components_pending_runtime'].append(component)
        nodes.append(row)
    document = dict(schema='openrpg-source-assembly-working.v1', source_prefab_guid=native['prefab_guid'], basis_diagonal=[1,1,-1,1], nodes=nodes, lod_groups=geometry['lod_groups'], materials=list(materials.values()), full_import=False, registered_in_vault=False, runtime_converted=False)

    def verify(value):
        assert len(value['nodes']) == 8
        actual = {n['path']: n for n in value['nodes']}
        for source in native['nodes']:
            row = actual[source['path']]
            assert row['original_node'] == source
            assert all(same(row['world_matrix'][k], v) for k, v in matrix(source['local_to_world']).items())
            assert same(row['position']['z'], flip(source['local_position']['z']))
            assert all(same(row['rotation'][a], flip(source['local_rotation'][a])) for a in 'xy')
            for component in source['components']:
                if component['identity']['type'] == 'UnityEngine.MeshRenderer':
                    slots = json.loads(component['serialized_json'])['MeshRenderer']['m_Materials']
                    assert [m['private_guid'] for m in row['materials']] == [s['guid'] for s in slots]
        identities = {(n['renderer_identity']['guid'], n['renderer_identity']['file_id']) for n in value['nodes'] if n.get('renderer_identity')}
        for group in value['lod_groups']:
            assert len(group['lods']) == 4
            for lod in group['lods']:
                assert len(lod['renderers']) == 1
                for renderer in lod['renderers']: assert (renderer['guid'], renderer['file_id']) in identities

    verify(document)
    controls = []
    bad = copy.deepcopy(document)
    first = next(n for n in bad['nodes'] if n['render_mesh'])
    first['materials'][0], first['materials'][1] = first['materials'][1], first['materials'][0]
    try: verify(bad)
    except AssertionError: controls.append('crossed_material_slots_refused')
    else: raise AssertionError('Crossed material slots admitted')
    bad = copy.deepcopy(document); bad['nodes'][0]['world_matrix']['e03'] = 1
    try: verify(bad)
    except AssertionError: controls.append('altered_world_translation_refused')
    else: raise AssertionError('Altered world translation admitted')
    for path, digest in PINS.items(): assert sha(path) == digest, path
    O.mkdir()
    output = O / 'ASSEMBLY.v1.json'
    output.write_text(json.dumps(document, indent=2, allow_nan=False), 'utf-8')
    verify(json.loads(output.read_text('utf-8')))
    pending = [c['identity']['type'] for n in nodes for c in n['source_components_pending_runtime']]
    result = dict(status='eight_node_hierarchy_converted_and_geometry_material_routes_reopened', finished_utc=datetime.now(timezone.utc).isoformat(), nodes=8, render_meshes=4, collider_meshes=1, material_slots_reconciled=12, lod_levels=4, coordinate_transform_reverse_float32_equal=True, negative_controls=controls, pending_runtime_component_types=pending, input_pins=PINS, output_pins={str(output):sha(output)}, shader_execution_verified=False, particle_runtime_converted=False, studio_verified=False, full_import=False)
    (O / 'RESULT.v1.json').write_text(json.dumps(result, indent=2), 'utf-8')
    print(json.dumps({k:v for k,v in result.items() if k not in ['input_pins','output_pins']}))

if __name__ == '__main__': main()
