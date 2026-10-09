"""Infer a native threshold comparison only from complete pinned boundary measurements."""
from pathlib import Path
from datetime import datetime, timezone
import collections
import copy
import hashlib
import json
import math
import struct

W = Path('F:/OpenRPG_ImportWork')
N = W / 'rocks-firepit-native-lod-boundary-v1'
OUT = W / 'rocks-firepit-native-lod-boundary-reopen-v1'


def read(path):
    return json.loads(path.read_text('utf-8-sig'))


def sha(path):
    with Path(path).open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


def bits(value):
    assert math.isfinite(value)
    return struct.unpack('>I', struct.pack('>f', value))[0]


def validate(samples, groups):
    expected = {(prefab, node, fov, index, delta) for (prefab, node), group in groups.items()
                for fov in (30.0, 60.0, 90.0) for index in range(len(group['levels'])) for delta in (-1, 0, 1)}
    seen, equal, candidates = set(), set(), {'strict_greater', 'greater_or_equal'}
    equality_rows, chosen_counts = [], collections.Counter()
    for sample in samples:
        key = (sample['prefab_path'], sample['node_path'], sample['field_of_view'], sample['source_level_index'], sample['neighbor_delta'])
        assert key in expected and key not in seen
        seen.add(key)
        group = groups[key[:2]]
        assert group['fade_mode'] == 0 and not group['animate_cross_fading']
        thresholds = [level['screen_relative_height'] for level in group['levels']]
        threshold = thresholds[key[3]]
        assert bits(sample['source_threshold']) == bits(threshold)
        assert sample['source_threshold_bits'] == f'{bits(threshold):08X}'
        assert bits(sample['requested_relative_height']) == bits(threshold) + key[4]
        assert sample['requested_relative_height_bits'] == f'{bits(sample["requested_relative_height"]):08X}'
        assert sample['distance'] > 0 and sample['distance_bits'] == f'{bits(sample["distance"]):08X}'
        assert sample['group_enabled'] and bits(sample['source_size']) == bits(group['size'])
        assert [bits(v) for v in sample['local_reference_point']] == [bits(v) for v in group['local_reference_point']]
        fields = {field['name']: field for field in sample['native_fields']}
        assert len(fields) == len(sample['native_fields'])
        for field in fields.values():
            if field['type'] == 'System.Single':
                assert field['float_bits'] == f'{bits(field["float_value"]):08X}'
            else:
                assert field['type'] == 'System.Int32' and type(field['integer_value']) is int
        height = fields['activeRelativeScreenSize']['float_value']
        chosen = fields['activeLODLevel']['integer_value']
        assert -1 <= chosen < len(thresholds)
        strict = next((i for i, value in enumerate(thresholds) if height > value), -1)
        inclusive = next((i for i, value in enumerate(thresholds) if height >= value), -1)
        if chosen != strict:
            candidates.discard('strict_greater')
        if chosen != inclusive:
            candidates.discard('greater_or_equal')
        chosen_counts[chosen] += 1
        if bits(height) == bits(threshold):
            equal.add((key[0], key[1], key[3]))
            equality_rows.append(dict(case=list(key), native_level=chosen, strict_level=strict, inclusive_level=inclusive))
    assert seen == expected
    all_boundaries = {(prefab, node, index) for (prefab, node), group in groups.items() for index in range(len(group['levels']))}
    return dict(samples=len(samples), boundaries=len(all_boundaries), exact_equal_boundaries=len(equal),
                exact_equality_samples=len(equality_rows), comparison_rule_candidates=sorted(candidates),
                equality_rows=equality_rows, active_level_distribution=dict(chosen_counts),
                every_boundary_exactly_observed=equal == all_boundaries)


def main():
    assert not OUT.exists(), 'Preserve previous measurement review'
    run = read(N / 'RUN-RESULT.v1.json')
    assert run['actual_unity_exit_code'] == 0 and run['status'] == 'native_lod_threshold_boundaries_captured_reopen_pending'
    native_path = N / 'NATIVE-RESULT.v1.json'
    assert sha(native_path) == run['native_result_sha256']
    prepared = read(N / 'PREPARED.v1.json')
    pins = dict(prepared['input_pins'])
    for path, expected in pins.items():
        assert sha(Path(path)) == expected
    native = read(native_path)
    assert native['captured_project_lod_bias'] == 1.0 and native['captured_project_maximum_lod_level'] == 0
    assert not native['actual_gpu_visibility_measured'] and not native['particle_behavior_measured']
    assembly_path = W / 'rocks-firepit-scene-bindings-v1/RESULT.v1.json'
    groups = {}
    for scene in read(assembly_path)['rows']:
        prefab = next(row['source_node']['gameobject']['asset_path'] for row in scene['nodes'] if row['source_node']['path'] == 'root')
        for group in scene['original_lod_groups']:
            groups[(prefab, group['node_path'])] = group
    result = validate(native['samples'], groups)
    assert result['samples'] == prepared['expected_samples']
    refused = []
    for case in ('missing_case', 'duplicate_case', 'threshold_bits', 'neighbor_delta', 'native_float_bits'):
        samples = copy.deepcopy(native['samples'])
        if case == 'missing_case':
            samples.pop()
        elif case == 'duplicate_case':
            samples[-1] = samples[0]
        elif case == 'threshold_bits':
            samples[0]['source_threshold_bits'] = '00000000'
        elif case == 'neighbor_delta':
            samples[0]['neighbor_delta'] = 9
        else:
            next(field for field in samples[0]['native_fields'] if field['name'] == 'activeRelativeScreenSize')['float_bits'] = '00000000'
        try:
            validate(samples, groups)
        except AssertionError:
            refused.append(case)
        else:
            raise AssertionError('Corrupted native boundary capture admitted')
    for path, expected in pins.items():
        assert sha(Path(path)) == expected
    pins[str(Path(__file__))] = sha(Path(__file__))
    pins[str(native_path)] = sha(native_path)
    qualified = result['every_boundary_exactly_observed'] and len(result['comparison_rule_candidates']) == 1
    result.update(status='native_threshold_comparison_rule_measured' if qualified else 'native_threshold_rule_or_equality_coverage_inconclusive',
                  finished_utc=datetime.now(timezone.utc).isoformat(), input_pins=pins, negative_controls_refused=refused,
                  native_metric_comparison_rule_qualified=qualified, forge_lod_runtime_qualified=False,
                  actual_gpu_visibility_qualified=False, full_import=False)
    OUT.mkdir()
    (OUT / 'RESULT.v1.json').write_text(json.dumps(result, indent=2), 'utf-8')
    print(json.dumps({k: result[k] for k in ('status', 'samples', 'boundaries', 'exact_equal_boundaries', 'comparison_rule_candidates')}))


if __name__ == '__main__':
    main()
