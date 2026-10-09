"""Recheck the repaired actual affine module; preserve failed v1 evidence."""
import datetime
import hashlib
import json
from pathlib import Path
import subprocess
import sys

ROOT = Path('F:/OpenRPG_ImportWork/source-graph-position-frame-native-v2')
OLD = ROOT.with_name('source-graph-position-frame-native-v1')
SOURCE = Path('E:/OpenRPG_Forge/OpenRPG_Forge-Rust/crates/forge_engine/src/source_graph_position_frame.rs')


def sha(path):
    with Path(path).open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


def main():
    previous = json.loads((OLD / 'RUN-RESULT.v1.json').read_text('utf-8-sig'))
    assert previous['actual_exit_code'] == 101 and previous['finished_utc']
    assert '3 passed; 1 failed;' in (OLD / 'native.log').read_text('utf-8-sig')
    old_pins = json.loads((OLD / 'PREPARED.v1.json').read_text('utf-8-sig'))['input_pins']
    assert sha(OLD / 'FAILED-SOURCE-POSITION-FRAME.v1.rs') == old_pins[str(SOURCE)]
    ROOT.mkdir(exist_ok=False)
    (ROOT / 'src').mkdir()
    (ROOT / 'temp').mkdir()
    (ROOT / 'src/main.rs').write_bytes((OLD / 'src/main.rs').read_bytes())
    (ROOT / 'Cargo.lock').write_bytes((OLD / 'Cargo.lock').read_bytes())
    cargo = (OLD / 'Cargo.toml').read_text('utf-8-sig')
    cargo += '\n[profile.dev.package.openrpg-source-position-frame-check]\ndebug=0\n'
    (ROOT / 'Cargo.toml').write_text(cargo, encoding='utf-8')
    (ROOT / 'controller.py').write_bytes((OLD / 'controller.py').read_bytes())
    pins = {str(path): sha(path) for path in [SOURCE, Path(__file__).resolve(),
        ROOT / 'src/main.rs', ROOT / 'Cargo.toml', ROOT / 'controller.py',
        OLD / 'RUN-RESULT.v1.json', OLD / 'native.log', OLD / 'FAILED-SOURCE-POSITION-FRAME.v1.rs']}
    prepared = dict(input_pins=pins, expected_tests=4, full_import=False,
                    whole_engine_entrypoint_is_not_compiled_by_this_harness=True,
                    original_failure_preserved=True, root_test_debuginfo=0,
                    dependencies_reused_without_profile_changes=True)
    (ROOT / 'PREPARED.v1.json').write_text(json.dumps(prepared, indent=2), encoding='utf-8')
    with (ROOT / 'controller.log').open('xb') as log:
        child = subprocess.Popen([sys.executable, '-B', str(ROOT / 'controller.py')],
                                 cwd=ROOT, stdout=log, stderr=log, stdin=subprocess.DEVNULL,
                                 creationflags=subprocess.CREATE_NO_WINDOW)
    (ROOT / 'JOB.v1.json').write_text(json.dumps(dict(controller_pid=child.pid,
        created_utc=datetime.datetime.now(datetime.timezone.utc).isoformat())), encoding='utf-8')
    print(json.dumps(dict(controller_pid=child.pid, expected_tests=4, full_import=False)))


if __name__ == '__main__':
    main()
