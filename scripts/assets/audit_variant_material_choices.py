"""Classify material choices for already-inventoried identical-mesh pairs.

Only GLB JSON prefixes and individually verified shared blocks are read here;
this is not a full logical SHA verification, visual qualification, or deletion.
"""
import argparse
import hashlib
import json
import struct
from pathlib import Path

import vault_storage

MAX_JSON = 32 * 1024 * 1024


def glb_document(vault, path):
    prefix = bytearray()
    for block in vault_storage.chunks(vault, path):
        prefix.extend(block)
        if len(prefix) >= 20:
            if prefix[:4] != b'glTF' or struct.unpack_from('<I', prefix, 4)[0] != 2:
                raise ValueError('invalid GLB header')
            length, kind = struct.unpack_from('<II', prefix, 12)
            if kind != 0x4e4f534a or length > MAX_JSON:
                raise ValueError('invalid or oversized GLB JSON')
            if len(prefix) >= 20 + length:
                return vault_storage.decode_json(bytes(prefix[20:20 + length]))
    raise ValueError('truncated GLB JSON')


def choose(main, variant):
    first, second = main.get('materials', []), variant.get('materials', [])
    if not isinstance(first, list) or not isinstance(second, list) or len(first) != len(second):
        raise ValueError('material slot count differs')
    directions, changes, controls = set(), [], []
    for index, (a, b) in enumerate(zip(first, second)):
        if not isinstance(a, dict) or not isinstance(b, dict) or a.get('name') != b.get('name'):
            raise ValueError('material names or ordering differ')
        modes = (a.get('alphaMode', 'OPAQUE'), b.get('alphaMode', 'OPAQUE'))
        doubles = (a.get('doubleSided', False), b.get('doubleSided', False))
        if any(x not in ('OPAQUE', 'MASK', 'BLEND') for x in modes) or any(type(x) is not bool for x in doubles):
            raise ValueError('unknown material control')
        def emission(material):
            factor = material.get('emissiveFactor', [0, 0, 0])
            extensions = material.get('extensions', {})
            if not isinstance(extensions, dict):
                raise ValueError('invalid material extensions')
            ext = extensions.get('KHR_materials_emissive_strength', {})
            if (not isinstance(factor, list) or len(factor) != 3
                    or any(type(x) not in (int, float) for x in factor) or not isinstance(ext, dict)):
                raise ValueError('invalid emissive controls')
            strength = ext.get('emissiveStrength', 1)
            if type(strength) not in (int, float):
                raise ValueError('invalid emissive strength')
            return [x * strength for x in factor]
        if emission(a) != emission(b):
            raise ValueError('emissive appearance differs')
        if modes == ('MASK', 'MASK') and a.get('alphaCutoff', 0.5) != b.get('alphaCutoff', 0.5):
            raise ValueError('alpha cutoff differs')
        controls.append({'slot': index, 'name': a.get('name'), 'main_alpha': modes[0],
                         'variant_alpha': modes[1], 'main_double': doubles[0], 'variant_double': doubles[1]})
        if modes[0] != modes[1]:
            if 'OPAQUE' not in modes:
                raise ValueError('MASK and BLEND are not interchangeable')
            directions.add('variant' if modes[0] == 'OPAQUE' else 'main')
            changes.append('alpha')
        if doubles[0] != doubles[1]:
            directions.add('main' if doubles[0] else 'variant')
            changes.append('double_sided')
    if len(directions) > 1:
        raise ValueError('alpha and double-sided preferences conflict')
    return {'keep_side': next(iter(directions), 'main'), 'preferred_controls': sorted(set(changes)),
            'controls': controls,
            'limitations': 'Images and other material values differ; this choice is not visual equivalence.'}


def audit(vault, source, candidates, pack, offset, limit):
    vault = vault.resolve(strict=True)
    source_raw, candidate_raw = source.read_bytes(), candidates.read_bytes()
    inventory, candidate_doc = json.loads(source_raw), json.loads(candidate_raw)
    if candidate_doc.get('mode') != 'identical-mesh' or candidate_doc['source_sha256'] != hashlib.sha256(source_raw).hexdigest():
        raise ValueError('candidate inventory mismatch')
    registry = vault_storage.confined(vault, vault / 'metadata/asset-import-registry.v1.json')
    before = registry.read_bytes()
    if hashlib.sha256(before).hexdigest() != inventory['registry_sha256']:
        raise ValueError('registry changed')
    eligible = {x['variant_id']: x for x in candidate_doc['candidates']
                if x['main']['storage'] == x['variant']['storage'] == 'descriptor'
                and x['main']['declared_pin_matches'] and x['variant']['declared_pin_matches']
                and all(x[side]['logical_bytes'] <= 512 * 1024 * 1024 for side in ('main', 'variant'))}
    rows = [x for x in inventory['pairs'] if x['variant_id'] in eligible
            and x['main_id'].split('.')[1] == pack][offset:offset + limit]
    if not rows:
        raise ValueError('no eligible candidates')
    results = []
    for index, row in enumerate(rows, 1):
        item = {'main_id': row['main_id'], 'variant_id': row['variant_id']}
        try:
            main = glb_document(vault, vault_storage.confined(vault, Path(row['main_path'])))
            variant = glb_document(vault, vault_storage.confined(vault, Path(row['variant_path'])))
            selected = choose(main, variant)
            retired = 'variant' if selected['keep_side'] == 'main' else 'main'
            if eligible[row['variant_id']][retired + '_has_aliases']:
                raise ValueError('retired card has existing historical aliases')
            item.update(state='candidate_only', **selected)
        except (OSError, ValueError, KeyError, struct.error) as error:
            item.update(state='review_required', reason=str(error))
        results.append(item)
        if index % 100 == 0 or index == len(rows):
            print(f'Classified {index}/{len(rows)} material pairs', flush=True)
    if registry.read_bytes() != before or source.read_bytes() != source_raw or candidates.read_bytes() != candidate_raw:
        raise ValueError('registry or input changed')
    return {'schema': 'openrpg.material-choice-audit.v1', 'state': 'audit_only',
            'registry_sha256': inventory['registry_sha256'], 'source_sha256': hashlib.sha256(source_raw).hexdigest(),
            'candidate_inventory_sha256': hashlib.sha256(candidate_raw).hexdigest(), 'pack': pack,
            'offset': offset, 'results': results,
            'limitations': 'JSON prefix and material controls only; full model SHA and actual render still require checks.'}


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--vault', type=Path, required=True)
    parser.add_argument('--source', type=Path, required=True)
    parser.add_argument('--candidates', type=Path, required=True)
    parser.add_argument('--pack', required=True)
    parser.add_argument('--offset', type=int, default=0)
    parser.add_argument('--limit', type=int, required=True)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    if args.offset < 0 or not 1 <= args.limit <= 2000 or args.output.exists():
        parser.error('invalid offset, limit or existing output')
    result = audit(args.vault, args.source, args.candidates, args.pack, args.offset, args.limit)
    with args.output.open('x', encoding='utf8', newline='\n') as stream:
        json.dump(result, stream, ensure_ascii=False, indent=2)
        stream.write('\n')
    print(json.dumps({'state': result['state'], 'count': len(result['results']),
                      'review_required': sum(x['state'] != 'candidate_only' for x in result['results'])}))
