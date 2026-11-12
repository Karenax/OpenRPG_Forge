"""Inventory identities omitted by the one-main/one-variant pairing audit.

This reports coverage only: it does not infer equivalence or authorize deletion.
"""
import argparse
import collections
import csv
import hashlib
import itertools
import json
import os
from pathlib import Path

from audit_variant_candidates import base_id, pipeline


def sha(data):
    return hashlib.sha256(data).hexdigest()


def main():
    p = argparse.ArgumentParser()
    p.add_argument('--vault', type=Path, required=True)
    p.add_argument('--output', type=Path, required=True)
    p.add_argument('--expected-registry-sha', required=True)
    p.add_argument('--pairs-output', type=Path, required=True)
    args = p.parse_args()
    root = args.vault.resolve(strict=True)
    registry_path = root / 'metadata/asset-import-registry.v1.json'
    before = registry_path.read_bytes()
    if sha(before) != args.expected_registry_sha:
        raise ValueError('registry SHA changed; refresh inventory')
    grouped = collections.defaultdict(list)
    registry = json.loads(before)
    for pack, entry in sorted(registry['packs'].items()):
        details_path = root / entry['path'] / 'details.v1.json'
        data = details_path.read_bytes()
        if sha(data) != entry['files']['details.v1.json']:
            raise ValueError('details SHA changed: ' + pack)
        for item in json.loads(data)['assets']:
            ident = item['id']
            grouped[base_id(ident)].append((ident, pack, item.get('model_path', ''),
                                           item.get('model_sha256', ''),
                                           pipeline([item.get('source_fbx'), item.get('source_prefab')])))
    counts = collections.Counter()
    omitted = []
    for key, records in sorted(grouped.items()):
        main = [r for r in records if '.variant-' not in r[0]]
        variants = [r for r in records if '.variant-' in r[0]]
        if not variants:
            counts['without_variant'] += 1
            continue
        if len(main) == 1 and len(variants) == 1:
            counts['one_main_one_variant'] += 1
            continue
        category = 'multiple_or_missing_main' if len(main) != 1 else 'multiple_variants'
        counts[category] += 1
        omitted.append({'base_id': key, 'classification': category,
                        'main_count': len(main), 'variant_count': len(variants),
                        'records_json': json.dumps(records, ensure_ascii=False),
                        'deletable_verified': False})
    if registry_path.read_bytes() != before:
        raise ValueError('registry changed during audit')
    with args.output.open('x', encoding='utf-8-sig', newline='') as f:
        writer = csv.DictWriter(f, fieldnames=['base_id','classification','main_count',
                                               'variant_count','records_json','deletable_verified'])
        writer.writeheader()
        writer.writerows(omitted)
    pair_count = 0
    with args.pairs_output.open('x', encoding='utf-8-sig', newline='') as f:
        fields = ['id','base_id','main_id','variant_id','main_path','variant_path',
                  'main_sha','variant_sha','main_pipeline','variant_pipeline','same_path']
        writer = csv.DictWriter(f, fieldnames=fields)
        writer.writeheader()
        for row in omitted:
            for a, b in itertools.combinations(json.loads(row['records_json']), 2):
                writer.writerow({'id': a[0] + '::' + b[0], 'base_id': row['base_id'],
                                 'main_id': a[0], 'variant_id': b[0],
                                 'main_path': a[2], 'variant_path': b[2],
                                 'main_sha': a[3], 'variant_sha': b[3],
                                 'main_pipeline': a[4], 'variant_pipeline': b[4],
                                 'same_path': os.path.normcase(os.path.normpath(a[2])) == os.path.normcase(os.path.normpath(b[2]))})
                pair_count += 1
    print(json.dumps({'registry_sha256': sha(before), 'groups': len(grouped),
                      'counts': dict(counts), 'omitted_rows': len(omitted),
                      'additional_pairs': pair_count}, ensure_ascii=False))


if __name__ == '__main__':
    main()
