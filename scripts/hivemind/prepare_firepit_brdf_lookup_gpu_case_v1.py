"""Use the private GPU witness machinery with 35 pinned source BRDF samples."""
from pathlib import Path
BASE = Path(__file__).with_name('prepare_firepit_diffuse_probe_gpu_case_v1.py')
source = BASE.read_text('utf-8').replace('firepit-diffuse-probe-gpu-case-v1', 'firepit-brdf-lookup-gpu-case-v1')
source = source.replace('shaders/source_diffuse_probe.wgsl', 'shaders/source_preintegrated_brdf.wgsl')
source = source.replace('private-six-direction-witness.glb', 'private-brdf-lookup-witness.glb')
start = source.index("    witness='")
end = source.index('\n', start)
witness = '''let witness_index=(u32(in.position.x)+u32(in.position.y))%35u;
let ndotv_cases=array<f32,5>(0.0001,0.01,0.1,0.5,1.0);
let roughness_cases=array<f32,7>(-1.0,0.0,0.25,0.5,0.75,1.0,2.0);
let lookup=source_preintegrated_brdf_sample(ndotv_cases[witness_index/7u],roughness_cases[witness_index%7u]);
out.color=vec4<f32>(lookup.xy,source_preintegrated_brdf_diffuse(lookup),0.5);'''
source = source[:start] + '    witness=' + repr(witness) + source[end:]
source = source.replace('private_diffuse_probe_witness', 'private_brdf_lookup_witness')
source = source.replace("    (ROOT/'CASE.v1.json').write_text", "    case['brdf_lookup_protocol']=read(W/'source-brdf-lookup-case-v1/CASE.v1.json')\n    case['brdf_lookup_protocol_path']=str(W/'source-brdf-lookup-case-v1/CASE.v1.json')\n    case['brdf_lookup_protocol_sha256']=sha(W/'source-brdf-lookup-case-v1/CASE.v1.json')\n    (ROOT/'CASE.v1.json').write_text")
source = source.replace('six_direction_gpu_witness_prepared_before_pixels', 'brdf_lookup_gpu_witness_prepared_before_pixels')
source = source.replace('expected_directions=6', 'expected_cases=35')
source = source.replace('minimum_samples_per_direction=100', 'minimum_samples_per_case=100')
source = source.replace('mathematical_irradiance_only=True', 'private_brdf_sampling_only=True')
source = source.replace('case_path,fixture,environment,audit,', "case_path,fixture,environment,audit,W/'source-brdf-lookup-case-v1/CASE.v1.json',")
exec(compile(source, str(BASE), 'exec'), globals())
