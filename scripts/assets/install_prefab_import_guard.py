"""Install the tested importer guard only while the Vault importer is locked."""
import hashlib
import os
from pathlib import Path
import shutil

from publish_model_preview_relinks import load_importer

OLD = 'ef17dadc1f6329613ff4f0744c8b3f86d6390b5792f171d1cb2c2697340db3b8'
NEW = 'f2a3bbe2c4de99927fe9da346efbd36a4c6e02c6c720824ddaace6bf9dcbdc17'
target = Path('E:/CodexData/skills/openrpg-import-assets/scripts/asset_import.py')
candidate = Path('E:/OpenRPG_Forge/.build/asset-conversion/asset-import-prefab-guard-candidate-20261112.py')
backup = Path('E:/OpenRPG_Forge/.build/asset-conversion/asset-import-before-prefab-guard-20261112.py')
temporary = target.with_name('asset_import.prefab-guard-pending.py')
digest = lambda path: hashlib.sha256(path.read_bytes()).hexdigest()
importer = load_importer(OLD)
if digest(candidate) != NEW or backup.exists() or temporary.exists():
    raise ValueError('candidate changed or install already started')
with importer.locked(Path('F:/OpenRPG_AssetVault')):
    if digest(target) != OLD:
        raise ValueError('shared importer changed')
    shutil.copyfile(target, backup)
    if digest(backup) != OLD:
        raise ValueError('backup mismatch')
    with temporary.open('xb') as stream:
        stream.write(candidate.read_bytes()); stream.flush(); os.fsync(stream.fileno())
    if digest(temporary) != NEW or digest(target) != OLD:
        raise ValueError('files changed before install')
    os.replace(temporary, target)
    if digest(target) != NEW:
        raise ValueError('installed importer mismatch')
print('installed', NEW)
