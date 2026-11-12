"""Add a verified Mystic source overlay to the existing Vault registry."""
import argparse
import json
import os
from pathlib import Path
import uuid

from publish_source_support_relinks import importer_module
from publish_variant_transition import ensure_vault_directory,exclusive_storage_lock
from unity_shared_overlay import configured_resolver
from verify_unity_shared_resolver import verify
import vault_storage as storage

PIN='7db53783d1681ababf0e933fae34dfdaaf3706b5f0a7319ca3f8e56b04636bd2'
OLD_REGISTRY='da3c7250fa2ebfa3aa5ed06ef27ffee12706283dec9e13fdb1ded38921f153ca'
PACKAGE='originals/unity-purchased/hivemind/source-support-v1/dungeon-remainder20-shader-blocked-v1'


def run(vault,manifest,unity_proof,guid_proof,importer,apply):
    vault=vault.resolve(strict=True);manifest=manifest.resolve(strict=True)
    with importer.shared_import_lock(vault),exclusive_storage_lock(vault):
        registry=storage.confined(vault,vault/'metadata/unity-shared-overlay-registry.v1.json')
        raw=registry.read_bytes();doc=json.loads(raw)
        if storage.sha(raw)!=OLD_REGISTRY or doc.get('schema')!='openrpg.unity-shared-overlay-registry.v1':
            raise ValueError('overlay registry changed')
        mr=manifest.read_bytes();m=json.loads(mr)
        if storage.sha(mr)!=PIN or not m['source_root'].startswith(PACKAGE+'/source/'):
            raise ValueError('candidate overlay changed')
        if configured_resolver(vault,vault/PACKAGE/'source') is not None:
            raise ValueError('package already has an overlay')
        unity_raw=unity_proof.read_bytes();u=json.loads(unity_raw)
        guid_raw=guid_proof.read_bytes();g=json.loads(guid_raw)
        if (u.get('state')!='passed_temporary_project_removed' or u.get('checked_count')!=46
                or u.get('dependency_count')!=48 or u.get('failures')!=0):raise ValueError('Unity proof missing')
        if g.get('state')!='passed' or g.get('manifest_sha256')!=PIN or g.get('guids')!=46 or g.get('guid_file_references')!=55:
            raise ValueError('GUID proof missing')
        checked=verify(vault,manifest)
        if checked['checked_files']!=92 or checked['manifest_sha256']!=PIN:raise ValueError('source comparison changed')
        source=storage.confined(vault,vault/'metadata/asset-source-support-registry.v1.json');before=source.read_bytes()
        models=storage.confined(vault,vault/'metadata/asset-import-registry.v1.json');model_before=models.read_bytes()
        record={'source_root':m['source_root'],'manifest':'metadata/unity-shared-overlays/'+PIN+'.json','sha256':PIN}
        updated={'schema':doc['schema'],'overlays':[*doc['overlays'],record]};new=importer.canonical_json(updated)
        if (registry.read_bytes()!=raw or manifest.read_bytes()!=mr or unity_proof.read_bytes()!=unity_raw or
                guid_proof.read_bytes()!=guid_raw or source.read_bytes()!=before or models.read_bytes()!=model_before):
            raise ValueError('publication inputs changed')
        if not apply:return {'state':'dry_run_passed','overlays':len(updated['overlays']),'shared_files':92}
        directory=ensure_vault_directory(vault,['metadata','unity-shared-overlays']);target=directory/(PIN+'.json')
        if target.exists():
            if target.read_bytes()!=mr:raise ValueError('conflicting immutable overlay')
        else:
            with target.open('xb') as f:f.write(mr);f.flush();os.fsync(f.fileno())
        if target.read_bytes()!=mr or registry.read_bytes()!=raw:raise ValueError('readback or registry changed')
        txid=uuid.uuid4().hex;txdir=ensure_vault_directory(vault,['metadata','unity-shared-overlay-transactions'])
        receipt={'schema':'openrpg.unity-shared-overlay-transaction.v1','state':'prepared','transaction':txid,
                 'prior_registry':doc,'prior_sha256':storage.sha(raw),'new_registry':updated,'new_sha256':storage.sha(new),
                 'manifest_sha256':PIN,'unity_proof_sha256':storage.sha(unity_raw),'guid_proof_sha256':storage.sha(guid_raw)}
        tx=txdir/(txid+'.json');importer.atomic_write(tx,importer.canonical_json(receipt))
        importer.atomic_write(registry,new)
        if registry.read_bytes()!=new or configured_resolver(vault,vault/PACKAGE/'source') is None:
            raise ValueError('published registry readback failed')
        receipt['state']='committed';importer.atomic_write(tx,importer.canonical_json(receipt))
        return {'state':'published','transaction':txid,'registry_sha256':storage.sha(new),'shared_files':92,'files_removed':0}


if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__)
    for name in ('vault','manifest','unity-proof','guid-proof','output'):p.add_argument('--'+name,type=Path,required=True)
    p.add_argument('--importer-sha256',required=True);p.add_argument('--apply',action='store_true');a=p.parse_args()
    if a.output.exists():p.error('output exists')
    result=run(a.vault,a.manifest,a.unity_proof,a.guid_proof,importer_module(a.importer_sha256),a.apply)
    with a.output.open('x',encoding='utf8') as f:json.dump(result,f,indent=2);f.write('\n')
    print(json.dumps(result))
