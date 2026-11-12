"""Materialize verified candidate catalogs and snapshots; do not publish."""
import argparse
import json
import os
from pathlib import Path
import vault_storage as storage
from publish_model_preview_relinks import load_importer
from publish_variant_transition import ensure_vault_directory,exclusive_storage_lock
from verify_nested_archive_catalog_relinks import verify


def materialize(vault,audit,recovery,stage,importer):
    vault=vault.resolve(strict=True);stage=stage.resolve(strict=True)
    with importer.locked(vault),exclusive_storage_lock(vault):
        proof=verify(vault,audit,recovery,stage);plan_raw=(stage/'plan.json').read_bytes();plan=json.loads(plan_raw)
        made=[];reused=[]
        def write_checked(path,raw,pin):
            if storage.sha(raw)!=pin:raise ValueError('stage bytes changed')
            if path.exists():
                if storage.sha(storage.confined(vault,path).read_bytes())!=pin:raise ValueError('existing file differs')
                reused.append(str(path));return
            with path.open('xb') as f:f.write(raw);f.flush();os.fsync(f.fileno())
            if storage.sha(storage.confined(vault,path).read_bytes())!=pin:raise ValueError('readback differs')
            made.append(str(path))
        for row in plan['catalogs']:
            old=storage.confined(vault,Path(row['old']));new=Path(row['new'])
            if new.parent!=old.parent or new.name!='openrpg-archive-'+row['new_sha256']+'.json':raise ValueError('invalid derived catalog path')
            write_checked(new,(stage/row['stage_file']).read_bytes(),row['new_sha256'])
        for pack,pins in plan['packs'].items():
            importer.safe_id(pack);parent=ensure_vault_directory(vault,['metadata','asset-import-revisions',pack]);dest=parent/pins['revision']
            if not dest.exists():dest.mkdir()
            storage.confined(vault,dest)
            for name in ('details.v1.json','profiles.v1.json'):write_checked(dest/name,(stage/pack/name).read_bytes(),pins[name])
        if (stage/'plan.json').read_bytes()!=plan_raw:raise ValueError('plan changed')
        return {'state':'materialized_not_published','stage_proof_sha256':storage.sha(encoded_json(proof)),
                'plan_sha256':storage.sha(plan_raw),'created':made,'reused':reused,
                'limitations':'Active registry unchanged; no reader or runtime qualification.'}


def encoded_json(value):
    return json.dumps(value,sort_keys=True,separators=(',',':')).encode()


if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__)
    for name in ('vault','audit','recovery','stage','output'):p.add_argument('--'+name,type=Path,required=True)
    p.add_argument('--importer-sha256',required=True);a=p.parse_args()
    if a.output.exists():p.error('output exists')
    result=materialize(a.vault,a.audit,a.recovery,a.stage,load_importer(a.importer_sha256))
    with a.output.open('x',encoding='utf8') as f:json.dump(result,f,indent=2);f.write('\n')
    print(json.dumps({'state':result['state'],'created':len(result['created']),'reused':len(result['reused'])}))
