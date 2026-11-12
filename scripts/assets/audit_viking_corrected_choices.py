"""Select the three previously published Viking frame-corrected houses.

The corrected models remain unqualified for Studio placement/runtime. Their
historical variants are preserved by exact logical-byte reconstruction.
"""
import argparse
import hashlib
import json
import math
from pathlib import Path
import vault_storage as storage

PINS={'001':'6a6d29cbb82d3ef2f5b9f2a42ce455427d26a25062e2f081a9a6fe43d52ec318',
      '002':'8e5278feef67253a897e0a39cd1221f4074a265f21642876e3ed7baa93dbb078',
      '003':'f214da3ab6e5760c4bc1f0c3c6acf372c53eb80f23d112d5022944c8a6eef91b'}


def check(main,old,vault):
    ids=[x for x in PINS if '.prefab-sm-housebuilding-'+x+'-a-' in main.get('id','')]
    if len(ids)!=1 or main.get('model_sha256')!=PINS[ids[0]]:raise ValueError('corrected model pin differs')
    if not main.get('license') or main['license']!=old.get('license'):raise ValueError('license differs')
    left,right=main.get('source_prefab'),old.get('source_prefab')
    if not (isinstance(left,list) and len(left)==1 and isinstance(right,list) and len(right)==1):
        raise ValueError('source prefab missing')
    x,y=left[0],right[0]
    if not storage.valid_hex(x.get('sha256'),64) or x['sha256']!=y.get('sha256'):
        raise ValueError('prefab source SHA differs')
    p=storage.confined(vault,Path(old['source_catalog']).parent/y['path'])
    if hashlib.sha256(p.read_bytes()).hexdigest()!=y['sha256']:raise ValueError('prefab source SHA mismatch')
    ma,mb=main.get('measurement',{}),old.get('measurement',{});da,db=ma.get('dimensions'),mb.get('dimensions')
    if not (isinstance(da,list) and isinstance(db,list) and len(da)==len(db)==3):raise ValueError('dimensions missing')
    if any(not math.isfinite(float(a)) or not math.isfinite(float(b)) or abs(float(a)-float(b))>1e-4 for a,b in zip(da,db)):
        raise ValueError('dimensions differ')
    if type(ma.get('triangles')) is not int or ma['triangles']<=0 or ma['triangles']!=mb.get('triangles'):
        raise ValueError('triangle counts differ')
    return {'keep_side':'main','preferred_controls':['previously_corrected_frame'],
            'source_prefab_sha256':x['sha256'],
            'limitations':'Corrected frame retained; material, graph, render and runtime parity not established.'}


def audit(vault,source,candidates):
    vault=vault.resolve(strict=True);src=source.read_bytes();cand=candidates.read_bytes();inv,doc=json.loads(src),json.loads(cand)
    index=storage.confined(vault,vault/'metadata/asset-import-registry.v1.json');before=index.read_bytes()
    if (storage.sha(before)!=inv['registry_sha256'] or doc.get('mode')!='viking-corrected'
            or doc['source_sha256']!=storage.sha(src) or doc['registry_sha256']!=inv['registry_sha256']):raise ValueError('registry or inventory changed')
    cards={}
    for e in json.loads(before)['packs'].values():
        p=storage.confined(vault,vault/e['path']/'details.v1.json');raw=p.read_bytes()
        if storage.sha(raw)!=e['files']['details.v1.json']:raise ValueError('active metadata changed')
        for c in json.loads(raw)['assets']:
            if c['id'] in cards:raise ValueError('duplicate identity')
            cards[c['id']]=c
    eligible={x['variant_id'] for x in doc['candidates'] if x['main']['storage']==x['variant']['storage']=='descriptor'
              and x['main']['declared_pin_matches'] and x['variant']['declared_pin_matches'] and not x['variant_has_aliases']
              and all(x[k]['logical_bytes']<=512*1024*1024 for k in ('main','variant'))}
    results=[]
    for row in inv['pairs']:
        if row['variant_id'] not in eligible:continue
        item={'main_id':row['main_id'],'variant_id':row['variant_id']}
        try:
            main,old=(cards[row[k+'_id']] for k in ('main','variant'))
            for k,c in (('main',main),('variant',old)):
                if c.get('model_path')!=row[k+'_path'] or c.get('model_sha256')!=row[k+'_sha']:raise ValueError('model pin changed')
            if 'viking-frame-correction-batch01-v1' not in Path(row['main_path']).parts:raise ValueError('corrected model scope differs')
            item.update(state='candidate_only',**check(main,old,vault))
        except (OSError,ValueError,KeyError,TypeError) as e:item.update(state='review_required',reason=str(e))
        results.append(item)
    if index.read_bytes()!=before or source.read_bytes()!=src or candidates.read_bytes()!=cand:raise ValueError('input changed')
    return {'schema':'openrpg.material-choice-audit.v1','state':'audit_only','registry_sha256':inv['registry_sha256'],
            'source_sha256':storage.sha(src),'candidate_inventory_sha256':storage.sha(cand),'pack':'modular-viking-village',
            'offset':0,'results':results,'limitations':'No new visual or runtime qualification.'}


if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__)
    for n in ('vault','source','candidates','output'):p.add_argument('--'+n,type=Path,required=True)
    a=p.parse_args()
    if a.output.exists():p.error('output exists')
    result=audit(a.vault,a.source,a.candidates)
    with a.output.open('x',encoding='utf8') as f:json.dump(result,f,ensure_ascii=False,indent=2);f.write('\n')
    print(json.dumps({'count':len(result['results']),'candidates':sum(x['state']=='candidate_only' for x in result['results'])}))
