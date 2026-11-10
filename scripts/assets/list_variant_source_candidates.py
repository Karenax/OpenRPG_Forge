"""Produce a review list of matching source files, never a deletion list."""
import argparse
import collections
import csv
import json
from pathlib import Path


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('groups', type=Path)
    parser.add_argument('output', type=Path)
    args = parser.parse_args()
    with args.groups.open(encoding='utf-8-sig', newline='') as f:
        groups = list(csv.DictReader(f))
    protected = {x['path'].replace('\\', '/').lower() for g in groups for x in json.loads(g['hdrp_files'])}
    candidates = {}
    for g in groups:
        if g['all_source_contents_identical'] != 'True':
            continue
        retained = [x['path'] for x in json.loads(g['hdrp_files'])]
        for f in json.loads(g['urp_files']):
            key = f['path'].replace('\\', '/').lower()
            if key in protected:
                continue
            if key in candidates and candidates[key]['sha256'] != f['actual_sha256']:
                raise ValueError('Conflicting hashes for ' + f['path'])
            item = candidates.setdefault(key, {'candidate': f['path'], 'sha256': f['actual_sha256'],
                'logical_bytes': f['bytes'], 'retained_hdrp': set(), 'groups': set(),
                'urp_source_cards': 0, 'deletable_verified': False,
                'blocker': 'referenced by active source cards; dependencies and other uses unverified'})
            item['retained_hdrp'].update(retained)
            item['groups'].add(g['pack'] + ':' + g['normalized_path'])
            item['urp_source_cards'] += int(g['urp_card_count'])
    rows = []
    for item in candidates.values():
        item['retained_hdrp'] = json.dumps(sorted(item['retained_hdrp']), ensure_ascii=False)
        item['groups'] = json.dumps(sorted(item['groups']), ensure_ascii=False)
        rows.append(item)
    fields = ['candidate','sha256','logical_bytes','retained_hdrp','groups','urp_source_cards','deletable_verified','blocker']
    with args.output.open('x', encoding='utf-8-sig', newline='') as f:
        writer = csv.DictWriter(f, fieldnames=fields); writer.writeheader(); writer.writerows(rows)
    print(json.dumps({'candidates': len(rows), 'logical_bytes': sum(r['logical_bytes'] for r in rows),
                      'active_card_references': sum(r['urp_source_cards'] for r in rows)}))


if __name__ == '__main__':
    main()
