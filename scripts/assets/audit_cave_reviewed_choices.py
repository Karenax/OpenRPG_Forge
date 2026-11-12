"""Triage Cave pairs with matching oriented triangles and source declarations.

Selecting the reviewed main conversion can change emission or back-face
appearance. Preserve the alternative bytes and references; visual parity is
not established by this audit.
"""
import argparse
import json
from pathlib import Path
import vault_storage as storage
from audit_witch_source_choices import triangle_positions


def check_metadata(main,variant,a,b):
    if not main.get('license') or main['license']!=variant.get('license'):
        raise ValueError('license differs or missing')
    if (main.get('missing') or variant.get('missing')
            or main.get('readiness',{}).get('metadata_ready') is not True
            or variant.get('readiness',{}).get('metadata_ready') is not True):
        raise ValueError('catalogue metadata incomplete')
    for card,entry in ((main,a),(variant,b)):
        if (entry.get('id')!=card.get('id') or entry.get('sha256')!=card.get('model_sha256')
                or entry.get('geometry_roundtrip_passed') is not True):
            raise ValueError('catalogue model or roundtrip evidence differs')
    if (not a.get('source_guid') or a['source_guid']!=b.get('source_guid')
            or not storage.valid_hex(a.get('source_sha256'),64) or a['source_sha256']!=b.get('source_sha256')):
        raise ValueError('declared source identity differs')
    if a.get('material_review_pending') is not False:
        raise ValueError('material review does not favor primary')
    ma,mb=main.get('measurement',{}),variant.get('measurement',{})
    for key in ('triangles',):
        if type(ma.get(key)) is not int or ma[key]<=0 or ma[key]!=mb.get(key):
            raise ValueError('geometry count differs')
    return {'keep_side':'main','preferred_controls':['reviewed_material'],
            'source_guid':a['source_guid'],'source_prefab_sha256':a['source_sha256'],
            'limitations':'Source SHA is declared, not reread. Emission, back faces and rendering may differ.'}


def audit(vault,source,candidates):
    vault=vault.resolve(strict=True);src=source.read_bytes();cand=candidates.read_bytes()
    inv,doc=json.loads(src),json.loads(cand);regpath=storage.confined(vault,vault/'metadata/asset-import-registry.v1.json');reg=regpath.read_bytes()
    if (storage.sha(reg)!=inv['registry_sha256'] or doc.get('mode')!='cave-candidates'
            or doc['source_sha256']!=storage.sha(src) or doc['registry_sha256']!=inv['registry_sha256']):
        raise ValueError('registry or inventory changed')
    assets={}
    for entry in json.loads(reg)['packs'].values():
        p=storage.confined(vault,vault/entry['path']/'details.v1.json');raw=p.read_bytes()
        if storage.sha(raw)!=entry['files']['details.v1.json']:raise ValueError('active metadata changed')
        for item in json.loads(raw)['assets']:
            if item['id'] in assets:raise ValueError('duplicate identity')
            assets[item['id']]=item
    eligible={x['variant_id']:x for x in doc['candidates']
              if x['main']['storage']==x['variant']['storage']=='descriptor'
              and x['main']['declared_pin_matches'] and x['variant']['declared_pin_matches']
              and not x['variant_has_aliases'] and all(x[k]['logical_bytes']<=512*1024*1024 for k in ('main','variant'))}
    catalogs={};results=[]
    for row in inv['pairs']:
        if row['variant_id'] not in eligible:continue
        result={'main_id':row['main_id'],'variant_id':row['variant_id']}
        try:
            main,variant=(assets[row[k+'_id']] for k in ('main','variant'))
            for k,item in (('main',main),('variant',variant)):
                if item.get('model_path')!=row[k+'_path'] or item.get('model_sha256')!=row[k+'_sha']:
                    raise ValueError('active model pin differs')
            entries=[];pins=[]
            for item in (main,variant):
                path=storage.confined(vault,Path(item['source_catalog']))
                if path not in catalogs:
                    raw=path.read_bytes();catalogs[path]=(storage.sha(raw),{x['id']:x for x in json.loads(raw)['assets']})
                pin,index=catalogs[path];entries.append(index.get(item['id'],{}));pins.append(pin)
            choice=check_metadata(main,variant,*entries)
            if row.get('transforms_exact') != 'True':raise ValueError('transforms differ')
            left,right=(triangle_positions(b''.join(storage.chunks(vault,storage.confined(vault,Path(row[k+'_path']))))) for k in ('main','variant'))
            if left != right:raise ValueError('oriented triangles differ')
            choice['catalog_sha256']=pins;result.update(state='candidate_only',**choice)
        except (OSError,ValueError,KeyError,TypeError) as error:
            result.update(state='review_required',reason=str(error))
        results.append(result)
    if regpath.read_bytes()!=reg or source.read_bytes()!=src or candidates.read_bytes()!=cand:
        raise ValueError('registry or inventory changed')
    for path,(pin,_) in catalogs.items():
        if storage.sha(path.read_bytes())!=pin:raise ValueError('source catalogue changed')
    return {'schema':'openrpg.material-choice-audit.v1','state':'audit_only','registry_sha256':inv['registry_sha256'],
            'source_sha256':storage.sha(src),'candidate_inventory_sha256':storage.sha(cand),
            'pack':'modular-cave','offset':0,'results':results,
            'limitations':'Geometry and declared source identity match; material appearance can differ.'}


if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__)
    p.add_argument('--vault',type=Path,required=True);p.add_argument('--source',type=Path,required=True)
    p.add_argument('--candidates',type=Path,required=True);p.add_argument('--output',type=Path,required=True)
    a=p.parse_args()
    if a.output.exists():p.error('output exists')
    result=audit(a.vault,a.source,a.candidates)
    with a.output.open('x',encoding='utf8',newline='\n') as f:json.dump(result,f,ensure_ascii=False,indent=2);f.write('\n')
    print(json.dumps({'count':len(result['results']),'candidates':sum(x['state']=='candidate_only' for x in result['results'])}))
