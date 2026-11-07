"""Apply the captured sky through the common material and measured Deferred kernel."""
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
ROOT = W / 'townsmith-paint-door-reflection-carrier-v1'
EXAMPLE = R / 'crates/forge_engine/examples/verify_source_glb_reflection_draw.rs'
QUEUE = S / 'queue_source_glb_reflection_draw_v1.py'
assert not ROOT.exists() and not EXAMPLE.exists() and not QUEUE.exists()


def read(path):
    return json.loads(path.read_text(encoding='utf-8-sig'))


def sha(path):
    with Path(path).open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


source_root = W / 'townsmith-door-source-camera-brdf-v10'
source_run = read(source_root / 'RUN-RESULT.v1.json')
assert source_run['actual_exit_code'] == 0 and source_run['source_inputs_unchanged'] and source_run['neutral_reference_byte_equal']
context = read(source_root / 'door-linear.context.json')
assert context['actual_lit_shader_mode'] == 'Deferred'
assert sha(source_root / 'door-linear.fgd.rgba32f') == 'd962fd1333474b433e12b951ee7f492d7f21307dc28f33d1b9ff6d4a51cd769c'
sky_proof_path = W / 'townsmith-door-source-sky-capture-reopen-v8/RESULT.v1.json'
sky_proof = read(sky_proof_path)
sky = Path(sky_proof['storage'])
assert sha(sky) == sky_proof['storage_sha256']
repeat_proof = W / 'townsmith-door-source-sky-repeat-reopen-v9/RESULT.v1.json'
assert read(repeat_proof)['same_case_independent_reproduction_verified']
original = W / 'townsmith-paint-door-ambient-carrier-v1/SM_Door-Single-B.measured-ambient.candidate.glb'
original_proof = read(original.parent / 'RESULT.v1.json')
assert sha(original) == original_proof['input_pins'][str(original)]
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
marker = '    // Total light'
assert text.count(marker) == 1
insertion = '''    // Actual source camera is Deferred. Project only the lighting inputs through
    // the measured original G-buffer kernel; authored graph outputs stay intact.
    // Screen-space occlusion is still unqualified; this control uses visibility 1.
    let source_reflection_normal=(source_graph.source_world_from_target_world*vec4<f32>(surface.normal_world,0.0)).xyz;
    let source_reflection_view=(source_graph.source_world_from_target_world*vec4<f32>(in.V,0.0)).xyz;
    indirect_light+=source_deferred_sky_specular(source_reflection_normal,source_reflection_view,
        perceptual_roughness,F0,surface.specular_occlusion,1.0,1.0,1.0);

'''
shader_dir = R / 'crates/forge_engine/src/shaders'
helpers = [shader_dir / name for name in ('source_deferred_diffuse.wgsl', 'source_sky_cube.wgsl', 'source_deferred_specular.wgsl')]
derived = (text.replace(marker, insertion + marker) + '\n' + '\n'.join(path.read_text(encoding='utf-8') for path in helpers)).encode('utf-8')
while len(binary) % 4:
    binary.append(0)
index = len(doc['bufferViews'])
doc['bufferViews'].append(dict(buffer=0, byteOffset=len(binary), byteLength=len(derived)))
binary.extend(derived)
contract['forward'] = dict(buffer_view=index, sha256=hashlib.sha256(derived).hexdigest())
while len(binary) % 4:
    binary.append(0)
doc['buffers'][0]['byteLength'] = len(binary)
doc.setdefault('extras', {})['measured_source_reflection_derivative'] = dict(source_camera_mode='Deferred', original_ambient_carrier_sha256=sha(original), source_ssao_qualified=False, source_render_parity_qualified=False, full_import=False)
encoded = json.dumps(doc, separators=(',', ':')).encode('utf-8')
encoded += b' ' * (-len(encoded) % 4)
output = struct.pack('<III', 0x46546c67, 2, 28 + len(encoded) + len(binary)) + struct.pack('<II', len(encoded), 0x4e4f534a) + encoded + struct.pack('<II', len(binary), 0x004e4942) + binary
ROOT.mkdir()
glb = ROOT / 'SM_Door-Single-B.measured-reflection.candidate.glb'
glb.write_bytes(output)
assert bytes(binary[:len(original_binary)]) == original_binary
(ROOT / 'source-reflection-forward.wgsl').write_bytes(derived)

