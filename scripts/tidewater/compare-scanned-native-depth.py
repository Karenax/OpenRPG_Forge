"""Compare an additive scanned run against the same native scene without scans."""
import hashlib
import json
from pathlib import Path
import sys
import numpy as np
from PIL import Image

with_scans, without_scans, receipt = map(Path, sys.argv[1:4])
if receipt.exists():
    raise RuntimeError('Fresh receipt required')
def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()

images = [np.array(Image.open(root / 'island.png').convert('RGB'))
          for root in (with_scans, without_scans)]
if images[0].shape != images[1].shape or images[0].shape != (1080, 1920, 3):
    raise RuntimeError('Matched 1920x1080 images required')
delta = np.abs(images[0].astype(np.int16) - images[1].astype(np.int16))
changed = np.any(delta != 0, axis=2)
y, x = np.nonzero(changed)
layers = []
depths = []
for root in (with_scans, without_scans):
    raw = root / 'island.object-depth.bin'
    if raw.stat().st_size != 3 * 2048 * 2048 * 4:
        raise RuntimeError('Complete three-cascade depth readback required')
    values = np.fromfile(raw, dtype='<f4').reshape(3, 2048, 2048)
    if not np.isfinite(values).all() or not ((values >= 0) & (values <= 1)).all():
        raise RuntimeError('Finite normalized depth required')
    depths.append(values)
for near, baseline in zip(*depths):
    layers.append({'changed': int(np.count_nonzero(near != baseline)),
                   'nearer_with_scans': int(np.count_nonzero(near < baseline)),
                   'farther_with_scans': int(np.count_nonzero(near > baseline))})
additive = any(layer['nearer_with_scans'] for layer in layers) and not any(
    layer['farther_with_scans'] for layer in layers)
result = {'schema': 'forge.tidewater.native-scanned-additive-comparison', 'version': 1,
          'with_scans': str(with_scans), 'without_scans': str(without_scans),
          'image_pixels_changed': int(changed.sum()), 'rgb_max_difference': int(delta.max()),
          'image_changed_bounds': [int(x.min()), int(y.min()), int(x.max()), int(y.max())] if x.size else None,
          'depth_layers': layers, 'additive_depth_only': additive,
          'verdict': 'DEPTH_ADDITIVE_PASS' if additive else 'UNRESOLVED',
          'rgb_attribution': 'UNQUALIFIED: temporal effects and capture timing are not paired',
          'files': {str(root / name): sha(root / name) for root in (with_scans, without_scans)
                    for name in ('island.png', 'island.object-depth.bin')},
          'limits': ['requires matching launch inputs and binary recorded separately',
                     'no source Three.js comparison', 'no per-instance coverage claim',
                     'no physical or Studio qualification']}
receipt.write_text(json.dumps(result, indent=2) + '\n', encoding='utf-8')
print(json.dumps({k: result[k] for k in ('verdict', 'image_pixels_changed', 'depth_layers')}, indent=2))
sys.exit(0 if result['verdict'] == 'DEPTH_ADDITIVE_PASS' else 2)
