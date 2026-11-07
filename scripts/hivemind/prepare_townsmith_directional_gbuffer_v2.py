"""Project diffuse lighting through the source sRGB8 GBuffer candidate."""
from pathlib import Path
import subprocess
import sys

S = Path(__file__).parent
target = S / 'build_townsmith_directional_gbuffer_v2.py'
assert not target.exists()
text = (S / 'prepare_townsmith_directional_brdf_v1.py').read_text(encoding='utf-8')
text = text.replace('townsmith-paint-door-directional-brdf-carrier-v1', 'townsmith-paint-door-directional-gbuffer-carrier-v2')
text = text.replace('verify_source_glb_directional_brdf_draw', 'verify_source_glb_directional_gbuffer_draw')
text = text.replace('queue_source_glb_directional_brdf_draw_v1', 'queue_source_glb_directional_gbuffer_draw_v2')
text = text.replace('townsmith-paint-door-native-directional-brdf-draw-v1', 'townsmith-paint-door-native-directional-gbuffer-draw-v2')
text = text.replace('SM_Door-Single-B.source-directional-brdf.candidate.glb', 'SM_Door-Single-B.source-directional-gbuffer.candidate.glb')
helper = '''// Analytic sRGB8 RT projection candidate; source observed GBuffer0 is sRGB8.
// Actual hardware encode/decode precision remains a separate qualification.
fn source_candidate_diffuse_gbuffer_projection(authored:vec3<f32>)->vec3<f32> {
 let x=max(authored,vec3<f32>(0.0));
 let encoded=select(1.055*pow(x,vec3<f32>(1.0/2.4))-vec3<f32>(0.055),12.92*x,x<=vec3<f32>(0.0031308));
 let c=vec3<f32>(source_deferred_unorm8(encoded.x),source_deferred_unorm8(encoded.y),source_deferred_unorm8(encoded.z));
 return select(pow((c+vec3<f32>(0.055))/1.055,vec3<f32>(2.4)),c/12.92,c<=vec3<f32>(0.04045));
}
'''
anchor = 'fn source_candidate_directional_brdf(light_id:u32'
assert text.count(anchor) == 1
text = text.replace(anchor, helper + anchor)
anchor = 'surface.diffuse_color*(1.0/3.141592653589793)'
assert text.count(anchor) == 1
text = text.replace(anchor, 'source_candidate_diffuse_gbuffer_projection(surface.diffuse_color)*(1.0/3.141592653589793)')
text = text.replace("paths = [Path(__file__), original,", "paths = [W/'townsmith-door-source-gbuffer-arrays-reopen-v21/RESULT.v1.json', Path(__file__), original,")
compile(text, str(target), 'exec')
target.write_text(text, encoding='utf-8')
subprocess.run([sys.executable, '-B', str(target)], check=True)
