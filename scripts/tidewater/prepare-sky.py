"""Prepare the original clear-sky functions for native atmosphere textures."""
from pathlib import Path
import hashlib
import json
import re
import shutil
import sys

scene, atmosphere, target = map(lambda x:Path(x).resolve(), sys.argv[1:4])
assert not target.exists() and not target.is_relative_to(scene) and not target.is_relative_to(atmosphere)
fixture=json.loads((scene/'fixture.json').read_text())
meta=json.loads((atmosphere/'atmosphere.json').read_text())
assert fixture['source']['commit']==meta['source_commit']
assert fixture.get('object_shadow_probe')
assert fixture['camera_pose']['position'][1]==meta['camera_y']
assert fixture['environment_lighting_probe']['sun_direction']==meta['real_sun_direction']
for folder,files in [(scene,fixture['files']),(atmosphere,meta['files'])]:
    for name,sha in files.items():
        assert Path(name).name==name
        assert hashlib.sha256((folder/name).read_bytes()).hexdigest()==sha
for name in fixture['files'].keys() & meta['files'].keys():
    assert fixture['files'][name]==meta['files'][name]
source=(atmosphere/'transmittance.wgsl').read_text()
frame=source.split('struct AtmosphereParams',1)[0]
atmo=re.search(r'struct AtmosphereParams\s*\{[^}]+\};?',source).group()
shader='''#import bevy_pbr::mesh_view_bindings::view
'''+frame+atmo+'''
var<private> frame:Frame;
struct SkyParams { moonDir:vec3f,sunDiskIntensity:f32,starIntensity:f32 };
var<private> skyParams:SkyParams;
struct SkySettings { moonDisk:vec4f,starsTimeNight:vec4f };
@group(#{MATERIAL_BIND_GROUP}) @binding(0) var<uniform> atmosphereParams:AtmosphereParams;
@group(#{MATERIAL_BIND_GROUP}) @binding(1) var<uniform> skySettings:SkySettings;
@group(#{MATERIAL_BIND_GROUP}) @binding(2) var atmosphereTransmittanceLUT:texture_2d<f32>;
@group(#{MATERIAL_BIND_GROUP}) @binding(3) var atmosphereSkyViewLUT:texture_2d<f32>;
@group(#{MATERIAL_BIND_GROUP}) @binding(4) var smpLinearClamp:sampler;
'''
shader+='\n'.join((atmosphere/n).read_text() for n in ['source-common.wgsl','atmosphere-core.wgsl','atmosphere-transmittance.wgsl','lookup.wgsl','sky-module.wgsl'])
shader+='''
struct SkyVertex { @builtin(position) clip:vec4f,@location(0) ndc:vec2f };
@vertex fn vertex(@location(0) position:vec3f)->SkyVertex {
    var o:SkyVertex;o.clip=vec4f(position.xy,0.0,1.0);o.ndc=position.xy;return o;
}
@fragment fn fragment(input:SkyVertex)->@location(0) vec4f {
    frame.time=skySettings.starsTimeNight.y;frame.night=skySettings.starsTimeNight.z;
    skyParams.moonDir=skySettings.moonDisk.xyz;skyParams.sunDiskIntensity=skySettings.moonDisk.w;
    skyParams.starIntensity=skySettings.starsTimeNight.x;
    let p=view.world_from_clip*vec4f(input.ndc,0.5,1.0);
    let dir=normalize(p.xyz/p.w-view.world_position);
    return vec4f(skyViewRadiance(dir),1.0);
}
'''
# Original modules can repeat a global directive; this clear-sky adapter uses
# explicit-LOD sampling and has no derivative-dependent operations.
shader=re.sub(r'diagnostic\s*\([^;]+;\s*','',shader)
target.mkdir(parents=True)
for name in fixture['files']:shutil.copyfile(scene/name,target/name)
for name in meta['files']:shutil.copyfile(atmosphere/name,target/name)
shutil.copyfile(atmosphere/'atmosphere.json',target/'atmosphere.json')
(target/'sky.wgsl').write_text(shader,encoding='utf-8')
fixture['sky_probe']='native_atmosphere_clear_sky_no_clouds'
fixture['preparation'][Path(__file__).name]=hashlib.sha256(Path(__file__).read_bytes()).hexdigest()
fixture['files']={p.name:hashlib.sha256(p.read_bytes()).hexdigest() for p in target.iterdir()}
(target/'fixture.json').write_text(json.dumps(fixture,indent=2),encoding='utf-8')
print(json.dumps({'output':str(target)}))
