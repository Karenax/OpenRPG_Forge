"""Install the tested LOD source importer while holding its publication lock."""
import hashlib
import os
from pathlib import Path
import shutil
from publish_source_support_relinks import importer_module

old='d6920349177afd6c51d08df0a377c707df4c6279b9f4823a1175abb01e4aadc7'
new='809f5a1c23ba2eb7381d31d6b20822ec39227ab23e3ffd7fbe91e9db067be22a'
target=Path('E:/CodexData/skills/openrpg-import-assets/scripts/source_support_import.py')
candidate=Path('E:/OpenRPG_Forge/.build/asset-conversion/source-support-import-pipeline-light-candidate-20261112.py')
backup=Path('E:/OpenRPG_Forge/.build/asset-conversion/source-support-import-before-pipeline-light-20261112.py')
pending=target.with_name('source_support_import.lod-pending.py')
digest=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
importer=importer_module(old)
if digest(candidate)!=new or backup.exists() or pending.exists():raise ValueError('candidate changed or installation already started')
with importer.shared_import_lock(Path('F:/OpenRPG_AssetVault')):
    if digest(target)!=old:raise ValueError('shared importer changed')
    shutil.copyfile(target,backup)
    if digest(backup)!=old:raise ValueError('backup mismatch')
    with pending.open('xb') as stream:
        stream.write(candidate.read_bytes());stream.flush();os.fsync(stream.fileno())
    if digest(pending)!=new or digest(target)!=old:raise ValueError('install input changed')
    os.replace(pending,target)
    if digest(target)!=new:raise ValueError('installed importer differs')
print('installed',new)
