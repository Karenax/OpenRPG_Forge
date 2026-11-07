"""Verify all current thumbnail requests through the actual native library reader."""
from pathlib import Path
import datetime, hashlib, json, os, subprocess
R=Path(__file__).resolve().parents[2];A=R/'apps/forge_studio/src'
W=Path('F:/OpenRPG_ImportWork');V=Path('F:/OpenRPG_AssetVault');O=W/'current-native-library-images-v778'
B=Path('C:/Users/karen/.rustup/toolchains/1.98.0-x86_64-pc-windows-msvc/bin')
assert not O.exists();O.mkdir();(O/'src').mkdir();(O/'temp').mkdir()
def sha(p):
    with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
reader=(A/'asset_library_native.rs').read_text('utf-8')
for name in ['history','physics_transport']:
    reader=reader.replace('mod '+name+';','#[path="'+(A/'asset_library_native'/(name+'.rs')).as_posix()+'"] mod '+name+';')
(O/'src/asset_library_native.rs').write_text(reader,'utf-8')
template=R/'scripts/hivemind/candidates/VerifyNativeLibraryImagesV778.rs'
(O/'src/main.rs').write_text(template.read_text('utf-8').replace('@@A@@',A.as_posix()),'utf-8')
(O/'Cargo.toml').write_text((W/'saved-physics-reader-v772/Cargo.toml').read_text('utf-8').replace('saved-physics-reader-v772','current-native-library-images-v778'),'utf-8')
env=os.environ.copy();env.update(RUSTC=str(B/'rustc.exe'),RUSTDOC=str(B/'rustdoc.exe'),CARGO_INCREMENTAL='0',CARGO_TARGET_DIR=str(W/'firepit-static-glb-product-check-v2/target'),TEMP=str(O/'temp'),TMP=str(O/'temp'))
with (O/'lock.log').open('wb') as log:assert subprocess.call([str(B/'cargo.exe'),'generate-lockfile','--offline'],cwd=O,env=env,stdout=log,stderr=log)==0
files=[Path(__file__),template,A/'asset_library_native.rs',A/'asset_library_native/history.rs',A/'asset_library_native/physics_transport.rs',A/'asset_library_protocol.rs',A/'asset_library_model_wire.rs',O/'Cargo.toml',O/'Cargo.lock',O/'src/main.rs',O/'src/asset_library_native.rs',V/'metadata/asset-import-registry.v1.json',V/'metadata/asset-source-support-registry.v1.json']
pins={str(p):sha(p) for p in files};(O/'INPUT-PINS.v1.json').write_text(json.dumps(pins,indent=2),'utf-8')
command=[str(B/'cargo.exe'),'run','--locked','--offline','-j','1','--',str(V),str(O)]
with (O/'cargo.log').open('wb') as log:
    proc=subprocess.Popen(command,cwd=O,env=env,stdout=log,stderr=log);(O/'JOB.v1.json').write_text(json.dumps(dict(pid=proc.pid,command=command)),'utf-8');code=proc.wait()
changed=[p for p,h in pins.items() if sha(p)!=h]
result=dict(status='current_native_primary_images_read_verified' if code==0 and not changed else 'failed_or_inputs_changed',exit_code=code,changed_inputs=changed,utc=datetime.datetime.now(datetime.timezone.utc).isoformat(),scope='Search pagination and declared primary image reads; missing images remain missing',studio_visual_verified=False,full_import_increment=0)
(O/'RESULT.v1.json').write_text(json.dumps(result,indent=2),'utf-8');print(json.dumps(result))

