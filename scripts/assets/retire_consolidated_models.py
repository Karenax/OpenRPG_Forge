"""Retire published duplicate model files with pinned compatibility evidence.

This never touches original Unity sources, shared packs or catalogues. A partial
run is recorded line by line; the caller must verify Studio and exports again.
"""
import argparse
import contextlib
import hashlib
import json
import os
from pathlib import Path
import shutil
import stat
import sys

import vault_storage
import publish_variant_transition
import variant_legacy_recipe
import validate_no_legacy_reads
import audit_candidate_local_references
from publish_variant_transition import digest, exclusive_storage_lock
from variant_legacy_recipe import verify_recipe, MAX_CONSOLIDATION_MODEL_BYTES


def normalized(value):
    return value.replace('\\', '/').casefold().removeprefix('//?/')


def protected_model_paths(vault, candidates):
    """Allow a repeated retained model, never a repeated retired or crossed path."""
    paths = {}
    for item in candidates:
        for side in ('retained', 'retired'):
            path = vault_storage.confined(vault, Path(item[side + '_path']))
            relative = path.relative_to(vault)
            usual = bool(relative.parts) and relative.parts[0].lower() in ('normalized', 'candidates', 'animation')
            delivered_witch = (side == 'retained' and len(relative.parts) == 3
                               and relative.parts[0].lower() == 'witch-house-urp-20261108-r2'
                               and relative.parts[1].lower() == 'models')
            if not (usual or delivered_witch) or path.suffix.lower() != '.glb':
                raise ValueError('model outside allowed payload directories')
            previous = paths.get(path)
            current = (side, item[side + '_sha256'])
            if previous is not None and (side != 'retained' or previous != current):
                raise ValueError('model path reused in retirement batch')
            paths[path] = current
    return paths


def active_references(vault, candidates):
    index = vault_storage.confined(vault, vault / 'metadata/asset-import-registry.v1.json')
    support_index = vault_storage.confined(vault, vault / 'metadata/asset-source-support-registry.v1.json')
    before, support_before = index.read_bytes(), support_index.read_bytes()
    registry, support = json.loads(before), json.loads(support_before)
    support_path = vault_storage.confined(vault, vault / support['path'])
    support_bytes = support_path.read_bytes()
    if hashlib.sha256(support_bytes).hexdigest() != support['sha256']:
        raise ValueError('source-support catalogue changed')
    targets = {normalized(c['retired_path']) for c in candidates}
    retained_ids = {c['retained_id'] for c in candidates}
    if len(targets) != len(candidates):
        raise ValueError('duplicate retirement paths')
    hits = []
    def visit(value, path, allow_preserved_history=False):
        if isinstance(value, dict):
            for key, child in value.items():
                if key == 'consolidated_variants' and allow_preserved_history and value.get('id') in retained_ids:
                    continue
                visit(child, path + '/' + str(key), allow_preserved_history)
        elif isinstance(value, list):
            for i, child in enumerate(value):
                visit(child, path + '/' + str(i), allow_preserved_history)
        elif isinstance(value, str) and normalized(value) in targets:
            hits.append(path)
    visit(json.loads(support_bytes), 'source-support')
    details_by_id = {}
    for pack, entry in sorted(registry['packs'].items()):
        root = vault_storage.confined(vault, vault / entry['path'])
        for filename in ('details.v1.json', 'profiles.v1.json'):
            raw = vault_storage.confined(vault, root / filename).read_bytes()
            if hashlib.sha256(raw).hexdigest() != entry['files'][filename]:
                raise ValueError('active metadata SHA mismatch: ' + pack)
            doc = json.loads(raw)
            visit(doc, pack + '/' + filename, filename == 'details.v1.json')
            if filename == 'details.v1.json':
                for asset in doc['assets']:
                    if asset['id'] in details_by_id:
                        raise ValueError('duplicate active ID')
                    details_by_id[asset['id']] = asset
    if index.read_bytes() != before or support_index.read_bytes() != support_before:
        raise ValueError('registry changed while checking references')
    if hits:
        raise ValueError('active references still target retired paths: ' + ', '.join(hits[:5]))
    return before, support_before, details_by_id


