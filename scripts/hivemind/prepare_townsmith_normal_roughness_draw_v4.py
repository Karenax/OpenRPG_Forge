"""Draw the original graph's source-world normal and roughness packing."""
from pathlib import Path
import subprocess
import sys

S = Path(__file__).parent
target = S / 'build_townsmith_normal_roughness_draw_v4.py'
assert not target.exists()
text = (S / 'build_townsmith_diffuse_material_draw_v3.py').read_text(encoding='utf-8')
text = text.replace('townsmith-paint-door-diffuse-material-carrier-v3', 'townsmith-paint-door-normal-roughness-carrier-v4')
text = text.replace('verify_source_glb_diffuse_material_draw', 'verify_source_glb_normal_roughness_draw')
text = text.replace('queue_source_glb_diffuse_material_draw_v3', 'queue_source_glb_normal_roughness_draw_v4')
text = text.replace('townsmith-paint-door-native-diffuse-material-draw-v3', 'townsmith-paint-door-native-normal-roughness-draw-v4')
text = text.replace('SM_Door-Single-B.diffuse-material-control.candidate.glb', 'SM_Door-Single-B.normal-roughness-control.candidate.glb')
anchor = 'out.color=vec4<f32>(source_candidate_diffuse_gbuffer_projection(surface.diffuse_color),surface.alpha);'
assert text.count(anchor) == 1
text = text.replace(anchor, 'out.color=source_candidate_normal_roughness_buffer(surface);')
helper = '''
// Source NormalBuffer/Packing.hlsl encoding, independent of lighting.
fn source_candidate_normal_roughness_buffer(surface:SourceGraphNativeLitSurface)->vec4<f32> {
 var n=normalize((source_graph.source_world_from_target_world*vec4<f32>(surface.normal_world,0.0)).xyz);
 let negative_z=(bitcast<u32>(n.z)&0x80000000u)!=0u;
 n.z=select(max(1.0/1024.0,abs(n.z)),-max(1.0/1024.0,abs(n.z)),negative_z);
 n=n/dot(abs(n),vec3<f32>(1.0));
 let t=clamp(-n.z,0.0,1.0);
 let oct=n.xy+select(vec2<f32>(-t),vec2<f32>(t),n.xy>=vec2<f32>(0.0));
 let packed=vec2<u32>(clamp(oct*0.5+vec2<f32>(0.5),vec2<f32>(0.0),vec2<f32>(1.0))*4095.5);
 let low=packed&vec2<u32>(255u);let high=packed>>vec2<u32>(8u);
 return vec4<f32>(vec3<f32>(f32(low.x),f32(low.y),f32(high.x|(high.y<<4u)))/255.0,source_deferred_unorm8(surface.perceptual_roughness));
}
'''
anchor = 'helper = '
assert text.count(anchor) == 1
text = text.replace('while len(binary) % 4:', 'while len(binary) % 4:', 1)
anchor = 'derived = (text.replace(anchor,'
assert text.count(anchor) == 1
text = text.replace(anchor, 'helper+=' + repr(helper) + '\n' + anchor)
text = text.replace("status='source_diffuse_material_only_private_control_prepared'", "status='source_normal_roughness_only_private_control_prepared'")
compile(text, str(target), 'exec')
target.write_text(text, encoding='utf-8')
subprocess.run([sys.executable, '-B', str(target)], check=True)
