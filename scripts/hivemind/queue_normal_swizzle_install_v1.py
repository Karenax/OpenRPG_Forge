"""Install tested normal-profile validator only after the pinned Bevy probe finishes."""
from pathlib import Path
from datetime import datetime, timezone
import hashlib
import json
import os
import subprocess
import sys
import time

from queue_townsmith_bevy_pipeline_v2 import live

S = Path(__file__).parent
R = S.parents[1]
W = Path('F:/OpenRPG_ImportWork')
ROOT = W / 'normal-decode-swizzle-common-install-v1'
PROBE = W / 'townsmith-s-paint-bevy-pipeline-v2'
CAND = W / 'normal-decode-swizzle-candidate-v1'
BIN = Path('C:/Users/karen/.rustup/toolchains/1.98.0-x86_64-pc-windows-msvc/bin')


def read(p):
    return json.loads(p.read_text(encoding='utf-8-sig'))


def sha(p):
    with Path(p).open('rb') as f:
        return hashlib.file_digest(f, 'sha256').hexdigest()


def save(name, data):
    p = ROOT / name
    temp = p.with_suffix('.tmp')
    temp.write_text(json.dumps(data, indent=2), encoding='utf-8')
    temp.replace(p)


def prepare():
    assert not ROOT.exists()
    receipt = W / 'townsmith-s-paint-material-packet-reopen-v1/RESULT.v1.json'
    verified = read(receipt)
    assert verified['status'] == 'material_packet_integrity_and_normal_swizzle_regression_verified'
    assert verified['corrected_candidate_tests_passed'] == 37 and verified['original_validator_expected_regression_failures'] == 2
    for p, h in verified['input_pins'].items():
        assert sha(p) == h, p
    source_pins = read(CAND / 'PREPARED.v1.json')['source_pins']
    for p, h in source_pins.items():
        assert sha(p) == h, p
    upstream_pid = read(PROBE / 'JOB.v1.json')['pid']
    assert live(upstream_pid) or read(PROBE / 'RUN-RESULT.v1.json')['status'] in ('actual_bevy_four_group_material_pipelines_created', 'actual_bevy_pipeline_refusals_retained', 'actual_bevy_pipeline_probe_refusal_retained')
    ROOT.mkdir()
    (ROOT / 'before').mkdir()
    candidate_pins = {str(p): sha(p) for p in CAND.glob('*.rs')}
    candidate_pins.update({str(p): sha(p) for p in (Path(__file__), S / 'queue_townsmith_bevy_pipeline_v2.py', receipt)})
    save('PREPARED.v1.json', {'source_pins': source_pins, 'candidate_pins': candidate_pins, 'upstream_controller_pid': upstream_pid})
    save('RUN-RESULT.v1.json', {'status': 'normal_swizzle_install_waiting_for_pinned_bevy_probe', 'full_import': False, 'complete_assets_delivered_increment': 0, 'upstream_controller_pid': upstream_pid})
    with (ROOT / 'controller.log').open('xb') as log:
        child = subprocess.Popen([sys.executable, '-B', str(Path(__file__).resolve()), 'run'], cwd=ROOT, stdout=log, stderr=log, stdin=subprocess.DEVNULL, creationflags=subprocess.CREATE_NO_WINDOW)
    save('JOB.v1.json', {'pid': child.pid})
    print(json.dumps({'pid': child.pid, 'root': str(ROOT), 'upstream_controller_pid': upstream_pid}))


