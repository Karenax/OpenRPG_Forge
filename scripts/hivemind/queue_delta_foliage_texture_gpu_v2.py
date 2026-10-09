"""Keep the v1 pin refusal and compile current engine texture helpers exactly."""
from pathlib import Path
import datetime
import hashlib
import json

BASE = Path(__file__).with_name('queue_delta_foliage_texture_gpu_v1.py')
SCRIPT = BASE.read_text('utf-8')
W = Path('F:/OpenRPG_ImportWork')
REFUSAL = W / 'delta-foliage-texture-gpu-preparation-refusal-v1'


def sha(path):
    with Path(path).open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


if not REFUSAL.exists():
    assert not (W / 'delta-foliage-texture-gpu-v1').exists()
    REFUSAL.mkdir()
    (REFUSAL / BASE.name).write_bytes(BASE.read_bytes())
    with (REFUSAL / 'RESULT.v1.json').open('x', encoding='utf-8') as stream:
        json.dump(dict(status='old_probe_engine_pin_refused_before_project_creation',
            observed_utc=datetime.datetime.now(datetime.timezone.utc).isoformat(), actual_prepare_exit_code=1,
            script_sha256=sha(BASE), refused_path='E:/OpenRPG_Forge/OpenRPG_Forge-Rust/crates/forge_engine/src/source_graph_imported_texture.rs',
            reason='Current engine has BC4 support and tests absent from the old pinned copy.',
            native_gpu_capture_executed=False, source_storage_changed=False), stream, indent=2)

SCRIPT = SCRIPT.replace("ROOT = W / 'delta-foliage-texture-gpu-v1'", "ROOT = W / 'delta-foliage-texture-gpu-v2'")
before = "check({row['path']: row['sha256'] for row in prior_pins})"
assert SCRIPT.count(before) == 1
SCRIPT = SCRIPT.replace(before, "check({row['path']: row['sha256'] for row in prior_pins if Path(row['path']).is_relative_to(OLD)})")
before = "shutil.copyfile(OLD / 'src' / name, ROOT / 'src' / name)"
assert SCRIPT.count(before) == 1
SCRIPT = SCRIPT.replace(before, "source = {'imported_texture.rs': REPO / 'crates/forge_engine/src/source_graph_imported_texture.rs', 'sampler.rs': REPO / 'crates/forge_engine/src/source_graph_sampler.rs'}.get(name, OLD / 'src' / name)\n        shutil.copyfile(source, ROOT / 'src' / name)\n        pins[str(source)] = sha(source)")
before = "pins.update({str(p): sha(p) for p in [CAPTURE / 'export/RESULT.json',"
assert SCRIPT.count(before) == 1
SCRIPT = SCRIPT.replace(before, "pins[str(BASE)] = sha(BASE)\n    pins.update({str(p): sha(p) for p in [CAPTURE / 'export/RESULT.json',")
exec(compile(SCRIPT, str(BASE), 'exec'))
