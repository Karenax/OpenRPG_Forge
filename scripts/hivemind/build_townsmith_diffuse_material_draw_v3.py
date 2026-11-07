"""Build a private directional source-BRDF carrier; leave original graph intact."""
from pathlib import Path
from datetime import datetime, timezone
import hashlib
import json
import struct
import subprocess
import sys

S = Path(__file__).parent
R = S.parents[1]
W = Path('F:/OpenRPG_ImportWork')
ROOT = W / 'townsmith-paint-door-diffuse-material-carrier-v3'
EXAMPLE = R / 'crates/forge_engine/examples/verify_source_glb_diffuse_material_draw.rs'
QUEUE = S / 'queue_source_glb_diffuse_material_draw_v3.py'
assert not ROOT.exists() and not EXAMPLE.exists() and not QUEUE.exists()


def read(path):
    return json.loads(path.read_text(encoding='utf-8-sig'))


def sha(path):
    with Path(path).open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


original = W / 'townsmith-paint-door-reflection-carrier-v1/SM_Door-Single-B.measured-reflection.candidate.glb'
proof = read(original.parent / 'RESULT.v1.json')
assert sha(original) == proof['input_pins'][str(original)]
raw = original.read_bytes()
length = struct.unpack_from('<I', raw, 12)[0]
doc = json.loads(raw[20:20 + length])
original_binary = raw[28 + length:]
binary = bytearray(original_binary)
contract = doc['materials'][0]['extensions']['OPENRPG_source_graph']
reference = contract['forward']
view = doc['bufferViews'][reference['buffer_view']]
stage = original_binary[view.get('byteOffset', 0):view.get('byteOffset', 0) + view['byteLength']]
assert hashlib.sha256(stage).hexdigest() == reference['sha256']
text = stage.decode('utf-8')
anchor = 'var light_contrib = lighting::directional_light(i, &lighting_input, enable_diffuse);'
assert text.count(anchor) == 1
helper = '''
// Private isotropic Deferred direct-light candidate derived from pinned HDRP
// Lit.hlsl EvaluateBSDF/PostEvaluateBSDF and Core BSDF.hlsl. Source hashes and
// package licences remain in the carrier receipt. Not a general runtime verdict.
// Analytic sRGB8 RT projection candidate; source observed GBuffer0 is sRGB8.
// Actual hardware encode/decode precision remains a separate qualification.
fn source_candidate_diffuse_gbuffer_projection(authored:vec3<f32>)->vec3<f32> {
 let x=max(authored,vec3<f32>(0.0));
 let encoded=select(1.055*pow(x,vec3<f32>(1.0/2.4))-vec3<f32>(0.055),12.92*x,x<=vec3<f32>(0.0031308));
 let c=vec3<f32>(source_deferred_unorm8(encoded.x),source_deferred_unorm8(encoded.y),source_deferred_unorm8(encoded.z));
 return select(pow((c+vec3<f32>(0.055))/1.055,vec3<f32>(2.4)),c/12.92,c<=vec3<f32>(0.04045));
}
fn source_candidate_directional_brdf(light_id:u32,surface:SourceGraphNativeLitSurface,v:vec3<f32>,enable_diffuse:bool)->vec3<f32> {
 let source_n=(source_graph.source_world_from_target_world*vec4<f32>(surface.normal_world,0.0)).xyz;
 let nr=source_deferred_normal_roughness(source_n,surface.perceptual_roughness);
 // This isolated fixture has a verified orthogonal world reflection R.
 let source_basis=mat3x3<f32>(source_graph.source_world_from_target_world[0].xyz,source_graph.source_world_from_target_world[1].xyz,source_graph.source_world_from_target_world[2].xyz);
 let n=normalize(transpose(source_basis)*nr.xyz);
 let light=view_bindings::lights.directional_lights[light_id];
 let l=light.direction_to_light.xyz;
 let ndv=dot(n,v);let nv=max(ndv,0.0001);let ndl=dot(n,l);let nl=clamp(ndl,0.0,1.0);
 let lv=dot(l,v);let inv_len=inverseSqrt(max(2.0*lv+2.0,0.00000011920928955078125));
 let nh=clamp((ndl+ndv)*inv_len,0.0,1.0);let lh=clamp(inv_len*lv+inv_len,0.0,1.0);
 let r=nr.w;let rough=max(r*r,1.0/1024.0);let a2=rough*rough;
 let part_v=sqrt((-nv*a2+nv)*nv+a2);
 let abs_nl=abs(ndl);let lambda_v=abs_nl*part_v;
 let lambda_l=nv*sqrt((-abs_nl*a2+abs_nl)*abs_nl+a2);
 let s=(nh*a2-nh)*nh+1.0;
 let dv=(1.0/3.141592653589793)*0.5*a2/max(s*s*(lambda_v+lambda_l),1.1754943508222875e-38);
 let f0=source_deferred_fresnel0(mix(surface.dielectric_f0,surface.metallic_f0,surface.metallic));
 let fresnel=f0+(vec3<f32>(1.0)-f0)*pow(abs(1.0-lh),5.0);
 let fgd=source_deferred_preintegrated_specular_sample(nv,r);
 let energy=vec3<f32>(1.0)+f0*(1.0/fgd.y-1.0);
 let specular=fresnel*dv*nl*energy;
 let fd90=0.5+(r+r*lv);
 let light_scatter=1.0+(fd90-1.0)*pow(abs(1.0-abs_nl),5.0);
 let view_scatter=1.0+(fd90-1.0)*pow(abs(1.0-nv),5.0);
 let diffuse=select(vec3<f32>(0.0),source_candidate_diffuse_gbuffer_projection(surface.diffuse_color)*(1.0/3.141592653589793)/1.03571*light_scatter*view_scatter*nl,enable_diffuse);
 return (diffuse+specular)*light.color.rgb;
}
'''
# Use the admitted orthogonal fixture basis; no new uniform field is invented.
assert 'source_world_from_target_world:mat4x4<f32>' in text
assert text.count('out.color=source_graph_native_light_transport(p,surface);') == 1
text=text.replace('out.color=source_graph_native_light_transport(p,surface);','out.color=vec4<f32>(source_candidate_diffuse_gbuffer_projection(surface.diffuse_color),surface.alpha);')
derived = (text.replace(anchor, 'var light_contrib = source_candidate_directional_brdf(i,surface,in.V,enable_diffuse);') + helper).encode('utf-8')
while len(binary) % 4:
    binary.append(0)
