"""Find cross-variant filename matches even when Unity GUIDs differ.

This is only a lead inventory; filenames never prove equivalence or permit removal.
"""
import argparse
import collections
import hashlib
import json
import re
from pathlib import Path, PureWindowsPath
import vault_storage as storage


def prefab_paths(value):
    """Read single or multiple source prefab records without losing their names."""
    entries = value if isinstance(value, list) else [value]
    result = []
    for entry in entries:
        if isinstance(entry, dict):
            original = entry.get('original_source_path')
            entry = original if isinstance(original, str) and original.strip() else entry.get('path')
        if isinstance(entry, str) and entry.strip() and entry not in result:
            result.append(entry)
    return result


def inventory(vault, registry_sha):
    vault = vault.resolve(strict=True)
    index = storage.confined(vault, vault / 'metadata/asset-import-registry.v1.json')
    before = index.read_bytes()
    if storage.sha(before) != registry_sha:
        raise ValueError('registry changed')
    groups = collections.defaultdict(list)
    display_groups = collections.defaultdict(list)
    no_prefab = 0
    for pack, entry in sorted(json.loads(before)['packs'].items()):
        path = storage.confined(vault, vault / entry['path'] / 'details.v1.json')
        raw = path.read_bytes()
        if storage.sha(raw) != entry['files']['details.v1.json']:
            raise ValueError('active metadata SHA changed')
        for card in json.loads(raw)['assets']:
            ident = card['id']
            prefabs = prefab_paths(card.get('source_prefab'))
            base = re.sub(r'\.variant-[a-f0-9]+$', '', pack, flags=re.I)
            lod_match = re.search(r'-lod(\d+)$', ident, re.I)
            lod = int(lod_match.group(1)) if lod_match else 0
            names = card.get('names') or {}
            display = names.get('en') if isinstance(names, dict) else None
            if isinstance(display, str) and display.strip():
                display = re.sub(r'\s+', ' ', display.strip()).casefold()
                display_groups[(base, display, lod)].append({'id': ident, 'pack': pack,
                    'source_prefab': prefabs[0] if prefabs else None, 'model_sha256': card.get('model_sha256')})
            if not prefabs:
                no_prefab += 1
                continue
            named = {}
            for prefab in prefabs:
                name = PureWindowsPath(prefab.replace('/', '\\')).stem.casefold()
                if name:
                    named.setdefault(name, prefab)
            if not named:
                no_prefab += 1
                continue
            for name, prefab in named.items():
                groups[(base, name, lod)].append({'id': ident, 'pack': pack, 'source_prefab': prefab,
                                                  'model_sha256': card.get('model_sha256'),
                                                  'model_path': card.get('model_path')})
    if index.read_bytes() != before:
        raise ValueError('registry changed during inventory')
    result = []
    same_pack = []
    for (pack, name, lod), cards in sorted(groups.items()):
        if len(cards) > 1 and len({c['pack'] for c in cards}) > 1:
            result.append({'base_pack': pack, 'source_filename': name, 'lod': lod, 'cards': cards})
        per_pack = collections.defaultdict(list)
        for card in cards:
            per_pack[card['pack']].append(card)
        for exact_pack, matches in sorted(per_pack.items()):
            if len(matches) > 1:
                same_pack.append({'pack': exact_pack, 'source_filename': name, 'lod': lod, 'cards': matches})
    display_leads = [{'base_pack': pack, 'display_name': name, 'lod': lod, 'cards': cards}
                     for (pack, name, lod), cards in sorted(display_groups.items())
                     if len(cards) > 1 and len({c['pack'] for c in cards}) > 1
                     and any(not isinstance(c['source_prefab'], str) or not c['source_prefab'].strip() for c in cards)]
    return {'state': 'inventory_only', 'registry_sha256': registry_sha, 'groups': result,
            'same_pack_name_groups': same_pack,
            'display_name_leads_with_missing_prefab': display_leads,
            'cards_without_source_prefab': no_prefab,
            'limitations': 'Filename matches are leads only; sources, geometry, materials and references are not compared.'}


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--vault', type=Path, required=True)
    parser.add_argument('--registry-sha', required=True)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    if args.output.exists():
        parser.error('output exists')
    result = inventory(args.vault, args.registry_sha)
    with args.output.open('x', encoding='utf8') as file:
        json.dump(result, file, ensure_ascii=False, indent=2)
        file.write('\n')
    print(json.dumps({'groups': len(result['groups']), 'same_pack_name_groups': len(result['same_pack_name_groups']), 'cards_without_source_prefab': result['cards_without_source_prefab'],
                      'display_name_leads_with_missing_prefab': len(result['display_name_leads_with_missing_prefab'])}))
