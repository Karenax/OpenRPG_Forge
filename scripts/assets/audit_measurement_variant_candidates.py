"""Compare candidates with identical measured bounds; never select or delete."""
import argparse
import json
from pathlib import Path
import compare_variant_glb_bytes as compare
import vault_storage as storage


def audit(vault, inventory):
    vault = vault.resolve(strict=True); source = inventory.read_bytes(); doc = json.loads(source)
    index = storage.confined(vault, vault/'metadata/asset-import-registry.v1.json'); before = index.read_bytes()
    if storage.sha(before) != doc['registry_sha256']:
        raise ValueError('registry changed')
    results = []
    for group in doc['groups']:
        row = {'base_pack': group.get('base_pack', 'multiple-packs'), 'triangles': group['triangles'], 'cards': [], 'comparisons': []}
        signatures = []
        for card in group['cards']:
            item = {'id': card['id'], 'source_prefab': card.get('source_prefab')}
            try:
                path = storage.confined(vault, Path(card['model_path']))
                raw = storage.read_bytes(vault, path, 256*1024*1024)
                if storage.sha(raw) != card['sha256']:
                    raise ValueError('logical SHA changed')
                signatures.append(compare.signature(raw)); item['state'] = 'compared'
            except (OSError, ValueError, KeyError, IndexError, TypeError) as error:
                signatures.append(None); item.update(state='review_required', reason=str(error))
            row['cards'].append(item)
        for i, a in enumerate(signatures):
            for j in range(i + 1, len(signatures)):
                b = signatures[j]
                if a is None or b is None: continue
                row['comparisons'].append({'left': row['cards'][i]['id'], 'right': row['cards'][j]['id'],
                    'mesh_payloads_exact': a['mesh_payloads'] == b['mesh_payloads'],
                    'images_exact': a['images'] == b['images'], 'materials_exact': a['materials'] == b['materials'],
                    'transforms_exact': a['transforms'] == b['transforms']})
        results.append(row)
        print(f"Compared {len(results)}/{len(doc['groups'])} measurement groups", flush=True)
    if index.read_bytes() != before or inventory.read_bytes() != source:
        raise ValueError('inventory changed')
    return {'state': 'audit_only', 'registry_sha256': doc['registry_sha256'], 'groups': results,
            'limitations': 'These are distinct source-prefab names; same geometry alone does not establish identical behavior.'}


if __name__ == '__main__':
    p = argparse.ArgumentParser(description=__doc__)
    for n in ('vault', 'inventory', 'output'): p.add_argument('--' + n, type=Path, required=True)
    args = p.parse_args()
    if args.output.exists(): p.error('output exists')
    result = audit(args.vault, args.inventory)
    with args.output.open('x', encoding='utf8') as out:
        json.dump(result, out, ensure_ascii=False, indent=2); out.write('\n')
    print(json.dumps({'state': result['state'], 'groups': len(result['groups'])}))
