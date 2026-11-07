"""Draw raw normal 6/7 samples with original native textures and samplers."""
from pathlib import Path
import subprocess,sys
s=Path(__file__).parent
builder=s/'build_townsmith_native_normal_textures_v32.py'
batch=s/'run_townsmith_native_normal_textures_v32.py'
assert not builder.exists() and not batch.exists()
q=(s/'prepare_townsmith_native_uv_v26.py').read_text(encoding='utf-8')
q=q.replace('v26','v32').replace('V26','V32')
q=q.replace('townsmith-native-uv-carrier-v32','townsmith-native-normal-textures-carrier-v32')
q=q.replace('verify_source_glb_uv_control_v32','verify_source_glb_normal_textures_v32')
q=q.replace('queue_source_glb_uv_control_v32','queue_source_glb_normal_textures_v32')
q=q.replace('townsmith-native-uv-channel-','townsmith-native-normal-textures-channel-')
q=q.replace('run_townsmith_native_uv_batch_v32','run_townsmith_native_normal_textures_v32')
q=q.replace('out.color=source_private_uv_bytes(in.source_uv0.xy);','out.color=source_private_normal_textures(in.source_uv0.xy);')
start=q.index("text += '''");end=q.index("\n'''",start)+4
function='''
// Diagnostic only: raw samples from the original normal textures.
fn source_private_normal_textures(uv:vec2<f32>)->vec4<f32> {
 let a=textureSample(t6,s6,uv);let b=textureSample(t7,s7,uv*30.0);
 return select(a,b,source_graph.source_time_parameters.w>0.5);
}
'''
q=q[:start]+'text += '+repr(function)+q[end:]
q=q.replace('*v < 6','*v < 2').replace('UV channel 0..5','Normal texture channel 0..1')
q=q.replace('0<=CHANNEL<6','0<=CHANNEL<2')
q=q.replace("channels=['u','v','du_dx','dv_dx','du_dy','dv_dy']", "channels=['normal6_raw','normal7_raw_uv30']")
q=q.replace('private_uv_byte_transport_carrier_prepared','private_original_normal_texture_sample_carrier_prepared')
compile(q,str(builder),'exec');builder.write_text(q,encoding='utf-8')
q=(s/'run_townsmith_native_uv_batch_v26.py').read_text(encoding='utf-8')
q=q.replace('v26','v32')
q=q.replace('townsmith-native-uv-batch-v32','townsmith-native-normal-textures-batch-v32')
q=q.replace('townsmith-native-uv-channel-','townsmith-native-normal-textures-channel-')
q=q.replace('queue_source_glb_uv_control_v32','queue_source_glb_normal_textures_v32')
q=q.replace('range(6)','range(2)').replace('six_native_uv_channels','two_native_normal_texture_channels')
compile(q,str(batch),'exec');batch.write_text(q,encoding='utf-8')
subprocess.run([sys.executable,'-B',str(builder)],check=True)