example = (R / 'crates/forge_engine/examples/verify_source_glb_ambient_draw.rs').read_text(encoding='utf-8')
example = example.replace('SourceGraphShaderRegistry,', 'SourceGraphSkyCube, SourceGraphShaderRegistry,', 1)
anchor = '    material.extension.set_preintegrated_brdf(&binding);'
assert example.count(anchor) == 1
example = example.replace(anchor, anchor + '''
    let sky_bytes=std::fs::read(input["sky_path"].as_str().ok_or("Measured sky path absent")?)?;
    let sky_sha=forge_core::bytes_sha256(&sky_bytes);
    if serde_json::to_value(sky_sha)?!=input["sky_sha256"] {return Err("Measured sky SHA differs".into());}
    let sky_binding=SourceGraphSkyCube::from_mip_major_rgba16f_le(&sky_bytes,sky_sha,256,1,9)?.install_unqualified(&mut images);
    sky_binding.validate_residency(&images)?;
    material.extension.set_sky_cube(&sky_binding);
''')
example = example.replace('"measured_gpu_ambient_applied":true,', '"measured_sky_reflection_applied":true,"source_camera_lit_shader_mode":context["actual_lit_shader_mode"],"source_sky_sha256":input["sky_sha256"],"source_ssao_qualified":false,"measured_gpu_ambient_applied":true,')
EXAMPLE.write_text(example, encoding='utf-8')
queue = (S / 'queue_source_glb_ambient_draw_v1.py').read_text(encoding='utf-8')
queue = queue.replace('townsmith-paint-door-native-ambient-draw-v1', 'townsmith-paint-door-native-reflection-draw-v1')
queue = queue.replace('townsmith-door-source-render-context-v7', 'townsmith-door-source-camera-brdf-v10')
queue = queue.replace('verify_source_glb_ambient_draw', 'verify_source_glb_reflection_draw')
queue = queue.replace('townsmith-paint-door-ambient-carrier-v1/SM_Door-Single-B.measured-ambient.candidate.glb', 'townsmith-paint-door-reflection-carrier-v1/SM_Door-Single-B.measured-reflection.candidate.glb')
queue = queue.replace(sha(original), sha(glb))
queue = queue.replace("source_context_sha256=sha(SOURCE/'door-linear.context.json')", 'sky_path=' + repr(str(sky)) + ',sky_sha256=' + repr(sha(sky)) + ",source_context_sha256=sha(SOURCE/'door-linear.context.json')")
queue = queue.replace('    pins={str(p):sha(p) for p in paths}', "    paths += [Path(" + repr(str(sky)) + "),SOURCE/'door-linear.fgd.rgba32f',W/'townsmith-paint-door-reflection-carrier-v1/RESULT.v1.json',W/'townsmith-door-source-sky-repeat-reopen-v9/RESULT.v1.json']\n    pins={str(p):sha(p) for p in paths}")
compile(queue, str(QUEUE), 'exec')
QUEUE.write_text(queue, encoding='utf-8')
paths = [Path(__file__), original, original.parent / 'RESULT.v1.json', sky_proof_path, sky, repeat_proof, source_root / 'RUN-RESULT.v1.json', source_root / 'door-linear.context.json', source_root / 'door-linear.fgd.rgba32f', *helpers, glb, EXAMPLE, QUEUE]
pins = {str(path): sha(path) for path in paths}
(ROOT / 'RESULT.v1.json').write_text(json.dumps(dict(status='measured_deferred_reflection_carrier_prepared', created_utc=datetime.now(timezone.utc).isoformat(), input_pins=pins, original_binary_prefix_byte_equal=True, original_geometry_and_authored_textures_preserved=True, source_camera_mode_measured='Deferred', source_fgd_table_matches_shared_table=True, source_ssao_qualified=False, source_face_orientation_verified=False, source_render_parity_qualified=False, full_import=False), indent=2), encoding='utf-8')
subprocess.run([sys.executable, '-B', str(QUEUE)], check=True)
