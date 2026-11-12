"""Recheck previously audited candidate GLBs; never publish or remove files."""
import csv
import hashlib
import json
from pathlib import Path

import vault_storage
from plan_metadata_only_variants import metadata_only, verify_differences
from variant_legacy_recipe import build_recipe

VAULT = Path(r'F:\OpenRPG_AssetVault')
BASE = Path(r'E:\OpenRPG_Forge\.build\asset-conversion')
REPORTS = Path(r'E:\OpenRPG_Forge\Preuves-et-analyses\rapports\audit-variantes-hdrp-urp-20261110')


def main():
    registry = VAULT / 'metadata/asset-import-registry.v1.json'
    before = registry.read_bytes()
    used = set()
    for p in (BASE / 'plan-combine-meta-1765-20261111.json',
              BASE / 'plan-provenance-meta-244-rebase-20261111.json',
              BASE / 'plan-provenance-graphe-12-20261111.json',
              REPORTS / 'plan-consolidation-23-20261111-v3.json'):
        used.update(x['retained_id'] for x in json.loads(p.read_bytes())['candidates'])
    with (REPORTS / 'variantes-paires.csv').open(encoding='utf-8-sig', newline='') as f:
        pairs = {x['id']: x for x in csv.DictReader(f)}
    candidates = {}
    for filename in ('audit-blocs-modular-haunted-village-20261111.json',
                     'audit-blocs-villa-forge-provenance-v2-20261111.json'):
        for entry in json.loads((REPORTS / filename).read_bytes())['results']:
            if entry['id'] not in used and metadata_only(entry):
                candidates[entry['id']] = entry
    passed, refused = [], []
    for i, (identity, entry) in enumerate(sorted(candidates.items()), 1):
        try:
            pair = pairs[identity]
            a = vault_storage.read_bytes(VAULT, vault_storage.confined(VAULT, Path(pair['main_path'])), 512 * 1024 * 1024)
            b = vault_storage.read_bytes(VAULT, vault_storage.confined(VAULT, Path(pair['variant_path'])), 512 * 1024 * 1024)
            build_recipe(a, b, pair['main_sha'], pair['variant_sha'])
            verify_differences(a, b, entry['differences'])
            passed.append(entry)
        except (OSError, ValueError, KeyError) as exc:
            refused.append({'id': identity, 'reason': str(exc)})
        if i % 20 == 0:
            print(f'checked {i}/{len(candidates)}; passed {len(passed)}; refused {len(refused)}', flush=True)
    if registry.read_bytes() != before:
        raise ValueError('registry changed during check')
    out = {'schema': 'openrpg.filtered-provenance-audit.v1',
           'registry_sha256': hashlib.sha256(before).hexdigest(),
           'results': passed, 'refused': refused}
    with (BASE / 'audit-provenance-compl-20261111.json').open('x', encoding='utf8') as f:
        json.dump(out, f, ensure_ascii=False, indent=2)
        f.write('\n')
    print(json.dumps({'passed': len(passed), 'refused': len(refused)}))


if __name__ == '__main__':
    main()
