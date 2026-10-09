"""Reconcile captured Unity LOD decisions with pinned original geometry.

This checks native editor measurements, not Forge rendering or GPU visibility.
"""
from pathlib import Path
import copy
import datetime
import hashlib
import json
import math
import struct

W = Path('F:/OpenRPG_ImportWork')
N = W / 'rocks-firepit-native-lod-metric-v1'
ROOT = W / 'rocks-firepit-native-lod-metric-reopen-v1'


def read(path):
    return json.loads(Path(path).read_text('utf-8-sig'))


def sha(path):
    with Path(path).open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


def bits(value):
    assert math.isfinite(value)
    return struct.pack('>f', value).hex().upper()


def vector(left, right):
    return len(left) == len(right) == 3 and all(bits(a) == bits(b) for a, b in zip(left, right))


def validate(samples, groups, geometry):
    expected = {(path, node, fov, bits(height)) for path, node in groups
                for fov in (30.0, 60.0, 90.0)
                for height in (0.01, 0.03, 0.1, 0.18, 0.25, 0.35, 0.6)}
    seen = set()
    distribution = {}
    discrepancies = []
    integer_names = {'triangleCount', 'vertexCount', 'rendererCount', 'submeshCount', 'activeLODLevel'}
    float_names = {'activeLODFade', 'activeDistance', 'activeRelativeScreenSize', 'activePixelSize', 'worldSpaceSize'}
    for sample in samples:
        key = (sample['prefab_path'], sample['node_path'], sample['field_of_view'], bits(sample['requested_relative_height']))
        assert key in expected and key not in seen, ('unexpected_or_duplicate_case', key)
        seen.add(key)
        group = groups[key[:2]]
        assert sample['group_enabled'] is True
        assert bits(sample['source_size']) == bits(group['size'])
        assert vector(sample['local_reference_point'], group['local_reference_point'])
        assert bits(sample['aspect']) == bits(16 / 9)
        assert sample['distance'] > 0 and bits(sample['distance']) == sample['distance_bits']
        for name in ('world_reference_point', 'lossy_scale', 'camera_position'):
            assert len(sample[name]) == 3
            for value in sample[name]:
                bits(value)
        fields = {row['name']: row for row in sample['native_fields']}
        assert len(fields) == len(sample['native_fields']) and set(fields) == integer_names | float_names
        for name, field in fields.items():
            if name in float_names:
                assert field['type'] == 'System.Single' and bits(field['float_value']) == field['float_bits']
            else:
                assert field['type'] == 'System.Int32' and type(field['integer_value']) is int
        height = fields['activeRelativeScreenSize']['float_value']
        chosen = fields['activeLODLevel']['integer_value']
        # These fixtures have no cross-fading and the samples avoid exact thresholds.
        assert group['fade_mode'] == 0 and not group['animate_cross_fading']
        assert all(bits(height) != bits(level['screen_relative_height']) for level in group['levels'])
        expected_level = next((i for i, level in enumerate(group['levels'])
                               if height > level['screen_relative_height']), -1)
        assert chosen == expected_level, ('lod_decision', key, chosen, expected_level)
        identities = [] if chosen == -1 else group['levels'][chosen]['renderer_components']
        counters = dict(triangleCount=0, vertexCount=0, submeshCount=0, rendererCount=len(identities))
        for identity in identities:
            mesh = geometry[(identity['guid'], identity['file_id'])]
            for native, source in [('triangleCount', 'triangles'), ('vertexCount', 'vertices'), ('submeshCount', 'submeshes')]:
                counters[native] += mesh[source]
        for name, count in counters.items():
            observed = fields[name]['integer_value']
            if chosen == -1:
                assert observed == count == 0, ('culled_geometry_counter', key, name)
            elif observed != count:
                discrepancies.append(dict(case=list(key), counter=name,
                                          native=observed, previous_geometry=count))
        distribution[str(chosen)] = distribution.get(str(chosen), 0) + 1
    assert seen == expected and len(seen) == 294, 'missing_cases'
    return distribution, discrepancies


