"""Move acquired village light constants to scene-owned material uniforms."""
from pathlib import Path
import hashlib
import json
import os
import re
import sys

source, target = map(Path, sys.argv[1:])
assert not target.exists()
fixture = json.loads((source / 'fixture.json').read_text())
for name, digest in fixture['files'].items():
    assert Path(name).name == name
    assert hashlib.sha256((source / name).read_bytes()).hexdigest() == digest
shader = (source / 'village.wgsl').read_text()
match = re.search(r'const envSH=array<vec3f,9>\((.*?)\);', shader)
assert match
sh = [[float(x) for x in value.split(',')]
      for value in re.findall(r'vec3f\(([^)]+)\)', match.group(1))]
assert len(sh) == 9
lighting = dict(fixture['environment_lighting_probe'], environment_sh=sh)
shader = shader[:match.start()] + shader[match.end():]
shader = shader.replace('envSH[', 'sceneLighting.environmentSH[')
for key, field in [('sun_direction', 'sunDirection'), ('sun_color', 'sunColor')]:
    literal = 'vec3f(' + ','.join(repr(v) for v in lighting[key]) + ')'
    assert literal in shader
    shader = shader.replace(literal, 'sceneLighting.' + field + '.xyz')
shader += '''
struct VillageSceneLighting {
    sunDirection: vec4f,
    sunColor: vec4f,
    environmentSH: array<vec4f,9>,
};
@group(#{MATERIAL_BIND_GROUP}) @binding(18) var<uniform> sceneLighting: VillageSceneLighting;
'''
target.mkdir(parents=True)
for name in fixture['files']:
    if name != 'village.wgsl':
        os.link(source / name, target / name)
(target / 'village.wgsl').write_text(shader, encoding='utf-8')
(target / 'village-lighting.json').write_text(json.dumps(lighting, indent=2), encoding='utf-8')
fixture['files'] = {p.name: hashlib.sha256(p.read_bytes()).hexdigest() for p in target.iterdir()}
fixture['preparation'][Path(__file__).name] = hashlib.sha256(Path(__file__).read_bytes()).hexdigest()
fixture['village_scene_lighting_uniform'] = True
(target / 'fixture.json').write_text(json.dumps(fixture, indent=2), encoding='utf-8')
print(target)
