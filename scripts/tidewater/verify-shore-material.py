"""Qualify the original simulated-shore material and scene oracle, not native parity."""
import argparse
import hashlib
import json
from pathlib import Path

import numpy as np
from PIL import Image

p = argparse.ArgumentParser()
for name in ['material', 'repeat_material', 'simulation', 'capture', 'before', 'report']:
    p.add_argument(name, type=Path)
p.add_argument('--repeat-capture', type=Path)
a = p.parse_args()
assert not a.report.exists()
digest = lambda data: hashlib.sha256(data).hexdigest()
m = json.loads((a.material / 'water-material.json').read_text())
assert m == json.loads((a.repeat_material / 'water-material.json').read_text())
assert m['source_commit'] == '1438b1abfcaee3267092b75573014f4d9b4a983c'
assert m['options']['shore_sim'] and m['options']['surf_foam'] and m['options']['shore']
for name, sha in m['files'].items():
    assert Path(name).name == name and '/' not in name and '\\' not in name
    for root in [a.material, a.repeat_material]:
        assert digest((root / name).read_bytes()) == sha
assert {'shoreSim', 'surfFoam'} <= {v['name'] for v in m['modules']}
assert {'shoreSimStateTex', 'shoreSimLaceNearest', 'surfFoamLaceTex'} <= m['bindings'].keys()
sim = json.loads((a.simulation / 'shore-sim.json').read_text())
assert sim['source_commit'] == m['source_commit']
assert sim.get('probe_kind', 'simulation') == 'simulation'
assert sim['terrain_sha256'] == m['terrain_sha256']
for n in ['shore-field.rgba32.bin'] + [f'lace-m{i}.rgba8.bin' for i in range(10)]:
    data = (a.material / n).read_bytes()
    assert digest(data) == sim['files'][n] and data == (a.simulation / n).read_bytes()
assert (a.material / 'uniform-shoreSimP.bin').read_bytes() == (a.simulation / 'simulation-params.bin').read_bytes()
meta = json.loads(Path(str(a.capture) + '.json').read_text())
assert meta['source_commit'] == m['source_commit']
assert meta['shore_sim']['frame'] == 120 and meta['shore_sim']['initial_state'] == 'zero'
assert meta['shore_sim']['deposit_schedule'] == []
assert meta['shore_sim']['terrain_wetness'] and meta['shore_sim']['surf_foam']
state = Path(str(a.capture) + '.shore-state.rgba16.bin').read_bytes()
assert digest(state) == meta['shore_sim']['state_sha256'] == sim['files']['frame-0120-state.rgba16.bin']
assert state == (a.simulation / 'frame-0120-state.rgba16.bin').read_bytes()
detail = None
if m['options']['detail']:
    assert meta['water_geometry']['surface_options']['detail']
    assert 'seaDetail' in {v['name'] for v in m['modules']}
    noise = np.frombuffer((a.material / 'sea-detail-noise.rgba16.bin').read_bytes(), dtype='<f2')
    assert noise.size == 256 * 256 * 4 and np.isfinite(noise).all()
    assert noise.min() >= 0 and noise.max() <= 1
    packed = Path(str(a.capture) + '.sea-detail.bin').read_bytes()
    assert len(packed) == 32 and digest(packed) == meta['sea_detail']['uniform_sha256']
    assert meta['sea_detail']['steps'] == 120 and meta['sea_detail']['dt'] == 1 / 60
    initial = (a.material / 'uniform-seaDetailParams.bin').read_bytes()
    assert initial[:8] == bytes(8) and packed[8:] == initial[8:]
    frame = np.frombuffer(Path(str(a.capture) + '.frame.bin').read_bytes(), dtype='<f4')
    assert frame.size == 180 and frame[145] == 2
    drift = np.asarray(frame[172:174], dtype=np.float64) * float(frame[167]) * 0.7 * 2
    actual = np.frombuffer(packed, dtype='<f4')[:2]
    assert np.array_equal(actual, drift.astype('<f4'))
    detail = dict(noise_sha256=m['files']['sea-detail-noise.rgba16.bin'],
                  paired_uniform_sha256=digest(packed), drift_metres=actual.tolist(),
                  noise_shape=[256, 256, 4], mip_levels=1)
x = np.asarray(Image.open(a.before).convert('RGB'), dtype=np.int16)
y = np.asarray(Image.open(a.capture).convert('RGB'), dtype=np.int16)
assert x.shape == y.shape == (1080, 1920, 3)
d = np.abs(x - y)
assert (d.max(2) > 3).any(), 'Effect has no visible footprint'
if a.repeat_capture:
    for suffix in ['', '.frame.bin', '.shore-state.rgba16.bin'] + (['.sea-detail.bin'] if detail else []):
        assert Path(str(a.capture) + suffix).read_bytes() == Path(str(a.repeat_capture) + suffix).read_bytes()
report = dict(material_files=len(m['files']), modules=len(m['modules']), source_commit=m['source_commit'],
              sea_detail=detail,
              capture=str(a.capture), capture_sha256=digest(a.capture.read_bytes()),
              state_sha256=digest(state), state_matches_standalone_frame120=True,
              repeat_capture_exact=bool(a.repeat_capture),
              visibly_changed_pixels_gt3=int((d.max(2) > 3).sum()), mean_effect_rgb=float(d.mean()),
              maximum_effect_rgb=int(d.max()),
              limits=['reference acquisition only', 'no native simulated-shore render verdict', 'single frame at two seconds'])
with a.report.open('x') as f:
    json.dump(report, f, indent=2)
print(json.dumps(report, indent=2))
