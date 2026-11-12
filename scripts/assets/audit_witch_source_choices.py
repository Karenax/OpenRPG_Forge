"""Triage Witch House conversions from matching Unity sources; no deletion.

Both GLBs may differ. The selected version is a catalogue choice, not proof of
visual equivalence. Historical GLBs and provenance must remain reconstructible.
"""
import argparse
import copy
import hashlib
import json
import math
import collections
import struct
import re
from pathlib import Path

import audit_variant_material_choices as materials
import vault_storage as storage


def matching_sources(main, variant, allow_vertex_count_difference=False):
    if not main.get('license') or main.get('license') != variant.get('license'):
        raise ValueError('license differs or is missing')
    a, b = main.get('source_prefab'), variant.get('source_prefab')
    if not isinstance(a, dict) or not isinstance(b, dict) or not storage.valid_hex(a.get('sha256'), 64) or a['sha256'] != b.get('sha256'):
        raise ValueError('source prefab SHA differs or is missing')
    def fbx(asset):
        entries = asset.get('source_fbx')
        if not isinstance(entries, list) or not entries:
            raise ValueError('source FBX missing')
        pairs = []
        for entry in entries:
            if not isinstance(entry, dict) or not storage.valid_hex(entry.get('sha256'), 64) or not entry.get('guid'):
                raise ValueError('source FBX pin missing')
            pairs.append((entry['guid'], entry['sha256']))
        return sorted(pairs)
    if fbx(main) != fbx(variant):
        raise ValueError('source FBX differs')
    a, b = main.get('measurement', {}), variant.get('measurement', {})
    for key in ('vertices', 'triangles'):
        if (type(a.get(key)) is not int or type(b.get(key)) is not int
                or min(a[key], b[key]) <= 0
                or (a[key] != b[key] and (key != 'vertices' or not allow_vertex_count_difference))):
            raise ValueError('geometry count differs')
    aa, bb = a.get('bounds'), b.get('bounds')
    if not (isinstance(aa, list) and isinstance(bb, list) and len(aa) == len(bb) == 2
            and all(isinstance(x, list) and len(x) == 3 for x in aa + bb)):
        raise ValueError('bounds missing')
    values = [(float(aa[i][j]), float(bb[i][j])) for i in range(2) for j in range(3)]
    if any(not math.isfinite(x) or not math.isfinite(y) or abs(x-y) > 1e-4 for x,y in values):
        raise ValueError('bounds differ')


def select(main, variant):
    a, b = copy.deepcopy(main), copy.deepcopy(variant)
    if any(x.get('animations') or x.get('skins') for x in (a,b)):
        raise ValueError('animated model requires review')
    aa, bb = a.get('materials', []), b.get('materials', [])
    if not aa or len(aa) != len(bb):
        raise ValueError('material slots differ or missing')
    for left, right in zip(aa,bb):
        name = left.get('name', '')
        other = right.get('name')
        if (not isinstance(other,str) or not name.endswith('__PBR_DIAGNOSTIC')
                or name.removesuffix('__PBR_DIAGNOSTIC') != re.sub(r'\.\d{3}$','',other)):
            raise ValueError('material base name differs')
        left['name'] = right['name']
    result = materials.choose(a,b)
    if result['keep_side'] != 'main':
        raise ValueError('material controls prefer variant')
    result['limitations'] = 'Same pinned sources and measured bounds; different conversions/materials are not visually equivalent.'
    return result


def triangle_positions(raw):
    """Canonical oriented position triangles; reject unsupported glTF layouts."""
    if len(raw) < 28 or raw[:4] != b'glTF' or struct.unpack_from('<II',raw,4) != (2,len(raw)):
        raise ValueError('invalid GLB')
    size, kind = struct.unpack_from('<II',raw,12)
    if kind != 0x4e4f534a or size > 16*1024*1024 or size % 4:
        raise ValueError('invalid JSON chunk')
    doc = storage.decode_json(raw[20:20+size]); offset = 20+size
    length, kind = struct.unpack_from('<II',raw,offset)
    if kind != 0x004e4942 or offset+8+length != len(raw):
        raise ValueError('invalid binary chunk')
    binary = memoryview(raw)[offset+8:]
    if doc.get('animations') or doc.get('skins') or len(doc.get('buffers',[])) != 1 or 'uri' in doc['buffers'][0]:
        raise ValueError('unsupported animated or external GLB')
    def accessor(index, shape, formats):
        a=doc['accessors'][index]
        if a.get('type') != shape or a.get('componentType') not in formats or 'sparse' in a or a.get('normalized'):
            raise ValueError('unsupported accessor')
        v=doc['bufferViews'][a['bufferView']]
        if v.get('buffer',0)!=0:raise ValueError('external buffer')
        fmt= formats[a['componentType']]; count=3 if shape=='VEC3' else 1
        width=struct.calcsize('<'+fmt*count);stride=v.get('byteStride',width)
        base=v.get('byteOffset',0); off=a.get('byteOffset',0); n=a['count']
        if (type(n) is not int or n<1 or n>10_000_000 or stride<width or min(base,off)<0
                or off+(n-1)*stride+width>v['byteLength'] or base+v['byteLength']>len(binary)):
            raise ValueError('invalid accessor bounds')
        return [struct.unpack_from('<'+fmt*count,binary,base+off+i*stride) for i in range(n)]
    result=[];total=0
    for mesh in doc.get('meshes',[]):
        groups=[]
        for primitive in mesh.get('primitives',[]):
            if primitive.get('mode',4)!=4 or primitive.get('extensions') or primitive.get('targets'):
                raise ValueError('unsupported primitive')
            vertices=accessor(primitive['attributes']['POSITION'],'VEC3',{5126:'f'})
            indices=accessor(primitive['indices'],'SCALAR',{5121:'B',5123:'H',5125:'I'})
            if len(indices)%3:raise ValueError('non-triangle indices')
            total+=len(indices)//3
            if total>2_000_000:raise ValueError('triangle budget exceeded')
            triangles=collections.Counter()
            for i in range(0,len(indices),3):
                tri=[]
                for j in range(3):
                    index=indices[i+j][0]
                    if index>=len(vertices):raise ValueError('index out of range')
                    point=vertices[index]
                    if any(not math.isfinite(x) for x in point):raise ValueError('nonfinite position')
                    tri.append(tuple(round(float(x),5) for x in point))
                triangles[min(tuple(tri),tuple(tri[1:]+tri[:1]),tuple(tri[2:]+tri[:2]))]+=1
            groups.append(triangles)
        result.append(groups)
    if not result:raise ValueError('no meshes')
    return result


