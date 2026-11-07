"""Preserve v99 refusal and prepare corrected API plus actual Web clock features."""
from pathlib import Path
from datetime import datetime, timezone
import hashlib
import json
import shutil

R = Path('E:/OpenRPG_Forge/OpenRPG_Forge-Rust')
W = Path('F:/OpenRPG_ImportWork')
OLD = W / 'srgb-browser-producer-v99'
O = W / 'srgb-browser-producer-v105'
assert not O.exists()
prior = json.loads((OLD / 'RUN-RESULT.v1.json').read_text(encoding='utf-8'))
assert prior['status'] == 'browser_probe_refusal_retained' and prior['build_exit_code'] == 101
engine_manifest = R / 'crates/forge_engine/Cargo.toml'
assert 'web = ["bevy/web", "bevy/webgpu", "physics-rapier"]' in engine_manifest.read_text(encoding='utf-8')
O.mkdir()
for name in ['src', 'site', 'temp']:
    (O / name).mkdir()
manifest = (OLD / 'Cargo.toml').read_text(encoding='utf-8')
assert manifest.count('features=["3d","ui","webgpu"]') == 1
manifest = manifest.replace('features=["3d","ui","webgpu"]', 'features=["3d","ui","web","webgpu"]')
(O / 'Cargo.toml').write_bytes(manifest.encode())
shutil.copy2(OLD / 'Cargo.lock', O / 'Cargo.lock')
source = (OLD / 'src/lib.rs').read_text(encoding='utf-8')
old = 'wgpu::InstanceDescriptor{backends:wgpu::Backends::BROWSER_WEBGPU,..Default::default()}'
new = 'wgpu::InstanceDescriptor{backends:wgpu::Backends::BROWSER_WEBGPU,..wgpu::InstanceDescriptor::new_without_display_handle()}'
assert source.count(old) == 1
(O / 'src/lib.rs').write_bytes(source.replace(old, new).encode())
shutil.copy2(OLD / 'site/index.html', O / 'site/index.html')
runner_old = R / 'scripts/hivemind/run_srgb_browser_probe_v99.py'
runner = R / 'scripts/hivemind/run_srgb_browser_probe_v105.py'
assert not runner.exists()
text = runner_old.read_text(encoding='utf-8').replace('srgb-browser-producer-v99', 'srgb-browser-producer-v105')
runner.write_bytes(text.encode())
modules = [R / 'crates/forge_engine/src' / name for name in
           ['source_graph_srgb_curve.rs', 'source_graph_srgb_measure.rs', 'source_graph_srgb_async.rs']]
paths = [Path(__file__), runner, runner_old, engine_manifest, R / 'Cargo.lock',
         *modules, OLD / 'RUN-RESULT.v1.json', OLD / 'build.stderr.log',
         O / 'Cargo.toml', O / 'src/lib.rs', O / 'site/index.html']
pins = {str(path): hashlib.sha256(path.read_bytes()).hexdigest() for path in paths}
(O / 'PREPARED.v1.json').write_text(json.dumps(dict(
    status='browser_probe_corrected_api_and_web_clock_prepared',
    prepared_utc=datetime.now(timezone.utc).isoformat(), input_pins=pins,
    previous_refusal_preserved=str(OLD / 'RUN-RESULT.v1.json'),
    core_producer_source_changed=False, browser_gpu_executed=False,
    actual_studio_web_clock_feature_enabled=True, full_import=False,
), indent=2) + '\n', encoding='utf-8')
print(str(O))
