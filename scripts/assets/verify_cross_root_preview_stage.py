"""Verify every metadata difference in a prepared cross-root preview stage."""
import argparse
import copy
import json
from pathlib import Path

import vault_storage as storage
from prepare_variant_metadata_transition import encoded


def verify(vault,stage):
    vault,stage=vault.resolve(strict=True),stage.resolve(strict=True)
    plan_raw=(stage/'plan.json').read_bytes();plan=json.loads(plan_raw)
    if plan.get('schema')!='openrpg.cross-root-preview-stage.v1' or plan.get('state')!='prepared_only':
        raise ValueError('invalid cross-root stage')
    reg=storage.confined(vault,vault/'metadata/asset-import-registry.v1.json')
    support=storage.confined(vault,vault/'metadata/asset-source-support-registry.v1.json')
    before,sb=reg.read_bytes(),support.read_bytes()
    if storage.sha(before)!=plan['registry_sha256'] or storage.sha(sb)!=plan['source_registry_sha256']:
        raise ValueError('registry changed')
    candidates={}
    for pair in plan['candidate_paths']:
        old,keep=Path(pair['old']),Path(pair['retained'])
        if not old.is_absolute() or not keep.is_absolute() or not old.is_relative_to(vault) or not keep.is_relative_to(vault):
            raise ValueError('preview outside Vault')
        for ident in pair['ids']:
            if ident in candidates:raise ValueError('duplicate preview ID')
            candidates[ident]=pair
    if set(candidates)!=set(plan['preview_ids']):raise ValueError('preview ID coverage differs')
    catalogs={};seen_preview=set()
    for row in plan['catalogs']:
        old=storage.confined(vault,Path(row['old']));before_cat=old.read_bytes()
        staged=(stage/row['stage_file']).read_bytes()
        if storage.sha(before_cat)!=row['old_sha256'] or storage.sha(staged)!=row['new_sha256']:
            raise ValueError('catalog SHA changed')
        expected=copy.deepcopy(json.loads(before_cat))
        for item in expected['assets']:
            if item['id'] not in candidates:continue
            pair=candidates[item['id']]
            if item.get('preview_sha256')!=pair['sha256'] or 'preview_vault' in item:
                raise ValueError('preview metadata differs')
            item['preview_vault']=Path(pair['retained']).relative_to(vault).as_posix()
            seen_preview.add(item['id'])
        if expected!=json.loads(staged):raise ValueError('derived catalog changes additional data')
        if Path(row['new']).parent!=old.parent or not Path(row['new']).name.endswith(row['new_sha256']+'.json'):
            raise ValueError('derived catalog destination differs')
        catalogs[str(old).casefold()]=row
    if seen_preview!=set(candidates):raise ValueError('catalog preview coverage differs')
    current=json.loads(before);changed=set();expected_ids=set(plan['changed_ids'])
    if len(expected_ids)!=len(plan['changed_ids']):raise ValueError('duplicate changed ID')
    for pack,pins in plan['packs'].items():
        entry=current['packs'][pack];docs={}
        for key,name in (('details','details.v1.json'),('profiles','profiles.v1.json')):
            original=storage.confined(vault,vault/entry['path']/name).read_bytes();staged=(stage/pack/name).read_bytes()
            if storage.sha(original)!=entry['files'][name] or storage.sha(staged)!=pins[name]:raise ValueError('snapshot SHA changed')
            expected=json.loads(original)
            if key=='details':
                for card in expected['assets']:
                    if card['id'] not in expected_ids:continue
                    item=catalogs[card['source_catalog'].casefold()]
                    origin=card.get('source_catalog_preview_origin')
                    if origin is None:card['source_catalog_preview_origin']={'path':item['old'],'sha256':item['old_sha256']}
                    elif not isinstance(origin,dict) or set(origin)!={'path','sha256'}:raise ValueError('invalid existing origin')
                    card['source_catalog']=item['new'];changed.add(card['id'])
            if expected!=json.loads(staged):raise ValueError('snapshot changes additional data')
            docs[key]=expected
        if storage.sha(encoded({'profiles':docs['profiles'],'details':docs['details']}))!=pins['revision']:
            raise ValueError('snapshot revision differs')
    if changed!=expected_ids:raise ValueError('changed ID coverage differs')
    if reg.read_bytes()!=before or support.read_bytes()!=sb or (stage/'plan.json').read_bytes()!=plan_raw:
        raise ValueError('inputs changed')
    return {'state':'stage_verified','paths':len(plan['candidate_paths']),'preview_models':len(candidates),
            'redirected_models':len(changed),'catalogs':len(catalogs),'packs':len(plan['packs']),
            'plan_sha256':storage.sha(plan_raw),'limitations':'Reader deployment and publication not qualified.'}


if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__)
    for n in ('vault','stage','output'):p.add_argument('--'+n,type=Path,required=True)
    a=p.parse_args();result=verify(a.vault,a.stage)
    with a.output.open('x',encoding='utf8') as f:json.dump(result,f,indent=2);f.write('\n')
    print(json.dumps(result))
