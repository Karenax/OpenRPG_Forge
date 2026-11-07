"""Causal material control: explicit fine gradients, original textures/samplers."""
from pathlib import Path
import subprocess,sys
s=Path(__file__).parent
builder=s/'build_townsmith_fine_sampling_v29.py'
assert not builder.exists()
q=(s/'build_townsmith_normal_roughness_draw_v4.py').read_text(encoding='utf-8')
q=q.replace('townsmith-paint-door-normal-roughness-carrier-v4','townsmith-paint-door-fine-sampling-carrier-v29')
q=q.replace('townsmith-paint-door-native-normal-roughness-draw-v4','townsmith-paint-door-native-fine-sampling-v29')
q=q.replace('verify_source_glb_normal_roughness_draw','verify_source_glb_fine_sampling_v29')
q=q.replace('queue_source_glb_normal_roughness_draw_v4','queue_source_glb_fine_sampling_v29')
q=q.replace('SM_Door-Single-B.normal-roughness-control.candidate.glb','SM_Door-Single-B.fine-sampling-control.candidate.glb')
anchor="text = stage.decode('utf-8')"
assert q.count(anchor)==1
q=q.replace(anchor,anchor+"\nassert text.count('textureSample(')==20\ntext=text.replace('textureSample(','source_private_fine_sample(')\n")
anchor='derived = (text.replace(anchor,'
assert q.count(anchor)==1
function='''
// Diagnostic only. Original implicit sampling remains the fidelity target.
fn source_private_fine_sample(tex:texture_2d<f32>,samp:sampler,uv:vec2<f32>)->vec4<f32> {
 return textureSampleGrad(tex,samp,uv,dpdxFine(uv),dpdyFine(uv));
}
'''
q=q.replace(anchor,'helper+='+repr(function)+'\n'+anchor)
q=q.replace("status='source_normal_roughness_only_private_control_prepared'", "status='private_fine_gradient_normal_roughness_control_prepared'")
# Pin a formatted owned example, not bytes later changed by the controller.
anchor='pins = {str(path): sha(path) for path in paths}'
assert q.count(anchor)==1
q=q.replace(anchor,"subprocess.run(['C:/Users/karen/.rustup/toolchains/1.98.0-x86_64-pc-windows-msvc/bin/rustfmt.exe','--edition','2024',str(EXAMPLE)],check=True)\n"+anchor)
compile(q,str(builder),'exec');builder.write_text(q,encoding='utf-8')
subprocess.run([sys.executable,'-B',str(builder)],check=True)
