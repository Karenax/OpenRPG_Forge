"""Draw the unchanged source graph with scene lighting disabled to isolate emission."""
from pathlib import Path
import hashlib
import json
import subprocess
import sys

S = Path(__file__).parent
R = S.parents[1]
W = Path('F:/OpenRPG_ImportWork')
EXAMPLE = R / 'crates/forge_engine/examples/verify_source_glb_emission_draw.rs'
QUEUE = S / 'queue_source_glb_emission_draw_v1.py'
assert not EXAMPLE.exists() and not QUEUE.exists()
text = (R / 'crates/forge_engine/examples/verify_source_glb_draw.rs').read_text(encoding='utf-8')
anchor = 'illuminance: context["light_intensity"].as_f64().unwrap() as f32'
assert text.count(anchor) == 1
text = text.replace(anchor, 'illuminance: 0.0')
text = text.replace('"gpu_draw_executed":true,"image":', '"emission_only_diagnostic":true,"directional_illuminance":0.0,"native_ambient_enabled":false,"native_sky_enabled":false,"gpu_draw_executed":true,"image":')
EXAMPLE.write_text(text, encoding='utf-8')
queue = (S / 'queue_source_glb_draw_v2.py').read_text(encoding='utf-8')
queue = queue.replace('townsmith-paint-door-native-draw-v2', 'townsmith-paint-door-native-emission-draw-v1')
queue = queue.replace('townsmith-door-source-render-context-v2', 'townsmith-door-source-emission-control-v13')
queue = queue.replace('verify_source_glb_draw', 'verify_source_glb_emission_draw')
QUEUE.write_text(queue, encoding='utf-8')
subprocess.run([sys.executable, '-B', str(QUEUE)], check=True)
