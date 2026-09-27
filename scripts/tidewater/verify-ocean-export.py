"""Validate pinned ocean acquisition integrity, finiteness and repeated execution."""
import hashlib
import json
import sys
import struct
from pathlib import Path
import numpy as np

root, repeat, output = map(Path, sys.argv[1:4])
assert len(sys.argv) == 4 and not output.exists()
a = json.loads((root / 'ocean.json').read_text())
b = json.loads((repeat / 'ocean.json').read_text())
assert a == b
assert a['source_commit'] == '1438b1abfcaee3267092b75573014f4d9b4a983c'
assert a['schema'] == 'forge.tidewater.ocean-probe' and a['version'] == 1
assert a['fft_size'] == 256 and a['cascades'] == 4
assert struct.unpack_from('<f', (root / 'params-initial.bin').read_bytes(), 212)[0] == 0
for snapshot in a['snapshots']:
    packed = (root / f'{snapshot["prefix"]}-params.bin').read_bytes()
    assert len(packed) == 224
    assert packed[212:216] == struct.pack('<f', snapshot['time']), 'aliased time snapshots'
ranges = {}
for name, digest in a['files'].items():
    assert Path(name).name == name and '/' not in name and '\\' not in name and '..' not in name
    data = (root / name).read_bytes()
    assert hashlib.sha256(data).hexdigest() == digest, name
    assert data == (repeat / name).read_bytes(), name
    if name.endswith('.rgba16.bin'):
        mip = int(name.split('-m')[1].split('.')[0])
        assert len(data) == max(1, 256 >> mip)**2 * 8, name
        values = np.frombuffer(data, dtype='<f2').astype(float)
    elif name.endswith('.f32.bin'):
        channels = 1 if '-foam.' in name else 4
        assert len(data) == 256 * 256 * 4 * channels * 4, name
        values = np.frombuffer(data, dtype='<f4').astype(float)
    else:
        continue
    assert np.isfinite(values).all(), name
    ranges[name] = dict(min=float(values.min()), max=float(values.max()),
                        nonzero=int(np.count_nonzero(values)))
for field in ('h0', 'waveData'):
    assert len({a['files'][f'{s["prefix"]}-{field}.f32.bin'] for s in a['snapshots']}) == 1
foam = []
for s in a['snapshots']:
    v = np.fromfile(root / f'{s["prefix"]}-foam.f32.bin', dtype='<f4')
    assert (v >= 0).all() and (v <= 1.5).all()
    foam.append(dict(frame=s['frame'], nonzero=int(np.count_nonzero(v)),
                     max=float(v.max()), mean=float(v.mean())))
if a.get('sea') == 'storm':
    assert all(s['nonzero'] > 0 for s in foam), 'storm must exercise persistent foam'
result = dict(status='source_acquisition_validated_not_native_parity',
              files_byte_exact=len(a['files']), finite_gpu_outputs=len(ranges),
              snapshots=a['snapshots'], foam=foam, ranges=ranges)
output.write_text(json.dumps(result, indent=2))
print(json.dumps({k:v for k,v in result.items() if k != 'ranges'}))
