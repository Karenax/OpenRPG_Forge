"""Compare exact embedded Villa decoder functions with native Unity constants."""
from pathlib import Path
from datetime import datetime, timezone
import hashlib
import json
import math
import os
import struct
import subprocess

R = Path(__file__).resolve().parents[2]
W = Path('F:/OpenRPG_ImportWork')
O = W / 'villa-builtin-normal-gpu-comparison-v1006'
TOOL = R / 'tools/forge_gpu_kernel_check'
SDK = Path('C:/Users/karen/.rustup/toolchains/1.98.0-x86_64-pc-windows-msvc/bin')
TARGET = W / 'source-graph-private-boundary-v1/target-rust198-explicit-domain-v8'

def read(p):
    return json.loads(p.read_text('utf-8-sig'))

def sha(p):
    with p.open('rb') as f:
        return hashlib.file_digest(f, 'sha256').hexdigest()

def write(name, value):
    pending = O / (name + '.pending')
    pending.write_text(json.dumps(value, indent=2), 'utf-8')
    pending.replace(O / name)

def function(text, name):
    start = text.index('fn ' + name + '(')
    opening = text.index('{', start)
    depth = 1
    for i in range(opening + 1, len(text)):
        depth += (text[i] == '{') - (text[i] == '}')
        if depth == 0:
            return text[start:i + 1]
    raise ValueError('Incomplete source function')

