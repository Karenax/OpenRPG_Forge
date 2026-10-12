"""Compare three native standard-depth layers against the source GPU oracle."""
import hashlib
import json
import sys
from pathlib import Path
import numpy as np

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from render_verification import standard_depth_diagnostics

oracle_path, native_path, output = map(Path, sys.argv[1:])
if output.exists():
    raise SystemExit('Refusing to overwrite comparison')
oracle = json.loads(oracle_path.read_text())
assert oracle['schema'] == 'forge.tidewater.object-shadow-oracle' and oracle['version'] == 1
assert oracle['source_commit'] == '1438b1abfcaee3267092b75573014f4d9b4a983c'
assert oracle['map_size'] == 2048 and len(oracle['files']) == 3
raw = native_path.read_bytes()
assert len(raw) == 3 * 2048 * 2048 * 4
native = np.frombuffer(raw, dtype='<f4').reshape(3, 2048, 2048)
assert np.isfinite(native).all() and native.min() >= 0 and native.max() <= 1
layers = []
for i in range(3):
    entry = oracle['files'][str(i)]
    source_bytes = Path(entry['file']).read_bytes()
    assert hashlib.sha256(source_bytes).hexdigest() == entry['sha256']
    source = np.frombuffer(source_bytes, dtype='<f4').reshape(2048, 2048)
    assert np.isfinite(source).all() and source.min() >= 0 and source.max() <= 1
    a, b = source < 1, native[i] < 1
    union, intersection = a | b, a & b
    error = abs(source.astype(np.float64) - native[i])
    diagnostics = standard_depth_diagnostics(source, native[i])
    layers.append({'layer': i, 'source_occupied': int(a.sum()), 'native_occupied': int(b.sum()),
                   'coverage_disagreement': int((a ^ b).sum()),
                   'intersection_over_union': diagnostics['intersection_over_union'],
                   'quality_diagnostics': diagnostics,
                   'unequal_depth_texels': int((error > 0).sum()),
                   'mean_absolute_error_on_union': float(error[union].mean()) if union.any() else 0.,
                   'max_absolute_error': float(error.max()),
                   'max_error_on_intersection': float(error[intersection].max()) if intersection.any() else 0.,
                   'source_sha256': entry['sha256'],
                   'native_sha256': hashlib.sha256(native[i].tobytes()).hexdigest()})
report = {'schema': 'forge.tidewater.native-object-depth-comparison', 'version': 1,
          'status': 'measured_depth_only_not_complete_shadow_parity',
          'oracle': str(oracle_path.resolve()), 'native': str(native_path.resolve()),
          'native_sha256': hashlib.sha256(raw).hexdigest(), 'layers': layers,
          'casters': oracle['casters'],
          'limits': ['fixed_camera_and_time', 'only_listed_casters', 'no_shadow_filter_or_scene_verdict']}
output.write_text(json.dumps(report, indent=2))
print(json.dumps({'native_sha256': report['native_sha256'], 'layers': layers}))
