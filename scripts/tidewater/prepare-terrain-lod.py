"""Extend a verified surface fixture with untouched source height/morph functions."""
import hashlib
import json
import runpy
import shutil
import sys
from pathlib import Path

source, template, destination = (Path(p).resolve() for p in sys.argv[1:])
here = Path(__file__).resolve().parent
assert not destination.exists() and not destination.is_relative_to(source) and not destination.is_relative_to(template)
manifest = runpy.run_path(str(here / 'verify-export.py'))['validate'](source)
fixture = json.loads((template / 'fixture.json').read_text())
assert fixture['source'] == manifest['source'] and 'terrain.wgsl' in fixture['files']
document = json.loads((template / 'terrain.json').read_text())
assert not document.get('height_deltas', []) and document['height_sampling']['base_elevation_m'] == 0
assert document['sampled_source']['samples_sha256'] == hashlib.sha256((source / 'terrain/heights.bin').read_bytes()).hexdigest()
for name, digest in fixture['files'].items():
    assert Path(name).name == name and hashlib.sha256((template / name).read_bytes()).hexdigest() == digest
code = (source / 'materials/terrain/modules/terrain.wgsl').read_text()
start = code.index('fn terrainHeightAt(')
end = code.index('\n}', start) + 2
shader = (here / 'terrain-lod-vertex.wgsl').read_text() + '\n' + code[start:end]
shader += (source / 'materials/terrain/modules/terrainLod.wgsl').read_text()
destination.mkdir(parents=True)
for name in fixture['files']:
    shutil.copyfile(template / name, destination / name)
surface = (destination / 'terrain.wgsl').read_text()
entry = 'fn fragment(vs:VertexOutput)->@location(0) vec4f {'
assert surface.count(entry) == 1
(destination / 'terrain.wgsl').write_text(surface.replace(entry,
    entry + '\n    if settings.flags.z > 0.5 { return vec4f(1.0); }'), encoding='utf-8')
(destination / 'terrain-lod.wgsl').write_text(shader, encoding='utf-8')
fixture['limitations'] += ['expanded_lod_mesh_not_production_instancing', 'static_camera_selection_only']
fixture['preparation'].update({n:hashlib.sha256((here / n).read_bytes()).hexdigest()
    for n in ('prepare-terrain-lod.py', 'terrain-lod-vertex.wgsl')})
fixture['files'] = {p.name:hashlib.sha256(p.read_bytes()).hexdigest() for p in destination.iterdir()}
(destination / 'fixture.json').write_text(json.dumps(fixture,indent=2),encoding='utf-8')
print(json.dumps({'output':str(destination),'status':'terrain_lod_probe_not_full_scene'}))
