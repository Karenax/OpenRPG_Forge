"""Check the real engine after the exact contact comparison correction."""
from pathlib import Path
import datetime, hashlib, json, os, subprocess

R = Path(__file__).resolve().parents[2]
W = Path('F:/OpenRPG_ImportWork')
O = W / 'source-physics-contact-fix-v752'
B = Path('C:/Users/karen/.rustup/toolchains/1.98.0-x86_64-pc-windows-msvc/bin')

def sha(path):
    with Path(path).open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()

assert O.is_dir() and not (O / 'JOB.v1.json').exists()
(O / 'temp').mkdir()
files = [Path(__file__), R / 'Cargo.toml', R / 'Cargo.lock']
files += [p for p in (R / 'crates').rglob('*') if p.is_file() and p.suffix in ['.rs', '.toml', '.wgsl']]
pins = {str(p): sha(p) for p in files}
(O / 'INPUT-PINS.v1.json').write_text(json.dumps(pins, indent=2), 'utf-8')
env = os.environ.copy()
env.update(RUSTC=str(B / 'rustc.exe'), RUSTDOC=str(B / 'rustdoc.exe'), CARGO_INCREMENTAL='0', CARGO_TARGET_DIR=str(W / 'house-rapier-box-query-v693/target'), TEMP=str(O / 'temp'), TMP=str(O / 'temp'))
command = [str(B / 'cargo.exe'), 'clippy', '--locked', '--offline', '-j', '1', '-p', 'forge_engine', '--lib', '--', '-D', 'warnings']
with (O / 'clippy.log').open('wb') as log:
    proc = subprocess.Popen(command, cwd=R, env=env, stdout=log, stderr=log)
    (O / 'JOB.v1.json').write_text(json.dumps({'pid': proc.pid, 'command': command}), 'utf-8')
    code = proc.wait()
changed = [p for p, h in pins.items() if sha(p) != h]
result = dict(status='engine_clippy_verified' if code == 0 and not changed else 'failed_or_inputs_changed', exit_code=code, changed_inputs=changed, utc=datetime.datetime.now(datetime.timezone.utc).isoformat(), studio_visual_verified=False, full_import_increment=0)
(O / 'RESULT.v1.json').write_text(json.dumps(result, indent=2), 'utf-8')
print(json.dumps(result))
