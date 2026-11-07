"""Compare all four gradients and the centered interpolant with native Unity."""
from pathlib import Path
parent=Path(__file__).with_name('run_palm_wind_gpu_comparison_v545.py')
code=parent.read_text('utf-8').replace('v545','v559')
needle="rust=r'''"
addition=r'''
correctedpath=W/'pirate-palm-wind-gpu-comparison-v556/src/wind.wgsl'
receipt=read(W/'pirate-palm-wind-gpu-comparison-v556/RESULT.v1.json')
assert sha(correctedpath)==receipt['pins'][str(correctedpath)]
helpers=correctedpath.read_text('utf-8-sig').split('struct SourceMaterialInputs',1)[0]
pins[str(correctedpath)]=sha(correctedpath)
wgsl=helpers+"""
struct InputCase { object_position:vec4<f32>,world_position:vec4<f32>,normal:vec4<f32>,tangent:vec4<f32>,uv:vec4<f32>,time:vec4<f32>, }
@group(0) @binding(0) var<storage,read> cases:array<InputCase>;
@group(0) @binding(1) var<storage,read_write> results:array<vec4<f32>>;
@compute @workgroup_size(64) fn wind(@builtin(global_invocation_id) id:vec3<u32>){
if id.x>=arrayLength(&cases){return;}
let v=cases[id.x];let u=v.world_position.x*3.0+v.time.x;
let ip=floor(vec2<f32>(u)*0.2);
let g00=source_gradient_noise_dir_v0(ip);
let g01=source_gradient_noise_dir_v0(ip+vec2<f32>(0.0,1.0));
let g10=source_gradient_noise_dir_v0(ip+vec2<f32>(1.0,0.0));
let g11=source_gradient_noise_dir_v0(ip+vec2<f32>(1.0,1.0));
let centered=source_gradient_noise_centered_v0(vec2<f32>(u),0.2);
let strength=centered*0.2;let displacement=strength*v.uv.y;
results[id.x*3u]=vec4<f32>(g00,g01);
results[id.x*3u+1u]=vec4<f32>(g10,g11);
results[id.x*3u+2u]=vec4<f32>(centered,displacement,fma(strength,v.uv.y,v.object_position.x),displacement);
}
"""
'''+needle
assert code.count(needle)==1;code=code.replace(needle,addition)
code=code.replace('referencepath=W/\'pirate-palm-wind-native-reference-v541/native-output.f32\'', 'referencepath=W/\'pirate-palm-wind-native-reference-v558/native-output.f32\'')
old="labels=['position_x','position_y','position_z','wind_delta','normal_x','normal_y','normal_z','normal_w','tangent_x','tangent_y','tangent_z','tangent_w']"
new="labels=['g00x','g00y','g01x','g01y','g10x','g10y','g11x','g11y','centered_noise','wind','position_x','optimized_wind_delta']"
assert code.count(old)==1;code=code.replace(old,new)
code=code.replace('limit=1e-5 if j%12<4 else 0','limit=1e-5').replace('normal_tangent_exact=True','normal_tangent_exact=False')
marker='expected=referencepath.read_bytes()'
addition="""nativepath=W/'pirate-palm-wind-native-reference-v558/RUN-RESULT.v1.json'
native=read(nativepath)
assert native['unity_exit_code']==0 and native['source_changes']==native['private_importer_changes']==[]
assert sha(inputpath)==sha(W/'pirate-palm-wind-native-reference-v558/INPUT.f32')
pins[str(nativepath)]=sha(nativepath);pins[str(referencepath)]=sha(referencepath)
expected=referencepath.read_bytes()"""
assert code.count(marker)==1;code=code.replace(marker,addition)
code=code.replace('for p in [Path(__file__),proofpath','for p in [parent,Path(__file__),proofpath')
exec(compile(code,str(parent)+'#four-gradient-comparison','exec'),globals())
