"""Retire verified, unreferenced conversion copies; never scan the Vault.

Plans use a previous E: inventory/hash pass and independently verified migration
receipts. Applying a plan rechecks current references, processes, completed work,
both representations and the source under a Windows write-denying handle.
"""
import argparse
import collections
import contextlib
import hashlib
import json
import os
from pathlib import Path
import re
import shutil
import sqlite3
import subprocess
import time

import vault_storage as storage


def hash_inventory(inventory, root, output, progress=None):
    """One reusable E-only hash pass, resumed by path; no directory/Vault scan."""
    done=set()
    if output.exists():
        done={json.loads(s)['path'] for s in output.read_text().splitlines()}
    with contextlib.closing(sqlite3.connect('file:'+str(inventory)+'?mode=ro',uri=True)) as db:
        rows=db.execute("SELECT path,bytes,mtime_ns,job FROM files WHERE extension='.glb' AND git='' ORDER BY path").fetchall()
    with output.open('a',encoding='utf8') as stream:
        for name,size,mtime,job in rows:
            if name in done:continue
            row=dict(path=name,bytes=size,mtime_ns=mtime,job=job)
            try:
                path=confined(root,Path(name));before=path.stat()
                if (before.st_size,before.st_mtime_ns)!=(size,mtime):raise ValueError('Changed since inventory')
                with path.open('rb') as source:digest=hashlib.file_digest(source,'sha256').hexdigest()
                after=path.stat()
                if (before.st_ino,before.st_size,before.st_mtime_ns)!=(after.st_ino,after.st_size,after.st_mtime_ns):raise ValueError('Changed during hash')
                row.update(sha256=digest,state='hashed')
            except (OSError,ValueError) as error:row.update(state='excluded',reason=str(error))
            stream.write(json.dumps(row)+'\n');stream.flush();os.fsync(stream.fileno());done.add(name)
            if progress is not None:progress(len(done),len(rows))


def confined(root, path):
    root=Path(os.path.abspath(root));path=Path(os.path.abspath(path))
    path.relative_to(root)
    for item in [path,*path.parents]:
        if item.is_symlink() or (item.exists() and getattr(item.lstat(),'st_file_attributes',0)&1024):
            raise ValueError('Link/reparse point: '+str(item))
        if item.is_dir() and (item/'.git').exists():raise ValueError('Git worktree: '+str(item))
        if item==root:break
    return path


def active_commands():
    if os.name!='nt':raise ValueError('Live process protection requires the Windows host')
    result=subprocess.run(['powershell','-NoProfile','-Command',
        'Get-CimInstance Win32_Process -ErrorAction Stop | Select-Object ProcessId,CommandLine | ConvertTo-Json -Compress'],
        capture_output=True,text=True,check=True)
    return result.stdout.lower()


