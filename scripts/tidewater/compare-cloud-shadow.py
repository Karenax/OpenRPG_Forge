"""Compare native 120-step cloud shadow with the original quarter-row history."""
from pathlib import Path
import hashlib, json, sys
import numpy as np

assert len(sys.argv) == 5
source, native, repeat, output = map(Path, sys.argv[1:])
assert not output.exists()
meta = json.loads((source / 'environment.json').read_text())
probe = json.loads((native / 'probe.json').read_text())
assert probe['native_shadow'] and not probe.get('source_cloud_results_uploaded',probe['source_results_uploaded'])
assert probe['source_manifest_sha256'] == hashlib.sha256((source / 'environment.json').read_bytes()).hexdigest()
data = (source / 'cloud-step120-shadow.bin').read_bytes()
assert hashlib.sha256(data).hexdigest() == meta['files']['cloud-step120-shadow.bin']
candidate = (native / 'shadow.r32.bin').read_bytes()
assert candidate == (repeat / 'shadow.r32.bin').read_bytes()
a, b = [np.frombuffer(value, dtype='<f4').reshape(256, 256) for value in [data, candidate]]
assert np.isfinite(a).all() and np.isfinite(b).all()
assert (a >= 0).all() and (a <= 1).all() and (b >= 0).all() and (b <= 1).all()
assert (a < .9).any() and (b < .9).any()
error = np.abs(a.astype('float64') - b.astype('float64'))
report = {'source': str(source), 'native': str(native), 'repeat_exact': True,
          'different_texels': int(np.count_nonzero(error)), 'mean_absolute_error': float(error.mean()),
          'max_absolute_error': float(error.max()), 'p99_absolute_error': float(np.quantile(error, .99)),
          'native_sha256': hashlib.sha256(candidate).hexdigest(),
          'limits': ['120 acquired uniform steps', 'native noise sampling and weather; no source shadow upload',
                     'fixed camera and original quarter-row updates', 'no scene receiver or full cloud parity']}
output.write_text(json.dumps(report, indent=2))
print(json.dumps(report, indent=2))
