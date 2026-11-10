"""Consolidate read-only variant comparison results; never propose deletions."""
import argparse
import collections
import csv
import json
from pathlib import Path


def main():
    p = argparse.ArgumentParser()
    p.add_argument('directory', type=Path)
    a = p.parse_args()
    root = a.directory
    original = [json.loads(line) for line in (root / 'comparaison-complete-v2.jsonl').read_text(encoding='utf-8').splitlines()]
    retries = []
    for name in ('comparaison-exceptions-v2.jsonl', 'comparaison-exceptions-1g-v2.jsonl'):
        retries_path = root / name
        if retries_path.exists():
            retries.extend(json.loads(line) for line in retries_path.read_text(encoding='utf-8').splitlines())
    by_id = {x['id']: x for x in original}
    if len(by_id) != len(original):
        raise ValueError('Duplicate IDs in the original comparison')
    for row in retries:
        if row['id'] not in by_id or by_id[row['id']]['status'] != 'unverified':
            raise ValueError('Unexpected or duplicate retry ID')
        by_id[row['id']] = row
    rows = list(by_id.values())
    fields = ['id', 'main_path', 'variant_path', 'main_sha', 'variant_sha', 'status', 'reason',
              'geometry_exact', 'mesh_payloads_exact', 'transforms_exact', 'materials_exact',
              'images_exact', 'main_image_count', 'variant_image_count', 'main_private_extensions',
              'variant_private_extensions', 'deletable_verified']
    with (root / 'comparaison-contenus-consolidee.csv').open('w', encoding='utf-8-sig', newline='') as f:
        writer = csv.DictWriter(f, fieldnames=fields, extrasaction='ignore')
        writer.writeheader()
        writer.writerows(rows)
    compared = [x for x in rows if x['status'] == 'compared']
    stats = {'records': len(rows), 'retried': len(retries),
             'statuses': dict(collections.Counter(x['status'] for x in rows)),
             'unverified_reasons': dict(collections.Counter(x.get('reason', 'unknown') for x in rows if x['status'] != 'compared'))}
    for key in ['geometry_exact', 'mesh_payloads_exact', 'transforms_exact', 'materials_exact', 'images_exact']:
        stats[key] = sum(x.get(key) is True for x in compared)
    stats['images_exact_with_images'] = sum(x.get('images_exact') is True and x.get('main_image_count', 0) > 0 for x in compared)
    stats['private_extensions'] = sum(bool(x.get('main_private_extensions') or x.get('variant_private_extensions')) for x in compared)
    stats['four_exact'] = sum(all(x.get(k) is True for k in ('mesh_payloads_exact','transforms_exact','materials_exact','images_exact')) for x in compared)
    (root / 'comparaison-contenus-statistiques.json').write_text(json.dumps(stats, indent=2, ensure_ascii=False) + '\n', encoding='utf-8')
    print(json.dumps(stats, indent=2, ensure_ascii=False))


if __name__ == '__main__':
    main()
