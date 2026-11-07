"""Compose deferred GI inputs from the pinned SDK; never consume source pixels."""
from pathlib import Path
from datetime import datetime, timezone
import copy, hashlib, json, struct

R = Path('E:/OpenRPG_Forge/OpenRPG_Forge-Rust')
W = Path('F:/OpenRPG_ImportWork')
O = W / 'townsmith-deferred-indirect-carrier-v94'
assert not O.exists()
def sha(p): return hashlib.sha256(p.read_bytes()).hexdigest()
parent = W / 'townsmith-common-native-srgb-carrier-v89/SM_Door-Single-B.common-native-srgb.candidate.glb'
proof = W / 'native-srgb-common-runtime-glb-reopen-v91/RESULT.v1.json'
assert sha(parent) == '38b11ac971d46552b9cd00141246c4c644191f0d5239a42203f7d099373df865'
ao_proof = W / 'townsmith-gbuffer3-ao-tag-control-v76/RESULT.v1.json'
ar = json.loads(ao_proof.read_text())
assert ar['ao_tag_pixels'] == 34445
assert next(row for row in ar['rows'] if row['target'] == 'source_graph_floor_uf11')['equal_pixels'] == 34445
sdk = W / 'unity-dungeon-original-shadergraph-hdrp-startup-v4/project/Library/PackageCache/com.unity.render-pipelines.high-definition@fc55a8abadc2/Runtime'
lit = sdk / 'Material/Lit/Lit.hlsl'
loop = sdk / 'Lighting/LightLoop/LightLoop.hlsl'
assert 'bsdfData.ambientOcclusion = gbuffer3.y;' in lit.read_text()
assert 'EvaluateAmbientProbe(bsdfData.normalWS)' in loop.read_text()
assert 'preLightData.diffuseFGD * GetDiffuseOrDefaultColor(bsdfData, _ReplaceDiffuseForIndirect).rgb' in lit.read_text()
raw = parent.read_bytes(); size = struct.unpack_from('<I', raw, 12)[0]
doc = json.loads(raw[20:20+size]); before_doc = copy.deepcopy(doc)
original = raw[28+size:]; data = bytearray(original)
contract = doc['materials'][0]['extensions']['OPENRPG_source_graph']
view = doc['bufferViews'][contract['forward']['buffer_view']]
shader = original[view.get('byteOffset', 0):view.get('byteOffset', 0)+view['byteLength']]
assert hashlib.sha256(shader).hexdigest() == contract['forward']['sha256']
text = shader.decode()
anchor = '''    let source_scene_fgd=source_preintegrated_brdf_diffuse(source_preintegrated_brdf_sample(NdotV,perceptual_roughness));
    let source_scene_irradiance=max(source_preconvolved_diffuse_irradiance(surface.bent_normal_world),vec3<f32>(0.0));
    let source_measured_ambient=source_scene_irradiance*source_scene_fgd*surface.diffuse_color*surface.diffuse_occlusion;'''
assert text.count(anchor) == 1
replacement = '''    // Private AO-tag / deferred ambient-probe branch control. All inputs
    // are computed from this GLB and admitted scene resources, not images.
    let source_indirect_basis=mat3x3<f32>(source_graph.source_world_from_target_world[0].xyz,source_graph.source_world_from_target_world[1].xyz,source_graph.source_world_from_target_world[2].xyz);
    let source_indirect_normal=(source_graph.source_world_from_target_world*vec4<f32>(surface.normal_world,0.0)).xyz;
    let source_indirect_nr=source_deferred_normal_roughness(source_indirect_normal,perceptual_roughness);
    let source_indirect_view=normalize((source_graph.source_world_from_target_world*vec4<f32>(in.V,0.0)).xyz);
    let source_indirect_nv=max(dot(source_indirect_nr.xyz,source_indirect_view),0.0001);
    let source_scene_fgd=source_preintegrated_brdf_diffuse(source_deferred_preintegrated_specular_sample(source_indirect_nv,source_indirect_nr.w));
    let source_scene_irradiance=max(source_preconvolved_diffuse_irradiance(normalize(transpose(source_indirect_basis)*source_indirect_nr.xyz)),vec3<f32>(0.0));
    let source_measured_ambient=source_scene_irradiance*source_scene_fgd*source_candidate_diffuse_gbuffer_projection(surface.diffuse_color)*source_private_indirect_ao_tag_store(surface.diffuse_occlusion);'''
