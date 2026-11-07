"""Verify canonical physics changes against the full forge_project test suite."""
from pathlib import Path
import datetime, hashlib, json, os, subprocess
R=Path(__file__).resolve().parents[2];W=Path('F:/OpenRPG_ImportWork');O=W/'placement-reviewed-physics-tests-v806'
B=Path('C:/Users/karen/.rustup/toolchains/1.98.0-x86_64-pc-windows-msvc/bin')
def sha(path):
    with Path(path).open('rb') as stream:return hashlib.file_digest(stream,'sha256').hexdigest()
def main():
    assert not O.exists();O.mkdir();(O/'temp').mkdir()
    files=[Path(__file__),R/'Cargo.toml',R/'Cargo.lock']
    for crate in ['forge_project','forge_core','forge_gameplay']:
        files.extend(p for p in (R/'crates'/crate).rglob('*') if p.is_file() and p.suffix in ['.rs','.toml','.json'])
    pins={str(path):sha(path) for path in files};(O/'INPUT-PINS.v1.json').write_text(json.dumps(pins,indent=2),'utf-8')
    env=os.environ.copy();env.update(RUSTC=str(B/'rustc.exe'),RUSTDOC=str(B/'rustdoc.exe'),CARGO_INCREMENTAL='0',CARGO_TARGET_DIR='F:/OpenRPG_Forge/relocated-work/Rust-target',TEMP=str(O/'temp'),TMP=str(O/'temp'))
    command=[str(B/'cargo.exe'),'test','--locked','--offline','-j','1','-p','forge_project','--test','placement_physics','--test','manual_placements','--test','source_object_transitions']
    with (O/'cargo.log').open('wb') as log:
        proc=subprocess.Popen(command,cwd=R,env=env,stdout=log,stderr=log);(O/'JOB.v1.json').write_text(json.dumps(dict(pid=proc.pid,command=command)),'utf-8');code=proc.wait()
    changed=[path for path,digest in pins.items() if sha(path)!=digest]
    result=dict(status='reviewed_physics_placement_regressions_verified' if code==0 and not changed else 'failed_or_inputs_changed',exit_code=code,changed_inputs=changed,input_count=len(pins),utc=datetime.datetime.now(datetime.timezone.utc).isoformat(),studio_visual_verified=False,full_import_increment=0)
    (O/'RESULT.v1.json').write_text(json.dumps(result,indent=2),'utf-8');print(json.dumps(result))
if __name__=='__main__':main()


