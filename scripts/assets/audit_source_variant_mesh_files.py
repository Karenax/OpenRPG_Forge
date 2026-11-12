"""Find byte-identical mesh dependencies of related HDRP/URP source cards.

This reports candidates only. Equal mesh bytes alone do not prove material or
prefab equivalence, and all consumers must be migrated before any deletion.
"""
import argparse
import json
import re
from pathlib import Path
import vault_storage as storage
from stage_source_support_pair_consolidation import PROVENANCE_DIFFERENCES
from stage_source_support_relinks import digest_file, norm


def audit(vault, inventory, singular=False, all_meshes=False):
    vault=vault.resolve(strict=True)
    raw_inventory=inventory.read_bytes();data=json.loads(raw_inventory)
    pointer=storage.confined(vault,vault/'metadata/asset-source-support-registry.v1.json')
    pointer_raw=pointer.read_bytes();entry=json.loads(pointer_raw)
    if storage.sha(pointer_raw)!=data['source_registry_sha256']:raise ValueError('inventory registry changed')
    catalog=storage.confined(vault,vault/entry['path']).read_bytes()
    if storage.sha(catalog)!=entry['sha256']:raise ValueError('source catalog SHA changed')
    cards={x['id']:x for x in json.loads(catalog)['assets']}
    fields={'preview_sha256','source_mesh_file','taxonomy'} if singular else {'preview_sha256','source_mesh_files'}
    wanted=(data['pairs'] if all_meshes else
            [p for p in data['pairs'] if p['declared_source_sha_equal'] and p['declared_meta_sha_equal']
             and set(p['different_fields'])-PROVENANCE_DIFFERENCES==fields])
    cache={};results=[]
    def inspect(relative):
        if not isinstance(relative,str):raise ValueError('invalid mesh path')
        norm(relative)
        path=storage.confined(vault,vault/relative)
        if path not in cache:
            pin=digest_file(path);meta=path.with_name(path.name+'.meta')
            if not meta.exists():raise ValueError('mesh meta missing')
            meta=storage.confined(vault,meta)
            cache[path]={'sha256':pin,'bytes':path.stat().st_size,'meta_sha256':digest_file(meta)}
        return cache[path]
    for pair in wanted:
        result={'hdrp_id':pair['hdrp_id'],'urp_id':pair['urp_id']}
        try:
            if all_meshes:
                def paths(card):
                    value=card.get('source_mesh_files')
                    if value is None:
                        value=[card['source_mesh_file']] if card.get('source_mesh_file') else []
                    if not isinstance(value,list):raise ValueError('invalid mesh list')
                    return value
                first,second=paths(cards[pair['hdrp_id']]),paths(cards[pair['urp_id']])
                if not first and not second:
                    continue
                def logical(path):
                    if not isinstance(path,str):raise ValueError('invalid mesh path')
                    suffix=re.split(r'(?i)/source/assets/',path.replace('\\','/'),maxsplit=1)
                    if len(suffix)!=2:raise ValueError('mesh path outside extracted Unity assets')
                    return re.sub(r'(?i)/(?:(?:hdrp|urp)(?:\(default\))?)/','/<pipeline>/',suffix[1]).casefold()
                first=sorted(first,key=logical);second=sorted(second,key=logical)
                if len(first)!=len(second) or any(logical(a)!=logical(b) for a,b in zip(first,second)):
                    raise ValueError('mesh paths do not identify the same Unity asset')
                if all(norm(a)==norm(b) for a,b in zip(first,second)):
                    continue
            elif singular:
                first=[cards[pair['hdrp_id']]['source_mesh_file']]
                second=[cards[pair['urp_id']]['source_mesh_file']]
            else:
                first=cards[pair['hdrp_id']]['source_mesh_files'];second=cards[pair['urp_id']]['source_mesh_files']
            if not isinstance(first,list) or not isinstance(second,list) or len(first)!=len(second) or not first:
                raise ValueError('missing or mismatched mesh dependency list')
            checks=[]
            for a,b in zip(first,second):
                if norm(a)==norm(b):continue
                aa,bb=inspect(a),inspect(b)
                checks.append({'hdrp':a,'urp':b,'hdrp_info':aa,'urp_info':bb,
                               'same_mesh':aa['sha256']==bb['sha256'],
                               'same_meta':aa['meta_sha256']==bb['meta_sha256']})
            result['dependencies']=checks
            result['state']='identical_dependencies' if all(x['same_mesh'] and x['same_meta'] for x in checks) else 'different_dependencies'
        except (OSError,ValueError,KeyError) as error:
            result.update(state='review_required',reason=str(error))
        results.append(result)
    if pointer.read_bytes()!=pointer_raw or inventory.read_bytes()!=raw_inventory:raise ValueError('input changed')
    return {'state':'audit_only','source_registry_sha256':storage.sha(pointer_raw),
            'inventory_sha256':storage.sha(raw_inventory),'unique_meshes_read':len(cache),'results':results,
            'limitations':'Mesh and meta bytes only. References, shaders, other dependencies and Studio are not qualified.'}


if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__)
    for name in ('vault','inventory','output'):p.add_argument('--'+name,type=Path,required=True)
    p.add_argument('--singular',action='store_true')
    p.add_argument('--all-meshes',action='store_true')
    a=p.parse_args();result=audit(a.vault,a.inventory,a.singular,a.all_meshes)
    with a.output.open('x',encoding='utf8') as f:json.dump(result,f,ensure_ascii=False,indent=2);f.write('\n')
    print(json.dumps({'pairs':len(result['results']),'unique_meshes_read':result['unique_meshes_read'],
        'identical':sum(x['state']=='identical_dependencies' for x in result['results']),
        'review_required':sum(x['state']=='review_required' for x in result['results'])}))
