"""Atomically publish a verified multi-pack consolidation; never remove models.

The actual Vault must have a separate, current compatibility receipt. This
publisher is intentionally distinct from the later guarded physical retirement.
"""
import copy
import contextlib
import argparse
import hashlib
import json
import os
from pathlib import Path
import re
import sys
import uuid

import vault_storage
import prepare_variant_metadata_transition
import variant_legacy_recipe
import validate_no_legacy_reads
from prepare_variant_metadata_transition import load_packs, verify_stage
from variant_legacy_recipe import verify_recipe, MAX_CONSOLIDATION_MODEL_BYTES


def digest(path):
    with Path(path).open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


@contextlib.contextmanager
def exclusive_storage_lock(vault):
    home = vault_storage.confined(vault, vault / '.forge-storage')
    path = home / 'WRITER.lock'
    owner = uuid.uuid4().hex
    with path.open('x', encoding='utf-8') as stream:
        json.dump({'owner': owner, 'pid': os.getpid(), 'purpose': 'variant-consolidation'}, stream)
        stream.flush()
        os.fsync(stream.fileno())
    try:
        yield owner
    finally:
        if path.exists() and json.loads(path.read_text(encoding='utf-8')).get('owner') == owner:
            path.unlink()


def ensure_vault_directory(vault, parts):
    root = vault
    for part in parts:
        root = root / part
        if not root.exists():
            root.mkdir()
        vault_storage.confined(vault, root)
    return root


def check_receipt(receipt, vault, stage, plan):
    if (receipt.get('schema') != 'openrpg.variant-reader-qualification.v1'
            or receipt.get('state') != 'ready'
            or receipt.get('vault') != str(vault)
            or receipt.get('registry_sha256') != plan['registry_sha256']
            or receipt.get('stage_manifest_sha256') != digest(stage / 'transition.json')):
        raise ValueError('missing or stale compatibility qualification')
    studio = Path(receipt.get('studio', ''))
    if not studio.is_absolute() or not studio.is_file() or digest(studio) != receipt.get('studio_sha256'):
        raise ValueError('qualified Studio differs from deployed executable')
    for key, source in (('publisher_sha256', __file__), ('storage_reader_sha256', vault_storage.__file__),
                        ('transition_tool_sha256', prepare_variant_metadata_transition.__file__),
                        ('recipe_tool_sha256', variant_legacy_recipe.__file__)):
        if digest(source) != receipt.get(key):
            raise ValueError('qualified publication tool differs: ' + key)
    required = {'native_legacy_reference', 'real_recipes',
                'studio_library', 'stage_snapshot_integrity', 'recovery'}
    if not isinstance(receipt.get('checks'), dict) or any(receipt['checks'].get(k) != 'passed' for k in required):
        raise ValueError('compatibility qualification incomplete')
    export = receipt['checks'].get('native_legacy_export')
    if export == 'baseline_matched':
        baseline = Path(receipt.get('export_baseline', ''))
        if not baseline.is_absolute() or not baseline.is_file():
            raise ValueError('export baseline missing')
        if digest(validate_no_legacy_reads.__file__) != receipt.get('baseline_validator_sha256'):
            raise ValueError('export baseline validator changed')
        sha = validate_no_legacy_reads.validate_export_baseline(
            baseline.read_bytes(), (stage / 'transition.json').read_bytes())
        if sha != receipt.get('export_baseline_sha256'):
            raise ValueError('export baseline changed')
    elif export != 'passed':
        raise ValueError('export qualification incomplete')


def prepare_entries(vault, stage, plan, recipes, importer):
    registry = importer.registry(vault)
    original = load_packs(vault, plan)
    verify_stage(stage, original, plan, recipes)
    manifest = json.loads((stage / 'transition.json').read_bytes())
    if not manifest.get('includes_legacy_recipes'):
        raise ValueError('legacy recipes are required')
    for index, item in enumerate(plan['candidates'], 1):
        for side in ('retained', 'retired'):
            path = vault_storage.confined(vault, Path(item[side + '_path']))
            if side == 'retained':
                raw = vault_storage.read_bytes(vault, path, MAX_CONSOLIDATION_MODEL_BYTES)
                verify_recipe(raw, recipes[item['retired_id']], item['retained_sha256'], item['retired_sha256'],
                              vault=vault, retired_path=Path(item['retired_path']))
            elif (recipes[item['retired_id']].get('schema') != 'openrpg.legacy-vault-descriptor.v1'
                  and vault_storage.digest(vault, path) != item['retired_sha256']):
                raise ValueError('retired model changed')
        if index % 25 == 0 or index == len(plan['candidates']):
            print(f"Verified {index}/{len(plan['candidates'])} publication pairs", flush=True)
    return registry, manifest


