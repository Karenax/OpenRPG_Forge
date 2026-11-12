"""Reconstruct and verify a Unity source export with duplicates locked."""
import argparse
import contextlib
import json
from pathlib import Path

from exclusive_source_delete import ExclusiveSource
from unity_shared_overlay import configured_resolver,materialize,cleanup,changed_overlay_rows
from verify_unity_overlay_guids import verify
import vault_storage as storage

WORK=Path('E:/OpenRPG_Forge/.build/asset-conversion')


def run(vault,package_source,audit,expected,destination,temporary_proof,previous=None):
    vault=vault.resolve(strict=True);work=WORK.resolve(strict=True)
    destination=destination.resolve(strict=False);temporary_proof=temporary_proof.resolve(strict=False)
    if destination.parent!=work or temporary_proof.parent!=work or destination.exists() or temporary_proof.exists():
        raise ValueError('new direct work-root destinations required')
    package=storage.confined(vault,vault/package_source);resolver=configured_resolver(vault,package)
    if resolver is None:raise ValueError('published overlay missing')
    doc=json.loads(resolver.manifest_raw);root=doc['source_root']+'/';rows=[]
    selected=changed_overlay_rows(doc,json.loads(previous.read_bytes()),expected) if previous else [r for r in doc['files'] if not r['vault_path'].casefold().startswith(root.casefold())]
    for row in selected:rows.append((storage.confined(vault,resolver.source_root/row['logical']),row))
    if len(rows)!=expected:raise ValueError('unexpected duplicate count')
    with contextlib.ExitStack() as stack:
        for path,row in rows:
            handle=stack.enter_context(ExclusiveSource(path))
            if handle.sha256()!=row['sha256']:raise ValueError('original changed')
        exported=materialize(vault,resolver.manifest_path,destination,work)
        temporary_proof.write_text(json.dumps(exported),encoding='utf8')
        guid=verify(resolver.manifest_path,audit,destination)
        if resolver.manifest_path.read_bytes()!=resolver.manifest_raw:raise ValueError('manifest changed')
    cleaned=cleanup(resolver.manifest_path,temporary_proof,destination,work)
    temporary_proof.unlink()
    return {'state':'passed','old_paths_locked':len(rows),'files_reconstructed':exported['files'],
            'logical_bytes':exported['logical_bytes'],'guids':guid['guids'],
            'guid_file_references':guid['guid_file_references'],'manifest_sha256':exported['manifest_sha256'],
            'cleanup_free_delta_observed':cleaned['free_delta_observed'],
            'limitations':'Reconstruction and text GUIDs verified; full Unity project import not exercised in this run.'}


if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__)
    for name in ('vault','audit','destination','temporary-proof','output'):p.add_argument('--'+name,type=Path,required=True)
    p.add_argument('--package-source',required=True);p.add_argument('--expected',type=int,required=True);p.add_argument('--previous-manifest',type=Path);a=p.parse_args()
    if a.output.exists():p.error('output exists')
    result=run(a.vault,a.package_source,a.audit,a.expected,a.destination,a.temporary_proof,a.previous_manifest)
    with a.output.open('x',encoding='utf8') as f:json.dump(result,f,indent=2);f.write('\n')
    print(json.dumps(result))
