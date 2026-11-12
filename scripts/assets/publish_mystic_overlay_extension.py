"""Publish an exact, tested 24-file extension to the Mystic Unity overlay."""
import argparse
import json
import os
from pathlib import Path
import uuid

from publish_source_support_relinks import importer_module
from publish_variant_transition import ensure_vault_directory,exclusive_storage_lock
from unity_shared_overlay import UnitySourceResolver,file_pin
import vault_storage as storage

OLD_REG='1122c19b389cbd71ea47fd58396be7351fa67a157b67aa4dec6d0305ef4fb425'
OLD='7db53783d1681ababf0e933fae34dfdaaf3706b5f0a7319ca3f8e56b04636bd2'
NEW='5ba96595407dbaf2ccbb903cfe01deea720125da2681bb796d4ddeb8c7e9650b'
PACKAGE='originals/unity-purchased/hivemind/source-support-v1/dungeon-remainder20-shader-blocked-v1'


def run(vault,stage,unity_proof,guid_proof,prior_proof,importer,apply):
    vault=vault.resolve(strict=True);stage=stage.resolve(strict=True)
    with importer.shared_import_lock(vault),exclusive_storage_lock(vault):
        registry=storage.confined(vault,vault/'metadata/unity-shared-overlay-registry.v1.json')
        raw=registry.read_bytes();doc=json.loads(raw)
        if storage.sha(raw)!=OLD_REG:raise ValueError('registry changed')
        old_path=storage.confined(vault,vault/('metadata/unity-shared-overlays/'+OLD+'.json'))
        old_raw=old_path.read_bytes();new_raw=stage.read_bytes()
        if storage.sha(old_raw)!=OLD or storage.sha(new_raw)!=NEW:raise ValueError('manifest SHA changed')
        old,new=json.loads(old_raw),json.loads(new_raw)
        if old['source_root']!=new['source_root'] or old['comparison_sha256']!=new['comparison_sha256']:
            raise ValueError('overlay identity changed')
        oldrows={x['logical'].casefold():x for x in old['files']};newrows={x['logical'].casefold():x for x in new['files']}
        if len(oldrows)!=len(old['files']) or len(newrows)!=len(new['files']) or set(oldrows)!=set(newrows):
            raise ValueError('overlay logical entries changed')
        changed=[]
        for key in oldrows:
            a,b=oldrows[key],newrows[key]
            if {k:v for k,v in a.items() if k!='vault_path'}!={k:v for k,v in b.items() if k!='vault_path'}:
                raise ValueError('overlay logical pin changed')
            if a['vault_path']!=b['vault_path']:
                if file_pin(storage.confined(vault,vault/a['vault_path']))!=(a['sha256'],a['bytes']):raise ValueError('old payload changed')
                if file_pin(storage.confined(vault,vault/b['vault_path']))!=(b['sha256'],b['bytes']):raise ValueError('retained payload changed')
                changed.append(key)
        if len(changed)!=24 or new['shared_files']!=116 or old['shared_files']!=92:
            raise ValueError('unexpected overlay extension')
        resolver=UnitySourceResolver(vault,stage,NEW)
        for row in new['files']:
            if row['vault_path'].casefold().startswith((new['source_root']+'/').casefold()):continue
            resolver.resolve(row['logical'])
        ur=unity_proof.read_bytes();gr=guid_proof.read_bytes();pr=prior_proof.read_bytes()
        u,g,p=json.loads(ur),json.loads(gr),json.loads(pr)
        if (u.get('state')!='passed_temporary_project_removed' or u.get('checked_count')!=12
                or u.get('dependency_count')!=9 or u.get('failures')!=0):raise ValueError('Unity proof missing')
        if (g.get('state')!='passed' or g.get('manifest_sha256')!=NEW or g.get('guids')!=12
                or p.get('state')!='passed' or p.get('manifest_sha256')!=NEW or p.get('guids')!=46):
            raise ValueError('GUID proof missing')
        entries=doc['overlays'];matches=[i for i,x in enumerate(entries) if x.get('sha256')==OLD]
        if len(matches)!=1:raise ValueError('existing overlay ambiguous')
        updated=json.loads(raw);updated['overlays'][matches[0]]={'source_root':new['source_root'],
            'manifest':'metadata/unity-shared-overlays/'+NEW+'.json','sha256':NEW}
        encoded=importer.canonical_json(updated)
        if (registry.read_bytes()!=raw or stage.read_bytes()!=new_raw or old_path.read_bytes()!=old_raw
                or unity_proof.read_bytes()!=ur or guid_proof.read_bytes()!=gr or prior_proof.read_bytes()!=pr):
            raise ValueError('inputs changed')
        if not apply:return {'state':'dry_run_passed','added_files':24,'total_shared':116}
        directory=ensure_vault_directory(vault,['metadata','unity-shared-overlays']);target=directory/(NEW+'.json')
        if target.exists():
            if target.read_bytes()!=new_raw:raise ValueError('conflicting overlay')
        else:
            with target.open('xb') as f:f.write(new_raw);f.flush();os.fsync(f.fileno())
        if target.read_bytes()!=new_raw or registry.read_bytes()!=raw:raise ValueError('readback changed')
        txid=uuid.uuid4().hex;txdir=ensure_vault_directory(vault,['metadata','unity-shared-overlay-transactions'])
        receipt={'schema':'openrpg.unity-shared-overlay-transaction.v1','state':'prepared','transaction':txid,
                 'prior_registry':doc,'prior_sha256':storage.sha(raw),'new_registry':updated,'new_sha256':storage.sha(encoded),
                 'manifest_sha256':NEW,'unity_proof_sha256':storage.sha(ur),'guid_proof_sha256':storage.sha(gr)}
        tx=txdir/(txid+'.json');importer.atomic_write(tx,importer.canonical_json(receipt));importer.atomic_write(registry,encoded)
        if registry.read_bytes()!=encoded:raise ValueError('registry readback failed')
        receipt['state']='committed';importer.atomic_write(tx,importer.canonical_json(receipt))
        return {'state':'published','transaction':txid,'registry_sha256':storage.sha(encoded),'added_files':24,'files_removed':0}


if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__)
    for name in ('vault','stage','unity-proof','guid-proof','prior-proof','output'):p.add_argument('--'+name,type=Path,required=True)
    p.add_argument('--importer-sha256',required=True);p.add_argument('--apply',action='store_true');a=p.parse_args()
    if a.output.exists():p.error('output exists')
    result=run(a.vault,a.stage,a.unity_proof,a.guid_proof,a.prior_proof,importer_module(a.importer_sha256),a.apply)
    with a.output.open('x',encoding='utf8') as f:json.dump(result,f,indent=2);f.write('\n')
    print(json.dumps(result))
