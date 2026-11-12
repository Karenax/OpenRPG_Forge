"""Retire the verified duplicate Witch House archive after current reference checks."""
import argparse
import hashlib
import json
from pathlib import Path
import shutil
import subprocess
import sys
import vault_storage as storage
from exclusive_source_delete import ExclusiveSource
from publish_model_preview_relinks import IMPORTER, load_importer
from publish_variant_transition import exclusive_storage_lock
from audit_active_archive_references import audit as model_audit

VAULT=Path('F:/OpenRPG_AssetVault')
OLD=VAULT/'originals/unity-purchased/hivemind/witch-house/variants/7a87221d80c1a122/WitchHouseURP.unitypackage'
KEEP=VAULT/'originals/unity-purchased/hivemind/source-support-remaining-v1/archives/7a/7a87221d80c1a122fc884a650b552f1ecd83a394e669f9c81835e94003aafdfd.unitypackage'
SHA='7a87221d80c1a122fc884a650b552f1ecd83a394e669f9c81835e94003aafdfd'


def strings(value):
    stack=[value]
    while stack:
        x=stack.pop()
        if isinstance(x,str):yield x.replace('\\','/').casefold()
        elif isinstance(x,dict):stack.extend(x.values())
        elif isinstance(x,list):stack.extend(x)


def run(work,remove):
    vault=VAULT.resolve(strict=True);work=work.resolve(strict=True)
    old=storage.confined(vault,OLD);keep=storage.confined(vault,KEEP)
    inventory=work/'hivemind-archive-hash-inventory-20261112.json'
    importer=load_importer(storage.sha(IMPORTER.read_bytes()))
    queue=Path('F:/OpenRPG_ImportWork/queue')
    target={str(old).replace('\\','/').casefold(),old.relative_to(vault).as_posix().casefold()}
    receipt=work/('witch-archive-retirement-20261112.json' if remove else 'witch-archive-dryrun-20261112.json')
    if receipt.exists():raise ValueError('receipt exists')
    with importer.locked(vault),exclusive_storage_lock(vault),ExclusiveSource(old) as old_handle,ExclusiveSource(keep) as keep_handle:
        before=shutil.disk_usage(vault).free
        if old.stat().st_size!=1235559289 or keep.stat().st_size!=1235559289:raise ValueError('archive size differs')
        if old_handle.sha256()!=SHA or keep_handle.sha256()!=SHA:raise ValueError('archive hash differs')
        support=storage.confined(vault,vault/'metadata/asset-source-support-registry.v1.json');sr=support.read_bytes();sp=json.loads(sr)
        raw=storage.confined(vault,vault/sp['path']).read_bytes()
        if storage.sha(raw)!=sp['sha256'] or target.intersection(strings(json.loads(raw))):raise ValueError('source registry references old archive')
        models=model_audit(vault,inventory)
        if next(x for x in models['archives'] if Path(x['path'])==old)['references']!=0:raise ValueError('model catalog references old archive')
        q=subprocess.run(['rg','-l','-i','-F',old.name,str(queue),'-g','*.json'],capture_output=True,text=True,encoding='utf8')
        if q.returncode not in (0,1):raise ValueError('queue scan failed: '+q.stderr)
        queues=[]
        for name in q.stdout.splitlines():
            p=Path(name);content=p.read_bytes()
            if target.intersection(strings(json.loads(content))):raise ValueError('queue references old archive')
            queues.append({'path':str(p),'sha256':storage.sha(content)})
        if support.read_bytes()!=sr:raise ValueError('support registry changed')
        if storage.sha((vault/'metadata/asset-import-registry.v1.json').read_bytes())!=models['registry_sha256']:raise ValueError('model registry changed')
        result={'state':'ready_for_removal' if remove else 'dryrun_passed','removed':str(old),'retained':str(keep),
                'sha256':SHA,'bytes':1235559289,'source_registry_sha256':sp['sha256'],
                'model_registry_sha256':models['registry_sha256'],'model_documents_checked':models['checked_documents'],
                'queue_documents':queues,'free_before':before,
                'limitations':'Active source/model catalogs and JSON queues checked; external consumers and historical records are not rewritten.'}
        with receipt.open('x',encoding='utf8') as f:json.dump(result,f,indent=2);f.write('\n');f.flush()
        if remove:old_handle.mark_delete()
    if remove:
        if old.exists() or not keep.exists():raise ValueError('post-removal check failed')
        result['state']='removed';result['free_delta_observed']=shutil.disk_usage(vault).free-before
        post=work/'witch-archive-retirement-postcheck-20261112.json'
        with post.open('x',encoding='utf8') as f:json.dump(result,f,indent=2);f.write('\n')
    return result


if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--remove',action='store_true');a=p.parse_args()
    print(json.dumps(run(Path(r'E:\OpenRPG_Forge\.build\asset-conversion'),a.remove)))