def preflight(vault, plan, stage, receipt):
    vault = Path(vault).resolve(strict=True)
    if receipt.get('candidate_reference_audit_sha256') != digest(audit_candidate_local_references.__file__):
        raise ValueError('candidate-reference audit changed or not qualified')
    local = audit_candidate_local_references.find_references(vault, plan['candidates'])
    if any(row['references'] for row in local):
        raise ValueError('candidate promotion metadata still references retired model')
    if (receipt.get('schema') != 'openrpg.variant-retirement-qualification.v1'
            or receipt.get('state') != 'ready' or receipt.get('vault') != str(vault)
            or receipt.get('stage_manifest_sha256') != digest(stage / 'transition.json')):
        raise ValueError('retirement qualification missing or stale')
    studio = Path(receipt.get('studio', ''))
    if not studio.is_absolute() or not studio.is_file() or digest(studio) != receipt.get('studio_sha256'):
        raise ValueError('qualified Studio changed')
    proof = Path(receipt.get('exclusive_read_proof', ''))
    if not proof.is_absolute() or not proof.is_file() or digest(proof) != receipt.get('exclusive_read_proof_sha256'):
        raise ValueError('exclusive-read proof changed')
    evidence = json.loads(proof.read_bytes())
    if (evidence.get('state') != 'passed' or evidence.get('exclusive_legacy_paths') != len(plan['candidates'])
            or evidence.get('registry_sha256') != receipt.get('registry_sha256')
            or evidence.get('plan_fingerprint') != validate_no_legacy_reads.plan_fingerprint(plan)
            or evidence.get('stage_manifest_sha256') != receipt.get('stage_manifest_sha256')
            or evidence.get('models_removed') != 0):
        raise ValueError('exclusive-read proof incomplete')
    command = evidence.get('child_command')
    if not isinstance(command, list) or not command or not Path(command[0]).is_file() or digest(command[0]) != evidence.get('test_binary_sha256'):
        raise ValueError('qualified native test binary changed')
    baseline = evidence.get('export_baseline')
    if baseline:
        path = Path(baseline)
        if not path.is_absolute() or not path.is_file() or digest(path) != evidence.get('export_baseline_sha256'):
            raise ValueError('qualified export baseline changed')
    for key, module in (('retirement_tool_sha256', __file__), ('storage_reader_sha256', vault_storage.__file__),
                        ('publisher_tool_sha256', publish_variant_transition.__file__),
                        ('recipe_tool_sha256', variant_legacy_recipe.__file__),
                        ('proof_tool_sha256', validate_no_legacy_reads.__file__)):
        if receipt.get(key) != digest(module):
            raise ValueError('qualified retirement tool changed: ' + key)
    registry, support, active = active_references(vault, plan['candidates'])
    if hashlib.sha256(registry).hexdigest() != receipt.get('registry_sha256'):
        raise ValueError('published registry changed')
    if hashlib.sha256(support).hexdigest() != receipt.get('source_registry_sha256'):
        raise ValueError('source-support registry changed')
    manifest = json.loads((stage / 'transition.json').read_bytes())
    current = json.loads(registry)
    for pack, pins in manifest['packs'].items():
        if current['packs'][pack]['revision'] != pins['revision']:
            raise ValueError('published pack revision changed: ' + pack)
    for item in plan['candidates']:
        if item['retired_id'] in active or item['retained_id'] not in active:
            raise ValueError('active identities are not consolidated')
        current_asset = active[item['retained_id']]
        if current_asset.get('model_sha256') != item['retained_sha256'] or normalized(current_asset.get('model_path', '')) != normalized(item['retained_path']):
            raise ValueError('retained asset changed')
        matches = [v for v in current_asset.get('consolidated_variants', []) if v.get('retired_id') == item['retired_id']]
        if len(matches) != 1 or matches[0].get('retired_model_sha256') != item['retired_sha256']:
            raise ValueError('legacy recipe metadata missing')
    return registry, support, active


