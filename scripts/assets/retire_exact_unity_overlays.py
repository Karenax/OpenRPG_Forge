"""Publish exact Unity source overlays and retire their duplicate physical files.

Each overlay is independently pinned. A durable recovery mapping is committed
before removal; incomplete removals remain readable through the overlay.
"""
import argparse
import contextlib
import hashlib
import json
import os
from pathlib import Path
import shutil
import uuid

from exclusive_source_delete import ExclusiveSource
from publish_source_support_relinks import importer_module
from publish_variant_transition import ensure_vault_directory, exclusive_storage_lock
from retire_relinked_source_copies import active_references
from unity_shared_overlay import UnitySourceResolver, changed_overlay_rows
import vault_storage as storage


def selected_rows(vault, document):
    if document.get('schema') != 'openrpg.unity-shared-overlay.v1':
        raise ValueError('invalid overlay schema')
    root = storage.confined(vault, vault / document['source_root'])
    rows = []
    for item in document['files']:
        old = storage.confined(vault, root / item['logical'])
        keep = storage.confined(vault, vault / item['vault_path'])
        if old == keep:
            continue
        if not old.is_relative_to(root):
            raise ValueError('invalid shared destination')
        if not storage.valid_hex(item['sha256'], 64) or not isinstance(item['bytes'], int):
            raise ValueError('invalid file pin')
        rows.append({'old': old.relative_to(vault).as_posix(),
                     'retained': keep.relative_to(vault).as_posix(),
                     'sha256': item['sha256'], 'bytes': item['bytes']})
    if len(rows) != document['shared_files'] or len({r['old'].casefold() for r in rows}) != len(rows):
        raise ValueError('overlay selection differs')
    return rows


