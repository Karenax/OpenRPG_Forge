"""Predeclare 48 combined source diffuse cases before any target pixels."""
from pathlib import Path
BASE=Path(__file__).with_name('prepare_firepit_diffuse_probe_gpu_case_v1.py')
source=BASE.read_text('utf-8').replace('firepit-diffuse-probe-gpu-case-v1','firepit-combined-diffuse-gpu-case-v1').replace('private-six-direction-witness.glb','private-combined-diffuse-witness.glb')
source=source.replace("helper_text=helper.read_text('utf-8')", "helper_text=helper.read_text('utf-8')+'\\n'+(ENGINE/'shaders/source_preintegrated_brdf.wgsl').read_text('utf-8')+'\\n'+(ENGINE/'shaders/source_deferred_diffuse.wgsl').read_text('utf-8')")
start=source.index("    witness='");end=source.index('\n',start)
witness='''let witness_index=(u32(in.position.x)+u32(in.position.y))%48u;
let normals=array<vec3<f32>,6>(vec3<f32>(1.0,0.0,0.0),vec3<f32>(-1.0,0.0,0.0),vec3<f32>(0.0,1.0,0.0),vec3<f32>(0.0,-1.0,0.0),vec3<f32>(0.0,0.0,1.0),vec3<f32>(0.0,0.0,-1.0));
let colors=array<vec3<f32>,4>(vec3<f32>(0.0),vec3<f32>(0.2,0.4,0.8),vec3<f32>(0.8,0.3,0.1),vec3<f32>(1.0));
let material_aos=array<f32,3>(0.0,0.25,1.0);
let screen_visibilities=array<f32,4>(0.0,0.482421875,0.75,1.0);
let ndotvs=array<f32,5>(0.0001,0.01,0.1,0.5,1.0);
let roughnesses=array<f32,7>(-1.0,0.0,0.25,0.5,0.75,1.0,2.0);
let irradiance=source_preconvolved_diffuse_irradiance(normals[witness_index%6u]);
let fgd=source_preintegrated_brdf_diffuse(source_preintegrated_brdf_sample(ndotvs[witness_index%5u],roughnesses[witness_index%7u]));
let diffuse=source_deferred_diffuse_lighting(irradiance,fgd,colors[witness_index/12u],material_aos[(witness_index/4u)%3u],screen_visibilities[witness_index%4u]);
out.color=vec4<f32>(diffuse*0.0008138020639307797,0.5);'''
source=source[:start]+'    witness='+repr(witness)+source[end:]
addition="""
    case['brdf_lookup_protocol']=read(W/'source-brdf-lookup-case-v1/CASE.v1.json')
    context=read(W/'firepit-source-diffuse-context-reopen-v2/RESULT.v1.json')
    exposure=read(W/'firepit-source-exposure-reopen-v1/RESULT.v1.json')
    assert context['effective_lit_shader_mode']=='Deferred'
    assert exposure['rows'][0]['effective_source_multiplier']==0.0008138020639307797
    case['combined_diffuse_exposure']=0.0008138020639307797
    coefficients=[[r[k] for k in ['x','y','z','w']] for r in case['diffuse_probe_coefficients']]
    normals=[(-1,0,0),(1,0,0),(0,1,0),(0,-1,0),(0,0,1),(0,0,-1)]
    colors=[(0,0,0),(.2,.4,.8),(.8,.3,.1),(1,1,1)]
    expected=[]
    for i in range(48):
        x,y,z=normals[i%6];linear=[x,y,z,1];quadratic=[x*y,y*z,z*z,z*x]
        irradiance=[sum(coefficients[ch][j]*linear[j] for j in range(4))+sum(coefficients[ch+3][j]*quadratic[j] for j in range(4))+coefficients[6][ch]*(x*x-y*y) for ch in range(3)]
        fgd=case['brdf_lookup_protocol']['sampling_cases'][(i%5)*7+i%7]['diffuse_fgd']
        ao=[0,.25,1][(i//4)%3];visibility=[0,.482421875,.75,1][i%4]
        color=colors[i//12]
        bounce=[max(visibility,((visibility*(2.0404*c-.3324)+(-4.7951*c+.6417))*visibility+(2.7552*c+.6903))*visibility) for c in color]
        expected.append([irradiance[ch]*fgd*color[ch]*ao*bounce[ch]*case['combined_diffuse_exposure'] for ch in range(3)])
    case['combined_diffuse_expected_rgb']=expected
"""
source=source.replace("    (ROOT/'CASE.v1.json').write_text",addition+"\n    (ROOT/'CASE.v1.json').write_text")
source=source.replace('expected_directions=6','expected_cases=48').replace('minimum_samples_per_direction=100','minimum_samples_per_case=100')
source=source.replace('six_direction_gpu_witness_prepared_before_pixels','combined_diffuse_gpu_witness_prepared_before_pixels')
source=source.replace('case_path,fixture,environment,audit,',"case_path,fixture,environment,audit,ENGINE/'shaders/source_preintegrated_brdf.wgsl',ENGINE/'shaders/source_deferred_diffuse.wgsl',W/'source-brdf-lookup-case-v1/CASE.v1.json',W/'firepit-source-diffuse-context-reopen-v2/RESULT.v1.json',W/'firepit-source-exposure-reopen-v1/RESULT.v1.json',")
exec(compile(source,str(BASE),'exec'),globals())
