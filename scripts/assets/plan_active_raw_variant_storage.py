"""Plan one lossless shared-storage batch for registered variant models."""
import argparse
import collections
import json
import re
from pathlib import Path

import vault_storage as storage


def prepare(vault, families=None, include_primary=False):
    vault=vault.resolve(strict=True);pointer=storage.confined(vault,vault/'metadata/asset-import-registry.v1.json')
    before=pointer.read_bytes();registry=json.loads(before);groups=collections.defaultdict(dict);refused=[]
    for pack,entry in sorted(registry['packs'].items()):
        details=storage.confined(vault,vault/entry['path']/'details.v1.json').read_bytes()
        if storage.sha(details)!=entry['files']['details.v1.json']:raise ValueError('active snapshot changed')
        for card in json.loads(details)['assets']:
            ident=card.get('id','')
            parts=ident.split('.')
            if len(parts)<3:continue
            family=parts[1]
            if families is not None and family not in families:continue
            if not include_primary and not re.search(r'\.variant-[a-f0-9]+\.',ident):continue
            try:
                path=storage.confined(vault,Path(card['model_path']))
                if not storage.eligible_model_storage_path(vault,path):
                    raise ValueError('outside converted-model storage')
                pin=card['model_sha256']
                if not storage.valid_hex(pin,64):raise ValueError('invalid pin')
                info=path.stat()
                if storage.windows_readonly(info) or not 0<info.st_size<=storage.MAX_MODEL:continue
                with path.open('rb') as stream:header=stream.read(8)
                if header==storage.MAGIC:continue
                if header[:4]!=b'glTF':raise ValueError('unexpected model header')
                row={'path':str(path),'bytes':info.st_size,'mtime_ns':info.st_mtime_ns,'sha256':pin,'asset_id':ident}
                key=str(path).casefold()
                previous=groups[family].get(key)
                if previous and previous['sha256']!=pin:raise ValueError('conflicting pin for same path')
                groups[family][key]=row
            except (OSError,ValueError,KeyError) as error:
                refused.append({'id':ident,'reason':str(error)})
    if pointer.read_bytes()!=before:raise ValueError('registry changed')
    available={key:sorted(group.values(),key=lambda row:row['path'].casefold()) for key,group in groups.items() if group}
    if not available:raise ValueError('no registered raw variant model found')
    family=max(available,key=lambda k:sum(row['bytes'] for row in available[k]))
    selected=available[family][:16]
    return {'schema':1,'root':str(vault),'utc':storage.utc(),
            'policy':{'scope':'registered active models; one storage transaction batch' if include_primary else 'registered active variants; one storage transaction batch','family':family,
                      'registry_sha256':storage.sha(before),'available_by_family':{k:len(v) for k,v in available.items()},
                      'refused':refused},'files':selected,'logical_bytes':sum(x['bytes'] for x in selected)}


if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--vault',type=Path,required=True);p.add_argument('--output',type=Path,required=True)
    p.add_argument('--families',help='comma separated explicit model families');p.add_argument('--include-primary',action='store_true');a=p.parse_args()
    if a.include_primary and not a.families:p.error('--include-primary requires explicit --families')
    if a.output.exists():p.error('output exists')
    families=set(a.families.split(',')) if a.families else None
    if families is not None and ('' in families or any(not re.fullmatch('[a-z0-9-]+',x) for x in families)):p.error('invalid families')
    plan=prepare(a.vault,families=families,include_primary=a.include_primary)
    with a.output.open('x',encoding='utf8') as f:json.dump(plan,f,indent=2);f.write('\n')
    print(json.dumps({'family':plan['policy']['family'],'files':len(plan['files']),
                      'logical_bytes':plan['logical_bytes'],'available_by_family':plan['policy']['available_by_family'],
                      'refused':len(plan['policy']['refused'])}))
