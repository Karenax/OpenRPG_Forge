"""Select the previously delivered Witch House 2026-11-08 corrected models.

This consolidates identities, not visual equivalence. The 51-model delivery
and its Studio checks are recorded separately; these checks preserve the old
model bytes and metadata and do not certify every render or exported game.
"""
import argparse
import hashlib
import json
import re
from pathlib import Path

import vault_storage as storage


def audit(vault,source,candidates):
    vault=vault.resolve(strict=True);src=source.read_bytes();cand=candidates.read_bytes();inv,doc=json.loads(src),json.loads(cand)
    index=storage.confined(vault,vault/'metadata/asset-import-registry.v1.json');before=index.read_bytes()
    if (storage.sha(before)!=inv['registry_sha256'] or doc.get('mode')!='witch-corrected'
            or doc['source_sha256']!=storage.sha(src) or doc['registry_sha256']!=inv['registry_sha256']):
        raise ValueError('registry or inventory changed')
    cards={}
    for entry in json.loads(before)['packs'].values():
        p=storage.confined(vault,vault/entry['path']/'details.v1.json');raw=p.read_bytes()
        if storage.sha(raw)!=entry['files']['details.v1.json']:raise ValueError('active metadata changed')
        for c in json.loads(raw)['assets']:
            if c['id'] in cards:raise ValueError('duplicate identity')
            cards[c['id']]=c
    eligible={x['variant_id']:x for x in doc['candidates'] if x['main']['storage']=='descriptor'
              and x['main']['declared_pin_matches'] and not x['main_has_aliases']
              and x['variant']['storage'] in ('raw','descriptor')
              and (x['variant']['storage']=='raw' or x['variant']['declared_pin_matches'])
              and all(x[k]['logical_bytes']<=512*1024*1024 for k in ('main','variant'))}
    catalogs={};results=[]
    for row in inv['pairs']:
        if row['variant_id'] not in eligible:continue
        result={'main_id':row['main_id'],'variant_id':row['variant_id']}
        try:
            main,corrected=(cards[row[k+'_id']] for k in ('main','variant'))
            if not main.get('license') or main['license']!=corrected.get('license'):
                raise ValueError('license differs')
            if re.sub(r'\.variant-[a-f0-9]+(?=\.)','',corrected['id'],count=1)!=main['id']:
                raise ValueError('asset identity differs')
            for k,card in (('main',main),('variant',corrected)):
                if card.get('model_path')!=row[k+'_path'] or card.get('model_sha256')!=row[k+'_sha']:
                    raise ValueError('model pin changed')
            path=storage.confined(vault,Path(corrected['source_catalog']))
            if path.parent != vault/'witch-house-urp-20261108-r2' or path.name not in ('catalog-update31.json','catalog-new20.json'):
                raise ValueError('outside corrected delivery catalogs')
            if path not in catalogs:
                raw=path.read_bytes();catalogs[path]=(storage.sha(raw),{x['id']:x for x in json.loads(raw)['assets']})
            pin,items=catalogs[path];entry=items[corrected['id']]
            if entry.get('sha256')!=row['variant_sha'] or entry.get('id')!=corrected['id']:
                raise ValueError('delivery catalog pin mismatch')
            support=entry.get('source_support_provenance') or {}
            guid=re.search(r'-([0-9a-f]{32})(?:-lod\d+)?$',corrected['id'],re.I)
            if not guid or support.get('source_guid')!=guid.group(1) or not storage.valid_hex(support.get('source_sha256'),64):
                raise ValueError('corrected prefab source identity missing')
            p=storage.confined(vault,vault/support['source_file'])
            if hashlib.sha256(p.read_bytes()).hexdigest()!=support['source_sha256']:
                raise ValueError('prefab source SHA mismatch')
            result.update(state='candidate_only',keep_side='variant',preferred_controls=['corrected_witch_delivery'],
                          source_guid=support['source_guid'],source_prefab_sha256=support['source_sha256'],
                          corrected_catalog_sha256=pin,
                          limitations='Corrected delivery selected; graph, materials, transforms and qualification may differ. Legacy bytes preserved.')
        except (OSError,ValueError,KeyError,TypeError) as e:result.update(state='review_required',reason=str(e))
        results.append(result)
    if index.read_bytes()!=before or source.read_bytes()!=src or candidates.read_bytes()!=cand:raise ValueError('inputs changed')
    for p,(pin,_) in catalogs.items():
        if storage.sha(p.read_bytes())!=pin:raise ValueError('delivery catalog changed')
    return {'schema':'openrpg.material-choice-audit.v1','state':'audit_only','registry_sha256':inv['registry_sha256'],
            'source_sha256':storage.sha(src),'candidate_inventory_sha256':storage.sha(cand),
            'pack':'witch-house','offset':0,'results':results,
            'limitations':'Corrected delivery selection only; no new visual or runtime qualification.'}


if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__)
    for n in ('vault','source','candidates','output'):p.add_argument('--'+n,type=Path,required=True)
    a=p.parse_args()
    if a.output.exists():p.error('output exists')
    result=audit(a.vault,a.source,a.candidates)
    with a.output.open('x',encoding='utf8') as f:json.dump(result,f,ensure_ascii=False,indent=2);f.write('\n')
    print(json.dumps({'count':len(result['results']),'candidates':sum(x['state']=='candidate_only' for x in result['results'])}))
