"""Prove the three derived mesh leaves from source parent and native vertices.

Derived leaves have no Unity Transform identity. Do not fabricate one to make
the original-transform reconciliation pass. No scene or source is modified.
"""
import datetime
import json
from pathlib import Path

from reconcile_villa_scene_transforms import C, IDENTITY, close, multiply, sha, trs
from bind_native_villa_source_frames import accessor_bytes, reflected, view_bytes
from assemble_native_villa_delivery import split_glb, digest

WORK = Path('F:/OpenRPG_ImportWork')
ROOT = WORK / 'villa-native-mesh-leaf-frame-reopen-v2'
SOURCE = WORK / 'hivemind-run/villa-forge/source'
INPUTS = WORK / 'villa-207-embedded-transform-reconciliation-v1/INPUTS.v1.json'
NATIVE = WORK / 'unity-villa-remaining-mesh-source-probe-v2'
ORIGINAL = WORK / 'villa-unity-captured-vertex-glb-batch-v6/RESULT.v1.json'


def read(path):
    return json.loads(path.read_text('utf-8-sig'))


def check(row, capture, request, pins):
    path = Path(row['glb'])
    raw = path.read_bytes()
    assert digest(raw) == row['sha256']
    pins[str(path)] = row['sha256']
    doc, binary = split_glb(raw)
    extra = doc['extras']
    captured = extra['unity_captured_vertices']
    assert captured['unity_guid'] == request['expected_guid']
    assert str(captured['unity_file_id']) == str(request['expected_file_id'])
    assert captured['manifest_sha256'] == pins[str(NATIVE/'manifest.json')]
    capture_result = Path(capture['capture_root'])/'RESULT.json'
    assert sha(capture_result) == captured['capture_result_sha256']
    pins[str(capture_result)] = captured['capture_result_sha256']
    native_cases = [case for case in read(capture_result)['cases']
                    if case['asset_id'] == row['asset_id']]
    assert len(native_cases) == 1
    native_case = native_cases[0]
    assert native_case['expected_identity_matches'] and not native_case['error']
    assert native_case['actual_guid'] == captured['unity_guid']
    assert str(native_case['actual_file_id']) == str(captured['unity_file_id'])
    snapshot_raw = view_bytes(doc, binary, extra['source_original_snapshot_view'])
    assert digest(snapshot_raw) == extra['source_original_snapshot_sha256']
    snapshot = json.loads(snapshot_raw)
    job = snapshot['job']
    source_prefab = SOURCE / job['source']
    assert sha(source_prefab) == job['source_sha256'] == extra['source_prefab_sha256']
    pins[str(source_prefab)] = job['source_sha256']
    nodes = doc['nodes']
    parents = {}
    for index, node in enumerate(nodes):
        for child in node.get('children', []):
            assert child not in parents
            parents[child] = index
    source_locals, source_ids, adapters = {}, {}, []
    for index, node in enumerate(nodes):
        e = node.get('extras', {})
        if 'original_local_transform' in e:
            key = str(e['source_unity_transform_id'])
            assert key not in source_ids
            original = job['transforms'][key]
            assert e['original_local_transform'] == original
            assert str(e['source_unity_gameobject_id']) == str(original['m_GameObject']['fileID'])
            source_ids[key] = index
            source_locals[index] = trs(original)
            assert close(multiply(multiply(C, source_locals[index]), C), node['matrix'])
            continue
        assert e.get('source_fbx_measured_frame') is True
        assert 'source_unity_transform_id' not in e and not node.get('children')
        assert index in parents and 'mesh' in node and close(node['matrix'], IDENTITY)
        assert e['unity_captured_vertices'] == captured
        parent = nodes[parents[index]]
        parent_id = str(parent['extras']['source_unity_transform_id'])
        components = [component for component in job['components']
                      if str(component['transform_id']) == parent_id
                      and component['mesh_guid'] == captured['unity_guid']
                      and str(component['mesh_fileid']) == str(captured['unity_file_id'])]
        assert len(components) == 1, 'Native leaf does not bind the original source renderer'
        component = components[0]
        source_mesh = SOURCE/component['mesh_source']
        assert sha(source_mesh) == component['mesh_sha256']
        pins[str(source_mesh)] = component['mesh_sha256']
        position_capture = captured['capture_buffers']['positions.f32']
        positions = view_bytes(doc, binary, position_capture['buffer_view'])
        assert len(positions) == position_capture['bytes']
        assert digest(positions) == position_capture['sha256']
        native_positions = Path(capture['capture_root'])/f"{capture['capture_ordinal']:03}"/'positions.f32'
        assert sha(native_positions) == position_capture['sha256']
        pins[str(native_positions)] = position_capture['sha256']
        expected = reflected(positions, 3, [0])
        assert len(expected) == native_case['vertices'] * 12
        primitives = doc['meshes'][node['mesh']]['primitives']
        assert primitives
        for primitive in primitives:
            assert accessor_bytes(doc, binary, primitive['attributes']['POSITION'], 3) == expected
        # An adapter around the exact native mesh has no additional authored
        # transform. Its identity is proved above by bytes and parent binding.
        source_locals[index] = IDENTITY
        adapters.append({'node': index, 'source_parent_transform_id': parent_id,
                         'mesh_guid': component['mesh_guid'],
                         'mesh_file_id': str(component['mesh_fileid']),
                         'native_positions_sha256': position_capture['sha256'],
                         'source_transform_identity_created': False})
    assert len(adapters) == 1
    roots = doc['scenes'][doc.get('scene', 0)]['nodes']
    for key, index in source_ids.items():
        father = str(job['transforms'][key]['m_Father']['fileID'])
        if index in roots:
            assert father == '0' and index not in parents
        else:
            assert parents.get(index) == source_ids.get(father)
    seen = set()
    def visit(index, source_parent, target_parent):
        assert index not in seen
        seen.add(index)
        source_world = multiply(source_parent, source_locals[index])
        target_world = multiply(target_parent, nodes[index]['matrix'])
        assert close(multiply(multiply(C, source_world), C), target_world)
        for child in nodes[index].get('children', []):
            visit(child, source_world, target_world)
    for root in roots:
        visit(root, IDENTITY, IDENTITY)
    assert seen == set(range(len(nodes)))
    assert sha(path) == row['sha256']
    return {'asset_id': row['asset_id'], 'glb': str(path), 'nodes_reconciled': len(seen),
            'derived_leaf_bindings': adapters, 'source_prefab_reopened': str(source_prefab),
            'source_and_native_mesh_parent_correspondence_verified': True}


