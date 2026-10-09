"""Reopen pinned projection observations; arithmetic candidates remain diagnostic."""
from pathlib import Path
import copy
import math
import struct
import reopen_native_lod_boundary_v1 as base

base.N = base.W / 'rocks-firepit-native-lod-boundary-v2'
base.OUT = base.W / 'rocks-firepit-native-lod-boundary-reopen-v2'
original_validate = base.validate
WRAPPER_SHA = base.sha(Path(__file__))


def f32(value):
    return struct.unpack('<f', struct.pack('<f', value))[0]


def validate(samples, groups):
    result = original_validate(samples, groups)
    matrices, distances = {}, {}
    for sample in samples:
        key = (sample['prefab_path'], sample['node_path'], sample['field_of_view'])
        matrix = sample['camera_projection_matrix_row_major']
        cutoff = sample['native_threshold_distances']
        assert len(matrix) == 16 and all(math.isfinite(v) for v in matrix)
        assert matrix[0] > 0 and matrix[5] > 0 and matrix[14] == -1 and matrix[15] == 0
        assert all(matrix[i] == 0 for i in (1,2,3,4,6,7,8,9,12,13))
        assert len(cutoff) == len(groups[key[:2]]['levels'])
        assert all(math.isfinite(v) and v > 0 for v in cutoff)
        assert all(a < b for a,b in zip(cutoff,cutoff[1:]))
        m = tuple(base.bits(v) for v in matrix)
        d = tuple(base.bits(v) for v in cutoff)
        assert key not in matrices or matrices[key] == m
        assert key not in distances or distances[key] == d
        matrices[key], distances[key] = m,d
        if sample['neighbor_delta'] == 0:
            assert base.bits(sample['distance']) == d[sample['source_level_index']]
    negative = []
    for mutation in ('projection_shape', 'projection_nonfinite', 'projection_inconsistent', 'cutoff_count', 'cutoff_nonfinite', 'cutoff_inconsistent'):
        changed = copy.deepcopy(samples)
        if mutation == 'projection_shape': changed[0]['camera_projection_matrix_row_major'].pop()
        elif mutation == 'projection_nonfinite': changed[0]['camera_projection_matrix_row_major'][5] = float('nan')
        elif mutation == 'projection_inconsistent': changed[0]['camera_projection_matrix_row_major'][5] *= 2
        elif mutation == 'cutoff_count': changed[0]['native_threshold_distances'].pop()
        elif mutation == 'cutoff_nonfinite': changed[0]['native_threshold_distances'][0] = float('nan')
        else: changed[0]['native_threshold_distances'][0] *= 0.99
        try:
            validate_projection_only(changed, groups)
        except AssertionError:
            negative.append(mutation)
        else: raise AssertionError('Corrupt projection capture admitted')
    mismatches = {}
    for distance_kind in ('requested', 'native', 'position_f32_squared', 'position_f64_squared'):
        for inclusive in (False, True):
            count = 0
            for sample in samples:
                fields = {v['name']:v for v in sample['native_fields']}
                if distance_kind == 'requested':
                    observed = sample['distance']; cutoffs = sample['native_threshold_distances']
                elif distance_kind == 'native':
                    observed = fields['activeDistance']['float_value']; cutoffs = sample['native_threshold_distances']
                else:
                    diffs = [a-b for a,b in zip(sample['camera_position'],sample['world_reference_point'])]
                    if distance_kind == 'position_f32_squared':
                        diffs = [f32(d) for d in diffs]
                        observed = f32(f32(f32(diffs[0]*diffs[0])+f32(diffs[1]*diffs[1]))+f32(diffs[2]*diffs[2]))
                        cutoffs = [f32(v*v) for v in sample['native_threshold_distances']]
                    else:
                        observed = sum(v*v for v in diffs)
                        cutoffs = [v*v for v in sample['native_threshold_distances']]
                chosen = next((i for i,v in enumerate(cutoffs) if observed <= v), -1) if inclusive else next((i for i,v in enumerate(cutoffs) if observed < v),-1)
                count += chosen != fields['activeLODLevel']['integer_value']
            mismatches[distance_kind + ('_inclusive' if inclusive else '_strict')] = count
    assert base.sha(Path(__file__)) == WRAPPER_SHA
    result.update(supplementary_input_pins={str(Path(__file__)): WRAPPER_SHA}, projection_groups=len(matrices), projection_negative_controls_refused=negative,
                  distance_candidate_mismatches=mismatches, arithmetic_candidates_diagnostic_only=True)
    return result


def validate_projection_only(samples, groups):
    seen = {}
    for sample in samples:
        key = (sample['prefab_path'],sample['node_path'],sample['field_of_view'])
        m,d = sample['camera_projection_matrix_row_major'],sample['native_threshold_distances']
        assert len(m) == 16 and all(math.isfinite(v) for v in m)
        assert len(d) == len(groups[key[:2]]['levels'])
        assert all(math.isfinite(v) and v > 0 for v in d)
        signature = tuple(base.bits(v) for v in m+d)
        assert key not in seen or seen[key] == signature
        seen[key] = signature


if __name__ == '__main__':
    base.validate = validate
    base.main()
