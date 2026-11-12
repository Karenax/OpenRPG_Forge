"""Retire identical Viking FBX copies only after catalog migration and verification."""
import argparse
import contextlib
import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess
import tempfile

import vault_storage as storage
from exclusive_source_delete import ExclusiveSource
from publish_model_preview_relinks import load_importer
from publish_variant_transition import exclusive_storage_lock
from retire_relinked_source_copies import active_references


def validate_plan(vault,plan):
    if plan.get('schema')!='openrpg.viking-fbx-relink-stage.v1' or plan.get('state')!='prepared_only':
        raise ValueError('invalid Viking transition')
    paths=plan.get('paths');seen=set();retained=set()
    if not isinstance(paths,list) or not paths:raise ValueError('empty transition')
    for row in paths:
        old=Path(row['old']);keep=Path(row['retained'])
        for p in (old,keep):
            if not p.is_absolute() or not p.is_relative_to(vault) or p.suffix.lower()!='.fbx':
                raise ValueError('FBX path outside Vault')
        rel=old.relative_to(vault).as_posix().casefold()
        if not rel.startswith('normalized/hivemind/modular-viking-village-') or '/sources/' not in rel:
            raise ValueError('old FBX outside Viking package sources')
        if old==keep or rel in seen or not storage.valid_hex(row.get('sha256'),64) or type(row.get('bytes')) is not int or row['bytes']<=0:
            raise ValueError('duplicate or invalid FBX mapping')
        seen.add(rel);retained.add(keep.relative_to(vault).as_posix().casefold())
    if seen & retained:raise ValueError('retained path is also a retirement target')
    return seen


