"""Check the independent ShoreSim history, optionally compare a native execution."""
import argparse
import hashlib
import json
import struct
from pathlib import Path

import numpy as np

p = argparse.ArgumentParser()
p.add_argument('source', type=Path)
p.add_argument('repeat', type=Path)
p.add_argument('report', type=Path)
p.add_argument('--native', type=Path)
p.add_argument('--up-to', type=int, choices=[1,120,540,1080], default=1080)
a = p.parse_args()
assert not a.report.exists(), 'New report required'
m = json.loads((a.source / 'shore-sim.json').read_text())
assert m == json.loads((a.repeat / 'shore-sim.json').read_text())
assert m['source_commit'] == '1438b1abfcaee3267092b75573014f4d9b4a983c'
assert m['resolution'] == 768 and m['steps'] == 1080
assert m.get('probe_kind', 'simulation') == 'simulation', 'Diagnostic is not simulation evidence'
assert m['direction_mode'] == 'original_terrain_fallback'
assert m['deposit_schedule'] == []
for name, digest in m['files'].items():
    for root in (a.source, a.repeat):
        assert hashlib.sha256((root / name).read_bytes()).hexdigest() == digest, name
report = {'source': str(a.source), 'repeat': str(a.repeat),
          'final_frame': a.up_to,
          'identical_files': len(m['files']), 'snapshots': [],
          'native': str(a.native) if a.native else None,
          'limits': ['empty spray deposit schedule', 'fixed 60 Hz',
                     'numerical probe is not full scene visual qualification']}
for s in m['snapshots']:
    if s['frame'] > a.up_to:
        continue
    prefix = s['prefix']
    state_name = prefix + '-state.rgba16.bin'
    deposit_name = prefix + '-deposit.u32.bin'
    state = np.fromfile(a.source / state_name, dtype='<f2').astype(np.float32).reshape(768,768,4)
    assert np.isfinite(state).all()
    assert (state[:,:,:3] >= 0).all() and (state[:,:,:3] <= 1).all()
    deposits = np.fromfile(a.source / deposit_name, dtype='<u4')
    assert deposits.size == 768 * 768 and not deposits.any()
    frame = (a.source / (prefix + '-uniform.bin')).read_bytes()
    assert len(frame) == 720
    assert struct.unpack_from('<I', frame, 576)[0] == s['frame']
    assert frame[580:584] == struct.pack('<f', s['time'])
    assert frame[584:588] == struct.pack('<f', 1/60 if s['frame'] else 0)
    if s['frame'] == 0:
        assert not state.any()
    row = {'frame': s['frame'], 'time': s['time'],
           'min': state.min(axis=(0,1)).tolist(), 'max': state.max(axis=(0,1)).tolist(),
           'mean': state.mean(axis=(0,1), dtype=np.float64).tolist()}
    if a.native:
        native = np.fromfile(a.native / state_name, dtype='<f2').astype(np.float32).reshape(state.shape)
        assert np.isfinite(native).all()
        assert (native[:,:,:3] >= 0).all() and (native[:,:,:3] <= 1).all()
        assert (a.native / deposit_name).read_bytes() == (a.source / deposit_name).read_bytes()
        if s['frame'] == 0:
            assert not native.any()
        delta = np.abs(native - state)
        row['native_error'] = {'mean': delta.mean(axis=(0,1), dtype=np.float64).tolist(),
                               'max': delta.max(axis=(0,1)).tolist(),
                               'p99': np.percentile(delta, 99, axis=(0,1)).tolist(),
                               'different_texels': int(np.any(delta != 0, axis=2).sum())}
    report['snapshots'].append(row)
with a.report.open('x') as f:
    json.dump(report, f, indent=2)
print(json.dumps(report, indent=2))
