"""Materialize verified model preview snapshots without changing the registry."""
import argparse
import json
import os
from pathlib import Path

import vault_storage as storage
from publish_model_preview_relinks import load_importer
from publish_variant_transition import ensure_vault_directory, exclusive_storage_lock
from verify_model_preview_stage import verify
from verify_model_catalog_redirects import verify as verify_redirects
from verify_cross_root_preview_stage import verify as verify_cross_root


def verify_transition(vault, stage):
    schema = json.loads((stage/'plan.json').read_bytes()).get('schema')
    if schema == 'openrpg.model-preview-relink-stage.v1':
        return verify(vault,stage)
    if schema == 'openrpg.model-catalog-redirect-stage.v1':
        return verify_redirects(vault,stage)
    if schema == 'openrpg.cross-root-preview-stage.v1':
        return verify_cross_root(vault,stage)
    raise ValueError('unsupported transition schema')


def materialize(vault, stage, importer):
    vault, stage = vault.resolve(strict=True), stage.resolve(strict=True)
    with importer.locked(vault), exclusive_storage_lock(vault):
        verified = verify_transition(vault, stage)
        plan_raw = (stage/'plan.json').read_bytes()
        plan = json.loads(plan_raw)
        created, reused = [], []
        for pack, pins in sorted(plan['packs'].items()):
            importer.safe_id(pack)
            parent = ensure_vault_directory(vault,['metadata','asset-import-revisions',pack])
            target = parent/pins['revision']
            if not target.exists():
                target.mkdir()
            storage.confined(vault,target)
            for name in ('details.v1.json','profiles.v1.json'):
                raw = (stage/pack/name).read_bytes()
                if storage.sha(raw) != pins[name]:
                    raise ValueError('staged snapshot changed')
                dest = target/name
                if dest.exists():
                    if storage.sha(storage.confined(vault,dest).read_bytes()) != pins[name]:
                        raise ValueError('snapshot file conflicts')
                    reused.append(str(dest))
                    continue
                with dest.open('xb') as f:
                    f.write(raw)
                    f.flush()
                    os.fsync(f.fileno())
                if storage.sha(storage.confined(vault,dest).read_bytes()) != pins[name]:
                    raise ValueError('snapshot readback failed')
                created.append(str(dest))
        verify_transition(vault, stage)
        if (stage/'plan.json').read_bytes() != plan_raw:
            raise ValueError('stage changed')
        return {'state':'materialized_not_published','plan_sha256':storage.sha(plan_raw),
                'verified_stage':verified,'created':created,'reused':reused}


if __name__ == '__main__':
    p = argparse.ArgumentParser(description=__doc__)
    for name in ('vault','stage','output'):
        p.add_argument('--'+name,type=Path,required=True)
    p.add_argument('--importer-sha256',required=True)
    a = p.parse_args()
    if a.output.exists():p.error('output exists')
    result = materialize(a.vault,a.stage,load_importer(a.importer_sha256))
    with a.output.open('x',encoding='utf8') as f:
        json.dump(result,f,ensure_ascii=False,indent=2)
        f.write('\n')
    print(json.dumps({'state':result['state'],'created':len(result['created']),'reused':len(result['reused'])}))
