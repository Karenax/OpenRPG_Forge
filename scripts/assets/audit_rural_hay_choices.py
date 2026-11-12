"""Select reviewed Rural hay conversions; preserve non-equivalent variants."""
import argparse
import hashlib
import json
import math
from pathlib import Path
import vault_storage as storage

GUID='abc00000000017760980773119374081'
ROOT=Path(r'F:\OpenRPG_ImportWork\hivemind-run\modular-rural-town')
SOURCES=(ROOT/'source/Assets/HIVEMIND/RuralTown/HDRP(Default)/Art/Prefabs/SM_Hay.prefab',
         ROOT/'variants/d11f8313e2b6012f/source/Assets/HIVEMIND/RuralTown/URP/Art/Prefabs/SM_Hay.prefab')


def check(cards, entries):
    a,b=cards;x,y=entries
    if not a.get('license') or a['license']!=b.get('license'):raise ValueError('license differs')
    if any(c.get('missing') or c.get('readiness',{}).get('metadata_ready') is not True for c in cards):
        raise ValueError('metadata incomplete')
    for c,e in zip(cards,entries):
        if e.get('id')!=c['id'] or e.get('sha256')!=c.get('model_sha256') or e.get('source_guid')!=GUID:
            raise ValueError('catalog identity or model pin differs')
    if x.get('material_review_pending') is not False or x.get('visual_review')!='agent_reviewed':
        raise ValueError('primary review not present')
    if x.get('geometry_roundtrip_passed') is not True or y.get('geometry_roundtrip_passed') is not True:
        raise ValueError('geometry roundtrip missing')
    hashes=[x.get('source_prefab',{}).get('sha256'),y.get('source_sha256')]
    for path,expected in zip(SOURCES,hashes):
        if not storage.valid_hex(expected,64):raise ValueError('source SHA missing')
        p=storage.confined(Path(ROOT.anchor),path)
        if hashlib.sha256(p.read_bytes()).hexdigest()!=expected:raise ValueError('source SHA differs')
    ma,mb=a.get('measurement',{}),b.get('measurement',{});da,db=ma.get('dimensions'),mb.get('dimensions')
    if not (isinstance(da,list) and isinstance(db,list) and len(da)==len(db)==3):raise ValueError('dimensions missing')
    if any(not math.isfinite(float(u)) or not math.isfinite(float(v)) or abs(float(u)-float(v))>1e-4 for u,v in zip(da,db)):
        raise ValueError('dimensions differ')
    if type(ma.get('triangles')) is not int or type(mb.get('triangles')) is not int or mb['triangles']<=0 or ma['triangles']!=2*mb['triangles']:
        raise ValueError('unexpected triangle count relationship')
    return {'keep_side':'main','preferred_controls':['reviewed_primary'], 'source_prefab_sha256':hashes,
            'limitations':'Different source revisions, frames, triangle counts and materials; visual/runtime equivalence not established.'}


def audit(vault,source,candidates):
    vault=vault.resolve(strict=True);src=source.read_bytes();cand=candidates.read_bytes();inv,doc=json.loads(src),json.loads(cand)
    index=storage.confined(vault,vault/'metadata/asset-import-registry.v1.json');before=index.read_bytes()
    if (storage.sha(before)!=inv['registry_sha256'] or doc.get('mode')!='rural-hay'
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
    results=[];catalogs={}
    for row in inv['pairs']:
        if row['variant_id'] not in eligible:continue
        item={'main_id':row['main_id'],'variant_id':row['variant_id']}
        try:
            entries=[];cs=[cards[row[k+'_id']] for k in ('main','variant')]
            for k,c in zip(('main','variant'),cs):
                if c.get('model_path')!=row[k+'_path'] or c.get('model_sha256')!=row[k+'_sha']:raise ValueError('model pin changed')
                path=storage.confined(vault,Path(c['source_catalog']))
                if path not in catalogs:
                    raw=path.read_bytes();catalogs[path]=(storage.sha(raw),{x['id']:x for x in json.loads(raw)['assets']})
                entries.append(catalogs[path][1][c['id']])
            item.update(state='candidate_only',**check(cs,entries))
        except (OSError,ValueError,KeyError,TypeError) as e:item.update(state='review_required',reason=str(e))
        results.append(item)
    if index.read_bytes()!=before or source.read_bytes()!=src or candidates.read_bytes()!=cand:raise ValueError('inputs changed')
    for path,(pin,_) in catalogs.items():
        if storage.sha(path.read_bytes())!=pin:raise ValueError('catalog changed')
    return {'schema':'openrpg.material-choice-audit.v1','state':'audit_only','registry_sha256':inv['registry_sha256'],
            'source_sha256':storage.sha(src),'candidate_inventory_sha256':storage.sha(cand),'pack':'modular-rural-town',
            'offset':0,'results':results,'limitations':'Source and dimensions checked; appearance is not equivalent.'}


if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__)
    for n in ('vault','source','candidates','output'):p.add_argument('--'+n,type=Path,required=True)
    a=p.parse_args()
    if a.output.exists():p.error('output exists')
    result=audit(a.vault,a.source,a.candidates)
    with a.output.open('x',encoding='utf8') as f:json.dump(result,f,ensure_ascii=False,indent=2);f.write('\n')
    print(json.dumps({'count':len(result['results']),'candidates':sum(x['state']=='candidate_only' for x in result['results'])}))