def main():
    assert not O.exists(), 'Preserve previous GPU evidence'
    diagnosis_path = W / 'villa-deferred-builtin-normal-diagnosis-v1/RESULT.v1.json'
    native_path = W / 'unity-builtin-normal-constant-reference-v1/NATIVE-RESULT.v1.json'
    native_run_path = native_path.with_name('RUN-RESULT.v1.json')
    diagnosis, native, native_run = map(read, (diagnosis_path, native_path, native_run_path))
    assert len(diagnosis['rows']) == 19 and native_run['actual_unity_exit_code'] == 0
    assert sha(native_path) == native_run['native_result_sha256']
    assert len(native['inputs']) == 4 and native['graphics_api'] == 'Direct3D11'
    assert all(row == dict(x=0., y=0., z=1., w=1.) for row in native['platform_flags'])
    pins = {str(p): sha(p) for p in (diagnosis_path, native_path, native_run_path, Path(__file__).resolve())}
    helpers = set()
    source_rows = []
    for row in diagnosis['rows']:
        p = Path(row['glb'])
        assert sha(p) == row['expected_original_glb_sha256']
        pins[str(p)] = row['expected_original_glb_sha256']
        with p.open('rb') as f:
            header = f.read(20)
            assert header[:4] == b'glTF' and header[16:20] == b'JSON'
            size = struct.unpack_from('<I', header, 12)[0]
            assert 0 < size <= 16 * 1024 * 1024
            document = json.loads(f.read(size))
            contract = document['materials'][row['material_index']]['extensions']['OPENRPG_source_graph']
            for stage in ('forward', 'prepass'):
                desc = contract[stage]
                view = document['bufferViews'][desc['buffer_view']]
                f.seek(28 + size + view.get('byteOffset', 0))
                raw = f.read(view['byteLength'])
                assert hashlib.sha256(raw).hexdigest() == desc['sha256']
                text = raw.decode('utf-8')
                helpers.add(function(text, 'source_decode_normal') + '\n' + function(text, 'source_graph_normal_sample'))
        source_rows.append({'asset_id': row['asset_id'], 'glb': str(p), 'sha256': pins[str(p)]})
    assert len(helpers) == 1, 'Decoder identity varies; separate comparisons required'
    helper = next(iter(helpers))
    O.mkdir()
    shader = helper + '''
@group(0) @binding(0) var<storage,read> inputs:array<vec4<f32>>;
@group(0) @binding(1) var<storage,read_write> outputs:array<vec4<f32>>;
@compute @workgroup_size(1)
fn diagnostic(@builtin(global_invocation_id) id:vec3<u32>) {
 outputs[id.x]=source_graph_normal_sample(inputs[id.x],f32(id.x/4u));
}
'''
    (O / 'kernel.wgsl').write_text(shader, 'utf-8')
    vectors = [[float(row[k]) for k in ('x', 'y', 'z', 'w')] for row in native['inputs']]
    (O / 'input.f32').write_bytes(b''.join(struct.pack('<4f', *v) for v in vectors * 3))
    for p in (O / 'kernel.wgsl', O / 'input.f32', *TOOL.rglob('*.rs'), TOOL / 'Cargo.toml'):
        pins[str(p)] = sha(p)
    # The compiler only consumes this standalone tool. Record the common checkout's
    # tracked/untracked inventory as context; do not build historical code copies.
    write('checkout-inventory.v1.json', {
        'tracked_and_untracked_paths': subprocess.check_output(['git', 'ls-files', '-c', '-o', '--exclude-standard', '-z'], cwd=R).decode('utf-8').split('\0'),
        'status_porcelain': subprocess.check_output(['git', 'status', '--porcelain=v1', '-z'], cwd=R).decode('utf-8').split('\0'),
        'compiler_source_root': str(TOOL), 'shared_checkout': True, 'full_checkout_qualified': False})
    write('PREPARED.v1.json', {'input_pins': pins, 'source_rows': source_rows, 'unique_decoder_functions': 1,
                              'source_glbs_changed': False, 'source_binding_qualified': False, 'full_import': False})
    env = os.environ.copy()
    env.update(RUSTC=str(SDK / 'rustc.exe'), RUSTDOC=str(SDK / 'rustdoc.exe'),
               CARGO_TARGET_DIR=str(TARGET), CARGO_INCREMENTAL='0', TEMP=str(O), TMP=str(O))
    def run(label, command):
        with (O / (label + '.stdout.log')).open('wb') as out, (O / (label + '.stderr.log')).open('wb') as err:
            child = subprocess.Popen(command, cwd=R, env=env, stdout=out, stderr=err, creationflags=subprocess.CREATE_NO_WINDOW)
            write('JOB.v1.json', {'stage': label, 'pid': child.pid, 'command': command, 'started_utc': datetime.now(timezone.utc).isoformat()})
            code = child.wait()
        assert code == 0, f'{label} refused with exit {code}; preserve logs'
    run('lock', [str(SDK / 'cargo.exe'), 'generate-lockfile', '--offline', '--manifest-path', str(TOOL / 'Cargo.toml')])
    pins[str(TOOL / 'Cargo.lock')] = sha(TOOL / 'Cargo.lock')
    write('PREPARED.v1.json', {'input_pins': pins, 'source_rows': source_rows, 'unique_decoder_functions': 1,
                              'source_glbs_changed': False, 'source_binding_qualified': False, 'full_import': False})
    run('build', [str(SDK / 'cargo.exe'), 'build', '--locked', '--offline', '--manifest-path', str(TOOL / 'Cargo.toml')])
    assert all(sha(Path(p)) == h for p, h in pins.items()), 'Input changed during build'
    exe = TARGET / 'debug/forge_gpu_kernel_check.exe'
    pins[str(exe)] = sha(exe)
    run('gpu', [str(exe), str(O / 'kernel.wgsl'), str(O / 'input.f32'), str(O / 'output.f32'), str(O / 'GPU-RESULT.v1.json')])
    raw = (O / 'output.f32').read_bytes()
    assert len(raw) == 192
    outputs = list(struct.iter_unpack('<4f', raw))
    assert all(math.isfinite(v) for row in outputs for v in row)
    expected = [[float(row[k]) for k in ('x', 'y', 'z', 'w')] for row in native['decoded_normals']]
    errors = [max(abs(a-b) for a,b in zip(output, target)) for output,target in zip(outputs[8:], expected)]
    controls = [max(abs(a-b) for a,b in zip(output, expected[i % 4])) for i,output in enumerate(outputs[:8])]
    assert all(sha(Path(p)) == h for p, h in pins.items()), 'Input changed during GPU comparison'
    passing = max(errors) <= 1e-6
    write('RESULT.v1.json', {'status': 'EXACT_VILLA_DECODER_GPU_PARITY_PASS' if passing else 'EXACT_VILLA_DECODER_GPU_PARITY_REFUSED',
          'finished_utc': datetime.now(timezone.utc).isoformat(), 'source_scenes_reopened': 19,
          'source_program_stages_reopened': 38, 'mode2_max_abs_errors': errors,
          'other_mode_control_errors': controls, 'mode2_gpu_matches_unity': passing,
          'input_pins': pins, 'gpu_receipt_sha256': sha(O / 'GPU-RESULT.v1.json'),
          'source_binding_qualified': False, 'source_material_render_parity_qualified': False,
          'glbs_modified': False, 'full_import': False})
    print(json.dumps({'mode2_gpu_matches_unity': passing, 'mode2_errors': errors, 'source_scenes_reopened': 19}))

if __name__ == '__main__':
    try:
        main()
    except Exception as error:
        if O.exists():
            write('FAILURE.v1.json', {'error': str(error), 'full_import': False})
        raise
