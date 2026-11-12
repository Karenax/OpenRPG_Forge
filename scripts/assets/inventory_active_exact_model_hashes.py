"""Read-only inventory of active cards with identical logical model SHA-256.

Equal model bytes alone do not prove that cards, roles or source assets are duplicates.
"""
import collections
import hashlib
import json
from pathlib import Path

import vault_storage

ROOT = Path(r'F:\OpenRPG_AssetVault')
OUTPUT = Path(r'E:\OpenRPG_Forge\.build\asset-conversion\active-exact-model-groups-20261111.json')


def main():
    index = ROOT / 'metadata/asset-import-registry.v1.json'
    before = index.read_bytes()
    registry = json.loads(before)
    groups = collections.defaultdict(list)
    for pack, entry in sorted(registry['packs'].items()):
        path = vault_storage.confined(ROOT, ROOT / entry['path'] / 'details.v1.json')
        raw = path.read_bytes()
        if hashlib.sha256(raw).hexdigest() != entry['files']['details.v1.json']:
            raise ValueError('active details SHA mismatch: ' + pack)
        for asset in json.loads(raw)['assets']:
            digest = asset.get('model_sha256')
            if digest:
                groups[digest].append({'id': asset['id'], 'pack': pack,
                                       'path': asset.get('model_path'),
                                       'name': asset.get('name'),
                                       'source': asset.get('source')})
    if index.read_bytes() != before:
        raise ValueError('registry changed during inventory')
    repeated = [{'sha256': sha, 'cards': cards} for sha, cards in sorted(groups.items()) if len(cards) > 1]
    result = {'state': 'inventory_only', 'registry_sha256': hashlib.sha256(before).hexdigest(),
              'group_count': len(repeated), 'card_count': sum(len(g['cards']) for g in repeated),
              'groups': repeated,
              'limitations': 'Equal model SHA does not imply equal metadata, source identity, rights or purpose.'}
    with OUTPUT.open('x', encoding='utf8') as f:
        json.dump(result, f, ensure_ascii=False, indent=2); f.write('\n')
    print(json.dumps({'groups': result['group_count'], 'cards': result['card_count']}))


if __name__ == '__main__':
    main()
