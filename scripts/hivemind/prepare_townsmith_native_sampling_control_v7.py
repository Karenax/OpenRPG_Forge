"""Diagnostic native sampler control; do not change common authored policy."""
from pathlib import Path
import subprocess
import sys

S = Path(__file__).parent
R = S.parents[1]
example = R / 'crates/forge_engine/examples/verify_source_glb_sampling_control_v7.rs'
queue_path = S / 'queue_source_glb_sampling_control_v7.py'
assert not example.exists() and not queue_path.exists()
text = (R / 'crates/forge_engine/examples/verify_source_glb_normal_roughness_draw.rs').read_text(encoding='utf-8')
assert text.count('"ForceEnable" => SourceGraphSceneSampling::UnityForceEnableD3d,') == 1
text = text.replace('"ForceEnable" => SourceGraphSceneSampling::UnityForceEnableD3d,', '"ForceEnable" => SourceGraphSceneSampling::Authored, // Explicit diagnostic override, compare separately to source Disable.')
text = text.replace('"scene_sampling_policy":format!("{policy:?}"),', '"diagnostic_scene_sampling_override":"Authored; source reference context retained; compare to separate Disable control","scene_sampling_policy":format!("{policy:?}"),')
example.write_text(text, encoding='utf-8')
queue = (S / 'queue_source_glb_normal_roughness_draw_v4.py').read_text(encoding='utf-8')
queue = queue.replace('townsmith-paint-door-native-normal-roughness-draw-v4', 'townsmith-paint-door-native-sampling-control-v7')
queue = queue.replace('verify_source_glb_normal_roughness_draw', 'verify_source_glb_sampling_control_v7')
queue_path.write_text(queue, encoding='utf-8')
subprocess.run([sys.executable, '-B', str(queue_path)], check=True)
