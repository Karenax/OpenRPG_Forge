"""Probe source scalar dimensions instead of replicated vec4 intermediates."""
from pathlib import Path
parent=Path(__file__).with_name('verify_palm_centered_noise_v556.py')
source=parent.read_text('utf-8').replace('v556','v562')
needle="exec(compile(source.replace(needle,injection),str(parent)+'#fxc-cancelled-noise-offset','exec'),globals())"
extra=r'''
scalar_probe="""wgsl=helpers+'''
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
'''
"""
injection=injection.replace(needle, "code=code.replace(\"rust=r'''\", "+repr(scalar_probe)+"+\"rust=r'''\")\n"+needle)
'''+needle
assert source.count(needle)==1
exec(compile(source.replace(needle,extra),str(parent)+'#source-scalar-dimensions-probe','exec'),globals())
