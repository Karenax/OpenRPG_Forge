"""Retain pinned gate tests, distinguishing module proof from browser qualification."""
from pathlib import Path
from datetime import datetime, timezone
import hashlib
import json
import subprocess

R = Path(__file__).resolve().parents[2]
OUT = Path('F:/OpenRPG_ImportWork/model-read-gate-v1')
BIN = Path('C:/Users/karen/.rustup/toolchains/1.98.0-x86_64-pc-windows-msvc/bin')


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main():
    target = OUT / 'RESULT.v1.json'
    assert not target.exists(), 'Preserve prior receipt'
    files = [R / 'apps/forge_studio/src/authoring_assets/model_read_gate.rs',
             R / 'apps/forge_studio/src/authoring_assets/vault.rs',
             R / 'apps/forge_studio/src/authoring_assets.rs', OUT / 'harness.rs']
    pins = {str(p): sha(p) for p in files}
    commands = [
        [str(BIN / 'rustc.exe'), '--edition', '2024', '--test', str(OUT / 'harness.rs'), '-o', str(OUT / 'gate-tests.exe')],
        [str(OUT / 'gate-tests.exe'), '--nocapture'],
        [str(BIN / 'rustc.exe'), '--edition', '2024', '--target', 'wasm32-unknown-unknown', '--crate-type', 'lib', str(OUT / 'harness.rs'), '-o', str(OUT / 'gate-wasm.rlib')],
    ]
    logs, codes = {}, []
    for index, command in enumerate(commands):
        log = OUT / f'PINNED-CHECK-{index}.log'
        with log.open('xb') as stream:
            code = subprocess.run(command, stdout=stream, stderr=subprocess.STDOUT).returncode
        codes.append(code)
        logs[str(log)] = sha(log)
        assert code == 0, log
    assert '3 passed; 0 failed' in (OUT / 'PINNED-CHECK-1.log').read_text('utf-8-sig')
    for path, expected in pins.items():
        assert sha(Path(path)) == expected
    result = dict(status='canonical_fifo_transport_gate_native_tests_and_wasm_module_compile_verified',
                  finished_utc=datetime.now(timezone.utc).isoformat(), actual_tests_passed=3,
                  actual_exit_codes=codes, canonical_sources_installed=True, active_binary_reads_limit=1,
                  pending_metadata_request_limit=128, source_pins=pins, log_pins=logs,
                  whole_frontend_compile_qualified=False, browser_rebuilt=False,
                  actual_browser_qualified=False, resident_scene_memory_qualified=False,
                  gpu_runtime_qualified=False, full_import=False)
    target.write_text(json.dumps(result, indent=2), 'utf-8')
    print(result['status'])


if __name__ == '__main__':
    main()