index = len(doc['bufferViews'])
doc['bufferViews'].append(dict(buffer=0, byteOffset=len(binary), byteLength=len(derived)))
binary.extend(derived)
contract['forward'] = dict(buffer_view=index, sha256=hashlib.sha256(derived).hexdigest())
while len(binary) % 4:
    binary.append(0)
doc['buffers'][0]['byteLength'] = len(binary)
doc.setdefault('extras', {})['source_directional_brdf_candidate'] = dict(full_import=False, source_render_parity_qualified=False,
    gbuffer_diffuse_color_quantization_qualified=False, source_sun_shape_qualified=False, source_ssao_qualified=False)
encoded = json.dumps(doc, separators=(',', ':')).encode('utf-8')
encoded += b' ' * (-len(encoded) % 4)
output = struct.pack('<III', 0x46546c67, 2, 28 + len(encoded) + len(binary)) + struct.pack('<II', len(encoded), 0x4e4f534a) + encoded + struct.pack('<II', len(binary), 0x004e4942) + binary
ROOT.mkdir()
glb = ROOT / 'SM_Door-Single-B.diffuse-material-control.candidate.glb'
glb.write_bytes(output)
assert bytes(binary[:len(original_binary)]) == original_binary
(ROOT / 'source-directional-forward.wgsl').write_bytes(derived)
example = (R / 'crates/forge_engine/examples/verify_source_glb_reflection_draw.rs').read_text(encoding='utf-8')
assert example.count('Color::linear_rgb(0.025, 0.025, 0.025)') == 1
example=example.replace('Color::linear_rgb(0.025, 0.025, 0.025)','Color::linear_rgba(0.0, 0.0, 0.0, 0.0)')
EXAMPLE.write_text(example, encoding='utf-8')
queue = (S / 'queue_source_glb_reflection_draw_v1.py').read_text(encoding='utf-8')
queue = queue.replace('townsmith-paint-door-native-reflection-draw-v1', 'townsmith-paint-door-native-diffuse-material-draw-v3')
queue = queue.replace('verify_source_glb_reflection_draw', 'verify_source_glb_diffuse_material_draw')
queue = queue.replace(str(original).replace('\\', '/').removeprefix('F:/OpenRPG_ImportWork/'), str(glb).replace('\\', '/').removeprefix('F:/OpenRPG_ImportWork/'))
queue = queue.replace(sha(original), sha(glb))
queue = queue.replace("W/'townsmith-paint-door-reflection-carrier-v1/RESULT.v1.json'", "W/'townsmith-paint-door-diffuse-material-carrier-v3/RESULT.v1.json'")
compile(queue, str(QUEUE), 'exec')
QUEUE.write_text(queue, encoding='utf-8')
packages = W / 'unity-dungeon-original-shadergraph-hdrp-startup-v4/project/Library/PackageCache'
paths = [W/'townsmith-door-source-gbuffer-arrays-reopen-v21/RESULT.v1.json', Path(__file__), original, original.parent / 'RESULT.v1.json', EXAMPLE, QUEUE, glb,
         packages / 'com.unity.render-pipelines.core@a4c9a9793789/ShaderLibrary/BSDF.hlsl',
         packages / 'com.unity.render-pipelines.core@a4c9a9793789/ShaderLibrary/CommonLighting.hlsl',
         packages / 'com.unity.render-pipelines.high-definition@fc55a8abadc2/Runtime/Material/Lit/Lit.hlsl']
pins = {str(path): sha(path) for path in paths}
(ROOT / 'RESULT.v1.json').write_text(json.dumps(dict(status='source_diffuse_material_only_private_control_prepared',
    created_utc=datetime.now(timezone.utc).isoformat(), input_pins=pins, original_binary_prefix_byte_equal=True,
    original_geometry_and_authored_textures_preserved=True, full_import=False, source_render_parity_qualified=False), indent=2), encoding='utf-8')
subprocess.run([sys.executable, '-B', str(QUEUE)], check=True)
