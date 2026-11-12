"""Materialize verified Viking catalog snapshots without changing registries."""
import argparse
import json
import os
from pathlib import Path

import vault_storage as storage
from publish_model_preview_relinks import load_importer
from publish_variant_transition import ensure_vault_directory,exclusive_storage_lock
from verify_viking_fbx_relinks import verify


def materialize(vault,candidates,audit,stage,rebuilt,importer):
    vault=vault.resolve(strict=True);stage=stage.resolve(strict=True)
    with importer.locked(vault),exclusive_storage_lock(vault):
        proof=verify(vault,candidates,audit,stage,rebuilt)
        plan_raw=(stage/'plan.json').read_bytes();plan=json.loads(plan_raw)
        created=[];reused=[]
        def write(path,raw,pin):
            if storage.sha(raw)!=pin:raise ValueError('stage file hash differs')
            if path.exists():
                if storage.sha(storage.confined(vault,path).read_bytes())!=pin:raise ValueError('existing output differs')
                reused.append(str(path));return
            with path.open('xb') as f:f.write(raw);f.flush();os.fsync(f.fileno())
            if storage.sha(storage.confined(vault,path).read_bytes())!=pin:raise ValueError('materialized file differs')
            created.append(str(path))
        for record in plan['catalogs']:
            old=storage.confined(vault,Path(record['old']));new=Path(record['new'])
            if new.parent!=old.parent or new.name!='openrpg-prefab-'+record['new_sha256']+'.json':
                raise ValueError('invalid derived catalog destination')
            write(new,(stage/record['stage_file']).read_bytes(),record['new_sha256'])
        for pack,pins in plan['packs'].items():
            importer.safe_id(pack)
            parent=ensure_vault_directory(vault,['metadata','asset-import-revisions',pack]);dest=parent/pins['revision']
            if not dest.exists():dest.mkdir()
            storage.confined(vault,dest)
            for name in ('details.v1.json','profiles.v1.json'):
                write(dest/name,(stage/pack/name).read_bytes(),pins[name])
        if (stage/'plan.json').read_bytes()!=plan_raw:raise ValueError('plan changed')
        return {'state':'materialized_not_published','plan_sha256':storage.sha(plan_raw),
                'stage_verification':proof,'created':created,'reused':reused}


if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__)
    for name in ('vault','candidates','audit','stage','rebuilt','output'):p.add_argument('--'+name,type=Path,required=True)
    p.add_argument('--importer-sha256',required=True);a=p.parse_args()
    if a.output.exists():p.error('output exists')
    result=materialize(a.vault,a.candidates,a.audit,a.stage,a.rebuilt,load_importer(a.importer_sha256))
    with a.output.open('x',encoding='utf8') as f:json.dump(result,f,indent=2);f.write('\n')
    print(json.dumps({'state':result['state'],'created':len(result['created']),'reused':len(result['reused'])}))
