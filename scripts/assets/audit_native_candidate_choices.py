"""Select better documented URP Native American conversions from matching sources.

This is catalogue triage, not visual/runtime equivalence; historical source
and model bytes must remain reconstructible.
"""
import argparse
import hashlib
import json
import math
from pathlib import Path
import vault_storage as storage
import audit_variant_material_choices as materials


def check_qualified_pair(old, primary, old_catalog, primary_catalog, vault):
    if (old.get('readiness',{}).get('metadata_ready') is not True
            or primary.get('readiness',{}).get('metadata_ready') is not True
            or old.get('missing') or primary.get('missing')):
        raise ValueError('both conversions are not metadata-ready')
    if not old.get('license') or old['license']!=primary.get('license'):
        raise ValueError('license differs or missing')
    for item,entry in ((old,old_catalog),(primary,primary_catalog)):
        if (entry.get('id')!=item.get('id') or entry.get('sha256')!=item.get('model_sha256')
                or entry.get('geometry_roundtrip_passed') is not True):
            raise ValueError('conversion catalogue evidence mismatch')
    if (primary_catalog.get('source_authored_frames_preserved') is not True
            or primary_catalog.get('material_review_pending') is not False):
        raise ValueError('primary conversion evidence incomplete')
    old_dir=storage.confined(vault,Path(old['source_catalog'])).parent
    for key in ('source_prefab','source_fbx'):
        entries=old.get(key);other=primary.get(key)
        if not isinstance(entries,list) or len(entries)!=1 or not isinstance(other,str):
            raise ValueError('source file evidence missing')
        entry=entries[0]
        if not storage.valid_hex(entry.get('sha256'),64):raise ValueError('source hash missing')
        old_path=storage.confined(vault,old_dir/entry['path'])
        new_path=storage.confined(vault,Path(other))
        if (old_path.suffix.lower()!=new_path.suffix.lower()
                or Path(entry.get('original_source_path','')).name.casefold()!=new_path.name.casefold()
                or hashlib.sha256(old_path.read_bytes()).hexdigest()!=entry['sha256']
                or hashlib.sha256(new_path.read_bytes()).hexdigest()!=entry['sha256']):
            raise ValueError('source file mismatch')
    a,b=old.get('measurement',{}),primary.get('measurement',{});da,db=a.get('dimensions'),b.get('dimensions')
    if not (isinstance(da,list) and isinstance(db,list) and len(da)==len(db)==3):raise ValueError('dimensions missing')
    pairs=[(float(x),float(y)) for x,y in zip(da,db)]
    if any(not math.isfinite(x) or not math.isfinite(y) or abs(x-y)>1e-4 for x,y in pairs):raise ValueError('dimensions differ')
    if type(a.get('triangles')) is not int or a['triangles']<=0 or a['triangles']!=b.get('triangles'):
        raise ValueError('triangle counts differ')
    doc=materials.glb_document(vault,storage.confined(vault,Path(old['model_path'])))
    private={str(x) for x in doc.get('extensionsUsed',[]) if str(x).startswith('OPENRPG_')}
    if doc.get('animations') or doc.get('skins') or private-{'OPENRPG_source_fragment'}:
        raise ValueError('old model uses unsupported animation or private extension')
    if any(n.get('extras',{}).get('source_role')=='collision' or n.get('name','').startswith(('UCX_','UTM_'))
           for n in doc.get('nodes',[])):
        raise ValueError('old model contains collision geometry')
    return {'keep_side':'variant','preferred_controls':['authored_frame','matching_unity_sources'],
            'source_prefab_sha256':old['source_prefab'][0]['sha256'],
            'limitations':'Both conversions documented; material and runtime equivalence not established.'}


