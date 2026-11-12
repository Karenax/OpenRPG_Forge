"""Verify absence, active references and retained logical models after removal."""
import argparse
import hashlib
import json
import os
from pathlib import Path

import retire_consolidated_models
import vault_storage
from variant_legacy_recipe import MAX_CONSOLIDATION_MODEL_BYTES


def verify(vault, plan, receipt, result):
    vault = vault.resolve(strict=True)
    rows = plan['candidates']
    if result.get('state') != 'removed_pending_verification' or len(result.get('removed', [])) != len(rows):
        raise ValueError('retirement result incomplete')
    expected = {retire_consolidated_models.normalized(x['retired_path']) for x in rows}
    removed = {retire_consolidated_models.normalized(x['path']) for x in result['removed']}
    if len(expected) != len(rows) or removed != expected:
        raise ValueError('retirement paths differ from plan')
    for row in rows:
        path = Path(row['retired_path'])
        if not path.is_absolute() or not path.is_relative_to(vault):
            raise ValueError('retired path outside Vault')
        vault_storage.confined(vault, path.parent)
        if os.path.lexists(path):
            raise ValueError('retired path still exists: ' + str(path))
    registry, support, active = retire_consolidated_models.active_references(vault, rows)
    if hashlib.sha256(registry).hexdigest() != receipt['registry_sha256'] or hashlib.sha256(support).hexdigest() != receipt['source_registry_sha256']:
        raise ValueError('registry changed after retirement')
    retained = {}
    for row in rows:
        if row['retired_id'] in active or row['retained_id'] not in active:
            raise ValueError('active identities differ from plan')
        path = vault_storage.confined(vault, Path(row['retained_path']))
        if path in retained and retained[path] != row['retained_sha256']:
            raise ValueError('conflicting retained SHA')
        retained[path] = row['retained_sha256']
    for index, (path, expected_sha) in enumerate(retained.items(), 1):
        if vault_storage.digest(vault, path, MAX_CONSOLIDATION_MODEL_BYTES) != expected_sha:
            raise ValueError('retained logical model SHA mismatch')
        if index % 25 == 0 or index == len(retained):
            print(f'Verified {index}/{len(retained)} retained logical models', flush=True)
    if hashlib.sha256((vault / 'metadata/asset-import-registry.v1.json').read_bytes()).hexdigest() != receipt['registry_sha256']:
        raise ValueError('registry changed during verification')
    return {'state': 'verified_paths_and_retained_models', 'retired_absent': len(rows),
            'retained_models_verified': len(retained), 'active_retired_references': 0,
            'registry_sha256': receipt['registry_sha256'],
            'free_delta_observed': result['free_delta_observed'],
            'limitations': 'External projects and saves are not exhaustively scanned; published legacy reference tests are separate.'}


if __name__ == '__main__':
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument('--vault', type=Path, required=True)
    p.add_argument('--plan', type=Path, required=True)
    p.add_argument('--receipt', type=Path, required=True)
    p.add_argument('--retirement', type=Path, required=True)
    p.add_argument('--output', type=Path, required=True)
    args = p.parse_args()
    if args.output.exists():
        raise ValueError('output exists')
    result = verify(args.vault, json.loads(args.plan.read_bytes()),
                    json.loads(args.receipt.read_bytes()), json.loads(args.retirement.read_bytes()))
    with args.output.open('x', encoding='utf8', newline='\n') as stream:
        json.dump(result, stream, ensure_ascii=False, indent=2)
        stream.write('\n')
    print(json.dumps(result))
