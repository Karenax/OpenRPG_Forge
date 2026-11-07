"""Derive the measured directional candidate comparison without altering old proofs."""
from pathlib import Path
import subprocess
import sys

S = Path(__file__).parent
target = S / 'compare_townsmith_directional_brdf_v1.py'
assert not target.exists()
text = (S / 'compare_townsmith_lighting_components_v18.py').read_text(encoding='utf-8')
text = text.replace('townsmith-door-lighting-components-comparison-v18', 'townsmith-door-directional-brdf-comparison-v1')
anchor = "'townsmith-paint-door-native-reflection-draw-v1'):"
assert text.count(anchor) == 1
text = text.replace(anchor, "'townsmith-paint-door-native-reflection-draw-v1', 'townsmith-paint-door-native-directional-brdf-draw-v1'):")
text = text.replace("('direct diffuse + specular', components[0] + components[1], native[0]),", "('direct diffuse + specular BEFORE', components[0] + components[1], native[0]),\n    ('direct diffuse + specular CANDIDATE', components[0] + components[1], native[3] - native[2] + native[0]),")
text = text.replace("('full without postprocessing', full, native[2]),", "('full without postprocessing BEFORE', full, native[2]),\n    ('full without postprocessing CANDIDATE', full, native[3]),")
text = text.replace("'unfitted_lighting_components_compared_fidelity_open'", "'source_directional_brdf_actual_draw_compared_fidelity_open'")
text = text.replace("'L03.TOWNSMITH.DESSIN.02.TRANSPORT.DIRECT.COMPARAISON'", "'L03.TOWNSMITH.DESSIN.02.TRANSPORT.DIRECT.BRDF.CANDIDAT'")
text = text.replace("next_action='Inspect source deferred direct BRDF and GBuffer precision against native direct shading; preserve bloom as a separate scene effect.'", "next_action='Qualify source GBuffer diffuse colour, sun shape and intermediate precision against this candidate; preserve scene AO, bloom and background as separate open controls.'")
text = text.replace("for row in rows[:3]", "for row in rows[:2]")
text = text.replace("current_work_sub_lot='L03.TOWNSMITH.DESSIN.02.TRANSPORT.DIRECT.BRDF'", "current_work_sub_lot='L03.TOWNSMITH.DESSIN.02.TRANSPORT.DIRECT.GBUFFER'")
anchor = "for path, expected in pins.items():"
assert text.count(anchor) == 1
text = text.replace(anchor, "carrier = W / 'townsmith-paint-door-directional-brdf-carrier-v1/RESULT.v1.json'\npins.update(read(carrier)['input_pins'])\npins[str(carrier)] = sha(carrier)\n" + anchor)
compile(text, str(target), 'exec')
target.write_text(text, encoding='utf-8')
subprocess.run([sys.executable, '-B', str(target)], check=True)
