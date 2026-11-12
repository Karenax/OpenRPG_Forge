"""Compare Mountain Temple source-graph pairs, preserving historical bytes.

Matching source and geometry do not establish equal graph rendering; neither
conversion is runtime-qualified. This tool does not publish or remove files.
"""
import argparse
import copy
import hashlib
import json
from pathlib import Path

import compare_variant_glb_bytes as compare
import vault_storage as storage


def clean_node(node):
    result=copy.deepcopy(node)
    result.get('extras',{}).pop('asset_id',None)
    return result


def check(a,b):
    ga,gb=a['geometry'],b['geometry']
    if a['mesh_payloads']!=b['mesh_payloads'] or a['images']!=b['images']:
        raise ValueError('mesh or image payload differs')
    if (a['private_extensions']!=['OPENRPG_source_graph'] or b['private_extensions']!=['OPENRPG_source_graph']):
        raise ValueError('unexpected extension')
    if ([clean_node(n) for n in ga.get('nodes') or []] != [clean_node(n) for n in gb.get('nodes') or []]
            or ga.get('scene')!=gb.get('scene')):
        raise ValueError('node transforms or properties differ')
    def scenes(d):return [{k:v for k,v in x.items() if k!='name'} for x in d.get('scenes') or []]
    if scenes(ga)!=scenes(gb):raise ValueError('scene structure differs')
    return {'keep_side':'main','preferred_controls':['matching_mesh_and_source'],
            'limitations':'Source graph programs may differ; visual and runtime equivalence not established.'}


def audit(vault,source,candidates,max_model=512*1024*1024):
    if not 1 <= max_model <= 1024*1024*1024:
        raise ValueError('model budget must be at most one GiB')
    vault=vault.resolve(strict=True);src=source.read_bytes();cand=candidates.read_bytes();inv,doc=json.loads(src),json.loads(cand)
    index=storage.confined(vault,vault/'metadata/asset-import-registry.v1.json');before=index.read_bytes()
    if (storage.sha(before)!=inv['registry_sha256'] or doc.get('mode')!='source-graph'
            or doc['source_sha256']!=storage.sha(src) or doc['registry_sha256']!=inv['registry_sha256']):
        raise ValueError('registry or inventory changed')
    cards={}
    for entry in json.loads(before)['packs'].values():
        p=storage.confined(vault,vault/entry['path']/'details.v1.json');raw=p.read_bytes()
        if storage.sha(raw)!=entry['files']['details.v1.json']:raise ValueError('active metadata changed')
        for card in json.loads(raw)['assets']:
            if card['id'] in cards:raise ValueError('duplicate identity')
            cards[card['id']]=card
    eligible={x['variant_id']:x for x in doc['candidates'] if x['main']['storage']==x['variant']['storage']=='descriptor'
              and x['main']['declared_pin_matches'] and x['variant']['declared_pin_matches']
              and not x['variant_has_aliases']}
    catalogs={};results=[]
    for row in inv['pairs']:
        if row['variant_id'] not in eligible or '.mountain-temple.' not in row['main_id']:continue
        result={'main_id':row['main_id'],'variant_id':row['variant_id']}
        try:
            if any(eligible[row['variant_id']][k]['logical_bytes']>max_model for k in ('main','variant')):
                raise ValueError('logical model exceeds selected audit byte budget')
            entries=[];signatures=[];pins=[]
            c=[cards[row[k+'_id']] for k in ('main','variant')]
            if not c[0].get('license') or c[0]['license']!=c[1].get('license'):raise ValueError('license differs')
            for k,card in zip(('main','variant'),c):
                if card.get('model_path')!=row[k+'_path'] or card.get('model_sha256')!=row[k+'_sha']:raise ValueError('model pin changed')
                model=storage.confined(vault,Path(row[k+'_path']));raw=storage.read_bytes(vault,model,max_model)
                if hashlib.sha256(raw).hexdigest()!=row[k+'_sha']:raise ValueError('model SHA differs')
                signatures.append(compare.signature(raw))
                cat=storage.confined(vault,Path(card['source_catalog']))
                if cat not in catalogs:
                    data=cat.read_bytes();catalogs[cat]=(storage.sha(data),{x['id']:x for x in json.loads(data)['assets']})
                pin,items=catalogs[cat];pins.append(pin);entry=items[card['id']]
                if entry.get('sha256')!=row[k+'_sha']:raise ValueError('catalog pin differs')
                entries.append(entry.get('source_support_provenance') or {})
            a,b=entries
            if (not a.get('source_guid') or a.get('source_guid')!=b.get('source_guid')
                    or not storage.valid_hex(a.get('source_sha256'),64) or a['source_sha256']!=b.get('source_sha256')):
                raise ValueError('source identity differs')
            for support in entries:
                p=storage.confined(vault,vault/support['source_file'])
                if hashlib.sha256(p.read_bytes()).hexdigest()!=support['source_sha256']:raise ValueError('source SHA mismatch')
            choice=check(*signatures);choice.update(source_guid=a['source_guid'],source_prefab_sha256=a['source_sha256'],catalog_sha256=pins)
            result.update(state='candidate_only',**choice)
        except (OSError,ValueError,KeyError,TypeError,IndexError) as e:result.update(state='review_required',reason=str(e))
        results.append(result)
    if index.read_bytes()!=before or source.read_bytes()!=src or candidates.read_bytes()!=cand:raise ValueError('inputs changed')
    for path,(pin,_) in catalogs.items():
        if storage.sha(path.read_bytes())!=pin:raise ValueError('catalog changed')
    return {'schema':'openrpg.material-choice-audit.v1','state':'audit_only','registry_sha256':inv['registry_sha256'],
            'source_sha256':storage.sha(src),'candidate_inventory_sha256':storage.sha(cand),
            'pack':'mountain-temple','offset':0,'results':results,
            'limitations':'Source and geometry compared; source graph rendering not qualified.'}


if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__)
    for n in ('vault','source','candidates','output'):p.add_argument('--'+n,type=Path,required=True)
    p.add_argument('--max-model-mib', type=int, default=512, help='bounded audit budget, at most 1024 MiB')
    a=p.parse_args()
    if a.output.exists():p.error('output exists')
    if not 1 <= a.max_model_mib <= 1024:p.error('model budget must be 1..1024 MiB')
    result=audit(a.vault,a.source,a.candidates,a.max_model_mib*1024*1024)
    with a.output.open('x',encoding='utf8') as f:json.dump(result,f,ensure_ascii=False,indent=2);f.write('\n')
    print(json.dumps({'count':len(result['results']),'candidates':sum(x['state']=='candidate_only' for x in result['results'])}))
