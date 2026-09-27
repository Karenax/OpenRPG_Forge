"""Compare native FFT history with source snapshots, including every layer/mip."""
import hashlib
import json
import sys
from pathlib import Path
import numpy as np

source, native, output = map(Path, sys.argv[1:4])
frame = int(sys.argv[4])
assert len(sys.argv) == 5 and frame in (1,60,120) and not output.exists()
meta = json.loads((source / 'ocean.json').read_text())
assert meta['source_commit'] == '1438b1abfcaee3267092b75573014f4d9b4a983c'
assert meta['schema'] == 'forge.tidewater.ocean-probe' and meta['version'] == 1
prefix = f'frame-{frame:03}'
def checked(name):
    raw = (source / name).read_bytes()
    assert hashlib.sha256(raw).hexdigest() == meta['files'][name], name
    return raw
result = dict(status='measured_fft_history_not_water_visual_parity',frame=frame,
              source=str(source),native=str(native),outputs={})
for name in ('h0','waveData','foam','displacement','derivatives'):
    for mip in (range(9) if name in ('displacement','derivatives') else [None]):
        if mip is None:
            filename = f'{name}.f32.bin'
            a_raw = checked(f'{prefix}-{filename}')
            shape = (4,256,256,1 if name=='foam' else 4)
            dtype = '<f4'
        else:
            filename = f'{name}-m{mip}.rgba16.bin'
            a_raw = b''.join(checked(f'{prefix}-{name}-c{c}-m{mip}.rgba16.bin') for c in range(4))
            shape = (4,256>>mip,256>>mip,4)
            dtype = '<f2'
        b_raw = (native / filename).read_bytes()
        assert len(a_raw) == len(b_raw), filename
        a = np.frombuffer(a_raw,dtype=dtype).astype(float).reshape(shape)
        b = np.frombuffer(b_raw,dtype=dtype).astype(float).reshape(shape)
        assert np.isfinite(a).all() and np.isfinite(b).all(), filename
        d = abs(a-b)
        result['outputs'][filename] = dict(reference_sha256=hashlib.sha256(a_raw).hexdigest(),
            native_sha256=hashlib.sha256(b_raw).hexdigest(),exact_scalars=int(np.count_nonzero(d==0)),
            total_scalars=int(d.size),max_absolute_error=float(d.max()),mean_absolute_error=float(d.mean()),
            max_per_channel=[float(d[:,:,:,i].max()) for i in range(shape[3])],
            max_per_cascade=[float(v.max()) for v in d])
        if name=='foam':
            assert (b>=0).all() and (b<=1.5).all()
            result['foam'] = dict(source_nonzero=int(np.count_nonzero(a)),native_nonzero=int(np.count_nonzero(b)),
                                 source_mean=float(a.mean()),native_mean=float(b.mean()))
output.write_text(json.dumps(result,indent=2))
print(json.dumps(dict(frame=frame,foam=result['foam'],max_errors={k:v['max_absolute_error'] for k,v in result['outputs'].items()})))
