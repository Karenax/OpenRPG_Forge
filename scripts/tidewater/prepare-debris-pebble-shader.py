"""Prepare original PebbleField stages for the shared Bevy Village receiver.

Usage: DEBRIS_PACKAGE BUILDING_PACKAGE NEW_OUTPUT
Cell storage uses bounded Bevy mesh tags; scene resource bindings remain live.
"""
import hashlib
import json
from pathlib import Path
import sys

from importlib.util import module_from_spec, spec_from_file_location


def sha(raw):
    return hashlib.sha256(raw).hexdigest()


def main():
    debris_root, building_root, output = map(lambda p: Path(p).resolve(), sys.argv[1:])
    assert not output.exists() and all(not output.is_relative_to(p) for p in [debris_root, building_root])
    raw = (debris_root / 'package.json').read_bytes()
    assert sha(raw) == '0a84592f5ccaa0b8077849b6cf054935f6ecb33f41aaf04a19cec3b42255189a'
    debris = json.loads(raw)
    building_raw = (building_root / 'package.json').read_bytes()
    assert sha(building_raw) == 'a53b6987563b0b0d5bb5b9113e1c3f27142baf53658a9e1a78338854f99599e6'
    building = json.loads(building_raw)
    assert debris['source_commit'] == building['source_commit'] == '1438b1abfcaee3267092b75573014f4d9b4a983c'
    spec = spec_from_file_location('nature_preparation', Path(__file__).with_name('prepare-debris-nature-shader.py'))
    helper = module_from_spec(spec)
    spec.loader.exec_module(helper)

    def read(root, manifest, entry):
        name = entry['file']
        assert Path(name).name == name
        assert manifest['files'][name] == {key: entry[key] for key in ['bytes', 'sha256']}
        data = (root / name).read_bytes()
        assert len(data) == entry['bytes'] and sha(data) == entry['sha256']
        return data

    contract = json.loads(read(debris_root, debris, debris['materials']))['source_contracts']['pebbles']
    assert contract['attributes'] == {'iCell': 'vec4f', 'aSlot': 'vec4f'}
    assert contract['varyings'] == {'vPebble': 'vec4f'}
    assert contract['defines'] == {'MATERIAL_SUN_MODULATION': 1}
    assert contract['side'] == 'front' and not contract['transparent'] and contract['depth_write']
    library = json.loads(read(building_root, building, building['material_library']))
    source = read(building_root, building, library['shader']).decode().replace('\r\n', '\n')
    prefix = source[:source.index('#ifdef VILLAGE_KIND_0\n')]
    last = source.index('#ifdef VILLAGE_KIND_6\n')
    tail = source[source.index('\n#endif', last) + len('\n#endif'):]
    assert '#ifdef VILLAGE_KIND_' not in prefix + tail
    prefix = prefix.replace('night:f32 }', 'night:f32,cameraPos:vec3f,frameIndex:u32 }')
    prefix = prefix.replace('frame.night=settings.flags.y;',
        'frame.night=settings.flags.y; frame.cameraPos=view.world_position; frame.frameIndex=shadowParams.frameIndex;')
    anchor = '@location(3) vTint:vec3f, @location(4) vData:vec4f,'
    assert prefix.count(anchor) == 1
    prefix = prefix.replace(anchor, anchor + '\n    @location(5) vPebble:vec4f,')
    anchor = 'struct SourceVertex { position:vec3f,normal:vec3f,uv:vec2f,tint:vec3f,vdata:vec4f };'
    assert prefix.count(anchor) == 1
    prefix = prefix.replace(anchor, anchor.replace('vdata:vec4f', 'vdata:vec4f,iCell:vec4f,aSlot:vec4f'))
    modules = {module['name']: module for module in contract['modules']}
    assert set(modules) == {'common', 'terrainShading', 'terrain', 'terrainSunModulation', 'debrisStone', 'debrisPebble'}
    programs = {name: read(debris_root, debris, module['code']).decode().replace('\r\n', '\n') for name,module in modules.items()}
    common = programs['common']
    start = common.index('fn perturbNormalByMap(')
    end = common.index('\n}', start) + 2
    common = common.split('// ---- depth (reversed-Z')[0] + common[start:end]
    terrain = programs['terrain']
    selected = '\n'.join(helper.function(terrain, name) for name in ['terrainUvOf', 'terrainHeightAt'])
    tail = tail.replace(helper.function(tail, 'terrainSunShadowAt'), helper.function(terrain, 'terrainSunShadowAt'))
    anchor = 'v.tint=input.tint;v.vdata=input.vdata;'
    assert tail.count(anchor) == 1
    tail = tail.replace(anchor, anchor + '\n    v.aSlot=input.vdata; v.iCell=pebbleCells.cells[mesh_functions::get_tag(input.instance)];')
    uniform = modules['terrain']['bindings']['terrainParams']['uniform']
    declarations = uniform['wgsl'] + '''
@group(#{MATERIAL_BIND_GROUP}) @binding(100) var terrainHeightTex:texture_2d<f32>;
@group(#{MATERIAL_BIND_GROUP}) @binding(101) var terrainDetailTex:texture_2d<f32>;
@group(#{MATERIAL_BIND_GROUP}) @binding(102) var smpAniso4Repeat:sampler;
@group(#{MATERIAL_BIND_GROUP}) @binding(103) var<uniform> terrainParams:TerrainParams;
@group(#{MATERIAL_BIND_GROUP}) @binding(104) var debrisPebbleMask:texture_2d<f32>;
@group(#{MATERIAL_BIND_GROUP}) @binding(105) var smpLinearClamp:sampler;
struct PebbleCells {cells:array<vec4f>};
@group(#{MATERIAL_BIND_GROUP}) @binding(106) var<storage,read> pebbleCells:PebbleCells;
'''
    vertex = read(debris_root, debris, contract['stages']['vertex']).decode()
    surface = read(debris_root, debris, contract['stages']['surface']).decode()
    shader = prefix + common + programs['terrainShading'] + selected + programs['debrisStone'] + programs['debrisPebble'] + declarations
    shader += '\nfn sourceVertex(v:ptr<function,SourceVertex>,o:ptr<function,Output>){\n' + vertex + '\n}\n'
    shader += 'fn sourceSurface(in:FragInput,s:ptr<function,Surface>){\n' + surface + '\n}\n' + tail
    assert shader.count('fn terrainSunShadowAt(') == 1 and 'materialSunModulation(' not in shader
    descriptor = {'schema':'forge.tidewater.debris-pebble-program','version':1,'source_commit':debris['source_commit'],
        'debris_package_sha256':sha(raw),'building_package_sha256':sha(building_raw),
        'preparation_sha256':sha(Path(__file__).read_bytes()),'shared_function_extractor_sha256':sha(Path(helper.__file__).read_bytes()),
        'shader':{'file':'pebbles.wgsl','bytes':len(shader.encode()),'sha256':sha(shader.encode())},
        'source_modules':{name:module['code']['sha256'] for name,module in modules.items()},
        'source_vertex_sha256':contract['stages']['vertex']['sha256'],'source_surface_sha256':contract['stages']['surface']['sha256'],
        'bindings':{'100':'live R32Float height','101':'actual detail mip chain','102':'repeat linear/aniso-4 sampler',
          '103':'live terrain domain and sun','104':'linear RGBA8 density mask, no mips','105':'linear clamp sampler',
          '106':'source iCell vec4 storage, tag-indexed, at most 240 cells'},
        'mesh_contract':'Identity model matrices; DATA=aSlot, tags index bounded live cells; no caster/prepass.',
        'limits':['Preparation only; no pipeline, binding upload, draw or visual parity qualification.']}
    output.mkdir()
    (output/'pebbles.wgsl').write_bytes(shader.encode())
    (output/'program.json').write_text(json.dumps(descriptor,indent=2),encoding='utf-8')
    print(json.dumps({'shader_bytes':len(shader.encode()),'sha256':descriptor['shader']['sha256'],'status':'prepared_not_rendered'}))


if __name__ == '__main__':
    main()
