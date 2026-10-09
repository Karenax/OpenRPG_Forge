"""Independently reopen captured prefab hierarchy and assembled pose/bindings.

This verifies retained native observations and target matrix composition, not
material rendering, draw-slot semantics, GLB delivery or Studio acceptance.
"""
import argparse
import datetime
import hashlib
import json
import math
from pathlib import Path

WORK = Path('F:/OpenRPG_ImportWork')
CAPTURE = WORK / 'hivemind-prefab-instance-source-closure-v1/projects/townsmith.HIERARCHY-RESULT.v1.json'


def sha(path):
    value = hashlib.sha256()
    with Path(path).open('rb') as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b''):
            value.update(block)
    return value.hexdigest()


def read(path):
    return json.loads(Path(path).read_text('utf-8-sig'))


def require(condition, reason):
    if not condition:
        raise ValueError(reason)


def matrix(values, column_major=False):
    require(len(values) == 16 and all(math.isfinite(v) for v in values), 'Invalid matrix')
    return [[values[c * 4 + r] if column_major else values[r * 4 + c]
             for c in range(4)] for r in range(4)]


def multiply(a, b):
    return [[sum(a[r][k] * b[k][c] for k in range(4)) for c in range(4)] for r in range(4)]


def reflected(value):
    signs = [-1, 1, 1, 1]
    return [[value[r][c] * signs[r] * signs[c] for c in range(4)] for r in range(4)]


def compare(actual, expected, reason):
    error = max(abs(actual[r][c] - expected[r][c]) for r in range(4) for c in range(4))
    require(all(math.isclose(actual[r][c], expected[r][c], rel_tol=1e-5, abs_tol=5e-5)
                for r in range(4) for c in range(4)), reason)
    return error


def identity(value):
    require(value['exact_asset_identity_available'] and not value['is_null'],
            'Exact native identity required')
    return value['guid'], str(value['file_id'])