def publish(vault, stage, plan, recipes, receipt, importer):
    vault = Path(vault).resolve(strict=True)
    stage = Path(stage).resolve(strict=True)
    check_receipt(receipt, vault, stage, plan)
    if digest(importer.__file__) != receipt.get('canonical_importer_sha256'):
        raise ValueError('canonical importer differs from qualification')
    with importer.locked(vault), exclusive_storage_lock(vault) as lock_owner, contextlib.ExitStack() as locked_models:
        if receipt['checks'].get('native_legacy_export') == 'baseline_matched':
            locked_models.enter_context(vault_storage.locked_source(Path(receipt['export_baseline']), share_delete=False))
            locked_models.enter_context(vault_storage.locked_source(Path(validate_no_legacy_reads.__file__), share_delete=False))
        vault_storage.confined(vault, vault / 'metadata/asset-import-registry.v1.json')
        if digest(vault / 'metadata/asset-import-registry.v1.json') != plan['registry_sha256']:
            raise ValueError('registry changed')
        protected = set()
        for item in plan['candidates']:
            for side in ('retained', 'retired'):
                path = vault_storage.confined(vault, Path(item[side + '_path']))
                if path not in protected:
                    locked_models.enter_context(vault_storage.locked_source(path, share_delete=False))
                    protected.add(path)
        registry, manifest = prepare_entries(vault, stage, plan, recipes, importer)
        next_registry = copy.deepcopy(registry)
        events = []
        for pack, pins in sorted(manifest['packs'].items()):
            importer.safe_id(pack)
            revision = pins['revision']
            if not re.fullmatch(r'[0-9a-f]{64}', revision):
                raise ValueError('invalid revision')
            target = ensure_vault_directory(vault, ('metadata', 'asset-import-revisions', pack, revision))
            files = {name: pins[name] for name in ('details.v1.json', 'profiles.v1.json')}
            for name, expected in files.items():
                raw = (stage / pack / name).read_bytes()
                if hashlib.sha256(raw).hexdigest() != expected:
                    raise ValueError('staged file changed')
                dest = target / name
                if dest.exists():
                    vault_storage.confined(vault, dest)
                    if digest(dest) != expected:
                        raise ValueError('immutable revision conflict')
                else:
                    with dest.open('xb') as stream:
                        stream.write(raw)
                        stream.flush()
                        os.fsync(stream.fileno())
            tx = uuid.uuid4().hex
            current = registry['packs'][pack]
            entry = {'revision': revision, 'path': target.relative_to(vault).as_posix(),
                     'files': files, 'transaction': tx}
            entry['history'] = importer.retained_history(vault, current, entry)
            next_registry['packs'][pack] = entry
            events.append({'schema_version': 1, 'operation': 'apply', 'pack_id': pack,
                           'previous': current, 'next': entry, 'state': 'prepared'})
        if digest(vault / 'metadata/asset-import-registry.v1.json') != plan['registry_sha256']:
            raise ValueError('registry changed during publication')
        group_id = uuid.uuid4().hex
        group_home = ensure_vault_directory(vault, ('metadata', 'asset-consolidation-history'))
        event_home = ensure_vault_directory(vault, ('metadata', 'asset-import-history'))
        group_path = group_home / (group_id + '.json')
        group = {'schema': 'openrpg.asset-consolidation-transaction.v1', 'state': 'prepared',
                 'previous_registry_sha256': plan['registry_sha256'], 'events': events}
        importer.atomic(group_path, group)
        for event in events:
            importer.atomic(event_home / (event['next']['transaction'] + '.json'), event)
        if digest(vault / 'metadata/asset-import-registry.v1.json') != plan['registry_sha256']:
            raise ValueError('registry changed before atomic swap')
        if json.loads((vault / '.forge-storage/WRITER.lock').read_text(encoding='utf8')).get('owner') != lock_owner:
            raise ValueError('storage lock ownership changed')
        check_receipt(receipt, vault, stage, plan)
        if digest(importer.__file__) != receipt['canonical_importer_sha256']:
            raise ValueError('canonical importer changed before atomic swap')
        importer.atomic(vault / 'metadata/asset-import-registry.v1.json', next_registry)
        group['state'] = 'committed'
        importer.atomic(group_path, group)
        for event in events:
            event['state'] = 'committed'
            importer.atomic(event_home / (event['next']['transaction'] + '.json'), event)
        return {'state': 'committed', 'group': group_id, 'packs': len(events),
                'models_removed': 0, 'physical_retirement': 'not_started'}


