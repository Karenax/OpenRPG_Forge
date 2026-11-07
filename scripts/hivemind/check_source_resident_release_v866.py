"""Run actual Studio runtime lifecycle tests; its normal native lib excludes this UI module."""
from pathlib import Path
from datetime import datetime, timezone
import hashlib, json, os, shutil, subprocess, tomllib

R = Path(__file__).resolve().parents[2]
W = Path('F:/OpenRPG_ImportWork')
O = W/'source-resident-release-tests-v866'
SEED = W/'gladiator-source-collider-host-cycle-v496'
B = Path('C:/Users/karen/.rustup/toolchains/1.98.0-x86_64-pc-windows-msvc/bin')
assert not O.exists()
O.mkdir(); (O/'src').mkdir(); (O/'temp').mkdir()

def sha(path):
    with Path(path).open('rb') as stream:
        return hashlib.file_digest(stream,'sha256').hexdigest()

name = 'openrpg-source-resident-release-v866'
cargo = (SEED/'Cargo.toml').read_text('utf-8').split('[[bin]]')[0]
cargo = cargo.replace('openrpg-gladiator-source-collider-host-v496',name)
cargo = cargo.replace('[dependencies]', '[dependencies]\nforge_core={path="'+(R/'crates/forge_core').as_posix()+'"}')
cargo += '[lib]\npath="src/lib.rs"\n'
(O/'Cargo.toml').write_text(cargo,'utf-8')
shutil.copyfile(SEED/'Cargo.lock',O/'Cargo.lock')
modules = ['route','intake','scene','resources','frame','state','context','bootstrap','lights','host','lod','runtime']
paths = [R/'apps/forge_studio/src/authoring_assets'/('source_graph_'+m+'.rs') for m in modules]
source = '\n'.join('#[allow(dead_code)]\n#[path="'+p.as_posix()+'"] mod source_graph_'+m+';' for p,m in zip(paths,modules))
(O/'src/lib.rs').write_text(source,'utf-8')
env = os.environ.copy()
env.update(RUSTC=str(B/'rustc.exe'),RUSTDOC=str(B/'rustdoc.exe'),CARGO_INCREMENTAL='0',
           CARGO_TARGET_DIR=str(W/'firepit-static-glb-product-check-v2/target'),TEMP=str(O/'temp'),TMP=str(O/'temp'))
before = tomllib.loads((O/'Cargo.lock').read_text('utf-8'))
with (O/'lock.log').open('wb') as log:
    code = subprocess.call([str(B/'cargo.exe'),'update','--offline','--workspace'],cwd=O,env=env,stdout=log,stderr=log)
assert code == 0
after = tomllib.loads((O/'Cargo.lock').read_text('utf-8'))
identity = lambda d:{(p['name'],p['version'],p.get('source')) for p in d['package'] if p['name'] != name}
assert identity(after).issubset(identity(before)), 'Dependency version drift'
files = [Path(__file__),SEED/'Cargo.toml',SEED/'Cargo.lock',O/'Cargo.toml',O/'Cargo.lock',O/'src/lib.rs',*paths]
for crate in ['forge_engine','forge_assets','forge_core']:
    files += [p for p in (R/'crates'/crate).rglob('*') if p.is_file() and (p.suffix in {'.rs','.wgsl','.toml'} or p.name=='build.rs')]
pins = {str(p):sha(p) for p in files}
(O/'INPUT-PINS.v1.json').write_text(json.dumps(pins,indent=2),'utf-8')
command = [str(B/'cargo.exe'),'test','--locked','--offline','-j','1','--lib','source_graph_runtime::tests::']
with (O/'cargo.log').open('wb') as log:
    proc = subprocess.Popen(command,cwd=O,env=env,stdout=log,stderr=log)
    (O/'JOB.v1.json').write_text(json.dumps(dict(pid=proc.pid,command=command)),'utf-8')
    code = proc.wait()
changed = [p for p,h in pins.items() if sha(p)!=h]
log = (O/'cargo.log').read_text('utf-8',errors='replace')
two_tests = '2 passed; 0 failed' in log and all(name in log for name in [
    'release_preserves_source_hierarchy_and_foreign_children_and_is_idempotent',
    'vanished_root_releases_an_owned_child_that_lost_its_parent'])
result = dict(status='actual_source_runtime_owned_release_and_orphan_cleanup_verified' if code==0 and not changed and two_tests else 'failed_or_inputs_changed',
              exit_code=code,changed_inputs=changed,two_actual_module_tests_executed=two_tests,
              actual_studio_gui_verified=False,native_simulation_qualified=False,
              full_import=False,complete_conversion_increment=0,finished_utc=datetime.now(timezone.utc).isoformat())
(O/'RESULT.v1.json').write_text(json.dumps(result,indent=2),'utf-8')
print(json.dumps(result))
