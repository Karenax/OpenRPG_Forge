"""Version the native UV control to explicit fine derivatives, four channels."""
from pathlib import Path
import subprocess, sys
s=Path(__file__).parent
builder=s/'build_townsmith_native_fine_uv_v28.py'
batch=s/'run_townsmith_native_fine_uv_batch_v28.py'
assert not builder.exists() and not batch.exists()
q=(s/'prepare_townsmith_native_uv_v26.py').read_text(encoding='utf-8')
q=q.replace('v26','v28').replace('V26','V28')
q=q.replace('townsmith-native-uv-carrier-v28','townsmith-native-fine-uv-carrier-v28')
q=q.replace('verify_source_glb_uv_control_v28','verify_source_glb_fine_uv_control_v28')
q=q.replace('queue_source_glb_uv_control_v28','queue_source_glb_fine_uv_control_v28')
q=q.replace('townsmith-native-uv-channel-','townsmith-native-fine-uv-channel-')
q=q.replace('run_townsmith_native_uv_batch_v28','run_townsmith_native_fine_uv_batch_v28')
q=q.replace('dpdx(uv);let dy=dpdy(uv);','dpdxFine(uv);let dy=dpdyFine(uv);')
q=q.replace('array<f32,6>(uv.x,uv.y,dx.x,dx.y,dy.x,dy.y)','array<f32,4>(dx.x,dx.y,dy.x,dy.y)')
q=q.replace('source_time_parameters.w),5u','source_time_parameters.w),3u')
q=q.replace('*v < 6','*v < 4').replace('UV channel 0..5','UV channel 0..3')
q=q.replace('0<=CHANNEL<6','0<=CHANNEL<4')
q=q.replace("channels=['u','v','du_dx','dv_dx','du_dy','dv_dy']", "channels=['du_dx_fine','dv_dx_fine','du_dy_fine','dv_dy_fine']")
compile(q,str(builder),'exec');builder.write_text(q,encoding='utf-8')
q=(s/'run_townsmith_native_uv_batch_v26.py').read_text(encoding='utf-8')
q=q.replace('v26','v28').replace('townsmith-native-uv-batch-v28','townsmith-native-fine-uv-batch-v28')
q=q.replace('townsmith-native-uv-channel-','townsmith-native-fine-uv-channel-')
q=q.replace('queue_source_glb_uv_control_v28','queue_source_glb_fine_uv_control_v28')
q=q.replace('range(6)','range(4)').replace('six_native_uv_channels','four_native_fine_uv_channels')
compile(q,str(batch),'exec');batch.write_text(q,encoding='utf-8')
subprocess.run([sys.executable,'-B',str(builder)],check=True)
