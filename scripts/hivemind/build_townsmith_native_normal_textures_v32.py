"""Private six-channel bit-preserving UV diagnostic using the common GLB path."""
from pathlib import Path
from datetime import datetime, timezone
import hashlib, json, struct, subprocess, sys

s = Path(__file__).parent
r = s.parents[1]
w = Path('F:/OpenRPG_ImportWork')
root = w/'townsmith-native-normal-textures-carrier-v32'
example = r/'crates/forge_engine/examples/verify_source_glb_normal_textures_v32.rs'
queue = s/'queue_source_glb_normal_textures_v32.py'
assert not root.exists() and not example.exists() and not queue.exists()
def sha(p):
    with p.open('rb') as stream: return hashlib.file_digest(stream,'sha256').hexdigest()
original = w/'townsmith-paint-door-normal-roughness-carrier-v4/SM_Door-Single-B.normal-roughness-control.candidate.glb'
proof = json.loads((original.parent/'RESULT.v1.json').read_text())
assert sha(original) == proof['input_pins'][str(original)]
raw = original.read_bytes()
length = struct.unpack_from('<I',raw,12)[0]
doc = json.loads(raw[20:20+length])
binary = bytearray(raw[28+length:])
prefix = bytes(binary)
contract = doc['materials'][0]['extensions']['OPENRPG_source_graph']
view = doc['bufferViews'][contract['forward']['buffer_view']]
stage = prefix[view.get('byteOffset',0):view.get('byteOffset',0)+view['byteLength']]
assert hashlib.sha256(stage).hexdigest() == contract['forward']['sha256']
text = stage.decode()
# This graph does not use time. Only the private diagnostic uses its .w lane.
assert text.count('source_time_parameters') == 1
anchor = 'out.color=source_candidate_normal_roughness_buffer(surface);'
assert text.count(anchor) == 1
text = text.replace(anchor,'out.color=source_private_normal_textures(in.source_uv0.xy);')
text += '\n// Diagnostic only: raw samples from the original normal textures.\nfn source_private_normal_textures(uv:vec2<f32>)->vec4<f32> {\n let a=textureSample(t6,s6,uv);let b=textureSample(t7,s7,uv*30.0);\n return select(a,b,source_graph.source_time_parameters.w>0.5);\n}\n'
derived = text.encode()
while len(binary)%4: binary.append(0)
index = len(doc['bufferViews'])
doc['bufferViews'].append(dict(buffer=0,byteOffset=len(binary),byteLength=len(derived)))
binary.extend(derived)
contract['forward']=dict(buffer_view=index,sha256=hashlib.sha256(derived).hexdigest())
while len(binary)%4: binary.append(0)
doc['buffers'][0]['byteLength']=len(binary)
doc.setdefault('extras',{})['private_uv_control']=dict(channels=['normal6_raw','normal7_raw_uv30'],full_import=False)
encoded=json.dumps(doc,separators=(',',':')).encode()
encoded+=b' '*(-len(encoded)%4)
output=struct.pack('<III',0x46546c67,2,28+len(encoded)+len(binary))+struct.pack('<II',len(encoded),0x4e4f534a)+encoded+struct.pack('<II',len(binary),0x004e4942)+binary
root.mkdir()
glb=root/'SM_Door-Single-B.uv-control.candidate.glb'
glb.write_bytes(output)
assert bytes(binary[:len(prefix)])==prefix
(root/'uv-control.forward.wgsl').write_bytes(derived)
rust=(r/'crates/forge_engine/examples/verify_source_glb_normal_roughness_draw.rs').read_text()
anchor='source_time_parameters: Vec4::ZERO,'
assert rust.count(anchor)==1
rust=rust.replace(anchor,'source_time_parameters: Vec4::new(0.0,0.0,0.0,input["diagnostic_channel"].as_u64().filter(|v| *v < 2).ok_or("Normal texture channel 0..1 required")? as f32),')
anchor='"gpu_draw_executed":true,'
assert rust.count(anchor)==1
rust=rust.replace(anchor,'"diagnostic_channel":input["diagnostic_channel"],"diagnostic_resource_flags_describe_installed_resources_only":true,"gpu_draw_executed":true,')
example.write_text(rust)
q=(s/'queue_source_glb_normal_roughness_draw_v4.py').read_text()
q=q.replace("ROOT=W/'townsmith-paint-door-native-normal-roughness-draw-v4'", "CHANNEL=int(os.environ['OPENRPG_PRIVATE_UV_CHANNEL']);assert 0<=CHANNEL<2\nROOT=W/f'townsmith-native-normal-textures-channel-{CHANNEL}-v32'")
q=q.replace('verify_source_glb_normal_roughness_draw','verify_source_glb_normal_textures_v32')
q=q.replace('townsmith-paint-door-normal-roughness-carrier-v4/SM_Door-Single-B.normal-roughness-control.candidate.glb','townsmith-native-normal-textures-carrier-v32/SM_Door-Single-B.uv-control.candidate.glb')
q=q.replace('5ca721b3e485f74114031452e3cbfa7ecb6b1c58047a2d7259c6f01bca800c26',sha(glb))
q=q.replace('townsmith-paint-door-normal-roughness-carrier-v4/RESULT.v1.json','townsmith-native-normal-textures-carrier-v32/RESULT.v1.json')
q=q.replace("dict(glb=str(GLB),", "dict(diagnostic_channel=CHANNEL,glb=str(GLB),")
compile(q,str(queue),'exec')
queue.write_text(q)
subprocess.run([str(Path('C:/Users/karen/.rustup/toolchains/1.98.0-x86_64-pc-windows-msvc/bin/rustfmt.exe')),'--edition','2024',str(example)],check=True)
pins={str(p):sha(p) for p in [Path(__file__),original,original.parent/'RESULT.v1.json',example,queue,glb,w/'townsmith-door-source-uv-reopen-v25/RESULT.v1.json']}
(root/'RESULT.v1.json').write_text(json.dumps(dict(status='private_original_normal_texture_sample_carrier_prepared',input_pins=pins,original_binary_prefix_byte_equal=True,full_import=False,native_uv_comparison_verified=False),indent=2))
subprocess.run([sys.executable,'-B',str(s/'run_townsmith_native_normal_textures_v32.py')],check=True)