def references(repository, jobs):
    """Current code plus dependencies of incomplete/suspended jobs; no history scan."""
    pattern=re.compile(r'jobs[/\\]+([a-zA-Z0-9_.-]+)',re.I)
    protected=set();evidence=[]
    roots=[repository/p for p in ('apps','crates','tools','scripts','examples','.cargo') if (repository/p).exists()]
    run=subprocess.run(['rg','--files',*[str(p) for p in roots],'-g','*.rs','-g','*.py','-g','*.json','-g','*.toml','-g','*.ps1'],
        capture_output=True,text=True,check=True)
    files=[Path(p) for p in run.stdout.splitlines()]
    files.extend(p for p in (repository/'PROJECT_CONTEXT.md',repository.parent/'.build/frontend/studio-native/forge-profile.json') if p.exists())
    control=jobs.parent
    files.extend(p for p in control.iterdir() if p.is_file() and p.suffix.lower()=='.json' and ('CURRENT' in p.name or 'ACTIVE' in p.name))
    report=repository.parent/'Preuves-et-analyses/rapports/hivemind-conversion.md'
    if report.exists():
        current=report.read_text(encoding='utf-8-sig').split('<!-- /HIVEMIND-CURRENT -->')[0]
        protected.update(m.lower() for m in pattern.findall(current))
    # Only STATE files at the documented job/run level; do not recurse through data.
    for entry in jobs.iterdir():
        if not entry.is_dir() or entry.is_symlink() or getattr(entry.lstat(),'st_file_attributes',0)&1024:continue
        for run_dir in entry.iterdir():
            if not re.fullmatch(r'r\d+',run_dir.name) or not run_dir.is_dir():continue
            confined(jobs,run_dir)
            state=run_dir/'STATE.json'
            if not state.exists():
                # Ad-hoc packets still carry functional source/dependency pins.
                files.extend(p for p in (run_dir/'PACKET.v1.json',run_dir/'context.json',run_dir/'manifest.json') if p.exists())
                continue
            try:
                doc=json.loads(state.read_bytes())
                complete=doc.get('stages') and all(s.get('status')=='complete' for s in doc['stages'].values())
            except (OSError,ValueError,TypeError):complete=False
            if not complete:
                protected.add(entry.name.lower())
                files.extend(p for p in (run_dir/'manifest.json',run_dir/'context.json') if p.exists())
    for p in files:
        # The cleanup source itself contains no concrete job names.
        if p.is_symlink() or getattr(p.lstat(),'st_file_attributes',0)&1024:continue
        digest=hashlib.sha256();found=set();tail=''
        with p.open('rb') as stream:
            while raw:=stream.read(1024**2):
                digest.update(raw);text=tail+raw.decode('utf-8-sig',errors='replace')
                found.update(m.lower() for m in pattern.findall(text));tail=text[-8192:]
        protected.update(found)
        if found:evidence.append(dict(path=str(p),sha256=digest.hexdigest(),jobs=sorted(found)))
    return protected,evidence


def source_ready(root, row, protected, commands, verified_replica=None):
    path=confined(root,Path(row['path']));parts=path.relative_to(root).parts
    if len(parts)<3 or path.suffix.lower()!='.glb' or not re.fullmatch(r'r\d+',parts[1]):raise ValueError('Not a derived job/run GLB')
    job=parts[0].lower()
    if job in protected or job in commands:raise ValueError('Functional reference or active process')
    if re.search(r'-(source|intake)(-|$)',job):raise ValueError('Source/support work retained')
    if any(p.lower() in ('source','sources','originals','assets','packages','projectsettings') for p in parts[2:]):raise ValueError('Original source tree retained')
    state=confined(root,root/parts[0]/parts[1]/'STATE.json')
    if state.exists():
        raw=state.read_bytes();doc=json.loads(raw)
        if not doc.get('stages') or any(s.get('status')!='complete' for s in doc['stages'].values()):raise ValueError('Incomplete or suspended run')
        proof=dict(path=str(state),sha256=storage.sha(raw))
    elif (state.parent/'DELIVERY-READBACK.v1.json').exists() or any(state.parent.glob('GROUP-*-DELIVERY-READBACK.v1.json')):
        # Ad-hoc workers have no controller STATE. Require an explicit delivered
        # payload readback pin for this exact model. One run may deliver several
        # groups, each with its own immutable receipt; keep the selected pin.
        receipts=[state.parent/'DELIVERY-READBACK.v1.json',*sorted(state.parent.glob('GROUP-*-DELIVERY-READBACK.v1.json'))]
        for receipt in receipts:
            if not receipt.exists():continue
            receipt=confined(root,receipt)
            raw=receipt.read_bytes();doc=json.loads(raw)
            if not re.fullmatch(r'[A-Z0-9_]*SHA256_READBACK_PASS',doc.get('status','')):raise ValueError('No verified delivery readback')
            matches=[f for f in doc.get('files',[])+doc.get('models',[]) if f.get('sha256')==row['sha256'] and f.get('bytes')==row['bytes']]
            if not matches:continue
            delivered=[]
            for match in matches:
                delivered_path=Path(match['path'])
                if not delivered_path.is_absolute():
                    if not doc.get('destination'):raise ValueError('Relative delivered path without destination')
                    delivered_path=Path(doc['destination'])/delivered_path
                delivered.append(str(delivered_path))
            proof=dict(path=str(receipt),sha256=storage.sha(raw),delivered_paths=delivered)
            break
        else:raise ValueError('Model absent from delivered payload')
    else:
        # A file-level replica receipt is sufficient for ad-hoc model outputs.
        # Missing STATE/READBACK filenames must not retain an unreferenced copy
        # forever when its exact bytes already survive in the verified Vault.
        r=verified_replica
        if not r or r.get('state')!='complete' or r.get('sha256')!=row['sha256'] or r.get('bytes')!=row['bytes'] or not storage.valid_hex(r.get('descriptor_sha256',''),64):raise ValueError('No independently verified replica for this ad-hoc model')
        proof=dict(kind='verified-vault-replica',path=r['path'],sha256=r['sha256'],descriptor_sha256=r['descriptor_sha256'])
    info=path.stat()
    if info.st_size!=row['bytes'] or info.st_mtime_ns!=row['mtime_ns']:raise ValueError('Source changed')
    return path,proof