def check_metadata(old, primary, old_catalog, primary_catalog, vault):
    if (old.get('readiness',{}).get('metadata_ready') is not False
            or primary.get('readiness',{}).get('metadata_ready') is not True
            or not old.get('missing') or primary.get('missing')):
        raise ValueError('qualification is not older-incomplete and primary-ready')
    if not old.get('license') or old['license']!=primary.get('license'):
        raise ValueError('license differs or missing')
    if old_catalog.get('id')!=old.get('id') or old_catalog.get('sha256')!=old.get('model_sha256'):
        raise ValueError('old model catalogue pin differs')
    if primary_catalog.get('id')!=primary.get('id') or primary_catalog.get('sha256')!=primary.get('model_sha256'):
        raise ValueError('primary model catalogue pin differs')
    if (primary_catalog.get('geometry_roundtrip_passed') is not True
            or primary_catalog.get('source_authored_frames_preserved') is not True
            or primary_catalog.get('material_review_pending') is not False):
        raise ValueError('primary conversion evidence incomplete')
    support=old_catalog.get('source_support_provenance')
    if not isinstance(support,dict) or not storage.valid_hex(support.get('source_sha256'),64):
        raise ValueError('old source prefab pin missing')
    source=storage.confined(vault,vault/support['source_file'])
    if hashlib.sha256(source.read_bytes()).hexdigest()!=support['source_sha256']:
        raise ValueError('old source prefab SHA mismatch')
    new_source=primary.get('source_prefab')
    if isinstance(new_source,dict):
        new_sha=new_source.get('sha256')
    elif isinstance(new_source,str):
        new_sha=hashlib.sha256(storage.confined(vault,Path(new_source)).read_bytes()).hexdigest()
    else:raise ValueError('primary source prefab missing')
    if new_sha!=support['source_sha256'] or not support.get('source_guid'):
        raise ValueError('source prefab SHA or GUID differs')
    a,b=old.get('measurement',{}),primary.get('measurement',{});da,db=a.get('dimensions'),b.get('dimensions')
    if not (isinstance(da,list) and isinstance(db,list) and len(da)==len(db)==3):raise ValueError('dimensions missing')
    pairs=[(float(x),float(y)) for x,y in zip(da,db)]
    if any(not math.isfinite(x) or not math.isfinite(y) or abs(x-y)>1e-4 for x,y in pairs):raise ValueError('dimensions differ')
    if type(a.get('triangles')) is not int or a['triangles']<=0 or a['triangles']!=b.get('triangles'):
        raise ValueError('triangle counts differ')
    collision=a.get('collision_geometry')
    if not isinstance(collision,dict) or collision.get('triangles')!=0 or collision.get('mesh_instances')!=0:
        raise ValueError('old collision status is not empty')
    return {'keep_side':'variant','preferred_controls':['authored_frame','documented_material'],
            'source_guid':support['source_guid'],'source_prefab_sha256':support['source_sha256'],
            'limitations':'Different source pipelines and materials; visual/runtime equivalence not established.'}


def audit(vault,source,candidates,include_raw_old=False):
    vault=vault.resolve(strict=True);src=source.read_bytes();cand=candidates.read_bytes()
    inv,doc=json.loads(src),json.loads(cand);regpath=storage.confined(vault,vault/'metadata/asset-import-registry.v1.json');reg=regpath.read_bytes()
    if (storage.sha(reg)!=inv['registry_sha256'] or doc.get('mode')!='native-candidates'
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
              if x['main']['storage'] in (('descriptor','raw') if include_raw_old else ('descriptor',))
              and (x['main']['storage']=='raw' or x['main']['declared_pin_matches'])
              and x['variant']['storage'] in ('descriptor','raw') and not x['main_has_aliases']
              and (x['variant']['storage']=='raw' or x['variant']['declared_pin_matches'])
              and all(x[k]['logical_bytes']<=512*1024*1024 for k in ('main','variant'))}
    catalogs={};results=[]
    for row in inv['pairs']:
        if row['variant_id'] not in eligible:continue
        result={'main_id':row['main_id'],'variant_id':row['variant_id']}
        try:
            old,primary=(assets[row[k+'_id']] for k in ('main','variant'))
            for k,item in (('main',old),('variant',primary)):
                if item.get('model_path')!=row[k+'_path'] or item.get('model_sha256')!=row[k+'_sha']:
                    raise ValueError('active model pin differs')
                if storage.confined(vault,Path(row[k+'_path'])).relative_to(vault).parts[0]!='normalized':
                    raise ValueError('model outside normalized scope')
            entries=[];pins=[]
            for item in (old,primary):
                path=storage.confined(vault,Path(item['source_catalog']))
                if path not in catalogs:
                    raw=path.read_bytes();catalogs[path]=(storage.sha(raw),{x['id']:x for x in json.loads(raw)['assets']})
                pin,index=catalogs[path];entries.append(index.get(item['id'],{}));pins.append(pin)
            if old.get('readiness',{}).get('metadata_ready') is True:
                choice=check_qualified_pair(old,primary,*entries,vault)
            else:
                choice=check_metadata(old,primary,*entries,vault)
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
            'pack':'native-american-village','offset':0,'include_raw_old':include_raw_old,'results':results,
            'limitations':'Source and measurements compared; visual/runtime equivalence not established.'}


if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__)
    p.add_argument('--vault',type=Path,required=True);p.add_argument('--source',type=Path,required=True)
    p.add_argument('--candidates',type=Path,required=True);p.add_argument('--output',type=Path,required=True)
    p.add_argument('--include-raw-old',action='store_true',help='triage raw historical models without permitting retirement')
    a=p.parse_args()
    if a.output.exists():p.error('output exists')
    result=audit(a.vault,a.source,a.candidates,a.include_raw_old)
    with a.output.open('x',encoding='utf8',newline='\n') as f:json.dump(result,f,ensure_ascii=False,indent=2);f.write('\n')
    print(json.dumps({'count':len(result['results']),'candidates':sum(x['state']=='candidate_only' for x in result['results'])}))
