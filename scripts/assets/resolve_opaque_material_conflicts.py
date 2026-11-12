"""Resolve mask/double-sided conflicts only when both base textures are opaque.

This reads and hashes both complete logical GLBs. Different RGB textures and
other material values still mean the two appearances are not equivalent.
"""
import argparse
import hashlib
import json
from pathlib import Path

import vault_storage
from audit_variant_material_choices import choose
from audit_viking_alpha_opaque import glb_document, texture_alpha_range


def resolve(main, variant, main_bin, variant_bin, cache):
    try:
        choose(main, variant)
    except ValueError as error:
        if str(error) != 'alpha and double-sided preferences conflict':
            raise
    else:
        raise ValueError('material pair is no longer a conflict')
    for doc in (main, variant):
        if any(str(x).startswith('OPENRPG_') for x in doc.get('extensionsUsed', [])):
            raise ValueError('private rendering extension requires review')
        for mesh in doc.get('meshes', []):
            for primitive in mesh.get('primitives', []):
                if 'COLOR_0' in primitive.get('attributes', {}):
                    raise ValueError('vertex color may affect alpha')
    directions, slots = set(), []
    for i, (a, b) in enumerate(zip(main['materials'], variant['materials'])):
        modes = (a.get('alphaMode', 'OPAQUE'), b.get('alphaMode', 'OPAQUE'))
        doubles = (a.get('doubleSided', False), b.get('doubleSided', False))
        if doubles[0] != doubles[1]:
            directions.add('main' if doubles[0] else 'variant')
        if modes[0] != modes[1]:
            if set(modes) != {'OPAQUE', 'MASK'}:
                raise ValueError('unsupported alpha modes')
            for doc, binary, material in ((main, main_bin, a), (variant, variant_bin, b)):
                cutoff = material.get('alphaCutoff', 0.5)
                if type(cutoff) not in (int, float) or not 0 <= cutoff <= 1:
                    raise ValueError('invalid alpha cutoff')
                if texture_alpha_range(doc, binary, material, cache) != (255, 255):
                    raise ValueError('base texture alpha is not fully opaque')
            slots.append(i)
    if len(directions) != 1 or not slots:
        raise ValueError('no unique double-sided choice')
    return {'keep_side': directions.pop(), 'preferred_controls': ['double_sided'],
            'opaque_alpha_slots': slots,
            'limitations': 'Both base textures are fully opaque; RGB and other material values may differ.'}


def audit(vault, source, candidates, choices):
    vault = vault.resolve(strict=True)
    source_raw, candidate_raw, choice_raw = source.read_bytes(), candidates.read_bytes(), choices.read_bytes()
    inventory, previous = json.loads(source_raw), json.loads(choice_raw)
    registry = vault_storage.confined(vault, vault / 'metadata/asset-import-registry.v1.json')
    before = registry.read_bytes()
    if (previous.get('schema') != 'openrpg.material-choice-audit.v1' or previous.get('state') != 'audit_only'
            or previous.get('source_sha256') != hashlib.sha256(source_raw).hexdigest()
            or previous.get('candidate_inventory_sha256') != hashlib.sha256(candidate_raw).hexdigest()
            or previous.get('registry_sha256') != inventory['registry_sha256']
            or hashlib.sha256(before).hexdigest() != inventory['registry_sha256']):
        raise ValueError('inventory or choices are stale')
    rows = {x['variant_id']: x for x in inventory['pairs']}
    eligibility = {x['variant_id']: x for x in json.loads(candidate_raw)['candidates']}
    cache, count = {}, 0
    for item in previous['results']:
        if item.get('reason') != 'alpha and double-sided preferences conflict':
            continue
        row = rows.get(item['variant_id'])
        if not row or row['main_id'] != item['main_id']:
            raise ValueError('candidate identity changed')
        try:
            docs = []
            for side in ('main', 'variant'):
                path = vault_storage.confined(vault, Path(row[side + '_path']))
                raw = vault_storage.read_bytes(vault, path, 512 * 1024 * 1024)
                if hashlib.sha256(raw).hexdigest() != row[side + '_sha']:
                    raise ValueError('logical model SHA changed')
                docs.append(glb_document(raw))
            result = resolve(docs[0][0], docs[1][0], docs[0][1], docs[1][1], cache)
            old_side = 'variant' if result['keep_side'] == 'main' else 'main'
            if eligibility[item['variant_id']][old_side + '_has_aliases']:
                raise ValueError('retired card has existing historical aliases')
            item.pop('reason', None)
            item.update(state='candidate_only', **result)
        except (OSError, ValueError, KeyError, IndexError) as error:
            item['reason'] = 'opacity review: ' + str(error)
        count += 1
        if count % 25 == 0:
            print(f'Checked {count} alpha conflicts', flush=True)
    if (registry.read_bytes() != before or source.read_bytes() != source_raw
            or candidates.read_bytes() != candidate_raw or choices.read_bytes() != choice_raw):
        raise ValueError('registry or inputs changed')
    previous['opacity_followup'] = {'source_choices_sha256': hashlib.sha256(choice_raw).hexdigest(),
        'fully_read_conflicts': count, 'unique_textures_decoded': len(cache),
        'limitations': 'Full logical SHA and base texture alpha only; visual/runtime qualification remains separate.'}
    return previous


if __name__ == '__main__':
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument('--vault', type=Path, required=True)
    p.add_argument('--source', type=Path, required=True)
    p.add_argument('--candidates', type=Path, required=True)
    p.add_argument('--choices', type=Path, required=True)
    p.add_argument('--output', type=Path, required=True)
    args = p.parse_args()
    if args.output.exists():
        raise ValueError('output exists')
    result = audit(args.vault, args.source, args.candidates, args.choices)
    with args.output.open('x', encoding='utf8', newline='\n') as stream:
        json.dump(result, stream, ensure_ascii=False, indent=2)
        stream.write('\n')
    print(json.dumps({'state': 'audit_only', 'candidates': sum(x['state'] == 'candidate_only' for x in result['results']),
                      'review_required': sum(x['state'] != 'candidate_only' for x in result['results'])}))
