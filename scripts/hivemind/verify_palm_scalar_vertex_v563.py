"""Use native scalar source dimensions in an isolated full vertex comparison."""
from pathlib import Path
parent=Path(__file__).with_name('run_palm_wind_gpu_comparison_v545.py')
code=parent.read_text('utf-8').replace('v545','v563')
marker="rust=r'''"
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
let v=cases[id.x];let coordinate=fma(v.world_position.x,3.0,v.time.x);
let centered=source_gradient_noise_centered_v0(vec2<f32>(coordinate),0.2);
let strength=centered*0.2;let displacement=strength*v.uv.y;
let position=vec3<f32>(fma(strength,v.uv.y,v.object_position.x),v.object_position.yz);
results[id.x*3u]=vec4<f32>(position,displacement);
results[id.x*3u+1u]=vec4<f32>(v.normal.xyz,0.0);
results[id.x*3u+2u]=vec4<f32>(v.tangent.xyz,0.0);
}
"""
'''+marker
assert code.count(marker)==1;code=code.replace(marker,addition)
code=code.replace('for p in [Path(__file__),proofpath','for p in [parent,Path(__file__),proofpath')
exec(compile(code,str(parent)+'#scalar-vertex-source-equations','exec'),globals())
