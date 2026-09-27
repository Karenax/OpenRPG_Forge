"""Verify original cloud acquisition, repeatability, and environment regression."""
from pathlib import Path
import hashlib, json, sys
import numpy as np

assert len(sys.argv) == 5
source, repeat, baseline, output = map(Path, sys.argv[1:])
assert not output.exists()
hash_bytes = lambda data: hashlib.sha256(data).hexdigest()
meta = json.loads((source / 'environment.json').read_text())
other = json.loads((repeat / 'environment.json').read_text())
old = json.loads((baseline / 'environment.json').read_text())
assert meta['source_commit'] == other['source_commit'] == old['source_commit'] == '1438b1abfcaee3267092b75573014f4d9b4a983c'
assert meta['reference_sha256'] == other['reference_sha256'] == old['reference_sha256']
assert meta['files'] == other['files']
for root, manifest in [(source, meta), (repeat, other), (baseline, old)]:
    for name, expected in manifest['files'].items():
        assert Path(name).name == name
        assert hash_bytes((root / name).read_bytes()) == expected, (root, name)
for name, expected in old['files'].items():
    assert meta['files'][name] == expected, name
cloud = json.loads((source / 'clouds.json').read_text())
assert cloud['implementation'] == 'SkyProClouds' and len(cloud['kernels']) == 6
assert len(cloud['frames']) == 120
assert len(cloud['noise']['mips']) == 7
assert cloud['noise']['dimensions'] == [64, 64, 64]
assert len((source / 'cloud-blueNoise.bin').read_bytes()) == 4096
for mip in cloud['noise']['mips']:
    assert len((source / mip['file']).read_bytes()) == int(np.prod(mip['size'])) * 4
for index, frame in enumerate(cloud['frames']):
    assert frame['step'] == index + 1 and frame['view'] == index % 2
    uniforms = np.fromfile(source / frame['uniforms']['frame'], dtype='<f4').reshape(-1, 4)
    assert uniforms.shape == (34, 4) and np.isfinite(uniforms).all()
    assert uniforms[10, 0] == index
    assert abs(float(uniforms[10, 1]) - (index + 1) / 60) < 1e-6
    assert uniforms[9, 2] == 4 and uniforms[9, 3] == (index > 0)
assert [s['step'] for s in cloud['snapshots']] == [1, 120]
statistics = {}
for snapshot in cloud['snapshots']:
    for name, texture in snapshot['textures'].items():
        dtype = {'rgba8unorm': 'u1', 'rgba16float': '<f2', 'r32float': '<f4'}[texture['format']]
        channels = 1 if texture['format'] == 'r32float' else 4
        values = np.fromfile(source / texture['file'], dtype=dtype)
        assert values.size == texture['width'] * texture['height'] * channels
        assert np.isfinite(values).all(), texture['file']
        statistics[f"{snapshot['step']}-{name}"] = {'min': float(values.min()), 'max': float(values.max()),
                                                   'nonzero': int(np.count_nonzero(values))}
assert meta['files']['cloud-step001-weather.bin'] == meta['files']['cloud-step120-weather.bin']
assert meta['files']['cloud-step001-bounds.bin'] == meta['files']['cloud-step120-bounds.bin']
assert meta['files']['cloud-step001-panorama.bin'] != meta['files']['cloud-step120-panorama.bin']
report = {'source': str(source), 'repeat': str(repeat), 'exact_payloads': len(meta['files']),
          'unchanged_environment_payloads': len(old['files']), 'steps': 120, 'statistics': statistics,
          'limits': ['source GPU acquisition', 'fixed camera and cold history', 'not native cloud parity']}
output.write_text(json.dumps(report, indent=2))
print(json.dumps({k: v for k, v in report.items() if k != 'statistics'}, indent=2))
