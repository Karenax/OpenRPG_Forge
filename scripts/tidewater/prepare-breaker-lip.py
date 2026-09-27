"""Prepare original breaking-lip shaders for the shared native scene."""
from pathlib import Path
import hashlib,json,re,shutil,sys
scene,source,reference,target=map(lambda p:Path(p).resolve(),sys.argv[1:5])
assert len(sys.argv)==5 and not target.exists()
assert not target.is_relative_to(scene) and not target.is_relative_to(source)
f=json.loads((scene/'fixture.json').read_text());m=json.loads((source/'breakers.json').read_text());r=json.loads(Path(str(reference)+'.json').read_text())
assert f['source']['commit']==m['source_commit']==r['source_commit']
assert r['camera']==f['camera_pose'] and r['breakers']['steps']==120
assert r['breakers']['spray'] is False and r['breakers']['clouds'] is False
for root,files in [(scene,f['files']),(source,m['files'])]:
 for n,h in files.items():
  assert Path(n).name==n and '/' not in n and '\\' not in n
  assert hashlib.sha256((root/n).read_bytes()).hexdigest()==h
read=lambda n:(source/n).read_text()
shader='''diagnostic(off, derivative_uniformity);
#import bevy_pbr::{mesh_view_bindings::view,view_transformations::position_world_to_clip}
'''
for n in ['frame','skyParams','atmosphereParams']:shader+=(scene/f'uniform-{n}.wgsl').read_text()+'\n'
mat=read('material-params.wgsl');mat=re.sub(r'MaterialParams\d+','LipMaterialParams',mat);shader+=mat
shader+='''
var<private> frame:Frame;
@group(#{MATERIAL_BIND_GROUP}) @binding(0) var<uniform> lipFrame:Frame;
@group(#{MATERIAL_BIND_GROUP}) @binding(1) var<storage,read> breakersCrest:array<vec4f>;
@group(#{MATERIAL_BIND_GROUP}) @binding(2) var brkLace:texture_2d<f32>;
@group(#{MATERIAL_BIND_GROUP}) @binding(3) var smpAnisoRepeat:sampler;
@group(#{MATERIAL_BIND_GROUP}) @binding(4) var<uniform> atmosphereParams:AtmosphereParams;
@group(#{MATERIAL_BIND_GROUP}) @binding(5) var<uniform> skyParams:SkyParams;
@group(#{MATERIAL_BIND_GROUP}) @binding(6) var atmosphereTransmittanceLUT:texture_2d<f32>;
@group(#{MATERIAL_BIND_GROUP}) @binding(7) var atmosphereSkyViewLUT:texture_2d<f32>;
@group(#{MATERIAL_BIND_GROUP}) @binding(8) var smpLinearClamp:sampler;
@group(#{MATERIAL_BIND_GROUP}) @binding(9) var<uniform> mat:LipMaterialParams;
'''
for n in ['common','breakersFresnel','breakersLip','atmosphereCore','atmosphereTransmittance','atmosphere','sky']:shader+=read(f'module-{n}.wgsl')+'\n'
shader+='''
fn setupFrame(){frame=lipFrame;frame.view=view.view_from_world;frame.proj=view.clip_from_view;frame.invView=view.world_from_view;frame.invProj=view.view_from_clip;frame.invViewProj=view.world_from_clip;frame.cameraPos=view.world_position;}
struct LipVertex{@builtin(position) clip:vec4f,@location(0) P:vec3f,@location(1) vLipN:vec3f,@location(2) vLip:vec4f,@location(3) vLipFade:f32};
@vertex fn vertex(@location(0) position:vec3f,@location(1) sheetId:vec4f)->LipVertex{
 setupFrame();var o:LipVertex;
'''
v=read('lip-vertex.wgsl').replace('v.sheetId','sheetId');v=v[:v.index('\tv.useWorld = true;')]
shader+=v+'o.P=select(vec3f(0.0,-1e5,0.0),P,valid);o.clip=position_world_to_clip(o.P);return o;}\n'
shader+='''struct LipInput{P:vec3f,front:bool,vs:LipVertex};
struct LipResult{color:vec4f};
fn shade(in:LipInput)->LipResult{var r:LipResult;
'''+read('lip-output.wgsl').replace('(*r).color','r.color')+'\nreturn r;}\n'
shader+='''@fragment fn fragment(v:LipVertex,@builtin(front_facing) front:bool)->@location(0) vec4f{setupFrame();var i:LipInput;i.P=v.P;i.front=front;i.vs=v;return shade(i).color;}
'''
shader=re.sub(r'\bview(?=\s*:\s*mat4x4f)','sourceView',shader);shader=re.sub(r'\bframe\.view\b','frame.sourceView',shader)
assert Path(str(reference)+'.frame.bin').read_bytes()==(scene/'water-frame.bin').read_bytes()
target.mkdir(parents=True)
for n in f['files']:shutil.copyfile(scene/n,target/n)
for n in ['material-params.bin']+[f'lace-m{i}.rgba8.bin' for i in range(10)]:shutil.copyfile(source/n,target/('lip-'+n))
(target/'breaker-lip.wgsl').write_text(shader)
f['breaker_lip_probe']={'stations':m['stations'],'reference':str(reference),'source_manifest_sha256':hashlib.sha256((source/'breakers.json').read_bytes()).hexdigest(),'reference_sha256':hashlib.sha256(reference.read_bytes()).hexdigest(),'spray':False,'clouds':False}
f['preparation'][Path(__file__).name]=hashlib.sha256(Path(__file__).read_bytes()).hexdigest()
f['files']={p.name:hashlib.sha256(p.read_bytes()).hexdigest() for p in target.iterdir()}
(target/'fixture.json').write_text(json.dumps(f,indent=2));print(target)
