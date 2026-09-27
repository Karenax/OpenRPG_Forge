"""Compare original and native cloud panoramas after the same lattice updates."""
from pathlib import Path
import hashlib,json,sys
import numpy as np
assert len(sys.argv)==5
source,native,repeat,output=map(Path,sys.argv[1:]);assert not output.exists()
meta=json.loads((source/'environment.json').read_text())
probe=json.loads((native/'probe.json').read_text())
assert probe['native_panorama_steps']==120 and not probe['source_cloud_results_uploaded']
native_atmosphere=probe.get('native_atmosphere',False)
assert probe['source_atmosphere_luts_uploaded'] is (not native_atmosphere)
assert probe['source_results_uploaded'] is (not native_atmosphere)
assert probe['source_manifest_sha256']==hashlib.sha256((source/'environment.json').read_bytes()).hexdigest()
original=(source/'cloud-step120-panorama.bin').read_bytes()
assert hashlib.sha256(original).hexdigest()==meta['files']['cloud-step120-panorama.bin']
data=(native/'panorama.rgba16.bin').read_bytes()
assert data==(repeat/'panorama.rgba16.bin').read_bytes()
a,b=[np.frombuffer(d,dtype='<f2').reshape(160,512,4).astype('float64') for d in [original,data]]
assert np.isfinite(a).all() and np.isfinite(b).all()
for values in [a,b]:
    assert (values[...,3]>=0).all() and (values[...,3]<=1).all()
    assert (values[...,3]<.9).any() and (values[...,3]>.99).any()
error=np.abs(a-b)
report={'source':str(source),'native':str(native),'repeat_exact':True,'native_atmosphere':native_atmosphere,
        'different_texels':int(np.any(error>0,axis=2).sum()),
        'mean_channel_error':error.mean(axis=(0,1)).tolist(),
        'max_channel_error':error.max(axis=(0,1)).tolist(),
        'p99_channel_error':np.quantile(error,.99,axis=(0,1)).tolist(),
        'native_sha256':hashlib.sha256(data).hexdigest(),
        'limits':['120 acquired uniform steps; atmosphere mode explicitly recorded','native panorama from zero',
                  'no scene reflection/composition or interactive performance qualification']}
output.write_text(json.dumps(report,indent=2));print(json.dumps(report,indent=2))
