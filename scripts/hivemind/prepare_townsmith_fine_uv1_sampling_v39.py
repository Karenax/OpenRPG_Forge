"""Isolate remaining UV1 mask filtering differences; keep source unchanged."""
from pathlib import Path
import subprocess
import sys

S=Path(__file__).parent
out=S/'build_townsmith_fine_uv1_sampling_v39.py'
assert not out.exists()
q=(S/'build_townsmith_source_uv1_v36.py').read_text(encoding='utf-8')
for old,new in [
 ('townsmith-source-uv1-carrier-v36','townsmith-fine-uv1-carrier-v39'),
 ('townsmith-native-source-uv1-v36','townsmith-native-fine-uv1-v39'),
 ('verify_source_glb_source_uv1_v36','verify_source_glb_fine_uv1_v39'),
 ('queue_source_glb_source_uv1_v36','queue_source_glb_fine_uv1_v39'),
 ('SM_Door-Single-B.source-uv1-control.candidate.glb','SM_Door-Single-B.fine-uv1-control.candidate.glb'),
 ('preserved_source_uv1_normal_roughness_control_prepared','private_fine_uv1_filtering_normal_roughness_control_prepared'),
]:
 assert old in q,old
 q=q.replace(old,new)
a='text=preserve_source_uv1(text)'
assert q.count(a)==1
helper='''
// Private causal control for the two original UV1 mask samples only.
fn source_private_fine_uv1_sample(tex:texture_2d<f32>,samp:sampler,uv:vec2<f32>)->vec4<f32> {
 return textureSampleGrad(tex,samp,uv,dpdxFine(uv),dpdyFine(uv));
}
'''
q=q.replace(a,a+"\nassert text.count('textureSample(t2,s2,source_uv1.xy)')==2\ntext=text.replace('textureSample(t2,s2,source_uv1.xy)','source_private_fine_uv1_sample(t2,s2,source_uv1.xy)')\ntext+="+repr(helper)+'\n')
compile(q,str(out),'exec')
out.write_text(q,encoding='utf-8')
subprocess.run([sys.executable,'-B',str(out)],check=True)
