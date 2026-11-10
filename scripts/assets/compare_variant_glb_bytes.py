"""Conservative byte-level GLB triage; never authorizes a deletion."""
import argparse
import csv
import hashlib
import json
import pathlib
import struct

import vault_storage

MAX_MODEL = 256 * 1024 * 1024


def hash_bytes(data):
    return hashlib.sha256(data).hexdigest()


def signature(raw):
    if len(raw) < 28 or raw[:4] != b'glTF' or struct.unpack_from('<II', raw, 4) != (2, len(raw)):
        raise ValueError('invalid GLB header')
    length, chunk = struct.unpack_from('<II', raw, 12)
    if chunk != 0x4e4f534a or length > 16 * 1024 * 1024 or length % 4:
        raise ValueError('invalid GLB JSON chunk')
    end = 20 + length
    doc = vault_storage.decode_json(raw[20:end])
    binary_length, binary_type = struct.unpack_from('<II', raw, end)
    if binary_type != 0x004e4942 or end + 8 + binary_length != len(raw):
        raise ValueError('unsupported GLB chunks')
    data = memoryview(raw)[end + 8:]
    if doc.get('animations') or doc.get('skins'):
        raise ValueError('animated or skinned model needs dedicated comparison')
    if len(doc.get('buffers', [])) != 1 or 'uri' in doc['buffers'][0]:
        raise ValueError('external or multiple buffers')

    def view(index):
        d = doc['bufferViews'][index]
        if d.get('buffer', 0) != 0:
            raise ValueError('external buffer view')
        start, size = d.get('byteOffset', 0), d['byteLength']
        if not isinstance(start, int) or not isinstance(size, int) or min(start, size) < 0 or start + size > len(data):
            raise ValueError('buffer view out of bounds')
        return {'sha256': hash_bytes(data[start:start + size]), 'byteLength': size,
                'byteStride': d.get('byteStride'), 'target': d.get('target')}

    def accessor(index):
        a = doc['accessors'][index]
        if 'sparse' in a or 'bufferView' not in a:
            raise ValueError('sparse or missing accessor view')
        return {'layout': {k: v for k, v in a.items() if k not in ('bufferView', 'name')},
                'view': view(a['bufferView'])}

    meshes = []
    for mesh in doc.get('meshes', []):
        primitives = []
        for p in mesh['primitives']:
            if p.get('targets') or p.get('extensions'):
                raise ValueError('morph targets or compressed primitives need dedicated comparison')
            primitives.append({'attributes': {k: accessor(v) for k, v in sorted(p['attributes'].items())},
                               'indices': accessor(p['indices']) if 'indices' in p else None,
                               'mode': p.get('mode', 4), 'material': p.get('material')})
        meshes.append(primitives)
    images = []
    for image in doc.get('images', []):
        if 'uri' in image or 'bufferView' not in image:
            raise ValueError('external image')
        images.append({'mimeType': image.get('mimeType'), 'view': view(image['bufferView'])})
    # Strict comparisons deliberately include node/scene extras and all extension data.
    transform_keys = ('mesh', 'children', 'matrix', 'rotation', 'translation', 'scale', 'skin', 'weights')
    transforms = [{k: n[k] for k in transform_keys if k in n} for n in doc.get('nodes', [])]
    return {'geometry': {'meshes': meshes, 'nodes': doc.get('nodes'), 'scenes': doc.get('scenes'),
                         'scene': doc.get('scene'), 'extensions': doc.get('extensions')},
            'mesh_payloads': meshes,
            'transforms': {'nodes': transforms, 'scenes': doc.get('scenes'), 'scene': doc.get('scene')},
            'materials': {'materials': doc.get('materials'), 'textures': doc.get('textures'),
                          'samplers': doc.get('samplers'), 'extensionsUsed': doc.get('extensionsUsed'),
                          'extensionsRequired': doc.get('extensionsRequired')},
            'images': images, 'private_extensions': sorted(x for x in doc.get('extensionsUsed', []) if x.startswith('OPENRPG_'))}


