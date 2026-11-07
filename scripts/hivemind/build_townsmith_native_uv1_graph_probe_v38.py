"""Private six-channel bit-preserving UV diagnostic using the common GLB path."""

from pathlib import Path

from datetime import datetime, timezone

import hashlib, json, struct, subprocess, sys



s = Path(__file__).parent

r = s.parents[1]

w = Path('F:/OpenRPG_ImportWork')

root = w/'townsmith-native-graph-probe-carrier-v38'

example = r/'crates/forge_engine/examples/verify_source_glb_graph_probe_v38.rs'

queue = s/'queue_source_glb_graph_probe_v38.py'

assert not root.exists() and not example.exists() and not queue.exists()

def sha(p):

    with p.open('rb') as stream: return hashlib.file_digest(stream,'sha256').hexdigest()

original = w/'townsmith-source-uv1-carrier-v36/SM_Door-Single-B.source-uv1-control.candidate.glb'

proof = json.loads((original.parent/'RESULT.v1.json').read_text(encoding='utf-8'))

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

assert text.count('    alpha:f32,')==1

text=text.replace('    alpha:f32,','alpha:f32,probe_blend1:vec4<f32>,probe_blend2:vec4<f32>,')

before='return MaterialValues(n109,(n115).xyz,source_tangent_space_normal,(n120).x,vec3<f32>(0.0,0.0,0.0),(n128).x,(n133).x,1.0);'

assert text.count(before)==1

text=text.replace(before,before.replace('1.0);','1.0,n52,n107);'))



# This graph does not use time. Only the private diagnostic uses its .w lane.

assert text.count('source_time_parameters') == 1

anchor = 'out.color=source_candidate_normal_roughness_buffer(surface);'

assert text.count(anchor) == 1

text = text.replace(anchor,'let raw=source_graph_surface_from_native_frame(in,is_front);out.color=source_private_graph_probe(raw);')

text += '\nfn source_private_graph_probe(raw:MaterialValues)->vec4<f32> {\n let normal=vec4<f32>(raw.normal_ts,1.0);\n return select(select(normal,raw.probe_blend1,source_graph.source_time_parameters.w>0.5),raw.probe_blend2,source_graph.source_time_parameters.w>1.5);\n}\n'

derived = text.encode()

while len(binary)%4: binary.append(0)

index = len(doc['bufferViews'])

doc['bufferViews'].append(dict(buffer=0,byteOffset=len(binary),byteLength=len(derived)))

binary.extend(derived)

contract['forward']=dict(buffer_view=index,sha256=hashlib.sha256(derived).hexdigest())

while len(binary)%4: binary.append(0)

doc['buffers'][0]['byteLength']=len(binary)

doc.setdefault('extras',{})['private_uv_control']=dict(channels=['normal_ts','blend1','blend2'],full_import=False)

encoded=json.dumps(doc,separators=(',',':')).encode()

encoded+=b' '*(-len(encoded)%4)

output=struct.pack('<III',0x46546c67,2,28+len(encoded)+len(binary))+struct.pack('<II',len(encoded),0x4e4f534a)+encoded+struct.pack('<II',len(binary),0x004e4942)+binary

root.mkdir()

glb=root/'SM_Door-Single-B.uv-control.candidate.glb'

glb.write_bytes(output)

assert bytes(binary[:len(prefix)])==prefix

(root/'uv-control.forward.wgsl').write_bytes(derived)

rust=(r/'crates/forge_engine/examples/verify_source_glb_normal_roughness_draw.rs').read_text(encoding='utf-8')

anchor='source_time_parameters: Vec4::ZERO,'

assert rust.count(anchor)==1

rust=rust.replace(anchor,'source_time_parameters: Vec4::new(0.0,0.0,0.0,input["diagnostic_channel"].as_u64().filter(|v| *v < 3).ok_or("Graph probe channel 0..2 required")? as f32),')

anchor='"gpu_draw_executed":true,'

assert rust.count(anchor)==1

rust=rust.replace(anchor,'"diagnostic_channel":input["diagnostic_channel"],"diagnostic_resource_flags_describe_installed_resources_only":true,"gpu_draw_executed":true,')

example.write_text(rust)

q=(s/'queue_source_glb_normal_roughness_draw_v4.py').read_text(encoding='utf-8')

q=q.replace("ROOT=W/'townsmith-paint-door-native-normal-roughness-draw-v4'", "CHANNEL=int(os.environ['OPENRPG_PRIVATE_UV_CHANNEL']);assert 0<=CHANNEL<3\nROOT=W/f'townsmith-native-graph-probe-channel-{CHANNEL}-v38'")

q=q.replace('verify_source_glb_normal_roughness_draw','verify_source_glb_graph_probe_v38')

q=q.replace('townsmith-paint-door-normal-roughness-carrier-v4/SM_Door-Single-B.normal-roughness-control.candidate.glb','townsmith-native-graph-probe-carrier-v38/SM_Door-Single-B.uv-control.candidate.glb')

q=q.replace('5ca721b3e485f74114031452e3cbfa7ecb6b1c58047a2d7259c6f01bca800c26',sha(glb))

q=q.replace('townsmith-paint-door-normal-roughness-carrier-v4/RESULT.v1.json','townsmith-native-graph-probe-carrier-v38/RESULT.v1.json')

q=q.replace("dict(glb=str(GLB),", "dict(diagnostic_channel=CHANNEL,glb=str(GLB),")

compile(q,str(queue),'exec')

queue.write_text(q)

subprocess.run([str(Path('C:/Users/karen/.rustup/toolchains/1.98.0-x86_64-pc-windows-msvc/bin/rustfmt.exe')),'--edition','2024',str(example)],check=True)

pins={str(p):sha(p) for p in [Path(__file__),original,original.parent/'RESULT.v1.json',example,queue,glb,w/'townsmith-door-source-uv-reopen-v25/RESULT.v1.json',w/'townsmith-s-paint-wgsl-uv-inputs-v2/RESULT.v1.json']}

(root/'RESULT.v1.json').write_text(json.dumps(dict(status='private_graph_intermediates_carrier_prepared',input_pins=pins,original_binary_prefix_byte_equal=True,full_import=False,native_uv_comparison_verified=False),indent=2))

subprocess.run([sys.executable,'-B',str(s/'run_townsmith_native_uv1_graph_probe_v38.py')],check=True)

