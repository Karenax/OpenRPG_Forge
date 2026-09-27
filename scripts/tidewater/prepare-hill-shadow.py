"""Add source shadow compute and lookup programs to the combined scene fixture."""
import hashlib
import json
import shutil
import sys
from pathlib import Path

source, shadow, target = (Path(v).resolve() for v in sys.argv[1:4])
assert not target.exists() and not target.is_relative_to(source) and not target.is_relative_to(shadow)
fixture = json.loads((source/'fixture.json').read_text())
meta = json.loads((shadow/'shadow.json').read_text())
assert meta['source_commit'] == fixture['source']['commit']
assert meta['heights_sha256'] == fixture['files']['heights.bin']
assert meta['sun_direction'] == fixture['environment_lighting_probe']['sun_direction']
for folder, files in ((source,fixture['files']),(shadow,meta['files'])):
    for name, expected in files.items():
        assert Path(name).name == name
        assert hashlib.sha256((folder/name).read_bytes()).hexdigest() == expected
lookup = '@group(#{MATERIAL_BIND_GROUP}) @binding(12) var terrainSunShadowTex:texture_2d<f32>;\n' + (shadow/'hill-lookup.wgsl').read_text()
target.mkdir(parents=True)
for name in fixture['files']: shutil.copyfile(source/name,target/name)
for name in ('hill-shadow.wgsl','shadow.json'): shutil.copyfile(shadow/name,target/name)
for name in ('plant.wgsl','terrain.wgsl','village.wgsl'):
    text = (source/name).read_text()
    if name == 'plant.wgsl':
        color = 'vec3f('+','.join(repr(v) for v in fixture['environment_lighting_probe']['sun_color'])+')'
        old = 'frame.sunDir,'+color+')'
        assert text.count(old) == 3
        text = text.replace(old,'frame.sunDir,'+color+'*terrainSunShadowAt(input.P))')
    else:
        text = text.replace('fn sourceSceneLight(s:LightingSurface,V:vec3f)', 'fn sourceSceneLight(s:LightingSurface,V:vec3f,P:vec3f)')
        old = 's.translucency,N,V,'
        start = text.index(old)
        end = text.index(')', text.index('vec3f(', text.index('vec3f(', start)+6))+1
        # The direct-light color vec3 is the last argument; preserve indirect terms.
        text = text[:end]+'*terrainSunShadowAt(P)'+text[end:]
        text = text.replace('surface.emissive),input.V)', 'surface.emissive),input.V,input.P)')
        text = text.replace('s.emissive),input.V)', 's.emissive),input.V,input.P)')
    (target/name).write_text(text+'\n'+lookup,encoding='utf-8')
fixture['hill_shadow_probe'] = 'native_height_mips_and_source_compute_uniform_sun'
fixture['preparation'][Path(__file__).name] = hashlib.sha256(Path(__file__).read_bytes()).hexdigest()
fixture['files'] = {p.name:hashlib.sha256(p.read_bytes()).hexdigest() for p in target.iterdir()}
(target/'fixture.json').write_text(json.dumps(fixture,indent=2),encoding='utf-8')
print(json.dumps({'output':str(target)}))
