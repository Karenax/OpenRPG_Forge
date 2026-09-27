"""Prepare all six source vegetation groups for the native development scene."""
import hashlib
import json
import runpy
import shutil
import sys
from pathlib import Path

source, template, destination = (Path(v).resolve() for v in sys.argv[1:4])
if destination.exists() or destination.is_relative_to(source) or destination.is_relative_to(template):
    raise SystemExit("new destination outside inputs required")
here = Path(__file__).resolve().parent
manifest = runpy.run_path(str(here / "verify-export.py"))["validate"](source)
fixture = json.loads((template / "fixture.json").read_text())
assert fixture["source"] == manifest["source"] and fixture.get("impostor_texture")
for name, expected in fixture["files"].items():
    assert Path(name).name == name
    assert hashlib.sha256((template / name).read_bytes()).hexdigest() == expected
destination.mkdir(parents=True)
for name in fixture["files"]:
    shutil.copyfile(template / name, destination / name)

def payload(path):
    data = (source / path).read_bytes()
    name = 'vegetation-' + hashlib.sha256(data).hexdigest() + '.bin'
    (destination / name).write_bytes(data)
    return name

runtime = json.loads((source / "vegetation/runtime.json").read_text())
kinds = {"veg-plant-leaf":0, "veg-canopy":1, "veg-impostor":2}
groups = []
for group in runtime["types"]:
    item = {key:group[key] for key in ("name", "count", "query_radius", "cell_size",
        "far_exclude_near", "near_range", "refresh_distance", "sort_far")}
    item["streams"] = {key:payload(group["streams"][key]["path"])
                       for key in ("iPos", "iDat", "matrices", "qr2")}
    item["levels"] = []
    for index, level in enumerate(group["levels"]):
        for mesh in level["meshes"]:
            item["levels"].append({"near":index == 0, "capacity":level["capacity"],
                "range":[level["lod_range"][k] for k in ('x','y','z')],
                "kind":kinds[mesh["material"]],
                "attributes":{k:payload(v["path"]) for k,v in mesh["attributes"].items()},
                "indices":payload(mesh["indices"]["path"])})
    groups.append(item)
(destination / "vegetation-scene.json").write_text(json.dumps({
    "schema":"forge.tidewater.vegetation-scene-probe", "version":1, "groups":groups},indent=2))

materials = json.loads((source / "materials/vegetation.json").read_text())
veg = next(m for m in materials["modules"] if m["name"] == "veg")
uniform = veg["bindings"]["vegParams"]["uniform"]
fields = uniform["fields"]
rows = fields["lobeTable"]["value"]
row_code = ','.join('vec4f('+','.join(str(float(x)) for x in row)+')' for row in rows)
near = fields["canopyNear"]["value"]
setup = f"\nfn setupSourceLobes(){{vegParams.lobeTable=array<vec4f,{len(rows)}>({row_code});"
setup += f"vegParams.canopyNear=vec2f({float(near['x'])},{float(near['y'])});}}\n"
common = (source / "materials/modules/common.wgsl").read_text().split('// ---- depth (reversed-Z')[0]
module_sets = [
    ('lodFade','veg','vegPlant'),
    ('lodFade','veg','vegCanopy','vegLeafAtlas','vegCanopyMask'),
    ('lodFade','veg','vegCanopy','vegImpostorSample')]
footer = (here / "plant-probe-footer.wgsl").read_text()
footer = footer.replace('let model=mesh_functions::get_world_from_local(v.instance);',
    'let tag=mesh_functions::get_tag(v.instance); let model=sourceModels[tag]; let record=sourceInstances[tag];')
footer = footer.replace('source.iPos=probe.iPos; source.iDat=probe.iDat;',
                        'source.iPos=record.iPos; source.iDat=record.iDat;')
shader = ''
for material_name, kind in kinds.items():
    material = next(m for m in materials['materials'] if m['name'] == material_name)
    shader += f'\n#ifdef PLANT_KIND_{kind}\n'
    shader += (here / 'plant-probe-header.wgsl').read_text() + '\n' + uniform['wgsl'] + common + setup
    shader += '\nstruct PlantRecord { iPos:vec4f, iDat:vec4f };\n'
    shader += '@group(#{MATERIAL_BIND_GROUP}) @binding(8) var<storage,read> sourceInstances:array<PlantRecord>;\n'
    shader += '@group(#{MATERIAL_BIND_GROUP}) @binding(9) var<storage,read> sourceModels:array<mat4x4f>;\n'
    for module in module_sets[kind]: shader += '\n' + (source / f'materials/modules/{module}.wgsl').read_text()
    shader += '\nfn sourceVertex(v:ptr<function,SourceVertex>,o:ptr<function,ProbeOut>){\n' + material['vertex'] + '\n}\n'
    shader += '\nfn sourceSurface(in:FragInput,s:ptr<function,SourceSurface>){\n' + material['surface'] + '\n}\n'
    shader += footer + '\n#endif\n'
(destination / 'plant.wgsl').write_text(shader,encoding='utf-8')
fixture['limitations'] += ['fixed_camera_vegetation_selection', 'no_grass_or_source_lighting']
fixture['preparation'][Path(__file__).name] = hashlib.sha256(Path(__file__).read_bytes()).hexdigest()
fixture['files'] = {p.name:hashlib.sha256(p.read_bytes()).hexdigest() for p in destination.iterdir()}
(destination / 'fixture.json').write_text(json.dumps(fixture,indent=2),encoding='utf-8')
print(json.dumps({'groups':len(groups),'instances':sum(g['count'] for g in groups),
                  'levels':sum(len(g['levels']) for g in groups)}))
