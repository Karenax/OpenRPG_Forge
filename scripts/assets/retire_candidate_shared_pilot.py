"""Retire the ten SHA-identical candidate mip copies after published sharing.

The retained bytes remain available through the candidate shared reader and
the explicit package materializer. Direct legacy filesystem paths no longer work.
"""
import argparse
import contextlib
import hashlib
import json
import os
from pathlib import Path
import shutil
import uuid

from candidate_shared_reader import CandidateSharedReader
from exclusive_source_delete import ExclusiveSource
from publish_source_support_relinks import importer_module
from publish_variant_transition import ensure_vault_directory, exclusive_storage_lock
from retire_relinked_source_copies import active_references
import vault_storage as storage

NAME = '019-abc00000000004821139089402820396.gpu-rgba8.mips'
PIN = '384507e016e8e52a859560dfbe7f6d9bcf5d59d56649b37ce4420f02a24b8411'
SIZE = 44739244


def selected_rows(data):
    if data.get('schema') != 'openrpg.candidate-dependency-sharing-plan.v1':
        raise ValueError('unexpected plan schema')
    rows = [r for r in data['rows'] if Path(r['old']).name == NAME]
    if len(rows) != 10 or len({r['old'].casefold() for r in rows}) != 10:
        raise ValueError('pilot must contain ten distinct copies')
    retained = set()
    for r in rows:
        old, keep = r['old'].split('/'), r['retained'].split('/')
        if (len(old) != 5 or len(keep) != 5 or old[:2] != ['candidates', 'hivemind'] or
                keep[:2] != old[:2] or old[3:] != ['dependencies', NAME] or
                keep[3:] != old[3:] or r['sha256'] != PIN or r['bytes'] != SIZE or
                any(part in ('', '.', '..') or '\\' in part for part in old + keep)):
            raise ValueError('unexpected pilot mapping')
        retained.add(r['retained'].casefold())
    if len(retained) != 1 or retained & {r['old'].casefold() for r in rows}:
        raise ValueError('unsafe retained path')
    return rows


def remaining_rows(data, start, count):
    """Select a bounded slice of the original plan after the retired pilot."""
    if data.get('schema') != 'openrpg.candidate-dependency-sharing-plan.v1':
        raise ValueError('unexpected plan schema')
    all_rows = data['rows']
    if len(all_rows) != 770 or not 0 <= start < 760 or not 1 <= count <= 50:
        raise ValueError('invalid plan size or batch bounds')
    selected_rows(data)  # Verify the original ten-row pilot remains identifiable.
    old_keys = {r['old'].casefold() for r in all_rows}
    if len(old_keys) != 770 or old_keys & {r['retained'].casefold() for r in all_rows}:
        raise ValueError('conflicting plan paths')
    remaining = [r for r in all_rows if Path(r['old']).name != NAME]
    rows = remaining[start:start+count]
    if not rows:
        raise ValueError('empty batch')
    for row in rows:
        old, keep = row['old'].split('/'), row['retained'].split('/')
        if (len(old) < 4 or len(keep) < 4 or old[:2] != ['candidates', 'hivemind'] or
                keep[:2] != old[:2] or old[-1] != keep[-1] or
                any(part in ('', '.', '..') or '\\' in part or ':' in part for part in old + keep) or
                not storage.valid_hex(row['sha256'], 64) or type(row['bytes']) is not int or row['bytes'] < 0):
            raise ValueError('unsafe candidate mapping')
    return rows


