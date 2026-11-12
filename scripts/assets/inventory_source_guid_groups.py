"""Inventory active cards sharing pack, prefab GUID and LOD; never delete.

This is a lead generator: shared GUIDs do not prove matching materials,
geometry, roles or rights. Different names and source paths are retained.
"""
import argparse
import collections
import hashlib
import json
import re
from pathlib import Path

import vault_storage


def inventory(vault, registry_sha):
    vault = vault.resolve(strict=True)
    index = vault_storage.confined(vault, vault / 'metadata/asset-import-registry.v1.json')
    before = index.read_bytes()
    if hashlib.sha256(before).hexdigest() != registry_sha:
        raise ValueError('registry changed')
    groups = collections.defaultdict(list)
    missing = 0
    for pack, entry in sorted(json.loads(before)['packs'].items()):
        p = vault_storage.confined(vault, vault / entry['path'] / 'details.v1.json')
        raw = p.read_bytes()
        if hashlib.sha256(raw).hexdigest() != entry['files']['details.v1.json']:
            raise ValueError('details changed: ' + pack)
        for card in json.loads(raw)['assets']:
            ident = card['id']
            source = card.get('source_prefab')
            guid = source.get('guid') if isinstance(source, dict) else None
            if not (isinstance(guid, str) and re.fullmatch(r'[0-9a-fA-F]{32}', guid)):
                match = re.search(r'-([0-9a-f]{32})(?:-lod\d+)?$', ident, re.I)
                guid = match.group(1) if match else None
            if not guid:
                missing += 1
                continue
            lod_match = re.search(r'-lod(\d+)$', ident, re.I)
            lod = int(lod_match.group(1)) if lod_match else 0
            base_pack = re.sub(r'\.variant-[a-f0-9]+$', '', pack, flags=re.I)
            groups[(base_pack, guid.lower(), lod)].append({
                'id': ident, 'pack': pack, 'model_path': card.get('model_path'),
                'model_sha256': card.get('model_sha256'), 'source_prefab': source,
                'license': card.get('license'), 'readiness': card.get('readiness')})
    if index.read_bytes() != before:
        raise ValueError('registry changed during inventory')
    repeated = [{'pack': k[0], 'source_guid': k[1], 'lod': k[2], 'cards': cards}
                for k, cards in sorted(groups.items()) if len(cards) > 1]
    return {'state': 'inventory_only', 'registry_sha256': registry_sha,
            'groups': repeated, 'cards_without_guid': missing,
            'limitations': 'Source GUID inferred from metadata or ID; not proof of equivalence.'}


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--vault', required=True, type=Path)
    parser.add_argument('--registry-sha', required=True)
    parser.add_argument('--output', required=True, type=Path)
    args = parser.parse_args()
    result = inventory(args.vault, args.registry_sha)
    with args.output.open('x', encoding='utf8') as stream:
        json.dump(result, stream, ensure_ascii=False, indent=2)
        stream.write('\n')
    print(json.dumps({'groups': len(result['groups']), 'cards_without_guid': result['cards_without_guid']}))
