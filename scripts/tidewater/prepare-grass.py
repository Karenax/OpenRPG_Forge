"""Add original grass programs/data to the lit native scene fixture."""
import hashlib
import json
import shutil
import sys
from pathlib import Path

source, export, target = (Path(v).resolve() for v in sys.argv[1:4])
assert not target.exists() and not target.is_relative_to(source) and not target.is_relative_to(export)
fixture = json.loads((source/'fixture.json').read_text())
grass = json.loads((export/'grass-source.json').read_text())
assert grass['source_commit'] == fixture['source']['commit']
assert grass['camera'] == fixture['camera_pose'] and grass['height_sha256'] == fixture['files']['heights.bin']
for folder, files in ((source,fixture['files']),(export,grass['files'])):
    for name, digest in files.items():
        assert Path(name).name == name
        assert hashlib.sha256((folder/name).read_bytes()).hexdigest() == digest
shader = (source/'plant.wgsl').read_text()
prefix, library = shader.split('fn F_Schlick(',1)
first = prefix.split('#ifdef PLANT_KIND_0\n',1)[1].split('\n#endif',1)[0]
header = first[:first.index('fn sourceVertex(')]
header = header.replace('struct SourceVertex {', 'struct SourceVertex { iCell:vec4f,aSide:vec4f,aSlot:vec4f,aBlade:vec4f,')
header = header.replace('struct ProbeOut {', '''struct ProbeOut {
 @location(11) vVegGrass:vec4f,@location(12) vVegGrassTone:vec4f,
 @location(13) vVegGrass2:vec4f,@location(14) vVegGust:f32,''')
header += '''
@group(#{MATERIAL_BIND_GROUP}) @binding(13) var grassHeights:texture_2d<f32>;
@group(#{MATERIAL_BIND_GROUP}) @binding(14) var grassMask:texture_2d<f32>;
'''
header += (export/'grass-module-vegGrass.wgsl').read_text()
footer = first[first.index('@vertex'):]
footer = footer.replace('source.aVeg=v.aVeg; source.aMat=v.aMat; source.aLobe=v.aLobe;',
    'source.iCell=record.iPos;source.aSide=v.aVeg;source.aSlot=v.aMat;source.aBlade=v.aLobe;')
body = '\nfn sourceVertex(v:ptr<function,SourceVertex>,o:ptr<function,ProbeOut>){\n'+grass['material']['vertex']+'\n}\n'
body += '\nfn sourceSurface(in:FragInput,s:ptr<function,SourceSurface>){\n'+grass['material']['surface']+'\n}\n'
shader = prefix+'\n#ifdef PLANT_KIND_3\n'+header+body+footer+'\n#endif\nfn F_Schlick('+library
target.mkdir(parents=True)
for name in fixture['files']: shutil.copyfile(source/name,target/name)
for name in grass['files']: shutil.copyfile(export/name,target/name)
for level in grass['levels']:
    level['attributes'] = {dict(aSide='aVeg',aSlot='aMat',aBlade='aLobe').get(k,k):v for k,v in level['attributes'].items()}
    level.update(near=True,range=[0,10000,10001],kind=3)
grass.pop('material');grass.pop('files')
(target/'grass.json').write_text(json.dumps(grass,indent=2))
(target/'plant.wgsl').write_text(shader,encoding='utf-8')
fixture['grass_probe'] = 'original_patches_mask_and_native_cell_selection'
fixture['preparation'][Path(__file__).name] = hashlib.sha256(Path(__file__).read_bytes()).hexdigest()
fixture['files'] = {p.name:hashlib.sha256(p.read_bytes()).hexdigest() for p in target.iterdir()}
(target/'fixture.json').write_text(json.dumps(fixture,indent=2))
print(json.dumps({'output':str(target)}))
