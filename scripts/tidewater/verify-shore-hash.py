"""Compare losslessly encoded binary32 shore arithmetic, separately from simulation."""
import argparse
import hashlib
import json
from pathlib import Path

import numpy as np

p = argparse.ArgumentParser()
p.add_argument('source', type=Path)
p.add_argument('native', type=Path)
p.add_argument('report', type=Path)
p.add_argument('--require-exact-hash', action='store_true')
a = p.parse_args()
assert not a.report.exists()
meta = json.loads((a.source / 'shore-sim.json').read_text())
assert meta['probe_kind'] == 'hash_diagnostic'
assert meta['source_commit'] == '1438b1abfcaee3267092b75573014f4d9b4a983c'
filename = 'frame-0001-state.rgba16.bin'
assert hashlib.sha256((a.source / filename).read_bytes()).hexdigest() == meta['files'][filename]

def decode(root):
    data = np.fromfile(root / filename, dtype='<f2').reshape(96, 8, 768, 4)
    assert np.isfinite(data).all() and (data >= 0).all() and (data <= 255).all()
    assert (data == np.floor(data)).all()
    return data.astype('u1').copy().view('<f4').reshape(96, 8, 768).transpose(1, 0, 2).reshape(8, -1)

source, native = decode(a.source), decode(a.native)
assert np.isfinite(source).all() and np.isfinite(native).all()
assert np.array_equal(source[0], native[0])
names = ['wave_number', 'angle', 'sin', 'product', 'hash', 'explicit_fma_angle', 'sin_fma', 'multiply']
rows = []
for i, name in enumerate(names):
    delta = np.abs(source[i].astype('f8') - native[i].astype('f8'))
    rows.append(dict(channel=name, different=int(np.count_nonzero(delta)),
                     mean=float(delta.mean()), maximum=float(delta.max())))
report = dict(source=str(a.source), native=str(a.native), samples=source.shape[1],
              unique_wave_numbers=int(np.unique(source[0]).size),
              minimum_wave_number=float(source[0].min()), maximum_wave_number=float(source[0].max()),
              channels=rows, exact_hash=rows[4]['different'] == 0,
              limits=['current GPU and compiler only', 'diagnostic is not full simulation or scene parity'])
with a.report.open('x') as f:
    json.dump(report, f, indent=2)
print(json.dumps(report, indent=2))
if a.require_exact_hash:
    assert report['exact_hash'], 'Native shore hash still differs'
