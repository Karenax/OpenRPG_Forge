"""Find exact references to candidate mesh and meta files in active catalogs."""
import argparse
import json
from pathlib import Path
import vault_storage as storage


def norm(s):return str(s).replace('\\','/').casefold()


def scan(value,targets,document,location,found):
    if isinstance(value,str):
        key=norm(value)
        if key in targets:found[targets[key]].append({'document':document,'field':location})
    elif isinstance(value,list):
        for n,item in enumerate(value):scan(item,targets,document,f'{location}/{n}',found)
    elif isinstance(value,dict):
        for key,item in value.items():scan(item,targets,document,f'{location}/{key}',found)


def audit(vault,source):
    vault=vault.resolve(strict=True);input_raw=source.read_bytes();data=json.loads(input_raw)
    paths=set()
    for row in data['results']:
        if row['state']!='identical_dependencies':continue
        for pair in row['dependencies']:
            for side in ('hdrp','urp'):
                path=pair[side];paths.update((path,path+'.meta'))
    targets={};found={p:[] for p in paths}
    for path in paths:
        for key in (norm(path),norm(vault/path)):
            if key in targets and targets[key]!=path:raise ValueError('ambiguous paths')
            targets[key]=path
    reg=storage.confined(vault,vault/'metadata/asset-import-registry.v1.json')
    support=storage.confined(vault,vault/'metadata/asset-source-support-registry.v1.json')
    rb,sb=reg.read_bytes(),support.read_bytes()
    if storage.sha(sb)!=data['source_registry_sha256']:raise ValueError('source registry changed')
    sp=json.loads(sb);sr=storage.confined(vault,vault/sp['path']).read_bytes()
    if storage.sha(sr)!=sp['sha256']:raise ValueError('source catalog changed')
    scan(json.loads(sr),targets,sp['path'],'',found)
    catalogs={}
    for pack,entry in sorted(json.loads(rb)['packs'].items()):
        for name in ('details.v1.json','profiles.v1.json'):
            path=storage.confined(vault,vault/entry['path']/name);raw=path.read_bytes()
            if storage.sha(raw)!=entry['files'][name]:raise ValueError('active pack SHA changed')
            doc=json.loads(raw);scan(doc,targets,str(path),'',found)
            if name=='details.v1.json':
                for card in doc['assets']:
                    cat=storage.confined(vault,Path(card['source_catalog']))
                    if cat not in catalogs:
                        cr=cat.read_bytes();catalogs[cat]=storage.sha(cr)
                        scan(json.loads(cr),targets,str(cat),'',found)
    for path,pin in catalogs.items():
        if storage.sha(path.read_bytes())!=pin:raise ValueError('active catalog changed')
    if reg.read_bytes()!=rb or support.read_bytes()!=sb or source.read_bytes()!=input_raw:raise ValueError('inputs changed')
    return {'state':'audit_only','model_registry_sha256':storage.sha(rb),'source_registry_sha256':storage.sha(sb),
            'source_sha256':storage.sha(input_raw),'active_catalogs_checked':len(catalogs),'paths':found,
            'limitations':'Exact strings in active JSON only; Unity GUID and external references are not covered.'}


if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__)
    for name in ('vault','source','output'):p.add_argument('--'+name,type=Path,required=True)
    a=p.parse_args();result=audit(a.vault,a.source)
    with a.output.open('x',encoding='utf8') as f:json.dump(result,f,ensure_ascii=False,indent=2);f.write('\n')
    print(json.dumps({'paths':len(result['paths']),'references':sum(len(x) for x in result['paths'].values()),
                      'active_catalogs_checked':result['active_catalogs_checked']}))
