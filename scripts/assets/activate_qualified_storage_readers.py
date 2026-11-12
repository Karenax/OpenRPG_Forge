"""Activate a newly qualified storage-reader receipt under the writer lock."""
import hashlib
import json
import os
import re
from pathlib import Path

import vault_storage as storage

vault=Path('F:/OpenRPG_AssetVault').resolve(strict=True)
work=Path('E:/OpenRPG_Forge/.build/asset-conversion')
pilot=work/'qualification-stockage-pipeline-light-pilot-20261112.json'
python_tests=work/'qualification-stockage-witch-python-tests-20261112.log'
campaign=Path('F:/OpenRPG_Forge/Preuves-et-analyses/actives/audit-variantes-hdrp-urp-20261110')
studio_tests=campaign/'test-studio-stockage-pipeline-light-v2-20261112.log'
delivery_tests=campaign/'test-delivery-stockage-standalone-20261112.log'
build=campaign/'build-source-pipeline-light-alias-v2-20261112.log'
receipt_path=vault/'.forge-storage/deployed-readers.v1.json'
previous=Path('E:/OpenRPG_Forge/Preuves-et-analyses/rapports/audit-variantes-hdrp-urp-20261110/lecteurs-stockage-recu-avant-pipeline-light-20261112.json')
output=work/'qualification-stockage-pipeline-light-receipt-active-20261112.json'
studio=Path('E:/OpenRPG_Forge/.build/frontend/studio-native/forge-studio.exe')
reader=Path('E:/OpenRPG_Forge/.build/cargo-target/release/examples/vault_storage.exe')
inspector=Path('E:/CodexData/skills/openrpg-import-assets/scripts/glb_inspect.py')
writer=Path(__file__).with_name('vault_storage.py')
proof=json.loads(pilot.read_bytes())
if proof.get('state')!='passed' or proof.get('temporary_files_removed') is not True:raise ValueError('pilot not complete')
if 'Ran 17 tests' not in python_tests.read_text(encoding='utf8') or '\nOK' not in python_tests.read_text(encoding='utf8'):
    raise ValueError('Python tests incomplete')
if '4 passed; 0 failed' not in studio_tests.read_text(encoding='utf8'):
    raise ValueError('Studio storage tests incomplete')
if '13 passed; 0 failed; 1 ignored' not in delivery_tests.read_text(encoding='utf8'):
    raise ValueError('native storage tests incomplete')
build_log=re.sub(r'\x1b\[[0-9;]*m','',build.read_text(encoding='utf8'))
build_receipt=json.loads(Path(str(build)+'.receipt.json').read_bytes())
if ('Finished `dev` profile' not in build_log or build_receipt.get('exit_code')!=0
        or build_receipt.get('arguments')!=['build','-p','forge_studio','--bin','forge-studio']):
    raise ValueError('Studio build evidence missing')
digest=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
for key,path in (('studio_sha256',studio),('reader_cli_sha256',reader),('python_import_reader_sha256',inspector),('storage_writer_sha256',writer)):
    if digest(path)!=proof[key]:raise ValueError('qualified reader changed: '+key)
if previous.exists() or output.exists():raise ValueError('qualification output already exists')
receipt={'state':'ready','vault':str(vault),'utc':storage.utc(),
         'studio':str(studio),'studio_sha256':proof['studio_sha256'],
         'reader_cli':str(reader),'reader_cli_sha256':proof['reader_cli_sha256'],
         'python_import_reader':str(inspector),'python_import_reader_sha256':proof['python_import_reader_sha256'],
         'storage_writer_sha256':proof['storage_writer_sha256'],
         'qualification_pilot':str(pilot),'qualification_tests':[str(python_tests),str(studio_tests),str(delivery_tests)],
         'scope':'Lossless storage readers only; no visual or runtime qualification. One Windows 8.3 alias test ignored.'}
storage.verify_deployed_readers(receipt,vault,reader)
worker=storage.Packer(vault)
try:
    before=storage.confined(vault,receipt_path).read_bytes()
    with previous.open('xb') as stream:stream.write(before);stream.flush();os.fsync(stream.fileno())
    if previous.read_bytes()!=before:raise ValueError('previous receipt readback differs')
    if receipt_path.read_bytes()!=before:raise ValueError('active receipt changed')
    storage.atomic_json(receipt_path,receipt)
    if storage.decode_json(receipt_path.read_bytes())!=receipt:raise ValueError('active receipt readback differs')
finally:
    worker.close()
with output.open('x',encoding='utf8') as stream:json.dump(receipt,stream,indent=2);stream.write('\n')
print(json.dumps({'state':'ready','studio_sha256':receipt['studio_sha256'],'previous_receipt_sha256':storage.sha(before)}))
