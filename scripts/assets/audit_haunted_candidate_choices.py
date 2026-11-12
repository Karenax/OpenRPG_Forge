"""Triage older Haunted Village candidates against their registered models.

Matching prefab sources and measured dimensions do not establish equivalent
material appearance or placement. Historical bytes remain reconstructible.
"""
import argparse
import hashlib
import json
import math
from pathlib import Path
import vault_storage as storage


def check_metadata(main, candidate, primary, old, vault, allow_cave_pending=False):
    if (main.get('readiness',{}).get('metadata_ready') is not True
            or candidate.get('readiness',{}).get('metadata_ready') is not False
            or main.get('missing') or not candidate.get('missing')):
        raise ValueError('qualification is not primary-ready and candidate-incomplete')
    if not main.get('license') or main['license']!=candidate.get('license'):
        raise ValueError('license differs or missing')
    if primary.get('id')!=main.get('id') or primary.get('sha256')!=main.get('model_sha256'):
        raise ValueError('primary catalogue model pin mismatch')
    if old.get('id')!=candidate.get('id') or old.get('sha256')!=candidate.get('model_sha256'):
        raise ValueError('candidate catalogue model pin mismatch')
    support=old.get('source_support_provenance')
    if not isinstance(support,dict):raise ValueError('candidate source evidence missing')
    queue=primary.get('queue_identity') or {}
    guid=primary.get('source_guid') or queue.get('prefab_guid')
    primary_sha=primary.get('source_prefab_sha256') or primary.get('source_sha256')
    if (not guid or guid!=support.get('source_guid') or not storage.valid_hex(primary_sha,64)
            or primary_sha!=support.get('source_sha256')):
        raise ValueError('source prefab identity or SHA differs')
    source=storage.confined(vault,vault/support['source_file'])
    if hashlib.sha256(source.read_bytes()).hexdigest()!=primary_sha:raise ValueError('source prefab SHA mismatch')
    passed=primary.get('geometry_roundtrip_passed') is True or (primary.get('roundtrip') or {}).get('passed') is True
    if not passed:raise ValueError('primary geometry roundtrip not passed')
    cave_pending = (allow_cave_pending and primary.get('visual_review') == 'agent_review'
                    and primary.get('material_review_pending') is True)
    if not (primary.get('visual_review')=='agent_reviewed' or cave_pending or
            primary.get('material_qualification')=='exact-S_Foliage-graph-SHA-and-portable-parameter-projection'):
        raise ValueError('primary material evidence not recognized')
    a,b=main.get('measurement',{}),candidate.get('measurement',{});da,db=a.get('dimensions'),b.get('dimensions')
    if not (isinstance(da,list) and isinstance(db,list) and len(da)==len(db)==3):raise ValueError('dimensions missing')
    pairs=[(float(x),float(y)) for x,y in zip(da,db)]
    if any(not math.isfinite(x) or not math.isfinite(y) or abs(x-y)>1e-4 for x,y in pairs):raise ValueError('dimensions differ')
    if type(a.get('triangles')) is not int or a['triangles']<=0 or a['triangles']!=b.get('triangles'):
        raise ValueError('triangle counts differ')
    collision=b.get('collision_geometry')
    if not isinstance(collision,dict) or collision.get('triangles')!=0 or collision.get('mesh_instances')!=0:
        raise ValueError('candidate collision status is not empty')
    return {'keep_side':'main','preferred_controls':[('primary_material_review_pending' if cave_pending else 'reviewed_primary'),'matching_prefab_source'],
            'source_guid':guid,'source_prefab_sha256':primary_sha,
            'limitations':('Primary material review pending. ' if cave_pending else '') + 'Coordinate frames and materials differ; visual and runtime equivalence not established.'}


def audit(vault,source,candidates,pack='modular-haunted-village'):
    vault=vault.resolve(strict=True);src=source.read_bytes();cand=candidates.read_bytes()
    inv,doc=json.loads(src),json.loads(cand);regpath=storage.confined(vault,vault/'metadata/asset-import-registry.v1.json');reg=regpath.read_bytes()
    mode={'modular-haunted-village':'haunted-candidates','modular-cave':'cave-candidates',
          'mountain-ancient-mountain':'ancient-ruins-candidates'}[pack]
    if (storage.sha(reg)!=inv['registry_sha256'] or doc.get('mode')!=mode
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
              and not x['variant_has_aliases'] and not x['main_has_aliases']
              and all(x[k]['logical_bytes']<=512*1024*1024 for k in ('main','variant'))}
    catalogs={};results=[]
    for row in inv['pairs']:
        if row['variant_id'] not in eligible:continue
        result={'main_id':row['main_id'],'variant_id':row['variant_id']}
        try:
            main,candidate=(assets[row[k+'_id']] for k in ('main','variant'))
            reverse = (pack == 'modular-cave' and main.get('readiness',{}).get('metadata_ready') is False
                       and candidate.get('readiness',{}).get('metadata_ready') is True)
            scopes = (('main',main,'candidates'),('variant',candidate,'normalized')) if reverse else (('main',main,'normalized'),('variant',candidate,'normalized' if pack=='mountain-ancient-mountain' else 'candidates'))
            for k,item,expected in scopes:
                if item.get('model_path')!=row[k+'_path'] or item.get('model_sha256')!=row[k+'_sha']:
                    raise ValueError('active model pin differs')
                if storage.confined(vault,Path(row[k+'_path'])).relative_to(vault).parts[0]!=expected:
                    raise ValueError('model outside expected scope')
                if pack=='mountain-ancient-mountain' and k=='variant' and 'ancient-mountain-source-graph-ruins6-linear-v1' not in Path(row[k+'_path']).parts:
                    raise ValueError('variant outside ruins source graph scope')
            entries=[];pins=[]
            for item in (main,candidate):
                path=storage.confined(vault,Path(item['source_catalog']))
                if path not in catalogs:
                    raw=path.read_bytes();catalogs[path]=(storage.sha(raw),{x['id']:x for x in json.loads(raw)['assets']})
                pin,index=catalogs[path];entries.append(index.get(item['id'],{}));pins.append(pin)
            if reverse:
                choice=check_metadata(candidate,main,entries[1],entries[0],vault,allow_cave_pending=True)
                choice['keep_side']='variant'
            else:
                choice=check_metadata(main,candidate,*entries,vault,allow_cave_pending=(pack in ('modular-cave','mountain-ancient-mountain')))
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
            'pack':pack,'offset':0,'results':results,
            'limitations':'Prefabs match; catalogue choice only. Visual and runtime equivalence not established.'}


if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__)
    p.add_argument('--vault',type=Path,required=True);p.add_argument('--source',type=Path,required=True)
    p.add_argument('--candidates',type=Path,required=True);p.add_argument('--output',type=Path,required=True)
    p.add_argument('--pack',choices=('modular-haunted-village','modular-cave','mountain-ancient-mountain'),default='modular-haunted-village')
    a=p.parse_args()
    if a.output.exists():p.error('output exists')
    result=audit(a.vault,a.source,a.candidates,a.pack)
    with a.output.open('x',encoding='utf8',newline='\n') as f:json.dump(result,f,ensure_ascii=False,indent=2);f.write('\n')
    print(json.dumps({'count':len(result['results']),'candidates':sum(x['state']=='candidate_only' for x in result['results'])}))
