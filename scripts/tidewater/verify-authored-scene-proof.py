"""Check saved-state native captures and independently count the coastal exclusion."""
import hashlib
import json
from pathlib import Path
import sys

import numpy as np
from PIL import Image

root, report_path = map(Path, sys.argv[1:])
assert not report_path.exists()
baseline_path = root / 'native-canonical-vegetation-001.png'
baseline = np.array(Image.open(baseline_path).convert('RGBA'))
report = {'baseline_sha256': hashlib.sha256(baseline_path.read_bytes()).hexdigest()}
for state, suffix in [('clearing', '002'), ('undo', '002'), ('zero', '001')]:
    path = root / f'native-authored-{state}-{suffix}.png'
    pixels = np.array(Image.open(path).convert('RGBA'))
    assert pixels.shape == baseline.shape == (1080, 1920, 4)
    delta = np.abs(pixels.astype(np.int16) - baseline.astype(np.int16))
    changed = np.any(delta, axis=2)
    y, x = np.where(changed)
    report[state] = {
        'file': path.name,
        'sha256': hashlib.sha256(path.read_bytes()).hexdigest(),
        'changed_pixels': int(changed.sum()),
        'max_channel_delta': int(delta.max()),
        'mean_channel_delta': float(delta.mean()),
        'bbox': [int(x.min()), int(y.min()), int(x.max()), int(y.max())] if len(x) else None,
    }
assert report['undo']['changed_pixels'] == 0
assert report['clearing']['changed_pixels'] > 0
assert report['zero']['changed_pixels'] > 0
origins = json.loads((root / 'imported-vegetation-003/import.json').read_text())
counts = {}
for block in origins['blocks']:
    source = root / 'source-017' / block['source_file']
    assert hashlib.sha256(source.read_bytes()).hexdigest() == block['source_sha256']
    instances = json.loads(source.read_text())
    counts[block['layer_id']] = sum(
        (i['x'] - 250) ** 2 + (i['z'] - 140) ** 2 <= 110 ** 2 for i in instances
    )
assert sum(counts.values()) == 515
log = (root / 'native-authored-clearing-002.log').read_text(encoding='utf-8')
assert log.count('visible=64868, excluded=515, density_removed=0') == 2
report['clearing']['source_excluded_by_layer'] = counts
report['scope'] = 'Fixed-camera native saved-state proof; not Studio or complete visual parity.'
report_path.write_text(json.dumps(report, indent=2))
print(json.dumps(report, indent=2))
