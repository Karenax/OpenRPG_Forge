"""Inventory source-support identities shared between HDRP and URP.

Source file hashes are declarations from the pinned catalogue, not fresh file
verification; matching hashes do not establish dependency or runtime parity.
This tool neither modifies the Vault nor approves a deletion.
"""
import argparse
import collections
import json
import re
from pathlib import Path

import vault_storage as storage

VARIANT = re.compile(r'\.variant-[a-f0-9]+(?=\.)')
PIPELINE = re.compile(r'(?i)(?<=/)(?:hdrp\s*(?:\(default\))?|urp\s*(?:\(default\))?)(?=/)')


def identity(card):
    ident = card.get('id')
    path = card.get('source_asset_path')
    if not isinstance(ident, str) or not isinstance(path, str):
        return None
    path = path.replace('\\', '/')
    matches = list(PIPELINE.finditer(path))
    if len(matches) != 1:
        return None
    m = matches[0]
    side = 'hdrp' if m.group().casefold().startswith('hdrp') else 'urp'
    return VARIANT.sub('', ident), (path[:m.start()] + '<pipeline>' + path[m.end():]).casefold(), side


def classify(cards):
    groups = collections.defaultdict(list)
    for card in cards:
        key = identity(card)
        if key:
            groups[key[:2]].append((key[2], card))
    out = []
    for (ident, path), entries in sorted(groups.items()):
        hdr = [c for side, c in entries if side == 'hdrp']
        urp = [c for side, c in entries if side == 'urp']
        if len(hdr) != 1 or len(urp) != 1:
            continue
        left, right = hdr[0], urp[0]
        same = lambda field: bool(left.get(field)) and left.get(field) == right.get(field)
        out.append({'base_id': ident, 'source_path': path, 'hdrp_id': left['id'], 'urp_id': right['id'],
                    'declared_source_sha_equal': same('source_sha256'),
                    'declared_meta_sha_equal': same('source_meta_sha256'),
                    'different_fields': sorted(k for k in set(left) | set(right) if left.get(k) != right.get(k))})
    return out


def inventory(vault):
    vault = vault.resolve(strict=True)
    pointer = storage.confined(vault, vault / 'metadata/asset-source-support-registry.v1.json')
    before = pointer.read_bytes()
    record = json.loads(before)
    path = storage.confined(vault, vault / record['path'])
    raw = path.read_bytes()
    if storage.sha(raw) != record['sha256']:
        raise ValueError('source-support catalog SHA changed')
    cards = json.loads(raw)['assets']
    pairs = classify(cards)
    if pointer.read_bytes() != before:
        raise ValueError('source-support pointer changed')
    return {'state': 'inventory_only', 'source_registry_sha256': storage.sha(before),
            'catalogue_sha256': record['sha256'], 'cards': len(cards), 'pairs': pairs,
            'limitations': 'Declared hashes only. Source files, dependencies, previews and references are not verified.'}


if __name__ == '__main__':
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument('--vault', type=Path, required=True)
    p.add_argument('--output', type=Path, required=True)
    args = p.parse_args()
    result = inventory(args.vault)
    with args.output.open('x', encoding='utf8', newline='\n') as f:
        json.dump(result, f, ensure_ascii=False, indent=2)
        f.write('\n')
    print(json.dumps({'cards': result['cards'], 'pairs': len(result['pairs']),
                      'matching_declared_source_and_meta_sha': sum(x['declared_source_sha_equal'] and x['declared_meta_sha_equal'] for x in result['pairs'])}))
