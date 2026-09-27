"""Bind acquired source sky to the source vegetation BRDF, without scene shadows."""
import hashlib
import json
import shutil
import struct
import subprocess
import sys
from pathlib import Path

upstream, source, environment, target = (Path(v).resolve() for v in sys.argv[1:5])
pin = '1438b1abfcaee3267092b75573014f4d9b4a983c'
git = lambda *args: subprocess.check_output(['git', '-C', str(upstream), *args], text=True).strip()
assert git('rev-parse', 'HEAD') == pin and not git('status', '--porcelain')
if target.exists() or any(target.is_relative_to(p) for p in (upstream, source, environment)):
    raise SystemExit('new destination outside inputs required')
fixture = json.loads((source / 'fixture.json').read_text())
env = json.loads((environment / 'environment.json').read_text())
assert env['source_commit'] == fixture['source']['commit'] == pin
assert env['schema'] == 'forge.tidewater.environment-probe' and env['version'] == 1
assert env['night'] == 0 and env['environment_intensity'] == 1
for folder, files in ((source, fixture['files']), (environment, env['files'])):
    for name, expected in files.items():
        assert Path(name).name == name
        assert hashlib.sha256((folder / name).read_bytes()).hexdigest() == expected
pose = fixture['camera_pose']
assert pose['position'] == [env['camera']['position'][k] for k in ('x', 'y', 'z')]
assert pose['rotation'] == [env['camera']['quaternion'][k] for k in ('_x', '_y', '_z', '_w')]
cube = bytearray()
assert len(env['mips']) == 6
for face in range(6):
    for level, mip in enumerate(env['mips']):
        assert mip['level'] == level and mip['width'] == mip['height'] == 128 >> level
        assert len(mip['faces']) == 6
        data = (environment / mip['faces'][face]).read_bytes()
        assert len(data) == (128 >> level)**2 * 8
        assert all((v[0] & 0x7c00) != 0x7c00 for v in struct.iter_unpack('<H', data))
        cube.extend(data)
sh = struct.unpack('<36f', (environment / env['sh']).read_bytes())
vec = lambda values: 'vec3f(' + ','.join(repr(v) for v in values) + ')'
constants = 'const envSH=array<vec3f,9>(' + ','.join(vec(sh[i:i+3]) for i in range(0,36,4)) + ');\n'
raw = (upstream / 'src/sky/Environment.js').read_text()
start = raw.index('\nfn hookEnvDiffuse(') + 1
hook = raw[start:raw.index('\n}', start)+2].replace('frame.envIntensity', '1.0')
lighting = (upstream / 'src/engine/render/wgsl/lighting.js').read_text()
start = lighting.index('fn DFGApprox(')
helpers = lighting[start:lighting.index('\n`,', start)]
adapter = '''
@group(#{MATERIAL_BIND_GROUP}) @binding(10) var envCube:texture_cube<f32>;
@group(#{MATERIAL_BIND_GROUP}) @binding(11) var envSampler:sampler;
fn sourceEnvironmentLight(s:SourceSurface,N:vec3f,V:vec3f)->vec3f {
 let rough=clamp(s.roughness,0.03,1.0);
 let diffuseColor=s.albedo*(1.0-s.metalness);
 let specF0=mix(vec3f(0.04)*s.specularIntensity,s.albedo,s.metalness);
 let specF90=mix(s.specularIntensity,1.0,s.metalness);
 let R=reflect(-V,N);
 let Rr=normalize(mix(R,N,rough*rough));
 let envIrr=hookEnvDiffuse(N)*PI;
 let radiance=textureSampleLevel(envCube,envSampler,Rr,rough*5.0).rgb;
 var single=vec3f(0.0);var multi=vec3f(0.0);
 multiscatter(N,V,specF0,specF90,rough,&single,&multi);
 let totalScatter=single+multi;
 let diffuseMS=diffuseColor*(1.0-max(max(totalScatter.r,totalScatter.g),totalScatter.b));
 let dotNV=sat(dot(N,V));
 let specAO=sat(pow(dotNV+s.ao,exp2(-16.0*rough-1.0))-1.0+s.ao);
 return diffuseMS*envIrr*INV_PI*s.ao+(radiance*single+multi*envIrr*INV_PI)*specAO;
}
'''
shader = (source / 'plant.wgsl').read_text()
old = 'frame.sunDir,vec3f(1.0)),1.0);'
assert shader.count(old) == 3
shader = shader.replace(old, 'frame.sunDir,' + vec(env['sun_color']) + ')\n        +sourceEnvironmentLight(surface,normalize(surface.normal),input.V),1.0);')
assert shader.count('frame.sunDir=probe.sunNight.xyz;') == 3
shader = shader.replace('frame.sunDir=probe.sunNight.xyz;', 'frame.sunDir=' + vec(env['sun_direction']) + ';')
shader += '\n' + constants + hook + '\n' + helpers + adapter
target.mkdir(parents=True)
for name in fixture['files']:
    shutil.copyfile(source / name, target / name)
(target / 'plant.wgsl').write_text(shader, encoding='utf-8')
(target / 'environment-cube.bin').write_bytes(cube)
shutil.copyfile(environment / 'environment.json', target / 'source-environment.json')
fixture['environment_lighting_probe'] = {'acquisition': env['acquisition'], 'sun_direction': env['sun_direction'], 'sun_color': env['sun_color']}
fixture.pop('direct_lighting_probe', None)
fixture['limitations'] = [v for v in fixture['limitations'] if v not in (
    'unit_direct_light_only_not_global_scene_lighting', 'no_environment_or_shadow_lighting')]
fixture['limitations'] += ['frozen_environment_not_dynamic_sky', 'no_scene_shadows_or_bounce', 'cold_environment_not_baseline_replay']
fixture['preparation'][Path(__file__).name] = hashlib.sha256(Path(__file__).read_bytes()).hexdigest()
fixture['files'] = {p.name: hashlib.sha256(p.read_bytes()).hexdigest() for p in target.iterdir()}
(target / 'fixture.json').write_text(json.dumps(fixture, indent=2), encoding='utf-8')
print(json.dumps({'output': str(target), 'cube_bytes': len(cube)}))
