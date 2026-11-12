"""Qualify current readers on an isolated, disposable F: model copy."""
import hashlib
import json
import os
from pathlib import Path
import shutil
import stat
import subprocess
import sys

import vault_storage as storage

vault=Path('F:/OpenRPG_AssetVault').resolve(strict=True)
campaign=Path('F:/OpenRPG_Forge/Preuves-et-analyses/actives/audit-variantes-hdrp-urp-20261110').resolve(strict=True)
pilot=campaign/'qualification-lecteurs-stockage-pipeline-light-20261112'
report=Path('E:/OpenRPG_Forge/.build/asset-conversion/qualification-stockage-pipeline-light-pilot-20261112.json')
source=vault/'normalized/hivemind/cyberpunk-city-hdrp-s-basictextured-portable378-v2/models/prefab-sm-balcon-concrete-plate-long-abc00000000018271482341142197234.glb'
expected='3b05dc486cdc75af43a0ca835f5d3d435b32fa2a21e785f54704debd50ce6dae'
cli=Path('E:/OpenRPG_Forge/.build/cargo-target/release/examples/vault_storage.exe')
studio=Path('E:/OpenRPG_Forge/.build/frontend/studio-native/forge-studio.exe')
inspector=Path('E:/CodexData/skills/openrpg-import-assets/scripts/glb_inspect.py')
writer=Path(__file__).with_name('vault_storage.py')
if pilot.exists() or report.exists():raise ValueError('pilot or report already exists')
if pilot.resolve(strict=False).parent!=campaign or campaign.is_symlink():raise ValueError('invalid pilot directory')
storage.confined(Path('F:/').resolve(strict=True),campaign)
source=storage.confined(vault,source)
source_physical=storage.sha(source.read_bytes())
data=storage.read_bytes(vault,source)
if storage.sha(data)!=expected or data[:4]!=b'glTF':raise ValueError('active source does not match')
pilot.mkdir();model=pilot/'pilot.glb'
with model.open('xb') as stream:
    stream.write(data);stream.flush();os.fsync(stream.fileno())
worker=storage.Packer(pilot)
try:
    item=worker.prepare(model,expected_sha=expected);worker.close_pack();worker.publish(item,verify_cli=cli)
finally:worker.close()
if model.read_bytes()[:8]!=storage.MAGIC or storage.digest(pilot,model)!=expected:raise ValueError('python logical reader mismatch')
run=subprocess.run([str(cli),'verify',str(pilot),str(model),'536870912'],capture_output=True,text=True,encoding='utf8')
if run.returncode or json.loads(run.stdout).get('sha256')!=expected:raise ValueError('native reader mismatch: '+run.stderr)
sys.path.insert(0,str(inspector.parent));import glb_inspect
os.environ['OPENRPG_ASSET_VAULT']=str(pilot)
if glb_inspect.digest(model)!=expected:raise ValueError('import reader mismatch')
if storage.sha(source.read_bytes())!=source_physical:raise ValueError('active source changed')
digest=lambda path:hashlib.sha256(path.read_bytes()).hexdigest()
proof={'state':'passed','source':str(source),'logical_sha256':expected,'source_physical_sha256':source_physical,
       'studio_sha256':digest(studio),'reader_cli_sha256':digest(cli),'python_import_reader_sha256':digest(inspector),
       'storage_writer_sha256':digest(writer),'bytes':len(data),
       'limitations':'Disposable storage round trip and native/Python readers; not visual or runtime qualification.'}
if pilot.resolve(strict=True).parent!=campaign or pilot.is_symlink():raise ValueError('pilot location changed')
for directory,subdirs,files in os.walk(pilot,followlinks=False):
    for name in subdirs+files:
        path=Path(directory)/name; info=path.lstat()
        if path.is_symlink() or getattr(info,'st_file_attributes',0)&stat.FILE_ATTRIBUTE_REPARSE_POINT:
            raise ValueError('reparse point in pilot; cleanup refused')
shutil.rmtree(pilot)
proof['temporary_files_removed']=not pilot.exists()
with report.open('x',encoding='utf8') as out:json.dump(proof,out,indent=2);out.write('\n')
print(json.dumps(proof))