def run(vault,stage,adapter,proof_path,published_proof_path,publication_path,binary,studio,importer,journal,remove):
    vault=vault.resolve(strict=True);stage=stage.resolve(strict=True);adapter=adapter.resolve(strict=True)
    plan_raw=(stage/'plan.json').read_bytes();plan=json.loads(plan_raw);targets=validate_plan(vault,plan)
    proof_raw=proof_path.read_bytes();pubproof_raw=published_proof_path.read_bytes();publication_raw=publication_path.read_bytes()
    proof,pubproof,publication=map(json.loads,(proof_raw,pubproof_raw,publication_raw))
    plan_sha=storage.sha(plan_raw);binary_sha=storage.sha(binary.read_bytes());studio_sha=storage.sha(studio.read_bytes())
    for item in (proof,pubproof):
        if (item.get('state')!='passed' or item.get('plan_sha256')!=plan_sha
                or item.get('native_test_sha256')!=binary_sha or item.get('studio_binary_sha256')!=studio_sha
                or item.get('cards')!=len(plan['changed_ids'])):
            raise ValueError('native reader proof differs')
    if publication.get('state')!='committed' or publication.get('models')!=len(plan['changed_ids']):
        raise ValueError('publication receipt differs')
    adapter_raw=(adapter/'plan.json').read_bytes()
    if storage.sha(adapter_raw)!=pubproof['adapter_sha256']:raise ValueError('reader adapter changed')
    if journal.exists():raise ValueError('journal already exists')
    with importer.locked(vault),exclusive_storage_lock(vault),contextlib.ExitStack() as stack:
        source_raw,model_raw=active_references(vault,targets);registry=json.loads(model_raw)
        for pack,pins in plan['packs'].items():
            if registry['packs'][pack]['revision']!=pins['revision']:
                raise ValueError('published Viking revision changed')
        # Retired catalog paths must be routed to the pinned derived catalogs on reimport.
        registry_sha=storage.sha(model_raw)
        for row in plan['catalogs']:
            if importer.redirected_prefab_catalog(vault,Path(row['old']),registry_sha)!=Path(row['new']):
                raise ValueError('canonical catalog redirect differs')
        queue=Path('F:/OpenRPG_ImportWork/queue')
        if not queue.is_dir():raise ValueError('import queue missing')
        with tempfile.TemporaryDirectory(dir='E:/OpenRPG_Forge/.build/asset-conversion') as tmp:
            patterns=Path(tmp)/'paths.txt'
            patterns.write_text(''.join(str(Path(x['old']).name)+'\n' for x in plan['paths']),encoding='utf8')
            scan=subprocess.run(['rg','-l','-i','-F','-f',str(patterns),str(queue),'-g','*.json','-g','*.py'],
                                capture_output=True,text=True,encoding='utf8',errors='replace')
        if scan.returncode not in (0,1) or scan.stdout:
            raise ValueError('import queue may reference Viking files')
        command=['powershell','-NoProfile','-Command',
                 'Get-CimInstance Win32_Process -ErrorAction Stop | Select-Object ProcessId,Name,CommandLine | ConvertTo-Json -Compress']
        processes=subprocess.run(command,capture_output=True,text=True,encoding='utf8',errors='replace')
        if processes.returncode:raise ValueError('process inventory unavailable')
        names={'modular-viking-village-native-first64-v3','modular-viking-village-raw-frame19-v1'}
        data=json.loads(processes.stdout or '[]');data=data if isinstance(data,list) else [data]
        for proc in data:
            if proc.get('ProcessId')==os.getpid():continue
            commandline=(proc.get('CommandLine') or '').casefold()
            if any(name in commandline for name in names):raise ValueError('Viking package process active')
        old_handles={};kept={}
        for row in plan['paths']:
            old=storage.confined(vault,Path(row['old']));keep=storage.confined(vault,Path(row['retained']))
            if storage.windows_readonly(old.stat()):raise ValueError('read-only FBX requires inspection')
            handle=stack.enter_context(ExclusiveSource(old));old_handles[str(old).casefold()]=handle
            if handle.sha256()!=row['sha256'] or old.stat().st_size!=row['bytes']:
                raise ValueError('old FBX differs')
            if keep not in kept:kept[keep]=stack.enter_context(storage.locked_source(keep,share_delete=False))
            stream=kept[keep];stream.seek(0)
            if hashlib.file_digest(stream,'sha256').hexdigest()!=row['sha256'] or keep.stat().st_size!=row['bytes']:
                raise ValueError('retained FBX differs')
        env=os.environ.copy();env['OPENRPG_ASSET_VAULT']=str(vault);env['OPENRPG_MODEL_PREVIEW_STAGE']=str(adapter)
        native=subprocess.run([str(binary),'--ignored','--exact',
            'asset_library_native::tests::published_model_catalog_redirects_load_all_thumbnails','--nocapture'],
            env=env,capture_output=True,text=True,encoding='utf8',errors='replace')
        output=native.stdout+native.stderr
        if native.returncode or '1 passed' not in output or f'Verified {len(plan["changed_ids"])} published catalog redirects' not in output:
            raise ValueError('native reader failed while old files locked')
        if ((vault/'metadata/asset-source-support-registry.v1.json').read_bytes()!=source_raw
                or (vault/'metadata/asset-import-registry.v1.json').read_bytes()!=model_raw
                or (stage/'plan.json').read_bytes()!=plan_raw or (adapter/'plan.json').read_bytes()!=adapter_raw
                or proof_path.read_bytes()!=proof_raw or published_proof_path.read_bytes()!=pubproof_raw
                or publication_path.read_bytes()!=publication_raw):
            raise ValueError('retirement inputs changed')
        result={'files':len(targets),'logical_bytes':sum(x['bytes'] for x in plan['paths']),
                'reader_locked':'passed','model_registry_sha256':registry_sha,'rows':plan['paths'],
                'limitations':'Historical package manifests retain old paths; restore with recovery table for direct historical access.'}
        if not remove:return dict(result,state='dry_run_passed')
        txdir=storage.confined(vault,vault/'metadata/asset-source-support-transactions')
        recovery=txdir/('viking-fbx-recovery-'+plan_sha+'.json')
        if recovery.exists():raise ValueError('recovery table already exists')
        with recovery.open('xb') as f:f.write((json.dumps(result,indent=2)+'\n').encode());f.flush();os.fsync(f.fileno())
        before=shutil.disk_usage(vault).free
        with journal.open('x',encoding='utf8') as log:
            def record(x):log.write(json.dumps(x)+'\n');log.flush();os.fsync(log.fileno())
            record({'state':'prepared','recovery':str(recovery),'free_before':before})
            for row in plan['paths']:
                record({'state':'verified',**row})
                old_handles[str(Path(row['old'])).casefold()].mark_delete()
                record({'state':'marked_delete','old':row['old']})
    if any(Path(row['old']).exists() for row in plan['paths']):raise ValueError('old FBX still present')
    for row in plan['paths']:
        path=storage.confined(vault,Path(row['retained']))
        with storage.locked_source(path,share_delete=False) as stream:
            if hashlib.file_digest(stream,'sha256').hexdigest()!=row['sha256']:raise ValueError('retained FBX post-check failed')
    return dict(result,state='removed_verified',recovery=str(recovery),free_delta_observed=shutil.disk_usage(vault).free-before)


if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__)
    for n in ('vault','stage','adapter','proof','published-proof','publication','binary','studio','journal','output'):
        p.add_argument('--'+n,type=Path,required=True)
    p.add_argument('--importer-sha256',required=True);p.add_argument('--remove',action='store_true');a=p.parse_args()
    if a.output.exists():p.error('output exists')
    result=run(a.vault,a.stage,a.adapter,a.proof,a.published_proof,a.publication,a.binary,a.studio,
               load_importer(a.importer_sha256),a.journal,a.remove)
    with a.output.open('x',encoding='utf8') as f:json.dump(result,f,indent=2);f.write('\n')
    print(json.dumps({k:v for k,v in result.items() if k!='rows'}))
