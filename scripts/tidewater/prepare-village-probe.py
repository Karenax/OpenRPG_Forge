"""Add verified source village to an existing native terrain/vegetation fixture."""
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
assert fixture['source'] == manifest['source']
for name, digest in fixture['files'].items():
    assert Path(name).name == name and hashlib.sha256((template / name).read_bytes()).hexdigest() == digest
scene = json.loads((source / 'village/render.json').read_text())
textures = json.loads((source / 'village/textures.json').read_text())['textures']
shader = (here / 'village-probe-header.wgsl').read_text()
first = scene['materials'][0]['uniforms']
shader += first['wgsl'].replace(first['struct'],'SourceMaterialParams')
materials = []
for kind, material in enumerate(scene['materials']):
    assert not material['defines'] and material['uniforms']['fields'].keys() == first['fields'].keys()
    shader += f'\n#ifdef VILLAGE_KIND_{kind}\n'
    for slot, (name,binding) in enumerate(material['bindings'].items(),1):
        shader += f'@group(#{{MATERIAL_BIND_GROUP}}) @binding({slot}) var {name}:texture_2d<f32>;\n'
    for module_id in material['modules']:
        module = scene['modules'][module_id]
        code = (source / module['code']).read_text()
        if module['name'] == 'common':
            start = code.index('fn perturbNormalByMap(')
            end = code.index('\n}',start)+2
            code = code.split('// ---- depth (reversed-Z')[0] + code[start:end]
        shader += code + '\n'
    shader += 'fn sourceVertex(v:ptr<function,SourceVertex>,o:ptr<function,Output>){\n' + material['vertex'] + '\n}\n'
    shader += 'fn sourceSurface(in:FragInput,s:ptr<function,Surface>){\n' + material['surface'] + '\n}\n#endif\n'
    fields = {name:v['value'] for name,v in material['uniforms']['fields'].items()}
    rgb = lambda name:[fields[name][c] for c in ('r','g','b')]
    materials.append({'kind':kind,'textures':[b['texture'] for b in material['bindings'].values()],
        'color':rgb('color')+[fields['opacity']], 'emissive':rgb('emissive')+[fields['emissiveIntensity']],
        'surface':[fields[k] for k in ('roughness','metalness','alphaTest','envMapIntensity')]})
shader += (here / 'village-probe-footer.wgsl').read_text()
destination.mkdir(parents=True)
for name in fixture['files']: shutil.copyfile(template / name,destination / name)
def copy_payload(entry):
    path = source / entry['path']
    name = 'village-' + path.name
    if not (destination / name).exists(): shutil.copyfile(path,destination / name)
    return name
geometries = {}
for identity, geometry in scene['geometries'].items():
    geometries[identity] = {'attributes':{n:copy_payload(e) for n,e in geometry['attributes'].items()},
        'indices':copy_payload(geometry['indices'])}
texture_meta = {}
for name, texture in textures.items():
    mips = texture['mips'];filename = f'village-texture-{name}.bin'
    (destination / filename).write_bytes(b''.join((source / mip['path']).read_bytes() for mip in mips))
    texture_meta[name] = {'file':filename,'width':mips[0]['width'],'height':mips[0]['height'],'mips':len(mips)}
nodes = [{**n,'material':next(i for i,m in enumerate(scene['materials']) if m['id']==n['material'])} for n in scene['nodes']]
(destination / 'village.json').write_text(json.dumps({'schema':'forge.tidewater.village-probe','version':1,
    'nodes':nodes,'geometries':geometries,'materials':materials,'textures':texture_meta},indent=2),encoding='utf-8')
(destination / 'village.wgsl').write_text(shader,encoding='utf-8')
fixture['limitations'] += ['source_village_materials_with_bevy_lighting','village_fish_props_missing','village_dynamic_groups_initial_pose']
fixture['preparation'].update({n:hashlib.sha256((here / n).read_bytes()).hexdigest() for n in
    ('prepare-village-probe.py','village-probe-header.wgsl','village-probe-footer.wgsl')})
fixture['files'] = {p.name:hashlib.sha256(p.read_bytes()).hexdigest() for p in destination.iterdir()}
(destination / 'fixture.json').write_text(json.dumps(fixture,indent=2),encoding='utf-8')
print(json.dumps({'output':str(destination),'nodes':len(nodes),'status':'village_native_fixture_not_scene_parity'}))
