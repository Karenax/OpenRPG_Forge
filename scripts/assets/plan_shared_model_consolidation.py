"""Prepare aliases for pairs already sharing one verified logical model.

Read-only with respect to the Vault; output is a plan and legacy recipes, not
a publication or permission to delete the shared model.
"""
import argparse
import csv
import hashlib
import json
from pathlib import Path

import vault_storage
from prepare_variant_metadata_transition import load_packs, prepare_transition
from prepare_variant_reference_patterns import vault_uuid
from variant_legacy_recipe import build_recipe


def make_plan(vault, candidates_csv):
    vault = vault.resolve(strict=True)
    registry = vault / 'metadata/asset-import-registry.v1.json'
    registry_raw = registry.read_bytes()
    pin = hashlib.sha256(registry_raw).hexdigest()
    with candidates_csv.open(encoding='utf-8-sig', newline='') as stream:
        rows = [r for r in csv.DictReader(stream) if r['same_path'] == 'True']
    items = []
    seen = set()
    for row in rows:
        keep, retired = row['main_id'], row['variant_id']
        if keep == retired or keep in seen or retired in seen:
            raise ValueError('overlapping identities')
        seen.update((keep, retired))
        if (row['main_path'] != row['variant_path'] or row['main_sha'] != row['variant_sha']
                or row['same_path_logical_hash_verified'] != 'match'):
            raise ValueError('old evidence does not establish a shared model')
        path = vault_storage.confined(vault, Path(row['main_path']))
        items.append({'retained_id': keep, 'retired_id': retired,
                      'retained_uuid': vault_uuid(keep), 'retired_uuid': vault_uuid(retired),
                      'retained_path': str(path), 'retired_path': str(path),
                      'retained_sha256': row['main_sha'], 'retired_sha256': row['variant_sha'],
                      'metadata_differences': row['different_metadata'],
                      'action': 'preserve_shared_model_and_legacy_metadata',
                      'ready_for_removal': False})
    if not items:
        raise ValueError('no candidates')
    plan = {'schema': 'openrpg.variant-consolidation-plan.v1', 'registry_sha256': pin,
            'selection': 'same active path and logical SHA', 'state': 'planning',
            'candidate_count': len(items), 'candidates': items}
    packs = load_packs(vault, plan)
    prepare_transition(packs, items)
    recipes = {}
    for index, item in enumerate(items, 1):
        path = vault_storage.confined(vault, Path(item['retained_path']))
        raw = vault_storage.read_bytes(vault, path, 512 * 1024 * 1024)
        recipes[item['retired_id']] = build_recipe(raw, raw, item['retained_sha256'], item['retired_sha256'])
        if index % 50 == 0:
            print('verified', index, 'of', len(items), flush=True)
    if registry.read_bytes() != registry_raw:
        raise ValueError('registry changed during planning')
    return plan, {'state': 'prepared_only', 'registry_sha256': pin, 'recipes': recipes}


def write_new(path, data):
    with path.open('x', encoding='utf-8', newline='\n') as stream:
        json.dump(data, stream, ensure_ascii=False, indent=2)
        stream.write('\n')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--vault', type=Path, required=True)
    parser.add_argument('--candidates', type=Path, required=True)
    parser.add_argument('--plan', type=Path, required=True)
    parser.add_argument('--recipes', type=Path, required=True)
    args = parser.parse_args()
    if args.plan.exists() or args.recipes.exists():
        raise ValueError('output already exists')
    plan, recipes = make_plan(args.vault, args.candidates)
    write_new(args.plan, plan)
    write_new(args.recipes, recipes)
    print(json.dumps({'state': plan['state'], 'candidates': plan['candidate_count']}))
