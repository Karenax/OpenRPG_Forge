"""Measure complete App-history cloud outputs; GPU execution and parity stay separate."""
from pathlib import Path
import hashlib, json, sys
import numpy as np

source, native, repeat, output = map(Path, sys.argv[1:])
assert not output.exists()
meta = json.loads((source/'cloud-output.json').read_bytes())
assert meta['source_commit'] == '1438b1abfcaee3267092b75573014f4d9b4a983c'
assert meta['gpu_validation_error'] is None and meta['clocks_before'] == meta['clocks_after']
assert meta['final_view_index'] == 1
pin = 'b688075e65db9f46b2fefab8a5cd982adead6496096ce0c26b764d75a4e2b8df'
for folder in [native, repeat]:
    execution = json.loads((folder/'package-execution.json').read_bytes())
    assert execution['manifest_sha256'] == pin
    assert execution['bootstrap_frames'] == execution['completed_steps'] == execution['render_graph_submissions'] == 124
sizes = {
    'weather.rgba8.bin':(1024,1024,4,'u1'),'bounds.rgba8.bin':(64,64,4,'u1'),
    'shadow.r32.bin':(256,256,1,'<f4'),'radiance.rgba16.bin':(240,135,4,'<f2'),
    'metadata.rgba16.bin':(240,135,4,'<f2'),'panorama.rgba16.bin':(512,160,4,'<f2'),
    **{f'{kind}{index}.rgba16.bin':(960,540,4,'<f2') for kind in ['color','meta'] for index in [0,1]},
}
assert set(meta['files']) == set(sizes)
measurements = {}
sha = lambda data: hashlib.sha256(data).hexdigest()
for name,(width,height,channels,dtype) in sizes.items():
    original = (source/name).read_bytes()
    data = (native/name).read_bytes()
    repeated = (repeat/name).read_bytes()
    assert sha(original) == meta['files'][name]
    arrays = [np.frombuffer(raw,dtype=dtype).reshape(height,width,channels).astype(np.float64)
              for raw in [original,data,repeated]]
    assert all(np.isfinite(a).all() for a in arrays), name
    error = np.abs(arrays[0]-arrays[1])
    measurements[name] = {
        'source_sha256':sha(original),'native_sha256':sha(data),'repeat_sha256':sha(repeated),
        'repeat_exact':data == repeated,'source_exact':original == data,
        'changed_texels':int(np.count_nonzero(np.any(error>0,axis=2))),
        'mean_channel_error':error.mean(axis=(0,1)).tolist(),
        'max_channel_error':error.max(axis=(0,1)).tolist(),
        'p99_channel_error':np.quantile(error,.99,axis=(0,1)).tolist(),
    }
report = {'status':'MEASURED','frames':124,'source':str(source),'native':str(native),
          'measurements':measurements,'repeat_exact':all(m['repeat_exact'] for m in measurements.values()),
          'source_exact':all(m['source_exact'] for m in measurements.values()),
          'limits':['Final GPU texture comparison only; no scene receiver or Studio visual acceptance',
                    'No tolerance or whole-scene parity verdict inferred from measurements'],
          'visual_parity':False}
output.write_text(json.dumps(report,indent=2))
print(json.dumps({k:v for k,v in report.items() if k!='measurements'}))
