"""Publish two verified Unity overlay manifests; do not remove any source."""
import argparse
import json
import os
from pathlib import Path

import vault_storage as storage
from publish_source_support_relinks import importer_module
from publish_variant_transition import ensure_vault_directory, exclusive_storage_lock
from verify_unity_shared_resolver import verify

EXPECTED={
    'gladiator':('abbb92a765d545fe8d5bb65c995b95a62fb935e39b9dfbabec7ccc985facdd50',60),
    'gothic':('6f80e2d992e7c87fd635e26fb1792d9e09210ce86fb105b88d9972c7d05f4eb8',82),
}


def prepare(vault, manifests):
    vault=vault.resolve(strict=True);records=[];inputs={};roots=set()
    for name,(pin,count) in EXPECTED.items():
        path=manifests[name].resolve(strict=True);raw=path.read_bytes()
        if storage.sha(raw)!=pin:raise ValueError('overlay manifest SHA differs: '+name)
        doc=json.loads(raw);root=doc['source_root']
        if doc.get('schema')!='openrpg.unity-shared-overlay.v1' or root.casefold() in roots:
            raise ValueError('invalid or duplicate overlay root')
        roots.add(root.casefold());result=verify(vault,path)
        if result['manifest_sha256']!=pin or result['checked_files']!=count:
            raise ValueError('overlay verification differs: '+name)
        inputs[name]=(path,raw)
        records.append({'source_root':root,'manifest':'metadata/unity-shared-overlays/'+pin+'.json','sha256':pin})
    return {'schema':'openrpg.unity-shared-overlay-registry.v1','overlays':records},inputs


def publish(vault, manifests, importer, apply):
    vault=vault.resolve(strict=True)
    with importer.shared_import_lock(vault),exclusive_storage_lock(vault):
        pointer=storage.confined(vault,vault/'metadata/asset-source-support-registry.v1.json')
        models=storage.confined(vault,vault/'metadata/asset-import-registry.v1.json')
        before,model_before=pointer.read_bytes(),models.read_bytes()
        registry,inputs=prepare(vault,manifests)
        target=vault/'metadata/unity-shared-overlay-registry.v1.json'
        if target.exists():raise ValueError('Unity overlay registry already exists')
        if pointer.read_bytes()!=before or models.read_bytes()!=model_before or any(path.read_bytes()!=raw for path,raw in inputs.values()):
            raise ValueError('publication input changed')
        if not apply:return {'state':'dry_run_passed','overlays':len(registry['overlays']),'shared_files':142}
        directory=ensure_vault_directory(vault,['metadata','unity-shared-overlays'])
        for name,(path,raw) in inputs.items():
            final=directory/(storage.sha(raw)+'.json')
            if final.exists():
                if final.read_bytes()!=raw:raise ValueError('conflicting immutable overlay')
            else:
                with final.open('xb') as stream:stream.write(raw);stream.flush();os.fsync(stream.fileno())
            if final.read_bytes()!=raw:raise ValueError('overlay readback differs')
        if pointer.read_bytes()!=before or models.read_bytes()!=model_before or target.exists():
            raise ValueError('registry inputs changed')
        content=importer.canonical_json(registry);importer.atomic_write(target,content)
        if target.read_bytes()!=content:raise ValueError('overlay registry readback differs')
        return {'state':'published','overlays':len(registry['overlays']),'shared_files':142,
                'registry_sha256':storage.sha(content),'files_removed':0}


if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__)
    for name in ('vault','gladiator','gothic','output'):p.add_argument('--'+name,type=Path,required=True)
    p.add_argument('--importer-sha256',required=True);p.add_argument('--apply',action='store_true');a=p.parse_args()
    if a.output.exists():p.error('output exists')
    result=publish(a.vault,{'gladiator':a.gladiator,'gothic':a.gothic},importer_module(a.importer_sha256),a.apply)
    with a.output.open('x',encoding='utf8') as f:json.dump(result,f,indent=2);f.write('\n')
    print(json.dumps(result))