def run(vault, manifests, importer, journal, apply, extend=False):
    vault = vault.resolve(strict=True)
    registry = storage.confined(vault, vault / 'metadata/unity-shared-overlay-registry.v1.json')
    source_registry = storage.confined(vault, vault / 'metadata/asset-source-support-registry.v1.json')
    model_registry = storage.confined(vault, vault / 'metadata/asset-import-registry.v1.json')
    with importer.shared_import_lock(vault), exclusive_storage_lock(vault):
        prior = registry.read_bytes()
        doc = json.loads(prior)
        if doc.get('schema') != 'openrpg.unity-shared-overlay-registry.v1':
            raise ValueError('invalid registry')
        registry_source = source_registry.read_bytes()
        candidates = []
        for path in manifests:
            raw = path.read_bytes()
            overlay = json.loads(raw)
            if not extend and overlay.get('source_registry_sha256') != storage.sha(registry_source):
                raise ValueError('source registry differs from overlay')
            root = overlay['source_root']
            matching = []
            for index, existing in enumerate(doc['overlays']):
                a, b = root.casefold().rstrip('/') + '/', existing['source_root'].casefold().rstrip('/') + '/'
                if a.startswith(b) or b.startswith(a):
                    if not extend or a != b:
                        raise ValueError('overlay roots overlap')
                    matching.append(index)
            if extend:
                if len(matching) != 1:
                    raise ValueError('existing overlay not unique')
                entry = doc['overlays'][matching[0]]
                previous_raw = storage.confined(vault, vault / entry['manifest']).read_bytes()
                if storage.sha(previous_raw) != entry['sha256']:
                    raise ValueError('existing overlay changed')
                previous = json.loads(previous_raw)
                if (overlay['directories'] != previous['directories'] or
                        overlay['logical_bytes'] != previous['logical_bytes'] or
                        overlay['source_registry_sha256'] != previous['source_registry_sha256'] or
                        overlay['comparison_sha256'] != previous['comparison_sha256']):
                    raise ValueError('overlay reconstruction contract changed')
                count = overlay['shared_files'] - previous['shared_files']
                if count <= 0:
                    raise ValueError('empty extension')
                selected = changed_overlay_rows(overlay, previous, count)
                rows = [{'old': (Path(root) / item['logical']).as_posix(),
                         'retained': item['vault_path'], 'sha256': item['sha256'],
                         'bytes': item['bytes']} for item in selected]
            else:
                rows = selected_rows(vault, overlay)
            if not rows:
                raise ValueError('empty overlay')
            candidates.append((path, raw, overlay, rows, matching[0] if extend else None))
        all_rows = [row for _, _, _, rows, _ in candidates for row in rows]
        if len({r['old'].casefold() for r in all_rows}) != len(all_rows):
            raise ValueError('duplicate original path')
        old_paths = {r['old'].casefold() for r in all_rows}
        if any(r['retained'].casefold() in old_paths for r in all_rows):
            raise ValueError('retained copy is also scheduled for deletion')
        replacing = {index for _, _, _, _, index in candidates if index is not None}
        for index, entry in enumerate(doc['overlays']):
            if index in replacing:
                continue
            raw_overlay = storage.confined(vault, vault / entry['manifest']).read_bytes()
            if storage.sha(raw_overlay) != entry['sha256']:
                raise ValueError('existing overlay changed')
            if any(item['vault_path'].casefold() in old_paths for item in json.loads(raw_overlay)['files']):
                raise ValueError('another overlay still uses a candidate')
        source_before, models_before = active_references(vault, {r['old'].casefold() for r in all_rows})
        if source_before != registry_source:
            raise ValueError('source registry changed')
        with contextlib.ExitStack() as stack:
            handles = []
            for row in all_rows:
                old = storage.confined(vault, vault / row['old'])
                keep = storage.confined(vault, vault / row['retained'])
                a = stack.enter_context(ExclusiveSource(old))
                b = stack.enter_context(storage.locked_source(keep, share_delete=False))
                if (old.stat().st_size != row['bytes'] or keep.stat().st_size != row['bytes'] or
                        a.sha256() != row['sha256'] or hashlib.file_digest(b, 'sha256').hexdigest() != row['sha256']):
                    raise ValueError('source copies differ: ' + row['old'])
                handles.append(a)
            if (registry.read_bytes() != prior or source_registry.read_bytes() != source_before or
                    model_registry.read_bytes() != models_before or
                    any(path.read_bytes() != raw for path, raw, _, _, _ in candidates)):
                raise ValueError('inputs changed')
            result = {'files': len(all_rows), 'logical_bytes': sum(r['bytes'] for r in all_rows)}
            if not apply:
                return dict(result, state='dry_run_passed')
            if journal.exists():
                raise ValueError('journal exists')
            outdir = ensure_vault_directory(vault, ['metadata', 'unity-shared-overlays'])
            records = []
            for _, raw, overlay, _, _ in candidates:
                pin = storage.sha(raw)
                target = outdir / (pin + '.json')
                if target.exists() and target.read_bytes() != raw:
                    raise ValueError('overlay collision')
                if not target.exists():
                    with target.open('xb') as stream:
                        stream.write(raw); stream.flush(); os.fsync(stream.fileno())
                if target.read_bytes() != raw:
                    raise ValueError('overlay readback failed')
                resolver = UnitySourceResolver(vault, target, pin)
                if resolver.source_root != vault / overlay['source_root']:
                    raise ValueError('overlay root differs')
                records.append({'source_root': overlay['source_root'],
                                'manifest': target.relative_to(vault).as_posix(), 'sha256': pin})
            updated = dict(doc, overlays=list(doc['overlays']))
            for record, (_, _, _, _, index) in zip(records, candidates):
                if index is None:
                    updated['overlays'].append(record)
                else:
                    updated['overlays'][index] = record
            txid = uuid.uuid4().hex
            txdir = ensure_vault_directory(vault, ['metadata', 'asset-source-support-transactions'])
            recovery = txdir / ('exact-unity-overlay-recovery-' + txid + '.json')
            importer.atomic_write(recovery, importer.canonical_json({'state': 'prepared', 'rows': all_rows,
                'prior_registry_sha256': storage.sha(prior), 'overlays': records}))
            if registry.read_bytes() != prior or source_registry.read_bytes() != source_before or model_registry.read_bytes() != models_before:
                raise ValueError('registry changed before publication')
            new = importer.canonical_json(updated)
            importer.atomic_write(registry, new)
            if registry.read_bytes() != new:
                raise ValueError('registry readback failed')
            before = shutil.disk_usage(vault).free
            with journal.open('x', encoding='utf8') as log:
                def record(entry):
                    log.write(json.dumps(entry, ensure_ascii=False) + '\n'); log.flush(); os.fsync(log.fileno())
                record({'state': 'prepared', 'recovery': str(recovery), 'free_before': before})
                for row, handle in zip(all_rows, handles):
                    record({'state': 'verified', 'old': row['old']})
                    handle.mark_delete()
                    record({'state': 'marked_delete', 'old': row['old']})
        if any((vault / r['old']).exists() for r in all_rows):
            raise ValueError('duplicate remains')
        if any(not (vault / r['retained']).is_file() for r in all_rows):
            raise ValueError('retained copy missing')
        return dict(result, state='removed_verified', recovery=str(recovery),
                    free_delta_observed=shutil.disk_usage(vault).free - before,
                    registry_sha256=storage.sha(new))


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--vault', type=Path, required=True)
    parser.add_argument('--manifest', type=Path, action='append', required=True)
    parser.add_argument('--journal', type=Path, required=True)
    parser.add_argument('--importer-sha256', required=True)
    parser.add_argument('--apply', action='store_true')
    parser.add_argument('--extend', action='store_true')
    args = parser.parse_args()
    print(json.dumps(run(args.vault, args.manifest, importer_module(args.importer_sha256),
                         args.journal, args.apply, args.extend), ensure_ascii=False))
