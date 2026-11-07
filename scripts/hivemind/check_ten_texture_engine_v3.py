"""Check changed Hivemind engine modules; retain unrelated broad-suite failures."""
from pathlib import Path
from datetime import datetime, timezone
import hashlib
import json
import os
import re
import subprocess

S = Path(__file__).parent
R = S.parents[1]
W = Path('F:/OpenRPG_ImportWork')
ROOT = W / 'source-graph-ten-texture-engine-check-v3'
BIN = Path('C:/Users/karen/.rustup/toolchains/1.98.0-x86_64-pc-windows-msvc/bin')


def sha(path):
    with Path(path).open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


def save(data):
    data['updated_utc'] = datetime.now(timezone.utc).isoformat()
    path = ROOT / 'RUN-RESULT.v1.json'
    temp = path.with_suffix('.tmp')
    temp.write_text(json.dumps(data, indent=2), encoding='utf-8')
    temp.replace(path)


def main():
    assert not ROOT.exists()
    assert subprocess.check_output(['powershell', '-NoProfile', '-Command', '@(Get-Process cargo -ErrorAction SilentlyContinue).Count'], text=True).strip() == '0'
    paths = [R / p for p in ('crates/forge_assets/src/source_graph_bundle.rs', 'crates/forge_engine/src/source_graph.rs',
                             'crates/forge_engine/src/source_graph_glb.rs', 'crates/forge_engine/src/source_graph_program.rs',
                             'crates/forge_engine/src/source_unlit_forward.rs')]
    paths += [Path(__file__), W / 'source-graph-ten-texture-engine-check-v2/RUN-RESULT.v1.json', W / 'source-graph-ten-texture-engine-check-v2/stdout.log']
    pins = {str(p): sha(p) for p in paths}
    ROOT.mkdir()
    (ROOT / 'PREPARED.v1.json').write_text(json.dumps({'input_pins': pins}, indent=2), encoding='utf-8')
    state = {'status': 'ten_texture_engine_binding_tests_running', 'full_import': False, 'complete_assets_delivered_increment': 0,
             'scope': 'source_graph, source_unlit_forward, source_graph_program, source_graph_glb; unrelated Tidewater fixture failures remain retained', 'cases': []}
    env = os.environ.copy()
    env.update(PATH=str(BIN) + os.pathsep + env.get('PATH', ''), RUSTC=str(BIN / 'rustc.exe'), CARGO_BUILD_JOBS='1', CARGO_INCREMENTAL='0',
               CARGO_TARGET_DIR=str(W / 'firepit-static-glb-product-check-v2/target'), TEMP=str(ROOT), TMP=str(ROOT))
    save(state)
    try:
        for label in ('source_graph', 'source_unlit_forward', 'source_graph_program', 'source_graph_glb'):
            command = [str(BIN / 'cargo.exe'), 'test', '--locked', '--offline', '-p', 'forge_engine', '--lib', label + '::']
            (ROOT / (label + '.COMMAND.v1.json')).write_text(json.dumps({'command': command, 'cwd': str(R)}), encoding='utf-8')
            out_path, err_path = ROOT / (label + '.stdout.log'), ROOT / (label + '.stderr.log')
            with out_path.open('xb') as out, err_path.open('xb') as err:
                child = subprocess.Popen(command, cwd=R, env=env, stdout=out, stderr=err, stdin=subprocess.DEVNULL, creationflags=subprocess.CREATE_NO_WINDOW)
                state.update(active_module=label, cargo_pid=child.pid)
                save(state)
                code = child.wait()
            text = out_path.read_text(encoding='utf-8')
            summary = re.search(r'test result: (\w+)\. (\d+) passed; (\d+) failed; (\d+) ignored;', text)
            case = {'module': label, 'actual_exit_code': code, 'passed': int(summary[2]) if summary else None,
                    'failed': int(summary[3]) if summary else None, 'ignored': int(summary[4]) if summary else None}
            state['cases'].append(case)
            assert code == 0 and summary and case['failed'] == 0, case
            for path, expected in pins.items():
                assert sha(path) == expected, path
        state.update(status='ten_texture_engine_binding_tests_passed_inputs_unchanged', actual_exit_code=0,
                     tests_passed=sum(c['passed'] for c in state['cases']), engine_binding_tests_verified=True,
                     actual_material_gpu_layout_verified=False, broad_engine_suite_qualified=False)
    except Exception as error:
        state.update(status='ten_texture_engine_binding_tests_refusal_retained', actual_exit_code=101, error=repr(error))
    save(state)
    print(json.dumps(state))


if __name__ == '__main__':
    main()
