"""Verify the 48 combined diffuse GPU cases against the predeclared protocol."""
from pathlib import Path
BASE=Path(__file__).with_name('reopen_firepit_diffuse_probe_gpu_v1.py')
source=BASE.read_text('utf-8').replace('firepit-diffuse-probe-gpu','firepit-combined-diffuse-gpu')
source=source.replace("pair['six_direction_diffuse_probe_witness'] and pair['diffuse_probe_bound_from_case']","pair['brdf_lookup_cases_expected']==48 and pair['shared_brdf_bound_materials']==4")
start=source.index('    c=[[row[k]');end=source.index(';rows=[];files=[]',start)
source=source[:start]+"    expected=case['combined_diffuse_expected_rgb']"+source[end:]
source=source.replace('range(6)','range(48)').replace('(x+y)%6','(x+y)%48')
source=source.replace('minimum_samples_per_direction','minimum_samples_per_case')
source=source.replace('directions=groups','cases=groups').replace("row['directions']","row['cases']")
source=source.replace('actual_gpu_diffuse_probe_six_direction_evaluation_verified','actual_gpu_48_case_deferred_diffuse_composition_verified')
source=source.replace('diffuse_probe_gpu_witness_refused','combined_diffuse_gpu_witness_refused')
source=source.replace('expected_source_irradiance=expected','expected_source_diffuse_rgb=expected')
source=source.replace('brdf_qualified=False','combined_diffuse_kernel_qualified=passed,scene_application_qualified=False')
source=source.replace('Connect diffuse irradiance through the actual source BRDF/occlusion/exposure with captured FGD and HDRI, then repeat original scene comparison','Apply this measured transport on imported surface and receiver; qualify screen AO sampling, HDRI specular and shadow parity before original scene comparison')
exec(compile(source,str(BASE),'exec'),globals())
