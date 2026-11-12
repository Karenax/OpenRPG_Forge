"""Rebuild a model prefab transition and compare every staged byte."""
import argparse
import json
from pathlib import Path
import tempfile

from retire_relinked_source_copies import contains_reference
from stage_model_prefab_relinks import prepare
from stage_source_support_relinks import norm
import vault_storage as storage


def verify(vault, candidates, audit, stage):
    stage = stage.resolve(strict=True)
    raw = (stage/'plan.json').read_bytes()
    plan = json.loads(raw)
    if plan.get('schema') != 'openrpg.model-prefab-relink-stage.v1' or plan.get('state') != 'prepared_only':
        raise ValueError('invalid transition')
    with tempfile.TemporaryDirectory(prefix='verify-prefab-', dir=stage.parent) as temporary:
        rebuilt = Path(temporary)/'stage'
        prepare(vault, candidates, audit, rebuilt)
        files = {p.relative_to(stage) for p in stage.rglob('*') if p.is_file()}
        expected = {p.relative_to(rebuilt) for p in rebuilt.rglob('*') if p.is_file()}
        if files != expected:
            raise ValueError('stage file coverage changed')
        for relative in files:
            if (stage/relative).read_bytes() != (rebuilt/relative).read_bytes():
                raise ValueError('stage differs: '+str(relative))
    targets = {norm(row['old']) for row in plan['paths']}
    for item in plan['catalogs']:
        doc = json.loads((stage/item['stage_file']).read_bytes())
        if contains_reference(doc, Path(item['new']), vault, targets):
            raise ValueError('staged catalog retains an old physical prefab reference')
    for pack in plan['packs']:
        for name in ('details.v1.json','profiles.v1.json'):
            doc = json.loads((stage/pack/name).read_bytes())
            if contains_reference(doc, vault/'metadata'/name, vault, targets):
                raise ValueError('staged model metadata retains an old physical prefab reference')
    if (stage/'plan.json').read_bytes() != raw:
        raise ValueError('plan changed')
    return {'state':'stage_verified', 'plan_sha256':storage.sha(raw),
            'registry_sha256':plan['registry_sha256'], 'source_registry_sha256':plan['source_registry_sha256'],
            'models':len(plan['changed_ids']), 'paths':len(targets), 'catalogs':len(plan['catalogs']),
            'limitations':'Metadata reconstruction and hashes only; no publication, interactive Studio or runtime validation.'}


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ('vault','candidates','audit','stage','output'):
        parser.add_argument('--'+name, type=Path, required=True)
    args = parser.parse_args()
    if args.output.exists():
        parser.error('output exists')
    result = verify(args.vault,args.candidates,args.audit,args.stage)
    with args.output.open('x',encoding='utf8') as stream:
        json.dump(result, stream, indent=2); stream.write('\n')
    print(json.dumps(result))
