"""Measure the exact helper present in all six candidate native material programs."""
from pathlib import Path
import datetime
import hashlib
import json
import os
import shutil
import subprocess
import sys

W = Path('F:/OpenRPG_ImportWork')
ROOT = W / 'delta-foliage-normal-decoder-gpu-v1'
RAW = W / 'delta-foliage-texture-gpu-v3'
BUILD = W / 'delta-foliage-texture-gpu-v2'
REFERENCE = W / 'unity-delta-foliage-normal-reference-v1'
PROGRAMS = W / 'delta-native-lit-fragments-rgorag-v2'
OLD = W / 'source-graph-imported-normal-decoder-gpu-probe-v1'
TARGET = W / 'source-graph-private-boundary-v1/target-rust198-explicit-domain-v8'


def read(path):
    return json.loads(Path(path).read_text('utf-8-sig'))


def sha(path):
    with Path(path).open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


def save(name, value, new=False):
    if new:
        with (ROOT / name).open('x', encoding='utf-8') as stream:
            json.dump(value, stream, indent=2, allow_nan=False)
    else:
        temp = ROOT / (name + '.tmp')
        temp.write_text(json.dumps(value, indent=2, allow_nan=False), encoding='utf-8')
        temp.replace(ROOT / name)


def check(pins):
    assert all(sha(path) == digest for path, digest in pins.items()), 'Decoder input changed'


def function(source, name):
    start = source.index('fn ' + name + '(')
    opening = source.index('{', start)
    depth = 1
    for offset in range(opening + 1, len(source)):
        depth += (source[offset] == '{') - (source[offset] == '}')
        if depth == 0:
            return source[start:offset + 1]
    raise ValueError('Incomplete source helper')


def prepare():
    assert not ROOT.exists()
    raw_path = W / 'delta-foliage-normal-raw-gpu-parity-v1/RESULT.v1.json'
    raw, reference, programs = [read(path) for path in [raw_path,
        REFERENCE / 'GPU-REFERENCE-REOPEN.v1.json', PROGRAMS / 'INDEPENDENT-REOPEN.v1.json']]
    assert raw['normal_texture_raw_sampling_verified'] and raw['common_measured_coordinate_mappings'] == ['identity']
    assert reference['verified_textures'] == programs['native_lit_programs_reopened'] == 6
    pins = dict(read(RAW / 'PREPARED.v1.json')['input_pins'])
    for path in [raw_path, REFERENCE / 'GPU-REFERENCE-REOPEN.v1.json', REFERENCE / 'export/RESULT.json',
                 PROGRAMS / 'INDEPENDENT-REOPEN.v1.json', Path(__file__).resolve(), OLD / 'compare.py']:
        pins[str(path)] = sha(path)
    wanted = {row['asset_id'] for row in reference['rows']}
    inputs = read(RAW / 'INPUT.v1.json')
    rows = [row for row in inputs['rows'] if row['asset_id'] in wanted]
    assert len(rows) == 6 and sum(len(row['mips']) for row in rows) == 75
    assert all(row['gpu_format'] == 'Bc3RgbaUnorm' for row in rows)
    helpers = []
    for row in programs['rows']:
        for stage in ['forward', 'prepass']:
            path = Path(row[stage])
            assert sha(path) == row[stage + '_sha256']
            pins[str(path)] = sha(path)
            text = path.read_text('utf-8-sig')
            helpers.append(function(text, 'source_decode_normal') + '\n\n' + function(text, 'source_graph_normal_sample'))
    assert len(set(helpers)) == 1 and 'if mode==2.0' in helpers[0]
    check(pins)
    ROOT.mkdir()
    (ROOT / 'src').mkdir()
    main = (BUILD / 'src/main.rs').read_text('utf-8-sig')
    assert main.count('assert_eq!(results.len(), 18);') == 1
    assert main.count('assert_eq!(total_mips, 225);') == 1
    assert main.count('"gpu_sampled_textures":18') == 2
    main = main.replace('assert_eq!(results.len(), 18);', 'assert_eq!(results.len(), 6);')
    main = main.replace('assert_eq!(total_mips, 225);', 'assert_eq!(total_mips, 75);')
    main = main.replace('"gpu_sampled_textures":18', '"gpu_sampled_textures":6')
    main = main.replace('(-0.000001..=1.000001)', '(-1.000001..=1.000001)')
    main = main.replace('"normal_decode_qualified":false', '"normal_decode_qualified":false,"candidate_decoder":"mode2_rg_or_ag","decoded_normals_in_rgba_files":true')
    (ROOT / 'src/main.rs').write_text(main, encoding='utf-8')
    shader = (BUILD / 'src/sample.wgsl').read_text('utf-8-sig')
    expression = 'textureSampleLevel(source_image,source_sampler,uv,parameters.x)'
    assert shader.count(expression) == 1
    shader = helpers[0] + '\n\n' + shader.replace(expression, 'source_graph_normal_sample(' + expression + ',2.0)')
    (ROOT / 'src/sample.wgsl').write_text(shader, encoding='utf-8')
    for name in ['imported_texture.rs', 'sampler.rs']:
        shutil.copyfile(BUILD / 'src' / name, ROOT / 'src' / name)
    before, after = 'openrpg-delta-foliage-texture-gpu-v1', 'openrpg-delta-foliage-normal-decoder-gpu-v1'
    for name in ['Cargo.toml', 'Cargo.lock']:
        text = (BUILD / name).read_text('utf-8-sig')
        assert text.count(before) == 1
        (ROOT / name).write_text(text.replace(before, after), encoding='utf-8')
    compare = (OLD / 'compare.py').read_text('utf-8-sig')
    compare = compare.replace("'unity-villa-normal-sampling-reference-v1'", "'unity-delta-foliage-normal-reference-v1'")
    compare = compare.replace("'source-graph-imported-normal-gpu-parity-v2/RESULT.v1.json'", "'delta-foliage-normal-raw-gpu-parity-v1/RESULT.v1.json'")
    compare = compare.replace('==14', '==6').replace('==168', '==75').replace('exact_normal_textures_compared\':14', 'exact_normal_textures_compared\':6')
    (ROOT / 'compare.py').write_text(compare, encoding='utf-8')
    for path in list((ROOT / 'src').iterdir()) + [ROOT / 'Cargo.toml', ROOT / 'Cargo.lock', ROOT / 'compare.py']:
        pins[str(path)] = sha(path)
    check(pins)
    save('INPUT.v1.json', dict(rows=rows, source_proof_pins=pins, full_import=False), new=True)
    pins[str(ROOT / 'INPUT.v1.json')] = sha(ROOT / 'INPUT.v1.json')
    save('PREPARED.v1.json', dict(input_pins=pins, exact_candidate_helpers_identical_in_all_six_materials=True,
        source_programs=str(PROGRAMS), expected_textures=6, expected_mips=75,
        explicit_mode=2, renderer_installed=False, full_import=False), new=True)
    with (ROOT / 'controller.log').open('xb') as log:
        child = subprocess.Popen([sys.executable, '-B', str(Path(__file__).resolve()), 'run'], cwd=ROOT,
            stdin=subprocess.DEVNULL, stdout=log, stderr=log, creationflags=subprocess.CREATE_NO_WINDOW)
    save('JOB.v1.json', dict(controller_pid=child.pid, created_utc=datetime.datetime.now(datetime.timezone.utc).isoformat()), new=True)
    print(json.dumps(dict(controller_pid=child.pid, normal_textures=6, mips=75, exact_candidate_helper=True)))


