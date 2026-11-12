"""Prepare unreferenced matching Unity .meta companions for guarded retirement."""
import argparse
import json
from pathlib import Path
import vault_storage as storage
from stage_source_support_relinks import digest_file,norm


def prepare(vault,stage,comparison,refs):
    vault=vault.resolve(strict=True)
    stage_raw=(stage/'plan.json').read_bytes();base=json.loads(stage_raw)
    comp_raw=comparison.read_bytes();comp=json.loads(comp_raw)
    reference=json.loads(refs.read_bytes())
    if reference['source_sha256']!=storage.sha(comp_raw):raise ValueError('reference audit differs')
    source=storage.confined(vault,vault/'metadata/asset-source-support-registry.v1.json')
    models=storage.confined(vault,vault/'metadata/asset-import-registry.v1.json')
    source_raw,model_raw=source.read_bytes(),models.read_bytes()
    if (json.loads(source_raw)['sha256']!=base['staged_catalog_sha256']
            or storage.sha(model_raw)!=base['asset_registry_sha256']):raise ValueError('expected mesh relink is not active')
    candidates={norm(x['old']):x for x in base['paths']};mapping={}
    for row in comp['results']:
        if row['state']!='identical_dependencies':continue
        for pair in row['dependencies']:
            old,keep=pair['hdrp'],pair['urp']
            if norm(old) not in candidates or candidates[norm(old)]['retained']!=keep:
                raise ValueError('mesh mapping differs')
            if not pair['same_meta'] or pair['hdrp_info']['meta_sha256']!=pair['urp_info']['meta_sha256']:
                raise ValueError('meta bytes differ')
            old_meta,keep_meta=old+'.meta',keep+'.meta'
            if reference['paths'].get(old_meta)!=[]:raise ValueError('meta has active references')
            pin=pair['hdrp_info']['meta_sha256']
            for rel in (old_meta,keep_meta):
                norm(rel)
                if digest_file(storage.confined(vault,vault/rel))!=pin:raise ValueError('meta SHA changed')
            value={'old':old_meta,'retained':keep_meta,'sha256':pin,'references':0}
            if old_meta in mapping and mapping[old_meta]!=value:raise ValueError('ambiguous meta mapping')
            mapping[old_meta]=value
    if len(mapping)!=len(candidates):raise ValueError('meta coverage differs')
    if source.read_bytes()!=source_raw or models.read_bytes()!=model_raw:raise ValueError('registry changed')
    return {'schema':'openrpg.source-support-relinks-stage.v1','state':'prepared_only',
        'purpose':'unreferenced_identical_mesh_meta_companions','parent_plan_sha256':storage.sha(stage_raw),
        'source_registry_sha256':storage.sha(source_raw),'asset_registry_sha256':storage.sha(model_raw),
        'staged_catalog_sha256':base['staged_catalog_sha256'],'changed_ids':base['changed_ids'],
        'paths':sorted(mapping.values(),key=lambda x:x['old'].casefold()),
        'limitations':'Active references and native reader must be checked before retirement; Unity isolated copies may need restoration.'}


if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__)
    for name in ('vault','stage','comparison','refs','output'):p.add_argument('--'+name,type=Path,required=True)
    a=p.parse_args();result=prepare(a.vault,a.stage,a.comparison,a.refs)
    with a.output.open('x',encoding='utf8') as f:json.dump(result,f,ensure_ascii=False,indent=2);f.write('\n')
    print(json.dumps({'state':result['state'],'paths':len(result['paths'])}))
