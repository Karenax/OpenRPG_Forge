"""Run existing storage/mip/sampler tests against the exact staged RGB8 candidate."""
from pathlib import Path
import datetime
import importlib.util
import json
import os
import subprocess
import sys

HERE = Path(__file__).resolve().parent
spec = importlib.util.spec_from_file_location('rgb8_check', HERE / 'check_delta_rgb8_transport_v3.py')
base = importlib.util.module_from_spec(spec)
spec.loader.exec_module(base)
SOURCE = base.ROOT
ROOT = SOURCE.parent / 'delta-rgb8-native-regressions-v1'


def main():
    assert not ROOT.exists()
    pins = base.read(SOURCE / 'PREPARED.v1.json')['input_pins']
    base.check(pins)
    expected = sum(p.read_text('utf-8-sig').count('#[test]') for p in (SOURCE / 'src').glob('*.rs'))
    assert expected == 15
    ROOT.mkdir()
    env = os.environ.copy()
    env.update(TEMP=str(ROOT), TMP=str(ROOT), CARGO_TARGET_DIR=str(base.TARGET), CARGO_INCREMENTAL='0')
    with (ROOT / 'native.log').open('xb') as log:
        code = subprocess.run(['C:/Users/karen/.cargo/bin/cargo.exe', '+1.98.0', 'test', '--locked', '--offline',
                '--manifest-path', str(SOURCE / 'Cargo.toml'), '-j1', '--', '--nocapture'],
            cwd=ROOT, env=env, stdout=log, stderr=log, creationflags=subprocess.CREATE_NO_WINDOW).returncode
    output = (ROOT / 'native.log').read_text('utf-8-sig')
    terminal = dict(actual_exit_code=code, expected_tests=expected, source_candidate=str(SOURCE),
                    native_log_sha256=base.sha(ROOT / 'native.log'), input_pins=pins,
                    gpu_rendered=False, canonical_renderer_installed=False, full_import=False)
    try:
        assert code == 0
        assert 'test result: ok. 15 passed; 0 failed;' in output
        base.check(pins)
        terminal.update(status='fifteen_actual_storage_texture_sampler_tests_passed', tests_passed=15)
    except Exception as error:
        terminal.update(status='refused_or_unverified', error=type(error).__name__ + ': ' + str(error), tests_passed=0)
    terminal['finished_utc'] = datetime.datetime.now(datetime.timezone.utc).isoformat()
    with (ROOT / 'RUN-RESULT.v1.json').open('x', encoding='utf-8') as stream:
        json.dump(terminal, stream, indent=2)
    print(json.dumps(dict(status=terminal['status'], tests_passed=terminal['tests_passed'], full_import=False)))


if __name__ == '__main__':
    main()
