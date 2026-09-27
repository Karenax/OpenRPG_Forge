"""Compare isolated native cloud marching against paired source atmosphere."""
from pathlib import Path
import hashlib,json,sys
import numpy as np

assert len(sys.argv)==5
source,native,repeat,output=map(Path,sys.argv[1:])
assert not output.exists()
meta=json.loads((source/'environment.json').read_text())
probe=json.loads((native/'probe.json').read_text())
native_atmosphere=probe.get('native_atmosphere',False)
assert probe['native_trace'] and probe['source_atmosphere_luts_uploaded'] is (not native_atmosphere)
assert probe['source_results_uploaded'] is (not native_atmosphere)
assert not probe['source_cloud_results_uploaded']
assert probe['source_manifest_sha256']==hashlib.sha256((source/'environment.json').read_bytes()).hexdigest()
measurements={}
for name in ['radiance','metadata']:
    filename=f'cloud-step120-{name}.bin'
    original=(source/filename).read_bytes()
    assert hashlib.sha256(original).hexdigest()==meta['files'][filename]
    candidate=(native/f'{name}.rgba16.bin').read_bytes()
    assert candidate==(repeat/f'{name}.rgba16.bin').read_bytes()
    a,b=[np.frombuffer(data,dtype='<f2').reshape(135,240,4).astype('float64') for data in [original,candidate]]
    assert np.isfinite(a).all() and np.isfinite(b).all()
    delta=np.abs(a-b)
    measurements[name]={'different_texels':int(np.any(delta>0,axis=2).sum()),
        'mean_channel_error':delta.mean(axis=(0,1)).tolist(),
        'max_channel_error':delta.max(axis=(0,1)).tolist(),
        'p99_channel_error':np.quantile(delta,.99,axis=(0,1)).tolist(),
        'source_range':[a.min(axis=(0,1)).tolist(),a.max(axis=(0,1)).tolist()],
        'native_sha256':hashlib.sha256(candidate).hexdigest()}
    if name=='radiance':
        assert (a[...,3]>.1).any() and (b[...,3]>.1).any()
        assert (a[...,3]>=0).all() and (a[...,3]<=1).all()
        assert (b[...,3]>=0).all() and (b[...,3]<=1).all()
report={'source':str(source),'native':str(native),'repeat_exact':True,'native_atmosphere':native_atmosphere,'measurements':measurements,
        'limits':['frame 120 only at fixed village camera','acquired uniforms; atmosphere mode explicitly recorded',
                  'native weather and volume/noise sampling','no temporal reconstruction or final scene parity']}
output.write_text(json.dumps(report,indent=2))
print(json.dumps(report,indent=2))
