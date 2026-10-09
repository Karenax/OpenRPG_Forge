"""Prepare the private 182-case cube witness and limits before target pixels."""
from pathlib import Path
import json

BASE=Path(__file__).with_name('prepare_firepit_diffuse_probe_gpu_case_v1.py')
W=Path('F:/OpenRPG_ImportWork')
SAMPLES=W/'firepit-source-sky-sampling-reopen-v3/RESULT.v1.json'
PACKET=W/'source-sky-cube-packet-v1/PACKET.v1.json'
sample_protocol=json.loads(SAMPLES.read_text('utf-8'))
assert sample_protocol['status']=='native_source_sky_182_cases_two_views_and_storage_verified'
sample_rows=sample_protocol['rows'];assert len(sample_rows)==182
directions=[sample_rows[i*7]['source_direction'] for i in range(26)]
lods=[sample_rows[i]['source_lod'] for i in range(7)]
for i,row in enumerate(sample_rows):
    assert row['source_direction']==directions[i//7] and row['source_lod']==lods[i%7]
    assert row['source_gpu_rgba'][3]==1.0

helper='''@group(#{MATERIAL_BIND_GROUP}) @binding(123) var source_sky_cube_image:texture_cube_array<f32>;
@group(#{MATERIAL_BIND_GROUP}) @binding(124) var source_sky_cube_sampler:sampler;
fn source_prefiltered_sky_sample(source_direction:vec3<f32>,slice:i32,lod:f32)->vec4<f32> {
    return textureSampleLevel(source_sky_cube_image,source_sky_cube_sampler,normalize(source_direction),slice,lod);
}
'''
vectors=','.join('vec3<f32>('+','.join(f'{v:.1f}' for v in d)+')' for d in directions)
levels=','.join(repr(float(v)) for v in lods)
witness=f'''let witness_index=(u32(in.position.x)+512u*u32(in.position.y))%182u;
let source_directions=array<vec3<f32>,26>({vectors});
let source_lods=array<f32,7>({levels});
let sample=source_prefiltered_sky_sample(source_directions[witness_index/7u],0,source_lods[witness_index%7u]);
out.color=vec4<f32>(sample.xyz,sample.w*0.5);'''

source=BASE.read_text('utf-8').replace('firepit-diffuse-probe-gpu-case-v1','firepit-sky-cube-gpu-case-v1')
source=source.replace('private-six-direction-witness.glb','private-sky-cube-witness.glb')
source=source.replace("helper=ENGINE/'shaders/source_diffuse_probe.wgsl';helper_text=helper.read_text('utf-8')",
    'helper=Path(__file__).resolve();helper_text='+repr(helper))
start=source.index("    witness='");end=source.index('\n',start)
source=source[:start]+'    witness='+repr(witness)+source[end:]
source=source.replace('private_diffuse_probe_witness','private_sky_cube_witness')
source=source.replace("    (ROOT/'CASE.v1.json').write_text",
    "    case['sky_sampling_protocol']=read(W/'firepit-source-sky-sampling-reopen-v3/RESULT.v1.json')\n"
    "    case['sky_cube_packet']=read(W/'source-sky-cube-packet-v1/PACKET.v1.json')\n"
    "    (ROOT/'CASE.v1.json').write_text")
source=source.replace('six_direction_gpu_witness_prepared_before_pixels','source_sky_182_case_gpu_witness_prepared_before_pixels')
source=source.replace('expected_directions=6','expected_cases=182')
source=source.replace('minimum_samples_per_direction=100','minimum_samples_per_case=50')
source=source.replace('absolute_channel_error_floor=0.0001','absolute_channel_error_floor=0.001')
source=source.replace('mathematical_irradiance_only=True','source_prefiltered_cube_sampling_only=True,source_direction_basis_explicit=True,target_basis_conversion_qualified=False')
source=source.replace('case_path,fixture,environment,audit,',
    "case_path,fixture,environment,audit,W/'firepit-source-sky-sampling-reopen-v3/RESULT.v1.json',W/'source-sky-cube-packet-v1/PACKET.v1.json',Path(read(W/'source-sky-cube-packet-v1/PACKET.v1.json')['path']),")
exec(compile(source,str(BASE),'exec'),globals())
