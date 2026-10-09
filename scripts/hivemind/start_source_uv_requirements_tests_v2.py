"""Compile actual UV intake requirements with the real Bevy Reflect crate."""
import datetime
import hashlib
import json
import os
from pathlib import Path
import re
import subprocess
import sys

ROOT = Path('F:/OpenRPG_ImportWork/source-graph-uv-requirements-native-v2')
SOURCE = Path('E:/OpenRPG_Forge/OpenRPG_Forge-Rust/crates/forge_engine/src/source_graph_uv_requirements.rs')
STAGES = Path('F:/OpenRPG_ImportWork/delta-vector-bevy-vertex-stages-v2/INDEPENDENT-REOPEN.v1.json')


def sha(path):
    with Path(path).open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


def save(name, value):
    path = ROOT / name
    temp = path.with_suffix('.tmp')
    temp.write_text(json.dumps(value, indent=2), encoding='utf-8')
    temp.replace(path)


def check(pins):
    for path, expected in pins.items():
        assert sha(path) == expected, 'Pinned source changed: '+path


def prepare():
    stages = json.loads(STAGES.read_text('utf-8-sig'))
    assert stages['native_vertex_programs_reopened'] == len(stages['rows']) == 6
    pins = {str(p): sha(p) for p in [SOURCE, STAGES, Path(__file__).resolve()]}
    includes = []
    for row in stages['rows']:
        assert sha(row['shader']) == row['sha256']
        pins[row['shader']] = row['sha256']
        includes.append('include_str!("'+Path(row['shader']).as_posix()+'")')
    ROOT.mkdir(exist_ok=False)
    (ROOT / 'src').mkdir()
    (ROOT / 'temp').mkdir()
    (ROOT / 'Cargo.toml').write_text(
        '[package]\nname="openrpg-source-uv-requirements-check"\nversion="0.1.0"\nedition="2024"\n'
        '[workspace]\n[dependencies]\nbevy_reflect="=0.19.1"\n', encoding='utf-8')
    (ROOT / 'src/main.rs').write_text(
        'extern crate bevy_reflect as bevy;\n#[path="'+SOURCE.as_posix()+'"]\nmod source_uv_requirements;\nfn main() {}\n'
        '#[test]\nfn six_actual_pinned_bevy_stages_require_original_uv1() {\n'
        'for source in ['+','.join(includes)+'] {\n'
        'let required=source_uv_requirements::SourceGraphUvRequirements::from_shader(source,[12,13,14,15,3,4,5]).unwrap();\n'
        'assert_eq!(required.channels(),2);\n}\n}\n', encoding='utf-8')
    archived = ROOT.with_name('source-graph-uv-requirements-native-v1') / 'PREVIOUS-SOURCE-UV-REQUIREMENTS.v1.rs'
    previous = json.loads((archived.parent / 'RUN-RESULT.v1.json').read_text('utf-8-sig'))
    assert previous['tests_passed'] == 5 and sha(archived) == previous['input_pins'][str(SOURCE)]
    pins[str(archived)] = sha(archived)
    cargo = ROOT / 'Cargo.toml'
    cargo.write_text(cargo.read_text('utf-8') + '\n[profile.dev.package.openrpg-source-uv-requirements-check]\ndebug=0\n', encoding='utf-8')
    main_source = ROOT / 'src/main.rs'
    regression = '\n#[path="' + archived.as_posix() + '"]\nmod previous_uv_requirements;\n'
    regression += '#[test] fn joined_uv0_uv1_declaration_no_longer_suppresses_the_requirement() {\n'
    regression += 'let source="struct SourceGraphVertex {\\n @location(10) source_uv0:vec4<f32>, @location(12) source_uv1:vec4<f32>,\\n}";\n'
    regression += 'assert!(previous_uv_requirements::SourceGraphUvRequirements::from_shader(source,[12,13,14,15,3,4,5]).is_ok());\n'
    regression += 'assert!(source_uv_requirements::SourceGraphUvRequirements::from_shader(source,[12,13,14,15,3,4,5]).is_err());\n}\n'
    main_source.write_text(main_source.read_text('utf-8') + regression, encoding='utf-8')
    for path in [ROOT / 'Cargo.toml', ROOT / 'src/main.rs']:
        pins[str(path)] = sha(path)
    save('PREPARED.v1.json', dict(input_pins=pins, expected_tests=10, actual_stage_cases=6,
                                 isolated_target=str(ROOT.with_name('source-graph-uv-requirements-native-v1') / 'target'), full_import=False))
    with (ROOT / 'controller.log').open('xb') as log:
        child = subprocess.Popen([sys.executable, '-B', str(Path(__file__).resolve()), 'run'],
                                 cwd=ROOT, stdout=log, stderr=log, stdin=subprocess.DEVNULL,
                                 creationflags=subprocess.CREATE_NO_WINDOW)
    save('JOB.v1.json', dict(controller_pid=child.pid,
                            created_utc=datetime.datetime.now(datetime.timezone.utc).isoformat()))
    print(json.dumps(dict(controller_pid=child.pid, expected_tests=10)))


def run():
    state = dict(status='testing_actual_source_uv_requirements', tests_passed=0,
                 full_engine_compiled=False, renderer_installed=False, gpu_rendered=False, full_import=False)
    save('RUN-RESULT.v1.json', state)
    try:
        pins = json.loads((ROOT / 'PREPARED.v1.json').read_text('utf-8'))['input_pins']
        check(pins)
        env = os.environ.copy()
        env.update(CARGO_TARGET_DIR=str(ROOT.with_name('source-graph-uv-requirements-native-v1') / 'target'), CARGO_INCREMENTAL='0',
                   TEMP=str(ROOT / 'temp'), TMP=str(ROOT / 'temp'))
        with (ROOT / 'native.log').open('xb') as log:
            child = subprocess.Popen(['C:/Users/karen/.cargo/bin/cargo.exe','+1.98.0','test','--offline',
                                      '-j1','--','--test-threads=1'], cwd=ROOT, env=env,
                                     stdout=log, stderr=log, stdin=subprocess.DEVNULL,
                                     creationflags=subprocess.CREATE_NO_WINDOW)
            state['cargo_pid'] = child.pid
            save('RUN-RESULT.v1.json', state)
            code = child.wait()
        state['actual_exit_code'] = code
        assert code == 0, 'Actual native compilation/test failure; preserved native.log'
        assert re.search(r'test result: ok\. 10 passed; 0 failed; 0 ignored;',
                         (ROOT / 'native.log').read_text('utf-8-sig'))
        check(pins)
        state.update(status='ten_actual_uv_requirement_tests_passed', tests_passed=10,
                     actual_stage_cases=6, input_pins=pins, native_log_sha256=sha(ROOT / 'native.log'),
                     actual_cargo_lock_sha256=sha(ROOT / 'Cargo.lock'))
    except Exception as error:
        state.update(status='refused_or_interrupted', error=type(error).__name__+': '+str(error))
    state['finished_utc'] = datetime.datetime.now(datetime.timezone.utc).isoformat()
    save('RUN-RESULT.v1.json', state)


if __name__ == '__main__':
    run() if len(sys.argv)>1 and sys.argv[1]=='run' else prepare()
