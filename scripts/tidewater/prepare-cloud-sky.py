"""Compose original cloud sampling into the existing native clear-sky material."""
from pathlib import Path
import hashlib,json,shutil,sys
assert len(sys.argv)==4
scene,cloud,target=map(lambda x:Path(x).resolve(),sys.argv[1:])
assert not target.exists() and not target.is_relative_to(scene) and not target.is_relative_to(cloud)
fixture=json.loads((scene/'fixture.json').read_text());meta=json.loads((cloud/'environment.json').read_text())
assert fixture['source']['commit']==meta['source_commit']
assert fixture['sky_probe']=='native_atmosphere_clear_sky_no_clouds'
assert fixture['camera_pose']['position']==[meta['camera']['position'][k] for k in ['x','y','z']]
assert fixture['camera_pose']['rotation']==[meta['camera']['quaternion'][k] for k in ['_x','_y','_z','_w']]
assert fixture['camera_pose']['fov_degrees']==meta['camera']['fov']
def checked(root,files,name):
    data=(root/name).read_bytes();assert hashlib.sha256(data).hexdigest()==files[name],name
    return data
shader=checked(scene,fixture['files'],'sky.wgsl').decode()
params=checked(cloud,meta['files'],'cloud-public-params.wgsl').decode()
module=checked(cloud,meta['files'],'cloud-module-clouds.wgsl').decode()
prefix=params+'''
@group(#{MATERIAL_BIND_GROUP}) @binding(5) var<uniform> cloudsParams:CloudsParams;
@group(#{MATERIAL_BIND_GROUP}) @binding(6) var cloudsPanorama:texture_2d<f32>;
@group(#{MATERIAL_BIND_GROUP}) @binding(7) var cloudsView:texture_2d<f32>;
@group(#{MATERIAL_BIND_GROUP}) @binding(8) var smpLinearRepeat:sampler;
'''+module
anchor='fn skyViewRadiance( dir: vec3f ) -> vec3f {'
assert shader.count(anchor)==1
start=shader.index(anchor);end=shader.index('\n}',start)
body=shader[start:end];assert body.count('return base;')==1
shader=shader[:start]+prefix+body.replace('return base;','let c=cloudsSampleView(dir); return base*c.a+c.rgb;')+shader[end:]
payloads={name:checked(scene,fixture['files'],name) for name in fixture['files']}
payloads['sky.wgsl']=shader.encode()
payloads['cloud-view-params.bin']=checked(cloud,meta['files'],'cloud-step120-public.bin')
assert len(payloads['cloud-view-params.bin'])==80
target.mkdir(parents=True)
for name,data in payloads.items():(target/name).write_bytes(data)
fixture['sky_probe']='native_atmosphere_cloud_history'
fixture['cloud_acquisition_sha256']=hashlib.sha256((cloud/'environment.json').read_bytes()).hexdigest()
fixture['preparation'][Path(__file__).name]=hashlib.sha256(Path(__file__).read_bytes()).hexdigest()
fixture['files']={name:hashlib.sha256(data).hexdigest() for name,data in payloads.items()}
(target/'fixture.json').write_text(json.dumps(fixture,indent=2))
print(json.dumps({'output':str(target),'files':len(payloads)}))
