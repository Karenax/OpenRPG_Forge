"""Append-only binary GLB derivative: replace only explicit LOD1 foliage mesh.

Uses Pillow/NumPy tooling, not new runtime dependencies. No source/kit overwrite.
The candidate deliberately uses non-mipmapped sampling pending LOD/mip qualification.
"""
import argparse
import copy
import hashlib
import io
import json
from pathlib import Path
import struct

import numpy as np
from PIL import Image


def sha(data):
    return hashlib.sha256(data).hexdigest()


def padded_png(data, tiles, tile_size):
    image = Image.open(io.BytesIO(data)).convert('RGBA')
    pixels = np.array(image)
    before_alpha = pixels[:, :, 3].copy()
    height = pixels.shape[0]
    # Extend nearest available RGB through four transparent texels, per tile.
    # Alpha and all originally covered RGB pixels remain byte-identical.
    for tile in tiles:
        x, bottom = tile['tile_xy']
        y = height - bottom - tile_size
        region = pixels[y:y+tile_size, x:x+tile_size]
        known = region[:, :, 3] > 0
        for _ in range(4):
            original_known = known.copy()
            for dy, dx in ((-1,0),(1,0),(0,-1),(0,1)):
                ys, xs = slice(max(0,dy), min(tile_size,tile_size+dy)), slice(max(0,dx), min(tile_size,tile_size+dx))
                yt, xt = slice(max(0,-dy), min(tile_size,tile_size-dy)), slice(max(0,-dx), min(tile_size,tile_size-dx))
                fill = original_known[ys,xs] & ~known[yt,xt]
                region[yt,xt,:3][fill] = region[ys,xs,:3][fill]
                known[yt,xt] |= fill
    if not np.array_equal(before_alpha, pixels[:, :, 3]):
        raise ValueError('padding changed coverage')
    result = io.BytesIO()
    Image.fromarray(pixels).save(result, format='PNG')
    return result.getvalue()


def gltf_vector(v):
    return (v[0], v[2], -v[1])


def normal_png_to_card_basis(data, encoding):
    """View bake uses +up; exported UV V and tangent.w=-1 use -up.

    glTF reconstructs B=cross(N,T)*w. Reflect the encoded Y component,
    not the image rows, to preserve the baked world-space lighting direction.
    This is specific to this view-space atlas contract, not arbitrary GLBs.
    """
    if encoding != 'linear_0.5N+0.5_card_right_up_front_facing_hemisphere':
        raise ValueError('explicit view-space normal encoding required')
    pixels = np.array(Image.open(io.BytesIO(data)).convert('RGBA'))
    pixels[:, :, 1] = 255 - pixels[:, :, 1]
    output = io.BytesIO()
    Image.fromarray(pixels).save(output, format='PNG')
    return output.getvalue()


def crop_tiles(report, albedo):
    """Keep every nonzero-alpha texel plus one texel of linear-filter support."""
    pixels = np.array(Image.open(io.BytesIO(albedo)).convert('RGBA'))
    size = report['layout']['size']
    if pixels.shape != (size,size,4): raise ValueError('unexpected atlas dimensions')
    full_area, cropped_area = 0.0, 0.0
    for tile in report['tiles']:
        x,y = tile['content_xy']
        width = tile['content_size']
        region = pixels[size-y-width:size-y,x:x+width,3][::-1]
        ys,xs = np.nonzero(region)
        if not len(xs): raise ValueError('empty card may not be silently removed')
        x0,y0 = max(0,int(xs.min())-1), max(0,int(ys.min())-1)
        x1,y1 = min(width,int(xs.max())+2), min(width,int(ys.max())+2)
        tile['uv_rect'] = [x0/width,y0/width,x1/width,y1/width]
        if np.count_nonzero(region[y0:y1,x0:x1]) != len(xs):
            raise ValueError('crop discarded alpha coverage')
        area = tile['card_size_m']**2
        full_area += area
        cropped_area += area*(x1-x0)*(y1-y0)/width**2
    return {'full_card_area_m2':full_area,'cropped_card_area_m2':cropped_area,
            'area_ratio':cropped_area/full_area,'linear_filter_margin_texels':1}


def card_geometry(report):
    positions, normals, tangents, uvs, indices = [], [], [], [], []
    size = report['layout']['size']
    for tile in report['tiles']:
        center = np.asarray(tile['center_blender_m'])
        right, up, front = np.asarray(tile['basis_blender'])
        if not np.allclose(np.cross(right, up), front, atol=1e-7):
            raise ValueError('card basis must be right-handed')
        start = len(positions)
        width = tile['card_size_m']
        x, y = tile['content_xy']
        content = tile['content_size']
        u0,v0,u1,v1 = tile.get('uv_rect',(0,0,1,1))
        if not (0<=u0<u1<=1 and 0<=v0<v1<=1): raise ValueError('invalid crop rectangle')
        for u, v in ((u0,v0),(u1,v0),(u1,v1),(u0,v1)):
            positions.append(gltf_vector(center + width*((u-.5)*right + (v-.5)*up)))
            normals.append(gltf_vector(front))
            tangents.append((*gltf_vector(right), -1.0))
            uvs.append(((x+u*content)/size, 1-(y+v*content)/size))
        indices.extend(start+i for i in (0,1,2,0,2,3))
    return positions, normals, tangents, uvs, indices


