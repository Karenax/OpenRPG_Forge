"""Retain the failed example; fix the iterator reference in a new example."""
from pathlib import Path
import subprocess
import sys

S = Path(__file__).parent
R = S.parents[1]
example = R / 'crates/forge_engine/examples/verify_source_glb_native_depth_draw_v6.rs'
queue_path = S / 'queue_source_glb_native_depth_draw_v6.py'
assert not example.exists() and not queue_path.exists()
text = (R / 'crates/forge_engine/examples/verify_source_glb_native_depth_draw.rs').read_text(encoding='utf-8')
assert text.count('f32::from_le_bytes(chunk.try_into().unwrap())') == 1
text = text.replace('f32::from_le_bytes(chunk.try_into().unwrap())', 'f32::from_le_bytes((*chunk).try_into().unwrap())')
example.write_text(text, encoding='utf-8')
queue = (S / 'queue_source_glb_native_depth_draw_v5.py').read_text(encoding='utf-8')
queue = queue.replace('townsmith-paint-door-native-depth-draw-v5', 'townsmith-paint-door-native-depth-draw-v6')
queue = queue.replace('verify_source_glb_native_depth_draw', 'verify_source_glb_native_depth_draw_v6')
queue_path.write_text(queue, encoding='utf-8')
comparison = (S / 'compare_townsmith_native_depth_v5.py').read_text(encoding='utf-8')
comparison = comparison.replace('townsmith-paint-door-native-depth-draw-v5', 'townsmith-paint-door-native-depth-draw-v6')
comparison = comparison.replace('townsmith-door-native-depth-comparison-v5', 'townsmith-door-native-depth-comparison-v6')
(S / 'compare_townsmith_native_depth_v6.py').write_text(comparison, encoding='utf-8')
subprocess.run([sys.executable, '-B', str(queue_path)], check=True)
