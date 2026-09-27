"""Adapt the original water optical shader to native scene/FFT/atmosphere inputs."""
from pathlib import Path
import hashlib,json,re,shutil,sys
scene,material,reference,target=map(lambda x:Path(x).resolve(),sys.argv[1:5])
assert len(sys.argv)==5 and not target.exists() and not target.is_relative_to(scene)
fixture=json.loads((scene/'fixture.json').read_text());meta=json.loads((material/'water-material.json').read_text())
ref=json.loads(Path(str(reference)+'.json').read_text())
assert fixture['source']['commit']==meta['source_commit']==ref['source_commit']
assert ref['camera']==fixture['camera_pose'] and ref['water_geometry']['output']=='original_water_optics'
assert fixture.get('ocean_geometry_probe') and fixture.get('sky_probe')
shore=meta['options']['shore'];assert isinstance(shore,bool)
sim=meta['options']['shore_sim'];assert isinstance(sim,bool) and (shore or not sim)
detail=meta['options']['detail'];assert isinstance(detail,bool) and (sim or not detail)
assert meta['options']==dict(terrain=True,shore=shore,wake=False,detail=detail,shore_sim=sim,surf_foam=sim,clouds=False,planar_reflection=False,separate_refraction=False,hull=False)
assert ref['water_geometry']['surface_options']['shore']==shore
assert ref['water_geometry']['surface_options'].get('shore_sim',False)==sim
assert ref['water_geometry']['surface_options'].get('detail',False)==detail
if sim: assert ref['shore_sim']['terrain_wetness'] and ref['shore_sim']['frame']==120
if shore:
 assert meta['terrain_sha256']==fixture['files']['heights.bin']
 assert meta['shore_field']==dict(res=512,origin=-1024,size=2048)
for root,files in [(scene,fixture['files']),(material,meta['files'])]:
 for name,digest in files.items():
  assert Path(name).name==name and '/' not in name and '\\' not in name
  assert hashlib.sha256((root/name).read_bytes()).hexdigest()==digest
read=lambda n:(material/n).read_text()
shader='''diagnostic(off, derivative_uniformity);
#import bevy_pbr::{mesh_view_bindings::{view,view_transmission_texture,view_transmission_sampler},view_transformations::position_world_to_clip}
'''
for name in ['frame','ocean','waterSurface','terrainParams','skyParams','atmosphereParams','mat','whaleMarks']:
 shader+=read(f'uniform-{name}.wgsl')+'\n'
shader+='''
struct CdlodParams { morph:array<vec4f,12> };
var<private> frame:Frame;
var<private> terrainParams:TerrainParams;
@group(#{MATERIAL_BIND_GROUP}) @binding(0) var<uniform> cdlod:CdlodParams;
@group(#{MATERIAL_BIND_GROUP}) @binding(1) var<uniform> ocean:OceanParams;
@group(#{MATERIAL_BIND_GROUP}) @binding(2) var<uniform> waterSurface:WaterSurfaceParams;
@group(#{MATERIAL_BIND_GROUP}) @binding(3) var terrainHeightTex:texture_2d<f32>;
@group(#{MATERIAL_BIND_GROUP}) @binding(4) var oceanDisplacement:texture_2d_array<f32>;
@group(#{MATERIAL_BIND_GROUP}) @binding(5) var smpLinearRepeat:sampler;
@group(#{MATERIAL_BIND_GROUP}) @binding(6) var waterSceneDepth:texture_depth_2d;
@group(#{MATERIAL_BIND_GROUP}) @binding(7) var<uniform> waterFrame:Frame;
@group(#{MATERIAL_BIND_GROUP}) @binding(8) var oceanDerivatives:texture_2d_array<f32>;
@group(#{MATERIAL_BIND_GROUP}) @binding(9) var smpAnisoRepeat:sampler;
@group(#{MATERIAL_BIND_GROUP}) @binding(10) var waterFoamTex:texture_2d<f32>;
@group(#{MATERIAL_BIND_GROUP}) @binding(11) var terrainNormalTex:texture_2d<f32>;
@group(#{MATERIAL_BIND_GROUP}) @binding(12) var smpLinearClamp:sampler;
@group(#{MATERIAL_BIND_GROUP}) @binding(13) var terrainSunShadowTex:texture_2d<f32>;
@group(#{MATERIAL_BIND_GROUP}) @binding(18) var<uniform> atmosphereParams:AtmosphereParams;
@group(#{MATERIAL_BIND_GROUP}) @binding(19) var<uniform> skyParams:SkyParams;
@group(#{MATERIAL_BIND_GROUP}) @binding(20) var atmosphereTransmittanceLUT:texture_2d<f32>;
@group(#{MATERIAL_BIND_GROUP}) @binding(21) var atmosphereSkyViewLUT:texture_2d<f32>;
@group(#{MATERIAL_BIND_GROUP}) @binding(22) var<uniform> mat:MaterialParams0;
@group(#{MATERIAL_BIND_GROUP}) @binding(23) var<uniform> whaleMarks:WhaleMarks;
'''
shader=shader.replace('mat:MaterialParams0', 'mat:'+re.search(r'struct\s+(\w+)',read('uniform-mat.wgsl')).group(1))
if shore:
 shader+=read('uniform-shoreP.wgsl')+'''
@group(#{MATERIAL_BIND_GROUP}) @binding(24) var terrainShoreTex:texture_2d<f32>;
@group(#{MATERIAL_BIND_GROUP}) @binding(25) var<uniform> shoreP:ShoreParams;
@group(#{MATERIAL_BIND_GROUP}) @binding(26) var shoreDirTex:texture_2d<f32>;
'''
terrain=read('module-terrain.wgsl')
if detail:
 shader+=read('uniform-seaDetailParams.wgsl')+'''
@group(#{MATERIAL_BIND_GROUP}) @binding(30) var seaDetailNoise:texture_2d<f32>;
@group(#{MATERIAL_BIND_GROUP}) @binding(31) var<uniform> seaDetailParams:SeaDetailParams;
'''
if sim:
 shader+=read('uniform-shoreSimP.wgsl')+'''
@group(#{MATERIAL_BIND_GROUP}) @binding(27) var shoreSimStateTex:texture_2d<f32>;
@group(#{MATERIAL_BIND_GROUP}) @binding(28) var<uniform> shoreSimP:ShoreSimParams;
@group(#{MATERIAL_BIND_GROUP}) @binding(29) var shoreSimLaceNearest:texture_2d<f32>;
'''
def function(text,name):
 start=text.index('fn '+name+'(');end=text.index('\n}',start)+2
 return text[start:end]+'\n'
