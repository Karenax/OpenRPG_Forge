"""Four UV/TBN diagnostics; preserve all existing geometry and texture bytes."""
from pathlib import Path
from datetime import datetime, timezone
import json
import struct
import hashlib
import math

W = Path('F:/OpenRPG_ImportWork')
OLD = W/'firepit-surface-instance-fix-case-v1'
ROOT = W/'firepit-varying-native-case-v1'
SOURCE = W/'firepit-source-varying-debug-v1'
ROLES = ['Texcoord0', 'VertexTangentWS', 'VertexBitangentWS', 'VertexNormalWS']
HDRP = W/'unity-dungeon-original-shadergraph-hdrp-startup-v4/project/Library/PackageCache/com.unity.render-pipelines.high-definition@fc55a8abadc2/Runtime'

def read(p):
    return json.loads(p.read_text('utf-8-sig'))

def sha(p):
    with Path(p).open('rb') as f:
        return hashlib.file_digest(f, 'sha256').hexdigest()

def diagnostic(text):
    start = text.index(' let surface_mode=(u32(in.position.x)')
    end = text.index(' return out;', start)
    witness = ''' let varying_mode=(u32(in.position.x)+512u*u32(in.position.y))%4u;
 let factor=1.0/length(in.world_normal);
 let mapping=source_graph_linear(source_graph.source_world_from_target_world);
 let source_n=mapping*in.world_normal*factor;
 let source_t=mapping*in.world_tangent.xyz*factor;
 let source_b=mapping*(select(-1.0,1.0,in.world_tangent.w>0.0)*cross(in.world_normal,in.world_tangent.xyz))*factor;
 var varying_value=in.source_uv0.xyz;
 switch varying_mode {
  case 1u:{varying_value=source_t*0.5+vec3<f32>(0.5);}
  case 2u:{varying_value=source_b*0.5+vec3<f32>(0.5);}
  case 3u:{varying_value=source_n*0.5+vec3<f32>(0.5);}
  default:{}
 }
 out.color=vec4<f32>(source_surface_debug_linear_from_srgb(varying_value),0.5);
'''
    if 'out.color=vec4<f32>(surface_value,0.25);' in text[start:end]:
        witness = witness.replace(',0.5);\n', ',0.25);\n')
    return text[:start]+witness+text[end:]

def main():
    assert not ROOT.exists()
    run = read(SOURCE/'RUN-RESULT.v1.json')
    source_prep = read(SOURCE/'PREPARED.v1.json')
    assert run['actual_exit_code'] == 0 and run['source_inputs_unchanged'] and not run['private_changed']
    assert len(source_prep['dependency_paths']) == 24
    prior = read(OLD/'RESULT.v1.json')
    case = read(OLD/'CASE.v1.json')
    pins = dict(prior['input_pins'])
    pins.update(source_prep['input_pins'])
    for path, expected in pins.items():
        assert sha(path) == expected, path
    refs = []
    for role in ROLES:
        path = SOURCE/f'DEBUG-{role}.rgba16f'
        assert sha(path) == run['debug_files'][str(path)]
        values = list(struct.iter_unpack('<4e', path.read_bytes()))
        assert len(values) == 512*512 and all(all(math.isfinite(x) for x in p) for p in values)
        refs.append(dict(role=role, path=str(path), sha256=sha(path)))
        pins[str(path)] = sha(path)
    for view in range(2):
        for rel in [f'images/{view}.png', f'linear-{view}.rgba16f']:
            assert sha(SOURCE/rel) == sha(W/'firepit-shadow-source-reference-v8'/rel)
            pins[str(SOURCE/rel)] = sha(SOURCE/rel)
    fixture = Path(case['fixture'])
    assert sha(fixture) == case['fixture_sha256']
    data = fixture.read_bytes()
    length = struct.unpack_from('<I', data, 12)[0]
    document = json.loads(data[20:20+length])
    binary = bytearray(data[28+length:])
    original = bytes(binary)
    changes = {}
    for material in document['materials']:
        graph = material['extensions']['OPENRPG_source_graph']
        item = graph['forward']
        key = (item['buffer_view'], item['sha256'])
        if key not in changes:
            view = document['bufferViews'][item['buffer_view']]
            raw = bytes(binary[view.get('byteOffset',0):view.get('byteOffset',0)+view['byteLength']])
            assert hashlib.sha256(raw).hexdigest() == item['sha256']
            shader = diagnostic(raw.decode()).encode()
            while len(binary)%4:
                binary.append(0)
            changes[key] = dict(buffer_view=len(document['bufferViews']),sha256=hashlib.sha256(shader).hexdigest())
            document['bufferViews'].append(dict(buffer=0,byteOffset=len(binary),byteLength=len(shader)))
            binary.extend(shader)
        graph['forward'] = changes[key].copy()
    while len(binary)%4:
        binary.append(0)
    assert bytes(binary[:len(original)]) == original
    document['buffers'][0]['byteLength'] = len(binary)
    encoded = json.dumps(document,separators=(',',':')).encode()
    encoded += b' '*((-len(encoded))%4)
    ROOT.mkdir()
    output = ROOT/'private-varying-diagnostic.glb'
    output.write_bytes(struct.pack('<III',0x46546c67,2,28+len(encoded)+len(binary))+struct.pack('<II',len(encoded),0x4e4f534a)+encoded+struct.pack('<II',len(binary),0x004e4942)+binary)
    receiver = {}
    for stage, item in case['receiver_program'].items():
        path = Path(item['path'])
        assert sha(path) == item['sha256']
        target = ROOT/f'private-receiver-{stage}.wgsl'
        target.write_text(diagnostic(path.read_text('utf-8')) if stage == 'forward' else path.read_text('utf-8'),'utf-8')
        receiver[stage] = dict(path=str(target),sha256=sha(target))
        pins[str(target)] = sha(target)
    case.update(fixture=str(output),fixture_sha256=sha(output),receiver_program=receiver,source_surface_debug_refs=refs,varying_diagnostic_only=True)
    case_path = ROOT/'CASE.v1.json'
    case_path.write_text(json.dumps(case,indent=2),'utf-8')
    for p in [Path(__file__).resolve(),fixture,OLD/'RESULT.v1.json',OLD/'CASE.v1.json',SOURCE/'RUN-RESULT.v1.json',SOURCE/'PREPARED.v1.json',HDRP/'RenderPipeline/ShaderPass/FragInputs.hlsl',HDRP/'Debug/DebugDisplayMaterial.hlsl',case_path,output]:
        pins[str(p)] = sha(p)
    result = dict(status='four_source_uv_tbn_debug_case_prepared_before_native_pixels',created_utc=datetime.now(timezone.utc).isoformat(),private_fixture=str(output),private_fixture_sha256=sha(output),source_surface_debug_refs=refs,model_alpha=.5,receiver_alpha=.25,maximum_encoded_surface_mae=2/255,
        source_dependencies_verified=24,source_reference_images_byte_identical=True,source_diagnostic_values_verified=4*512*512,
        source_binary_prefix_unchanged=True,source_binary_bytes=len(original),input_pins=pins,thresholds_not_fitted_after_pixels=True,
        receiver_uv_geometry_parity_expected=False,full_import=False,editor_visual_verified=False)
    (ROOT/'RESULT.v1.json').write_text(json.dumps(result,indent=2),'utf-8')
    print(json.dumps({k:v for k,v in result.items() if k not in ['input_pins','source_surface_debug_refs']}))

if __name__ == '__main__':
    main()
