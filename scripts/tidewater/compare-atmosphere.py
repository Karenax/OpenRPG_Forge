"""Compare native atmosphere calculations with the pinned source, without a parity gate."""
from pathlib import Path
import hashlib
import json
import sys
import numpy as np

source, native, output = map(Path, sys.argv[1:4])
assert not output.exists()
meta = json.loads((source/'atmosphere.json').read_text())
assert meta['source_commit'] == '1438b1abfcaee3267092b75573014f4d9b4a983c'
rows = []
for name, size, dtype in [('transmittance.rgba16.bin',256*64*4,'<f2'),
                          ('multiScat.rgba16.bin',32*32*4,'<f2'),
                          ('skyView.rgba16.bin',192*108*4,'<f2'),
                          ('irradiance.bin',16,'<f4')]:
    ref, candidate = (source/name).read_bytes(), (native/name).read_bytes()
    assert hashlib.sha256(ref).hexdigest() == meta['files'][name]
    a, b = [np.frombuffer(v,dtype=dtype).astype(np.float64) for v in (ref,candidate)]
    assert a.size == b.size == size and np.isfinite(a).all() and np.isfinite(b).all()
    delta = np.abs(a-b)
    row = {'name':name, 'scalar_count':size, 'different_scalars':int((a!=b).sum()),
           'max_absolute_error':float(delta.max()),'mean_absolute_error':float(delta.mean()),
           'p99_absolute_error':float(np.percentile(delta,99)),
           'reference_sha256':hashlib.sha256(ref).hexdigest(),
           'native_sha256':hashlib.sha256(candidate).hexdigest()}
    if name == 'irradiance.bin':
        row['reference_values'] = a.tolist()
        row['native_values'] = b.tolist()
    rows.append(row)
result = {'schema':'forge.tidewater.atmosphere-comparison','version':1,
          'source':str(source.resolve()), 'native':str(native.resolve()), 'results':rows,
          'status':'measured_not_full_atmosphere_or_scene_parity',
          'limits':['fixed_daytime_parameters','no_clouds_sky_composition_or_temporal_verdict']}
output.write_text(json.dumps(result,indent=2))
print(json.dumps(result))
