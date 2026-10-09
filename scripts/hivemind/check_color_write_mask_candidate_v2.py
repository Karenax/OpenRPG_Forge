"""Retry only the stopped mask check with the exact qualified 1.98 toolchain."""
from pathlib import Path
import ast
import importlib.util
import sys

BASE = Path(__file__).with_name('check_color_write_mask_candidate_v1.py')
spec = importlib.util.spec_from_file_location('mask_check_pinned', BASE)
probe = importlib.util.module_from_spec(spec)
spec.loader.exec_module(probe)
OLD = probe.ROOT
probe.ROOT = probe.W / 'source-graph-color-write-mask-native-check-v2'
BIN = Path('C:/Users/karen/.rustup/toolchains/1.98.0-x86_64-pc-windows-msvc/bin')
probe.CARGO = BIN / 'cargo.exe'

def main():
    prior = probe.read(OLD / 'RUN-RESULT.v1.json')
    assert prior['finished_utc'] and prior['status'] == 'refused_or_unverified'
    assert prior['tests_passed'] == 0 and prior['actual_exit_code'] != 0
    stop = probe.read(OLD / 'CONTROLLED-STOP.v1.json')
    assert stop['reason'] == 'stable toolchain differs from pinned 1.98.0 baseline'
    assert not stop['deleted_files']
    functions = [n for n in ast.parse(BASE.read_text('utf-8-sig')).body
        if isinstance(n, ast.FunctionDef) and n.name in {'prepare', 'run'}]
    code = ast.unparse(ast.Module(body=functions, type_ignores=[]))
    anchor = 'CARGO_TARGET_DIR=str(TARGET)'
    assert code.count(anchor) == 1
    code = code.replace(anchor, "CARGO_TARGET_DIR=str(TARGET), RUSTC=str(BIN / 'rustc.exe')")
    anchor = "for p in [ROOT / 'src/lib.rs', ROOT / 'Cargo.toml', ROOT / 'Cargo.lock']:"
    assert code.count(anchor) == 1
    code = code.replace(anchor, "for p in [BASE, BIN / 'cargo.exe', BIN / 'rustc.exe', OLD / 'RUN-RESULT.v1.json', OLD / 'CONTROLLED-STOP.v1.json', ROOT / 'src/lib.rs', ROOT / 'Cargo.toml', ROOT / 'Cargo.lock']:")
    namespace = dict(probe.__dict__)
    namespace.update(__file__=str(Path(__file__).resolve()), BASE=BASE, BIN=BIN, OLD=OLD)
    exec(compile(code, str(Path(__file__).resolve()), 'exec'), namespace)
    namespace['run' if len(sys.argv) > 1 and sys.argv[1] == 'run' else 'prepare']()

if __name__ == '__main__':
    main()