def make_plan(root, repository, hashed, receipts, commands):
    protected,evidence=references(repository,root)
    rows=[r for r in hashed if r.get('state')=='hashed'];groups=collections.defaultdict(list)
    verified=collections.defaultdict(list)
    for r in receipts:
        if r.get('state')=='complete':verified[(r['bytes'],r['sha256'])].append(r)
    for r in rows:groups[(r['bytes'],r['sha256'])].append(r)
    selected=[];excluded=[]
    for key,group in groups.items():
        keeper=max(group,key=lambda r:(r['job'].lower() in protected,r['mtime_ns']))
        for row in group:
            if not verified[key] and row['path']==keeper['path']:continue
            try:
                _,state=source_ready(root,row,protected,commands,verified[key][0] if verified[key] else None)
                choices=verified[key]
                if state.get('delivered_paths'):
                    choices=[r for r in choices if r['path'] in state['delivered_paths']]
                    if not choices:raise ValueError('Delivered representation not yet independently verified by migration')
                target=dict(kind='vault-receipt',receipt=choices[0]) if choices else dict(kind='work',file=keeper)
                selected.append(dict(source=row,target=target,completed_state=state))
            except (ValueError,OSError,KeyError) as error:excluded.append(dict(path=row['path'],reason=str(error)))
    return dict(schema=1,work_root=str(root),repository=str(repository),utc=storage.utc(),
        files=selected,excluded=excluded,protected_jobs=sorted(protected),reference_evidence=evidence)


def verify_target(item, root, vault, stack=None):
    if stack is None:
        with contextlib.ExitStack() as held:return verify_target(item,root,vault,held)
    source=item['source'];target=item['target']
    if target['kind']=='work':
        path=confined(root,Path(target['file']['path']))
        if path==Path(source['path']):raise ValueError('Self retention')
        stream=stack.enter_context(storage.locked_source(path,share_delete=False))
        if hashlib.file_digest(stream,'sha256').hexdigest()!=source['sha256']:raise ValueError('Retained copy changed')
    elif target['kind']=='vault-receipt':
        receipt=target['receipt'];path=storage.confined(vault,Path(receipt['path']))
        stream=stack.enter_context(storage.locked_source(path,share_delete=False));raw=stream.read(storage.MAX_DESCRIPTOR+1)
        if receipt.get('representation')=='raw-glb':
            # Raw GLBs have no descriptor: rehash the whole locked file, and
            # require both the receipt and source to name the exact same bytes.
            if not raw.startswith(b'glTF') or receipt['descriptor_sha256']!=source['sha256']:
                raise ValueError('Invalid raw GLB replica receipt')
            stream.seek(0)
            if hashlib.file_digest(stream,'sha256').hexdigest()!=source['sha256'] or path.stat().st_size!=source['bytes']:
                raise ValueError('Raw Vault model changed')
            return
        # Compacted models retain their exact independently verified descriptor.
        if not raw.startswith(storage.MAGIC) or storage.sha(raw)!=receipt['descriptor_sha256']:raise ValueError('Verified Vault descriptor changed')
        doc=storage.decode_json(raw[len(storage.MAGIC):]);storage.validate_descriptor(doc,storage.MAX_MODEL)
        marker=storage.confined(vault,vault/'.forge-storage/root.v1.json')
        if storage.decode_json(marker.read_bytes()).get('vault_id')!=doc['vault_id']:raise ValueError('Vault identity mismatch')
        if doc['sha256']!=source['sha256'] or doc['bytes']!=source['bytes']:raise ValueError('Logical model differs')
    else:raise ValueError('Unknown retention target')


