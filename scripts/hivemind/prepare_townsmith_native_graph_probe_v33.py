"""Capture native graph TS normal and both blend weights before frame conversion."""
from pathlib import Path
import subprocess,sys
s=Path(__file__).parent
builder=s/'build_townsmith_native_graph_probe_v33.py';batch=s/'run_townsmith_native_graph_probe_v33.py'
assert not builder.exists() and not batch.exists()
q=(s/'prepare_townsmith_native_uv_v26.py').read_text(encoding='utf-8')
q=q.replace('v26','v33').replace('V26','V33').replace('townsmith-native-uv-carrier-v33','townsmith-native-graph-probe-carrier-v33')
q=q.replace('verify_source_glb_uv_control_v33','verify_source_glb_graph_probe_v33').replace('queue_source_glb_uv_control_v33','queue_source_glb_graph_probe_v33')
q=q.replace('townsmith-native-uv-channel-','townsmith-native-graph-probe-channel-').replace('run_townsmith_native_uv_batch_v33','run_townsmith_native_graph_probe_v33')
anchor="text = stage.decode()"
assert q.count(anchor)==1
patch='''
assert text.count('alpha:f32,')==1
text=text.replace('alpha:f32,','alpha:f32,probe_blend1:vec4<f32>,probe_blend2:vec4<f32>,')
before='return MaterialValues(n109,(n115).xyz,source_tangent_space_normal,(n120).x,vec3<f32>(0.0,0.0,0.0),(n128).x,(n133).x,1.0);'
assert text.count(before)==1
text=text.replace(before,before.replace('1.0);','1.0,n52,n107);'))
'''
q=q.replace(anchor,anchor+patch)
q=q.replace('out.color=source_private_uv_bytes(in.source_uv0.xy);','let raw=source_graph_surface_from_native_frame(in,is_front);out.color=source_private_graph_probe(raw);')
start=q.index("text += '''");end=q.index("\n'''",start)+4
helper='''
fn source_private_graph_probe(raw:MaterialValues)->vec4<f32> {
 let normal=vec4<f32>(raw.normal_ts,1.0);
 return select(select(normal,raw.probe_blend1,source_graph.source_time_parameters.w>0.5),raw.probe_blend2,source_graph.source_time_parameters.w>1.5);
}
'''
q=q[:start]+'text += '+repr(helper)+q[end:]
q=q.replace('*v < 6','*v < 3').replace('UV channel 0..5','Graph probe channel 0..2').replace('0<=CHANNEL<6','0<=CHANNEL<3')
q=q.replace("channels=['u','v','du_dx','dv_dx','du_dy','dv_dy']", "channels=['normal_ts','blend1','blend2']")
q=q.replace('private_uv_byte_transport_carrier_prepared','private_graph_intermediates_carrier_prepared')
compile(q,str(builder),'exec');builder.write_text(q,encoding='utf-8')
q=(s/'run_townsmith_native_uv_batch_v26.py').read_text(encoding='utf-8')
q=q.replace('v26','v33').replace('townsmith-native-uv-batch-v33','townsmith-native-graph-probe-batch-v33')
q=q.replace('townsmith-native-uv-channel-','townsmith-native-graph-probe-channel-').replace('queue_source_glb_uv_control_v33','queue_source_glb_graph_probe_v33')
q=q.replace('range(6)','range(3)').replace('six_native_uv_channels','three_native_graph_probe_channels')
compile(q,str(batch),'exec');batch.write_text(q,encoding='utf-8')
subprocess.run([sys.executable,'-B',str(builder)],check=True)
