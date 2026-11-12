"""Stage additional exact Unity asset mappings without changing the Vault."""
import argparse
import copy
import json
from pathlib import Path

from unity_shared_overlay import UnitySourceResolver,file_pin
import vault_storage as storage


def extend(vault,manifest,comparison):
    vault=vault.resolve(strict=True);mr=manifest.read_bytes();cr=comparison.read_bytes()
    result=copy.deepcopy(json.loads(mr));comp=json.loads(cr)
    if result.get('schema')!='openrpg.unity-shared-overlay.v1' or comp.get('state')!='audit_only':
        raise ValueError('invalid overlay or comparison')
    pointer=storage.confined(vault,vault/'metadata/asset-source-support-registry.v1.json');before=pointer.read_bytes()
    if storage.sha(before)!=comp['source_registry_sha256']:raise ValueError('source registry changed')
    resolver=UnitySourceResolver(vault,manifest,storage.sha(mr));by_name={x['logical'].casefold():x for x in result['files']}
    if len(by_name)!=len(result['files']):raise ValueError('duplicate overlay path')
    changed=set()
    for pair in comp['results']:
        if pair['state']!='identical_dependencies':raise ValueError('unqualified source pair')
        for dep in pair['dependencies']:
            old=storage.confined(vault,vault/dep['hdrp']);keep=storage.confined(vault,vault/dep['urp'])
            if not old.is_relative_to(resolver.source_root) or old.suffix.lower() not in {'.prefab','.mat'}:
                raise ValueError('candidate outside overlay scope')
            for suffix,key in (('','sha256'),('.meta','meta_sha256')):
                a=Path(str(old)+suffix);b=Path(str(keep)+suffix);rel=a.relative_to(resolver.source_root).as_posix()
                row=by_name.get(rel.casefold());pin=dep['hdrp_info'][key]
                if (not row or rel.casefold() in changed or row['sha256']!=pin or row['vault_path'].casefold()!=a.relative_to(vault).as_posix().casefold()
                        or file_pin(a)!=(pin,row['bytes']) or file_pin(b)!=(pin,row['bytes'])):
                    raise ValueError('overlay source pair differs')
                row['vault_path']=b.relative_to(vault).as_posix();changed.add(rel.casefold())
    if len(changed)!=24:raise ValueError('unexpected candidate count')
    result['shared_files']+=len(changed)
    result['additional_comparison_sha256']=[*result.get('additional_comparison_sha256',[]),storage.sha(cr)]
    result['limitations']='Prepared only; additional GUID and consumer verification required before removal.'
    if manifest.read_bytes()!=mr or comparison.read_bytes()!=cr or pointer.read_bytes()!=before:raise ValueError('inputs changed')
    return result


if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__)
    for name in ('vault','manifest','comparison','output'):p.add_argument('--'+name,type=Path,required=True)
    a=p.parse_args()
    if a.output.exists():p.error('output exists')
    result=extend(a.vault,a.manifest,a.comparison)
    with a.output.open('x',encoding='utf8') as f:json.dump(result,f,indent=2);f.write('\n')
    print(json.dumps({'state':'prepared_only','shared_files':result['shared_files'],'added_files':24}))
