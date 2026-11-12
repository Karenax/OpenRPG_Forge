"""Verify a pinned Unity overlay resolver against its still-present originals."""
import argparse
import json
from pathlib import Path
import vault_storage as storage
from unity_shared_overlay import UnitySourceResolver, file_pin


def verify(vault, manifest):
    raw=manifest.read_bytes();pin=storage.sha(raw);doc=json.loads(raw)
    resolver=UnitySourceResolver(vault,manifest,pin);checked=[]
    for row in doc['files']:
        logical=resolver.source_root / Path(row['logical'])
        retained=storage.confined(resolver.vault,resolver.vault/row['vault_path'])
        if logical == retained: continue
        original=storage.confined(resolver.vault,logical)
        if file_pin(original)!=(row['sha256'],row['bytes']):raise ValueError('logical original changed')
        actual=resolver.resolve(row['logical'])
        if actual!=retained:raise ValueError('resolved unexpected path')
        checked.append({'logical':row['logical'],'sha256':row['sha256'],'bytes':row['bytes']})
    if not checked or manifest.read_bytes()!=raw:raise ValueError('empty or changed manifest')
    return {'state':'passed','manifest_sha256':pin,'checked_files':len(checked),
            'logical_bytes':sum(x['bytes'] for x in checked),'files':checked,
            'limitations':'Resolver comparison only; no conversion consumer or deletion qualified.'}


if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__)
    for name in ('vault','manifest','output'):p.add_argument('--'+name,type=Path,required=True)
    a=p.parse_args()
    if a.output.exists():p.error('output exists')
    result=verify(a.vault,a.manifest)
    with a.output.open('x',encoding='utf8') as f:json.dump(result,f,indent=2);f.write('\n')
    print(json.dumps({k:v for k,v in result.items() if k!='files'}))
