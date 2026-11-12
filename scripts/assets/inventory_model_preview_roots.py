"""Inventory exact preview duplicates and each active model's permitted root."""
import argparse
import collections
import json
from pathlib import Path

import vault_storage as storage
from stage_model_preview_relinks import asset_root
from stage_source_support_relinks import digest_file


def inventory(vault):
    vault=vault.resolve(strict=True)
    index=storage.confined(vault,vault/'metadata/asset-import-registry.v1.json')
    original=index.read_bytes();reg=json.loads(original)
    groups=collections.defaultdict(dict);catalogs={};counts=collections.Counter()
    for pack,entry in sorted(reg['packs'].items()):
        raw=storage.confined(vault,vault/entry['path']/'details.v1.json').read_bytes()
        if storage.sha(raw)!=entry['files']['details.v1.json']:raise ValueError('model snapshot changed')
        for card in json.loads(raw)['assets']:
            cat=storage.confined(vault,Path(card['source_catalog']))
            if cat not in catalogs:
                data=cat.read_bytes();assets=json.loads(data)['assets'];items={x['id']:x for x in assets}
                if len(items)!=len(assets):raise ValueError('duplicate catalog identity')
                catalogs[cat]=(storage.sha(data),items)
            row=catalogs[cat][1].get(card['id'])
            if row is None or row.get('sha256')!=card.get('model_sha256'):raise ValueError('catalog model mismatch')
            rel,pin=row.get('preview'),row.get('preview_sha256')
            if rel is None and pin is None:
                counts['no_preview']+=1;continue
            if not isinstance(rel,str) or not storage.valid_hex(pin,64):raise ValueError('invalid preview declaration')
            base=asset_root(cat,card,row)
            if 'preview_vault' in row:
                shared=row['preview_vault']
                if not isinstance(shared,str) or Path(shared).is_absolute():raise ValueError('invalid shared preview')
                path=storage.confined(vault,vault/Path(shared))
            else:
                path=base/Path(rel)
            if not path.is_absolute() or not path.is_relative_to(vault):raise ValueError('preview outside Vault')
            key=str(path).casefold()
            record=groups[pin].setdefault(key,{'path':str(path),'models':[]})
            record['models'].append({'id':card['id'],'pack':pack,'asset_root':str(base),'catalog':str(cat)})
            counts['with_preview']+=1
    if index.read_bytes()!=original:raise ValueError('registry changed')
    for path,(sha,_) in catalogs.items():
        if storage.sha(path.read_bytes())!=sha:raise ValueError('catalog changed')
    duplicates=[]
    for pin,paths in groups.items():
        if len(paths)<2:continue
        rows=list(paths.values())
        for row in rows:
            path=storage.confined(vault,Path(row['path']))
            row['bytes']=path.stat().st_size
            if digest_file(path)!=pin:raise ValueError('preview SHA changed')
        duplicates.append({'sha256':pin,'paths':rows})
    if index.read_bytes()!=original:raise ValueError('registry changed during image verification')
    return {'state':'inventory_only','registry_sha256':storage.sha(original),'counts':dict(counts),
            'catalogs':len(catalogs),'groups':duplicates,
            'limitations':'Only active model previews; other catalog, project and external references not qualified.'}


if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__)
    for n in ('vault','output'):p.add_argument('--'+n,type=Path,required=True)
    a=p.parse_args();result=inventory(a.vault)
    with a.output.open('x',encoding='utf8') as f:json.dump(result,f,ensure_ascii=False,indent=2);f.write('\n')
    print(json.dumps({'counts':result['counts'],'groups':len(result['groups']),
                      'extra_paths':sum(len(g['paths'])-1 for g in result['groups'])}))
