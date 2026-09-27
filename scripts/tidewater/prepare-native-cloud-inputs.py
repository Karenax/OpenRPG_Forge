"""Materialize only the inputs read by the native-atmosphere cloud probe."""
from pathlib import Path
import hashlib,json,sys
assert len(sys.argv) in [5,6]
native_controller=len(sys.argv)==6
assert not native_controller or sys.argv[5]=="--native-controller"
cloud,atmosphere,out_cloud,out_atmosphere=map(Path,sys.argv[1:5])
assert not out_cloud.exists() and not out_atmosphere.exists() and out_cloud!=out_atmosphere
names=[f'cloud-{name}.wgsl' for name in ['weather','bounds','shadow','trace','temporal','pano']]
names += [f'cloud-volume-m{level}.rgba8.bin' for level in range(7)]+['cloud-blueNoise.bin']
names += [f'cloud-step{step:03}-{name}.bin' for step in range(1,121) for name in (['global','atmosphere'] if native_controller else ['frame','global','settings','atmosphere'])]
for source,output,manifest,files in [(cloud,out_cloud,'environment.json',names),
    (atmosphere,out_atmosphere,'atmosphere.json',['params.bin','transmittance.wgsl','multiScat.wgsl','skyView.wgsl','irradiance.wgsl'])]:
    raw=(source/manifest).read_bytes();meta=json.loads(raw)
    assert meta['source_commit']=='1438b1abfcaee3267092b75573014f4d9b4a983c'
    payloads={name:(source/name).read_bytes() for name in files}
    for name,data in payloads.items():assert hashlib.sha256(data).hexdigest()==meta['files'][name],name
    output.mkdir(parents=True)
    (output/manifest).write_bytes(raw)
    for name,data in payloads.items():(output/name).write_bytes(data)
    print(json.dumps({'output':str(output),'files':len(payloads)+1,'results_included':False,'native_controller':native_controller}))
