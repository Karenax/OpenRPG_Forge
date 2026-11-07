"""Run the actual Studio reconciliation module without a simulated renderer."""
from pathlib import Path
import datetime, hashlib, json, os, subprocess

R = Path(__file__).resolve().parents[2]
O = Path('F:/OpenRPG_ImportWork/placement-physics-reuse-tests-v787')
B = Path('C:/Users/karen/.rustup/toolchains/1.98.0-x86_64-pc-windows-msvc/bin')
assert not O.exists()
O.mkdir(); (O/'src').mkdir(); (O/'temp').mkdir()
module = R/'apps/forge_studio/src/authoring_placement_reuse.rs'
(O/'src/main.rs').write_text(
    '#[allow(dead_code)]\n#[path="'+module.as_posix()+'"]\nmod authoring_placement_reuse;\nfn main() {}\n', 'utf-8')
(O/'Cargo.toml').write_text('''[package]
name = "placement-physics-reuse-tests-v787"
version = "0.1.0"
edition = "2024"
[dependencies]
forge_core = { path = "'''+(R/'crates/forge_core').as_posix()+'''" }
forge_project = { path = "'''+(R/'crates/forge_project').as_posix()+'''" }
''', 'utf-8')
env = os.environ.copy()
env.update(RUSTC=str(B/'rustc.exe'), RUSTDOC=str(B/'rustdoc.exe'), CARGO_INCREMENTAL='0',
    CARGO_TARGET_DIR='F:/OpenRPG_Forge/relocated-work/Rust-target', TEMP=str(O/'temp'), TMP=str(O/'temp'))
with (O/'lock.log').open('wb') as log:
    assert subprocess.call([str(B/'cargo.exe'),'generate-lockfile','--offline'],cwd=O,env=env,stdout=log,stderr=log)==0
def sha(p):
    with Path(p).open('rb') as f:
        return hashlib.file_digest(f,'sha256').hexdigest()
files = [Path(__file__), module, R/'Cargo.toml', R/'Cargo.lock', O/'Cargo.toml', O/'Cargo.lock', O/'src/main.rs']
for crate in ['forge_core','forge_project','forge_gameplay']:
    files.extend(p for p in (R/'crates'/crate).rglob('*') if p.is_file() and p.suffix in ['.rs','.toml'])
pins = {str(p):sha(p) for p in files}
(O/'INPUT-PINS.v1.json').write_text(json.dumps(pins,indent=2),'utf-8')
command = [str(B/'cargo.exe'),'test','--locked','--offline','-j','1','--','--nocapture']
with (O/'cargo.log').open('wb') as log:
    proc = subprocess.Popen(command,cwd=O,env=env,stdout=log,stderr=log)
    (O/'JOB.v1.json').write_text(json.dumps(dict(pid=proc.pid,command=command)),'utf-8')
    code = proc.wait()
changed = [p for p,h in pins.items() if sha(p)!=h]
result = dict(status='actual_studio_placement_reuse_tests_verified' if code==0 and not changed else 'failed_or_inputs_changed',
    exit_code=code,changed_inputs=changed,utc=datetime.datetime.now(datetime.timezone.utc).isoformat(),
    scope='Actual Studio root reconciliation decisions; no scene mount, GUI, or runtime simulation',
    studio_visual_verified=False,full_import_increment=0)
(O/'RESULT.v1.json').write_text(json.dumps(result,indent=2),'utf-8')
print(json.dumps(result))
