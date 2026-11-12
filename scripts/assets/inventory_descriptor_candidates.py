"""Inventory already shared model pairs; no publication or deletion.

The source inventory provides candidate associations, not proof that two
renders are equivalent. The descriptor metadata is inspected without treating
its declared SHA as verification of the reconstructed model bytes.
"""
import argparse
import collections
import hashlib
import json
from pathlib import Path

import vault_storage


def inventory(vault, source, mode='source-graph'):
    vault = vault.resolve(strict=True)
    registry = vault_storage.confined(vault, vault / 'metadata/asset-import-registry.v1.json')
    before = registry.read_bytes()
    source_raw = source.read_bytes()
    document = json.loads(source_raw)
    if hashlib.sha256(before).hexdigest() != document['registry_sha256']:
        raise ValueError('inventory registry has changed')
    active = {}
    for entry in json.loads(before)['packs'].values():
        path = vault_storage.confined(vault, vault / entry['path'] / 'details.v1.json')
        raw = path.read_bytes()
        if hashlib.sha256(raw).hexdigest() != entry['files']['details.v1.json']:
            raise ValueError('active metadata SHA changed')
        for item in json.loads(raw)['assets']:
            if item['id'] in active:
                raise ValueError('duplicate active identity')
            active[item['id']] = item
    results, seen = [], set()
    for pair in document['pairs']:
        if mode == 'source-graph':
            selected = (pair['images_exact'] == 'True'
                        and 'OPENRPG_source_graph' in pair['main_private_extensions']
                        and 'OPENRPG_source_graph' in pair['variant_private_extensions'])
        elif mode == 'identical-mesh':
            selected = pair['images_exact'] == 'False' and pair['transforms_exact'] == 'True'
        elif mode == 'collision-candidates':
            selected = (pair['main_id'].split('.')[1] == 'dungeon-builder'
                        and pair['mesh_payloads_exact'] == 'False'
                        and pair['images_exact'] == 'True')
        elif mode == 'witch-source-candidates':
            selected = (pair['main_id'].split('.')[1] == 'witch-house'
                        and 'witch-house-urp-basic-source-frame-batch209-v1' in pair['variant_path'])
        elif mode == 'witch-additional-candidates':
            selected = (pair['main_id'].split('.')[1] == 'witch-house'
                        and pair['transforms_exact'] == 'True'
                        and any(name in pair['variant_path'] for name in (
                            'witch-house-urp-basic-orm-srgb13-v1',
                            'witch-house-urp-basic-source-corner-batch6-v1',
                            'witch-house-urp-lit9-v1')))
        elif mode == 'witch-corrected':
            selected = (pair['main_id'].split('.')[1] == 'witch-house'
                        and 'witch-house-urp-20261108-r2' in pair['variant_path'])
        elif mode == 'military-candidates':
            selected = pair['main_id'].split('.')[1] == 'military-base-megapack'
        elif mode == 'haunted-candidates':
            selected = pair['main_id'].split('.')[1] == 'modular-haunted-village'
        elif mode == 'native-candidates':
            selected = pair['main_id'].split('.')[1] == 'native-american-village'
        elif mode == 'sewers-reviewed':
            selected = (pair['main_id'].split('.')[1] == 'modular-sewers'
                        and pair['transforms_exact'] == 'True')
        elif mode == 'sewers-source':
            selected = (pair['main_id'].split('.')[1] == 'modular-sewers'
                        and pair['transforms_exact'] == 'True')
        elif mode == 'rural-reviewed':
            selected = (pair['main_id'].split('.')[1] == 'modular-rural-town'
                        and pair['transforms_exact'] == 'True')
        elif mode == 'rural-source':
            selected = (pair['main_id'].split('.')[1] == 'modular-rural-town'
                        and pair['transforms_exact'] == 'True')
        elif mode == 'rural-hay':
            selected = (pair['main_id'].split('.')[1] == 'modular-rural-town'
                        and '.prefab-sm-hay-abc00000000017760980773119374081' in pair['main_id'])
        elif mode == 'cyber-reviewed':
            selected = (pair['main_id'].split('.')[1] == 'cyberpunk-city'
                        and pair['transforms_exact'] == 'True')
        elif mode == 'hospital-reviewed':
            selected = (pair['main_id'].split('.')[1] == 'modular-abandoned-hospital'
                        and pair['transforms_exact'] == 'True')
        elif mode == 'cave-candidates':
            selected = pair['main_id'].split('.')[1] == 'modular-cave'
        elif mode == 'ancient-ruins-candidates':
            selected = (pair['main_id'].split('.')[1] == 'mountain-ancient-mountain'
                        and '.prefab-sm-ruin-' in pair['main_id'])
        elif mode == 'viking-corrected':
            selected = (pair['main_id'].split('.')[1] == 'modular-viking-village'
                        and 'viking-frame-correction-batch01-v1' in pair['main_path'])
        elif mode == 'dark-sword':
            selected = (pair['main_id'].split('.')[1] == 'dark-fantasy-weapons'
                        and '.prefab-sm-sword-3-d15f08ba73cc6fb4c8ef28748644ed7f' in pair['main_id'])
        elif mode == 'witch-book':
            selected = (pair['main_id'].split('.')[1] == 'witch-house'
                        and '.prefab-sm-openbook-lo-abc00000000000685439161669535067' in pair['main_id'])
        elif mode == 'ancient-flowers':
            selected = (pair['main_id'].split('.')[1] == 'mountain-ancient-mountain'
                        and '.prefab-sm-flower-' in pair['main_id'])
        elif mode == 'town-cart':
            selected = (pair['main_id'].split('.')[1] == 'townsmith'
                        and '.prefab-sm-cart-small-abc00000000013862754103857300948-lod3' in pair['main_id'])
        else:
            raise ValueError('unknown candidate mode')
        if not (pair['comparison_current'] and selected
                and (mode in ('collision-candidates', 'witch-source-candidates', 'witch-additional-candidates', 'witch-corrected', 'military-candidates', 'haunted-candidates', 'native-candidates', 'cave-candidates', 'ancient-ruins-candidates', 'viking-corrected', 'sewers-source', 'rural-source', 'rural-hay', 'dark-sword', 'witch-book', 'ancient-flowers', 'town-cart')
                     or pair['mesh_payloads_exact'] == 'True')):
            continue
        a, b = (active.get(pair[side + '_id']) for side in ('main', 'variant'))
        if not a or not b or a['id'] in seen or b['id'] in seen:
            raise ValueError('candidate identity missing or reused')
        seen.update((a['id'], b['id']))
        for side, asset in (('main', a), ('variant', b)):
            if (asset.get('model_path') != pair[side + '_path']
                    or asset.get('model_sha256') != pair[side + '_sha']):
                raise ValueError('candidate model metadata changed')
        result = {'main_id': a['id'], 'variant_id': b['id'], 'state': 'candidate_only',
                  'main_has_aliases': bool(a.get('consolidated_variants')),
                  'variant_has_aliases': bool(b.get('consolidated_variants'))}
        for side, asset in (('main', a), ('variant', b)):
            try:
                path = vault_storage.confined(vault, Path(asset['model_path']))
                descriptor = vault_storage.read_descriptor(vault, path)
                result[side] = {'storage': 'descriptor' if descriptor else 'raw',
                                'physical_bytes': path.stat().st_size,
                                'logical_bytes': descriptor['bytes'] if descriptor else path.stat().st_size,
                                'declared_pin_matches': descriptor['sha256'] == asset['model_sha256'] if descriptor else None}
            except (OSError, ValueError) as error:
                result[side] = {'storage': 'refused', 'reason': str(error)}
        results.append(result)
    if registry.read_bytes() != before or source.read_bytes() != source_raw:
        raise ValueError('registry or source inventory changed during inspection')
    categories = collections.Counter((x['main']['storage'], x['variant']['storage'],
                                      x['main_has_aliases'], x['variant_has_aliases']) for x in results)
    return {'schema': 'openrpg.descriptor-candidate-inventory.v1', 'state': 'inventory_only', 'mode': mode,
            'registry_sha256': hashlib.sha256(before).hexdigest(),
            'source_sha256': hashlib.sha256(source_raw).hexdigest(), 'count': len(results),
            'categories': [{'main': k[0], 'variant': k[1], 'main_has_aliases': k[2],
                            'variant_has_aliases': k[3], 'count': n} for k, n in categories.most_common()],
            'limitations': ['No model bytes or render equivalence verified by this inventory.',
                            'No publication, migration, or deletion performed.'],
            'candidates': results}


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--vault', type=Path, required=True)
    parser.add_argument('--source', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--mode', choices=('source-graph', 'identical-mesh', 'collision-candidates', 'witch-source-candidates', 'witch-additional-candidates', 'witch-corrected', 'military-candidates', 'haunted-candidates', 'native-candidates', 'sewers-reviewed', 'sewers-source', 'rural-reviewed', 'rural-source', 'rural-hay', 'cyber-reviewed', 'hospital-reviewed', 'cave-candidates', 'ancient-ruins-candidates', 'viking-corrected', 'dark-sword', 'witch-book', 'ancient-flowers', 'town-cart'), default='source-graph')
    args = parser.parse_args()
    result = inventory(args.vault, args.source, args.mode)
    with args.output.open('x', encoding='utf8', newline='\n') as stream:
        json.dump(result, stream, ensure_ascii=False, indent=2)
        stream.write('\n')
    print(json.dumps({'count': result['count'], 'categories': result['categories']}))
