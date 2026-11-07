"""Verify the actual contact adapter on all 24 pinned physics carriers."""
from pathlib import Path
import datetime, hashlib, json, os, subprocess
R=Path(__file__).resolve().parents[2]
W=Path('F:/OpenRPG_ImportWork')
O=W/'source-physics-contacts-v745'
B=Path('C:/Users/karen/.rustup/toolchains/1.98.0-x86_64-pc-windows-msvc/bin')
def sha(path):
    with Path(path).open('rb') as stream:return hashlib.file_digest(stream,'sha256').hexdigest()
def main():
    assert not O.exists()
    O.mkdir();(O/'src').mkdir();(O/'temp').mkdir()
    seed=W/'house-transport-mount-v726/Cargo.toml'
    text=seed.read_text('utf-8').replace('house-transport-mount-v726','source-physics-contacts-v745')
    text=text.replace('[dependencies]','[dependencies]\nserde={version="=1.0.229",features=["derive"]}')
    (O/'Cargo.toml').write_text(text,'utf-8')
    template=R/'scripts/hivemind/candidates/VerifyPhysicsContactsV745.rs'
    (O/'src/main.rs').write_text(template.read_text('utf-8').replace('@@ROOT@@',R.as_posix()),'utf-8')
    env=os.environ.copy();env.update(RUSTC=str(B/'rustc.exe'),RUSTDOC=str(B/'rustdoc.exe'),CARGO_INCREMENTAL='0',CARGO_TARGET_DIR=str(W/'firepit-static-glb-product-check-v2/target'),TEMP=str(O/'temp'),TMP=str(O/'temp'))
    with (O/'lock.log').open('wb') as log:
        assert subprocess.call([str(B/'cargo.exe'),'generate-lockfile','--offline'],cwd=O,env=env,stdout=log,stderr=log)==0
    files=[Path(__file__),template,seed,O/'Cargo.toml',O/'Cargo.lock',O/'src/main.rs',W/'physics-package-v727/PACKAGES.v1.json']
    files += [R/'crates/forge_engine/src'/name for name in ['source_colliders.rs','source_collider_projection.rs','source_graph_scene.rs','source_physics_contacts.rs']]
    files += [R/'crates/forge_assets/src'/name for name in ['source_graph_glb.rs','source_physics_transport.rs','source_physics_package.rs']]
    rows=json.loads((W/'physics-package-v727/PACKAGES.v1.json').read_text('utf-8'))
    for row in rows:
        carrier=W/'house-runtime-state-glb-v675'/f"{row['prefab_guid']}.native-state.glb"
        package=W/'physics-package-v727'/row['file']
        assert sha(carrier)==row['carrier_sha256'] and sha(package)==row['sha256']
        files += [carrier,package]
    pins={str(path):sha(path) for path in files}
    (O/'INPUT-PINS.v1.json').write_text(json.dumps(pins,indent=2),'utf-8')
    with (O/'cargo.log').open('wb') as log:
        proc=subprocess.Popen([str(B/'cargo.exe'),'run','--locked','--offline','-j','1'],cwd=O,env=env,stdout=log,stderr=log)
        (O/'JOB.v1.json').write_text(json.dumps(dict(pid=proc.pid)),'utf-8')
        code=proc.wait()
    unchanged=all(sha(path)==digest for path,digest in pins.items())
    result=dict(status='source_contacts_verified' if code==0 and unchanged else 'failed',exit_code=code,unchanged_input_pins=unchanged,input_pins=pins,utc=datetime.datetime.now(datetime.timezone.utc).isoformat(),native_simulation_qualified=False,studio_visual_verified=False,full_import_increment=0)
    (O/'RESULT.v1.json').write_text(json.dumps(result,indent=2),'utf-8')
    print(json.dumps({key:value for key,value in result.items() if key!='input_pins'}))
if __name__=='__main__':main()
