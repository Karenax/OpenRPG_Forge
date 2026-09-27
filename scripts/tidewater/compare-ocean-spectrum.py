"""Measure initial spectrum differences without claiming animated water parity."""
import hashlib
import json
import sys
from pathlib import Path
import numpy as np

source, native, output = map(Path, sys.argv[1:4])
assert len(sys.argv) == 4 and not output.exists()
meta = json.loads((source / 'ocean.json').read_text())
result = {'status':'measured_initial_spectrum_not_animated_ocean_parity', 'buffers':{}}
for name in ('h0','waveData'):
    ref_name = f'frame-001-{name}.f32.bin'
    a_bytes = (source / ref_name).read_bytes()
    b_bytes = (native / f'{name}.f32.bin').read_bytes()
    assert hashlib.sha256(a_bytes).hexdigest() == meta['files'][ref_name]
    assert len(a_bytes) == len(b_bytes) == 256*256*4*16
    a = np.frombuffer(a_bytes,dtype='<f4').astype(float).reshape(4,256,256,4)
    b = np.frombuffer(b_bytes,dtype='<f4').astype(float).reshape(4,256,256,4)
    assert np.isfinite(a).all() and np.isfinite(b).all(), name
    d = abs(a-b)
    result['buffers'][name] = dict(reference_sha256=hashlib.sha256(a_bytes).hexdigest(),
        native_sha256=hashlib.sha256(b_bytes).hexdigest(),
        exact_scalars=int(np.count_nonzero(d==0)), total_scalars=int(d.size),
        max_absolute_error=float(d.max()), mean_absolute_error=float(d.mean()),
        max_per_cascade=[float(v.max()) for v in d],
        reference_max_abs=float(abs(a).max()))
output.write_text(json.dumps(result,indent=2))
print(json.dumps(result))
