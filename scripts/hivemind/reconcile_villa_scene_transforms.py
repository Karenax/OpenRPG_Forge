"""Reconcile retained Unity TRS with the delivered local/composed matrices.

This checks embedded authored metadata, not observed Unity or GPU world frames.
"""
import datetime
import hashlib
import json
import math
from pathlib import Path
import struct

WORK = Path('F:/OpenRPG_ImportWork')
ROOT = WORK / 'villa-207-embedded-transform-reconciliation-v1'
C = [-1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1]
IDENTITY = [1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1]


def read(path):
    return json.loads(path.read_text('utf-8-sig'))


def sha(path):
    h = hashlib.sha256()
    with path.open('rb') as stream:
        for block in iter(lambda: stream.read(4 * 1024 * 1024), b''):
            h.update(block)
    return h.hexdigest()


def save(name, value):
    path = ROOT / name
    temporary = path.with_suffix('.json.tmp')
    temporary.write_text(json.dumps(value, indent=2, allow_nan=False), 'utf-8')
    temporary.replace(path)


def multiply(a, b):
    return [sum(a[k * 4 + row] * b[col * 4 + k] for k in range(4))
            for col in range(4) for row in range(4)]


def trs(original):
    q = original['m_LocalRotation']
    x, y, z, s = (float(q[k]) for k in ('x', 'y', 'z', 'w'))
    assert abs(x*x + y*y + z*z + s*s - 1) <= 1e-4, 'Non-unit retained quaternion'
    scale = [float(original['m_LocalScale'][k]) for k in ('x', 'y', 'z')]
    pos = [float(original['m_LocalPosition'][k]) for k in ('x', 'y', 'z')]
    assert all(math.isfinite(v) for v in [x, y, z, s, *scale, *pos])
    rotation = [1-2*(y*y+z*z), 2*(x*y+z*s), 2*(x*z-y*s), 0,
                2*(x*y-z*s), 1-2*(x*x+z*z), 2*(y*z+x*s), 0,
                2*(x*z+y*s), 2*(y*z-x*s), 1-2*(x*x+y*y), 0,
                *pos, 1]
    for col in range(3):
        for row in range(3):
            rotation[col * 4 + row] *= scale[col]
    return rotation


def close(a, b):
    assert len(a) == len(b) == 16
    return all(math.isfinite(x) and math.isfinite(y)
               and abs(x-y) <= 1e-5 * max(1, abs(x), abs(y)) for x, y in zip(a, b))


def document(path):
    with path.open('rb') as stream:
        header = stream.read(20)
        assert len(header) == 20 and header[:4] == b'glTF'
        assert struct.unpack_from('<I', header, 4)[0] == 2
        assert struct.unpack_from('<I', header, 8)[0] == path.stat().st_size
        assert header[16:20] == b'JSON'
        return json.loads(stream.read(struct.unpack_from('<I', header, 12)[0]))


