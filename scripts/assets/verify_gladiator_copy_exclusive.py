"""Verify actual project-source copies while old Unity duplicates are locked."""
import argparse
import contextlib
import hashlib
import importlib.util
import json
from pathlib import Path

from exclusive_source_delete import ExclusiveSource
from unity_shared_overlay import configured_resolver
import vault_storage as storage

REPO=Path(__file__).resolve().parents[2]
WORK=Path('E:/OpenRPG_Forge/.build/asset-conversion')
PACKAGE='originals/unity-purchased/hivemind/source-support-v1/modular-gladiator-arena-base170-v1'


def run(vault,audit_path,destination):
    vault=vault.resolve(strict=True);work=WORK.resolve(strict=True);destination=destination.resolve(strict=False)
    if destination.parent!=work or destination.exists():raise ValueError('new direct work-root destination required')
    source=storage.confined(vault,vault/PACKAGE/'source');resolver=configured_resolver(vault,source)
    if resolver is None:raise ValueError('published overlay unavailable')
    manifest=json.loads(resolver.manifest_raw);root=manifest['source_root']+'/';candidates=[]
    for row in manifest['files']:
        if not row['vault_path'].casefold().startswith(root.casefold()):
            candidates.append((storage.confined(vault,resolver.source_root/row['logical']),row))
    if len(candidates)!=60:raise ValueError('unexpected duplicate set')
    raw=audit_path.read_bytes();audit=json.loads(raw)
    script=REPO/'scripts/hivemind/run_gladiator_native_geometry_v380.py'
    spec=importlib.util.spec_from_file_location('gladiator_exclusive_copy',script)
    module=importlib.util.module_from_spec(spec);spec.loader.exec_module(module);module.resolver=resolver
    destination.mkdir()
    with contextlib.ExitStack() as stack:
        for path,row in candidates:
            handle=stack.enter_context(ExclusiveSource(path))
            if handle.sha256()!=row['sha256']:raise ValueError('original SHA changed')
        pins,copies=module.copy_audited_sources(audit,vault/PACKAGE,destination)
        if audit_path.read_bytes()!=raw:raise ValueError('audit changed')
        if len(copies)!=2*len(audit['resolved_files']):raise ValueError('incomplete copy')
    # A cleanup is permitted only for exactly the files this invocation wrote.
    expected={Path(x['copy']).resolve():x['sha256'] for x in copies}
    entries=list(destination.rglob('*'))
    if any(x.is_symlink() or getattr(x.lstat(),'st_file_attributes',0)&1024 for x in entries):raise ValueError('reparse point in output')
    if {x.resolve() for x in entries if x.is_file()}!=set(expected):raise ValueError('unexpected output file')
    for path,pin in expected.items():
        with path.open('rb') as stream:
            if hashlib.file_digest(stream,'sha256').hexdigest()!=pin:raise ValueError('copy changed before cleanup')
    for path in expected:path.unlink()
    for path in sorted((x for x in entries if x.is_dir()),key=lambda x:len(x.parts),reverse=True):path.rmdir()
    destination.rmdir()
    return {'state':'passed','old_paths_locked':len(candidates),'copied_files':len(copies),
            'audit_sha256':storage.sha(raw),'overlay_manifest_sha256':storage.sha(resolver.manifest_raw),
            'limitations':'Source copying only; Unity import and native geometry extraction not exercised.'}


if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__)
    for n in ('vault','audit','destination','output'):p.add_argument('--'+n,type=Path,required=True)
    a=p.parse_args()
    if a.output.exists():p.error('output exists')
    result=run(a.vault,a.audit,a.destination)
    with a.output.open('x',encoding='utf8') as f:json.dump(result,f,indent=2);f.write('\n')
    print(json.dumps(result))
