"""Find potential active counterparts for cards lacking a source prefab.

Identifier similarity yields leads only. It is never evidence for deletion.
"""
import argparse
import collections
import json
import re
from pathlib import Path

import vault_storage as storage


def key(ident, pack):
    prefix = pack + '.'
    if not ident.startswith(prefix):
        raise ValueError('asset ID does not match its pack')
    tail = ident[len(prefix):].lower()
    tail = re.sub(r'-lod(\d+)$', lambda m: ':lod' + m.group(1), tail)
    if ':lod' not in tail:
        tail += ':lod0'
    tail = re.sub(r'-(?:abc\d{20,}|[0-9a-f]{32})(?=:lod\d+$)', '', tail)
    tail = re.sub(r'^prefab-', '', tail)
    base = re.sub(r'\.variant-[a-f0-9]+$', '', pack.lower())
    return base, tail


def inventory(vault, source):
    vault = vault.resolve(strict=True)
    raw_source = source.read_bytes()
    missing = json.loads(raw_source)
    if missing.get('state') != 'inventory_only':
        raise ValueError('expected missing-prefab inventory')
    index = storage.confined(vault, vault / 'metadata/asset-import-registry.v1.json')
    before = index.read_bytes()
    if storage.sha(before) != missing['registry_sha256']:
        raise ValueError('registry changed')
    target_ids = {x['id'] for x in missing['missing_cards']}
    if len(target_ids) != len(missing['missing_cards']):
        raise ValueError('duplicate missing-card ID')
    groups = collections.defaultdict(list)
    seen = set()
    for pack, entry in sorted(json.loads(before)['packs'].items()):
        details = storage.confined(vault, vault / entry['path'] / 'details.v1.json').read_bytes()
        if storage.sha(details) != entry['files']['details.v1.json']:
            raise ValueError('active metadata changed')
        for card in json.loads(details)['assets']:
            ident = card['id']
            if ident in seen:
                raise ValueError('duplicate asset ID')
            seen.add(ident)
            groups[key(ident, pack)].append({'id': ident, 'pack': pack,
                'model_path': card.get('model_path'), 'model_sha256': card.get('model_sha256'),
                'source_prefab': card.get('source_prefab')})
    if not target_ids <= seen:
        raise ValueError('missing cards no longer active')
    leads = []
    for (pack, name), cards in sorted(groups.items()):
        targets = [card['id'] for card in cards if card['id'] in target_ids]
        if targets and len(cards) > 1:
            leads.append({'base_pack': pack, 'normalized_identifier': name,
                          'target_ids': targets, 'cards': cards})
    if index.read_bytes() != before or source.read_bytes() != raw_source:
        raise ValueError('inputs changed')
    return {'state': 'inventory_only', 'registry_sha256': storage.sha(before),
            'source_sha256': storage.sha(raw_source), 'target_count': len(target_ids),
            'groups': leads, 'limitations': 'Identifier matches are leads, not proof of equivalence; unmatched names do not prove uniqueness.'}


if __name__ == '__main__':
    p = argparse.ArgumentParser(description=__doc__)
    for name in ('vault', 'source', 'output'):
        p.add_argument('--' + name, type=Path, required=True)
    a = p.parse_args()
    if a.output.exists():
        p.error('output already exists')
    result = inventory(a.vault, a.source)
    with a.output.open('x', encoding='utf-8', newline='\n') as f:
        json.dump(result, f, ensure_ascii=False, indent=2)
        f.write('\n')
    print(json.dumps({'targets': result['target_count'], 'groups': len(result['groups'])}))