def audit(vault, source, candidates, compare_triangles=False):
    vault = vault.resolve(strict=True)
    src, cand = source.read_bytes(), candidates.read_bytes()
    inv, doc = json.loads(src), json.loads(cand)
    registry_path = storage.confined(vault, vault/'metadata/asset-import-registry.v1.json')
    registry = registry_path.read_bytes()
    if (storage.sha(registry) != inv['registry_sha256'] or doc.get('mode') not in ('witch-source-candidates','witch-additional-candidates')
            or doc['source_sha256'] != storage.sha(src) or doc['registry_sha256'] != inv['registry_sha256']):
        raise ValueError('registry or inventory pin differs')
    assets = {}
    for entry in json.loads(registry)['packs'].values():
        p = storage.confined(vault, vault/entry['path']/'details.v1.json')
        raw = p.read_bytes()
        if storage.sha(raw) != entry['files']['details.v1.json']:
            raise ValueError('active metadata changed')
        for item in json.loads(raw)['assets']:
            if item['id'] in assets:
                raise ValueError('duplicate active identity')
            assets[item['id']] = item
    eligible = {x['variant_id']: x for x in doc['candidates']
                if x['main']['storage'] in ('raw','descriptor') and x['variant']['storage'] == 'descriptor'
                and (x['main']['storage'] == 'raw' or x['main']['declared_pin_matches'])
                and x['variant']['declared_pin_matches']
                and not x['variant_has_aliases']
                and all(x[k]['logical_bytes'] <= 512*1024*1024 for k in ('main','variant'))}
    results = []
    for row in inv['pairs']:
        if row['variant_id'] not in eligible:
            continue
        result = {'main_id': row['main_id'], 'variant_id': row['variant_id']}
        try:
            main, variant = (assets[row[k+'_id']] for k in ('main','variant'))
            for key, item in (('main',main), ('variant',variant)):
                if item.get('model_path') != row[key+'_path'] or item.get('model_sha256') != row[key+'_sha']:
                    raise ValueError('model pin differs')
            matching_sources(main,variant,compare_triangles)
            if compare_triangles:
                if row.get('transforms_exact')!='True':
                    raise ValueError('scene transforms differ')
                raw=[]
                for k in ('main','variant'):
                    data=storage.read_bytes(vault,storage.confined(vault,Path(row[k+'_path'])),512*1024*1024)
                    if hashlib.sha256(data).hexdigest()!=row[k+'_sha']:
                        raise ValueError('model SHA differs')
                    raw.append(data)
                if triangle_positions(raw[0])!=triangle_positions(raw[1]):
                    raise ValueError('oriented position triangles differ')
            docs = [materials.glb_document(vault, storage.confined(vault,Path(row[k+'_path']))) for k in ('main','variant')]
            result.update(state='candidate_only', **select(*docs))
        except (OSError, ValueError, KeyError, TypeError) as error:
            result.update(state='review_required', reason=str(error))
        results.append(result)
    if registry_path.read_bytes()!=registry or source.read_bytes()!=src or candidates.read_bytes()!=cand:
        raise ValueError('registry or inventory changed')
    return {'schema':'openrpg.material-choice-audit.v1','state':'audit_only',
            'registry_sha256':inv['registry_sha256'],'source_sha256':storage.sha(src),
            'candidate_inventory_sha256':storage.sha(cand),'pack':'witch-house','offset':0,
            'results':results,'compare_triangles':compare_triangles,
            'limitations':'Declared sources and bounds compared; visual and material equivalence not established.'}


if __name__ == '__main__':
    p=argparse.ArgumentParser(description=__doc__)
    p.add_argument('--vault',type=Path,required=True);p.add_argument('--source',type=Path,required=True)
    p.add_argument('--candidates',type=Path,required=True);p.add_argument('--output',type=Path,required=True)
    p.add_argument('--compare-triangles',action='store_true')
    args=p.parse_args()
    if args.output.exists():p.error('output exists')
    result=audit(args.vault,args.source,args.candidates,args.compare_triangles)
    with args.output.open('x',encoding='utf8',newline='\n') as out:
        json.dump(result,out,ensure_ascii=False,indent=2);out.write('\n')
    print(json.dumps({'count':len(result['results']),'candidates':sum(x['state']=='candidate_only' for x in result['results'])}))
