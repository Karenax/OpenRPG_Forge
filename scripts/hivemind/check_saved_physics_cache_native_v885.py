"""Run the actual UI cache module's tests; ordinary native Studio excludes its UI."""
from pathlib import Path
from datetime import datetime, timezone
import hashlib, json, os, shutil, subprocess, tomllib

R = Path(__file__).resolve().parents[2]
W = Path('F:/OpenRPG_ImportWork')
O = W/'saved-physics-cache-tests-v885'
B = Path('C:/Users/karen/.rustup/toolchains/1.98.0-x86_64-pc-windows-msvc/bin')
SOURCE = R/'apps/forge_studio/src/authoring_assets/vault/saved_physics_cache.rs'
assert not O.exists()
O.mkdir();(O/'src').mkdir();(O/'temp').mkdir()

def sha(path):
    with Path(path).open('rb') as stream:
        return hashlib.file_digest(stream,'sha256').hexdigest()

name = 'openrpg-saved-physics-cache-v885'
cargo = f'[package]\nname="{name}"\nversion="0.1.0"\nedition="2024"\n[workspace]\n[dependencies]\n'
for crate in ['forge_core','forge_assets']:
    cargo += f'{crate}={{path="{(R/"crates"/crate).as_posix()}"}}\n'
cargo += '[lib]\npath="src/lib.rs"\n'
(O/'Cargo.toml').write_text(cargo,'utf-8')
(O/'src/lib.rs').write_text(f'#[path="{SOURCE.as_posix()}"] mod saved_physics_cache;\n','utf-8')
shutil.copyfile(R/'Cargo.lock',O/'Cargo.lock')
env = os.environ.copy()
env.update(RUSTC=str(B/'rustc.exe'),RUSTDOC=str(B/'rustdoc.exe'),CARGO_INCREMENTAL='0',
           CARGO_TARGET_DIR='F:/OpenRPG_Forge/relocated-work/Rust-target',TEMP=str(O/'temp'),TMP=str(O/'temp'))
before = tomllib.loads((O/'Cargo.lock').read_text('utf-8'))
with (O/'lock.log').open('wb') as log:
    code = subprocess.call([str(B/'cargo.exe'),'update','--offline','--workspace'],cwd=O,env=env,stdout=log,stderr=log)
assert code == 0
after = tomllib.loads((O/'Cargo.lock').read_text('utf-8'))
identity = lambda d:{(p['name'],p['version'],p.get('source')) for p in d['package'] if p['name'] != name}
assert identity(after).issubset(identity(before))
files = [Path(__file__),SOURCE,R/'Cargo.lock',O/'Cargo.toml',O/'Cargo.lock',O/'src/lib.rs']
for crate in ['forge_core','forge_assets']:
    files += [p for p in (R/'crates'/crate).rglob('*') if p.is_file() and (p.suffix in {'.rs','.toml'} or p.name=='build.rs')]
pins = {str(p):sha(p) for p in files}
(O/'INPUT-PINS.v1.json').write_text(json.dumps(pins,indent=2),'utf-8')
command = [str(B/'cargo.exe'),'test','--locked','--offline','-j','1','--lib','saved_physics_cache::tests::']
with (O/'cargo.log').open('wb') as log:
    proc = subprocess.Popen(command,cwd=O,env=env,stdout=log,stderr=log)
    (O/'JOB.v1.json').write_text(json.dumps(dict(pid=proc.pid,command=command)),'utf-8')
    code = proc.wait()
changed = [p for p,h in pins.items() if sha(p) != h]
log = (O/'cargo.log').read_text('utf-8',errors='replace')
executed = '3 passed; 0 failed' in log and all(t in log for t in [
    'same_model_and_manifest_keep_distinct_packages_and_asset_identities',
    'mismatched_packet_is_refused_without_evicting_a_verified_resident',
    'bounded_eviction_preserves_live_handles_and_recent_reads'])
result = dict(status='actual_historical_packet_cache_tests_verified' if code == 0 and executed and not changed else 'cache_tests_failed_or_inputs_changed',
              exit_code=code,three_actual_tests_executed=executed,changed_inputs=changed,
              actual_studio_gui_verified=False,full_import=False,complete_conversion_increment=0,
              utc=datetime.now(timezone.utc).isoformat())
(O/'RESULT.v1.json').write_text(json.dumps(result,indent=2),'utf-8')
print(json.dumps(result))
