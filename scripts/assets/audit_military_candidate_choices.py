"""Triage older Military candidates against their registered source-face model.

The selected primary model and old candidate can differ in coordinates,
geometry and materials. This is a choice of active catalogue entry, not visual
equivalence. Historical bytes and UUID/SHA references must remain accessible.
"""
import argparse
import hashlib
import json
import math
from pathlib import Path

import vault_storage as storage


def check_metadata(main, candidate, catalog_entry, vault):
    if (main.get('readiness',{}).get('metadata_ready') is not True
            or candidate.get('readiness',{}).get('metadata_ready') is not False
            or main.get('missing') or not candidate.get('missing')):
        raise ValueError('qualification is not primary-ready and older-candidate-incomplete')
    if not main.get('license') or main['license'] != candidate.get('license'):
        raise ValueError('license differs or missing')
    source = candidate.get('source_prefab')
    if not isinstance(source,dict) or not storage.valid_hex(source.get('sha256'),64):
        raise ValueError('candidate prefab source pin missing')
    if (not source.get('guid') or source['guid'] != catalog_entry.get('source_guid')
            or catalog_entry.get('id') != main.get('id')
            or catalog_entry.get('sha256') != main.get('model_sha256')
            or catalog_entry.get('source_prefab') != main.get('source_prefab')
            or catalog_entry.get('geometry_roundtrip_passed') is not True
            or catalog_entry.get('visual_review') != 'agent_review'):
        raise ValueError('primary conversion evidence does not match source identity')
    source_path=storage.confined(vault,Path(source['path']))
    if hashlib.sha256(source_path.read_bytes()).hexdigest()!=source['sha256']:
        raise ValueError('candidate source prefab SHA mismatch')
    a,b=main.get('measurement',{}),candidate.get('measurement',{})
    da,db=a.get('dimensions'),b.get('dimensions')
    if not (isinstance(da,list) and isinstance(db,list) and len(da)==len(db)==3):
        raise ValueError('dimensions missing')
    pairs=[(float(x),float(y)) for x,y in zip(da,db)]
    if any(not math.isfinite(x) or not math.isfinite(y) or abs(x-y)>1e-4 for x,y in pairs):
        raise ValueError('dimensions differ')
    if type(a.get('triangles')) is not int or a['triangles']<=0 or a['triangles']!=b.get('triangles'):
        raise ValueError('triangle counts differ')
    # Missing collision metadata is treated as zero only for the primary;
    # the candidate must explicitly report no collision geometry.
    collision=b.get('collision_geometry')
    if not isinstance(collision,dict) or collision.get('triangles')!=0 or collision.get('mesh_instances')!=0:
        raise ValueError('candidate has collision geometry or unknown collision status')
    return {'keep_side':'main','preferred_controls':['source_face_conversion','complete_metadata'],
            'source_guid':source['guid'],'candidate_prefab_sha256':source['sha256'],
            'primary_prefab_sha256':catalog_entry.get('source_prefab_sha256'),
            'limitations':'Different source revisions, coordinate frames and materials; render and runtime equivalence not established.'}


def audit(vault, source, candidates):
    vault=vault.resolve(strict=True);src=source.read_bytes();cand=candidates.read_bytes()
    inv,doc=json.loads(src),json.loads(cand);regpath=storage.confined(vault,vault/'metadata/asset-import-registry.v1.json');reg=regpath.read_bytes()
    if (storage.sha(reg)!=inv['registry_sha256'] or doc.get('mode')!='military-candidates'
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
            main,candidate=(assets[row[k+'_id']] for k in ('main','variant'))
            for k,item in (('main',main),('variant',candidate)):
                if item.get('model_path')!=row[k+'_path'] or item.get('model_sha256')!=row[k+'_sha']:
                    raise ValueError('active model pin differs')
            for k,expected in (('main','normalized'),('variant','candidates')):
                p=storage.confined(vault,Path(row[k+'_path']))
                if p.relative_to(vault).parts[0]!=expected:raise ValueError('model outside expected scope')
            catalog_path=storage.confined(vault,Path(main['source_catalog']))
            if catalog_path not in catalogs:
                raw=catalog_path.read_bytes();obj=json.loads(raw)
                catalogs[catalog_path]=(storage.sha(raw),{x['id']:x for x in obj['assets']})
            catsha,index=catalogs[catalog_path]
            item=check_metadata(main,candidate,index.get(main['id'],{}),vault)
            item['primary_catalog_sha256']=catsha
            result.update(state='candidate_only',**item)
        except (OSError,ValueError,KeyError,TypeError) as error:
            result.update(state='review_required',reason=str(error))
        results.append(result)
    if regpath.read_bytes()!=reg or source.read_bytes()!=src or candidates.read_bytes()!=cand:
        raise ValueError('registry or inventory changed')
    for p,(expected,_) in catalogs.items():
        if storage.sha(p.read_bytes())!=expected:raise ValueError('primary source catalog changed')
    return {'schema':'openrpg.material-choice-audit.v1','state':'audit_only','registry_sha256':inv['registry_sha256'],
            'source_sha256':storage.sha(src),'candidate_inventory_sha256':storage.sha(cand),'pack':'military-base-megapack',
            'offset':0,'results':results,'limitations':'Selection favors the primary conversion; old source and bytes preserved, visual parity not established.'}


if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__)
    p.add_argument('--vault',type=Path,required=True);p.add_argument('--source',type=Path,required=True)
    p.add_argument('--candidates',type=Path,required=True);p.add_argument('--output',type=Path,required=True)
    a=p.parse_args()
    if a.output.exists():p.error('output exists')
    result=audit(a.vault,a.source,a.candidates)
    with a.output.open('x',encoding='utf8',newline='\n') as f:json.dump(result,f,ensure_ascii=False,indent=2);f.write('\n')
    print(json.dumps({'count':len(result['results']),'candidates':sum(x['state']=='candidate_only' for x in result['results'])}))
