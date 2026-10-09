"""Reopen all 35 target GPU cases against the prior source BRDF protocol."""
from pathlib import Path
BASE = Path(__file__).with_name('reopen_firepit_diffuse_probe_gpu_v1.py')
source = BASE.read_text('utf-8').replace('firepit-diffuse-probe-gpu', 'firepit-brdf-lookup-gpu')
source = source.replace("pair['six_direction_diffuse_probe_witness'] and pair['diffuse_probe_bound_from_case']",
    "pair['brdf_lookup_cases_expected']==35 and pair['shared_brdf_bound_materials']==4")
start = source.index('    c=[[row[k]')
end = source.index(';rows=[];files=[]', start)
source = source[:start] + "    expected=[[r['sampled_rgba'][0],r['sampled_rgba'][1],r['diffuse_fgd']] for r in case['brdf_lookup_protocol']['sampling_cases']]" + source[end:]
source = source.replace('range(6)', 'range(35)').replace('(x+y)%6', '(x+y)%35')
source = source.replace("minimum_samples_per_direction", "minimum_samples_per_case")
source = source.replace('directions=groups', 'cases=groups').replace("row['directions']", "row['cases']")
source = source.replace('actual_gpu_diffuse_probe_six_direction_evaluation_verified', 'actual_gpu_brdf_35_case_sampling_verified')
source = source.replace('diffuse_probe_gpu_witness_refused', 'brdf_gpu_sampling_witness_refused')
source = source.replace('expected_source_irradiance=expected', 'expected_source_brdf_samples=expected')
source = source.replace('brdf_qualified=False', 'brdf_lookup_sampling_qualified=passed,brdf_applied_to_scene=False')
source = source.replace('Connect diffuse irradiance through the actual source BRDF/occlusion/exposure with captured FGD and HDRI, then repeat original scene comparison',
    'Apply measured SH through captured source FGD/occlusion/exposure; HDRI specular and shadow parity remain open')
exec(compile(source, str(BASE), 'exec'), globals())
