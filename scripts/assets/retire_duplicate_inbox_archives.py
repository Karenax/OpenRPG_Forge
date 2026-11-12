"""Retire byte-identical inbox archives after live Vault and queue checks."""
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


def run(vault, inventory, importer, journal, remove):
    vault=vault.resolve(strict=True);raw=inventory.read_bytes();doc=json.loads(raw);rows=[];targets=set()
    for group in doc['groups']:
        if not group['same_sha'] or len(group['files'])!=2:raise ValueError('invalid comparison group')
        choices=group['files'];old=[x for x in choices if Path(x['path']).parent==vault/'inbox/manual']
        keep=[x for x in choices if Path(x['path']).is_relative_to(vault/'originals')]
        if len(old)!=1 or len(keep)!=1 or old[0]['sha256']!=keep[0]['sha256']:raise ValueError('invalid archive pair')
        for item in (old[0],keep[0]):
            if not storage.valid_hex(item['sha256'],64):raise ValueError('invalid archive hash')
        rel=Path(old[0]['path']).relative_to(vault).as_posix().casefold()
        if rel in targets:raise ValueError('duplicate candidate')
        targets.add(rel);rows.append({'old':old[0]['path'],'retained':keep[0]['path'],
                                     'sha256':old[0]['sha256'],'bytes':old[0]['bytes']})
    if not rows or journal.exists():raise ValueError('empty plan or existing journal')
    with importer.shared_import_lock(vault),exclusive_storage_lock(vault),contextlib.ExitStack() as stack:
        source,models=active_references(vault,targets)
        q=subprocess.run(['rg','-l','-i','-e',r'inbox.{1,4}manual',r'F:\OpenRPG_ImportWork\queue',
                          '-g','*.json','-g','*.py'],capture_output=True,text=True,encoding='utf8')
        if q.returncode not in (0,1) or q.stdout:raise ValueError('queue may reference inbox archives')
        locked=[]
        for row in rows:
            old=storage.confined(vault,Path(row['old']));keep=storage.confined(vault,Path(row['retained']))
            if old.stat().st_size!=row['bytes'] or keep.stat().st_size!=row['bytes']:raise ValueError('archive size changed')
            a=stack.enter_context(ExclusiveSource(old));b=stack.enter_context(storage.locked_source(keep,share_delete=False))
            if a.sha256()!=row['sha256'] or hashlib.file_digest(b,'sha256').hexdigest()!=row['sha256']:
                raise ValueError('archive SHA changed')
            locked.append(a)
        if (vault/'metadata/asset-source-support-registry.v1.json').read_bytes()!=source or (vault/'metadata/asset-import-registry.v1.json').read_bytes()!=models or inventory.read_bytes()!=raw:
            raise ValueError('inputs changed')
        if not remove:return {'state':'dry_run_passed','candidates':len(rows),'logical_bytes':sum(x['bytes'] for x in rows)}
        recovery=vault/'metadata/asset-source-support-transactions'/('inbox-archive-recovery-'+storage.sha(raw)+'.json')
        if recovery.exists():raise ValueError('recovery record already exists')
        with recovery.open('xb') as f:
            f.write((json.dumps({'schema':'openrpg.inbox-archive-recovery.v1','rows':rows},indent=2)+'\n').encode());f.flush();os.fsync(f.fileno())
        before=shutil.disk_usage(vault).free
        with journal.open('x',encoding='utf8') as log:
            def record(value):
                log.write(json.dumps(value)+'\n');log.flush();os.fsync(log.fileno())
            record({'state':'prepared','recovery':str(recovery),'free_before':before})
            for row,handle in zip(rows,locked):
                record({'state':'verified',**row})
                if remove is True:
                    handle.mark_delete()
                    record({'state':'marked_delete','old':row['old']})
                else:
                    raise ValueError('explicit --remove required')
    if any(Path(r['old']).exists() or not Path(r['retained']).exists() for r in rows):raise ValueError('post-retirement path check failed')
    return {'state':'removed_pending_rehash','files':len(rows),'logical_bytes':sum(x['bytes'] for x in rows),
            'free_delta_observed':shutil.disk_usage(vault).free-before,'recovery':str(recovery),
            'limitations':'Active catalogs and JSON/Python queues checked; external consumers not checked.'}


if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__)
    for n in ('vault','inventory','journal','output'):p.add_argument('--'+n,type=Path,required=True)
    p.add_argument('--importer-sha256',required=True);p.add_argument('--remove',action='store_true');a=p.parse_args()
    if a.output.exists():p.error('output exists')
    result=run(a.vault,a.inventory,importer_module(a.importer_sha256),a.journal,a.remove)
    with a.output.open('x',encoding='utf8') as f:json.dump(result,f,indent=2);f.write('\n')
    print(json.dumps(result))