for name in ['terrainUvOf','terrainHeightAt','terrainNormalRock','terrainSunShadowAt']+(['terrainShoreSample','terrainNormalRockLevel','terrainNormalAt'] if shore else []):
 shader+=function(terrain,name)
for name in ['common','waterFresnel','waterHelpers','whaleWater','ocean','cdlod','waterSurfaceAttenuation']+(['shore'] if shore else [])+(['shoreSim','surfFoam'] if sim else [])+(['seaDetail'] if detail else [])+['waterSurface','atmosphereCore','atmosphereTransmittance','atmosphere','sky']:
 code=read(f'module-{name}.wgsl')
 if name=='waterHelpers':
  assert code.count('textureLoad( waterSceneDepth, p, 0 ).x')==1
  code=code.replace('textureLoad( waterSceneDepth, p, 0 ).x','textureLoad( waterSceneDepth, p, 0 )')
 if name=='shore':
  original='fn shoreHash1( x: f32 ) -> f32 { return fract( sin( x * 127.1 + 311.7 ) * 43758.5453 ); }'
  assert code.count(original)==1
  code=code.replace(original,Path(__file__).with_name('native-shore-hash.wgsl').read_text())
 shader+=code+'\n'
shadow=(scene/'object-shadow.wgsl').read_text()
shader+=shadow[:shadow.index('fn interleavedGradientNoise(')]+shadow[shadow.index('fn shadowCascadeOf('):]
shader+='''
fn setupWaterFrame() {
 frame=waterFrame;
 frame.view=view.view_from_world;frame.proj=view.clip_from_view;
 frame.invView=view.world_from_view;frame.invProj=view.view_from_clip;
 frame.invViewProj=view.world_from_clip;frame.cameraPos=view.world_position;
 terrainParams.origin=-1024.0;terrainParams.size=2048.0;terrainParams.res=2048.0;terrainParams.sunBaked=1.0;terrainParams.shoreRes=512.0;
}
struct WaterVertex {
 @builtin(position) clip:vec4f,@location(0) P:vec3f,
 @location(1) vLagXZ:vec2f,@location(2) vWaveH:f32,@location(3) vSeaDepth:f32,
 @location(4) vFoam:f32,@location(5) vShoreN:vec3f,@location(6) vShoreFoam:f32,
 @location(7) vSwash:f32,@location(8) vSurfMask:vec2f
};
@vertex fn vertex(@location(0) grid:vec3f,@location(1) node:vec4f)->WaterVertex {
 setupWaterFrame();let s=waterSurfaceVertex(node,grid.xz);
 var o:WaterVertex;o.clip=position_world_to_clip(s.position);o.P=s.position;
 o.vLagXZ=s.lagXZ;o.vWaveH=s.height;o.vSeaDepth=s.depth;o.vFoam=s.foam;
 o.vShoreN=s.shoreN;o.vShoreFoam=s.shoreFoam;o.vSwash=s.swash;o.vSurfMask=s.surfMask;return o;
}
struct WaterInput { P:vec3f,pixel:vec2f,front:bool,vs:WaterVertex };
struct WaterResult { color:vec4f,mask:vec4f };
fn shadeWater(in:WaterInput)->WaterResult {
 var r:WaterResult;
'''
body=read('water-output.wgsl')
# These two branches correspond to the explicitly disabled source options.
for define in ['WATER_HULL','WATER_REFRACTION']:
 body,n=re.subn(r'#if '+define+r'\b.*?#endif','',body,flags=re.S);assert n==1
