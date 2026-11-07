"""Compare matched noise intermediate equations, retaining the failed wind proof."""
from pathlib import Path
parent=Path(__file__).with_name('run_palm_wind_gpu_comparison_v545.py')
code=parent.read_text('utf-8').replace('v545','v548')
needle="rust=r'''"
diagnostic=r'''
wgsl=helpers+"""
struct InputCase { object_position:vec4<f32>,world_position:vec4<f32>,normal:vec4<f32>,tangent:vec4<f32>,uv:vec4<f32>,time:vec4<f32>, }
@group(0) @binding(0) var<storage,read> cases:array<InputCase>;
@group(0) @binding(1) var<storage,read_write> results:array<vec4<f32>>;
@compute @workgroup_size(64) fn wind(@builtin(global_invocation_id) id:vec3<u32>){
if id.x>=arrayLength(&cases){return;}
let v=cases[id.x];let u=v.world_position.x*3.0+v.time.x;
let p=vec2<f32>(u)*0.2;let ip=floor(p);let fp=fract(p);
let reduced=source_hlsl_remainder2(ip,289.0);
let h0=source_hlsl_remainder((34.0*reduced.x+1.0)*reduced.x,289.0)+reduced.y;
let h1=source_hlsl_remainder((34.0*h0+1.0)*h0,289.0);
let dir=source_gradient_noise_dir_v0(ip);
let noise=source_gradient_noise_v0(vec2<f32>(u),0.2);
results[id.x*3u]=vec4<f32>(p,ip);
results[id.x*3u+1u]=vec4<f32>(fp,h0,h1);
results[id.x*3u+2u]=vec4<f32>(dir,noise,(noise-0.5)*0.2*v.uv.y);
}
"""
'''+needle
assert code.count(needle)==1;code=code.replace(needle,diagnostic)
code=code.replace("referencepath=W/'pirate-palm-wind-native-reference-v541/native-output.f32'", "referencepath=W/'pirate-palm-wind-native-reference-v547/native-output.f32'")
code=code.replace("normal_tangent_exact=True", "normal_tangent_exact=False")
code=code.replace("limit=1e-5 if j%12<4 else 0", "limit=1e-5")
old="labels=['position_x','position_y','position_z','wind_delta','normal_x','normal_y','normal_z','normal_w','tangent_x','tangent_y','tangent_z','tangent_w']"
new="labels=['scaled_uv_x','scaled_uv_y','floor_x','floor_y','fract_x','fract_y','hash_stage0','hash_stage1','gradient00_x','gradient00_y','noise','wind_before_position_add']"
assert code.count(old)==1;code=code.replace(old,new)
code=code.replace("for p in [Path(__file__),proofpath", "for p in [parent,Path(__file__),proofpath")
old="for p,h in pins.items():assert sha(p)==h,p\nexpected=referencepath.read_bytes()"
new="""for p,h in pins.items():assert sha(p)==h,p
nativepath=W/'pirate-palm-wind-native-reference-v547/RUN-RESULT.v1.json'
native=read(nativepath)
assert native.get('unity_exit_code')==0 and native['source_changes']==native['private_importer_changes']==[]
assert sha(W/'pirate-palm-wind-native-reference-v547/INPUT.f32')==sha(inputpath)
pins[str(nativepath)]=sha(nativepath);pins[str(referencepath)]=sha(referencepath)
expected=referencepath.read_bytes()"""
assert code.count(old)==1;code=code.replace(old,new)
exec(compile(code,str(parent)+'#matched-noise-intermediates','exec'),globals())
