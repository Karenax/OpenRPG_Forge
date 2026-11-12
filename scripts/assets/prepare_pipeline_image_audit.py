"""Pair identical pipeline images with their identical Unity metadata."""
import argparse
import json
from pathlib import Path
import vault_storage as storage


def prepare(source, fbx=False, materials_prefabs=False):
    raw=source.read_bytes();inv=json.loads(raw)
    if inv.get('state')!='inventory_only' or inv.get('skipped_reparse'):
        raise ValueError('invalid inventory or skipped junctions')
    index={item['old'].casefold():item for item in inv['pairs']}
    if len(index)!=len(inv['pairs']):raise ValueError('duplicate inventory paths')
    results=[]
    for item in inv['pairs']:
        if fbx and materials_prefabs:raise ValueError('choose one asset class')
        suffixes=('.fbx',) if fbx else ('.mat','.prefab') if materials_prefabs else ('.png','.tga')
        if not item['old'].lower().endswith(suffixes):continue
        meta=index.get((item['old']+'.meta').casefold())
        if not meta or meta['retained'].casefold()!=(item['retained']+'.meta').casefold():
            continue
        if not storage.valid_hex(item['sha256'],64) or not storage.valid_hex(meta['sha256'],64):
            raise ValueError('invalid inventory SHA')
        info={'sha256':item['sha256'],'bytes':item['bytes'],'meta_sha256':meta['sha256']}
        results.append({'state':'identical_dependencies','dependencies':[{'hdrp':item['old'],
            'urp':item['retained'],'hdrp_info':info,'urp_info':dict(info),'same_mesh':True,'same_meta':True}]})
    return {'state':'audit_only','source_registry_sha256':inv['source_registry_sha256'],
        'inventory_sha256':storage.sha(raw),'results':results,
        'limitations':'Exact image and .meta bytes only; active and Unity GUID references remain unqualified.'}


if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__)
    for name in ('inventory','output'):p.add_argument('--'+name,type=Path,required=True)
    p.add_argument('--fbx',action='store_true')
    p.add_argument('--materials-prefabs',action='store_true')
    a=p.parse_args();result=prepare(a.inventory,a.fbx,a.materials_prefabs)
    with a.output.open('x',encoding='utf8') as f:json.dump(result,f,ensure_ascii=False,indent=2);f.write('\n')
    print(json.dumps({'pairs':len(result['results']),
        'logical_bytes':sum(x['dependencies'][0]['hdrp_info']['bytes'] for x in result['results'])}))
