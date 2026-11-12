"""Publish complete candidate overlays without retiring any candidate files.

Existing direct-path consumers are unchanged. Publication is a reversible
registry addition, not permission to delete or a proof of consumer migration.
"""
import argparse
import json
import os
from pathlib import Path
import uuid

from publish_source_support_relinks import importer_module
from publish_variant_transition import ensure_vault_directory, exclusive_storage_lock
from unity_shared_overlay import UnitySourceResolver, configured_resolver, file_pin
import vault_storage as storage


def publish(vault, stage, plan_path, importer, apply=False):
    vault, stage = vault.resolve(strict=True), stage.resolve(strict=True)
    with importer.shared_import_lock(vault), exclusive_storage_lock(vault):
        registry = storage.confined(vault, vault / 'metadata/unity-shared-overlay-registry.v1.json')
        source = storage.confined(vault, vault / 'metadata/asset-source-support-registry.v1.json')
        models = storage.confined(vault, vault / 'metadata/asset-import-registry.v1.json')
        old_registry, source_raw, model_raw = registry.read_bytes(), source.read_bytes(), models.read_bytes()
        current = json.loads(old_registry)
        index_raw, plan_raw = (stage / 'index.json').read_bytes(), plan_path.read_bytes()
        index, plan = json.loads(index_raw), json.loads(plan_raw)
        if (current.get('schema') != 'openrpg.unity-shared-overlay-registry.v1' or
                index.get('state') != 'prepared_only' or index.get('full_plan') is not True or
                index.get('plan_sha256') != storage.sha(plan_raw) or
                index.get('source_registry_sha256') != storage.sha(source_raw) or
                index.get('model_registry_sha256') != storage.sha(model_raw)):
            raise ValueError('candidate overlay inputs are stale or incomplete')
        if plan.get('schema') != 'openrpg.candidate-dependency-sharing-plan.v1':
            raise ValueError('invalid candidate plan')
        expected = {r['old'].casefold(): r for r in plan['rows']}
        if len(expected) != len(plan['rows']) or any(r['retained'].casefold() in expected for r in plan['rows']):
            raise ValueError('duplicate or cyclic candidate mappings')
        records, manifests, found, roots, cache = [], [], set(), set(), {}
        for entry in index['packages']:
            package = entry['package']
            if not package.startswith('candidates/hivemind/') or len(Path(package).parts) != 3:
                raise ValueError('candidate package outside scope')
            key = package.casefold().rstrip('/') + '/'
            if key in roots:
                raise ValueError('duplicate candidate package')
            roots.add(key)
            for existing in current['overlays']:
                other = existing['source_root'].casefold().rstrip('/') + '/'
                if key.startswith(other) or other.startswith(key):
                    raise ValueError('candidate overlay overlaps existing registry')
            rel_manifest = Path(entry['manifest'])
            if rel_manifest.name != str(rel_manifest) or not rel_manifest.name.endswith('.json'):
                raise ValueError('invalid staged manifest name')
            path = stage / rel_manifest
            raw = path.read_bytes()
            if storage.sha(raw) != entry['sha256']:
                raise ValueError('candidate manifest SHA differs')
            doc = json.loads(raw)
            if (doc.get('schema') != 'openrpg.unity-shared-overlay.v1' or doc.get('source_root') != package or
                    doc.get('comparison_sha256') != storage.sha(plan_raw) or
                    doc.get('source_registry_sha256') != storage.sha(source_raw) or
                    doc.get('model_registry_sha256') != storage.sha(model_raw)):
                raise ValueError('candidate manifest identity differs')
            resolver = UnitySourceResolver(vault, path, entry['sha256'])
            if resolver.source_root != vault / package:
                raise ValueError('candidate source root differs')
            aliases = 0
            for item in doc['files']:
                old = (package + '/' + item['logical']).casefold()
                row = expected.get(old)
                if row is None:
                    if item['vault_path'].casefold() != old:
                        raise ValueError('unplanned candidate alias')
                    continue
                if (item['vault_path'].casefold() != row['retained'].casefold() or
                        (item['sha256'], item['bytes']) != (row['sha256'], row['bytes'])):
                    raise ValueError('candidate alias differs from plan')
                for relative in (row['old'], row['retained']):
                    if relative not in cache:
                        cache[relative] = file_pin(storage.confined(vault, vault / relative))
                    if cache[relative] != (row['sha256'], row['bytes']):
                        raise ValueError('candidate payload changed: ' + relative)
                found.add(old)
                aliases += 1
            if aliases != entry['shared_files'] or aliases != doc['shared_files']:
                raise ValueError('candidate alias count differs')
            records.append({'source_root': package, 'manifest': 'metadata/unity-shared-overlays/' + entry['sha256'] + '.json',
                            'sha256': entry['sha256']})
            manifests.append((path, raw))
        if found != set(expected):
            raise ValueError('candidate plan is not fully covered')
        if (registry.read_bytes() != old_registry or source.read_bytes() != source_raw or models.read_bytes() != model_raw or
                (stage / 'index.json').read_bytes() != index_raw or plan_path.read_bytes() != plan_raw or
                any(path.read_bytes() != raw for path, raw in manifests)):
            raise ValueError('candidate publication inputs changed')
        result = {'packages': len(records), 'shared_files': len(found), 'files_removed': 0}
        if not apply:
            return dict(result, state='dry_run_passed')
        directory = ensure_vault_directory(vault, ['metadata', 'unity-shared-overlays'])
        for record, (_, raw) in zip(records, manifests):
            path = directory / (record['sha256'] + '.json')
            if path.exists() and path.read_bytes() != raw:
                raise ValueError('conflicting immutable manifest')
            if not path.exists():
                with path.open('xb') as stream:
                    stream.write(raw)
                    stream.flush()
                    os.fsync(stream.fileno())
        updated = dict(current, overlays=current['overlays'] + records)
        new_raw = importer.canonical_json(updated)
        txid = uuid.uuid4().hex
        txdir = ensure_vault_directory(vault, ['metadata', 'unity-shared-overlay-transactions'])
        transaction = txdir / (txid + '.json')
        receipt = {'schema': 'openrpg.unity-shared-overlay-transaction.v1', 'state': 'prepared',
                   'transaction': txid, 'prior_registry': current, 'prior_sha256': storage.sha(old_registry),
                   'new_registry_sha256': storage.sha(new_raw), 'plan_sha256': storage.sha(plan_raw),
                   'packages': len(records), 'shared_files': len(found), 'files_removed': 0}
        importer.atomic_write(transaction, importer.canonical_json(receipt))
        if registry.read_bytes() != old_registry or source.read_bytes() != source_raw or models.read_bytes() != model_raw:
            raise ValueError('candidate registries changed before publication')
        importer.atomic_write(registry, new_raw)
        if registry.read_bytes() != new_raw:
            raise ValueError('candidate registry readback failed')
        for record in records:
            resolver = configured_resolver(vault, vault / record['source_root'])
            if resolver is None or storage.sha(resolver.manifest_raw) != record['sha256']:
                raise ValueError('candidate resolver registration failed')
        receipt['state'] = 'committed'
        importer.atomic_write(transaction, importer.canonical_json(receipt))
        return dict(result, state='published', transaction=txid, registry_sha256=storage.sha(new_raw))


if __name__ == '__main__':
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument('--vault', required=True, type=Path)
    p.add_argument('--stage', required=True, type=Path)
    p.add_argument('--plan', required=True, type=Path)
    p.add_argument('--importer-sha256', required=True)
    p.add_argument('--apply', action='store_true')
    args = p.parse_args()
    print(json.dumps(publish(args.vault, args.stage, args.plan, importer_module(args.importer_sha256), args.apply)))
