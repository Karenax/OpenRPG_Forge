"""Qualify the reviewed Military container choice, without claiming visual parity."""
import argparse
import hashlib
import json
from pathlib import Path

import vault_storage as storage

GUID = 'abc00000000000840775317738035467'
ROOT = Path(r'F:\OpenRPG_ImportWork\hivemind-run\military-base-megapack')
SOURCES = (
    ROOT / 'source/Assets/Hivemind/MilitaryCamp/HDRP(Default)/Art/Prefabs/SM_Container_a.prefab',
    ROOT / 'variants/efb4165347c6a93b/source/Assets/Hivemind/MilitaryCamp/URP/Art/Prefabs/SM_Container_a.prefab',
)


def check(cards, entries, source_paths=SOURCES):
    main, variant = cards
    if not main.get('license') or main['license'] != variant.get('license'):
        raise ValueError('license differs')
    for card, entry, path in zip(cards, entries, source_paths):
        if (card.get('missing') or card.get('readiness', {}).get('metadata_ready') is not True
                or entry.get('id') != card['id'] or entry.get('sha256') != card.get('model_sha256')
                or entry.get('source_guid') != GUID or entry.get('source_prefab') != card.get('source_prefab')
                or entry.get('geometry_roundtrip_passed') is not True):
            raise ValueError('metadata, conversion or source identity differs')
        expected = entry.get('source_prefab_sha256')
        if not storage.valid_hex(expected, 64) or hashlib.sha256(path.read_bytes()).hexdigest() != expected:
            raise ValueError('source prefab SHA differs')
    if entries[0].get('material_review_pending') is not False or entries[0].get('visual_review') != 'agent_review':
        raise ValueError('primary material review missing')
    a, b = (card.get('measurement', {}) for card in cards)
    for key in ('bounds', 'dimensions', 'triangles', 'vertices', 'nodes'):
        if not a.get(key) or a.get(key) != b.get(key):
            raise ValueError('measurement differs: ' + key)
    ma, mb = (entry.get('components', []) for entry in entries)
    if (len(ma) != 1 or len(mb) != 1 or not storage.valid_hex(ma[0].get('mesh_sha256'), 64)
            or ma[0]['mesh_sha256'] != mb[0].get('mesh_sha256')):
        raise ValueError('source mesh pin differs')
    return {'keep_side': 'main', 'preferred_controls': ['reviewed_primary', 'matching_source_mesh_pin'],
            'source_prefab_sha256': [e['source_prefab_sha256'] for e in entries],
            'limitations': 'Materials and ORM textures differ; appearance and runtime equivalence are not established.'}


def audit(vault, source, candidates, *, mode='military-candidates', guid=GUID,
          sources=SOURCES, pack='military-base-megapack', checker=check):
    vault = vault.resolve(strict=True)
    src, cand = source.read_bytes(), candidates.read_bytes()
    inv, doc = json.loads(src), json.loads(cand)
    index = storage.confined(vault, vault / 'metadata/asset-import-registry.v1.json')
    before = index.read_bytes()
    if (storage.sha(before) != inv['registry_sha256'] or doc.get('mode') != mode
            or doc['source_sha256'] != storage.sha(src) or doc['registry_sha256'] != inv['registry_sha256']):
        raise ValueError('registry or inventory changed')
    cards = {}
    for entry in json.loads(before)['packs'].values():
        path = storage.confined(vault, vault / entry['path'] / 'details.v1.json')
        raw = path.read_bytes()
        if storage.sha(raw) != entry['files']['details.v1.json']:
            raise ValueError('active metadata changed')
        for card in json.loads(raw)['assets']:
            if card['id'] in cards:
                raise ValueError('duplicate identity')
            cards[card['id']] = card
    eligible = {x['variant_id'] for x in doc['candidates']
                if x['main']['storage'] == x['variant']['storage'] == 'descriptor'
                and x['main']['declared_pin_matches'] and x['variant']['declared_pin_matches']
                and not x['variant_has_aliases'] and all(x[k]['logical_bytes'] <= 512*1024*1024 for k in ('main','variant'))}
    results, catalogs = [], {}
    for row in inv['pairs']:
        if row['variant_id'] not in eligible or guid not in row['main_id']:
            continue
        result = {'main_id': row['main_id'], 'variant_id': row['variant_id']}
        try:
            pair, entries = [], []
            for side in ('main', 'variant'):
                card = cards[row[side + '_id']]
                if card.get('model_path') != row[side + '_path'] or card.get('model_sha256') != row[side + '_sha']:
                    raise ValueError('active model changed')
                path = storage.confined(vault, Path(card['source_catalog']))
                if path not in catalogs:
                    raw = path.read_bytes()
                    catalogs[path] = (storage.sha(raw), {x['id']: x for x in json.loads(raw)['assets']})
                pair.append(card)
                entries.append(catalogs[path][1][card['id']])
            for path in sources:
                storage.confined(Path(path.anchor), path)
            result.update(state='candidate_only', **checker(pair, entries, sources))
        except (OSError, ValueError, KeyError, TypeError) as error:
            result.update(state='review_required', reason=str(error))
        results.append(result)
    if index.read_bytes() != before or source.read_bytes() != src or candidates.read_bytes() != cand:
        raise ValueError('inputs changed')
    if any(storage.sha(path.read_bytes()) != pin for path, (pin, _) in catalogs.items()):
        raise ValueError('catalog changed')
    return {'schema': 'openrpg.material-choice-audit.v1', 'state': 'audit_only',
            'registry_sha256': inv['registry_sha256'], 'source_sha256': storage.sha(src),
            'candidate_inventory_sha256': storage.sha(cand), 'pack': pack,
            'offset': 0, 'results': results,
            'limitations': 'The chosen material differs; historical bytes and references must remain available.'}


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ('vault', 'source', 'candidates', 'output'):
        parser.add_argument('--' + name, type=Path, required=True)
    args = parser.parse_args()
    if args.output.exists():
        parser.error('output exists')
    result = audit(args.vault, args.source, args.candidates)
    with args.output.open('x', encoding='utf8') as file:
        json.dump(result, file, ensure_ascii=False, indent=2)
        file.write('\n')
    print(json.dumps({'count': len(result['results']), 'candidates': sum(x['state'] == 'candidate_only' for x in result['results'])}))
