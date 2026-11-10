"""Compare explicitly recorded material bindings; absence means unverified."""
import argparse
import collections
import csv
import hashlib
import json
import re
from pathlib import Path


def sha(value):
    return hashlib.sha256(value).hexdigest()


def base_id(value):
    return re.sub(r'\.variant-[a-f0-9]+(?=\.)', '', value, count=1)


def binding_signature(bindings):
    result=[]
    for b in bindings:
        textures=sorted((t.get('role'),t.get('guid'),tuple(t.get('offset',[])),tuple(t.get('scale',[])),t.get('status'))
                        for t in b.get('textures',[]))
        result.append((b.get('material_guid'),b.get('material_sha256'),b.get('shader_guid'),textures))
    return sorted(result,key=lambda x:json.dumps(x,ensure_ascii=False,sort_keys=True))


def main():
    p=argparse.ArgumentParser()
    p.add_argument('--vault',type=Path,required=True)
    p.add_argument('--candidates',type=Path,required=True)
    p.add_argument('--output',type=Path,required=True)
    a=p.parse_args(); vault=a.vault.resolve(strict=True)
    pointer_file=vault/'metadata/asset-source-support-registry.v1.json'
    original=pointer_file.read_bytes(); pointer=json.loads(original)
    raw=(vault/pointer['path']).read_bytes()
    if sha(raw)!=pointer['sha256']:raise ValueError('source catalogue SHA mismatch')
    by_path=collections.defaultdict(list)
    for item in json.loads(raw)['assets']:
        by_path[item.get('source_file','').replace('\\','/').casefold()].append(item)
    with a.candidates.open(encoding='utf-8-sig',newline='') as f: candidates=list(csv.DictReader(f))
    manifest_cache={}
    def record(card):
        e=card.get('evidence') or {}; name=e.get('package_manifest'); expected=e.get('package_manifest_sha256')
        if not name or not expected:return None,'missing manifest or SHA'
        key=(name,expected)
        if key not in manifest_cache:
            path=(vault/name).resolve(strict=True)
            if not path.is_relative_to(vault):raise ValueError('manifest outside Vault')
            data=path.read_bytes()
            if sha(data)!=expected:raise ValueError('manifest SHA mismatch: '+name)
            m=json.loads(data); rows=m.get('asset_map')
            if not isinstance(rows,list):manifest_cache[key]=None
            else:manifest_cache[key]={x.get('id'):x for x in rows if isinstance(x,dict)}
        rows=manifest_cache[key]
        if rows is None or card['id'] not in rows:return None,'asset_map entry absent'
        binding=rows[card['id']].get('material_bindings')
        if not isinstance(binding,list) or not binding:return None,'material bindings absent'
        return binding_signature(binding),None
    with a.output.open('x',encoding='utf-8',newline='\n') as out:
        for n,c in enumerate(candidates,1):
            urp=by_path[c['candidate'].replace('\\','/').casefold()]
            hdrp=[item for path in json.loads(c['retained_hdrp']) for item in by_path[path.replace('\\','/').casefold()]]
            index=collections.defaultdict(list)
            for item in hdrp:index[base_id(item['id'])].append(item)
            pairs=[]
            for item in urp:
                matches=index[base_id(item['id'])]
                if len(matches)!=1:
                    pairs.append({'urp_id':item['id'],'status':'unverified','reason':'ambiguous ID match'});continue
                try:
                    h,he=record(matches[0]);u,ue=record(item)
                    if he or ue:pairs.append({'urp_id':item['id'],'hdrp_id':matches[0]['id'],'status':'unverified','reason':he or ue})
                    else:pairs.append({'urp_id':item['id'],'hdrp_id':matches[0]['id'],'status':'compared','bindings_exact':h==u})
                except (OSError,ValueError,KeyError,TypeError) as error:
                    pairs.append({'urp_id':item['id'],'status':'unverified','reason':str(error)})
            out.write(json.dumps({'candidate':c['candidate'],'pairs':pairs,'deletable_verified':False},ensure_ascii=False)+'\n');out.flush()
            if n%50==0 or n==len(candidates):print(f'{n}/{len(candidates)} candidates; {len(manifest_cache)} manifests',flush=True)
    if pointer_file.read_bytes()!=original:raise ValueError('source registry changed')


if __name__=='__main__':main()
