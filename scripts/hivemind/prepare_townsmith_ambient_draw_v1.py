"""Add measured scene irradiance to an immutable source GLB derivative and draw it."""
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
ROOT = W / 'townsmith-paint-door-ambient-carrier-v1'
EXAMPLE = R / 'crates/forge_engine/examples/verify_source_glb_ambient_draw.rs'
QUEUE = S / 'queue_source_glb_ambient_draw_v1.py'
assert not ROOT.exists() and not EXAMPLE.exists() and not QUEUE.exists()


def read(path):
    return json.loads(path.read_text(encoding='utf-8-sig'))


def sha(path):
    with Path(path).open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


original = W / 'townsmith-paint-door-glb-v1/SM_Door-Single-B.source-material.candidate.glb'
assert sha(original) == 'c9e500dd9c07e2b655441d4e748c03caa0f18b2ee38e114db5049f4859f09c49'
proof_path = W / 'townsmith-door-source-gpu-ambient-reopen-v1/RESULT.v1.json'
proof = read(proof_path)
assert proof['actual_gpu_ambient_buffer_captured'] and proof['source_inputs_unchanged']
brdf = read(W / 'source-brdf-lookup-case-v1/CASE.v1.json')['inputs']
assert sha(Path(brdf['rgba32f'])) == brdf['sha256']
raw = original.read_bytes()
length = struct.unpack_from('<I', raw, 12)[0]
doc = json.loads(raw[20:20 + length])
original_binary = raw[28 + length:]
binary = bytearray(original_binary)
shader_dir = R / 'crates/forge_engine/src/shaders'
helpers = [shader_dir / name for name in ('source_diffuse_probe.wgsl', 'source_preintegrated_brdf.wgsl')]
helper_text = '\n'.join(path.read_text(encoding='utf-8') for path in helpers)
assert len(doc['materials']) == 1
contract = doc['materials'][0]['extensions']['OPENRPG_source_graph']
before = dict(contract['forward'])
view = doc['bufferViews'][before['buffer_view']]
source = original_binary[view.get('byteOffset', 0):view.get('byteOffset', 0) + view['byteLength']]
assert hashlib.sha256(source).hexdigest() == before['sha256']
text = source.decode('utf-8').replace('\r\n', '\n')
marker = '    // Total light'
assert text.count(marker) == 1
insertion = '''    // Measured scene irradiance; original graph and authored textures remain intact.
    // This controlled draw has no native ambient, lightmap, irradiance volume or sky.
    // HDRP BRDF lookup is explicit. Whole forward/deferred equivalence remains open.
    let source_scene_fgd=source_preintegrated_brdf_diffuse(source_preintegrated_brdf_sample(NdotV,perceptual_roughness));
    let source_scene_irradiance=max(source_preconvolved_diffuse_irradiance(surface.bent_normal_world),vec3<f32>(0.0));
    indirect_light+=source_scene_irradiance*source_scene_fgd*surface.diffuse_color*surface.diffuse_occlusion;

'''
derived = (text.replace(marker, insertion + marker) + '\n' + helper_text).encode('utf-8')
while len(binary) % 4:
    binary.append(0)
index = len(doc['bufferViews'])
doc['bufferViews'].append(dict(buffer=0, byteOffset=len(binary), byteLength=len(derived)))
binary.extend(derived)
contract['forward'] = dict(buffer_view=index, sha256=hashlib.sha256(derived).hexdigest())
while len(binary) % 4:
    binary.append(0)
doc['buffers'][0]['byteLength'] = len(binary)
doc.setdefault('extras', {})['measured_scene_ambient_derivative'] = dict(original_sha256=sha(original), full_import=False, original_geometry_and_texture_views_preserved=True)
encoded = json.dumps(doc, separators=(',', ':')).encode('utf-8')
encoded += b' ' * (-len(encoded) % 4)
output = struct.pack('<III', 0x46546c67, 2, 28 + len(encoded) + len(binary)) + struct.pack('<II', len(encoded), 0x4e4f534a) + encoded + struct.pack('<II', len(binary), 0x004e4942) + binary
ROOT.mkdir()
glb = ROOT / 'SM_Door-Single-B.measured-ambient.candidate.glb'
glb.write_bytes(output)
assert bytes(binary[:len(original_binary)]) == original_binary
(ROOT / 'source-ambient-forward.wgsl').write_bytes(derived)

