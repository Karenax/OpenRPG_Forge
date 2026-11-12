"""Count formerly audited variant pairs still active; never modify the Vault."""
import collections
import csv
import hashlib
import json
from pathlib import Path

import vault_storage

ROOT = Path(r'F:\OpenRPG_AssetVault')
REPORTS = Path(r'E:\OpenRPG_Forge\Preuves-et-analyses\rapports\audit-variantes-hdrp-urp-20261110')
OUTPUT = Path(r'E:\OpenRPG_Forge\.build\asset-conversion\remaining-variant-pairs-20261111.json')


def main():
    index = ROOT / 'metadata/asset-import-registry.v1.json'
    before = index.read_bytes()
    registry = json.loads(before)
    active = {}
    for entry in registry['packs'].values():
        p = vault_storage.confined(ROOT, ROOT / entry['path'] / 'details.v1.json')
        raw = p.read_bytes()
        if hashlib.sha256(raw).hexdigest() != entry['files']['details.v1.json']:
            raise ValueError('active metadata changed')
        for a in json.loads(raw)['assets']:
            if a['id'] in active:
                raise ValueError('duplicate active identity')
            active[a['id']] = a
    with (REPORTS / 'comparaison-contenus-consolidee.csv').open(encoding='utf-8-sig', newline='') as f:
        comparisons = {x['id']: x for x in csv.DictReader(f)}
    with (REPORTS / 'variantes-paires.csv').open(encoding='utf-8-sig', newline='') as f:
        pairs = list(csv.DictReader(f))
    results = []
    for p in pairs:
        a, b = active.get(p['main_id']), active.get(p['variant_id'])
        if a is None or b is None:
            continue
        comparison = comparisons.get(p['id'], {})
        results.append({'id': p['id'], 'main_id': p['main_id'], 'variant_id': p['variant_id'],
                        'main_path': a.get('model_path'), 'variant_path': b.get('model_path'),
                        'main_sha': a.get('model_sha256'), 'variant_sha': b.get('model_sha256'),
                        'comparison_current': (a.get('model_sha256') == comparison.get('main_sha')
                                               and b.get('model_sha256') == comparison.get('variant_sha')),
                        'mesh_payloads_exact': comparison.get('mesh_payloads_exact'),
                        'images_exact': comparison.get('images_exact'),
                        'materials_exact': comparison.get('materials_exact'),
                        'transforms_exact': comparison.get('transforms_exact'),
                        'main_private_extensions': comparison.get('main_private_extensions'),
                        'variant_private_extensions': comparison.get('variant_private_extensions')})
    if index.read_bytes() != before:
        raise ValueError('registry changed')
    counts = collections.Counter((r['comparison_current'], r['mesh_payloads_exact'], r['images_exact'],
                                  r['materials_exact']) for r in results)
    out = {'state': 'inventory_only', 'registry_sha256': hashlib.sha256(before).hexdigest(),
           'active_pairs': len(results),
           'categories': [{'current': k[0], 'mesh_exact': k[1], 'images_exact': k[2],
                           'materials_exact': k[3], 'count': n} for k, n in counts.most_common()],
           'pairs': results, 'limitations': 'Only pairs in historical inventory; not an exhaustive collection total.'}
    with OUTPUT.open('x', encoding='utf8') as f:
        json.dump(out, f, ensure_ascii=False, indent=2); f.write('\n')
    print(json.dumps({'active_pairs': len(results), 'categories': out['categories']}))


if __name__ == '__main__':
    main()
