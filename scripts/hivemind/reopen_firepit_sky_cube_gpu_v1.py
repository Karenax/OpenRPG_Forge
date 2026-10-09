"""Read the actual two GPU buffers against all 182 prior Unity samples."""
from pathlib import Path
BASE_SKY=Path(__file__).with_name('reopen_firepit_diffuse_probe_gpu_v1.py')
source=BASE_SKY.read_text('utf-8')
source=source.replace('firepit-diffuse-probe-gpu','firepit-sky-cube-gpu')
source=source.replace("pair['six_direction_diffuse_probe_witness'] and pair['diffuse_probe_bound_from_case']",
    "pair['sky_cube_cases_expected']==182 and pair['shared_sky_cube_bound_materials']==4 and pair['missing_sky_frame_refused_atomically']")
start=source.index('    c=[[row[k]');end=source.index(';rows=[];files=[]',start)
source=source[:start]+"    expected=[r['source_gpu_rgba'][:3] for r in case['sky_sampling_protocol']['rows']]"+source[end:]
source=source.replace('range(6)','range(182)').replace('(x+y)%6','(x+512*y)%182')
source=source.replace('minimum_samples_per_direction','minimum_samples_per_case')
source=source.replace('directions=groups','cases=groups').replace("row['directions']","row['cases']")
source=source.replace('actual_gpu_diffuse_probe_six_direction_evaluation_verified','actual_gpu_source_sky_182_case_sampling_verified')
source=source.replace('diffuse_probe_gpu_witness_refused','source_sky_gpu_sampling_witness_refused')
source=source.replace('expected_source_irradiance=expected','expected_source_sky_rgb=expected,sky_cube_cases_verified=182 if passed else 0')
source=source.replace('brdf_qualified=False','source_sky_sampling_qualified=passed,sky_specular_applied_to_scene=False,brdf_qualified=False')
source=source.replace('Connect diffuse irradiance through the actual source BRDF/occlusion/exposure with captured FGD and HDRI, then repeat original scene comparison',
    'Apply the source HDRI specular with measured BRDF and source direction/roughness/occlusion/exposure; repeat original scene comparison')
source=source.replace('Inspect raw alpha and directional errors; retain refusal without fitting coefficients or tolerances',
    'Inspect cube face, mip and sampling errors; retain the refusal without fitting source data or tolerances')
exec(compile(source,str(BASE_SKY),'exec'),globals())
