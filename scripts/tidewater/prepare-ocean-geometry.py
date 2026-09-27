"""Prepare original CDLOD/WaterSurface vertex functions for native FFT images."""
from pathlib import Path
import hashlib,json,shutil,sys
scene,lod,target=map(lambda p:Path(p).resolve(),sys.argv[1:4])
assert len(sys.argv)==4 and not target.exists() and not target.is_relative_to(scene) and not target.is_relative_to(lod)
fixture=json.loads((scene/'fixture.json').read_text());meta=json.loads((lod/'ocean-lod.json').read_text())
assert fixture['source']['commit']==meta['source_commit'] and fixture.get('sky_probe')
assert meta['surface_options']==dict(terrain=True,shore=False,wake=False,detail=False,shore_sim=False,surf_foam=False)
for root,files in [(scene,fixture['files']),(lod,meta['files'])]:
    for name,digest in files.items():
        assert Path(name).name==name and '/' not in name and '\\' not in name
        assert hashlib.sha256((root/name).read_bytes()).hexdigest()==digest
terrain=(scene/'terrain-lod.wgsl').read_text()
start=terrain.index('fn terrainHeightAt(');end=terrain.index('\n}',start)+2
shader='''#import bevy_pbr::{mesh_view_bindings::{view,view_transmission_texture,view_transmission_sampler},view_transformations::position_world_to_clip}
struct CdlodParams { morph:array<vec4f,12> };
struct OceanSizes { sizes:array<vec4f,4> };
struct WaterSurfaceParams { amplitude:f32,slopeScale:f32,foamCoverage:f32,foamSharpness:f32,foamScale:f32 };
struct HeightDomain { origin:f32,size:f32,res:f32 };
struct SurfaceFrame { cameraPos:vec3f,seaLevel:f32 };
var<private> terrainParams:HeightDomain;
var<private> frame:SurfaceFrame;
@group(#{MATERIAL_BIND_GROUP}) @binding(0) var<uniform> cdlod:CdlodParams;
@group(#{MATERIAL_BIND_GROUP}) @binding(1) var<uniform> ocean:OceanSizes;
@group(#{MATERIAL_BIND_GROUP}) @binding(2) var<uniform> waterSurface:WaterSurfaceParams;
@group(#{MATERIAL_BIND_GROUP}) @binding(3) var terrainHeightTex:texture_2d<f32>;
@group(#{MATERIAL_BIND_GROUP}) @binding(4) var oceanDisplacement:texture_2d_array<f32>;
@group(#{MATERIAL_BIND_GROUP}) @binding(5) var smpLinearRepeat:sampler;
@group(#{MATERIAL_BIND_GROUP}) @binding(6) var waterSceneDepth:texture_depth_2d;
@group(#{MATERIAL_BIND_GROUP}) @binding(7) var<uniform> waterDiagnostic:vec4u;
'''+terrain[start:end]+'\n'+'\n'.join((lod/name).read_text() for name in ['cdlod.wgsl','surface-attenuation.wgsl','surface-vertex.wgsl'])+'''
struct WaterVertex { @builtin(position) clip:vec4f };
@vertex fn vertex(@location(0) grid:vec3f,@location(1) node:vec4f)->WaterVertex {
    frame.cameraPos=view.world_position;frame.seaLevel=0.0;
    terrainParams.origin=-1024.0;terrainParams.size=2048.0;terrainParams.res=2048.0;
    let surface=waterSurfaceVertex(node,grid.xz);
    var o:WaterVertex;o.clip=position_world_to_clip(surface.position);return o;
}
@fragment fn fragment(input:WaterVertex)->@location(0) vec4f {
    let uv=input.clip.xy/vec2f(textureDimensions(waterSceneDepth));
    if waterDiagnostic.x==1u { return textureSampleLevel(view_transmission_texture,view_transmission_sampler,uv,0.0); }
    if waterDiagnostic.x==2u { return vec4f(vec3f(textureLoad(waterSceneDepth,vec2i(input.clip.xy),0)*100.0),1.0); }
    return vec4f(1.0);
}
'''
target.mkdir(parents=True)
for name in fixture['files']:shutil.copyfile(scene/name,target/name)
(target/'ocean-geometry.wgsl').write_text(shader,encoding='utf-8')
fixture['ocean_geometry_probe']=dict(surface_options=meta['surface_options'],lod_config=meta['config'],lod_export_sha256=hashlib.sha256((lod/'ocean-lod.json').read_bytes()).hexdigest(),purpose='white_surface_geometry_not_water_material')
fixture['preparation'][Path(__file__).name]=hashlib.sha256(Path(__file__).read_bytes()).hexdigest()
fixture['files']={p.name:hashlib.sha256(p.read_bytes()).hexdigest() for p in target.iterdir()}
(target/'fixture.json').write_text(json.dumps(fixture,indent=2),encoding='utf-8')
print(json.dumps(dict(output=str(target))))