def recover(vault, group_path, importer):
    """Resolve a partial journal without changing the registry or deleting files."""
    vault = Path(vault).resolve(strict=True)
    group_path = vault_storage.confined(vault, Path(group_path))
    allowed = vault / 'metadata/asset-consolidation-history'
    if group_path.parent != allowed or group_path.suffix != '.json':
        raise ValueError('invalid consolidation journal path')
    with importer.locked(vault), exclusive_storage_lock(vault):
        group = json.loads(group_path.read_bytes())
        if group.get('schema') != 'openrpg.asset-consolidation-transaction.v1' or group.get('state') not in ('prepared', 'committed'):
            raise ValueError('invalid or already resolved consolidation journal')
        events = group.get('events')
        if not isinstance(events, list) or not events:
            raise ValueError('empty consolidation journal')
        registry_path = vault_storage.confined(vault, vault / 'metadata/asset-import-registry.v1.json')
        registry = importer.registry(vault)
        if digest(registry_path) == group['previous_registry_sha256']:
            if group['state'] != 'prepared':
                raise ValueError('committed journal with old registry')
            result = 'aborted'
        elif all(registry['packs'].get(event['pack_id']) == event['next'] for event in events):
            result = 'committed'
        else:
            raise ValueError('registry does not match either transaction state')
        event_home = vault_storage.confined(vault, vault / 'metadata/asset-import-history')
        pending = []
        for event in events:
            tx = event['next']['transaction']
            if not re.fullmatch(r'[0-9a-f]{32}', tx):
                raise ValueError('invalid transaction ID')
            path = event_home / (tx + '.json')
            if not path.exists():
                if result == 'committed':
                    raise ValueError('committed transaction journal missing')
                continue
            vault_storage.confined(vault, path)
            recorded = json.loads(path.read_bytes())
            if any(recorded.get(k) != event.get(k) for k in ('schema_version', 'operation', 'pack_id', 'previous', 'next')):
                raise ValueError('pack journal mismatch')
            recorded['state'] = result
            pending.append((path, recorded))
        for path, event in pending:
            importer.atomic(path, event)
        group['state'] = result
        importer.atomic(group_path, group)
        return {'state': result, 'packs': len(events), 'registry_changed': False, 'models_removed': 0}


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--vault', type=Path, required=True)
    parser.add_argument('--stage', type=Path, required=True)
    parser.add_argument('--plan', type=Path, required=True)
    parser.add_argument('--recipes', type=Path, required=True)
    parser.add_argument('--receipt', type=Path, required=True)
    parser.add_argument('--importer-scripts', type=Path, required=True)
    args = parser.parse_args()
    sys.path.insert(0, str(args.importer_scripts.resolve(strict=True)))
    import asset_import
    plan = vault_storage.decode_json(args.plan.read_bytes())
    recipe_document = vault_storage.decode_json(args.recipes.read_bytes())
    if recipe_document.get('state') != 'prepared_only' or recipe_document.get('registry_sha256') != plan['registry_sha256']:
        raise ValueError('recipe registry mismatch')
    receipt = vault_storage.decode_json(args.receipt.read_bytes())
    print(json.dumps(publish(args.vault, args.stage, plan, recipe_document['recipes'], receipt, asset_import), ensure_ascii=False))
