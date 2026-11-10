"""Find exact ID references in active, hash-pinned Vault metadata."""
import argparse
import collections
import hashlib
import json
from pathlib import Path


def sha(b):
    return hashlib.sha256(b).hexdigest()


def walk(value, path=''):
    if isinstance(value, str):
        yield path, value
    elif isinstance(value, dict):
        for k, v in value.items():
            yield from walk(v, path + '/' + str(k))
    elif isinstance(value, list):
        for i, v in enumerate(value):
            yield from walk(v, path + '/' + str(i))


def main():
    p=argparse.ArgumentParser()
    p.add_argument('--vault',type=Path,required=True)
    p.add_argument('--ids',type=Path,required=True)
    p.add_argument('--output',type=Path,required=True)
    p.add_argument('--include-catalogues',action='store_true')
    a=p.parse_args(); vault=a.vault.resolve(strict=True)
    ids=a.ids.read_text(encoding='utf-8').splitlines()
    if len(ids)!=len(set(ids)) or not ids:
        raise ValueError('duplicate or empty IDs')
    selected=set(ids); found=collections.defaultdict(list)
    imp_path=vault/'metadata/asset-import-registry.v1.json'
    src_path=vault/'metadata/asset-source-support-registry.v1.json'
    imp_raw,src_raw=imp_path.read_bytes(),src_path.read_bytes()
    imp,src=json.loads(imp_raw),json.loads(src_raw)
    def inspect(doc,label):
        for path,value in walk(doc):
            if value in selected:
                found[value].append(label+path)
    source=(vault/src['path']).read_bytes()
    if sha(source)!=src['sha256']:
        raise ValueError('source catalog SHA mismatch')
    inspect(json.loads(source),'source-support')
    catalogs=set()
    for pack,entry in sorted(imp['packs'].items()):
        for name in ('details.v1.json','profiles.v1.json'):
            raw=(vault/entry['path']/name).read_bytes()
            if sha(raw)!=entry['files'][name]:
                raise ValueError('pack metadata SHA mismatch: '+pack+'/'+name)
            doc=json.loads(raw)
            inspect(doc,'import/'+pack+'/'+name)
            if name=='details.v1.json' and a.include_catalogues:
                for asset in doc.get('assets',[]):
                    if asset.get('source_catalog'):
                        path=Path(asset['source_catalog'])
                        path=(path if path.is_absolute() else vault/path).resolve(strict=True)
                        if not path.is_relative_to(vault):
                            raise ValueError('source catalog outside Vault')
                        catalogs.add(path)
    catalog_hashes={}
    for path in sorted(catalogs):
        raw=path.read_bytes()
        catalog_hashes[str(path)]=sha(raw)
        inspect(json.loads(raw),'source-catalog/'+str(path.relative_to(vault)))
    for path,expected in catalog_hashes.items():
        if sha(Path(path).read_bytes())!=expected:
            raise ValueError('source catalog changed during audit: '+path)
    if imp_path.read_bytes()!=imp_raw or src_path.read_bytes()!=src_raw:
        raise ValueError('registry changed during audit')
    data={'import_registry_sha256':sha(imp_raw),'source_registry_sha256':sha(src_raw),
          'scope':'exact string values in active details, profiles, source support and requested source catalogues',
          'source_catalogue_count':len(catalogs), 'source_catalogue_hashes':catalog_hashes,
          'references':{id:found[id] for id in ids}}
    with a.output.open('x',encoding='utf-8') as f:json.dump(data,f,ensure_ascii=False,indent=2)
    print(json.dumps({'ids':len(ids),'referenced_ids':sum(bool(found[id]) for id in ids),
                      'references':sum(len(found[id]) for id in ids)}))


if __name__=='__main__':
    main()