def verify(arguments):
    receipt_path = Path(arguments.receipt)
    root = Path(arguments.output)
    require(not root.exists(), 'Existing verification evidence preserved')
    root.mkdir()
    result = {'status': 'verifying_native_prefab_pose', 'expected_prefabs': arguments.expected,
              'rows': [], 'gpu_rendered': False, 'material_draw_mapping_qualified': False,
              'studio_verified': False, 'full_import': False}
    try:
        receipt = read(receipt_path)
        require(receipt['source_and_derivative_pins_unchanged'], 'Assembly has not closed')
        require(receipt['prefabs_bound'] == arguments.expected == len(receipt['rows']),
                'Assembly scope differs')
        pins = dict(receipt['source_pins'])
        require(str(CAPTURE) in pins, 'Native capture is not an assembly input')
        for path in [receipt_path, Path(__file__).resolve()]:
            pins[str(path)] = sha(path)
        for path, expected in pins.items():
            require(sha(path) == expected, 'Assembly/source input changed: ' + path)
        capture = read(CAPTURE)
        captured = {row['asset_id']: row for row in capture['cases']}
        seen_assets = set()
        for row in receipt['rows']:
            asset_id = row['asset_id']
            require(asset_id not in seen_assets, 'Duplicate prefab identity')
            seen_assets.add(asset_id)
            original = captured[asset_id]
            require(not original['error'], 'Native capture has an error')
            require(row['original_root'] == original['root']
                    and row['original_lod_groups'] == original['lod_groups'], 'Root/LOD changed')
            source_nodes = {node['path']: node for node in original['nodes']}
            require(len(source_nodes) == len(original['nodes']) == len(row['nodes']),
                    'Missing or duplicate hierarchy nodes')
            source_world, target_world = {}, {}
            max_error = 0.0
            for retained in row['nodes']:
                node = retained['source_node']
                path, parent = node['path'], node['parent_path']
                require(path not in source_world and node == source_nodes[path],
                        'Native node/components/state changed')
                identity(node['transform'])
                identity(node['gameobject'])
                local = matrix(node['source_local_matrix_row_major'])
                target_local = matrix(retained['target_local_matrix_column_major'], True)
                max_error = max(max_error, compare(target_local, reflected(local),
                                                   'Local target basis differs'))
                require(not parent or parent in source_world, 'Parent missing or not parent-first')
                source_world[path] = multiply(source_world[parent], local) if parent else local
                target_world[path] = multiply(target_world[parent], target_local) if parent else target_local
                native_world = matrix(node['source_local_to_world_matrix_row_major'])
                retained_world = matrix(retained['target_source_world_matrix_column_major'], True)
                max_error = max(max_error,
                    compare(source_world[path], native_world, 'Native parent composition differs'),
                    compare(target_world[path], reflected(native_world), 'Target parent composition differs'),
                    compare(target_world[path], retained_world, 'Retained target world differs'))
            require(set(source_world) == set(source_nodes), 'Hierarchy coverage differs')
            expected_renderers = {(node['path'], identity(renderer['component'])): renderer
                for node in original['nodes'] for renderer in node['renderers']
                if renderer.get('static_mesh_adapter_applicable')}
            seen_renderers = set()
            mismatches = 0
            for binding in row['static_renderer_bindings']:
                key = binding['node_path'], identity(binding['renderer_identity'])
                require(key not in seen_renderers, 'Duplicate renderer binding')
                seen_renderers.add(key)
                renderer = expected_renderers[key]
                require(binding['source_mesh_identity'] == renderer['mesh']['source']
                        and binding['original_material_slots'] == renderer['original_material_slots'],
                        'Mesh or material slot identity/order changed')
                geometry_path = Path(binding['geometry_derivative']) / 'GEOMETRY.v1.json'
                require(sha(geometry_path) == binding['geometry_receipt_sha256'],
                        'Bound geometry receipt changed')
                pins[str(geometry_path)] = binding['geometry_receipt_sha256']
                mismatches += int(binding['material_draw_mapping_requires_native_qualification'])
            require(seen_renderers == set(expected_renderers), 'Static renderer coverage differs')
            deferred = [{'node_path': node['path'], 'renderer': renderer,
                         'reason': 'requires_own_rig_or_effect_adapter'}
                        for node in original['nodes'] for renderer in node['renderers']
                        if not renderer.get('static_mesh_adapter_applicable')]
            require(row['deferred_renderers'] == deferred, 'Deferred renderer coverage changed')
            result['rows'].append({'asset_id': asset_id, 'nodes_verified': len(source_nodes),
                'static_renderer_bindings_verified': len(seen_renderers),
                'max_matrix_error': max_error, 'draw_mappings_still_unqualified': mismatches,
                'inactive_components_colliders_and_lod_retained': True})
        for path, expected in pins.items():
            require(sha(path) == expected, 'Verification input changed during reopen: ' + path)
        result.update(status='native_prefab_pose_and_renderer_identities_reopened',
            prefabs_verified=len(result['rows']),
            nodes_verified=sum(row['nodes_verified'] for row in result['rows']),
            renderer_bindings_verified=sum(row['static_renderer_bindings_verified'] for row in result['rows']),
            all_input_pins_reopened_before_after=True, input_pins=pins)
    except Exception as error:
        result.update(status='refused_or_unverified', error=str(error))
    result['finished_utc'] = datetime.datetime.now(datetime.timezone.utc).isoformat()
    (root / 'RESULT.v1.json').write_text(json.dumps(result, indent=2) + '\n', 'utf-8')
    print(json.dumps({key: value for key, value in result.items() if key not in ['rows', 'input_pins']}))
    require(result['status'] != 'refused_or_unverified', result.get('error', 'Verification refused'))


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--receipt', required=True)
    parser.add_argument('--output', required=True)
    parser.add_argument('--expected', type=int, required=True)
    verify(parser.parse_args())
