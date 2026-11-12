"""Compare GLB JSON and complete binary chunks for one selected candidate pack."""
import argparse
import csv
import hashlib
import itertools
import json
from pathlib import Path

from audit_variant_exact_candidates import diffs, load
from plan_metadata_only_variants import metadata_only, verify_documents


def audit(vault, volumes, comparison, pack):
    vault = vault.resolve(strict=True)
    registry = vault / 'metadata/asset-import-registry.v1.json'
    before = registry.read_bytes()
    volume_data = json.loads(volumes.read_bytes())
    pin = hashlib.sha256(before).hexdigest()
    if pin != volume_data['registry_sha256']:
        raise ValueError('volume inventory is stale')
    wanted = {r['id'] for r in volume_data['rows'] if r.get('pack') == pack and r.get('state') == 'current_candidate'}
    if not wanted:
        raise ValueError('no current candidates for pack')
    with comparison.open(encoding='utf-8-sig', newline='') as stream:
        rows = [r for r in csv.DictReader(stream) if r['id'] in wanted]
    if {r['id'] for r in rows} != wanted or len(rows) != len(wanted):
        raise ValueError('candidate comparison missing or duplicated')
    results = []
    for i, row in enumerate(rows, 1):
        entry = {'id': row['id'], 'deletable': False}
        try:
            a, tail_a = load(vault, row['main_path'], row['main_sha'])
            b, tail_b = load(vault, row['variant_path'], row['variant_sha'])
            paths = list(itertools.islice(diffs(a, b), 501))
            entry.update(state='compared', binary_chunk_exact=tail_a == tail_b,
                         difference_count=len(paths), differences=paths[:500], truncated=len(paths) > 500)
            entry['metadata_only'] = False
            if metadata_only(entry):
                try:
                    verify_documents(a, b, entry['differences'])
                    entry['metadata_only'] = True
                except ValueError:
                    pass
        except (OSError, ValueError, KeyError) as error:
            entry.update(state='unverified', reason=str(error))
        results.append(entry)
        if i % 25 == 0:
            print(f'compared {i}/{len(rows)}', flush=True)
    if registry.read_bytes() != before:
        raise ValueError('registry changed')
    return {'schema': 'openrpg.variant-glb-difference-audit.v1', 'registry_sha256': pin,
            'pack': pack, 'results': results}


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--vault', type=Path, required=True)
    parser.add_argument('--volumes', type=Path, required=True)
    parser.add_argument('--comparison', type=Path, required=True)
    parser.add_argument('--pack', required=True)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    result = audit(args.vault, args.volumes, args.comparison, args.pack)
    with args.output.open('x', encoding='utf8', newline='\n') as stream:
        json.dump(result, stream, ensure_ascii=False, indent=2)
        stream.write('\n')
    print(json.dumps({'pairs': len(result['results']),
                      'metadata_only': sum(x.get('metadata_only', False) for x in result['results']),
                      'unverified': sum(x['state'] == 'unverified' for x in result['results'])}))
