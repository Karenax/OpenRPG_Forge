"""Isolate normal, FGD and sky factors of the two refused cases, without fitting."""
from pathlib import Path
BASE=Path(__file__).with_name('prepare_firepit_specular_gpu_case_v1.py')
source=BASE.read_text('utf-8').replace('firepit-specular-gpu-case-v1','firepit-specular-stage-gpu-case-v1')
start=source.index("witness=f'''");end=source.index('\nhelpers=',start)
witness='''let diagnostic=(u32(in.position.x)+512u*u32(in.position.y))%8u;
let original_normal=vec3<f32>(0.0,1.0,0.0);
let original_view=normalize(vec3<f32>(1.0,0.1,0.3));
let authored_r=select(-1.0,0.0,diagnostic/4u==1u);
let nr=source_deferred_normal_roughness(original_normal,authored_r);
let ndv=max(dot(nr.xyz,original_view),0.0001);
let f0=source_deferred_fresnel0(vec3<f32>(0.04));
let table=source_preintegrated_brdf_sample(ndv,nr.w);
let fgd=source_preintegrated_brdf_specular(table,f0,1.0);
let direction=source_deferred_reflection_direction(nr.xyz,original_view,nr.w,ndv);
let sky=source_prefiltered_sky_sample(direction,0,nr.w*(1.7-0.7*nr.w)*6.0).xyz;
let material_ao=source_deferred_specular_ao(max(dot(original_normal,original_view),0.0001),1.0,authored_r*authored_r);
let final=source_deferred_sky_specular(original_normal,original_view,authored_r,vec3<f32>(0.04),material_ao,1.0,1.0,1.0);
var value=nr.xyz;
switch diagnostic%4u {case 1u:{value=fgd;} case 2u:{value=sky;} case 3u:{value=final;} default:{}}
out.color=vec4<f32>(value,0.5);'''
source=source[:start]+'witness='+repr(witness)+source[end:]
source=source.replace('expected_cases=756','expected_cases=8')
source=source.replace('source_deferred_specular_756_case_gpu_witness_prepared_before_pixels','source_specular_two_refusals_four_stage_diagnostic_prepared')
exec(compile(source,str(BASE),'exec'),globals())
