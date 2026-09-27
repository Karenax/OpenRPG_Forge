"""Adapt the original sprite material to Bevy camera bindings and native GPU buffers."""
from pathlib import Path
import hashlib,json,re,shutil,sys
assert len(sys.argv)==5
scene,source,reference,target=map(lambda p:Path(p).resolve(),sys.argv[1:])
assert not target.exists() and not target.is_relative_to(scene) and not target.is_relative_to(source)
f=json.loads((scene/'fixture.json').read_text());m=json.loads((source/'spray.json').read_text());r=json.loads(Path(str(reference)+'.json').read_text())
assert f['source']['commit']==m['source_commit']==r['source_commit']
assert r['camera']==f['camera_pose'] and r['spray']['steps']==120
assert r['spray']['global_wind']==m['global_wind']=={'speed':6,'direction':[1,0]}
assert r['breakers']['spray'] is True and r['breakers']['clouds'] is False
assert 'breaker-lip.wgsl' in f['files']
for root,files in [(scene,f['files']),(source,m['files'])]:
 for n,h in files.items():
  assert Path(n).name==n and '/' not in n and '\\' not in n
  assert hashlib.sha256((root/n).read_bytes()).hexdigest()==h,n
read=lambda n:(source/n).read_text()
assert m['sprite']['capacity']==40960 and m['sprite']['blend']=='premultiplied'
assert m['sprite']['modules']==['common','sprayCommon','breakersSprayShadow','sprayHooks']
shader='''diagnostic(off, derivative_uniformity);
#import bevy_pbr::{mesh_view_bindings::view,view_transformations::position_world_to_clip}
'''+(scene/'uniform-frame.wgsl').read_text()
shader+=re.sub(r'MaterialParams\d+','SprayMaterialParams',read('sprite-material-params.wgsl'))
shader+='''
var<private> frame:Frame;
@group(#{MATERIAL_BIND_GROUP}) @binding(0) var<uniform> sprayFrame:Frame;
@group(#{MATERIAL_BIND_GROUP}) @binding(1) var<storage,read> sprayPosR:array<vec4f>;
@group(#{MATERIAL_BIND_GROUP}) @binding(2) var<storage,read> sprayVelR:array<vec4f>;
@group(#{MATERIAL_BIND_GROUP}) @binding(3) var<storage,read> sprayInfoR:array<vec4f>;
@group(#{MATERIAL_BIND_GROUP}) @binding(4) var<storage,read> breakersCrest:array<vec4f>;
@group(#{MATERIAL_BIND_GROUP}) @binding(5) var sprayPuff:texture_2d<f32>;
@group(#{MATERIAL_BIND_GROUP}) @binding(6) var smpLinearRepeat:sampler;
@group(#{MATERIAL_BIND_GROUP}) @binding(7) var sprayDots:texture_2d<f32>;
@group(#{MATERIAL_BIND_GROUP}) @binding(8) var spraySceneDepth:texture_depth_2d;
@group(#{MATERIAL_BIND_GROUP}) @binding(9) var<uniform> mat:SprayMaterialParams;
'''
for n in m['sprite']['modules']:shader+=read(f'sprite-module-{n}.wgsl')+'\n'
shader+='''
fn setupFrame(){frame=sprayFrame;frame.view=view.view_from_world;frame.proj=view.clip_from_view;frame.invView=view.world_from_view;frame.invProj=view.view_from_clip;frame.invViewProj=view.world_from_clip;frame.cameraPos=view.world_position;}
struct SpriteVertex{@builtin(position) clip:vec4f,@location(0) P:vec3f,@location(1) vUV:vec2f,@location(2) vCol:vec4f,@location(3) vMisc:vec4f,@location(4) vFwd:vec4f};
@vertex fn vertex(@location(0) position:vec3f,@location(1) slot:u32)->SpriteVertex{
 setupFrame();var o:SpriteVertex;
'''
v=read('sprite-vertex.wgsl').replace('v.instance','slot').replace('v.position','position')
assert v.count('\tv.useWorld = true;')==1
v=v[:v.index('\tv.useWorld = true;')]
shader+=v+'o.P=select(vec3f(0.0,-1e5,0.0),world,alive && a > 1e-4);o.clip=position_world_to_clip(o.P);return o;}\n'
shader+='''struct SpriteInput{P:vec3f,pixel:vec2f,vs:SpriteVertex};
struct SpriteResult{color:vec4f};
fn shade(in:SpriteInput)->SpriteResult{var r:SpriteResult;
'''+read('sprite-output.wgsl')+'\nreturn r;}\n'
shader+='''@fragment fn fragment(v:SpriteVertex)->@location(0) vec4f{setupFrame();var i:SpriteInput;i.P=v.P;i.pixel=v.clip.xy;i.vs=v;return shade(i).color;}
'''
shader=re.sub(r'\bview(?=\s*:\s*mat4x4f)','sourceView',shader);shader=re.sub(r'\bframe\.view\b','frame.sourceView',shader)
assert Path(str(reference)+'.frame.bin').read_bytes()==(scene/'water-frame.bin').read_bytes()
target.mkdir(parents=True)
for n in f['files']:shutil.copyfile(scene/n,target/n)
for n in m['files']:
 if n.startswith('sprite-'):shutil.copyfile(source/n,target/n)
(target/'spray-sprites.wgsl').write_text(shader)
f['spray_sprite_probe']={'capacity':40960,'reference':str(reference),'source_manifest_sha256':hashlib.sha256((source/'spray.json').read_bytes()).hexdigest(),'reference_sha256':hashlib.sha256(reference.read_bytes()).hexdigest(),'global_wind':m['global_wind'],'clouds':False}
f['preparation'][Path(__file__).name]=hashlib.sha256(Path(__file__).read_bytes()).hexdigest()
f['files']={p.name:hashlib.sha256(p.read_bytes()).hexdigest() for p in target.iterdir()}
(target/'fixture.json').write_text(json.dumps(f,indent=2));print(target)