text = text.replace(anchor, replacement)
text += '''
// Source GBuffer3 AO tag green: measured unsigned float with six mantissa bits.
// Only indirect GI is affected; authored AO and direct microshadow stay intact.
fn source_private_indirect_ao_tag_store(value:f32)->f32 {
 let x=clamp(value,0.0,1.0);
 if(x<0.00006103515625){return floor(x/0.00000095367431640625)*0.00000095367431640625;}
 return bitcast<f32>(bitcast<u32>(x)&0xfffe0000u);
}
'''
new_shader = text.encode(); data.extend(bytes(-len(data) % 4))
index = len(doc['bufferViews'])
doc['bufferViews'].append(dict(buffer=0, byteOffset=len(data), byteLength=len(new_shader)))
data.extend(new_shader); data.extend(bytes(-len(data) % 4))
contract['forward'] = dict(buffer_view=index, sha256=hashlib.sha256(new_shader).hexdigest())
doc['buffers'][0]['byteLength'] = len(data)
encoded = json.dumps(doc, separators=(',', ':')).encode(); encoded += b' ' * (-len(encoded) % 4)
out = struct.pack('<III', 0x46546c67, 2, 28+len(encoded)+len(data)) + struct.pack('<II', len(encoded), 0x4e4f534a) + encoded + struct.pack('<II', len(data), 0x004e4942) + data
O.mkdir(); glb = O / 'SM_Door-Single-B.deferred-indirect.candidate.glb'; glb.write_bytes(out)
wgsl = O / 'forward.wgsl'; wgsl.write_bytes(new_shader)
reopen = glb.read_bytes(); js = struct.unpack_from('<I', reopen, 12)[0]
reopened = json.loads(reopen[20:20+js]); assert reopen[28+js:][:len(original)] == original
comparison = copy.deepcopy(reopened)
comparison['bufferViews'] = comparison['bufferViews'][:-1]
comparison['buffers'] = before_doc['buffers']
comparison['materials'][0]['extensions']['OPENRPG_source_graph']['forward'] = before_doc['materials'][0]['extensions']['OPENRPG_source_graph']['forward']
assert comparison == before_doc
queue_base = R / 'scripts/hivemind/queue_source_glb_common_native_srgb_v91.py'
queue = R / 'scripts/hivemind/queue_source_glb_deferred_indirect_v94.py'; assert not queue.exists()
q = queue_base.read_text().replace("ROOT=W/f'townsmith-common-native-srgb-layer-{LAYER}-v91'", "ROOT=W/f'townsmith-deferred-indirect-layer-{LAYER}-v94'")
q = q.replace(str(parent.relative_to(W)).replace('\\', '/'), str(glb.relative_to(W)).replace('\\', '/'))
q = q.replace('38b11ac971d46552b9cd00141246c4c644191f0d5239a42203f7d099373df865', sha(glb))
q = q.replace("W/'townsmith-common-native-srgb-carrier-v89/RESULT.v1.json'", "W/'townsmith-deferred-indirect-carrier-v94/RESULT.v1.json'")
queue.write_bytes(q.encode())
result = dict(status='source_deferred_indirect_candidate_reopened_gpu_pending', finished_utc=datetime.now(timezone.utc).isoformat(),
    parent_sha256=sha(parent), candidate_sha256=sha(glb), original_binary_prefix_byte_equal=True,
    all_document_fields_except_forward_and_appended_shader_storage_unchanged=True,
    observed_source_branch='AO_IN_GBUFFER3_TAG and deferred ambient probe fallback',
    physical_basis_assumption='verified orthogonal world reflection in this private fixture',
    candidate_scope='decoded normal/roughness, diffuse sRGB, AO tag and GPU FGD interpolation for indirect GI only',
    source_branch_activation_not_independently_captured=True,
    input_pins={str(p):sha(p) for p in [Path(__file__), parent, proof, ao_proof, lit, loop, glb, wgsl, queue]},
    installed_in_product=False, source_pixels_applied_to_native=False, pixel_fit_or_threshold_relaxation=False,
    full_material_fidelity_qualified=False, full_import=False, complete_assets_delivered_increment=0)
(O/'RESULT.v1.json').write_text(json.dumps(result, indent=2)+'\n')
print(json.dumps({'status':result['status'],'candidate':str(glb),'sha256':sha(glb)}))
