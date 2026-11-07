"""Retain the complete source hierarchy, then compare the actual v137 receipt.

Prepare never represents a native result. Verify requires the owned native run's
successful terminal receipt and cryptographic linkage to its output.
"""
from pathlib import Path
from datetime import datetime, timezone
import argparse
import hashlib
import json
import math
import struct

W = Path('F:/OpenRPG_ImportWork')
O = W / 'medieval-torch-full-hierarchy-contract-v138'


def read(path):
    return json.loads(Path(path).read_text('utf-8-sig'))


def sha(path):
    with Path(path).open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


def f32(value):
    assert math.isfinite(value), 'Non-finite transform'
    return struct.unpack('<f', struct.pack('<f', value))[0]


def vector(data, keys):
    return {key: f32(data[key]) for key in keys}


def matrix(data):
    x, y, z, w = (f32(data['m_LocalRotation'][k]) for k in 'xyzw')
    assert abs(x*x + y*y + z*z + w*w - 1) < 1e-5
    scale = [f32(data['m_LocalScale'][k]) for k in 'xyz']
    assert all(value != 0 for value in scale), 'Singular source scale'
    position = [f32(data['m_LocalPosition'][k]) for k in 'xyz']
    rotation = [[1-2*(y*y+z*z), 2*(x*y-z*w), 2*(x*z+y*w)],
                [2*(x*y+z*w), 1-2*(x*x+z*z), 2*(y*z-x*w)],
                [2*(x*z-y*w), 2*(y*z+x*w), 1-2*(x*x+y*y)]]
    return [[f32(rotation[i][j]*scale[j]) for j in range(3)] + [position[i]]
            for i in range(3)] + [[0., 0., 0., 1.]]


def multiply(a, b):
    return [[f32(sum(a[i][k]*b[k][j] for k in range(4)))
             for j in range(4)] for i in range(4)]


def compare(expected, native):
    assert len(native['nodes']) == len(expected['nodes']) == 8
    observed = {n['path']: n for n in native['nodes']}
    assert len(observed) == 8, 'Duplicate native hierarchy path'
    assert set(observed) == {n['path'] for n in expected['nodes']}
    checks = []
    for node in expected['nodes']:
        actual = observed[node['path']]
        assert actual['name'] == node['name'], 'Native node name differs'
        for native_key, source_key, keys in [
            ('local_position', 'm_LocalPosition', 'xyz'),
            ('local_scale', 'm_LocalScale', 'xyz'),
            ('local_rotation', 'm_LocalRotation', 'xyzw')]:
            for key in keys:
                assert struct.pack('<f', actual[native_key][key]) == struct.pack(
                    '<f', node['source_transform'][source_key][key]), (
                        node['path'], native_key, key)
        values = [actual['local_to_world'][f'm{i}{j}']
                  for i in range(4) for j in range(4)]
        reference = node['source_world_matrix_row_major']
        # Matrix arithmetic can differ between Unity and Python. Local source
        # values above are exact float32; world comparison is separately bounded.
        error = max(abs(a-b) for a, b in zip(values, reference))
        assert all(math.isfinite(a) and abs(a-b) <= 1e-7 + 4*2**-23*abs(b)
                   for a, b in zip(values, reference)), node['path']
        native_types = sorted(c['identity']['type'].split('.')[-1]
                              for c in actual['components'])
        expected_types = sorted(node['component_types'])
        # SDK behaviours have concrete runtime names, captured separately.
        concrete = sorted(t for t in native_types if t not in
                          {'HDAdditionalLightData', 'LensFlareComponentSRP'})
        source_concrete = sorted(t for t in expected_types if t != 'MonoBehaviour')
        assert concrete == source_concrete, (node['path'], native_types)
        assert len(native_types) == len(expected_types)
        if 'MonoBehaviour' in expected_types:
            assert set(native_types) >= {'HDAdditionalLightData', 'LensFlareComponentSRP'}
        checks.append(dict(path=node['path'], local_float32_exact=True,
                           world_max_absolute_error=error, component_types=native_types))
    return checks


