"""Continue UV1 correction with actual full lit material, not diagnostic colour."""
from pathlib import Path
import subprocess
import sys

S=Path(__file__).parent
out=S/'build_townsmith_source_uv1_lighting_v37.py'
assert not out.exists()
q=(S/'build_townsmith_source_uv1_v36.py').read_text(encoding='utf-8')
for old,new in [
 ('townsmith-source-uv1-carrier-v36','townsmith-source-uv1-lighting-carrier-v37'),
 ('townsmith-native-source-uv1-v36','townsmith-native-source-uv1-lighting-v37'),
 ('verify_source_glb_source_uv1_v36','verify_source_glb_source_uv1_lighting_v37'),
 ('queue_source_glb_source_uv1_v36','queue_source_glb_source_uv1_lighting_v37'),
 ('SM_Door-Single-B.source-uv1-control.candidate.glb','SM_Door-Single-B.source-uv1-lighting.candidate.glb'),
 ('preserved_source_uv1_normal_roughness_control_prepared','preserved_source_uv1_full_material_lighting_control_prepared'),
]:
 assert old in q,old
 q=q.replace(old,new)
a="text=text.replace('out.color=source_graph_native_light_transport(p,surface);','out.color=source_candidate_normal_roughness_buffer(surface);')"
assert q.count(a)==1
q=q.replace(a,'# Preserve actual light transport; normal-only output is not used in this control.')
compile(q,str(out),'exec')
out.write_text(q,encoding='utf-8')
subprocess.run([sys.executable,'-B',str(out)],check=True)
