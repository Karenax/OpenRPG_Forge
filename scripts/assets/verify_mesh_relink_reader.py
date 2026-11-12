"""Verify remapped source mesh dependencies under a staged or published revision."""
import argparse
import json
from pathlib import Path
import vault_storage as storage
from stage_source_support_relinks import digest_file,norm


def verify(vault,stage,published=False):
    vault,stage=vault.resolve(strict=True),stage.resolve(strict=True)
    plan_raw=(stage/'plan.json').read_bytes();plan=json.loads(plan_raw)
    if plan.get('schema')!='openrpg.source-support-relinks-stage.v1':raise ValueError('wrong plan')
    source_pointer=storage.confined(vault,vault/'metadata/asset-source-support-registry.v1.json')
    model_pointer=storage.confined(vault,vault/'metadata/asset-import-registry.v1.json')
    before,models=source_pointer.read_bytes(),model_pointer.read_bytes()
    pin=json.loads(before)
    catalog=(stage/'source-support.v1.json').read_bytes()
    if storage.sha(catalog)!=plan['staged_catalog_sha256']:raise ValueError('staged catalog changed')
    if published and pin['sha256']!=plan['staged_catalog_sha256']:raise ValueError('catalog not active')
    if not published and storage.sha(before)!=plan['source_registry_sha256']:raise ValueError('source registry changed')
    if storage.sha(models)!=plan['asset_registry_sha256']:raise ValueError('model registry changed')
    mapping={norm(x['old']):x for x in plan['paths']};counts={k:0 for k in mapping}
    kept_cache={};changed=set(plan['changed_ids']);found=set()
    for card in json.loads(catalog)['assets']:
        if card['id'] not in changed:continue
        found.add(card['id'])
        files=card.get('source_mesh_files')
        if files is None and card.get('source_mesh_file') is not None:
            files=[card['source_mesh_file']]
        if not isinstance(files,list):raise ValueError('mesh list absent')
        for rel in files:
            if norm(rel) in mapping:raise ValueError('old mesh still referenced')
            for key,item in mapping.items():
                if norm(item['retained'])==norm(rel):
                    path=storage.confined(vault,vault/rel)
                    if path not in kept_cache:kept_cache[path]=digest_file(path)
                    if kept_cache[path]!=item['sha256']:raise ValueError('retained mesh SHA changed')
                    counts[key]+=1
    if found!=changed or any(counts[norm(x['old'])]!=x['references'] for x in plan['paths']):
        raise ValueError('mesh or changed-card coverage differs')
    if source_pointer.read_bytes()!=before or model_pointer.read_bytes()!=models or (stage/'plan.json').read_bytes()!=plan_raw:
        raise ValueError('inputs changed')
    return {'state':'passed','published':published,'stage_plan_sha256':storage.sha(plan_raw),
            'source_registry_sha256':storage.sha(before),'model_registry_sha256':storage.sha(models),
            'changed_cards':len(found),'retained_meshes_verified':len(kept_cache),'references':sum(counts.values()),
            'limitations':'Mesh paths and hashes only; Unity project import and external consumers are unverified.'}


if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__)
    for name in ('vault','stage','output'):p.add_argument('--'+name,type=Path,required=True)
    p.add_argument('--published',action='store_true');a=p.parse_args()
    result=verify(a.vault,a.stage,a.published)
    with a.output.open('x',encoding='utf8') as f:json.dump(result,f,indent=2);f.write('\n')
    print(json.dumps(result))
