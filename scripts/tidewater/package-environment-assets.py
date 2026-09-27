"""Create a local pinned sky/cloud resource package, without GPU result textures.
Usage: SCENE CLOUD_INPUTS UPSTREAM NEW_OUTPUT
Terrain/object shadows remain scene-generated dependencies.
"""
from pathlib import Path
import hashlib,json,sys
scene,cloud,upstream,out=map(Path,sys.argv[1:])
assert not out.exists()
commit='1438b1abfcaee3267092b75573014f4d9b4a983c'
fixture=json.loads((scene/'fixture.json').read_text())
atmos=json.loads((scene/'atmosphere.json').read_text())
env=json.loads((cloud/'environment.json').read_text())
assert fixture['source']['commit']==atmos['source_commit']==env['source_commit']==commit
files={}
def include(name,raw):
    assert Path(name).name==name and not any(c in name for c in '\\/:')
    assert name not in files or files[name]==raw
    files[name]=raw
def checked(root,meta,name):
    raw=(root/name).read_bytes()
    assert hashlib.sha256(raw).hexdigest()==meta['files'][name],name
    include(name,raw)
include('atmosphere.json',(scene/'atmosphere.json').read_bytes())
include('cloud-environment.json',(cloud/'environment.json').read_bytes())
for name in ['params.bin','transmittance.wgsl','multiScat.wgsl','skyView.wgsl','irradiance.wgsl']:
    checked(scene,atmos,name)
for name in ['sky.wgsl','environment-cube.bin','village-lighting.json','cloud-view-params.bin']:
    checked(scene,fixture,name)
for name in ['cloud-weather.wgsl','cloud-bounds.wgsl','cloud-shadow.wgsl','cloud-trace.wgsl','cloud-temporal.wgsl','cloud-pano.wgsl','cloud-blueNoise.bin']:
    checked(cloud,env,name)
for level in range(7):
    checked(cloud,env,f'cloud-volume-m{level}.rgba8.bin')
for step in range(1,121):
    for kind in ['global','atmosphere']:
        checked(cloud,env,f'cloud-step{step:03}-{kind}.bin')
license=(upstream/'LICENSE').read_bytes()
assert b'MIT License' in license and b'Permission is hereby granted' in license
include('LICENSE-Tidewater.txt',license)
manifest=dict(schema='forge.tidewater.environment-package',version=1,source_commit=commit,
    files={name:dict(sha256=hashlib.sha256(raw).hexdigest(),bytes=len(raw)) for name,raw in sorted(files.items())})
raw=json.dumps(manifest,indent=2).encode()
out.mkdir()
for name,data in files.items(): (out/name).write_bytes(data)
(out/'package.json').write_bytes(raw)
print(json.dumps(dict(files=len(files),bytes=sum(map(len,files.values())),sha256=hashlib.sha256(raw).hexdigest())))
