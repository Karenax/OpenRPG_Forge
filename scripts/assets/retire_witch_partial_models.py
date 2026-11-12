"""Retire only verified Witch House partial models duplicated by package v2."""
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

OLD_ROOT='normalized/hivemind/witch-house-variant-7a87221d80c1a122-source-authored-frames-batch56-v1.partial'
KEEP_ROOT='normalized/hivemind/witch-house-variant-7a87221d80c1a122-source-authored-frames-batch56-v2'


def run(vault,plan_path,importer,journal,remove):
    vault=vault.resolve(strict=True);raw=plan_path.read_bytes();plan=json.loads(raw);rows=plan['rows'];targets=set()
    if plan['state']!='candidate_only' or len(rows) not in (5,32) or journal.exists():raise ValueError('invalid plan')
    for row in rows:
        a,b=Path(row['old']),Path(row['retained'])
        old_root,keep_root=Path(OLD_ROOT),Path(KEEP_ROOT)
        if not a.is_relative_to(old_root) or not b.is_relative_to(keep_root):raise ValueError('candidate outside exact scope')
        rel=a.relative_to(old_root)
        permitted={'models'} if len(rows)==5 else {'collisions','evidence','previews','review'}
        if (not rel.parts or '..' in rel.parts or rel.parts[0] not in permitted or rel!=b.relative_to(keep_root)
            or a.suffix.lower() not in {'.glb','.json','.png'} or not storage.valid_hex(row['sha256'],64)):
            raise ValueError('candidate outside exact scope')
        if row['old'].casefold() in targets:raise ValueError('duplicate candidate')
        targets.add(row['old'].casefold())
    with importer.shared_import_lock(vault),exclusive_storage_lock(vault),contextlib.ExitStack() as stack:
        source,models=active_references(vault,targets)
        base=storage.confined(vault,vault/KEEP_ROOT);m=(base/'copy-manifest.v1.json').read_bytes()
        receipt=json.loads((base/'package-readback.v1.json').read_bytes())
        if receipt['status']!='passed' or receipt['manifest_sha256']!=storage.sha(m) or receipt['expected_file_hashes_match'] is not True:
            raise ValueError('published package receipt invalid')
        entries={x['path'].casefold():x for x in json.loads(m)['files']}
        search=subprocess.run(['rg','-l','-i','-F',Path(OLD_ROOT).name,r'F:\OpenRPG_ImportWork\queue',
                               '-g','*.json','-g','*.py'],capture_output=True,text=True,encoding='utf8')
        if search.returncode not in (0,1) or search.stdout:raise ValueError('queue may reference partial package')
        handles=[]
        for row in rows:
            old=storage.confined(vault,vault/row['old']);keep=storage.confined(vault,vault/row['retained'])
            entry=entries.get(keep.relative_to(base).as_posix().casefold())
            if not entry or entry['sha256']!=row['sha256'] or entry['bytes']!=row['bytes']:raise ValueError('package manifest differs')
            if old.stat().st_size!=row['bytes'] or keep.stat().st_size!=row['bytes']:raise ValueError('model size differs')
            a=stack.enter_context(ExclusiveSource(old));b=stack.enter_context(storage.locked_source(keep,share_delete=False))
            if a.sha256()!=row['sha256'] or hashlib.file_digest(b,'sha256').hexdigest()!=row['sha256']:
                raise ValueError('model SHA differs')
            handles.append(a)
        if plan_path.read_bytes()!=raw or (vault/'metadata/asset-source-support-registry.v1.json').read_bytes()!=source or (vault/'metadata/asset-import-registry.v1.json').read_bytes()!=models:
            raise ValueError('inputs changed')
        if not remove:return {'state':'dry_run_passed','files':len(rows),'logical_bytes':sum(x['bytes'] for x in rows)}
        recovery=vault/'metadata/asset-source-support-transactions'/('witch-partial-model-recovery-'+storage.sha(raw)+'.json')
        if recovery.exists():raise ValueError('recovery record already exists')
        with recovery.open('xb') as f:f.write(raw);f.flush();os.fsync(f.fileno())
        before=shutil.disk_usage(vault).free
        with journal.open('x',encoding='utf8') as log:
            def record(obj):log.write(json.dumps(obj)+'\n');log.flush();os.fsync(log.fileno())
            record({'state':'prepared','recovery':str(recovery),'free_before':before})
            for row,handle in zip(rows,handles):
                record({'state':'verified',**row})
                if remove is True:handle.mark_delete();record({'state':'marked_delete','old':row['old']})
                else:raise ValueError('explicit --remove required')
    if any((vault/r['old']).exists() or not (vault/r['retained']).exists() for r in rows):raise ValueError('post-retirement path check failed')
    return {'state':'removed_pending_rehash','files':len(rows),'logical_bytes':sum(x['bytes'] for x in rows),
            'free_delta_observed':shutil.disk_usage(vault).free-before,'recovery':str(recovery),
            'limitations':'Active catalogs and queues checked; external consumers and runtime not qualified.'}


if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__)
    for n in ('vault','plan','journal','output'):p.add_argument('--'+n,type=Path,required=True)
    p.add_argument('--importer-sha256',required=True);p.add_argument('--remove',action='store_true');a=p.parse_args()
    if a.output.exists():p.error('output exists')
    result=run(a.vault,a.plan,importer_module(a.importer_sha256),a.journal,a.remove)
    with a.output.open('x',encoding='utf8') as f:json.dump(result,f,indent=2);f.write('\n')
    print(json.dumps(result))
