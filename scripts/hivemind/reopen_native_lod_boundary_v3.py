"""Reopen native culling parameters and require deterministic prior observations."""
from pathlib import Path
import copy, math
import reopen_native_lod_boundary_v2 as projection
base = projection.base
base.N = base.W / 'rocks-firepit-native-lod-boundary-v3'
base.OUT = base.W / 'rocks-firepit-native-lod-boundary-reopen-v3'
PREVIOUS_PATH = base.W / 'rocks-firepit-native-lod-boundary-v2/NATIVE-RESULT.v1.json'
PREVIOUS_SHA = base.sha(PREVIOUS_PATH)
PREVIOUS = base.read(PREVIOUS_PATH)['samples']
assert PREVIOUS_SHA == base.read(PREVIOUS_PATH.parent/'RUN-RESULT.v1.json')['native_result_sha256']
SCRIPT_SHA = base.sha(Path(__file__))

def check_culling(samples):
    assert len(samples) == len(PREVIOUS)
    for sample, previous in zip(samples,PREVIOUS):
        assert {k:v for k,v in sample.items() if not k.startswith('culling_lod_')} == previous
        assert sample['culling_lod_is_orthographic'] is False
        assert base.bits(sample['culling_lod_field_of_view']) == base.bits(sample['field_of_view'])
        assert math.isfinite(sample['culling_lod_ortho_size']) and sample['culling_lod_ortho_size'] > 0
        assert type(sample['culling_lod_pixel_height']) is int and sample['culling_lod_pixel_height'] > 0
        assert len(sample['culling_lod_camera_position']) == 3
        assert [base.bits(v) for v in sample['culling_lod_camera_position']] == [base.bits(v) for v in sample['camera_position']]

def validate(samples,groups):
    result = projection.validate(samples,groups)
    check_culling(samples)
    refused=[]
    for case in ('orthographic_flag','culling_fov','pixel_height','culling_position','previous_native_decision'):
        rows=copy.deepcopy(samples)
        if case=='orthographic_flag':rows[0]['culling_lod_is_orthographic']=True
        elif case=='culling_fov':rows[0]['culling_lod_field_of_view']+=1
        elif case=='pixel_height':rows[0]['culling_lod_pixel_height']=0
        elif case=='culling_position':rows[0]['culling_lod_camera_position'][2]+=1
        else:next(v for v in rows[0]['native_fields'] if v['name']=='activeLODLevel')['integer_value']=-1
        try:check_culling(rows)
        except AssertionError:refused.append(case)
        else:raise AssertionError('Corrupt native culling observation admitted')
    assert base.sha(PREVIOUS_PATH)==PREVIOUS_SHA and base.sha(Path(__file__))==SCRIPT_SHA
    result['supplementary_input_pins'].update({str(PREVIOUS_PATH):PREVIOUS_SHA,str(Path(__file__)):SCRIPT_SHA})
    result.update(native_observations_identical_to_projection_capture=len(samples),
                  native_culling_camera_position_and_fov_equal=True,
                  native_culling_negative_controls_refused=refused,
                  native_culling_parameters_ruled_out_as_source_of_projection_discrepancy=True)
    return result
if __name__=='__main__':
    base.validate=validate
    base.main()
