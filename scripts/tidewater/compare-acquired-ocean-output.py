"""Measure resident FFT texture differences without inferring visual acceptance."""
from pathlib import Path
import argparse
import hashlib
import json
import numpy as np

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('reference', type=Path)
parser.add_argument('native', type=Path)
parser.add_argument('repeat', type=Path)
parser.add_argument('output', type=Path)
args = parser.parse_args()
assert not args.output.exists(), 'Preserve previous measurements'
sha = lambda p: hashlib.sha256(p.read_bytes()).hexdigest()
metadata = args.reference / 'ocean-output.json'
assert sha(metadata) == '1cd92d916f4574e57ab83fc9d1c52a721be0c53bc02f5a1c98aa09d38ee9de85'
source = json.loads(metadata.read_bytes())
assert source['source_commit'] == '1438b1abfcaee3267092b75573014f4d9b4a983c'
assert source['clocks_before'] == source['clocks_after']
assert source['gpu_validation_error'] is None
package_pin = 'b54f26065b796c6e4731a415048739263c3416ab8bdd3a364927c26961f1656e'
final_params = args.reference / 'ocean-step124-params.bin'
assert sha(final_params) == source['files'][final_params.name]
source_time = np.frombuffer(final_params.read_bytes(), dtype='<f4')[53]
clocks = []
for root in [args.native, args.repeat]:
    execution = json.loads((root / 'execution.json').read_bytes())
    assert execution['manifest_sha256'] == package_pin
    assert execution['bootstrap_frames'] == execution['completed_steps'] == 124
    assert execution['live_paused'] and execution['outputs'] == 75
    assert execution['texture_outputs'] == 72 and execution['buffer_outputs'] == 3
    completed = execution['completed_frame']
    assert completed['step'] == 124
    assert np.float32(completed['time']) == source_time
    assert np.float32(completed['dt']) == np.float32(1.0 / 60.0)
    clocks.append(completed)
results = {}
for kind in ['displacement', 'derivatives']:
    for layer in range(4):
        for mip in range(9):
            name = f'{kind}-c{layer}-m{mip}.rgba16.bin'
            paths = [root / name for root in [args.reference, args.native, args.repeat]]
            assert sha(paths[0]) == source['files'][name], name
            arrays = [np.frombuffer(p.read_bytes(), dtype='<f2').astype(np.float64).reshape(-1, 4) for p in paths]
            assert all(a.shape == ((256 >> mip) ** 2, 4) and np.isfinite(a).all() for a in arrays), name
            delta = np.abs(arrays[1] - arrays[0])
            results[name] = {
                'source_sha256': sha(paths[0]), 'native_sha256': sha(paths[1]),
                'repeat_sha256': sha(paths[2]), 'source_exact': sha(paths[0]) == sha(paths[1]),
                'repeat_exact': sha(paths[1]) == sha(paths[2]),
                'changed_texels': int(np.any(delta != 0, axis=1).sum()),
                'mean_abs_per_channel': delta.mean(axis=0).tolist(),
                'max_abs_per_channel': delta.max(axis=0).tolist(),
                'p99_abs_per_channel': np.quantile(delta, .99, axis=0).tolist(),
            }
for name, channels in [('h0.f32.bin', 4), ('waveData.f32.bin', 4), ('foam.f32.bin', 1)]:
    paths = [root / name for root in [args.reference, args.native, args.repeat]]
    assert sha(paths[0]) == source['files'][name], name
    arrays = [np.frombuffer(p.read_bytes(), dtype='<f4').astype(np.float64).reshape(-1, channels) for p in paths]
    assert all(a.shape == (256 * 256 * 4, channels) and np.isfinite(a).all() for a in arrays), name
    delta = np.abs(arrays[1] - arrays[0])
    results[name] = {
        'source_sha256': sha(paths[0]), 'native_sha256': sha(paths[1]),
        'repeat_sha256': sha(paths[2]), 'source_exact': sha(paths[0]) == sha(paths[1]),
        'repeat_exact': sha(paths[1]) == sha(paths[2]),
        'changed_records': int(np.any(delta != 0, axis=1).sum()),
        'mean_abs_per_channel': delta.mean(axis=0).tolist(),
        'max_abs_per_channel': delta.max(axis=0).tolist(),
        'p99_abs_per_channel': np.quantile(delta, .99, axis=0).tolist(),
    }
report = {'status': 'MEASURED', 'files': results,
          'all_source_exact': all(r['source_exact'] for r in results.values()),
          'all_repeat_exact': all(r['repeat_exact'] for r in results.values()),
          'source_metadata_sha256': sha(metadata), 'package_sha256': package_pin,
          'dependent_material_clocks': clocks,
          'scope': '72 texture layers/mips and three raw h0/waveData/foam buffers',
          'visual_parity_qualified': False}
args.output.write_text(json.dumps(report, indent=2))
print(json.dumps({k: v for k, v in report.items() if k != 'files'}))
