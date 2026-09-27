"""Bind original cloud shadow sampling to every currently assembled scene receiver."""
from pathlib import Path
import hashlib,json,sys
assert len(sys.argv)==4
scene,cloud,target=map(lambda s:Path(s).resolve(),sys.argv[1:])
assert not target.exists() and not target.is_relative_to(scene) and not target.is_relative_to(cloud)
fixture=json.loads((scene/'fixture.json').read_text());meta=json.loads((cloud/'environment.json').read_text())
assert fixture['source']['commit']==meta['source_commit']
assert fixture['cloud_water_reflection_probe']['cloud_shadows'] is False
def checked(root,files,name):
 data=(root/name).read_bytes();assert hashlib.sha256(data).hexdigest()==files[name],name;return data
payloads={name:checked(scene,fixture['files'],name) for name in fixture['files']}
assert payloads['cloud-view-params.bin']==checked(cloud,meta['files'],'cloud-step120-public.bin')
params=checked(cloud,meta['files'],'cloud-public-params.wgsl').decode()
shadow=checked(cloud,meta['files'],'cloud-module-cloudsShadow.wgsl').decode()
for name,binding,uniform in [('plant.wgsl',41,40),('terrain.wgsl',41,40),('village.wgsl',41,40),('spray-sprites.wgsl',41,40),('water-optics.wgsl',34,None),('breaker-lip.wgsl',13,None)]:
 shader=payloads[name].decode()
 prefix=f'\n@group(#{{MATERIAL_BIND_GROUP}}) @binding({binding}) var cloudsShadowMap:texture_2d<f32>;\n'+shadow
 if uniform is not None:prefix=params+f'\n@group(#{{MATERIAL_BIND_GROUP}}) @binding({uniform}) var<uniform> cloudsParams:CloudsParams;\n'+prefix
 if name in ['plant.wgsl','terrain.wgsl','village.wgsl']:
  anchor='*terrainSunShadowAt(input.P)' if name=='plant.wgsl' else '*terrainSunShadowAt(P)'
  assert shader.count(anchor)==(4 if name=='plant.wgsl' else 1)
  shader=shader.replace(anchor,anchor+('*cloudsShadow(input.P.xz)' if name=='plant.wgsl' else '*cloudsShadow(P.xz)'))
 elif name=='water-optics.wgsl':
  anchor='sunLight *= terrainSunShadowAt( pos );';assert shader.count(anchor)==1
  shader=shader.replace(anchor,anchor+'\n sunLight *= cloudsShadow( pos.xz );')
 elif name=='breaker-lip.wgsl':
  anchor='let sun = frame.sunColor * 1.0;';assert shader.count(anchor)==1
  shader=shader.replace(anchor,'let sun = frame.sunColor * cloudsShadow( pos.xz );')
 else:
  anchor='fn sprayCloudShadow( xz: vec2f ) -> f32 { return 1.0; }';assert shader.count(anchor)==1
  shader=shader.replace(anchor,'fn sprayCloudShadow( xz: vec2f ) -> f32 { return cloudsShadow( xz ); }')
 payloads[name]=(shader+'\n'+prefix).encode()
target.mkdir(parents=True)
for name,data in payloads.items():(target/name).write_bytes(data)
fixture['cloud_shadow_probe']={'receivers':['terrain','village','vegetation','grass','water','breaker_lip','spray'],'map':'native_shared_cloud_graph'}
fixture['cloud_water_reflection_probe']['cloud_shadows']=True
fixture['files']={name:hashlib.sha256(data).hexdigest() for name,data in payloads.items()}
fixture['preparation'][Path(__file__).name]=hashlib.sha256(Path(__file__).read_bytes()).hexdigest()
(target/'fixture.json').write_text(json.dumps(fixture,indent=2));print(target)