def run():
    state = dict(status='building_exact_six_material_normal_decoder_probe', native_normal_textures_verified=0,
        original_shadergraph_render_parity_qualified=False, renderer_installed=False, studio_verified=False, full_import=False)
    save('RUN-RESULT.v1.json', state)
    try:
        prepared = read(ROOT / 'PREPARED.v1.json')
        check(prepared['input_pins'])
        env = os.environ.copy()
        env.update(TEMP=str(ROOT), TMP=str(ROOT), CARGO_TARGET_DIR=str(TARGET), CARGO_INCREMENTAL='0')
        with (ROOT / 'build.log').open('xb') as log:
            code = subprocess.run(['C:/Users/karen/.cargo/bin/cargo.exe', '+1.98.0', 'build', '--locked', '--offline', '-j1'],
                cwd=ROOT, env=env, stdout=log, stderr=log, creationflags=subprocess.CREATE_NO_WINDOW).returncode
        state['build_exit_code'] = code
        assert code == 0, 'Decoder build refused; retain build.log'
        check(prepared['input_pins'])
        executable = ROOT / 'normal-decoder-gpu-probe.immutable.v1.exe'
        shutil.copyfile(TARGET / 'debug/openrpg-delta-foliage-normal-decoder-gpu-v1.exe', executable)
        save('BUILD-RUN.v1.json', dict(build_exit_code=code, executable_sha256=sha(executable),
             exact_candidate_helper=True, build_log_sha256=sha(ROOT / 'build.log')), new=True)
        state.update(status='sampling_exact_six_material_normal_decoder', executable_sha256=sha(executable))
        save('RUN-RESULT.v1.json', state)
        with (ROOT / 'execution.stdout.log').open('xb') as out, (ROOT / 'execution.stderr.log').open('xb') as err:
            child = subprocess.Popen([str(executable), str(ROOT / 'INPUT.v1.json'), str(ROOT / 'gpu')],
                cwd=ROOT, env=env, stdout=out, stderr=err, creationflags=subprocess.CREATE_NO_WINDOW)
            state['gpu_process_pid'] = child.pid
            save('RUN-RESULT.v1.json', state)
            state['execution_exit_code'] = child.wait()
        assert state['execution_exit_code'] == 0
        with (ROOT / 'comparison.log').open('xb') as log:
            code = subprocess.run([sys.executable, '-B', str(ROOT / 'compare.py')], cwd=ROOT,
                env=env, stdout=log, stderr=log, creationflags=subprocess.CREATE_NO_WINDOW).returncode
        state['comparison_exit_code'] = code
        assert code == 0, 'Decoder differs; retain per-image comparison and fixed tolerances'
        report = read(ROOT / 'DECODER-PARITY.v1.json')
        assert report['exact_normal_textures_compared'] == 6 and report['mip_levels_compared'] == 75
        assert report['candidate_mode2_matches_float_rgorag_gpu_reference']
        check(prepared['input_pins'])
        state.update(status='six_exact_candidate_decoders_match_measured_native_reference', native_normal_textures_verified=6,
            normal_mips_verified=75, parity_receipt_sha256=sha(ROOT / 'DECODER-PARITY.v1.json'))
    except Exception as error:
        state.update(status='refused_or_unverified', error=type(error).__name__ + ': ' + str(error))
    state['finished_utc'] = datetime.datetime.now(datetime.timezone.utc).isoformat()
    save('RUN-RESULT.v1.json', state)


if __name__ == '__main__':
    run() if len(sys.argv) > 1 and sys.argv[1] == 'run' else prepare()
