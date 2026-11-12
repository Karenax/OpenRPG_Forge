"""Retire Unity source duplicates only after verified shared-overlay deployment."""
import argparse
import contextlib
import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess

from exclusive_source_delete import ExclusiveSource
from publish_source_support_relinks import importer_module
from publish_variant_transition import exclusive_storage_lock
from retire_relinked_source_copies import active_references
from unity_shared_overlay import configured_resolver
import vault_storage as storage

WORK=Path('E:/OpenRPG_Forge/.build/asset-conversion')
REGISTRY_SHA='da3c7250fa2ebfa3aa5ed06ef27ffee12706283dec9e13fdb1ded38921f153ca'
PACKS=(
 ('gladiator','originals/unity-purchased/hivemind/source-support-v1/modular-gladiator-arena-base170-v1',60,
  'abbb92a765d545fe8d5bb65c995b95a62fb935e39b9dfbabec7ccc985facdd50',30,45),
 ('gothic','originals/unity-purchased/hivemind/source-support-v1/modular-dungeon-gothic-base352-v1',82,
  '6f80e2d992e7c87fd635e26fb1792d9e09210ce86fb105b88d9972c7d05f4eb8',41,133),
)


def run(vault,importer,journal,remove):
    vault=vault.resolve(strict=True);work=WORK.resolve(strict=True)
    if journal.exists():raise ValueError('journal exists')
    registry=storage.confined(vault,vault/'metadata/unity-shared-overlay-registry.v1.json');registry_raw=registry.read_bytes()
    if storage.sha(registry_raw)!=REGISTRY_SHA:raise ValueError('overlay registry changed')
    rows=[];resolvers=[];archives=[]
    for name,package,count,pin,guids,refs in PACKS:
        base=storage.confined(vault,vault/package);resolver=configured_resolver(vault,base/'source')
        if resolver is None or storage.sha(resolver.manifest_raw)!=pin:raise ValueError('published overlay changed')
        proof_path=work/(name+'-export-locked-proof-20261112.json');proof=json.loads(proof_path.read_bytes())
        if (proof.get('state')!='passed' or proof.get('manifest_sha256')!=pin or
                proof.get('old_paths_locked')!=count or proof.get('guids')!=guids or proof.get('guid_file_references')!=refs):
            raise ValueError('exclusive reconstruction proof missing')
        manifest=json.loads(resolver.manifest_raw);root=manifest['source_root']+'/';selected=[]
        for entry in manifest['files']:
            if entry['vault_path'].casefold().startswith(root.casefold()):continue
            old=storage.confined(vault,resolver.source_root/entry['logical']);retained=storage.confined(vault,vault/entry['vault_path'])
            if old==retained or old.suffix.lower() not in {'.png','.tga','.meta'} or not storage.valid_hex(entry['sha256'],64):
                raise ValueError('unexpected duplicate payload')
            selected.append({'old':old.relative_to(vault).as_posix(),'retained':retained.relative_to(vault).as_posix(),
                             'sha256':entry['sha256'],'bytes':entry['bytes'],'pack':name})
        if len(selected)!=count:raise ValueError('unexpected duplicate count')
        rows.extend(selected);resolvers.append(resolver)
        source_manifest=json.loads(storage.confined(vault,base/'manifest.v1.json').read_bytes())
        archive=storage.confined(vault,Path(source_manifest['source_archive']))
        archives.append((archive,source_manifest['source_archive_sha256'],source_manifest['source_archive_bytes']))
    if len({x['old'].casefold() for x in rows})!=142:raise ValueError('duplicate old paths')
    unity=json.loads((work/'unity6-subgraphs-success-receipt-20261112.json').read_bytes())
    if (unity.get('state')!='passed' or unity.get('unity_exit_code')!=0 or unity['result'].get('checked_count')!=71
        or unity['result'].get('dependency_count')!=178 or unity['result'].get('failures')!=0):
        raise ValueError('Unity isolated dependency proof missing')
    for name,count in [('gladiator-overlay-exclusive-20261112.json',60),('gladiator-copy-exclusive-20261112.json',60),
                       ('gothic-overlay-exclusive-20261112.json',82)]:
        proof=json.loads((work/name).read_bytes())
        if proof.get('state')!='passed' or proof.get('old_paths_locked',proof.get('locked_original_files'))!=count:
            raise ValueError('exclusive reader proof missing: '+name)
    with importer.shared_import_lock(vault),exclusive_storage_lock(vault),contextlib.ExitStack() as stack:
        source_before,models_before=active_references(vault,{r['old'].casefold() for r in rows})
        queue=subprocess.run(['rg','-l','-i','-e','modular-gladiator-arena-base170-v1','-e','modular-dungeon-gothic-base352-v1',
                              r'F:\OpenRPG_ImportWork\queue','-g','*.json','-g','*.py'],capture_output=True,text=True,encoding='utf8')
        if queue.returncode not in (0,1) or queue.stdout:raise ValueError('active queue references extraction')
        processes=subprocess.run(['powershell','-NoProfile','-Command',
            'Get-CimInstance Win32_Process -ErrorAction Stop | Select-Object Name,CommandLine | ConvertTo-Json -Compress'],
            capture_output=True,text=True,check=True).stdout.casefold()
        if any(x in processes for x in ('modular-gladiator-arena-base170-v1','modular-dungeon-gothic-base352-v1','unity.exe')):
            raise ValueError('active process may use Unity source')
        for archive,pin,size in archives:
            stream=stack.enter_context(storage.locked_source(archive,share_delete=False))
            if os.fstat(stream.fileno()).st_size!=size or hashlib.file_digest(stream,'sha256').hexdigest()!=pin:
                raise ValueError('reference archive changed')
        handles=[]
        for row in rows:
            old=storage.confined(vault,vault/row['old']);keep=storage.confined(vault,vault/row['retained'])
            if old.stat().st_size!=row['bytes'] or keep.stat().st_size!=row['bytes']:raise ValueError('source size changed')
            a=stack.enter_context(ExclusiveSource(old));b=stack.enter_context(storage.locked_source(keep,share_delete=False))
            if a.sha256()!=row['sha256'] or hashlib.file_digest(b,'sha256').hexdigest()!=row['sha256']:
                raise ValueError('source SHA changed')
            handles.append(a)
        if (registry.read_bytes()!=registry_raw or
            (vault/'metadata/asset-source-support-registry.v1.json').read_bytes()!=source_before or
            (vault/'metadata/asset-import-registry.v1.json').read_bytes()!=models_before or
            any(x.manifest_path.read_bytes()!=x.manifest_raw for x in resolvers)):
            raise ValueError('registry or overlay changed')
        result={'files':len(rows),'logical_bytes':sum(x['bytes'] for x in rows),'rows':rows}
        if not remove:return dict(result,state='dry_run_passed')
        recovery=vault/'metadata/asset-source-support-transactions'/('unity-overlay-recovery-'+REGISTRY_SHA+'.json')
        if recovery.exists():raise ValueError('recovery already exists')
        with recovery.open('xb') as f:f.write((json.dumps(result,indent=2)+'\n').encode());f.flush();os.fsync(f.fileno())
        before=shutil.disk_usage(vault).free
        with journal.open('x',encoding='utf8') as log:
            def record(x):log.write(json.dumps(x)+'\n');log.flush();os.fsync(log.fileno())
            record({'state':'prepared','recovery':str(recovery),'free_before':before})
            for row,handle in zip(rows,handles):
                record({'state':'verified',**row});handle.mark_delete();record({'state':'marked_delete','old':row['old']})
    if any((vault/r['old']).exists() for r in rows):raise ValueError('old source still present')
    for row in rows:
        with storage.confined(vault,vault/row['retained']).open('rb') as f:
            if hashlib.file_digest(f,'sha256').hexdigest()!=row['sha256']:raise ValueError('retained post-check failed')
    return dict(result,state='removed_verified',free_delta_observed=shutil.disk_usage(vault).free-before,recovery=str(recovery))


if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__)
    for name in ('vault','journal','output'):p.add_argument('--'+name,type=Path,required=True)
    p.add_argument('--importer-sha256',required=True);p.add_argument('--remove',action='store_true');a=p.parse_args()
    if a.output.exists():p.error('output exists')
    result=run(a.vault,importer_module(a.importer_sha256),a.journal,a.remove)
    with a.output.open('x',encoding='utf8') as f:json.dump(result,f,indent=2);f.write('\n')
    print(json.dumps({k:v for k,v in result.items() if k!='rows'}))
