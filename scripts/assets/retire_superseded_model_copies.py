"""Retire three inactive pilot models with verified active identical models."""
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

PAIRS = (
 ('normalized/hivemind/medieval-kingdom-v1/hdrp-alpha-pilot-v1/models/prefab-sm-fish-2-abc00000000002571688947622677864.glb',
  'normalized/hivemind/medieval-kingdom-v1/hdrp-canonical-library-v1/models/prefab-sm-fish-2-abc00000000002571688947622677864.glb',
  '7fd40c99', 'normalized/hivemind/medieval-kingdom-v1/hdrp-alpha-pilot-v1'),
 ('normalized/hivemind/modular-abandoned-hospital-v1/hdrp-extension-library-v1/models/prefab-sm-lamp1-abc00000000015848839726158823200-lod1.glb',
  'normalized/hivemind/modular-abandoned-hospital-v1/models/prefab-sm-lamp1-abc00000000015848839726158823200-lod1.glb',
  '2704de99', 'normalized/hivemind/modular-abandoned-hospital-v1/hdrp-extension-library-v1'),
 ('normalized/hivemind/modular-abandoned-hospital-v1/hdrp-extension-library-v1/models/prefab-sm-lamp1-abc00000000015848839726158823200.glb',
  'normalized/hivemind/modular-abandoned-hospital-v1/models/prefab-sm-lamp1-abc00000000015848839726158823200.glb',
  'a62d8c06', 'normalized/hivemind/modular-abandoned-hospital-v1/hdrp-extension-library-v1'),
)


def run(vault, inventory, prior_audit, importer, journal, remove):
    vault = vault.resolve(strict=True)
    raw, audit_raw = inventory.read_bytes(), prior_audit.read_bytes()
    groups = json.loads(raw)['groups']; audit = json.loads(audit_raw)
    indexed = {x['path'].replace('\\','/').casefold():x for x in audit['archives']}
    rows=[]
    for old, keep, prefix, root in PAIRS:
        matches=[g for g in groups if g['sha256'].startswith(prefix)]
        if len(matches)!=1 or not storage.valid_hex(matches[0]['sha256'],64):raise ValueError('invalid candidate identity')
        g=matches[0]; files={Path(x['path']):x for x in g['files']}
        a,b=vault/old,vault/keep
        if a not in files or b not in files or files[a]['bytes']!=files[b]['bytes']:
            raise ValueError('inventory paths or sizes differ')
        if indexed.get(str(a).replace('\\','/').casefold(),{}).get('references')!=0:
            raise ValueError('old model had active references')
        if indexed.get(str(b).replace('\\','/').casefold(),{}).get('references',0)<1:
            raise ValueError('retained model was not active')
        rows.append({'old':old,'retained':keep,'root':root,'sha256':g['sha256'],'bytes':files[a]['bytes']})
    if journal.exists():raise ValueError('journal already exists')
    with importer.shared_import_lock(vault), exclusive_storage_lock(vault), contextlib.ExitStack() as stack:
        source, models = active_references(vault,{r['old'].casefold() for r in rows})
        if storage.sha(models)!=audit['registry_sha256']:
            raise ValueError('model registry changed since active-retained audit')
        queue=subprocess.run(['rg','-l','-i','-e','hdrp-alpha-pilot-v1','-e','hdrp-extension-library-v1',
                              r'F:\OpenRPG_ImportWork\queue','-g','*.json','-g','*.py'],
                             capture_output=True,text=True,encoding='utf8')
        if queue.returncode not in (0,1) or queue.stdout:raise ValueError('queue references obsolete packages')
        processes=subprocess.run(['powershell','-NoProfile','-Command',
            'Get-CimInstance Win32_Process -ErrorAction Stop | Select-Object CommandLine | ConvertTo-Json -Compress'],
            capture_output=True,text=True,check=True).stdout.casefold()
        if 'hdrp-alpha-pilot-v1' in processes or 'hdrp-extension-library-v1' in processes:
            raise ValueError('active process may use obsolete packages')
        handles=[]
        for row in rows:
            root=Path(row['root']); old=storage.confined(vault,vault/row['old']); keep=storage.confined(vault,vault/row['retained'])
            receipt=json.loads(storage.confined(vault,vault/root/'ssd-delivery-verification.v1.json').read_bytes())
            rel=old.relative_to(vault/root).as_posix()
            entries=[x for x in receipt['files'] if x['path']==rel]
            if receipt.get('passed') is not True or len(entries)!=1 or entries[0].get('passed') is not True or entries[0].get('sha256')!=row['sha256'] or entries[0].get('bytes')!=row['bytes']:
                raise ValueError('original delivery receipt differs')
            if old.stat().st_size!=row['bytes'] or keep.stat().st_size!=row['bytes']:
                raise ValueError('payload size differs')
            a=stack.enter_context(ExclusiveSource(old));b=stack.enter_context(storage.locked_source(keep,share_delete=False))
            if a.sha256()!=row['sha256'] or hashlib.file_digest(b,'sha256').hexdigest()!=row['sha256']:
                raise ValueError('payload SHA differs')
            handles.append(a)
        if (inventory.read_bytes()!=raw or prior_audit.read_bytes()!=audit_raw or
            (vault/'metadata/asset-source-support-registry.v1.json').read_bytes()!=source or
            (vault/'metadata/asset-import-registry.v1.json').read_bytes()!=models):raise ValueError('inputs changed')
        result={'files':len(rows),'logical_bytes':sum(r['bytes'] for r in rows),'rows':rows}
        if not remove:return dict(result,state='dry_run_passed')
        recovery=vault/'metadata/asset-source-support-transactions'/('superseded-model-recovery-'+storage.sha(raw+audit_raw)+'.json')
        if recovery.exists():raise ValueError('recovery record already exists')
        with recovery.open('xb') as f:f.write((json.dumps(result,indent=2)+'\n').encode());f.flush();os.fsync(f.fileno())
        before=shutil.disk_usage(vault).free
        with journal.open('x',encoding='utf8') as f:
            def record(x):f.write(json.dumps(x)+'\n');f.flush();os.fsync(f.fileno())
            record({'state':'prepared','recovery':str(recovery),'free_before':before})
            for row,handle in zip(rows,handles):record({'state':'verified',**row});handle.mark_delete();record({'state':'marked_delete','old':row['old']})
    if any((vault/r['old']).exists() for r in rows):raise ValueError('old path remains')
    for row in rows:
        with storage.confined(vault,vault/row['retained']).open('rb') as f:
            if hashlib.file_digest(f,'sha256').hexdigest()!=row['sha256']:raise ValueError('post-check failed')
    return dict(result,state='removed_verified',free_delta_observed=shutil.disk_usage(vault).free-before,recovery=str(recovery))


if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__)
    for n in ('vault','inventory','prior-audit','journal','output'):p.add_argument('--'+n,type=Path,required=True)
    p.add_argument('--importer-sha256',required=True);p.add_argument('--remove',action='store_true');a=p.parse_args()
    if a.output.exists():p.error('output exists')
    result=run(a.vault,a.inventory,a.prior_audit,importer_module(a.importer_sha256),a.journal,a.remove)
    with a.output.open('x',encoding='utf8') as f:json.dump(result,f,indent=2);f.write('\n')
    print(json.dumps({k:v for k,v in result.items() if k!='rows'}))
