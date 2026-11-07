"""Draw source diffuse output only, keeping original graph, mesh and textures."""
from pathlib import Path
import subprocess
import sys

S = Path(__file__).parent
target = S / 'build_townsmith_diffuse_material_draw_v3.py'
assert not target.exists()
text = (S / 'build_townsmith_directional_gbuffer_v2.py').read_text(encoding='utf-8')
text = text.replace('townsmith-paint-door-directional-gbuffer-carrier-v2', 'townsmith-paint-door-diffuse-material-carrier-v3')
text = text.replace('verify_source_glb_directional_gbuffer_draw', 'verify_source_glb_diffuse_material_draw')
text = text.replace('queue_source_glb_directional_gbuffer_draw_v2', 'queue_source_glb_diffuse_material_draw_v3')
text = text.replace('townsmith-paint-door-native-directional-gbuffer-draw-v2', 'townsmith-paint-door-native-diffuse-material-draw-v3')
text = text.replace('SM_Door-Single-B.source-directional-gbuffer.candidate.glb', 'SM_Door-Single-B.diffuse-material-control.candidate.glb')
anchor = 'derived = (text.replace(anchor,'
assert text.count(anchor) == 1
text = text.replace(anchor, "assert text.count('out.color=source_graph_native_light_transport(p,surface);') == 1\ntext=text.replace('out.color=source_graph_native_light_transport(p,surface);','out.color=vec4<f32>(source_candidate_diffuse_gbuffer_projection(surface.diffuse_color),surface.alpha);')\n" + anchor)
anchor = "EXAMPLE.write_text(example, encoding='utf-8')"
assert text.count(anchor) == 1
text = text.replace(anchor, "assert example.count('Color::linear_rgb(0.025, 0.025, 0.025)') == 1\nexample=example.replace('Color::linear_rgb(0.025, 0.025, 0.025)','Color::linear_rgba(0.0, 0.0, 0.0, 0.0)')\n" + anchor)
text = text.replace("status='source_directional_brdf_private_candidate_prepared'", "status='source_diffuse_material_only_private_control_prepared'")
compile(text, str(target), 'exec')
target.write_text(text, encoding='utf-8')
subprocess.run([sys.executable, '-B', str(target)], check=True)
