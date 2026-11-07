"""Seal the common collision decoder's check against pinned private carriers."""
import datetime
import hashlib
import json
import os
from pathlib import Path
import subprocess

R = Path(__file__).resolve().parents[2]
O = Path('F:/OpenRPG_ImportWork/source-collision-geometry-v708')
RECEIPT = Path('F:/OpenRPG_ImportWork/house-runtime-state-glb-v675/RESULT.v1.json')
BIN = Path('C:/Users/karen/.rustup/toolchains/1.98.0-x86_64-pc-windows-msvc/bin')


def sha(path):
    with Path(path).open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


assert O.is_dir() and not (O / 'RUN-RESULT.v1.json').exists()
files = [Path(__file__), RECEIPT, R / 'Cargo.toml', R / 'Cargo.lock',
         R / 'crates/forge_assets/Cargo.toml', R / 'crates/forge_assets/src/lib.rs',
         R / 'crates/forge_assets/src/source_collision_geometry.rs',
         R / 'crates/forge_assets/src/source_graph_glb.rs',
         R / 'crates/forge_assets/examples/verify_source_collision_geometry.rs']
pins = {str(path): sha(path) for path in files}
receipt = json.loads(RECEIPT.read_text('utf-8-sig'))
for row in receipt['rows']:
    assert sha(row['glb']) == row['sha256']
    pins[row['glb']] = row['sha256']
state = {'status': 'running', 'input_pins': pins, 'full_import_increment': 0,
         'physics_admitted': False, 'studio_verified': False}


def save():
    state['utc'] = datetime.datetime.now(datetime.timezone.utc).isoformat()
    tmp = O / 'RUN-RESULT.tmp'
    tmp.write_text(json.dumps(state, indent=2), 'utf-8')
    tmp.replace(O / 'RUN-RESULT.v1.json')


save()
env = os.environ.copy()
env.update(RUSTC=str(BIN / 'rustc.exe'), RUSTDOC=str(BIN / 'rustdoc.exe'),
           CARGO_INCREMENTAL='0', TEMP=str(O), TMP=str(O),
           CARGO_TARGET_DIR='F:/OpenRPG_ImportWork/source-graph-private-boundary-v1/target-rust198-explicit-domain-v8')
command = [str(BIN / 'cargo.exe'), 'run', '--locked', '--offline', '-j', '1',
           '-p', 'forge_assets', '--example', 'verify_source_collision_geometry',
           '--', str(RECEIPT), pins[str(RECEIPT)]]
with (O / 'STDOUT.log').open('wb') as out, (O / 'STDERR.log').open('wb') as err:
    process = subprocess.Popen(command, cwd=R, env=env, stdout=out, stderr=err)
    state['pid'] = process.pid
    state['command'] = command
    save()
    code = process.wait()
state['exit_code'] = code
state['unchanged_input_pins'] = all(sha(p) == h for p, h in pins.items())
if code == 0 and state['unchanged_input_pins']:
    result = json.loads((O / 'STDOUT.log').read_text('utf-8-sig'))
    assert result['carriers'] == len(receipt['rows']) == 24
    assert result['mesh_bindings'] == 19
    assert result['decoded_integrity_verified'] and not result['physics_admitted']
    state.update(status='common_collision_geometry_verified',
                 carriers=result['carriers'], mesh_bindings=result['mesh_bindings'],
                 vertices=result['vertices'], triangles=result['triangles'],
                 output_sha256=sha(O / 'STDOUT.log'))
else:
    state['status'] = 'verification_failed'
save()
print(json.dumps({k: v for k, v in state.items() if k != 'input_pins'}, indent=2))
raise SystemExit(0 if state['status'] == 'common_collision_geometry_verified' else 1)