example = (R / 'crates/forge_engine/examples/verify_source_glb_draw.rs').read_text(encoding='utf-8')
example = example.replace('SourceGraphShaderRegistry, SourceGraphUniforms,', 'SourceGraphShaderRegistry, SourceGraphUniforms, SourceGraphDiffuseProbeUniforms, SourceGraphPreintegratedBrdf,')
anchor = '    let material = material_result?;'
assert example.count(anchor) == 1
example = example.replace(anchor, '''    let mut material = material_result?;
    let values=context["actual_gpu_ambient_coefficients"].as_array().ok_or("Measured GPU probe absent")?;
    if values.len()!=28 {return Err("Measured GPU probe length differs".into());}
    let mut coefficients=[Vec4::ZERO;7];
    for i in 0..7 {for ch in 0..4 {coefficients[i][ch]=values[i*4+ch].as_f64().ok_or("Non-numeric probe")? as f32;}}
    material.extension.diffuse_probe=SourceGraphDiffuseProbeUniforms::from_preconvolved(coefficients,uniforms.source_world_from_target_world)?;
    let brdf_bytes=std::fs::read(input["brdf_path"].as_str().ok_or("Source BRDF path absent")?)?;
    let brdf_sha=forge_core::bytes_sha256(&brdf_bytes);
    if serde_json::to_value(brdf_sha)?!=input["brdf_sha256"] {return Err("Source BRDF SHA differs".into());}
    let binding=SourceGraphPreintegratedBrdf::from_rgba32f_le(&brdf_bytes,brdf_sha,64,64)?.install_unqualified(&mut images);
    material.extension.set_preintegrated_brdf(&binding);
    material.extension.validate_scene_dependencies(&images)?;
    let mut ambient_encoded=encase::UniformBuffer::new(Vec::<u8>::new());
    ambient_encoded.write(&material.extension.diffuse_probe)?;
    std::fs::write(root.join("AMBIENT-UNIFORMS.bin"),ambient_encoded.into_inner())?;
''')
# Source matrices are Copy; original uniforms were moved into registration above.
example = example.replace('coefficients,uniforms.source_world_from_target_world', 'coefficients,matrix("source_object_from_target_object")?')
example = example.replace('"gpu_draw_executed":true,"image":', '"measured_gpu_ambient_applied":true,"source_gpu_ambient_coefficients":context["actual_gpu_ambient_coefficients"],"source_brdf_sha256":input["brdf_sha256"],"gpu_draw_executed":true,"image":')
EXAMPLE.write_text(example, encoding='utf-8')

queue = (S / 'queue_source_glb_draw_v2.py').read_text(encoding='utf-8')
queue = queue.replace('townsmith-paint-door-native-draw-v2', 'townsmith-paint-door-native-ambient-draw-v1')
queue = queue.replace('townsmith-door-source-render-context-v2', 'townsmith-door-source-render-context-v7')
queue = queue.replace('verify_source_glb_draw', 'verify_source_glb_ambient_draw')
queue = queue.replace('townsmith-paint-door-glb-v1/SM_Door-Single-B.source-material.candidate.glb', 'townsmith-paint-door-ambient-carrier-v1/SM_Door-Single-B.measured-ambient.candidate.glb')
queue = queue.replace('c9e500dd9c07e2b655441d4e748c03caa0f18b2ee38e114db5049f4859f09c49', sha(glb))
queue = queue.replace('source_context_sha256=sha(SOURCE/\'door-linear.context.json\')', 'brdf_path=' + repr(brdf['rgba32f']) + ',brdf_sha256=' + repr(brdf['sha256']) + ',source_context_sha256=sha(SOURCE/\'door-linear.context.json\')')
queue = queue.replace('    pins={str(p):sha(p) for p in paths}', '    paths += [Path(' + repr(brdf['rgba32f']) + '),W/\'townsmith-door-source-gpu-ambient-reopen-v1/RESULT.v1.json\',W/\'townsmith-paint-door-ambient-carrier-v1/RESULT.v1.json\']\n    pins={str(p):sha(p) for p in paths}')
compile(queue, str(QUEUE), 'exec')
QUEUE.write_text(queue, encoding='utf-8')
pins = {str(path): sha(path) for path in (Path(__file__), original, proof_path, *helpers, Path(brdf['rgba32f']), glb, EXAMPLE, QUEUE)}
(ROOT / 'RESULT.v1.json').write_text(json.dumps(dict(status='measured_ambient_carrier_and_draw_prepared', created_utc=datetime.now(timezone.utc).isoformat(), input_pins=pins, original_binary_prefix_byte_equal=True, original_meshes_and_textures_preserved=True, source_diffuse_brdf_variant_qualified=False, source_render_parity_qualified=False, full_import=False), indent=2), encoding='utf-8')
subprocess.run([sys.executable, '-B', str(QUEUE)], check=True)
