"""Measure native weather error and independently check conservative repeat bounds."""
from pathlib import Path
import hashlib, json, sys
import numpy as np

assert len(sys.argv) == 5
source, native, repeat, output = map(Path, sys.argv[1:])
assert not output.exists()
meta = json.loads((source / 'environment.json').read_text())
probe = json.loads((native / 'probe.json').read_text())
assert probe.get('source_cloud_results_uploaded',probe['source_results_uploaded']) is False
assert probe['source_manifest_sha256'] == hashlib.sha256((source / 'environment.json').read_bytes()).hexdigest()
report = {'native': str(native), 'source': str(source), 'measurements': {}}
arrays = {}
for name, size in [('weather', 1024), ('bounds', 64)]:
    filename = f'cloud-step120-{name}.bin'
    data = (source / filename).read_bytes()
    assert hashlib.sha256(data).hexdigest() == meta['files'][filename]
    candidate = (native / f'{name}.rgba8.bin').read_bytes()
    assert candidate == (repeat / f'{name}.rgba8.bin').read_bytes()
    a = np.frombuffer(data, dtype='u1').reshape(size, size, 4)
    b = np.frombuffer(candidate, dtype='u1').reshape(size, size, 4)
    arrays[name] = b
    delta = np.abs(a.astype('int16') - b.astype('int16'))
    report['measurements'][name] = {'different_texels': int(np.any(delta != 0, axis=2).sum()),
        'max_channel_error': int(delta.max()), 'mean_channel_error': float(delta.mean()),
        'native_sha256': hashlib.sha256(candidate).hexdigest()}
for y in range(64):
    for x in range(64):
        xs = np.arange(x * 16 - 1, x * 16 + 17) % 1024
        ys = np.arange(y * 16 - 1, y * 16 + 17) % 1024
        expected = min(255, int(arrays['weather'][np.ix_(ys, xs)][..., 0].max()) + 1)
        assert int(arrays['bounds'][y, x, 0]) == expected, (x, y)
report['conservative_bounds_verified'] = 4096
report['limits'] = ['two source compute kernels on Bevy device', 'fixed source settings',
                    'no volumetric march, temporal resolve, shadows or final scene parity']
output.write_text(json.dumps(report, indent=2))
print(json.dumps(report, indent=2))
