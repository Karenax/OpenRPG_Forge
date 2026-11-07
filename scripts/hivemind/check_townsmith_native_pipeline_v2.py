"""Test complete exported stages on D3D12; inferred layout is a limited witness."""
from pathlib import Path
from datetime import datetime, timezone
import hashlib
import json
import os
import subprocess

S = Path(__file__).parent
W = Path('F:/OpenRPG_ImportWork')
ROOT = W / 'townsmith-s-paint-native-pipeline-v2'
DEPS = W / 'firepit-static-glb-product-check-v2/target/debug/deps'
BIN = Path('C:/Users/karen/.rustup/toolchains/1.98.0-x86_64-pc-windows-msvc/bin')
FORMAT_SOURCE = Path('C:/Users/karen/.cargo/registry/src/index.crates.io-1949cf8c6b5b557f/bevy_core_pipeline-0.19.1/src/prepass/mod.rs')


def sha(path):
    with Path(path).open('rb') as handle:
        return hashlib.file_digest(handle, 'sha256').hexdigest()


def read(path):
    return json.loads(path.read_text(encoding='utf-8-sig'))


def save(data):
    data['updated_utc'] = datetime.now(timezone.utc).isoformat()
    path = ROOT / 'RUN-RESULT.v1.json'
    temp = path.with_suffix('.tmp')
    temp.write_text(json.dumps(data, indent=2), encoding='utf-8')
    temp.replace(path)


def main():
    assert not ROOT.exists()
    exported_path = W / 'townsmith-s-paint-composed-wgsl-v1/RESULT.v1.json'
    exported = read(exported_path)
    assert exported['status'] == 'seven_real_bevy_compositions_exported_to_standalone_wgsl'
    assert 'NORMAL_PREPASS_FORMAT: TextureFormat = TextureFormat::Rgb10a2Unorm;' in FORMAT_SOURCE.read_text(encoding='utf-8')
    prior = read(W / 'townsmith-s-paint-native-texture-gpu-control-v2/PREPARED.v1.json')
    libraries = prior['libraries']
    pins = dict(exported['input_pins'])
    for name, path in libraries.items():
        assert sha(path) == prior['input_pins'][path], name
        pins[path] = sha(path)
    candidate = S / 'candidates/SourceTownsmithPipelineAdmissionV2.rs'
    pins.update({str(p): sha(p) for p in (candidate, Path(__file__), exported_path, FORMAT_SOURCE)})
    for path, expected in pins.items():
        assert sha(path) == expected, path
    ROOT.mkdir()
    inputs = ROOT / 'INPUT.v1.json'
    inputs.write_text(json.dumps({'cases': exported['cases'], 'actual_bevy_layout_admission_qualified': False, 'source_pipeline_state_qualified': False}, indent=2), encoding='utf-8')
    pins[str(inputs)] = sha(inputs)
    code = ROOT / 'pipeline-admission.rs'
    code.write_bytes(candidate.read_bytes())
    pins[str(code)] = sha(code)
    (ROOT / 'PREPARED.v1.json').write_text(json.dumps({'input_pins': pins, 'libraries': libraries}, indent=2), encoding='utf-8')
    state = {'status': 'standalone_native_pipeline_probe_compiling', 'full_import': False, 'complete_assets_delivered_increment': 0}
    save(state)
    env = os.environ.copy()
    env.update(TEMP=str(ROOT), TMP=str(ROOT))
    exe = ROOT / 'pipeline-admission.exe'
    command = [str(BIN / 'rustc.exe'), '--edition=2024', str(code), '-L', 'dependency=' + str(DEPS), '-o', str(exe)]
    for name, path in libraries.items():
        command.extend(['--extern', name + '=' + path])
    (ROOT / 'COMMAND.v1.json').write_text(json.dumps({'command': command}), encoding='utf-8')
    try:
        with (ROOT / 'compile.stdout.log').open('xb') as out, (ROOT / 'compile.stderr.log').open('xb') as err:
            child = subprocess.Popen(command, env=env, stdout=out, stderr=err, stdin=subprocess.DEVNULL, creationflags=subprocess.CREATE_NO_WINDOW)
            state['rustc_pid'] = child.pid
            save(state)
            state['compile_exit_code'] = child.wait()
            assert child.returncode == 0
        with (ROOT / 'gpu.stdout.log').open('xb') as out, (ROOT / 'gpu.stderr.log').open('xb') as err:
            child = subprocess.Popen([str(exe), str(ROOT)], env=env, stdout=out, stderr=err, stdin=subprocess.DEVNULL, creationflags=subprocess.CREATE_NO_WINDOW)
            state.update(status='standalone_native_pipeline_probe_running', gpu_pid=child.pid)
            save(state)
            state['gpu_exit_code'] = child.wait()
            assert child.returncode == 0
        native = read(ROOT / 'NATIVE-RESULT.v1.json')
        for path, expected in pins.items():
            assert sha(path) == expected, path
        state.update(status=native['status'], pipelines_created=native['pipelines_created'], native_receipt_sha256=sha(ROOT / 'NATIVE-RESULT.v1.json'),
                     actual_bevy_bind_group_admission_qualified=False, gpu_draw_executed=False, source_unity_render_parity_qualified=False, forge_editor_verified=False)
    except Exception as error:
        state.update(status='native_pipeline_probe_failure_retained', error=repr(error))
    save(state)
    print(json.dumps(state))


if __name__ == '__main__':
    main()
