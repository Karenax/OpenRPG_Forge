"""Verify published overlay GUID reads while the duplicate paths are locked."""
import argparse
import contextlib
import json
from pathlib import Path

from exclusive_source_delete import ExclusiveSource
from unity_shared_overlay import UnitySourceResolver,configured_resolver,changed_overlay_rows
from verify_unity_overlay_guids import verify
import vault_storage as storage


def run(vault,source_package,audit_path,expected,previous=None):
    vault=vault.resolve(strict=True);package=storage.confined(vault,vault/source_package)
    resolver=configured_resolver(vault,package)
    if resolver is None:raise ValueError('published overlay missing')
    doc=json.loads(resolver.manifest_raw);root=doc['source_root']+'/';rows=[]
    selected=changed_overlay_rows(doc,json.loads(previous.read_bytes()),expected) if previous else [r for r in doc['files'] if not r['vault_path'].casefold().startswith(root.casefold())]
    for row in selected:rows.append((storage.confined(vault,resolver.source_root/row['logical']),row))
    if len(rows)!=expected:raise ValueError('unexpected duplicate count')
    pointer=storage.confined(vault,vault/'metadata/unity-shared-overlay-registry.v1.json');before=pointer.read_bytes()
    with contextlib.ExitStack() as stack:
        for path,row in rows:
            handle=stack.enter_context(ExclusiveSource(path))
            if handle.sha256()!=row['sha256']:raise ValueError('original SHA changed')
        result=verify(resolver.manifest_path,audit_path,resolver=resolver)
        if pointer.read_bytes()!=before:raise ValueError('overlay registry changed')
    return dict(result,old_paths_locked=len(rows),registry_sha256=storage.sha(before),
                limitations='GUID text audit only; other readers and Unity import not exercised under locks.')


if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__)
    for name in ('vault','audit','output'):p.add_argument('--'+name,type=Path,required=True)
    p.add_argument('--package-source',required=True);p.add_argument('--expected',type=int,required=True);p.add_argument('--previous-manifest',type=Path);a=p.parse_args()
    if a.output.exists():p.error('output exists')
    result=run(a.vault,a.package_source,a.audit,a.expected,a.previous_manifest)
    with a.output.open('x',encoding='utf8') as f:json.dump(result,f,indent=2);f.write('\n')
    print(json.dumps(result))
