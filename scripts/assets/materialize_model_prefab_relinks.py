"""Materialize verified prefab catalog revisions without publishing them."""
import argparse
import json
import os
from pathlib import Path

import vault_storage as storage
from publish_model_preview_relinks import load_importer
from publish_variant_transition import ensure_vault_directory, exclusive_storage_lock
from verify_model_prefab_relinks import verify


def materialize(vault, candidates, audit, stage, importer):
    vault, stage = vault.resolve(strict=True), stage.resolve(strict=True)
    with importer.locked(vault), exclusive_storage_lock(vault):
        proof = verify(vault,candidates,audit,stage)
        plan_raw = (stage/'plan.json').read_bytes(); plan = json.loads(plan_raw)
        created, reused = [], []
        def write(dest, raw, pin):
            if storage.sha(raw) != pin:
                raise ValueError('staged bytes changed')
            if dest.exists():
                if storage.sha(storage.confined(vault,dest).read_bytes()) != pin:
                    raise ValueError('existing file differs')
                reused.append(str(dest)); return
            with dest.open('xb') as stream:
                stream.write(raw); stream.flush(); os.fsync(stream.fileno())
            if storage.sha(storage.confined(vault,dest).read_bytes()) != pin:
                raise ValueError('readback failed')
            created.append(str(dest))
        for record in plan['catalogs']:
            old = storage.confined(vault,Path(record['old']))
            new = Path(record['new'])
            if new.parent != old.parent or new.name != 'openrpg-prefab-'+record['new_sha256']+'.json':
                raise ValueError('invalid derived catalog destination')
            write(new,(stage/record['stage_file']).read_bytes(),record['new_sha256'])
        for pack, pins in plan['packs'].items():
            importer.safe_id(pack)
            parent = ensure_vault_directory(vault,['metadata','asset-import-revisions',pack])
            dest = parent/pins['revision']
            if not dest.exists(): dest.mkdir()
            storage.confined(vault,dest)
            for name in ('details.v1.json','profiles.v1.json'):
                write(dest/name,(stage/pack/name).read_bytes(),pins[name])
        if (stage/'plan.json').read_bytes() != plan_raw:
            raise ValueError('plan changed')
        return {'state':'materialized_not_published','plan_sha256':storage.sha(plan_raw),
                'verified_stage':proof,'created':created,'reused':reused}


if __name__ == '__main__':
    p = argparse.ArgumentParser(description=__doc__)
    for name in ('vault','candidates','audit','stage','output'):
        p.add_argument('--'+name,type=Path,required=True)
    p.add_argument('--importer-sha256',required=True)
    a = p.parse_args()
    if a.output.exists(): p.error('output exists')
    result = materialize(a.vault,a.candidates,a.audit,a.stage,load_importer(a.importer_sha256))
    with a.output.open('x',encoding='utf8') as stream:
        json.dump(result,stream,indent=2); stream.write('\n')
    print(json.dumps({'state':result['state'],'created':len(result['created']),'reused':len(result['reused'])}))
