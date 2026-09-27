"""Extend the verified vegetation lighting to terrain and village surfaces."""
import hashlib
import json
import shutil
import sys
from pathlib import Path

source, target = (Path(v).resolve() for v in sys.argv[1:3])
if target.exists() or target.is_relative_to(source):
    raise SystemExit('new destination outside input required')
fixture = json.loads((source / 'fixture.json').read_text())
assert 'environment_lighting_probe' in fixture
for name, expected in fixture['files'].items():
    assert Path(name).name == name
    assert hashlib.sha256((source / name).read_bytes()).hexdigest() == expected
plant = (source / 'plant.wgsl').read_text()
assert plant.count('fn F_Schlick(') == 1
library = plant[plant.index('fn F_Schlick('):]
library = library.replace('s:SourceSurface', 's:LightingSurface')
library = library.replace('let envIrr=hookEnvDiffuse(N)*PI;', 'let envIrr=hookEnvDiffuse(N)*PI*s.envIntensity;')
library = library.replace('rough*5.0).rgb;', 'rough*5.0).rgb*s.envIntensity;')
env = fixture['environment_lighting_probe']
vec = lambda values: 'vec3f(' + ','.join(repr(v) for v in values) + ')'
library = '''
struct LightingSurface {
 albedo:vec3f,normal:vec3f,roughness:f32,metalness:f32,specularIntensity:f32,
 translucency:vec3f,ao:f32,envIntensity:f32,emissive:vec3f,
};
''' + library + '''
fn sourceSceneLight(s:LightingSurface,V:vec3f)->vec3f {
 let N=normalize(s.normal);
 return sourceDirectLight(s.albedo,s.roughness,s.metalness,s.specularIntensity,
     s.translucency,N,V,SOURCE_SUN_DIRECTION,SOURCE_SUN_COLOR)
     +sourceEnvironmentLight(s,N,V)+s.emissive;
}
'''
library = library.replace('SOURCE_SUN_DIRECTION', vec(env['sun_direction'])).replace('SOURCE_SUN_COLOR', vec(env['sun_color']))
here = Path(__file__).resolve().parent
terrain = (source / 'terrain.wgsl').read_text()
footer = (here / 'terrain-probe-footer.wgsl').read_text()
tail = footer[footer.index('    var pbr='):]
assert terrain.count(tail) == 1
terrain = terrain.replace(tail, '''    return vec4f(sourceSceneLight(LightingSurface(surface.albedo,surface.normal,
        surface.roughness,surface.metalness,1.0,vec3f(0.0),surface.ao,
        surface.envIntensity,surface.emissive),input.V),1.0);
}
''') + library
terrain = terrain.replace('frame.sunDir=settings.sun.xyz;', 'frame.sunDir=' + vec(env['sun_direction']) + ';')
village = (source / 'village.wgsl').read_text()
footer = (here / 'village-probe-footer.wgsl').read_text()
tail = footer[footer.index('    var pbr='):]
assert village.count(tail) == 1
village = village.replace(tail, '''    return vec4f(sourceSceneLight(LightingSurface(s.albedo,s.normal,s.roughness,
        s.metalness,1.0,vec3f(0.0),s.ao,s.envIntensity,s.emissive),input.V),s.alpha);
}
''') + library
target.mkdir(parents=True)
for name in fixture['files']:
    shutil.copyfile(source / name, target / name)
(target / 'terrain.wgsl').write_text(terrain, encoding='utf-8')
(target / 'village.wgsl').write_text(village, encoding='utf-8')
(target / 'scene-lighting.wgsl').write_text(library, encoding='utf-8')
fixture['scene_lighting_probe'] = 'shared_source_direct_and_acquired_environment_without_scene_occlusion'
fixture['limitations'] = [v for v in fixture['limitations'] if v not in (
    'terrain_and_village_still_bevy_lighting', 'source_village_materials_with_bevy_lighting')]
fixture['preparation'][Path(__file__).name] = hashlib.sha256(Path(__file__).read_bytes()).hexdigest()
fixture['files'] = {p.name: hashlib.sha256(p.read_bytes()).hexdigest() for p in target.iterdir()}
(target / 'fixture.json').write_text(json.dumps(fixture, indent=2), encoding='utf-8')
print(json.dumps({'output': str(target), 'status': fixture['scene_lighting_probe']}))