def prepare():
    assert not O.exists(), 'Preserve prior immutable stage'
    proof_path = W / 'medieval-torch-effective-particles-v122/RESULT.v1.json'
    proof = read(proof_path)
    ir_path = Path(next(iter(proof['output_pins'])))
    assert sha(ir_path) == proof['output_pins'][str(ir_path)]
    ir = read(ir_path)
    clone_path = W / 'medieval-torch-private-prefab-clone-v134/RESULT.v1.json'
    clone = read(clone_path)
    docs = {d['file_id']: d for d in ir['parent_documents']}
    transforms = {k: d for k, d in docs.items() if d['type'] == 'Transform'}
    roots = [d for d in transforms.values() if not d['stripped']
             and d['data']['m_Father']['fileID'] == '0']
    assert len(roots) == 1
    nested = {}
    for case in ir['cases']:
        children = {d['file_id']: d for d in case['effective_documents']}
        root = next(d for d in children.values() if d['type'] == 'Transform'
                    and d['data']['m_Father']['fileID'] == '0')
        bridges = [d for d in transforms.values() if d['stripped']
                   and d['data']['m_PrefabInstance']['fileID'] == case['parent_instance']['file_id']
                   and d['data']['m_CorrespondingSourceObject']['guid'] == case['source_guid']]
        assert len(bridges) == 1
        nested[bridges[0]['file_id']] = (root, children, case['source_guid'])
    rows = []

    def walk(transform, scope, path, parent, source_guid, visited):
        fid = transform['file_id']
        assert (source_guid, fid) not in visited, 'Hierarchy cycle'
        visited = visited | {(source_guid, fid)}
        data = transform['data']
        obj = scope[data['m_GameObject']['fileID']]['data']
        components = [scope[v['component']['fileID']] for v in obj['m_Component']]
        world = multiply(parent, matrix(data))
        references = [dict(type=d['type'], source_file_id=d['file_id'], data=d['data'])
                      for d in components if d['type'] in
                      {'MeshFilter', 'MeshRenderer', 'MeshCollider', 'LODGroup'}]
        rows.append(dict(path=path, name=obj['m_Name'], source_guid=source_guid,
                         private_guid=clone['guid_mapping'][source_guid],
                         source_transform=data, component_types=[d['type'] for d in components],
                         geometry_and_lod_source=references,
                         source_world_matrix_row_major=[v for row in world for v in row]))
        for i, ref in enumerate(data['m_Children']):
            cid = ref['fileID']
            if cid in nested:
                child, child_scope, guid = nested[cid]
                walk(child, child_scope, path+'/'+str(i), world, guid, visited)
            else:
                walk(scope[cid], scope, path+'/'+str(i), world, source_guid, visited)

    walk(roots[0], docs, 'root', [[float(i == j) for j in range(4)]
                               for i in range(4)], ir['source_group_id'].split('unity-guid-')[1], set())
    assert len(rows) == 8
    pins = {str(p): sha(p) for p in [proof_path, ir_path, clone_path, Path(__file__).resolve()]}
    for row in clone['rows']:
        for entry in row['files']:
            assert sha(entry['original']) == entry['original_sha256']
            assert sha(entry['private_copy']) == entry['private_sha256']
            pins[entry['original']] = entry['original_sha256']
            pins[entry['private_copy']] = entry['private_sha256']
    result = dict(status='complete_source_hierarchy_contract_prepared_not_native_verified',
                  lot='L06.MEDIEVAL.TORCH.001', nodes=rows, input_pins=pins,
                  native_matrix_verified=False, runtime_converted=False, full_import=False,
                  prepared_utc=datetime.now(timezone.utc).isoformat())
    O.mkdir()
    (O/'PREPARED.v1.json').write_text(json.dumps(result, indent=2, allow_nan=False), 'utf-8')
    print(json.dumps(dict(prepared_nodes=len(rows), native_verified=False, proof=str(O/'PREPARED.v1.json'))))


def verify():
    expected = read(O/'PREPARED.v1.json')
    assert not (O/'RESULT.v1.json').exists(), 'Preserve prior result'
    for path, digest in expected['input_pins'].items():
        assert sha(path) == digest, path
    run_path = W/'medieval-torch-native-prefab-import-v137/RUN-RESULT.v1.json'
    native_path = W/'medieval-torch-native-prefab-import-v137/NATIVE-RESULT.v1.json'
    run = read(run_path)
    assert run['status'] == 'complete_private_torch_prefab_native_import_and_hierarchy_inspected'
    assert run['unity_exit_code'] == 0 and run['native_import_verified']
    assert sha(native_path) == run['native_receipt_sha256']
    native = read(native_path)
    checks = compare(expected, native)
    result = dict(status='eight_native_nodes_source_transforms_and_component_types_verified',
                  lot=expected['lot'], checks=checks, input_pins={
                      str(p): sha(p) for p in [O/'PREPARED.v1.json', run_path, native_path]},
                  native_matrix_verified=True, geometry_subassets_verified=False,
                  temporal_behavior_verified=False, runtime_converted=False,
                  full_import=False, finished_utc=datetime.now(timezone.utc).isoformat())
    (O/'RESULT.v1.json').write_text(json.dumps(result, indent=2, allow_nan=False), 'utf-8')
    print(json.dumps(dict(verified_nodes=len(checks), full_import=False)))


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('action', choices=['prepare', 'verify'])
    args = parser.parse_args()
    prepare() if args.action == 'prepare' else verify()