def main():
    assert not ROOT.exists(), 'Immutable result already exists'
    run = read(N / 'RUN-RESULT.v1.json')
    assert run['actual_unity_exit_code'] == 0 and run['status'] == 'actual_native_lod_metrics_captured_reopen_pending'
    assert sha(N / 'NATIVE-RESULT.v1.json') == run['native_result_sha256']
    prepared = read(N / 'PREPARED.v1.json')
    correction = read(N / 'PREPARATION-CONTEXT-CORRECTION.v1.json')
    assert sha(N / 'PREPARED.v1.json') == correction['after_sha256']
    assert sha(N / 'PREPARED-COMPONENT-REUSE.v1.json') == correction['before_sha256']
    assert prepared['prefab_instantiated'] and prepared['private_metric_fixture_clone'] and not prepared['camera_rendered']
    pins = dict(prepared['input_pins'])
    dependency = prepared['reused_preparation_dependency']
    pins[dependency['path']] = dependency['sha256']
    for path, digest in pins.items():
        assert sha(path) == digest, path
    native = read(N / 'NATIVE-RESULT.v1.json')
    assert native['captured_project_lod_bias'] == 1.0 and native['captured_project_maximum_lod_level'] == 0
    assert not native['actual_gpu_visibility_measured'] and not native['particle_behavior_measured']
    assembly_path = W / 'rocks-firepit-scene-bindings-v1/RESULT.v1.json'
    assembly = read(assembly_path)
    groups, geometry = {}, {}
    for scene in assembly['rows']:
        prefab_path = next(row['source_node']['gameobject']['asset_path']
                           for row in scene['nodes'] if row['source_node']['path'] == 'root')
        for group in scene['original_lod_groups']:
            groups[(prefab_path, group['node_path'])] = group
        for renderer in scene['static_renderer_bindings']:
            path = Path(renderer['geometry_derivative']) / 'GEOMETRY.v1.json'
            assert sha(path) == renderer['geometry_receipt_sha256']
            pins[str(path)] = renderer['geometry_receipt_sha256']
            data = read(path)
            identity = renderer['renderer_identity']
            geometry[(identity['guid'], identity['file_id'])] = data['geometry']
    assert len(groups) == 14
    distribution, discrepancies = validate(native['samples'], groups, geometry)
    refused = []
    for name in ('missing_case', 'duplicate_case', 'distance_bits', 'reference_point', 'native_float_bits', 'geometry_counter'):
        altered = copy.deepcopy(native['samples'])
        if name == 'missing_case':
            altered.pop()
        elif name == 'duplicate_case':
            altered[-1] = altered[0]
        elif name == 'distance_bits':
            altered[0]['distance_bits'] = '00000000'
        elif name == 'reference_point':
            altered[0]['local_reference_point'][0] += 1
        elif name == 'native_float_bits':
            next(f for f in altered[0]['native_fields'] if f['name'] == 'activeDistance')['float_bits'] = '00000000'
        else:
            next(f for f in altered[0]['native_fields'] if f['name'] == 'triangleCount')['integer_value'] += 1
        try:
            validate(altered, groups, geometry)
        except AssertionError:
            refused.append(name)
        else:
            raise AssertionError(('corruption_not_refused', name))
    for path, digest in pins.items():
        assert sha(path) == digest, path
    for path in [Path(__file__), assembly_path, *[N / name for name in ('NATIVE-RESULT.v1.json', 'RUN-RESULT.v1.json', 'PREPARED.v1.json', 'PREPARED-COMPONENT-REUSE.v1.json', 'PREPARATION-CONTEXT-CORRECTION.v1.json')]]:
        pins[str(path)] = sha(path)
    result = dict(status=('refused_native_geometry_counter_discrepancies' if discrepancies
                          else 'native_lod_measurements_and_original_geometry_reconciled'),
                  samples=294, lod_groups=14, active_lod_distribution=distribution,
                  native_geometry_counters_verified=not discrepancies,
                  geometry_counter_discrepancies=discrepancies,
                  native_lod_decisions_compared=True, float_bits_verified=True,
                  source_pins_unchanged=True, negative_controls_refused=refused,
                  threshold_boundary_behavior_qualified=False,
                  forge_lod_runtime_qualified=False, gpu_visibility_qualified=False,
                  full_import=False, input_pins=pins,
                  finished_utc=datetime.datetime.now(datetime.timezone.utc).isoformat())
    ROOT.mkdir()
    (ROOT / 'RESULT.v1.json').write_text(json.dumps(result, indent=2), 'utf-8')
    print(json.dumps({k: v for k, v in result.items() if k != 'input_pins'}))


if __name__ == '__main__':
    main()