def build(report, source_bytes, image_bytes):
    magic, version, total, json_size, kind = struct.unpack_from('<5I', source_bytes)
    if (magic, version, total, kind) != (0x46546c67, 2, len(source_bytes), 0x4e4f534a):
        raise ValueError('invalid GLB')
    original = json.loads(source_bytes[20:20+json_size])
    model = copy.deepcopy(original)
    bin_size, bin_type = struct.unpack_from('<2I', source_bytes, 20+json_size)
    binary_start = 28+json_size
    if bin_type != 0x004e4942 or binary_start+bin_size != len(source_bytes):
        raise ValueError('exactly one binary chunk required')
    if len(model.get('buffers', [])) != 1 or any('uri' in i for i in model.get('buffers', [])+model.get('images', [])):
        raise ValueError('embedded single buffer required')
    name = report['source_object'].replace('_LOD0_', '_LOD1_')
    nodes = [n for n in model['nodes'] if n.get('name') == name]
    if len(nodes) != 1 or nodes[0].get('extras', {}).get('forge_lod') != 1:
        raise ValueError('explicit unique LOD1 node required')
    node = nodes[0]
    mesh_index = node['mesh']
    if (any(k in node for k in ('matrix','translation','rotation','scale','skin')) or
        any(node in [model['nodes'][c] for c in n.get('children', [])] for n in model['nodes']) or
        sum(n.get('mesh') == mesh_index for n in model['nodes']) != 1):
        raise ValueError('candidate supports only an untransformed, unshared root foliage mesh')
    old_mesh = model['meshes'][mesh_index]
    if not all(model['materials'][p['material']].get('extras', {}).get('forge_foliage_alpha') for p in old_mesh['primitives']):
        raise ValueError('target is not exclusively foliage')
    binary = bytearray(source_bytes[binary_start:])

    def view(data, target=None):
        binary.extend(b'\0' * (-len(binary) % 4))
        entry = {'buffer': 0, 'byteOffset': len(binary), 'byteLength': len(data)}
        if target: entry['target'] = target
        binary.extend(data)
        model['bufferViews'].append(entry)
        return len(model['bufferViews'])-1

    def accessor(values, width, component, semantic):
        array = np.asarray(values, dtype='<f4' if component == 5126 else '<u4')
        entry = {'bufferView': view(array.tobytes(), 34963 if width == 1 else 34962),
                 'componentType': component, 'count': len(values),
                 'type': 'SCALAR' if width == 1 else f'VEC{width}'}
        if semantic == 'POSITION':
            entry.update(min=array.min(axis=0).tolist(), max=array.max(axis=0).tolist())
        model['accessors'].append(entry)
        return len(model['accessors'])-1

    sampler = len(model.setdefault('samplers', []))
    model['samplers'].append({'magFilter':9729, 'minFilter':9729, 'wrapS':33071, 'wrapT':33071})
    textures = {}
    for channel in ('albedo', 'normal', 'orm'):
        model['images'].append({'bufferView':view(image_bytes[channel]), 'mimeType':'image/png'})
        model['textures'].append({'sampler':sampler, 'source':len(model['images'])-1})
        textures[channel] = len(model['textures'])-1
    material = len(model['materials'])
    model['materials'].append({'name':'FoliageClusterAtlasCandidate', 'alphaMode':'BLEND', 'doubleSided':True,
        'pbrMetallicRoughness':{'baseColorTexture':{'index':textures['albedo']},
                              'metallicRoughnessTexture':{'index':textures['orm']}},
        'normalTexture':{'index':textures['normal']},
        'extras':{'forge_foliage_alpha':True, 'forge_foliage_alpha_mode':'blend',
                  'forge_atlas_candidate':True, 'forge_mips_qualified':False}})
    positions, normals, tangents, uvs, indices = card_geometry(report)
    model['meshes'][mesh_index] = {'name':old_mesh.get('name', name), 'primitives':[{
        'attributes': {key:accessor(values,width,5126,key) for key,values,width in
                       (('POSITION',positions,3),('NORMAL',normals,3),('TANGENT',tangents,4),('TEXCOORD_0',uvs,2))},
        'indices':accessor(indices,1,5125,'INDICES'), 'material':material, 'mode':4}]}
    binary.extend(b'\0' * (-len(binary) % 4))
    model['buffers'][0]['byteLength'] = len(binary)
    # Exact preservation of every original byte and JSON field except this mesh
    # and the buffer length; append-only resources do not modify other LODs.
    restored = copy.deepcopy(model)
    restored['meshes'][mesh_index] = original['meshes'][mesh_index]
    restored['buffers'] = original['buffers']
    for key in ('bufferViews','accessors','images','textures','materials','samplers'):
        if key in original: restored[key] = restored[key][:len(original[key])]
        else: restored.pop(key, None)
    if restored != original or binary[:bin_size] != source_bytes[binary_start:]:
        raise ValueError('non-foliage source data changed')
    encoded = json.dumps(model, separators=(',', ':')).encode()
    encoded += b' ' * (-len(encoded) % 4)
    result = struct.pack('<5I', 0x46546c67, 2, 28+len(encoded)+len(binary), len(encoded), 0x4e4f534a)
    result += encoded + struct.pack('<2I',len(binary),0x004e4942) + binary
    previous = sum(original['accessors'][p['indices']]['count']//3 for p in old_mesh['primitives'])
    return result, {'triangles_before':previous, 'triangles_after':len(indices)//3,
                    'cards':len(positions)//4, 'vertices':len(positions), 'mesh_index':mesh_index,
                    'original_binary_prefix_preserved':True, 'other_json_preserved':True}


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--atlas', type=Path, required=True)
    parser.add_argument('--sha256', required=True)
    parser.add_argument('--output-dir', type=Path, required=True)
    parser.add_argument('--crop', action='store_true', help='trim only zero-alpha card borders')
    args = parser.parse_args()
    report_bytes = args.atlas.read_bytes()
    if sha(report_bytes) != args.sha256.lower(): raise ValueError('atlas report hash mismatch')
    report = json.loads(report_bytes)
    if (report.get('schema') != 'forge.foliage-atlas-study' or report.get('schema_version') != 1 or
        report.get('pixel_origin') != 'bottom-left' or report['layout']['tile_size'] != 64 or
        report['layout']['size'] > 2048): raise ValueError('unsupported atlas')
    output = args.output_dir.resolve()
    if output.exists() or output.drive.lower() != 'e:': raise ValueError('fresh E: output required')
    source = Path(report['source'])
    source_bytes = source.read_bytes()
    if sha(source_bytes) != report['source_sha256']: raise ValueError('source changed')
    kit = json.loads((source.parent.parent/'build.json').read_text())
    asset = next(a for a in kit['assets'] if a['glb']['sha256'] == sha(source_bytes))
    if asset['id'] in kit.get('authored_source', {}).get('assets', []):
        raise ValueError('authored trees may not be replaced implicitly')
    images, crop = {}, None
    for record in report['images']:
        name = record['path']
        if Path(name).name != name: raise ValueError('image path must be a basename')
        data = (args.atlas.parent/name).read_bytes()
        if sha(data) != record['sha256']: raise ValueError('atlas image changed')
        if record['channel'] == 'normal':
            data = normal_png_to_card_basis(data, report.get('normal_encoding'))
        if args.crop and record['channel'] == 'albedo':
            crop = crop_tiles(report,data)
        images[record['channel']] = padded_png(data, report['tiles'], report['layout']['tile_size'])
    if set(images) != {'albedo','normal','orm'}: raise ValueError('all three material maps required')
    result, counts = build(report, source_bytes, images)
    if result != build(report, source_bytes, images)[0]: raise ValueError('non-deterministic GLB')
    if sha(source.read_bytes()) != report['source_sha256']: raise ValueError('source changed during export')
    output.mkdir(parents=True, exist_ok=False)
    name = asset['id']+'.glb'
    (output/name).write_bytes(result)
    evidence = {'schema':'forge.foliage-atlas-lod-candidate','schema_version':1,
                'asset_id':asset['id'], 'source':str(source), 'source_sha256':sha(source_bytes),
                'atlas':str(args.atlas), 'atlas_sha256':sha(report_bytes),
                'script_sha256':sha(Path(__file__).read_bytes()), 'glb':name, 'glb_sha256':sha(result),
                'derived_maps':{k:sha(v) for k,v in images.items()}, **counts,
                'crop':crop,
                'normal_basis':'view_right_up_front_to_gltf_right_minus_up_front',
                'sampler':'linear_without_mips_candidate_only', 'runtime_promoted':False,
                'human_accepted':False, 'remaining':['glTF validation','Blender reimport/render',
                    'mip/LOD transition qualification','governed kit and runtime comparison']}
    (output/'derivation.json').write_text(json.dumps(evidence,indent=2)+'\n',encoding='utf-8')
    print(json.dumps(counts))


if __name__ == '__main__':
    main()
