"""Retire verified Mystic Unity duplicates using the published source overlay."""
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
from unity_shared_overlay import configured_resolver,changed_overlay_rows
import vault_storage as storage

WORK=Path('E:/OpenRPG_Forge/.build/asset-conversion')
PACKAGE='originals/unity-purchased/hivemind/source-support-v1/dungeon-remainder20-shader-blocked-v1'
PIN='7db53783d1681ababf0e933fae34dfdaaf3706b5f0a7319ca3f8e56b04636bd2'
REGISTRY='1122c19b389cbd71ea47fd58396be7351fa67a157b67aa4dec6d0305ef4fb425'
EXT_PIN='5ba96595407dbaf2ccbb903cfe01deea720125da2681bb796d4ddeb8c7e9650b'
EXT_REGISTRY='6d37ba0f89ed010ca9e70f44ee744451df5e622b744bdf14c936116895b694f7'


def run(vault,importer,journal,remove,extension=False):
    vault=vault.resolve(strict=True);work=WORK.resolve(strict=True)
    if journal.exists():raise ValueError('journal already exists')
    registry=storage.confined(vault,vault/'metadata/unity-shared-overlay-registry.v1.json');registry_raw=registry.read_bytes()
    expected_registry=EXT_REGISTRY if extension else REGISTRY;pin=EXT_PIN if extension else PIN
    count=24 if extension else 92;guids=12 if extension else 46;references=9 if extension else 55
    if storage.sha(registry_raw)!=expected_registry:raise ValueError('overlay registry changed')
    base=storage.confined(vault,vault/PACKAGE);resolver=configured_resolver(vault,base/'source')
    if resolver is None or storage.sha(resolver.manifest_raw)!=pin:raise ValueError('published overlay changed')
    manifest=json.loads(resolver.manifest_raw);root=manifest['source_root']+'/';rows=[]
    previous=storage.confined(vault,vault/('metadata/unity-shared-overlays/'+PIN+'.json'))
    selected=changed_overlay_rows(manifest,json.loads(previous.read_bytes()),count) if extension else [x for x in manifest['files'] if not x['vault_path'].casefold().startswith(root.casefold())]
    for item in selected:
        old=storage.confined(vault,resolver.source_root/item['logical']);keep=storage.confined(vault,vault/item['vault_path'])
        if old==keep or old.suffix.lower() not in ({'.prefab','.mat','.meta'} if extension else {'.png','.tga','.fbx','.meta'}) or not storage.valid_hex(item['sha256'],64):
            raise ValueError('unexpected duplicate')
        rows.append({'old':old.relative_to(vault).as_posix(),'retained':keep.relative_to(vault).as_posix(),
                     'sha256':item['sha256'],'bytes':item['bytes']})
    if len(rows)!=count or len({r['old'].casefold() for r in rows})!=count:raise ValueError('unexpected duplicate count')
    proof=json.loads((work/('mystic-extension-export-locked-proof-20261112.json' if extension else 'mystic-export-locked-proof-20261112.json')).read_bytes())
    if (proof.get('state')!='passed' or proof.get('old_paths_locked')!=count or proof.get('manifest_sha256')!=pin
            or proof.get('guids')!=guids or proof.get('guid_file_references')!=references):raise ValueError('reconstruction proof missing')
    exclusive=json.loads((work/('mystic-extension-exclusive-20261112.json' if extension else 'mystic-overlay-exclusive-20261112.json')).read_bytes())
    if exclusive.get('state')!='passed' or exclusive.get('old_paths_locked')!=count or exclusive.get('registry_sha256')!=expected_registry:
        raise ValueError('exclusive GUID proof missing')
    prepared_raw=(work/('mystic-prefab-unity-prepared-20261112.json' if extension else 'mystic-unity-guid-prepared-20261112.json')).read_bytes()
    unity=json.loads((work/('mystic-prefab-unity-receipt-20261112.json' if extension else 'mystic-unity-guid-receipt-20261112.json')).read_bytes())
    if (unity.get('state')!='passed_temporary_project_removed' or unity.get('prepared_sha256')!=storage.sha(prepared_raw)
            or unity.get('checked_count')!=guids or unity.get('dependency_count')!=(9 if extension else 48) or unity.get('failures')!=0):
        raise ValueError('Unity proof missing')
    pm=json.loads(storage.confined(vault,base/'manifest.v1.json').read_bytes())
    archive=storage.confined(vault,Path(pm['source_archive']))
    with importer.shared_import_lock(vault),exclusive_storage_lock(vault),contextlib.ExitStack() as stack:
        source_before,models_before=active_references(vault,{r['old'].casefold() for r in rows})
        queue=subprocess.run(['rg','-l','-i','-F',Path(PACKAGE).name,r'F:\OpenRPG_ImportWork\queue','-g','*.json','-g','*.py'],
                             capture_output=True,text=True,encoding='utf8')
        if queue.returncode not in (0,1) or queue.stdout:raise ValueError('queue references extraction')
        processes=subprocess.run(['powershell','-NoProfile','-Command',
            'Get-CimInstance Win32_Process -ErrorAction Stop | Select-Object Name,CommandLine | ConvertTo-Json -Compress'],
            capture_output=True,text=True,check=True).stdout.casefold()
        if Path(PACKAGE).name in processes or 'unity.exe' in processes:raise ValueError('active process may use source')
        a=stack.enter_context(storage.locked_source(archive,share_delete=False))
        if os.fstat(a.fileno()).st_size!=pm['source_archive_bytes'] or hashlib.file_digest(a,'sha256').hexdigest()!=pm['source_archive_sha256']:
            raise ValueError('reference archive changed')
        handles=[]
        for row in rows:
            old=storage.confined(vault,vault/row['old']);keep=storage.confined(vault,vault/row['retained'])
            if old.stat().st_size!=row['bytes'] or keep.stat().st_size!=row['bytes']:raise ValueError('size changed')
            old_handle=stack.enter_context(ExclusiveSource(old));new_handle=stack.enter_context(storage.locked_source(keep,share_delete=False))
            if old_handle.sha256()!=row['sha256'] or hashlib.file_digest(new_handle,'sha256').hexdigest()!=row['sha256']:
                raise ValueError('payload SHA changed')
            handles.append(old_handle)
        if (registry.read_bytes()!=registry_raw or resolver.manifest_path.read_bytes()!=resolver.manifest_raw
            or (vault/'metadata/asset-source-support-registry.v1.json').read_bytes()!=source_before
            or (vault/'metadata/asset-import-registry.v1.json').read_bytes()!=models_before):raise ValueError('registry changed')
        result={'files':len(rows),'logical_bytes':sum(r['bytes'] for r in rows),'rows':rows}
        if not remove:return dict(result,state='dry_run_passed')
        recovery=vault/'metadata/asset-source-support-transactions'/('mystic-overlay-recovery-'+pin+'.json')
        if recovery.exists():raise ValueError('recovery already exists')
        with recovery.open('xb') as f:f.write((json.dumps(result,indent=2)+'\n').encode());f.flush();os.fsync(f.fileno())
        before=shutil.disk_usage(vault).free
        with journal.open('x',encoding='utf8') as f:
            def record(x):f.write(json.dumps(x)+'\n');f.flush();os.fsync(f.fileno())
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
    p.add_argument('--importer-sha256',required=True);p.add_argument('--remove',action='store_true');p.add_argument('--extension',action='store_true');a=p.parse_args()
    if a.output.exists():p.error('output exists')
    result=run(a.vault,importer_module(a.importer_sha256),a.journal,a.remove,a.extension)
    with a.output.open('x',encoding='utf8') as f:json.dump(result,f,indent=2);f.write('\n')
    print(json.dumps({k:v for k,v in result.items() if k!='rows'}))