def run():
    state = read(ROOT / 'RUN-RESULT.v1.json')
    try:
        prepared = read(ROOT / 'PREPARED.v1.json')
        while live(prepared['upstream_controller_pid']):
            time.sleep(3)
        probe = read(PROBE / 'RUN-RESULT.v1.json')
        assert probe['status'] in ('actual_bevy_four_group_material_pipelines_created', 'actual_bevy_pipeline_refusals_retained', 'actual_bevy_pipeline_probe_refusal_retained'), probe
        # Probe refusal is retained and does not invalidate independently verified
        # validation semantics; never mutate its pinned sources while it is live.
        while subprocess.check_output(['powershell', '-NoProfile', '-Command', '@(Get-Process cargo -ErrorAction SilentlyContinue).Count'], text=True).strip() != '0':
            state['status'] = 'normal_swizzle_install_waiting_for_existing_cargo'
            save('RUN-RESULT.v1.json', state)
            time.sleep(10)
        for p, h in {**prepared['source_pins'], **prepared['candidate_pins']}.items():
            assert sha(p) == h, p
        target = R / 'crates/forge_assets/src/source_graph_bundle.rs'
        before = ROOT / 'before/source_graph_bundle.rs'
        before.write_bytes(target.read_bytes())
        assert sha(before) == prepared['source_pins'][str(target)]
        text = (CAND / 'source_graph_bundle.candidate.rs').read_text(encoding='utf-8').replace('"imported_storage.candidate.rs"', '"source_graph_bundle/imported_storage.rs"').replace('"scalar_parameters.rs"', '"source_graph_bundle/scalar_parameters.rs"')
        formatted = subprocess.run([str(BIN / 'rustfmt.exe'), '--edition', '2024', '--config', 'skip_children=true'], input=text.encode('utf-8'), capture_output=True, creationflags=subprocess.CREATE_NO_WINDOW)
        assert formatted.returncode == 0, formatted.stderr.decode('utf-8', errors='replace')
        temp = target.with_suffix('.swizzle.tmp')
        assert not temp.exists()
        temp.write_bytes(formatted.stdout)
        temp.replace(target)
        installed = {str(target): sha(target)}
        save('INSTALLED.v1.json', {'installed_source_pins': installed, 'previous_source_epoch': {str(target): {'sha256': sha(before), 'snapshot': str(before)}}, 'upstream_probe_status': probe['status'], 'upstream_probe_receipt_sha256': sha(PROBE / 'RUN-RESULT.v1.json'), 'full_import': False})
        env = os.environ.copy()
        env.update(PATH=str(BIN) + os.pathsep + env.get('PATH', ''), RUSTC=str(BIN / 'rustc.exe'), CARGO_BUILD_JOBS='1', CARGO_INCREMENTAL='0', CARGO_TARGET_DIR=str(W / 'firepit-static-glb-product-check-v2/target'), TEMP=str(ROOT), TMP=str(ROOT))
        for label, package, scope in [('assets', 'forge_assets', 'source_graph_bundle'), ('engine', 'forge_engine', 'source_graph::'), ('unlit', 'forge_engine', 'source_unlit_forward::'), ('program', 'forge_engine', 'source_graph_program::'), ('glb', 'forge_engine', 'source_graph_glb::')]:
            command = [str(BIN / 'cargo.exe'), 'test', '--locked', '--offline', '-p', package, '--lib', scope]
            save(label + '.COMMAND.v1.json', {'command': command, 'cwd': str(R)})
            with (ROOT / (label + '.stdout.log')).open('xb') as out, (ROOT / (label + '.stderr.log')).open('xb') as err:
                child = subprocess.Popen(command, cwd=R, env=env, stdout=out, stderr=err, stdin=subprocess.DEVNULL, creationflags=subprocess.CREATE_NO_WINDOW)
                state.update(status='normal_swizzle_installed_' + label + '_tests_running', cargo_pid=child.pid)
                save('RUN-RESULT.v1.json', state)
                state[label + '_exit_code'] = child.wait()
                assert child.returncode == 0, label
            for p, h in installed.items():
                assert sha(p) == h, p
        assert '29 passed; 0 failed' in (ROOT / 'assets.stdout.log').read_text(encoding='utf-8')
        state.update(status='normal_swizzle_common_installed_workspace_tests_passed', installed_source_pins=installed, assets_tests_passed=29, actual_material_render_qualified=False, forge_editor_verified=False)
    except Exception as error:
        state.update(status='normal_swizzle_install_or_tests_refusal_retained', error=repr(error), traceback=__import__('traceback').format_exc())
    state['finished_utc'] = datetime.now(timezone.utc).isoformat()
    save('RUN-RESULT.v1.json', state)


if __name__ == '__main__':
    prepare() if len(sys.argv) == 1 else run()
