"""Count exact candidate archive paths in active model registry and catalogs."""
import argparse
import collections
import hashlib
import json
import os
from pathlib import Path
import vault_storage as storage


def matching_targets(value, document, vault, targets, suffixes=None):
    normalized=value.replace('\\','/');matches=set()
    target=targets.get(normalized.casefold())
    if target:matches.add(target)
    if suffixes is not None and not normalized.casefold().endswith(suffixes):
        return matches
    relative=Path(value)
    if not relative.is_absolute():
        bases=[vault]
        bases.extend(parent for parent in document.parents
                     if parent != vault and parent.is_relative_to(vault))
        for base in bases:
            target=targets.get(os.path.normpath(str(base/relative)).replace('\\','/').casefold())
            if target:matches.add(target)
    return matches


def catalog_references(value):
    stack=[value]
    while stack:
        item=stack.pop()
        if isinstance(item,dict):
            for key,child in item.items():
                if key=='source_catalog' and isinstance(child,str):yield child
                elif isinstance(child,(dict,list)):stack.append(child)
        elif isinstance(item,list):stack.extend(item)


def audit(vault, inventory):
    vault=vault.resolve(strict=True);inv_raw=inventory.read_bytes();inv=json.loads(inv_raw)
    targets={}
    for group in inv['groups']:
        for row in group['files']:
            path=Path(row['path'])
            if not path.is_absolute() or not path.is_relative_to(vault):raise ValueError('archive outside Vault')
            for key in (str(path).replace('\\','/').casefold(),path.relative_to(vault).as_posix().casefold()):
                targets[key]=str(path)
    pointer=storage.confined(vault,vault/'metadata/asset-import-registry.v1.json');raw=pointer.read_bytes();reg=json.loads(raw)
    suffix_set={Path(value).suffix.casefold() for value in targets.values()}
    suffixes=tuple(suffix_set) if suffix_set and '' not in suffix_set else None
    counts=collections.Counter();fields=collections.defaultdict(collections.Counter);documents=collections.defaultdict(set);checked={};unresolved=[]
    def scan(obj,document):
        stack=[('',obj,document)]
        while stack:
            field,value,context=stack.pop()
            if isinstance(value,dict):
                catalog=value.get('source_catalog')
                candidate=Path(catalog) if isinstance(catalog,str) else None
                if candidate is not None and candidate.is_absolute() and candidate.is_relative_to(vault):
                    context=candidate
                stack.extend((key,child,context) for key,child in value.items())
            elif isinstance(value,list):stack.extend((field,x,context) for x in value)
            elif isinstance(value,str):
                for target in matching_targets(value,context,vault,targets,suffixes):
                    counts[target]+=1;fields[target][field]+=1;documents[target].add(str(document))
    scan(reg,pointer)
    for pack,entry in sorted(reg['packs'].items()):
        path=storage.confined(vault,vault/entry['path']/'details.v1.json');data=path.read_bytes()
        if storage.sha(data)!=entry['files']['details.v1.json']:raise ValueError('model snapshot changed')
        checked[path]=storage.sha(data);details=json.loads(data);scan(details,path)
        for reference in set(catalog_references(details)):
            path=Path(reference)
            if not path.is_absolute() or not path.is_relative_to(vault) or not path.is_file():
                unresolved.append(reference);continue
            path=storage.confined(vault,path)
            if path not in checked:
                data=path.read_bytes();checked[path]=storage.sha(data);scan(json.loads(data),path)
    if pointer.read_bytes()!=raw or inventory.read_bytes()!=inv_raw:raise ValueError('input changed')
    for path,pin in checked.items():
        if storage.sha(path.read_bytes())!=pin:raise ValueError('active catalog changed')
    unique=sorted(set(targets.values()))
    return {'state':'active_model_reference_audit','registry_sha256':storage.sha(raw),
            'inventory_sha256':storage.sha(inv_raw),'checked_documents':len(checked),
            'archives':[{'path':x,'references':counts[x],'fields':dict(fields[x]),'documents':sorted(documents[x])} for x in unique],
            'unresolved_catalogs':sorted(set(unresolved)),
            'limitations':'Active model snapshots and reachable top-level or nested source catalogs; other consumers and path conventions not checked.'}


if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__)
    for n in ('vault','inventory','output'):p.add_argument('--'+n,type=Path,required=True)
    a=p.parse_args();result=audit(a.vault,a.inventory)
    with a.output.open('x',encoding='utf8') as f:json.dump(result,f,indent=2);f.write('\n')
    print(json.dumps({'state':result['state'],'checked_documents':result['checked_documents'],'archives':result['archives']}))
