"""Retire two exact archive duplicates after nested catalog migration."""
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
from retire_relinked_source_copies import active_references,strings


def run(vault,stage,native_proof,redirect_proof,binary,importer,journal,remove):
    vault=vault.resolve(strict=True);plan_raw=(stage/'plan.json').read_bytes();plan=json.loads(plan_raw)
    nr=native_proof.read_bytes();native=json.loads(nr);rr=redirect_proof.read_bytes();redirect=json.loads(rr)
    if (plan.get('schema')!='openrpg.nested-archive-relink-stage.v1' or len(plan['archive_rows'])!=2
        or native.get('state')!='passed' or redirect.get('state')!='passed'
        or native['plan_sha256']!=storage.sha(plan_raw) or redirect['plan_sha256']!=storage.sha(plan_raw)
        or native['cards']!=len(plan['changed_ids']) or redirect['catalogs']!=len(plan['catalogs'])
        or storage.sha(binary.read_bytes())!=native['native_test_sha256'] or journal.exists()):
        raise ValueError('invalid plan or reader proofs')
    targets=set();rows=[]
    for row in plan['archive_rows']:
        old,keep=Path(row['old']),Path(row['retained'])
        if (not old.is_absolute() or not keep.is_absolute() or not old.is_relative_to(vault/'originals')
            or not keep.is_relative_to(vault/'originals') or old==keep or old.suffix.lower()!='.unitypackage'
            or keep.suffix.lower()!='.unitypackage' or not storage.valid_hex(row['sha256'],64)):
            raise ValueError('archive outside scope')
        rel=old.relative_to(vault).as_posix().casefold()
        if rel in targets:raise ValueError('duplicate candidate')
        targets.add(rel);rows.append(row)
    with importer.shared_import_lock(vault),exclusive_storage_lock(vault),contextlib.ExitStack() as stack:
        source,models=active_references(vault,targets)
        if storage.sha(models)!=native['registry_sha256'] or storage.sha(models)!=redirect['registry_sha256']:
            raise ValueError('model registry differs from proofs')
        if storage.sha(source)!=plan['source_registry_sha256']:raise ValueError('source registry differs')
        if storage.sha(Path(r'E:\CodexData\skills\openrpg-import-assets\scripts\asset_import.py').read_bytes())!=redirect['importer_sha256']:
            raise ValueError('archive redirect importer changed')
        qualified=targets | {(vault.as_posix()+'/'+x).casefold() for x in targets}
        for row in rows:
            q=subprocess.run(['rg','-l','-i','-F',Path(row['old']).name,r'F:\OpenRPG_ImportWork\queue',
                              '-g','*.json','-g','*.py'],capture_output=True,text=True,encoding='utf8')
            if q.returncode not in (0,1):raise ValueError('queue scan failed')
            for path in q.stdout.splitlines():
                if Path(path).suffix.lower()!='.json':raise ValueError('queue script mentions archive basename')
                if qualified.intersection(strings(json.loads(Path(path).read_bytes()))):raise ValueError('queue references candidate')
        handles=[]
        for row in rows:
            old=storage.confined(vault,Path(row['old']));keep=storage.confined(vault,Path(row['retained']))
            if old.stat().st_size!=row['bytes'] or keep.stat().st_size!=row['bytes']:raise ValueError('archive size differs')
            a=stack.enter_context(ExclusiveSource(old));b=stack.enter_context(storage.locked_source(keep,share_delete=False))
            if a.sha256()!=row['sha256'] or hashlib.file_digest(b,'sha256').hexdigest()!=row['sha256']:
                raise ValueError('archive hash differs')
            handles.append(a)
        if ((vault/'metadata/asset-import-registry.v1.json').read_bytes()!=models
            or (vault/'metadata/asset-source-support-registry.v1.json').read_bytes()!=source
            or (stage/'plan.json').read_bytes()!=plan_raw or native_proof.read_bytes()!=nr or redirect_proof.read_bytes()!=rr):
            raise ValueError('inputs changed')
        if not remove:return {'state':'dry_run_passed','archives':len(rows),'logical_bytes':sum(x['bytes'] for x in rows)}
        recovery=vault/'metadata/asset-source-support-transactions'/('nested-archive-recovery-'+storage.sha(plan_raw)+'.json')
        if recovery.exists():raise ValueError('recovery already exists')
        with recovery.open('xb') as f:f.write(json.dumps({'schema':'openrpg.nested-archive-recovery.v1','rows':rows},indent=2).encode());f.flush();os.fsync(f.fileno())
        before=shutil.disk_usage(vault).free
        with journal.open('x',encoding='utf8') as f:
            def record(x):f.write(json.dumps(x)+'\n');f.flush();os.fsync(f.fileno())
            record({'state':'prepared','recovery':str(recovery),'free_before':before})
            for row,handle in zip(rows,handles):
                record({'state':'verified','old':row['old'],'retained':row['retained'],'sha256':row['sha256']})
                if remove is True:handle.mark_delete();record({'state':'marked_delete','old':row['old']})
                else:raise ValueError('explicit --remove required')
    if any(Path(x['old']).exists() or not Path(x['retained']).exists() for x in rows):raise ValueError('post-removal path check failed')
    return {'state':'removed_pending_rehash','archives':len(rows),'logical_bytes':sum(x['bytes'] for x in rows),
            'free_delta_observed':shutil.disk_usage(vault).free-before,'recovery':str(recovery),
            'limitations':'Active nested catalogs and queues checked; external consumers not checked.'}


if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__)
    for name in ('vault','stage','native-proof','redirect-proof','binary','journal','output'):p.add_argument('--'+name,type=Path,required=True)
    p.add_argument('--importer-sha256',required=True);p.add_argument('--remove',action='store_true');a=p.parse_args()
    if a.output.exists():p.error('output exists')
    result=run(a.vault,a.stage,a.native_proof,a.redirect_proof,a.binary,importer_module(a.importer_sha256),a.journal,a.remove)
    with a.output.open('x',encoding='utf8') as f:json.dump(result,f,indent=2);f.write('\n')
    print(json.dumps(result))