def retire(vault, plan, stage, receipt, importer, journal, allow_readonly=False):
    vault = Path(vault).resolve(strict=True)
    stage = Path(stage).resolve(strict=True)
    if Path(journal).exists():
        raise ValueError('journal already exists')
    removed = []
    before_free = shutil.disk_usage(vault).free
    with importer.locked(vault), exclusive_storage_lock(vault), contextlib.ExitStack() as handles:
        registry, support, active = preflight(vault, plan, stage, receipt)
        if digest(importer.__file__) != receipt.get('canonical_importer_sha256'):
            raise ValueError('canonical importer changed')
        proof = Path(receipt['exclusive_read_proof'])
        evidence = json.loads(proof.read_bytes())
        for critical in (Path(receipt['studio']), proof, Path(evidence['child_command'][0]),
                         stage / 'transition.json', Path(__file__), Path(vault_storage.__file__),
                         Path(publish_variant_transition.__file__), Path(variant_legacy_recipe.__file__),
                         Path(validate_no_legacy_reads.__file__), Path(importer.__file__)):
            handles.enter_context(vault_storage.locked_source(critical, share_delete=False))
        if evidence.get('export_baseline'):
            handles.enter_context(vault_storage.locked_source(Path(evidence['export_baseline']), share_delete=False))
        preflight(vault, plan, stage, receipt)
        protected = {}
        for path, (side, _) in protected_model_paths(vault, plan['candidates']).items():
            if side == 'retired' and vault_storage.windows_readonly(path.stat()) and not allow_readonly:
                raise ValueError('retired file is read-only; original preserved')
            protected[path] = handles.enter_context(vault_storage.locked_source(path, share_delete=(side == 'retired')))
        for number, item in enumerate(plan['candidates'], 1):
            keep = vault_storage.confined(vault, Path(item['retained_path']))
            old = vault_storage.confined(vault, Path(item['retired_path']))
            current = active[item['retained_id']]
            variant = next(v for v in current['consolidated_variants'] if v['retired_id'] == item['retired_id'])
            raw = vault_storage.read_bytes(vault, keep, MAX_CONSOLIDATION_MODEL_BYTES)
            verify_recipe(raw, variant['legacy_recipe'], item['retained_sha256'], item['retired_sha256'],
                          vault=vault, retired_path=old)
            if (variant['legacy_recipe'].get('schema') != 'openrpg.legacy-vault-descriptor.v1'
                    and vault_storage.digest(vault, old) != item['retired_sha256']):
                raise ValueError('retired logical SHA changed')
            if number % 25 == 0 or number == len(plan['candidates']):
                print(f"Reverified {number}/{len(plan['candidates'])} retirement pairs", flush=True)
        index = vault / 'metadata/asset-import-registry.v1.json'
        support_index = vault / 'metadata/asset-source-support-registry.v1.json'
        if index.read_bytes() != registry or support_index.read_bytes() != support:
            raise ValueError('registry changed before retirement')
        with Path(journal).open('x', encoding='utf-8', newline='\n') as log:
            def record(value):
                log.write(json.dumps(value, ensure_ascii=False) + '\n')
                log.flush()
                os.fsync(log.fileno())
            record({'state': 'prepared', 'count': len(plan['candidates']), 'free_before': before_free,
                    'registry_sha256': receipt['registry_sha256']})
            for number, item in enumerate(plan['candidates'], 1):
                if index.read_bytes() != registry or support_index.read_bytes() != support:
                    raise ValueError('registry changed during retirement')
                path = vault_storage.confined(vault, Path(item['retired_path']))
                stream = protected[path]
                locked, current = os.fstat(stream.fileno()), path.stat()
                if (locked.st_ino, locked.st_size, locked.st_mtime_ns) != (current.st_ino, current.st_size, current.st_mtime_ns):
                    raise ValueError('retired file changed after verification')
                record({'state': 'verified', 'path': str(path), 'logical_sha256': item['retired_sha256'], 'physical_bytes': current.st_size})
                readonly = vault_storage.windows_readonly(current)
                if readonly:
                    if not allow_readonly or os.name != 'nt':
                        raise ValueError('retired file is read-only; original preserved')
                    old_mode = stat.S_IMODE(current.st_mode)
                    record({'state': 'readonly_clearing', 'path': str(path), 'original_mode': old_mode})
                    try:
                        os.chmod(path, old_mode | stat.S_IWRITE)
                        path.unlink()
                    except BaseException:
                        if path.exists():
                            os.chmod(path, old_mode)
                            record({'state': 'readonly_restored', 'path': str(path)})
                        raise
                else:
                    path.unlink()
                removed.append({'path': str(path), 'physical_bytes': current.st_size})
                record({'state': 'removed', 'path': str(path)})
                if number % 25 == 0 or number == len(plan['candidates']):
                    print(f"Removed {number}/{len(plan['candidates'])} verified files", flush=True)
            record({'state': 'removed_pending_verification', 'files': len(removed)})
    # Windows may defer reclaiming unlinked files until the last handle closes.
    after_free = shutil.disk_usage(vault).free
    with Path(journal).open('a', encoding='utf8') as log:
        log.write(json.dumps({'state': 'handles_closed', 'free_after': after_free}) + '\n')
        log.flush()
        os.fsync(log.fileno())
    return {'state': 'removed_pending_verification', 'removed': removed,
            'free_before': before_free, 'free_after': after_free,
            'free_delta_observed': after_free - before_free}


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--vault', type=Path, required=True)
    parser.add_argument('--plan', type=Path, required=True)
    parser.add_argument('--stage', type=Path, required=True)
    parser.add_argument('--receipt', type=Path, required=True)
    parser.add_argument('--journal', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--importer-scripts', type=Path, required=True)
    parser.add_argument('--allow-readonly-retirement', action='store_true',
                        help='Clear the Windows read-only attribute only on verified retired copies')
    args = parser.parse_args()
    if args.output.exists() or args.journal.exists():
        raise ValueError('output or journal already exists')
    sys.path.insert(0, str(args.importer_scripts.resolve(strict=True)))
    import asset_import
    plan = vault_storage.decode_json(args.plan.read_bytes())
    receipt = vault_storage.decode_json(args.receipt.read_bytes())
    result = retire(args.vault, plan, args.stage, receipt, asset_import, args.journal,
                    allow_readonly=args.allow_readonly_retirement)
    with args.output.open('x', encoding='utf8', newline='\n') as stream:
        json.dump(result, stream, ensure_ascii=False, indent=2)
        stream.write('\n')
    print(json.dumps({'state': result['state'], 'files': len(result['removed']),
                      'free_delta_observed': result['free_delta_observed']}))
