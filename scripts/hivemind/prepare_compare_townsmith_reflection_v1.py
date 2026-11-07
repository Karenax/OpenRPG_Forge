"""Derive a comparison with the prior ambient draw as its fixed control."""
from pathlib import Path
import subprocess
import sys

S = Path(__file__).parent
target = S / 'compare_townsmith_reflection_draw_v1.py'
assert not target.exists()
text = (S / 'compare_townsmith_ambient_draw_v1.py').read_text(encoding='utf-8')
text = text.replace('townsmith-paint-door-native-ambient-comparison-v1', 'townsmith-paint-door-native-reflection-comparison-v1')
text = text.replace("DRAW = W / 'townsmith-paint-door-native-ambient-draw-v1'", "DRAW = W / 'townsmith-paint-door-native-reflection-draw-v1'")
text = text.replace('townsmith-door-source-render-context-v7', 'townsmith-door-source-camera-brdf-v10')
text = text.replace('townsmith-paint-door-native-draw-v2/FORGE-LINEAR.rgba16f', 'townsmith-paint-door-native-ambient-draw-v1/FORGE-LINEAR.rgba16f')
text = text.replace('Forge before ambient', 'Forge before reflections').replace('Forge measured ambient', 'Forge measured reflections')
text = text.replace('measured_ambient_actual_draw_compared_fidelity_open', 'measured_reflection_actual_draw_compared_fidelity_open')
text = text.replace('L03.TOWNSMITH.DESSIN.02.APPLICATION', 'L03.TOWNSMITH.DESSIN.02.REFLETS.APPLICATION')
text = text.replace("assert native['consecutive_equal_readbacks'] >= 3", "assert native['measured_sky_reflection_applied']\nassert native['consecutive_equal_readbacks'] >= 3")
text = text.replace('changed_pixels_after_ambient', 'changed_pixels_after_reflection')
text = text.replace('measured_gpu_ambient_applied=True, rgb_threshold=', 'measured_sky_reflection_applied=True, measured_gpu_ambient_applied=True, rgb_threshold=')
text = text.replace('source_sky_and_specular_reflection_applied=False,', 'source_specular_reflection_applied=True, source_background_sky_applied=False, source_ssao_qualified=False,')
compile(text, str(target), 'exec')
target.write_text(text, encoding='utf-8')
subprocess.run([sys.executable, '-B', str(target)], check=True)
