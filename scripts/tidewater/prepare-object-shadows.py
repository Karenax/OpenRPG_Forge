"""Bind the pinned source PCSS/PCF filter to native scene material receivers."""
import hashlib
import json
from pathlib import Path
import shutil
import subprocess
import sys

upstream, source, target = (Path(v).resolve() for v in sys.argv[1:4])
pin = '1438b1abfcaee3267092b75573014f4d9b4a983c'
assert subprocess.check_output(['git', '-C', str(upstream), 'rev-parse', 'HEAD'], text=True).strip() == pin
assert not target.exists() and not target.is_relative_to(source)
fixture = json.loads((source/'fixture.json').read_text())
assert fixture['source']['commit'] == pin and fixture.get('hill_shadow_probe')
for name, sha in fixture['files'].items():
    assert Path(name).name == name
    assert hashlib.sha256((source/name).read_bytes()).hexdigest() == sha
lighting_path = upstream/'src/engine/render/wgsl/lighting.js'
common_path = upstream/'src/engine/render/wgsl/common.js'
for path in (lighting_path, common_path):
    subprocess.run(['git', '-C', str(upstream), 'diff', '--exit-code', 'HEAD', '--', str(path)], check=True)
lighting, common = lighting_path.read_text(), common_path.read_text()
shader = lighting.split('export const shadowModule =', 1)[1].split('code: /* wgsl */`', 1)[1].split('`', 1)[0]
helpers = common[common.index('fn interleavedGradientNoise('):common.index('// ---- gradient noise:')]
shader = shader.replace('frame.cameraPos', 'shadowParams.camera.xyz')
shader = shader.replace('- vec3f( frame.view[ 0 ][ 2 ], frame.view[ 1 ][ 2 ], frame.view[ 2 ][ 2 ] )', 'shadowParams.forward.xyz')
shader = shader.replace('frame.frameIndex', 'shadowParams.frameIndex')
assert 'frame.' not in shader
header = '''
struct ObjectShadowSettings {
    matrices:array<mat4x4f,4>, cascades:array<vec4f,4>, blend:array<vec4f,4>,
    count:u32, mapSize:f32, bias:f32, fade:f32,
    enabled:f32, pcssCascades:u32, sunAngularDiameter:f32, frameIndex:u32,
    camera:vec4f, forward:vec4f,
};
@group(#{MATERIAL_BIND_GROUP}) @binding(15) var sunShadowMap:texture_depth_2d_array;
@group(#{MATERIAL_BIND_GROUP}) @binding(16) var smpShadow:sampler_comparison;
@group(#{MATERIAL_BIND_GROUP}) @binding(17) var<uniform> shadowParams:ObjectShadowSettings;
'''
program = header + helpers + shader
target.mkdir(parents=True)
for name in fixture['files']:
    shutil.copyfile(source/name, target/name)
for name in ('plant.wgsl', 'terrain.wgsl', 'village.wgsl'):
    text = (source/name).read_text()
    if name == 'plant.wgsl':
        old = '*terrainSunShadowAt(input.P)'
        assert text.count(old) in (3, 4)
        text = text.replace(old, old+'*sunShadow(input.P,normalize(surface.normal),input.pixel)')
    else:
        old = 'fn sourceSceneLight(s:LightingSurface,V:vec3f,P:vec3f)'
        assert text.count(old) == 1
        text = text.replace(old, 'fn sourceSceneLight(s:LightingSurface,V:vec3f,P:vec3f,pixel:vec2f)')
        old = '*terrainSunShadowAt(P)'
        assert text.count(old) == 1
        text = text.replace(old, old+'*sunShadow(P,N,pixel)')
        assert text.count('),input.V,input.P)') == 1
        text = text.replace('),input.V,input.P)', '),input.V,input.P,input.pixel)')
    assert 'fn interleavedGradientNoise(' not in text
    (target/name).write_text(text+'\n'+program, encoding='utf-8')
(target/'object-shadow.wgsl').write_text(program, encoding='utf-8')
fixture['object_shadow_probe'] = {'filter': 'source_pcss_pcf', 'frame_index': 124,
    'source_files': {str(p.relative_to(upstream)):hashlib.sha256(p.read_bytes()).hexdigest()
                     for p in (lighting_path,common_path)}}
fixture['preparation'][Path(__file__).name] = hashlib.sha256(Path(__file__).read_bytes()).hexdigest()
fixture['files'] = {p.name:hashlib.sha256(p.read_bytes()).hexdigest() for p in target.iterdir()}
(target/'fixture.json').write_text(json.dumps(fixture,indent=2),encoding='utf-8')
print(json.dumps({'output':str(target)}))