def reconcile(doc):
    nodes = doc['nodes']
    roots = doc['scenes'][doc.get('scene', 0)]['nodes']
    original, target, transform_ids = {}, {}, {}
    for index, node in enumerate(nodes):
        extras = node['extras']
        source = extras['original_local_transform']
        key = str(extras['source_unity_transform_id'])
        assert key not in transform_ids, 'Duplicate retained source transform identity'
        transform_ids[key] = index
        original[index] = trs(source)
        target[index] = node['matrix']
        assert close(multiply(multiply(C, original[index]), C), target[index]), f'Local basis mismatch: node {index}'
    parents = {}
    for index, node in enumerate(nodes):
        for child in node.get('children', []):
            assert child not in parents, 'Duplicate glTF parent'
            parents[child] = index
    for index, node in enumerate(nodes):
        father = str(node['extras']['original_local_transform']['m_Father']['fileID'])
        if index in roots:
            assert index not in parents and father == '0', 'Source/glTF root parent differs'
        else:
            assert parents.get(index) == transform_ids.get(father), 'Retained source parent differs'
    seen = set()
    maximum = 0.0
    def visit(index, source_parent, target_parent):
        nonlocal maximum
        assert index not in seen, 'Cycle or duplicate selected node'
        seen.add(index)
        source_world = multiply(source_parent, original[index])
        target_world = multiply(target_parent, target[index])
        expected = multiply(multiply(C, source_world), C)
        assert close(expected, target_world), f'Composed basis mismatch: node {index}'
        maximum = max(maximum, *(abs(a-b) for a, b in zip(expected, target_world)))
        for child in nodes[index].get('children', []):
            visit(child, source_world, target_world)
    for root in roots:
        visit(root, IDENTITY, IDENTITY)
    assert seen == set(range(len(nodes))), 'Unreconciled nodes outside selected hierarchy'
    frame = doc['extras']['source_frame_binding']
    assert close(frame['source_object_from_target_object'], C)
    assert close(frame['target_object_from_source_object'], C)
    return {'nodes_reconciled': len(seen), 'maximum_composed_matrix_absolute_error': maximum}


def main():
    assert not ROOT.exists(), 'Prior evidence preserved'
    ROOT.mkdir()
    state = {'status': 'reconciling_embedded_transforms', 'expected_scenes': 207,
             'scenes_reconciled': 0, 'nodes_reconciled': 0, 'rows': [], 'refusals': [],
             'observed_unity_world_qualified': False, 'gpu_rendered': False,
             'studio_verified': False, 'full_import': False}
    save('RUN-RESULT.v1.json', state)
    try:
        pins = {str(Path(__file__).resolve()): sha(Path(__file__).resolve())}
        inputs = []
        for number in range(1, 12):
            folder = WORK / f'villa-native-source-frame-bound-l04-villa-{number:02}-v1'
            frame_path, inputs_path = folder/'RUN-RESULT.v1.json', folder/'INPUTS.v1.json'
            receipt, rows = read(frame_path), read(inputs_path)
            assert receipt['status'] == 'native_original_source_frames_bound_and_reopened'
            assert receipt['verified_scenes'] == len(rows) == (7 if number == 11 else 20)
            assert {r['asset_id'] for r in rows} == {r['asset_id'] for r in receipt['rows']}
            pins.update({str(frame_path): sha(frame_path), str(inputs_path): sha(inputs_path)})
            inputs.extend(rows)
        assert len(inputs) == len({r['asset_id'] for r in inputs}) == 207
        save('INPUTS.v1.json', inputs)
        for row in inputs:
            path = Path(row['glb'])
            assert sha(path) == row['sha256'], 'Physical GLB differs from closed receipt'
            try:
                result = reconcile(document(path))
                state['rows'].append({'asset_id': row['asset_id'], 'glb': str(path),
                                      'glb_sha256': row['sha256'], **result})
                state['scenes_reconciled'] += 1
                state['nodes_reconciled'] += result['nodes_reconciled']
            except (AssertionError, KeyError, ValueError) as error:
                state['refusals'].append({'asset_id': row['asset_id'], 'reason': str(error)})
            assert sha(path) == row['sha256'], 'Physical GLB changed during reconciliation'
            save('RUN-RESULT.v1.json', state)
        for path, expected in pins.items():
            assert sha(Path(path)) == expected, 'Source receipt/helper changed'
        state.update(status=('embedded_local_and_composed_transforms_reconciled' if not state['refusals']
                             else 'embedded_transform_disagreements_retained'), input_pins=pins,
                     source_receipt_pins_unchanged=True,
                     finished_utc=datetime.datetime.now(datetime.timezone.utc).isoformat())
    except Exception as error:
        state.update(status='refused_or_partial', error=str(error))
    save('RUN-RESULT.v1.json', state)
    print(json.dumps({k: v for k, v in state.items() if k not in ('rows', 'input_pins')}))


if __name__ == '__main__':
    main()
