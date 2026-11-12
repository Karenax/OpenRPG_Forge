"""Plan one Townsmith tent version while reconstructing alternative GLBs.

Choose the masked main entry; the alternative draws backfaces and is opaque.
This is not a claim of equal appearance. The historical GLB is reconstructed
exactly from its metadata prefix and the identical binary tail.
"""
import argparse
import hashlib
import json
from pathlib import Path

import audit_variant_material_choices as materials
from prepare_variant_metadata_transition import load_packs, prepare_transition
from prepare_variant_reference_patterns import vault_uuid
from variant_legacy_recipe import build_recipe
import vault_storage as storage

PREFIX='hivemind.townsmith.prefab-sm-tent-d-abc00000000004293966000721858020'


def validate_docs(a,b):
    if any(x.get('skins') or x.get('animations') or x.get('extensionsUsed') for x in (a,b)):
        raise ValueError('unsupported animation or extension')
    if len(a.get('nodes',[])) != 1 or len(b.get('nodes',[])) != 1:
        raise ValueError('unexpected node count')
    nodes=[x['nodes'][0] for x in (a,b)]
    for key in ('source_prefab_guid','source_mesh_guid','source_gameobject','source_sha256','license'):
        if not nodes[0].get('extras',{}).get(key) or nodes[0]['extras'][key] != nodes[1].get('extras',{}).get(key):
            raise ValueError('source identity differs: '+key)
    am,bm=(x.get('materials',[]) for x in (a,b))
    if len(am)!=2 or len(bm)!=2 or [m.get('name') for m in am] != [m.get('name') for m in bm]:
        raise ValueError('material slots differ')
    if (am[0]['name']!='M_Tent_Inst' or am[0].get('alphaMode')!='MASK'
            or bm[0].get('alphaMode','OPAQUE')!='OPAQUE'
            or am[0].get('doubleSided',False) is not False or bm[0].get('doubleSided') is not True):
        raise ValueError('expected mask/backface conflict differs')


def plan(vault,source):
    vault=vault.resolve(strict=True);raw_source=source.read_bytes();inv=json.loads(raw_source)
    index=storage.confined(vault,vault/'metadata/asset-import-registry.v1.json');before=index.read_bytes()
    if storage.sha(before)!=inv['registry_sha256']:raise ValueError('registry changed')
    cards={}
    for entry in json.loads(before)['packs'].values():
        path=storage.confined(vault,vault/entry['path']/'details.v1.json');raw=path.read_bytes()
        if storage.sha(raw)!=entry['files']['details.v1.json']:raise ValueError('details changed')
        for card in json.loads(raw)['assets']:
            if card['id'] in cards:raise ValueError('duplicate identity')
            cards[card['id']]=card
    rows=[];recipes={}
    for row in inv['pairs']:
        if not (row['main_id']==PREFIX or row['main_id'].startswith(PREFIX+'-lod')):continue
        if (not row['comparison_current'] or row['mesh_payloads_exact']!='True'
                or row['images_exact']!='True' or row['transforms_exact']!='True'):
            raise ValueError('comparison incomplete')
        main,old=(cards[row[k+'_id']] for k in ('main','variant'))
        if (not main.get('license') or main['license']!=old.get('license')
                or old.get('consolidated_variants') or main.get('missing') or old.get('missing')):
            raise ValueError('metadata, license or aliases need review')
        raw=[];docs=[]
        for k,card in (('main',main),('variant',old)):
            if card.get('model_path')!=row[k+'_path'] or card.get('model_sha256')!=row[k+'_sha']:
                raise ValueError('model pin changed')
            p=storage.confined(vault,Path(row[k+'_path']))
            raw.append(storage.read_bytes(vault,p,512*1024*1024));docs.append(materials.glb_document(vault,p))
        validate_docs(*docs)
        recipe=build_recipe(raw[0],raw[1],row['main_sha'],row['variant_sha'])
        recipes[row['variant_id']]=recipe
        rows.append({'retained_id':row['main_id'],'retired_id':row['variant_id'],
                     'retained_uuid':vault_uuid(row['main_id']),'retired_uuid':vault_uuid(row['variant_id']),
                     'retained_path':row['main_path'],'retired_path':row['variant_path'],
                     'retained_sha256':row['main_sha'],'retired_sha256':row['variant_sha'],
                     'choice':'masked main; old opaque double-sided model reconstructible', 'ready_for_removal':False})
    if len(rows)!=6 or index.read_bytes()!=before or source.read_bytes()!=raw_source:
        raise ValueError('unexpected count or inputs changed')
    result={'schema':'openrpg.variant-consolidation-plan.v1','state':'planning',
            'registry_sha256':inv['registry_sha256'],'candidate_count':len(rows),'candidates':rows}
    prepare_transition(load_packs(vault,result),rows,recipes)
    return result,{'state':'prepared_only','registry_sha256':inv['registry_sha256'],'recipes':recipes}


if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__)
    for x in ('vault','source','plan-output','recipes-output'):p.add_argument('--'+x,type=Path,required=True)
    a=p.parse_args()
    if a.plan_output.exists() or a.recipes_output.exists():p.error('output exists')
    result,recipes=plan(a.vault,a.source)
    for path,doc in ((a.plan_output,result),(a.recipes_output,recipes)):
        with path.open('x',encoding='utf8') as f:json.dump(doc,f,ensure_ascii=False,indent=2);f.write('\n')
    print(json.dumps({'candidates':len(result['candidates']),'removed':0}))