def main():
    assert not ROOT.exists(), 'Prior evidence preserved'
    ROOT.mkdir()
    rows = [row for row in read(INPUTS)
            if 'prefab-sm-cyprus-lar-windows-18-' in row['asset_id']]
    assert len(rows) == 3
    native = {r['asset_id']: r for r in read(NATIVE/'manifest.json')['cases']}
    captures = {r['asset_id']: r for r in read(ORIGINAL)['rows']}
    helper_paths = [Path(__file__).resolve(), INPUTS, NATIVE/'manifest.json', ORIGINAL,
                    Path(__file__).with_name('reconcile_villa_scene_transforms.py'),
                    Path(__file__).with_name('bind_native_villa_source_frames.py'),
                    Path(__file__).with_name('assemble_native_villa_delivery.py')]
    pins = {str(p): sha(p) for p in helper_paths}
    state = {'status': 'reopening_three_native_mesh_leaf_frames', 'expected_scenes': 3,
             'scenes_reconciled': 0, 'rows': [], 'refusals': [], 'glb_modified': False,
             'observed_unity_world_qualified': False, 'gpu_rendered': False,
             'studio_verified': False, 'full_import': False}
    for row in rows:
        try:
            state['rows'].append(check(row, captures[row['asset_id']], native[row['asset_id']], pins))
            state['scenes_reconciled'] += 1
        except Exception as error:
            state['refusals'].append({'asset_id': row['asset_id'], 'reason': str(error)})
    changed = [p for p, expected in pins.items() if sha(Path(p)) != expected]
    state.update(status=('three_native_mesh_leaf_parent_frames_reopened'
                         if not state['refusals'] and not changed else 'refused_or_partial'),
                 input_pins=pins, changed_inputs=changed,
                 finished_utc=datetime.datetime.now(datetime.timezone.utc).isoformat())
    (ROOT/'RESULT.v1.json').write_text(json.dumps(state, indent=2), 'utf-8')
    print(json.dumps({k: v for k, v in state.items() if k not in ('rows', 'input_pins')}))


if __name__ == '__main__':
    main()
