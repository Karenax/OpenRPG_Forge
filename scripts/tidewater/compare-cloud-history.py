"""Compare native zero-initialized temporal cloud history, without imported history."""
from pathlib import Path
import hashlib,json,sys
import numpy as np
assert len(sys.argv)==5
source,native,repeat,output=map(Path,sys.argv[1:]);assert not output.exists()
meta=json.loads((source/'environment.json').read_text())
probe=json.loads((native/'probe.json').read_text())
assert probe['native_history_steps']==120 and not probe['source_cloud_results_uploaded']
native_atmosphere=probe.get('native_atmosphere',False)
assert probe['source_atmosphere_luts_uploaded'] is (not native_atmosphere)
assert probe['source_results_uploaded'] is (not native_atmosphere)
assert probe['source_manifest_sha256']==hashlib.sha256((source/'environment.json').read_bytes()).hexdigest()
measurements={}
for name in ['color0','meta0','color1','meta1']:
    filename=f'cloud-step120-{name}.bin';original=(source/filename).read_bytes()
    assert hashlib.sha256(original).hexdigest()==meta['files'][filename]
    data=(native/f'{name}.rgba16.bin').read_bytes()
    assert data==(repeat/f'{name}.rgba16.bin').read_bytes()
    a,b=[np.frombuffer(d,dtype='<f2').reshape(540,960,4).astype('float64') for d in [original,data]]
    assert np.isfinite(a).all() and np.isfinite(b).all()
    error=np.abs(a-b)
    measurements[name]={'different_texels':int(np.any(error>0,axis=2).sum()),
        'mean_channel_error':error.mean(axis=(0,1)).tolist(),
        'max_channel_error':error.max(axis=(0,1)).tolist(),
        'p99_channel_error':np.quantile(error,.99,axis=(0,1)).tolist(),
        'native_sha256':hashlib.sha256(data).hexdigest()}
report={'source':str(source),'native':str(native),'repeat_exact':True,'native_atmosphere':native_atmosphere,'measurements':measurements,
        'limits':['120 acquired uniform steps; atmosphere mode explicitly recorded','native march plus temporal history from zero',
                  'panorama not measured by this report; no live controller, scene or performance qualification']}
output.write_text(json.dumps(report,indent=2));print(json.dumps(report,indent=2))