def apply_plan(plan, vault, commands, journal=None):
    root=Path(plan['work_root']);repository=Path(plan['repository'])
    protected,evidence=references(repository,root)
    # Never delete a path which this plan retains for another file.
    retained={x['target']['file']['path'] for x in plan['files'] if x['target']['kind']=='work'}
    result=dict(utc=storage.utc(),free_before=shutil.disk_usage(root).free,removed=[],excluded=[],reference_evidence=evidence)
    refresh=time.monotonic()
    def record(entry):
        if journal is not None:
            with Path(journal).open('a',encoding='utf8') as stream:
                stream.write(json.dumps(entry)+'\n');stream.flush();os.fsync(stream.fileno())
    for item in plan['files']:
        if journal is not None and time.monotonic()-refresh>10:
            commands=active_commands();refresh=time.monotonic()
        row=item['source']
        try:
            if row['path'] in retained:raise ValueError('Retained by this plan')
            replica=item['target'].get('receipt') if item['target']['kind']=='vault-receipt' else None
            path,state=source_ready(root,row,protected,commands,replica)
            if state!=item['completed_state']:raise ValueError('Completed run state changed')
            with contextlib.ExitStack() as held:
                verify_target(item,root,vault,held)
                stream=held.enter_context(storage.locked_source(path))
                before=os.fstat(stream.fileno())
                if hashlib.file_digest(stream,'sha256').hexdigest()!=row['sha256']:raise ValueError('Source hash changed')
                current=path.stat()
                if (before.st_ino,before.st_size,before.st_mtime_ns)!=(current.st_ino,current.st_size,current.st_mtime_ns):raise ValueError('Source replaced during verification')
                record(dict(state='verified-before-retirement',item=item,utc=storage.utc()))
                path.unlink()
            result['removed'].append(item)
            record(dict(state='removed',path=row['path'],utc=storage.utc()))
        except (OSError,ValueError,KeyError) as error:result['excluded'].append(dict(path=row['path'],reason=str(error)))
    result.update(free_after=shutil.disk_usage(root).free,logical_bytes=sum(x['source']['bytes'] for x in result['removed']))
    return result


def main():
    repository=Path(__file__).resolve().parents[2];workspace=repository.parent
    p=argparse.ArgumentParser(description=__doc__);sub=p.add_subparsers(dest='command',required=True)
    plan=sub.add_parser('plan');plan.add_argument('--hashes',type=Path,required=True);plan.add_argument('--receipts',type=Path,required=True);plan.add_argument('--output',type=Path,required=True)
    apply=sub.add_parser('apply');apply.add_argument('--plan',type=Path,required=True);apply.add_argument('--output',type=Path,required=True)
    args=p.parse_args();root=workspace/'.build/asset-conversion/deterministic_import/jobs';vault=Path(os.environ.get('OPENRPG_ASSET_VAULT','F:/OpenRPG_AssetVault'))
    commands=active_commands()
    if args.command=='plan':
        hashes=[json.loads(s) for s in args.hashes.read_text().splitlines()]
        receipts=json.loads(args.receipts.read_bytes());receipts=[r for rows in receipts.values() for r in rows]
        result=make_plan(root,repository,hashes,receipts,commands)
    else:
        plan=json.loads(args.plan.read_bytes())
        if plan.get('schema')!=1 or Path(plan['work_root'])!=root or Path(plan['repository'])!=repository:raise ValueError('Plan scope mismatch')
        result=apply_plan(plan,vault,commands,args.output.with_suffix('.jsonl'))
    storage.atomic_json(args.output,result)
    print(json.dumps(dict(files=len(result.get('files',result.get('removed',[]))),excluded=len(result['excluded']),logical_bytes=result.get('logical_bytes',0))))


if __name__=='__main__':main()
