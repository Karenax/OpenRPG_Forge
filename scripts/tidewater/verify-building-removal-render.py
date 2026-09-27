"""Check saved removal/undo captures and their shadow readbacks."""
from pathlib import Path
import hashlib
import json
import sys

import numpy as np
from PIL import Image

root, output = map(Path, sys.argv[1:])
assert not output.exists()
base = np.asarray(Image.open(root / 'native-buildings-initial-001.png')).astype(int)
proof = {}
for name in ['initial', 'removed', 'undo']:
    prefix = 'native-buildings-' + name + '-001'
    path = root / (prefix + '.png')
    image = np.asarray(Image.open(path)).astype(int)
    assert image.shape == base.shape
    difference = abs(base - image)
    item = {'png_sha256': hashlib.sha256(path.read_bytes()).hexdigest(),
            'changed_pixels': int(np.any(difference, axis=2).sum()),
            'max_channel_difference': int(difference.max())}
    for suffix in ['object-depth.bin', 'shadow.bin']:
        before = (root / ('native-buildings-initial-001.' + suffix)).read_bytes()
        after = (root / (prefix + '.' + suffix)).read_bytes()
        assert len(before) == len(after) and len(before) > 0
        item[suffix + '_identical'] = before == after
    assert 'ERROR' not in (root / (prefix + '.log')).read_text()
    proof[name] = item
assert proof['removed']['changed_pixels'] > 0
assert not proof['removed']['object-depth.bin_identical']
assert proof['undo']['changed_pixels'] == 0
assert proof['undo']['object-depth.bin_identical'] and proof['undo']['shadow.bin_identical']
output.write_text(json.dumps(proof, indent=2))
print(json.dumps(proof))
