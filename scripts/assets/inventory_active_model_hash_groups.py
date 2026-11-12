"""Inventory active model cards by declared logical SHA; no deletion authority."""
import argparse
import collections
import json
from pathlib import Path

import vault_storage as storage


def inventory(vault):
    vault=vault.resolve(strict=True)
    pointer=storage.confined(vault,vault/'metadata/asset-import-registry.v1.json')
    before=pointer.read_bytes();registry=json.loads(before)
    groups=collections.defaultdict(list);cards=0;unusable=[]
    for pack,entry in sorted(registry['packs'].items()):
        path=storage.confined(vault,vault/entry['path']/'details.v1.json')
        raw=path.read_bytes()
        if storage.sha(raw)!=entry['files']['details.v1.json']:
            raise ValueError('catalogue SHA differs: '+pack)
        for card in json.loads(raw)['assets']:
            cards+=1;pin=card.get('model_sha256');model=card.get('model_path')
            if not storage.valid_hex(pin,64) or not isinstance(model,str):
                unusable.append(card.get('id'));continue
            prefab=card.get('source_prefab')
            prefab=prefab.get('path') if isinstance(prefab,dict) else prefab
            groups[pin].append({'id':card['id'],'pack':pack,'model_path':model,
                                'source_prefab':prefab,'names':card.get('names')})
    if pointer.read_bytes()!=before:raise ValueError('model registry changed')
    duplicates=[]
    for pin,items in sorted(groups.items()):
        if len(items)<2:continue
        paths={x['model_path'].replace('\\','/').casefold() for x in items}
        duplicates.append({'sha256':pin,'distinct_paths':len(paths),'cards':items})
    return {'state':'inventory_only','registry_sha256':storage.sha(before),'cards':cards,
            'unusable_ids':unusable,'groups':duplicates,
            'limitations':'Declared SHA only; payloads, provenance, references and rendering are not verified.'}


if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__)
    p.add_argument('--vault',required=True,type=Path);p.add_argument('--output',required=True,type=Path)
    a=p.parse_args();result=inventory(a.vault)
    with a.output.open('x',encoding='utf8') as f:json.dump(result,f,ensure_ascii=False,indent=2);f.write('\n')
    print(json.dumps({'cards':result['cards'],'groups':len(result['groups']),
                      'distinct_path_groups':sum(x['distinct_paths']>1 for x in result['groups']),
                      'unusable':len(result['unusable_ids'])}))
