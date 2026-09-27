"""Render a saved workspace without fixture placements; reject missing/corrupt sources."""
import hashlib
import json
from pathlib import Path
import shutil
import subprocess
import sys

import numpy as np
from PIL import Image

root, executable, output = map(Path, sys.argv[1:4])
output.mkdir()
fixture = root / (sys.argv[4] if len(sys.argv) == 5 else 'workspace-vegetation-scene-001')
workspace = root / 'workspace-adoption-002'
area = 'e620dd19-ead9-5913-a6d6-0f93109148c0'
files = json.loads((fixture / 'fixture.json').read_text())['files']
assert not any(name.startswith('canonical-') for name in files)
scene = json.loads((fixture / 'vegetation-scene.json').read_text())
for group in scene['groups']:
    for name in group['streams'].values():
        assert not (fixture / name).exists()

def snapshot():
    return {str(p.relative_to(workspace)): hashlib.sha256(p.read_bytes()).hexdigest()
            for p in (workspace / 'workspace-v1').rglob('*') if p.is_file()}

before = snapshot()

def run(name, location, selected_area):
    image = output / f'{name}.png'
    args = [str(executable), str(fixture), str(image), '--water-spray-clouds']
    args += [str(root / d) for d in ['ocean-native-inputs-001', 'shore-native-inputs-001',
                                   'spray-native-budget-inputs-001', 'cloud-controller-inputs-001']]
    args += ['--workspace-vegetation', str(location), selected_area]
    result = subprocess.run(args, capture_output=True, text=True, encoding='utf-8', errors='replace', timeout=120)
    log = result.stdout + result.stderr
    (output / f'{name}.log').write_text(log, encoding='utf-8')
    return result.returncode, log, image

code, log, image = run('saved-clearing', workspace, area)
assert code == 0, log
assert log.count('visible=64868, excluded=515, density_removed=0') == 2
baseline = np.array(Image.open(root / 'native-authored-clearing-002.png').convert('RGBA'))
pixels = np.array(Image.open(image).convert('RGBA'))
delta = np.abs(pixels.astype(np.int16) - baseline.astype(np.int16))
assert not delta.any(), 'Workspace rendering differs from identical archived fixture'
code, log, image = run('missing-area', workspace, '00000000-0000-0000-0000-000000000001')
assert code != 0 and 'area absent from saved workspace' in log and not image.exists()
corrupt = output / 'corrupt-workspace'
shutil.copytree(workspace / 'workspace-v1', corrupt / 'workspace-v1')
blob = next((corrupt / 'workspace-v1/source-blobs').glob('*.bin'))
with blob.open('r+b') as stream:
    first = stream.read(1)
    stream.seek(0)
    stream.write(bytes([first[0] ^ 1]))
code, log, image = run('corrupt-blob', corrupt, area)
assert code != 0 and 'hash' in log.lower() and not image.exists(), log
assert before == snapshot(), 'Source workspace authority changed'
report = {'status': 'pass', 'workspace_unchanged': True,
          'fixture_height_blob_absent': not (fixture / 'heights.bin').exists(),
          'fixture_instance_streams_absent': True, 'canonical_fixture_inputs_absent': True,
          'visible': 64868, 'excluded': 515, 'changed_pixels': int(np.any(delta, axis=2).sum()),
          'missing_area_rejected': True, 'corrupt_blob_rejected': True,
          'binary_sha256': hashlib.sha256(executable.read_bytes()).hexdigest(),
          'scope': 'Saved workspace native load; no Studio interaction or hot reload qualification'}
(output / 'report.json').write_text(json.dumps(report, indent=2))
print(json.dumps(report, indent=2))
