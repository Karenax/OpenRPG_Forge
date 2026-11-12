"""Verify that a staged catalog redirect changes only source catalog pointers."""
import argparse
import json
from pathlib import Path

import vault_storage as storage
from prepare_variant_metadata_transition import encoded


def verify(vault, stage):
    vault,stage=vault.resolve(strict=True),stage.resolve(strict=True)
    plan_raw=(stage/'plan.json').read_bytes()
    plan=json.loads(plan_raw)
    if plan.get('schema')!='openrpg.model-catalog-redirect-stage.v1' or plan.get('state')!='prepared_only':
        raise ValueError('invalid catalog redirect stage')
    reg=storage.confined(vault,vault/'metadata/asset-import-registry.v1.json')
    support=storage.confined(vault,vault/'metadata/asset-source-support-registry.v1.json')
    before,support_before=reg.read_bytes(),support.read_bytes()
    if storage.sha(before)!=plan['registry_sha256'] or storage.sha(support_before)!=plan['source_registry_sha256']:
        raise ValueError('registries changed')
    catalogs={}
    for item in plan['catalogs']:
        old,new=storage.confined(vault,Path(item['old'])),storage.confined(vault,Path(item['new']))
        a,b=old.read_bytes(),new.read_bytes()
        if storage.sha(a)!=item['old_sha256'] or storage.sha(b)!=item['new_sha256']:
            raise ValueError('catalog hash differs')
        old_doc,new_doc=json.loads(a),json.loads(b)
        catalogs[str(old).casefold()]=(item,{x['id']:x for x in old_doc['assets']},
                                      {x['id']:x for x in new_doc['assets']})
    expected_ids=set(plan['changed_ids'])
    if len(expected_ids)!=len(plan['changed_ids']):raise ValueError('duplicate changed ID')
    registry=json.loads(before)
    found=set()
    for pack,pins in plan['packs'].items():
        entry=registry['packs'][pack];docs={}
        for key,name in (('details','details.v1.json'),('profiles','profiles.v1.json')):
            raw=storage.confined(vault,vault/entry['path']/name).read_bytes()
            staged=(stage/pack/name).read_bytes()
            if storage.sha(raw)!=entry['files'][name] or storage.sha(staged)!=pins[name]:
                raise ValueError('snapshot hash differs')
            expected=json.loads(raw)
            if key=='details':
                for card in expected['assets']:
                    if card['id'] not in expected_ids:continue
                    item,old,new=catalogs[card['source_catalog'].casefold()]
                    ident=card['id']
                    if ident not in old or ident not in new or old[ident]!=new[ident] or old[ident].get('sha256')!=card.get('model_sha256'):
                        raise ValueError('catalog entry differs')
                    if 'source_catalog_preview_origin' in card:raise ValueError('origin metadata already present')
                    card['source_catalog_preview_origin']={'path':item['old'],'sha256':item['old_sha256']}
                    card['source_catalog']=item['new']
                    found.add(ident)
            if expected!=json.loads(staged):raise ValueError('snapshot changes additional metadata')
            docs[key]=expected
        if storage.sha(encoded({'profiles':docs['profiles'],'details':docs['details']}))!=pins['revision']:
            raise ValueError('snapshot revision differs')
    if found!=expected_ids:raise ValueError('redirect identity coverage differs')
    if reg.read_bytes()!=before or support.read_bytes()!=support_before or (stage/'plan.json').read_bytes()!=plan_raw:
        raise ValueError('inputs changed')
    return {'state':'stage_verified','models':len(found),'packs':len(plan['packs']),
            'catalogs':len(catalogs),'plan_sha256':storage.sha(plan_raw),
            'limitations':'No publication, external references or native reader checked.'}


if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__)
    for n in ('vault','stage','output'):p.add_argument('--'+n,type=Path,required=True)
    a=p.parse_args();result=verify(a.vault,a.stage)
    with a.output.open('x',encoding='utf8') as f:
        json.dump(result,f,ensure_ascii=False,indent=2);f.write('\n')
    print(json.dumps(result))