def run(vault, plan, importer, journal, apply, start=None, count=50):
    vault = vault.resolve(strict=True)
    if os.name != 'nt' or str(vault).casefold() != r'f:\openrpg_assetvault':
        raise ValueError('expected Windows F: Vault')
    long_vault = Path('\\\\?\\' + str(vault))
    raw = plan.read_bytes()
    data = json.loads(raw)
    rows = selected_rows(data) if start is None else remaining_rows(data, start, count)
    registry = vault / 'metadata/unity-shared-overlay-registry.v1.json'
    with importer.shared_import_lock(vault), exclusive_storage_lock(vault), contextlib.ExitStack() as stack:
        registry_raw = registry.read_bytes()
        source, models = active_references(vault, {r['old'].casefold() for r in rows})
        handles = []
        for row in rows:
            old = storage.confined(long_vault, long_vault / row['old'])
            keep = storage.confined(long_vault, long_vault / row['retained'])
            reader = CandidateSharedReader.from_package(vault, vault / '/'.join(row['old'].split('/')[:3]))
            relative = '/'.join(row['old'].split('/')[3:])
            entry = reader.reader.entries.get(relative.casefold())
            if entry != (Path(row['retained']), row['sha256'], row['bytes']):
                raise ValueError('published sharing differs from plan: ' + row['old'])
            original = stack.enter_context(ExclusiveSource(old))
            retained = stack.enter_context(storage.locked_source(keep, share_delete=False))
            if (old.stat().st_size != row['bytes'] or keep.stat().st_size != row['bytes'] or
                    original.sha256() != row['sha256'] or hashlib.file_digest(retained, 'sha256').hexdigest() != row['sha256']):
                raise ValueError('duplicate content changed: ' + row['old'])
            with reader.open(relative) as stream:
                if hashlib.file_digest(stream, 'sha256').hexdigest() != row['sha256']:
                    raise ValueError('published reader mismatch')
            handles.append(original)
        if (registry.read_bytes() != registry_raw or plan.read_bytes() != raw or
                (vault/'metadata/asset-source-support-registry.v1.json').read_bytes() != source or
                (vault/'metadata/asset-import-registry.v1.json').read_bytes() != models):
            raise ValueError('plan or registry changed during checks')
        result = {'files': len(rows), 'logical_bytes': sum(r['bytes'] for r in rows),
                  'registry_sha256': storage.sha(registry_raw)}
        if not apply:
            return dict(result, state='dry_run_passed')
        if journal.exists():
            raise ValueError('journal already exists')
        txdir = ensure_vault_directory(vault, ['metadata', 'asset-source-support-transactions'])
        recovery = txdir / ('candidate-shared-pilot-recovery-' + uuid.uuid4().hex + '.json')
        payload = {'state': 'prepared', 'rows': rows, 'plan_sha256': storage.sha(raw),
                   'overlay_registry_sha256': storage.sha(registry_raw),
                   'source_registry_sha256': storage.sha(source), 'model_registry_sha256': storage.sha(models)}
        importer.atomic_write(recovery, importer.canonical_json(payload))
        if json.loads(recovery.read_bytes()) != payload:
            raise ValueError('recovery mapping readback failed')
        before = shutil.disk_usage(vault).free
        with journal.open('x', encoding='utf8') as log:
            for row, handle in zip(rows, handles):
                log.write(json.dumps({'state': 'verified', 'old': row['old']}) + '\n')
                log.flush(); os.fsync(log.fileno())
                handle.mark_delete()
                log.write(json.dumps({'state': 'marked_delete', 'old': row['old']}) + '\n')
                log.flush(); os.fsync(log.fileno())
    for row in rows:
        if (long_vault / row['old']).exists():
            raise ValueError('duplicate remains after retirement')
        reader = CandidateSharedReader.from_package(vault, vault / '/'.join(row['old'].split('/')[:3]))
        with reader.open('/'.join(row['old'].split('/')[3:])) as stream:
            if hashlib.file_digest(stream, 'sha256').hexdigest() != row['sha256']:
                raise ValueError('shared read failed after retirement')
    return dict(result, state='removed_verified', recovery=str(recovery),
                free_delta_observed=shutil.disk_usage(vault).free-before)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--vault', type=Path, required=True)
    parser.add_argument('--plan', type=Path, required=True)
    parser.add_argument('--importer-sha256', required=True)
    parser.add_argument('--journal', type=Path, required=True)
    parser.add_argument('--apply', action='store_true')
    parser.add_argument('--remaining-start', type=int)
    parser.add_argument('--count', type=int, default=50)
    args = parser.parse_args()
    print(json.dumps(run(args.vault, args.plan, importer_module(args.importer_sha256),
                         args.journal, args.apply, args.remaining_start, args.count), ensure_ascii=False))
