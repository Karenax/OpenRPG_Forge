"""Retire obsolete Sewers v1 geometry diagnostics duplicated by v2."""
import argparse
import contextlib
import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess

import vault_storage as storage
from exclusive_source_delete import ExclusiveSource
from publish_source_support_relinks import importer_module
from publish_variant_transition import exclusive_storage_lock
from retire_relinked_source_copies import active_references

OLD = Path('normalized/hivemind/modular-sewers-urp-source-face-repair-v1')
KEEP = Path('normalized/hivemind/modular-sewers-urp-source-face-repair-v2')
PART = Path('evidence/source-geometry/models')


def run(vault, inventory, importer, journal, remove):
    vault = vault.resolve(strict=True)
    raw = inventory.read_bytes()
    rows = []
    for group in json.loads(raw)['groups']:
        paths = {Path(x['path']): x for x in group['files']}
        for path, row in paths.items():
            if not path.is_relative_to(vault / OLD / PART):
                continue
            relative = path.relative_to(vault / OLD)
            keep = vault / KEEP / relative
            if keep not in paths or path.suffix.lower() != '.glb' or not storage.valid_hex(group['sha256'], 64):
                raise ValueError('missing exact same-path counterpart')
            if paths[keep]['bytes'] != row['bytes']:
                raise ValueError('candidate size differs')
            rows.append({'old': path.relative_to(vault).as_posix(), 'retained': keep.relative_to(vault).as_posix(),
                         'sha256': group['sha256'], 'bytes': row['bytes']})
    if len(rows) != 7 or len({r['old'].casefold() for r in rows}) != len(rows):
        raise ValueError('unexpected obsolete diagnostic set')
    if journal.exists(): raise ValueError('journal already exists')
    with importer.shared_import_lock(vault), exclusive_storage_lock(vault), contextlib.ExitStack() as stack:
        source_pin, model_pin = active_references(vault, {r['old'].casefold() for r in rows})
        queue = subprocess.run(['rg','-l','-i','-F',OLD.name,r'F:\OpenRPG_ImportWork\queue',
                                '-g','*.json','-g','*.py'], capture_output=True,text=True,encoding='utf8')
        if queue.returncode not in (0,1) or queue.stdout:
            raise ValueError('queue may still use old diagnostic package')
        manifests = []
        for root in (OLD, KEEP):
            data = storage.confined(vault, vault/root/'delivery-manifest.v1.json').read_bytes()
            manifests.append({x['path'].casefold():x for x in json.loads(data)['files']})
        handles = []
        for row in rows:
            old = storage.confined(vault, vault/row['old'])
            keep = storage.confined(vault, vault/row['retained'])
            relative = old.relative_to(vault/OLD).as_posix().casefold()
            for manifest in manifests:
                entry = manifest.get(relative)
                if not entry or entry.get('sha256') != row['sha256'] or entry.get('bytes') != row['bytes']:
                    raise ValueError('delivery manifest differs')
            if old.stat().st_size != row['bytes'] or keep.stat().st_size != row['bytes']:
                raise ValueError('payload size changed')
            a = stack.enter_context(ExclusiveSource(old))
            b = stack.enter_context(storage.locked_source(keep,share_delete=False))
            if a.sha256() != row['sha256'] or hashlib.file_digest(b,'sha256').hexdigest() != row['sha256']:
                raise ValueError('payload SHA changed')
            handles.append(a)
        if (inventory.read_bytes()!=raw or (vault/'metadata/asset-source-support-registry.v1.json').read_bytes()!=source_pin
                or (vault/'metadata/asset-import-registry.v1.json').read_bytes()!=model_pin):
            raise ValueError('inputs changed')
        result = {'files':len(rows),'logical_bytes':sum(r['bytes'] for r in rows),'rows':rows}
        if not remove: return dict(result,state='dry_run_passed')
        recovery = vault/'metadata/asset-source-support-transactions'/('sewers-diagnostic-recovery-'+storage.sha(raw)+'.json')
        if recovery.exists(): raise ValueError('recovery record already exists')
        with recovery.open('xb') as f:
            f.write((json.dumps(result,indent=2)+'\n').encode());f.flush();os.fsync(f.fileno())
        before = shutil.disk_usage(vault).free
        with journal.open('x',encoding='utf8') as log:
            def record(item):
                log.write(json.dumps(item)+'\n');log.flush();os.fsync(log.fileno())
            record({'state':'prepared','recovery':str(recovery),'free_before':before})
            for row,handle in zip(rows,handles):
                record({'state':'verified',**row});handle.mark_delete();record({'state':'marked_delete','old':row['old']})
    if any((vault/r['old']).exists() for r in rows):raise ValueError('old path remains')
    for row in rows:
        with storage.confined(vault,vault/row['retained']).open('rb') as f:
            if hashlib.file_digest(f,'sha256').hexdigest()!=row['sha256']:
                raise ValueError('retained file post-check failed')
    return dict(result,state='removed_verified',free_delta_observed=shutil.disk_usage(vault).free-before,recovery=str(recovery))


if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__)
    for name in ('vault','inventory','journal','output'):p.add_argument('--'+name,type=Path,required=True)
    p.add_argument('--importer-sha256',required=True);p.add_argument('--remove',action='store_true');a=p.parse_args()
    if a.output.exists():p.error('output exists')
    result=run(a.vault,a.inventory,importer_module(a.importer_sha256),a.journal,a.remove)
    with a.output.open('x',encoding='utf8') as f:json.dump(result,f,indent=2);f.write('\n')
    print(json.dumps({k:v for k,v in result.items() if k!='rows'}))
