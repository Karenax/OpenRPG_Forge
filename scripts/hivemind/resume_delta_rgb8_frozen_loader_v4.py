"""Complete eight exact include_str dependencies; retain failed build and cache."""
from pathlib import Path
import datetime
import importlib.util
import json
import shutil
import subprocess
import sys

HERE = Path(__file__).resolve().parent
BASE = HERE / 'check_delta_rgb8_frozen_loader_v1.py'
spec = importlib.util.spec_from_file_location('frozen_loader', BASE)
native = importlib.util.module_from_spec(spec)
spec.loader.exec_module(native)
W = Path('F:/OpenRPG_ImportWork')
OLD = W / 'delta-rgb8-frozen-canonical-loader-v3'
ROOT = W / 'delta-rgb8-frozen-canonical-loader-v4'
SNAPSHOT = W / 'delta-rgb8-frozen-canonical-loader-v1/snapshot'
native.ROOT = ROOT

def prepare():
    assert not ROOT.exists()
    previous = native.read(OLD / 'RUN-RESULT.v1.json')
    assert previous.get('finished_utc') and previous['actual_exit_code'] == 101
    assert 'due to 8 previous errors' in (OLD / 'native.stderr.log').read_text('utf-8-sig')
    prepared = native.read(OLD / 'PREPARED.v1.json')
    native.check(prepared['input_pins'])
    pins = dict(prepared['input_pins'])
    for path in [BASE, Path(__file__), OLD / 'RUN-RESULT.v1.json', OLD / 'native.stderr.log',
                 OLD / 'Cargo.lock', OLD / 'Cargo.toml', OLD / 'src/main.rs']:
        pins[str(path)] = native.sha(path)
    names = ['environment', 'player_controller', 'terrain', 'vegetation', 'water', 'weather', 'world-sketch']
    relatives = [Path('examples/visual_valley') / (name+'.v1.json') for name in names]
    relatives.append(Path('examples/gate0/proof.scene.json'))
    copies = []
    for relative in relatives:
        source = native.REPO / relative
        target = SNAPSHOT / relative
        assert source.is_file() and not source.is_symlink() and not target.exists()
        copies.append(dict(original=str(source), copied=str(target), sha256=native.sha(source)))
    ROOT.mkdir()
    (ROOT / 'src').mkdir()
    (ROOT / 'temp').mkdir()
    for name in ['Cargo.toml', 'Cargo.lock', 'src/main.rs']:
        shutil.copyfile(OLD / name, ROOT / name)
        pins[str(ROOT/name)] = native.sha(ROOT/name)
    for row in copies:
        target = Path(row['copied'])
        target.parent.mkdir(parents=True, exist_ok=True)
        with target.open('xb') as output:
            output.write(Path(row['original']).read_bytes())
        assert native.sha(target) == row['sha256'] == native.sha(row['original'])
        pins[str(target)] = row['sha256']
    native.check(pins)
    native.save('SNAPSHOT.v1.json', dict(previous_snapshot=str(OLD/'SNAPSHOT.v1.json'),
        previous_snapshot_sha256=native.sha(OLD/'SNAPSHOT.v1.json'), added_embedded_examples=copies,
        frozen_input_pins=pins, prior_failure_preserved=True, existing_frozen_sources_reused=True,
        existing_lock_reused=True, expected_host_tests=17, canonical_checkout_unchanged=True,
        full_import=False), new=True)
    native.save('PREPARED.v1.json', dict(input_pins=pins, wait_pid=28340, wait_birth_ticks='0',
        target=prepared['target'], expected_tests=17, canonical_checkout_unchanged=True,
        full_import=False), new=True)
    with (ROOT/'controller.log').open('xb') as output:
        child = subprocess.Popen([sys.executable, '-B', str(Path(__file__).resolve()), 'run'],
            cwd=ROOT, stdin=subprocess.DEVNULL, stdout=output, stderr=output,
            creationflags=subprocess.CREATE_NO_WINDOW)
    native.save('JOB.v1.json', dict(controller_pid=child.pid,
        created_utc=datetime.datetime.now(datetime.timezone.utc).isoformat()), new=True)
    print(json.dumps(dict(controller_pid=child.pid, original_include_files_added=8,
                         frozen_sources_and_cache_reused=True, expected_tests=17)))

if __name__ == '__main__':
    native.run() if len(sys.argv)>1 and sys.argv[1]=='run' else prepare()
