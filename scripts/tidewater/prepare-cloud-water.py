"""Connect the original panorama reflection to water and breaking lip shaders."""
from pathlib import Path
import hashlib,json,struct,sys
assert len(sys.argv)==4
scene,cloud,target=map(lambda s:Path(s).resolve(),sys.argv[1:])
assert not target.exists() and not target.is_relative_to(scene) and not target.is_relative_to(cloud)
fixture=json.loads((scene/'fixture.json').read_text());meta=json.loads((cloud/'environment.json').read_text())
assert fixture['sky_probe']=='native_atmosphere_cloud_history'
assert fixture['source']['commit']==meta['source_commit']
def checked(root,files,name):
 data=(root/name).read_bytes();assert hashlib.sha256(data).hexdigest()==files[name],name;return data
payloads={name:checked(scene,fixture['files'],name) for name in fixture['files']}
frame=payloads['water-frame.bin']
assert struct.unpack_from('<f',frame,668)[0]==meta['wind']['speed']
assert list(struct.unpack_from('<2f',frame,688))==meta['wind']['direction']
params=checked(cloud,meta['files'],'cloud-public-params.wgsl').decode()
code=checked(cloud,meta['files'],'cloud-module-clouds.wgsl').decode()
start=code.index('fn cloudsSample(');end=code.index('\n}',start)+2;sample=code[start:end]
for name,binding,sampler in [('water-optics.wgsl',32,None),('breaker-lip.wgsl',10,12)]:
 shader=payloads[name].decode()
 prefix=params+f'\n@group(#{{MATERIAL_BIND_GROUP}}) @binding({binding}) var<uniform> cloudsParams:CloudsParams;\n'
 prefix+=f'@group(#{{MATERIAL_BIND_GROUP}}) @binding({binding+1}) var cloudsPanorama:texture_2d<f32>;\n'
 if sampler is not None:prefix+=f'@group(#{{MATERIAL_BIND_GROUP}}) @binding({sampler}) var smpLinearRepeat:sampler;\n'
 prefix+=sample+'\n'
 anchor='fn skyReflectionRadiance( dir: vec3f ) -> vec3f {'
 assert shader.count(anchor)==1
 start=shader.index(anchor);end=shader.index('\n}',start)
 body=shader[start:end];assert body.count('return base;')==1
 shader=shader[:start]+prefix+body.replace('return base;','let c=cloudsSample(dir); return base*c.a+c.rgb;')+shader[end:]
 payloads[name]=shader.encode()
target.mkdir(parents=True)
for name,data in payloads.items():(target/name).write_bytes(data)
fixture['cloud_water_reflection_probe']={'panorama':'native_shared_with_sky','receivers':['water','breaker_lip'],'cloud_shadows':False}
fixture['files']={name:hashlib.sha256(data).hexdigest() for name,data in payloads.items()}
fixture['preparation'][Path(__file__).name]=hashlib.sha256(Path(__file__).read_bytes()).hexdigest()
(target/'fixture.json').write_text(json.dumps(fixture,indent=2));print(target)
