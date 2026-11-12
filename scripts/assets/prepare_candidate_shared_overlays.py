"""Prepare complete, non-destructive shared overlays for candidate packages.

The plan identifies byte-identical source and retained files. This tool writes
only to a new work directory, validates every packaged file, and never changes
the Vault, publishes overlays, or authorizes removing candidate dependencies.
"""
import argparse
import collections
import json
from pathlib import Path

import vault_storage as storage
from unity_shared_overlay import UnitySourceResolver, file_pin, inventory_tree


def prepare(vault, plan_path, output, limit=None):
    vault = vault.resolve(strict=True)
    plan_raw = plan_path.read_bytes()
    plan = json.loads(plan_raw)
    if plan.get('schema') != 'openrpg.candidate-dependency-sharing-plan.v1' or plan.get('state') != 'prepared_only':
        raise ValueError('invalid candidate sharing plan')
    source_ptr = storage.confined(vault, vault / 'metadata/asset-source-support-registry.v1.json')
    model_ptr = storage.confined(vault, vault / 'metadata/asset-import-registry.v1.json')
    source_before, model_before = source_ptr.read_bytes(), model_ptr.read_bytes()
    grouped = collections.defaultdict(dict)
    old_paths = set()
    for row in plan['rows']:
        old = UnitySourceResolver._relative(row['old'])
        keep = UnitySourceResolver._relative(row['retained'])
        if (len(old.parts) < 4 or old.parts[:2] != ('candidates', 'hivemind') or
                len(keep.parts) < 4 or keep.parts[:2] != ('candidates', 'hivemind') or
                not storage.valid_hex(row['sha256'], 64) or type(row['bytes']) is not int or row['bytes'] <= 0):
            raise ValueError('invalid candidate alias')
        key = old.as_posix().casefold()
        if key in old_paths or old == keep:
            raise ValueError('duplicate alias')
        old_paths.add(key)
        package = Path(*old.parts[:3]).as_posix()
        logical = Path(*old.parts[3:]).as_posix().casefold()
        grouped[package][logical] = row
    if not grouped or any(row['retained'].casefold() in old_paths for row in plan['rows']):
        raise ValueError('empty or cyclic alias plan')
    if output.exists():
        raise ValueError('output already exists')
    output.mkdir()
    records = []
    retained_cache = {}
    packages = sorted(grouped, key=str.casefold)
    if limit is not None:
        if limit < 1:
            raise ValueError('limit must be positive')
        packages = packages[:limit]
    for number, package in enumerate(packages, 1):
        root = storage.confined(vault, vault / package)
        files, directories = inventory_tree(root)
        aliases = grouped[package]
        if not set(aliases).issubset(files):
            raise ValueError('candidate file missing: ' + package)
        entries = []
        for key, (logical, path) in sorted(files.items()):
            pin, size = file_pin(storage.confined(vault, path))
            row = aliases.get(key)
            physical = path.relative_to(vault).as_posix()
            if row is not None:
                if (pin, size) != (row['sha256'], row['bytes']):
                    raise ValueError('candidate file changed: ' + physical)
                keep = row['retained']
                if keep not in retained_cache:
                    retained_cache[keep] = file_pin(storage.confined(vault, vault / keep))
                if retained_cache[keep] != (pin, size):
                    raise ValueError('retained file changed: ' + keep)
                physical = keep
            entries.append({'logical': logical, 'vault_path': physical, 'sha256': pin, 'bytes': size})
        manifest = {'schema': 'openrpg.unity-shared-overlay.v1', 'state': 'prepared_only',
                    'source_root': package, 'source_registry_sha256': storage.sha(source_before),
                    'model_registry_sha256': storage.sha(model_before), 'comparison_sha256': storage.sha(plan_raw),
                    'directories': directories, 'files': entries, 'shared_files': len(aliases),
                    'logical_bytes': sum(row['bytes'] for row in entries),
                    'limitations': 'Preparation only; no production reader integration or deletion.'}
        raw = (json.dumps(manifest, ensure_ascii=False, sort_keys=True, indent=2) + '\n').encode('utf8')
        path = output / (f'{number:03d}-' + package.split('/')[-1] + '.json')
        path.write_bytes(raw)
        records.append({'package': package, 'manifest': path.name, 'sha256': storage.sha(raw),
                        'files': len(entries), 'shared_files': len(aliases),
                        'logical_bytes': manifest['logical_bytes']})
        print(json.dumps({'prepared': number, 'total': len(packages), 'package': package,
                          'shared_files': len(aliases)}), flush=True)
    if source_ptr.read_bytes() != source_before or model_ptr.read_bytes() != model_before or plan_path.read_bytes() != plan_raw:
        raise ValueError('registries or plan changed during preparation')
    index = {'state': 'prepared_only', 'plan_sha256': storage.sha(plan_raw),
             'source_registry_sha256': storage.sha(source_before), 'model_registry_sha256': storage.sha(model_before),
             'packages': records, 'full_plan': len(records) == len(grouped),
             'limitations': 'No publication, reader deployment, or deletion.'}
    (output / 'index.json').write_text(json.dumps(index, ensure_ascii=False, indent=2) + '\n', encoding='utf8')
    return index


if __name__ == '__main__':
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument('--vault', required=True, type=Path)
    p.add_argument('--plan', required=True, type=Path)
    p.add_argument('--output', required=True, type=Path)
    p.add_argument('--limit', type=int)
    args = p.parse_args()
    work = Path('E:/OpenRPG_Forge/.build/asset-conversion').resolve(strict=True)
    if args.output.parent.resolve(strict=True) != work:
        p.error('output must be a direct child of the asset-conversion work root')
    result = prepare(args.vault, args.plan, args.output, args.limit)
    print(json.dumps({'state': result['state'], 'packages': len(result['packages']),
                      'full_plan': result['full_plan']}))