assert '#' not in body
shader+=body+'\nreturn r;\n}\n'
shader+='''@fragment fn fragment(input:WaterVertex,@builtin(front_facing) front:bool)->@location(0) vec4f {
 setupWaterFrame();var i:WaterInput;i.P=input.P;i.pixel=input.clip.xy;i.front=front;i.vs=input;
 return shadeWater(i).color;
}
'''
shader=shader.replace('waterSceneColor','view_transmission_texture')
if sim: shader=shader.replace('surfFoamLaceTex','shoreSimLaceNearest')
# Avoid naga_oil importing Bevy's global `view` into the source Frame field.
# The packed layout is unchanged; only this field's identifier is namespaced.
shader=re.sub(r'\bview(?=\s*:\s*mat4x4f)', 'sourceView', shader)
shader=re.sub(r'\bframe\.view\b', 'frame.sourceView', shader)
frame_bytes=Path(str(reference)+'.frame.bin').read_bytes()
assert len(frame_bytes)==720 and frame_bytes[700:704]==bytes(4), 'Frame.pad0 must be zero for native rounding'
target.mkdir(parents=True)
for name in fixture['files']:shutil.copyfile(scene/name,target/name)
for name in meta['files']:shutil.copyfile(material/name,target/name)
shutil.copyfile(material/'water-material.json',target/'water-material.json')
shutil.copyfile(Path(str(reference)+'.frame.bin'),target/'water-frame.bin')
if detail:
 packed=Path(str(reference)+'.sea-detail.bin').read_bytes()
 assert len(packed)==32 and hashlib.sha256(packed).hexdigest()==ref['sea_detail']['uniform_sha256']
 assert ref['sea_detail']['steps']==120 and ref['sea_detail']['dt']==1/60
 (target/'uniform-seaDetailParams.bin').write_bytes(packed)
if 'water_debug' in ref:
 debug=ref['water_debug'];assert debug['mode'] in [2,3,11]
 packed=Path(str(reference)+'.material.bin').read_bytes()
 assert len(packed)==80 and hashlib.sha256(packed).hexdigest()==debug['material_sha256']
 assert int.from_bytes(packed[68:72],'little',signed=True)==debug['mode']
 (target/'uniform-mat.bin').write_bytes(packed)
(target/'water-optics.wgsl').write_text(shader,encoding='utf-8')
if sim:
 terrain_shader=(scene/'terrain.wgsl').read_text()
 old='fn terWetFoam( xz: vec2f, h: f32 ) -> vec2f {\n\treturn vec2f( smoothstep( 0.5, 0.0, h ), 0.0 );\n}'
 assert terrain_shader.count(old)==1
 terrain_shader=terrain_shader.replace(old,'fn terWetFoam( xz: vec2f, h: f32 ) -> vec2f { return terrainWetness(xz,h); }')
 terrain_shader+=read('uniform-shoreSimP.wgsl')+'''
@group(#{MATERIAL_BIND_GROUP}) @binding(18) var shoreSimStateTex:texture_2d<f32>;
@group(#{MATERIAL_BIND_GROUP}) @binding(19) var shoreSimLaceNearest:texture_2d<f32>;
@group(#{MATERIAL_BIND_GROUP}) @binding(20) var<uniform> shoreSimP:ShoreSimParams;
'''+read('module-shoreSim.wgsl')+read('terrain-wetness-hook.wgsl')
 if 'terrain_debug' in ref:
  assert ref['terrain_debug']['mode']=='wetness'
  entry='    if settings.flags.z > 0.5 { return vec4f(1.0); }'
  assert terrain_shader.count(entry)==1
  terrain_shader=terrain_shader.replace(entry,'    if settings.flags.z < 2.0 { return vec4f(terrainWetness(vs.world_position.xz,vs.world_position.y),0.0,1.0); }'+entry)
 (target/'terrain.wgsl').write_text(terrain_shader,encoding='utf-8')
fixture['water_optics_probe']=dict(reference=str(reference),reference_frame_sha256=hashlib.sha256((target/'water-frame.bin').read_bytes()).hexdigest(),options=meta['options'],foam='acquired_original_static_texture',frame='paired_source_constants_native_camera_matrices')
if 'water_debug' in ref: fixture['water_optics_probe']['diagnostic']=ref['water_debug']
if 'terrain_debug' in ref: fixture['water_optics_probe']['terrain_diagnostic']=ref['terrain_debug']
fixture['preparation'][Path(__file__).name]=hashlib.sha256(Path(__file__).read_bytes()).hexdigest()
if shore:
 fixture['preparation']['native-shore-hash.wgsl']=hashlib.sha256(Path(__file__).with_name('native-shore-hash.wgsl').read_bytes()).hexdigest()
fixture['files']={p.name:hashlib.sha256(p.read_bytes()).hexdigest() for p in target.iterdir()}
(target/'fixture.json').write_text(json.dumps(fixture,indent=2),encoding='utf-8')
print(json.dumps(dict(output=str(target))))
