"""Probe original normal texture samples as exact float32 bits, without source edits."""
from pathlib import Path
from datetime import datetime,timezone
import hashlib,json,struct,subprocess,sys
s=Path(__file__).parent;r=s.parents[1];w=Path('F:/OpenRPG_ImportWork');root=w/'townsmith-native-normal-texel-bits-carrier-v60';assert not root.exists()
def sha(p):
 with p.open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
parent=w/'townsmith-native-graph-bits-carrier-v58/SM_Door-Single-B.graph-bits.candidate.glb';proof=json.loads((parent.parent/'RESULT.v1.json').read_text());assert sha(parent)==proof['input_pins'][str(parent)]
raw=parent.read_bytes();n=struct.unpack_from('<I',raw,12)[0];doc=json.loads(raw[20:20+n]);prefix=raw[28+n:];binary=bytearray(prefix);c=doc['materials'][0]['extensions']['OPENRPG_source_graph'];v=doc['bufferViews'][c['forward']['buffer_view']];stage=prefix[v.get('byteOffset',0):v.get('byteOffset',0)+v['byteLength']];assert hashlib.sha256(stage).hexdigest()==c['forward']['sha256'];t=stage.decode()
anchor='let raw=source_graph_surface_from_native_frame(in,is_front);out.color=source_private_graph_probe(raw);';assert t.count(anchor)==1;t=t.replace(anchor,'out.color=source_private_graph_probe(in.source_uv0.xy);')
anchor='fn source_private_graph_probe(raw:MaterialValues)->vec4<f32> {';assert t.count(anchor)==1;t=t[:t.index(anchor)]+"""fn source_private_graph_probe(uv:vec2<f32>)->vec4<f32> {
 let channel=u32(source_graph.source_time_parameters.w);
 let p6=vec2<i32>(floor(fract(uv)*2048.0));
 let p7=vec2<i32>(floor(fract(uv*30.0)*2048.0));
 let a=textureLoad(t6,p6,0);
 let b=textureLoad(t7,p7,0);
 var value=select(a,b,channel>=4u)[channel%4u];
 if channel>=8u {value=select(f32(p6[(channel-8u)%2u]),f32(p7[(channel-8u)%2u]),channel>=10u);}
 let bits=bitcast<u32>(value);
 return vec4<f32>(f32(bits&255u),f32((bits>>8u)&255u),f32((bits>>16u)&255u),f32((bits>>24u)&255u))/255.0;
}
"""
b=t.encode();idx=len(doc['bufferViews']);doc['bufferViews'].append(dict(buffer=0,byteOffset=len(binary),byteLength=len(b)));binary.extend(b);c['forward']=dict(buffer_view=idx,sha256=hashlib.sha256(b).hexdigest());binary.extend(b'\0'*(-len(binary)%4));doc['buffers'][0]['byteLength']=len(binary);doc['extras']['private_uv_control']=dict(channels=['normal6_r','normal6_g','normal6_b','normal6_a','normal7_r','normal7_g','normal7_b','normal7_a','texel6_x','texel6_y','texel7_x','texel7_y'],transport='float32_bits_in_four_normalized_bytes',full_import=False)
j=json.dumps(doc,separators=(',',':')).encode();j+=b' '*(-len(j)%4);out=struct.pack('<III',0x46546c67,2,28+len(j)+len(binary))+struct.pack('<II',len(j),0x4e4f534a)+j+struct.pack('<II',len(binary),0x004e4942)+binary;assert binary[:len(prefix)]==prefix
root.mkdir();glb=root/'SM_Door-Single-B.normal-texel-bits.candidate.glb';glb.write_bytes(out);(root/'normal-texel-bits.forward.wgsl').write_bytes(b)
ex=r/'crates/forge_engine/examples/verify_source_glb_normal_texel_bits_v60.rs';assert not ex.exists();rust=(r/'crates/forge_engine/examples/verify_source_glb_graph_bits_v58.rs').read_text();assert '*v < 9' in rust;ex.write_text(rust.replace('*v < 9','*v < 12').replace('Graph bit probe channel 0..8 required','Normal texel bit probe channel 0..11 required'),encoding='utf-8')
q=s/'queue_source_glb_normal_texel_bits_v60.py';assert not q.exists();qt=(s/'queue_source_glb_graph_bits_v58.py').read_text();qt=qt.replace('CHANNEL<9','CHANNEL<12').replace('townsmith-native-graph-bits-channel-{CHANNEL}-v58','townsmith-native-normal-texel-bits-channel-{CHANNEL}-v60').replace('verify_source_glb_graph_bits_v58','verify_source_glb_normal_texel_bits_v60').replace('townsmith-native-graph-bits-carrier-v58/SM_Door-Single-B.graph-bits.candidate.glb','townsmith-native-normal-texel-bits-carrier-v60/SM_Door-Single-B.normal-texel-bits.candidate.glb').replace(sha(parent),sha(glb)).replace('townsmith-native-graph-bits-carrier-v58/RESULT.v1.json','townsmith-native-normal-texel-bits-carrier-v60/RESULT.v1.json');compile(qt,str(q),'exec');q.write_text(qt,encoding='utf-8')
runner=s/'run_townsmith_normal_texel_bits_v60.py';assert not runner.exists();rt=(s/'run_townsmith_native_graph_bits_v58.py').read_text().replace('townsmith-native-graph-bits-batch-v58','townsmith-native-normal-texel-bits-batch-v60').replace('range(9)','range(12)').replace('queue_source_glb_graph_bits_v58.py','queue_source_glb_normal_texel_bits_v60.py').replace('townsmith-native-graph-bits-channel-{channel}-v58','townsmith-native-normal-texel-bits-channel-{channel}-v60').replace('nine_native_graph_bit_channels_drawn_with_stable_readbacks','twelve_native_normal_texel_bit_channels_drawn_with_stable_readbacks');compile(rt,str(runner),'exec');runner.write_text(rt,encoding='utf-8')
subprocess.run(['C:/Users/karen/.rustup/toolchains/1.98.0-x86_64-pc-windows-msvc/bin/rustfmt.exe','--edition','2024',str(ex)],check=True)
(root/'RESULT.v1.json').write_text(json.dumps(dict(status='original_normal_sample_float32_bit_transport_prepared',input_pins={str(p):sha(p) for p in [Path(__file__),parent,glb,ex,q,runner]},original_binary_prefix_byte_equal=True,full_import=False,material_fidelity_qualified=False),indent=2),encoding='utf-8')
if len(sys.argv)==1:subprocess.run([sys.executable,'-B',str(runner)],check=True)
