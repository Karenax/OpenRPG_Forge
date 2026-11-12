"""Create immutable derived catalogs for a verified model-preview stage.

This does not alter active registries, originals, models or images. The derived
catalogs are intended as inputs to a separately qualified publication.
"""
import argparse
import json
import os
from pathlib import Path

import vault_storage as storage
from publish_source_support_relinks import importer_module
from verify_model_preview_stage import verify
from verify_cross_root_preview_stage import verify as verify_cross_root


def verify_transition(vault, stage):
    schema = json.loads((stage/'plan.json').read_bytes()).get('schema')
    if schema == 'openrpg.cross-root-preview-stage.v1':
        return verify_cross_root(vault, stage)
    return verify(vault, stage)


def materialize(vault, stage, importer):
    vault, stage = vault.resolve(strict=True), stage.resolve(strict=True)
    with importer.shared_import_lock(vault):
        result = verify_transition(vault, stage)
        plan_raw = (stage/'plan.json').read_bytes()
        plan = json.loads(plan_raw)
        created, reused = [], []
        for item in plan['catalogs']:
            old = storage.confined(vault, Path(item['old']))
            new = Path(item['new'])
            if new.parent != old.parent or not new.name.startswith('openrpg-preview-'):
                raise ValueError('derived catalog destination differs')
            raw = (stage/item['stage_file']).read_bytes()
            if storage.sha(raw) != item['new_sha256'] or storage.sha(old.read_bytes()) != item['old_sha256']:
                raise ValueError('catalog changed')
            if new.exists():
                if storage.sha(storage.confined(vault, new).read_bytes()) != item['new_sha256']:
                    raise ValueError('derived catalog conflicts')
                reused.append(str(new))
                continue
            with new.open('xb') as f:
                f.write(raw)
                f.flush()
                os.fsync(f.fileno())
            if storage.sha(storage.confined(vault, new).read_bytes()) != item['new_sha256']:
                raise ValueError('derived catalog readback failed')
            created.append(str(new))
        verify_transition(vault, stage)
        if (stage/'plan.json').read_bytes() != plan_raw:
            raise ValueError('stage changed')
        return {'state':'materialized_not_published', 'plan_sha256':storage.sha(plan_raw),
                'verified_stage':result, 'created':created, 'reused':reused,
                'limitations':'Active registry unchanged; derived catalogs are needed for staged reader testing.'}


if __name__ == '__main__':
    p = argparse.ArgumentParser(description=__doc__)
    for n in ('vault','stage','output'):
        p.add_argument('--'+n,type=Path,required=True)
    p.add_argument('--importer-sha256', required=True)
    a = p.parse_args()
    if a.output.exists():p.error('output exists')
    result = materialize(a.vault, a.stage, importer_module(a.importer_sha256))
    with a.output.open('x',encoding='utf8') as f:
        json.dump(result,f,ensure_ascii=False,indent=2)
        f.write('\n')
    print(json.dumps({'state':result['state'],'created':len(result['created']),'reused':len(result['reused'])}))
