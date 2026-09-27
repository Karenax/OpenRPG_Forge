"""Measure paired sRGB albedo passes, without making a lighting parity claim."""
import hashlib
import json
import sys
from pathlib import Path
import numpy as np
from PIL import Image

reference, candidate, output = map(Path,sys.argv[1:4])
mode = sys.argv[4] if len(sys.argv) == 5 else 'albedo'
assert mode in ('albedo','direct','environment','hill','shadows','sky') and len(sys.argv) in (4,5)
assert not output.exists()
def load(path):
    with Image.open(path) as im:
        data = np.asarray(im.convert('RGB')).astype(np.int16)
    assert data.shape == (1080,1920,3)
    return data
a,b = load(reference),load(candidate)
occupied_a,occupied_b = np.any(a != 0,axis=2),np.any(b != 0,axis=2)
union = occupied_a | occupied_b
assert 100 < int(union.sum()) <= union.size
delta = np.abs(a-b)
result = {'schema':f'forge.tidewater.{mode}-comparison','version':1,
    'status':f'measured_fixed_{mode}_not_full_scene_parity',
    'reference':str(reference.resolve()),'candidate':str(candidate.resolve()),
    'reference_sha256':hashlib.sha256(reference.read_bytes()).hexdigest(),
    'candidate_sha256':hashlib.sha256(candidate.read_bytes()).hexdigest(),
    'occupied_union_pixels':int(union.sum()),
    'coverage_disagreement_pixels':int((occupied_a ^ occupied_b).sum()),
    'exact_rgb_pixels_in_union':int(np.all(a[union]==b[union],axis=1).sum()),
    'mean_absolute_channel_error_8bit_on_union':float(delta[union].mean()),
    'p99_max_channel_error_8bit_on_union':float(np.percentile(delta.max(axis=2)[union],99)),
    'max_channel_error_8bit':int(delta.max()),
    'limits':['single_camera_and_time','initial_dynamic_group_poses','no_lighting_normal_or_temporal_verdict']}
if mode == 'direct':
    result['limits']=['single_camera_and_time','unit_direct_light_only',
        'no_environment_shadows_postprocessing_or_temporal_verdict',
        'occupied_pixels_are_nonblack_color_not_geometry_coverage']
if mode == 'environment':
    result['limits']=['single_camera_and_time','acquired_cold_environment',
        'no_scene_shadows_bounce_postprocessing_or_temporal_verdict',
        'occupied_pixels_are_nonblack_color_not_geometry_coverage']
if mode == 'hill':
    result['limits']=['single_camera_and_time','native_heightfield_shadows',
        'no_object_shadows_cloud_shadows_bounce_postprocessing_or_temporal_verdict',
        'occupied_pixels_are_nonblack_color_not_geometry_coverage']
if mode == 'shadows':
    result['limits']=['single_camera_and_time','native_heightfield_and_object_shadows',
        'acquired_cold_environment','no_cloud_shadows_bounce_postprocessing_or_temporal_verdict',
        'occupied_pixels_are_nonblack_color_not_geometry_coverage']
if mode == 'sky':
    result['limits']=['single_camera_and_time','native_clear_sky_and_object_shadows',
        'acquired_cold_environment_for_surface_lighting',
        'no_clouds_ocean_bounce_postprocessing_or_temporal_verdict',
        'occupied_pixels_are_nonblack_color_not_geometry_coverage']
output.write_text(json.dumps(result,indent=2),encoding='utf-8')
print(json.dumps(result))
