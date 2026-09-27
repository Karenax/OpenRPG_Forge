"""Native saved relief and undo captures; independent source membership evidence."""
from pathlib import Path
import hashlib
import json
import subprocess
import sys
import numpy as np
from PIL import Image

root, binary, output = map(Path, sys.argv[1:])
output.mkdir()
report = {'binary_sha256': hashlib.sha256(binary.read_bytes()).hexdigest()}
baseline = np.array(Image.open(root/'native-canonical-vegetation-001.png').convert('RGBA'))
for state in ['raised', 'undo']:
    image = output/f'{state}.png'
    args = [str(binary), str(root/'workspace-terrain-scene-001'), str(image), '--water-spray-clouds']
    args += [str(root/p) for p in ['ocean-native-inputs-001','shore-native-inputs-001',
                                'spray-native-budget-inputs-001','cloud-controller-inputs-001']]
    args += ['--workspace-vegetation', str(root/f'workspace-relief-{state}-001'),
             'e620dd19-ead9-5913-a6d6-0f93109148c0']
    result = subprocess.run(args, capture_output=True, text=True, encoding='utf-8', errors='replace', timeout=150)
    log = result.stdout+result.stderr
    (output/f'{state}.log').write_text(log, encoding='utf-8')
    assert result.returncode == 0, log
    moved = 203 if state == 'raised' else 0
    assert log.count(f'canonical ground projection: moved={moved}') == 2, log
    pixels = np.array(Image.open(image).convert('RGBA'))
    delta = np.abs(pixels.astype(np.int16)-baseline.astype(np.int16))
    y,x = np.where(np.any(delta,axis=2))
    report[state] = {'sha256':hashlib.sha256(image.read_bytes()).hexdigest(),
        'moved_plants':moved,'changed_pixels':len(x),'max_channel_delta':int(delta.max()),
        'mean_channel_delta':float(delta.mean()),
        'bbox':[int(x.min()),int(y.min()),int(x.max()),int(y.max())] if len(x) else None}
(output/'report.json').write_text(json.dumps(report,indent=2))
print(json.dumps(report,indent=2))
assert report['raised']['changed_pixels'] > 0
assert report['undo']['changed_pixels'] == 0
