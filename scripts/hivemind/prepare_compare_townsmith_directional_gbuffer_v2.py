"""Compare the sRGB8 projection against the prior actual BRDF candidate."""
from pathlib import Path
import subprocess
import sys

S = Path(__file__).parent
target = S / 'compare_townsmith_directional_gbuffer_v2.py'
assert not target.exists()
text = (S / 'compare_townsmith_directional_brdf_v1.py').read_text(encoding='utf-8')
text = text.replace('townsmith-door-directional-brdf-comparison-v1', 'townsmith-door-directional-gbuffer-comparison-v2')
anchor = "'townsmith-paint-door-native-directional-brdf-draw-v1'):"
assert text.count(anchor) == 1
text = text.replace(anchor, "'townsmith-paint-door-native-directional-gbuffer-draw-v2', 'townsmith-paint-door-native-directional-brdf-draw-v1'):")
text = text.replace("'townsmith-paint-door-directional-brdf-carrier-v1/RESULT.v1.json'", "'townsmith-paint-door-directional-gbuffer-carrier-v2/RESULT.v1.json'")
text = text.replace("('direct diffuse + specular BEFORE', components[0] + components[1], native[0]),", "('direct diffuse + specular BEFORE', components[0] + components[1], native[4] - native[2] + native[0]),")
text = text.replace("('full without postprocessing BEFORE', full, native[2]),", "('full without postprocessing BEFORE', full, native[4]),")
text = text.replace('source_directional_brdf_actual_draw_compared_fidelity_open', 'source_directional_srgb_gbuffer_actual_draw_compared_fidelity_open')
text = text.replace('L03.TOWNSMITH.DESSIN.02.TRANSPORT.DIRECT.BRDF.CANDIDAT', 'L03.TOWNSMITH.DESSIN.02.TRANSPORT.DIRECT.GBUFFER.CANDIDAT')
text = text.replace("next_action='Qualify source GBuffer diffuse colour, sun shape and intermediate precision against this candidate; preserve scene AO, bloom and background as separate open controls.'", "next_action='Capture native/source material outputs and independent depth masks; qualify source buffer lifetime and viewport mapping, then apply scene AO, bloom and background separately.'")
text = text.replace("current_work_sub_lot='L03.TOWNSMITH.DESSIN.02.TRANSPORT.DIRECT.GBUFFER'", "current_work_sub_lot='L03.TOWNSMITH.DESSIN.02.TRANSPORT.DIRECT.MATERIAUX.PROFONDEUR'")
compile(text, str(target), 'exec')
target.write_text(text, encoding='utf-8')
subprocess.run([sys.executable, '-B', str(target)], check=True)