def run(vault, pairs, output, limit, start=0, max_model=MAX_MODEL, ids=None):
    vault = pathlib.Path(vault).resolve()
    pairs, output = pathlib.Path(pairs), pathlib.Path(output)
    registry = vault / 'metadata/asset-import-registry.v1.json'
    expected_registry = json.loads((pairs.parent / 'resume.json').read_bytes())['registry_sha256']
    if hash_bytes(registry.read_bytes()) != expected_registry:
        raise ValueError('catalogue changed; regenerate pair inventory')
    with pairs.open(encoding='utf-8-sig', newline='') as f:
        rows = [r for r in csv.DictReader(f) if r['same_path'] == 'False']
    if ids is not None:
        expected_ids = set(ids)
        rows = [r for r in rows if r['id'] in expected_ids]
        if {r['id'] for r in rows} != expected_ids:
            raise ValueError('requested model ids absent from pair inventory')
    rows = rows[start:]
    if limit is not None:
        rows = rows[:limit]
    if output.exists():
        raise ValueError('output exists')
    with output.open('x', encoding='utf-8', newline='\n') as stream:
        for i, row in enumerate(rows, 1):
            if (i == 1 or i % 20 == 0) and hash_bytes(registry.read_bytes()) != expected_registry:
                raise ValueError('catalogue changed during audit; stop and regenerate inventory')
            result = {'id': row['id'], 'main_path': row['main_path'], 'variant_path': row['variant_path'],
                      'main_sha': row['main_sha'], 'variant_sha': row['variant_sha'],
                      'status': 'unverified', 'deletable_verified': False}
            try:
                signatures = []
                for prefix in ('main', 'variant'):
                    path = pathlib.Path(row[prefix + '_path']).resolve(strict=True)
                    if not path.is_relative_to(vault):
                        raise ValueError('model outside Vault')
                    raw = vault_storage.read_bytes(vault, path, max_model)
                    actual = hash_bytes(raw)
                    if actual != row[prefix + '_sha']:
                        raise ValueError(prefix + ' logical SHA mismatch')
                    signatures.append(signature(raw))
                result.update(status='compared', geometry_exact=signatures[0]['geometry'] == signatures[1]['geometry'],
                              mesh_payloads_exact=signatures[0]['mesh_payloads'] == signatures[1]['mesh_payloads'],
                              transforms_exact=signatures[0]['transforms'] == signatures[1]['transforms'],
                              materials_exact=signatures[0]['materials'] == signatures[1]['materials'],
                              images_exact=signatures[0]['images'] == signatures[1]['images'],
                              main_image_count=len(signatures[0]['images']), variant_image_count=len(signatures[1]['images']),
                              main_private_extensions=signatures[0]['private_extensions'],
                              variant_private_extensions=signatures[1]['private_extensions'])
            except (OSError, ValueError, KeyError, IndexError, struct.error, TypeError) as error:
                result['reason'] = str(error)
            stream.write(json.dumps(result, ensure_ascii=False) + '\n'); stream.flush()
            print(f'{i}/{len(rows)} {result["status"]} {row["id"]}', flush=True)
    if hash_bytes(registry.read_bytes()) != expected_registry:
        raise ValueError('catalogue changed during audit; results require revalidation')


if __name__ == '__main__':
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument('--vault', type=pathlib.Path, required=True)
    p.add_argument('--pairs', type=pathlib.Path, required=True)
    p.add_argument('--output', type=pathlib.Path, required=True)
    p.add_argument('--limit', type=int)
    p.add_argument('--start', type=int, default=0, help='zero-based offset among distinct-path pairs')
    p.add_argument('--max-model-mib', type=int, default=256, help='bounded logical model read budget')
    p.add_argument('--retry-jsonl', type=pathlib.Path, help='retry unverified ids from earlier JSONL')
    a = p.parse_args()
    if (a.limit is not None and a.limit < 1) or a.start < 0 or not 1 <= a.max_model_mib <= 1024:
        p.error('invalid limit, offset, or memory budget')
    ids = None
    if a.retry_jsonl:
        with a.retry_jsonl.open(encoding='utf-8') as stream:
            ids = [row['id'] for row in map(json.loads, stream) if row['status'] != 'compared']
    run(a.vault, a.pairs, a.output, a.limit, a.start, a.max_model_mib * 1024 * 1024, ids)
