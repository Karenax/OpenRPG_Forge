"""Attach pinned source material transport to a native geometry candidate.

Scene targets remain required host inputs. No captured frame targets become
PNG assets. This does not qualify camera mapping, GPU rendering or Studio.
"""
import argparse
import copy
import hashlib
import json
from pathlib import Path
import struct
import re


def sha(raw):
    return hashlib.sha256(raw).hexdigest()


def pinned(path, expected, maximum=64*1024*1024):
    if path.stat().st_size > maximum:
        raise ValueError('Input budget exceeded')
    raw = path.read_bytes()
    if sha(raw) != expected:
        raise ValueError(f'Input SHA differs: {path}')
    return raw


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ['geometry', 'textures', 'draw', 'shadergraph', 'output']:
        parser.add_argument('--'+name, type=Path, required=True)
    for name in ['geometry-sha256', 'textures-sha256', 'draw-sha256', 'shadergraph-sha256']:
        parser.add_argument('--'+name, required=True)
    args = parser.parse_args()
    if args.output.exists():
        raise ValueError('Preserve previous output')
    raw = pinned(args.geometry, args.geometry_sha256)
    if struct.unpack_from('<III', raw) != (0x46546c67, 2, len(raw)):
        raise ValueError('Geometry GLB header invalid')
    n = struct.unpack_from('<I', raw, 12)[0]
    doc = json.loads(raw[20:20+n])
    binary = bytearray(raw[28+n:])
    original_binary = bytes(binary)
    manifest_raw = pinned(args.textures, args.textures_sha256, 1024*1024)
    manifest = json.loads(manifest_raw)
    draw_raw = pinned(args.draw, args.draw_sha256, 1024*1024)
    draw = json.loads(draw_raw)
    graph = pinned(args.shadergraph, args.shadergraph_sha256)
    def blob(data):
        binary.extend(b'\0'*(-len(binary)%4))
        index = len(doc['bufferViews'])
        doc['bufferViews'].append(dict(buffer=0, byteOffset=len(binary), byteLength=len(data)))
        binary.extend(data)
        return dict(buffer_view=index, sha256=sha(data))
    replacements = []
    for resource in manifest['resources']:
        p = (args.textures.parent/resource['path']).resolve()
        if not p.is_relative_to(args.textures.parent.resolve()):
            raise ValueError('Texture resource escapes package root')
        data = pinned(p, resource['sha256'])
        if len(data) != resource['byte_length']:
            raise ValueError('Texture resource length differs')
        replacements.append(blob(data))
    def remap(value):
        if isinstance(value, dict):
            if set(value) == {'buffer_view', 'sha256'}:
                actual = replacements[value['buffer_view']]
                if actual['sha256'] != value['sha256']:
                    raise ValueError('Texture view hash differs')
                return actual
            return {k:remap(v) for k,v in value.items()}
        if isinstance(value, list):
            return [remap(v) for v in value]
        return value
    textures = remap(manifest['textures'])
    if len(textures) != 3 or len(draw['textures']) != 5:
        raise ValueError('This captured profile requires3 authored and2 scene inputs')
    def draw_member(pin):
        p = (args.draw.parent/pin['file']).resolve()
        if not p.is_relative_to(args.draw.parent.resolve()):
            raise ValueError('Draw resource outside source root')
        return pinned(p, pin['sha256'])
    stage_raw = draw_member(draw['render_shader'])
    stage = blob(stage_raw)
    bank = draw_member(draw['render_parameters'])
    projection = draw_member(draw['vertex_projection']['shader']).decode('utf-8')
    for channel in range(3):
        lane = 10+channel*2
        expected = f'let v{channel+3}=vec4<f32>(source_float(vertex,{lane}u),source_float(vertex,{lane+1}u),0.0,1.0);'
        if expected not in projection:
            raise ValueError('Original source UV vec4 expansion is not verified')
    source_view = doc['extras']['original_stream']['buffer_view']
    raw_view = doc['bufferViews'][source_view]
    native = original_binary[raw_view['byteOffset']:raw_view['byteOffset']+raw_view['byteLength']]
    if sha(native) != doc['extras']['original_stream']['sha256'] or len(native)%64:
        raise ValueError('Original source stream differs')
    count = len(native)//64
    raw_view.update(byteStride=64,target=34962)
    source_attributes = {}
    for name,lane,kind in [('_OPENRPG_SOURCE_NORMAL',3,'VEC3'),('_OPENRPG_SOURCE_TANGENT',6,'VEC4')]:
        source_attributes[name] = len(doc['accessors'])
        doc['accessors'].append(dict(bufferView=source_view,byteOffset=lane*4,componentType=5126,count=count,type=kind))
    for channel in range(3):
        expanded = b''.join(struct.pack('<4f',row[10+channel*2],row[11+channel*2],0.0,1.0)
                            for row in struct.iter_unpack('<16f',native))
        uv = blob(expanded)
        doc['bufferViews'][uv['buffer_view']]['target'] = 34962
        name = f'_OPENRPG_SOURCE_UV{channel}'
        source_attributes[name] = len(doc['accessors'])
        doc['accessors'].append(dict(bufferView=uv['buffer_view'],componentType=5126,count=count,type='VEC4'))
    if len(bank) != 256 or struct.unpack_from('<I', bank, 240)[0] != 0xffffffff:
        raise ValueError('Captured compiler bank/control layout differs')
    words = sorted({int(lane)*4+'xyzw'.index(component)
                    for lane,component in re.findall(r'bitcast<u32>\(bank\.d\[(\d+)u\]\.([xyzw])\)',stage_raw.decode('utf-8'))})
    if words != [41,42,54,60]:
        raise ValueError('Compiler bank unsigned-control reads differ from the qualified profile')
    scalar = dict(names=[f'compiler.bank.lane{i}' for i in range(64)], values=blob(bank), word_lanes=words)
    scene = [dict(slot=3, resource_key='source.decal.buffer0', format=draw['textures'][3]['format'],
                  dimensions=dict(kind='viewport'), sampler=draw['textures'][3]['sampler']),
             dict(slot=4, resource_key='source.preintegrated.brdf', format=draw['textures'][4]['format'],
                  dimensions=dict(kind='fixed', width=draw['textures'][4]['width'], height=draw['textures'][4]['height']),
                  sampler=draw['textures'][4]['sampler'])]
    material = dict(schema_version=7, source_program_sha256=sha(graph),
                    source_material_sha256=doc['extras']['source_prefab']['sha256'],
                    forward=stage, prepass=stage, libraries=[], textures=textures, scene_textures=scene,
                    state=dict(color_write_mask=15,color_blend=[1,0],alpha_blend=[1,0],
                               forward_cull=2,prepass_cull=2,depth_compare=4,write_depth=True,
                               transparent=False,alpha_test=False,alpha_to_coverage=False,
                               render_queue=2000,disabled_passes=[]),
                    requires_original_vertex_color=False,normal_decode_modes=[None]*8,scalar_parameters=scalar)
    doc['materials'] = [dict(name='Pinned source effect transport', extensions=dict(OPENRPG_source_graph=material))]
    for mesh in doc['meshes']:
        for primitive in mesh['primitives']:
            primitive['material'] = 0
            primitive['attributes'].update(source_attributes)
    doc['extensionsUsed'] = ['OPENRPG_source_graph']
    doc['extensionsRequired'] = ['OPENRPG_source_graph']
    doc['extras'].update(qualification='source_material_transport_candidate',material_required=True,
                        source_material_document_kind='unity_visual_effect_component_property_sheet',
                        original_shadergraph=blob(graph), original_draw_manifest=blob(draw_raw),
                        current_camera_mapping_qualified=False,scene_inputs_required=True)
    doc['buffers'][0]['byteLength'] = len(binary)
    encoded = json.dumps(doc,separators=(',',':')).encode()
    encoded += b' '*(-len(encoded)%4)
    binary.extend(b'\0'*(-len(binary)%4))
    result = (struct.pack('<III',0x46546c67,2,28+len(encoded)+len(binary))
              +struct.pack('<II',len(encoded),0x4e4f534a)+encoded
              +struct.pack('<II',len(binary),0x004e4942)+binary)
    if binary[:len(original_binary)] != original_binary:
        raise ValueError('Geometry/native provenance bytes changed')
    args.output.parent.mkdir(parents=True,exist_ok=True)
    args.output.write_bytes(result)
    receipt = dict(status='SOURCE_MATERIAL_CARRIER_PACKAGED_SCENE_CAMERA_GPU_PENDING',
                   output=str(args.output),output_sha256=sha(result),geometry_sha256=sha(raw),
                   texture_manifest_sha256=sha(manifest_raw),draw_manifest_sha256=sha(draw_raw),
                   shadergraph_sha256=sha(graph),geometry_original_binary_prefix_unchanged=True,
                   authored_textures=3,scene_inputs=2,bank_word_lanes=words,
                   stage_shader_unchanged=True,bank_unchanged=True,converter_sha256=sha(Path(__file__).read_bytes()),
                   GPU_qualified=False,Studio_integrated=False,goal_complete=False)
    args.output.with_suffix('.PACKAGING.v1.json').write_text(json.dumps(receipt,indent=2)+'\n')
    print(json.dumps(receipt))


if __name__ == '__main__':
    main()
