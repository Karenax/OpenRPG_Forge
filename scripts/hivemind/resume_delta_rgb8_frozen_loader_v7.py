"""Keep the immutable current snapshot; move the harness outside its workspace."""
from pathlib import Path
import importlib.util
import json
import shutil
import subprocess
import sys

BASE = Path(__file__).with_name('check_delta_rgb8_frozen_loader_v1.py')
spec = importlib.util.spec_from_file_location('frozen_loader', BASE)
native = importlib.util.module_from_spec(spec)
spec.loader.exec_module(native)
OLD = Path('F:/OpenRPG_ImportWork/delta-rgb8-frozen-canonical-loader-v6')
ROOT = OLD.parent / 'delta-rgb8-frozen-canonical-loader-v7'
native.ROOT = ROOT
native.SNAPSHOT = OLD / 'snapshot'

def prepare():
    prior = native.read(OLD / 'RUN-RESULT.v1.json')
    assert prior['actual_exit_code'] == 101 and prior['status'] == 'refused_or_unverified'
    assert '`workspace.package.edition` was not defined' in (OLD / 'native.stderr.log').read_text('utf-8-sig')
    assert not ROOT.exists()
    prepared = native.read(OLD / 'PREPARED.v1.json')
    native.check(prepared['input_pins'])
    ROOT.mkdir(); (ROOT / 'src').mkdir(); (ROOT / 'temp').mkdir()
    for name in ['Cargo.toml', 'Cargo.lock', 'src/main.rs']:
        shutil.copyfile(OLD / name, ROOT / name)
    pins = dict(prepared['input_pins'])
    for p in [Path(__file__).resolve(), BASE, OLD / 'RUN-RESULT.v1.json', OLD / 'native.stderr.log',
              OLD / 'SNAPSHOT.v1.json', ROOT / 'Cargo.toml', ROOT / 'Cargo.lock', ROOT / 'src/main.rs']:
        pins[str(p)] = native.sha(p)
    native.check(pins)
    native.save('SNAPSHOT.v1.json', dict(frozen_input_pins=pins,
        retained_snapshot=str(native.SNAPSHOT), retained_snapshot_receipt_sha256=native.sha(OLD / 'SNAPSHOT.v1.json'),
        original_sources=native.read(OLD / 'SNAPSHOT.v1.json')['original_sources'],
        repair='harness moved outside the copied workspace; all 860 source files retained',
        registry_dependencies_match_actual_application_lock=True,
        current_checkout_unchanged=True, full_import=False), new=True)
    native.save('PREPARED.v1.json', dict(input_pins=pins, target=prepared['target'],
        wait_pid=prior['cargo_pid'], wait_birth_ticks='0', expected_tests=17,
        actual_source_files_copied=prepared['actual_source_files_copied'],
        canonical_checkout_unchanged=True, full_import=False), new=True)
    with (ROOT / 'controller.log').open('xb') as log:
        child = subprocess.Popen([sys.executable, '-B', str(Path(__file__).resolve()), 'run'], cwd=ROOT,
            stdout=log, stderr=log, stdin=subprocess.DEVNULL, creationflags=subprocess.CREATE_NO_WINDOW)
    native.save('JOB.v1.json', dict(controller_pid=child.pid), new=True)
    print(json.dumps(dict(controller_pid=child.pid, retained_sources=860, full_import=False)))

if __name__ == '__main__':
    native.run() if len(sys.argv) > 1 and sys.argv[1] == 'run' else prepare()
