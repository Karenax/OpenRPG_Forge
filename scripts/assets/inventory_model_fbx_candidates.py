"""Find model cards sharing FBX names when prefab identity is absent.

Names are only leads; they never establish that models or materials match.
"""
import argparse
import collections
import json
import re
from pathlib import Path, PureWindowsPath

from inventory_name_variant_candidates import prefab_paths
import vault_storage as storage


def mesh_paths(value):
    entries=value if isinstance(value,list) else [value]
    result=[]
    for entry in entries:
        if isinstance(entry,dict):
            original=entry.get('original_source_path')
            path=original if isinstance(original,str) and original.strip() else entry.get('path')
            guid=entry.get('guid') or entry.get('meta_guid')
            fileid=entry.get('fileID')
        else:path,guid,fileid=entry,None,None
        if isinstance(path,str) and path.strip():
            result.append((path,guid,fileid))
    return result


def inventory(vault):
    vault=vault.resolve(strict=True);pointer=storage.confined(vault,vault/'metadata/asset-import-registry.v1.json')
    before=pointer.read_bytes();groups=collections.defaultdict(dict);missing=0
    for pack,entry in sorted(json.loads(before)['packs'].items()):
        raw=storage.confined(vault,vault/entry['path']/'details.v1.json').read_bytes()
        if storage.sha(raw)!=entry['files']['details.v1.json']:raise ValueError('catalogue SHA differs')
        base=re.sub(r'\.variant-[a-f0-9]+$','',pack,flags=re.I)
        for card in json.loads(raw)['assets']:
            absent=not prefab_paths(card.get('source_prefab'))
            if absent:missing+=1
            ident=card['id'];match=re.search(r'-lod(\d+)$',ident,re.I);lod=int(match.group(1)) if match else 0
            for path,guid,fileid in mesh_paths(card.get('source_fbx')):
                name=PureWindowsPath(path.replace('/','\\')).stem.casefold()
                if not name:continue
                groups[(base,name,lod)][ident]={'id':ident,'pack':pack,'fbx':path,'guid':guid,
                    'fileID':fileid,'prefab_missing':absent,'model_path':card.get('model_path'),
                    'model_sha256':card.get('model_sha256')}
    if pointer.read_bytes()!=before:raise ValueError('registry changed')
    results=[]
    for (pack,name,lod),records in sorted(groups.items()):
        cards=list(records.values())
        if len(cards)>1 and any(c['prefab_missing'] for c in cards):
            results.append({'base_pack':pack,'fbx_name':name,'lod':lod,'cards':cards})
    return {'state':'inventory_only','registry_sha256':storage.sha(before),'cards_without_prefab':missing,
            'groups':results,'limitations':'FBX names are candidate leads only, not geometry or material equivalence.'}


if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--vault',required=True,type=Path);p.add_argument('--output',required=True,type=Path)
    a=p.parse_args();result=inventory(a.vault)
    with a.output.open('x',encoding='utf8') as f:json.dump(result,f,ensure_ascii=False,indent=2);f.write('\n')
    print(json.dumps({'missing_prefab_cards':result['cards_without_prefab'],'candidate_groups':len(result['groups'])}))
