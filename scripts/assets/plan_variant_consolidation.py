"""Build a conservative, non-mutating plan for near-identical model pairs."""
import argparse
import csv
import hashlib
import json
import re
from pathlib import Path

from prepare_variant_reference_patterns import vault_uuid


def sha(data):
    return hashlib.sha256(data).hexdigest()


def only_node_asset_ids(record):
    return (record.get('status') == 'compared'
            and record.get('binary_chunk_exact') is True
            and record.get('difference_paths_truncated') is False
            and bool(record.get('json_difference_paths'))
            and all(re.fullmatch(r'\$/nodes/\d+/extras/asset_id', path)
                    for path in record['json_difference_paths']))


def make_plan(report, vault, output):
    registry_path = vault / 'metadata/asset-import-registry.v1.json'
    registry_before = registry_path.read_bytes()
    expected = json.loads((report / 'resume.json').read_text(encoding='utf-8'))['registry_sha256']
    if sha(registry_before) != expected:
        raise ValueError('registry changed; regenerate evidence')
    refs = json.loads((report / 'candidats-23-references-ids-complet-catalogues.json').read_text(encoding='utf-8'))
    if refs['import_registry_sha256'] != expected:
        raise ValueError('reference audit used another registry')
    with (report / 'variantes-paires.csv').open(encoding='utf-8-sig', newline='') as f:
        pairs = {r['id']: r for r in csv.DictReader(f)}
    with (report / 'candidats-23-allocation-conditionnelle.csv').open(encoding='utf-8-sig', newline='') as f:
        sizes = {r['id']: r for r in csv.DictReader(f)}
    differences = [json.loads(s) for s in (report / 'candidats-23-ecarts-glb.jsonl').read_text(encoding='utf-8').splitlines()]
    rows = []
    for difference in differences:
        if not only_node_asset_ids(difference):
            raise ValueError('candidate differs beyond node identity: ' + difference['id'])
        pair = pairs[difference['id']]
        size = sizes[difference['id']]
        for source_key, pair_key in [('main_path', 'main_path'), ('variant_path', 'variant_path')]:
            if difference[source_key] != pair[pair_key]:
                raise ValueError('model path differs between evidence files')
        if size['retained_id'] != pair['main_id'] or size['variant_id'] != pair['variant_id']:
            raise ValueError('identities differ between evidence files')
        main_refs = refs['references'].get(pair['main_id'], [])
        variant_refs = refs['references'].get(pair['variant_id'], [])
        if not main_refs or not variant_refs:
            raise ValueError('expected active reference evidence missing')
        rows.append({'retained_id': pair['main_id'], 'retired_id': pair['variant_id'],
                     'retained_uuid': vault_uuid(pair['main_id']),
                     'retired_uuid': vault_uuid(pair['variant_id']),
                     'retained_path': pair['main_path'], 'retired_path': pair['variant_path'],
                     'retained_sha256': pair['main_sha'], 'retired_sha256': pair['variant_sha'],
                     'metadata_differences': pair['different_metadata'],
                     'retired_active_references': variant_refs,
                     'conditional_variant_allocation_bytes': int(size['allocation_estimate_bytes']),
                     'action': 'migrate_references_and_preserve_metadata_before_retirement',
                     'ready_for_removal': False})
    if registry_path.read_bytes() != registry_before:
        raise ValueError('registry changed while planning')
    plan = {'schema': 'openrpg.variant-consolidation-plan.v1', 'registry_sha256': expected,
            'selection': 'principal model when payload differs only in node asset_id',
            'state': 'planning', 'candidate_count': len(rows),
            'conditional_allocation_bytes': sum(r['conditional_variant_allocation_bytes'] for r in rows),
            'limitations': ['active and historical project references not yet migrated',
                            'source metadata must be preserved',
                            'logical model hashes differ',
                            'Studio placement and export paths need qualification'], 'candidates': rows}
    with output.open('x', encoding='utf-8', newline='\n') as f:
        json.dump(plan, f, ensure_ascii=False, indent=2)
        f.write('\n')
    return plan


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--report', type=Path, required=True)
    parser.add_argument('--vault', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    result = make_plan(args.report, args.vault, args.output)
    print(json.dumps({k: result[k] for k in ('state','candidate_count','conditional_allocation_bytes')}))
