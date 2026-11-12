"""Retire complete byte-matched copies from interrupted source extractions."""
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
from unity_shared_overlay import inventory_tree
import vault_storage as storage

PREFIX = 'originals/unity-purchased/hivemind/source-support-v1/'
PIRATE = PREFIX + 'pirate-island-hdrp-urp-source-support218-v1/previews/'


def run(vault, inventory, importer, journal, apply, rejected=False, pirate_previews=False, unreferenced_previews=False,
        normalized_partial=False, normalized_evidence=False, normalized_replaced=False, hospital_extension=False):
    vault = vault.resolve(strict=True)
    if os.name != 'nt' or not str(vault).startswith('F:\\'):
        raise ValueError('expected Windows F: vault')
    long_vault = Path('\\\\?\\' + str(vault))
    raw = inventory.read_bytes()
    data = json.loads(raw)
    marker = '.rejected-stream-without-extended-path' if rejected else '.failed-partial-'
    selected = [d for d in data['directories'] if (True if unreferenced_previews or normalized_partial or normalized_evidence or normalized_replaced or hospital_extension else
                d['directory'] == PIRATE + 'variant' if pirate_previews else marker in d['directory'].casefold())]
    if not selected:
        raise ValueError('no interrupted extractions')
    rows = []
    for item in selected:
        rel = item['directory']
        allowed = 'normalized/hivemind/' if normalized_partial or normalized_evidence or normalized_replaced or hospital_extension else PREFIX
        if (not rel.startswith(allowed) or (normalized_partial and '.partial' not in rel.split('/')[2].casefold()) or
                (normalized_evidence and (not rel.split('/')[2].casefold().endswith('-v1') or '/evidence/' not in (rel + '/').casefold())) or
                (normalized_replaced and (not rel.split('/')[2].casefold().endswith('-v2') or '/models/' not in (rel + '/').casefold())) or
                (hospital_extension and rel.casefold() != 'normalized/hivemind/modular-abandoned-hospital-v1/hdrp-extension-library-v1/models') or
                (unreferenced_previews and '/previews/' not in (rel + '/').casefold()) or
                (not pirate_previews and not unreferenced_previews and not normalized_partial and not normalized_evidence and not normalized_replaced and not hospital_extension and '/' in rel[len(PREFIX):]) or item['unmatched'] or
                item['counts'].get('other', 0) or item['counts'].get('reparse', 0) or
                item['counts']['files'] != len(item['matches'])):
            raise ValueError('extraction is not fully matched')
        root = storage.confined(long_vault, long_vault / rel)
        actual, _ = inventory_tree(root)
        expected = {}
        for row in item['matches']:
            old, keep = row['old'], row['retained']
            if (not old.casefold().startswith(rel.casefold() + '/') or
                    not keep.casefold().startswith(allowed.casefold()) or
                    (normalized_partial and ('.partial' in keep.split('/')[2].casefold() or
                     not old.casefold().endswith('.glb') or not keep.casefold().endswith('.glb'))) or
                    (normalized_evidence and (not keep.split('/')[2].casefold().endswith('-v2') or
                     '/evidence/' not in keep.casefold() or not old.casefold().endswith('.glb') or
                     not keep.casefold().endswith('.glb'))) or
                    (normalized_replaced and (not keep.split('/')[2].casefold().endswith('-v3') or
                     old.split('/')[2][:-3].casefold() != keep.split('/')[2][:-3].casefold() or
                     [p.casefold() for p in old.split('/')[3:]] != [p.casefold() for p in keep.split('/')[3:]] or
                     not old.casefold().endswith('.glb'))) or
                    (hospital_extension and (not keep.casefold().startswith('normalized/hivemind/modular-abandoned-hospital-v1/models/') or
                     Path(old).name.casefold() != Path(keep).name.casefold() or not old.casefold().endswith('.glb'))) or
                    (pirate_previews and (not keep.casefold().startswith((PIRATE + 'base/').casefold()) or
                     Path(old).name.casefold() != Path(keep).name.casefold() or not old.casefold().endswith('.png'))) or
                    (unreferenced_previews and ('/previews/' not in keep.casefold() or
                     not old.casefold().endswith('.png') or not keep.casefold().endswith('.png'))) or
                    (not rejected and '.failed-partial-' in keep.casefold()) or
                    keep.casefold().startswith(rel.casefold() + '/') or
                    not storage.valid_hex(row['sha256'], 64)):
                raise ValueError('invalid file mapping')
            logical = old[len(rel) + 1:].casefold()
            if logical in expected:
                raise ValueError('repeated original')
            expected[logical] = row
        if (not set(expected).issubset(actual) if pirate_previews or unreferenced_previews or normalized_partial or normalized_evidence or normalized_replaced or hospital_extension else set(actual) != set(expected)):
            raise ValueError('interrupted extraction contents changed')
        rows.extend(item['matches'])
    if len({r['old'].casefold() for r in rows}) != len(rows):
        raise ValueError('duplicate input paths')
    if any(r['retained'].casefold() in {x['old'].casefold() for x in rows} for r in rows):
        raise ValueError('retained copy is also scheduled for deletion')
    with importer.shared_import_lock(vault), exclusive_storage_lock(vault), contextlib.ExitStack() as stack:
        source, models = active_references(vault, {r['old'].casefold() for r in rows})
        handles = []
        for row in rows:
            old = storage.confined(long_vault, long_vault / row['old'])
            keep = storage.confined(long_vault, long_vault / row['retained'])
            a = stack.enter_context(ExclusiveSource(old))
            b = stack.enter_context(storage.locked_source(keep, share_delete=False))
            if (old.stat().st_size != row['bytes'] or keep.stat().st_size != row['bytes'] or
                    a.sha256() != row['sha256'] or hashlib.file_digest(b, 'sha256').hexdigest() != row['sha256']):
                raise ValueError('duplicate content changed: ' + row['old'])
            handles.append(a)
        if ((vault/'metadata/asset-source-support-registry.v1.json').read_bytes() != source or
                (vault/'metadata/asset-import-registry.v1.json').read_bytes() != models or
                inventory.read_bytes() != raw):
            raise ValueError('inventory or registries changed')
        result = {'files': len(rows), 'logical_bytes': sum(r['bytes'] for r in rows)}
        if not apply:
            return dict(result, state='dry_run_passed')
        if journal.exists():
            raise ValueError('journal exists')
        txdir = ensure_vault_directory(vault, ['metadata', 'asset-source-support-transactions'])
        recovery = txdir / ('interrupted-copy-recovery-' + uuid.uuid4().hex + '.json')
        importer.atomic_write(recovery, importer.canonical_json({'state': 'prepared', 'rows': rows,
            'source_registry_sha256': storage.sha(source), 'model_registry_sha256': storage.sha(models)}))
        before = shutil.disk_usage(vault).free
        with journal.open('x', encoding='utf8') as log:
            for row, handle in zip(rows, handles):
                log.write(json.dumps({'state':'verified','old':row['old']}, ensure_ascii=False) + '\n')
                log.flush(); os.fsync(log.fileno())
                handle.mark_delete()
                log.write(json.dumps({'state':'marked_delete','old':row['old']}, ensure_ascii=False) + '\n')
                log.flush(); os.fsync(log.fileno())
    if any((long_vault / row['old']).exists() for row in rows):
        raise ValueError('some duplicates remain')
    if any(not (long_vault / row['retained']).is_file() for row in rows):
        raise ValueError('retained copy missing')
    return dict(result, state='removed_verified', recovery=str(recovery),
                free_delta_observed=shutil.disk_usage(vault).free - before)


if __name__ == '__main__':
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument('--vault', type=Path, required=True)
    p.add_argument('--inventory', type=Path, required=True)
    p.add_argument('--journal', type=Path, required=True)
    p.add_argument('--importer-sha256', required=True)
    p.add_argument('--apply', action='store_true')
    p.add_argument('--rejected', action='store_true')
    p.add_argument('--pirate-previews', action='store_true')
    p.add_argument('--unreferenced-previews', action='store_true')
    p.add_argument('--normalized-partial', action='store_true')
    p.add_argument('--normalized-evidence', action='store_true')
    p.add_argument('--normalized-replaced', action='store_true')
    p.add_argument('--hospital-extension', action='store_true')
    a = p.parse_args()
    print(json.dumps(run(a.vault, a.inventory, importer_module(a.importer_sha256),
                         a.journal, a.apply, a.rejected, a.pirate_previews, a.unreferenced_previews,
                         a.normalized_partial, a.normalized_evidence, a.normalized_replaced,
                         a.hospital_extension), ensure_ascii=False))
