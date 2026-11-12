"""Compare rebuilt Unity GUIDs and known text references to the source audit."""
import argparse
import hashlib
import json
from pathlib import Path,PurePosixPath
from audit_pipeline_texture_guid_references import GUID_REF,TEXT,meta_guid
import vault_storage as storage
from unity_shared_overlay import UnitySourceResolver


def verify(manifest_path,audit_path,tree=None,resolver=None):
    raw=manifest_path.read_bytes();manifest=json.loads(raw);audit_raw=audit_path.read_bytes();audit=json.loads(audit_raw)
    accepted=[manifest['comparison_sha256'],*manifest.get('additional_comparison_sha256',[])]
    if audit['comparison_sha256'] not in accepted:raise ValueError('comparison audit differs')
    if (tree is None)==(resolver is None):raise ValueError('choose either source tree or resolver')
    if tree is not None:tree=tree.resolve(strict=True)
    def read_source(rel):
        if resolver is None:return (tree/rel).read_bytes()
        with resolver.open_verified(rel) as source:return source.read()
    root=PurePosixPath(manifest['source_root']);expected={};actual={}
    manifest_files={x['logical'].casefold():x for x in manifest['files']}
    for item in audit['images']:
        old=PurePosixPath(item['hdrp'])
        if not old.is_relative_to(root):continue
        rel=old.relative_to(root).as_posix();meta=rel+'.meta';row=manifest_files.get(meta.casefold())
        if not row:raise ValueError('metadata absent from manifest')
        data=read_source(meta)
        if hashlib.sha256(data).hexdigest()!=row['sha256'] or meta_guid(data)!=item['guid']:
            raise ValueError('reconstructed GUID differs: '+meta)
        refs=set()
        for ref in item['references']:
            path=PurePosixPath(ref['path'])
            if ref['side']=='hdrp' and path.is_relative_to(root):refs.add(path.relative_to(root).as_posix())
        if item['guid'] in expected:raise ValueError('duplicate GUID')
        expected[item['guid']]=refs;actual[item['guid']]=set()
    if not expected:raise ValueError('no audited shared GUIDs')
    for row in manifest['files']:
        rel=row['logical']
        if Path(rel).suffix.lower() not in TEXT:continue
        content=read_source(rel)
        if hashlib.sha256(content).hexdigest()!=row['sha256']:raise ValueError('reconstructed text differs')
        for guid in set(x.decode('ascii').lower() for x in GUID_REF.findall(content)):
            if guid in actual:actual[guid].add(rel)
    if expected!=actual:raise ValueError('Unity GUID references changed')
    if manifest_path.read_bytes()!=raw or audit_path.read_bytes()!=audit_raw:raise ValueError('inputs changed')
    return {'state':'passed','manifest_sha256':storage.sha(raw),'audit_sha256':storage.sha(audit_raw),
        'guids':len(expected),'guid_file_references':sum(map(len,expected.values())),
        'limitations':'Text GUID comparison only; Unity import and binary references not tested.'}


if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__)
    for name in ('manifest','audit','output'):p.add_argument('--'+name,type=Path,required=True)
    source=p.add_mutually_exclusive_group(required=True)
    source.add_argument('--tree',type=Path);source.add_argument('--vault',type=Path)
    a=p.parse_args()
    resolver=UnitySourceResolver(a.vault,a.manifest,storage.sha(a.manifest.read_bytes())) if a.vault else None
    result=verify(a.manifest,a.audit,a.tree,resolver)
    with a.output.open('x',encoding='utf8') as f:json.dump(result,f,indent=2);f.write('\n')
    print(json.dumps(result))
