"""Triage Rural plant variants by declared provenance and position triangles.

Material emission differs. This chooses a catalogue entry and preserves the
alternative bytes; it does not establish equal visual or runtime behavior.
"""
import argparse
import hashlib
import json
from pathlib import Path

import audit_variant_material_choices as materials
from audit_witch_source_choices import triangle_positions
import vault_storage as storage


def check(docs, raws, cards):
    if not cards[0].get('license') or cards[0].get('license') != cards[1].get('license'):
        raise ValueError('licenses differ')
    if any(c.get('missing') or c.get('readiness', {}).get('metadata_ready') is not True for c in cards):
        raise ValueError('metadata not ready')
    nodes = []
    for doc in docs:
        if doc.get('animations') or doc.get('skins') or doc.get('extensionsUsed'):
            raise ValueError('unsupported animated model or extensions')
        ns = [n for n in doc.get('nodes', []) if 'mesh' in n]
        if len(ns) != 1 or ns[0].get('children'):
            raise ValueError('unsupported node structure')
        nodes.append(ns[0].get('extras', {}))
    for key in ('source_prefab_guid','source_mesh_guid','source_sha256','source_gameobject'):
        if not nodes[0].get(key) or nodes[0][key] != nodes[1].get(key):
            raise ValueError('source identity differs: '+key)
    if triangle_positions(raws[0]) != triangle_positions(raws[1]):
        raise ValueError('oriented triangles differ')
    a,b = (d.get('materials',[]) for d in docs)
    if len(a) != 1 or len(b) != 1:
        raise ValueError('unexpected material count')
    if (a[0].get('alphaMode') != 'MASK' or b[0].get('alphaMode') != 'MASK'
            or a[0].get('doubleSided') is not True or b[0].get('doubleSided') is not True
            or a[0].get('alphaCutoff',0.5) != b[0].get('alphaCutoff',0.5)
            or not a[0].get('name','').startswith('M_WildCarrot_Atlas_')
            or b[0].get('extras',{}).get('source_material') != a[0].get('name')
            or a[0].get('extras',{}).get('source_unity_guid') != b[0].get('extras',{}).get('source_unity_guid')):
        raise ValueError('plant material identity or transparency differs')
    if 'emissiveTexture' not in b[0] or b[0].get('emissiveFactor') != [1,1,1] or 'emissiveTexture' in a[0]:
        raise ValueError('expected emission difference not present')
    return {'keep_side':'main','preferred_controls':['matching_alpha','avoid_baked_emission'],
            'limitations':'Alternative includes emission. Textures, normals and UVs may differ; visual/runtime not qualified.'}


def audit(vault, source, candidates):
    vault=vault.resolve(strict=True);src=source.read_bytes();cand=candidates.read_bytes()
    inv,doc=json.loads(src),json.loads(cand);index=storage.confined(vault,vault/'metadata/asset-import-registry.v1.json');before=index.read_bytes()
    if (storage.sha(before)!=inv['registry_sha256'] or doc.get('mode')!='rural-source'
            or doc['source_sha256']!=storage.sha(src) or doc['registry_sha256']!=inv['registry_sha256']):
        raise ValueError('inventory or registry changed')
    cards={}
    for entry in json.loads(before)['packs'].values():
        path=storage.confined(vault,vault/entry['path']/'details.v1.json');raw=path.read_bytes()
        if storage.sha(raw)!=entry['files']['details.v1.json']:raise ValueError('active metadata changed')
        for c in json.loads(raw)['assets']:
            if c['id'] in cards:raise ValueError('duplicate identity')
            cards[c['id']]=c
    eligible={x['variant_id'] for x in doc['candidates'] if x['main']['storage']==x['variant']['storage']=='descriptor'
              and x['main']['declared_pin_matches'] and x['variant']['declared_pin_matches']
              and not x['variant_has_aliases'] and all(x[k]['logical_bytes']<=512*1024*1024 for k in ('main','variant'))}
    results=[]
    for row in inv['pairs']:
        if row['variant_id'] not in eligible:continue
        item={'main_id':row['main_id'],'variant_id':row['variant_id']}
        try:
            c=[cards[row[k+'_id']] for k in ('main','variant')]
            if row['transforms_exact']!='True':raise ValueError('transforms differ')
            raw=[];docs=[]
            for k,card in zip(('main','variant'),c):
                if card.get('model_path')!=row[k+'_path'] or card.get('model_sha256')!=row[k+'_sha']:
                    raise ValueError('model pin changed')
                path=storage.confined(vault,Path(row[k+'_path']));data=storage.read_bytes(vault,path,512*1024*1024)
                if hashlib.sha256(data).hexdigest()!=row[k+'_sha']:raise ValueError('model SHA differs')
                raw.append(data);docs.append(materials.glb_document(vault,path))
            item.update(state='candidate_only',**check(docs,raw,c))
        except (OSError,ValueError,KeyError,TypeError) as e:item.update(state='review_required',reason=str(e))
        results.append(item)
    if index.read_bytes()!=before or source.read_bytes()!=src or candidates.read_bytes()!=cand:
        raise ValueError('input changed')
    return {'schema':'openrpg.material-choice-audit.v1','state':'audit_only','registry_sha256':inv['registry_sha256'],
            'source_sha256':storage.sha(src),'candidate_inventory_sha256':storage.sha(cand),
            'pack':'modular-rural-town','offset':0,'results':results,
            'limitations':'Source identity and positions compared; material appearance not equivalent.'}


if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__)
    for name in ('vault','source','candidates','output'):p.add_argument('--'+name,type=Path,required=True)
    a=p.parse_args()
    if a.output.exists():p.error('output exists')
    result=audit(a.vault,a.source,a.candidates)
    with a.output.open('x',encoding='utf8') as f:json.dump(result,f,ensure_ascii=False,indent=2);f.write('\n')
    print(json.dumps({'count':len(result['results']),'candidates':sum(r['state']=='candidate_only' for r in result['results'])}))
