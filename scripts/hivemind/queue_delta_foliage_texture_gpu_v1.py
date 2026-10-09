"""Preserve imported storage; qualify native GPU sampling on all 18 foliage images."""
from pathlib import Path
import datetime
import hashlib
import json
import os
import shutil
import subprocess
import sys

W = Path('F:/OpenRPG_ImportWork')
ROOT = W / 'delta-foliage-texture-gpu-v1'
CAPTURE = W / 'unity-delta-foliage-texture-storage-v2'
OLD = W / 'source-graph-imported-texture-gpu-probe-v2'
TARGET = W / 'source-graph-private-boundary-v1/target-rust198-explicit-domain-v8'
REPO = Path('E:/OpenRPG_Forge/OpenRPG_Forge-Rust')
FORMATS = {'RGBA_DXT1_SRGB': ('Bc1RgbaUnormSrgb', 4, 8),
           'RGBA_DXT5_SRGB': ('Bc3RgbaUnormSrgb', 4, 16),
           'RGBA_DXT5_UNorm': ('Bc3RgbaUnorm', 4, 16),
           'R8G8B8A8_UNorm': ('Rgba8Unorm', 1, 4),
           'R8G8B8_UNorm': ('Rgba8Unorm', 1, 4)}


def read(path):
    return json.loads(Path(path).read_text('utf-8-sig'))


def sha(path):
    with Path(path).open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


def save(name, value, new=False):
    target = ROOT / name
    if new:
        with target.open('x', encoding='utf-8') as stream:
            json.dump(value, stream, indent=2, allow_nan=False)
    else:
        temp = target.with_suffix('.tmp')
        temp.write_text(json.dumps(value, indent=2, allow_nan=False), encoding='utf-8')
        temp.replace(target)


def check(pins):
    for path, digest in pins.items():
        assert sha(path) == digest, 'Changed input: ' + path


def prepare():
    assert not ROOT.exists()
    native = read(CAPTURE / 'export/RESULT.json')
    reopened = read(CAPTURE / 'TEXTURE-REOPEN.v1.json')
    assert reopened['source_texture_storage_captures_verified'] == len(native['cases']) == 18
    assert sha(CAPTURE / 'export/RESULT.json') == reopened['unity_result_sha256']
    assert native['unity_version'] == '6000.5.8f1' and native['color_space'] == 'Linear'
    prior_pins = read(OLD / 'HELPER-PINS.v1.json')
    check({row['path']: row['sha256'] for row in prior_pins})
    pins = dict(read(CAPTURE / 'PREPARED.v1.json')['input_pins'])
    pins.update({str(p): sha(p) for p in [CAPTURE / 'export/RESULT.json',
        CAPTURE / 'TEXTURE-REOPEN.v1.json', Path(__file__).resolve()]})
    check(pins)
    ROOT.mkdir()
    (ROOT / 'src').mkdir()
    main = (OLD / 'src/main.rs').read_text('utf-8')
    assert main.count('assert_eq!(results.len(), 44);') == 1
    assert main.count('assert_eq!(total_mips, 528);') == 1
    assert main.count('"gpu_sampled_textures":44') == 2
    main = main.replace('assert_eq!(results.len(), 44);', 'assert_eq!(results.len(), 18);')
    main = main.replace('assert_eq!(total_mips, 528);', 'assert_eq!(total_mips, 225);')
    main = main.replace('"gpu_sampled_textures":44', '"gpu_sampled_textures":18')
    (ROOT / 'src/main.rs').write_text(main, encoding='utf-8')
    for name in ['imported_texture.rs', 'sampler.rs', 'sample.wgsl']:
        shutil.copyfile(OLD / 'src' / name, ROOT / 'src' / name)
        pins[str(OLD / 'src' / name)] = sha(OLD / 'src' / name)
    cargo = (OLD / 'Cargo.toml').read_text('utf-8').replace('openrpg-imported-texture-gpu-probe-v2', 'openrpg-delta-foliage-texture-gpu-v1')
    (ROOT / 'Cargo.toml').write_text(cargo, encoding='utf-8')
    shutil.copyfile(OLD / 'Cargo.lock', ROOT / 'Cargo.lock')
    verifier = (OLD / 'verify_gpu.py').read_text('utf-8').replace('==44', '==18').replace('==528', '==225')
    verifier = verifier.replace('verified_gpu_textures=44', 'verified_gpu_textures=18').replace('verified_mip_levels=528', 'verified_mip_levels=225')
    verifier = verifier.replace('528*64*64', '225*64*64')
    (ROOT / 'verify_gpu.py').write_text(verifier, encoding='utf-8')
    rows, expansions = [], []
    for index, case in enumerate(native['cases']):
        directory = CAPTURE / 'export' / f'{index:03}'
        fmt, block, block_bytes = FORMATS[case['graphics_format']]
        raw = directory / 'raw-whole.bin'
        assert sha(raw) == reopened['rows'][index]['buffers']['raw-whole.bin']
        pins[str(raw)] = sha(raw)
        storage, mips, offset = raw, [], 0
        rgb = case['graphics_format'] == 'R8G8B8_UNorm'
        if rgb:
            storage = ROOT / f'rgb24-expanded-{index:03}.rgba8.bin'
            output = storage.open('xb')
        else:
            output = None
        try:
            for level, mip in enumerate(case['mips']):
                path = directory / f'mip-{level:02}.bin'
                data = path.read_bytes()
                assert hashlib.sha256(data).hexdigest() == reopened['rows'][index]['buffers'][path.name]
                pins[str(path)] = sha(path)
                if rgb:
                    assert len(data) == mip['width'] * mip['height'] * 3
                    rgba = bytearray((len(data) // 3) * 4)
                    rgba[0::4], rgba[1::4], rgba[2::4] = data[0::3], data[1::3], data[2::3]
                    rgba[3::4] = b'\xff' * (len(data) // 3)
                    data = bytes(rgba)
                    if level == 0:
                        oracle = directory / 'cpu-base-rgba32.bin'
                        assert case['cpu_rgba32_available'] and data == oracle.read_bytes()
                        pins[str(oracle)] = sha(oracle)
                    output.write(data)
                size = ((mip['width'] + block - 1) // block) * ((mip['height'] + block - 1) // block) * block_bytes
                assert len(data) == size
                mips.append(dict(level=level, width=mip['width'], height=mip['height'], offset=offset,
                                 bytes=len(data), sha256=hashlib.sha256(data).hexdigest()))
                offset += len(data)
        finally:
            if output:
                output.close()
        assert storage.stat().st_size == offset
        pins[str(storage)] = sha(storage)
        settings = json.loads(case['imported_settings_json'])
        rows.append(dict(asset_id=case['asset_id'], unity_guid=case['actual_guid'], unity_file_id=case['actual_file_id'],
            gpu_format=fmt, storage=str(storage), storage_sha256=sha(storage), storage_bytes=offset,
            width=case['width'], height=case['height'], mips=mips,
            resolved_sampler={key: case[key] for key in ['filter_mode', 'wrap_u', 'wrap_v', 'wrap_w', 'anisotropy', 'mip_bias']},
            source_mipmaps_enabled=bool(settings['mipmaps']['m_EnableMipMap']),
            rgb24_expansion=rgb, original_storage=str(raw), original_storage_sha256=sha(raw)))
        if rgb:
            expansions.append(dict(unity_guid=case['actual_guid'], original_storage=str(raw),
                derived_storage=str(storage), original_sha256=sha(raw), derived_sha256=sha(storage),
                rgb_bytes_preserved_all_mips=True, alpha_255_matches_native_cpu_base=True, mips=len(mips)))
    assert len(rows) == 18 and sum(len(row['mips']) for row in rows) == 225 and len(expansions) == 3
    for path in list((ROOT / 'src').iterdir()) + [ROOT / 'Cargo.toml', ROOT / 'Cargo.lock', ROOT / 'verify_gpu.py']:
        pins[str(path)] = sha(path)
    for path in (REPO / 'crates/forge_core/src').rglob('*.rs'):
        pins[str(path)] = sha(path)
    check(pins)
    save('INPUT.v1.json', dict(rows=rows, source_proof_pins=pins, full_import=False), new=True)
    save('PREPARED.v1.json', dict(input_pins=dict(pins, **{str(ROOT / 'INPUT.v1.json'): sha(ROOT / 'INPUT.v1.json')}),
        source_probe_root=str(OLD), target=str(TARGET), rgb24_expansions=expansions,
        expected_textures=18, expected_mips=225, gpu_qualified=False, full_import=False), new=True)
    with (ROOT / 'controller.log').open('xb') as log:
        child = subprocess.Popen([sys.executable, '-B', str(Path(__file__).resolve()), 'run'], cwd=ROOT,
            stdin=subprocess.DEVNULL, stdout=log, stderr=log, creationflags=subprocess.CREATE_NO_WINDOW)
    save('JOB.v1.json', dict(controller_pid=child.pid, created_utc=datetime.datetime.now(datetime.timezone.utc).isoformat()), new=True)
    print(json.dumps(dict(controller_pid=child.pid, expected_textures=18, expected_mips=225, rgb24_expansions=3)))


def run():
    state = dict(status='building_eighteen_texture_gpu_probe', gpu_textures_verified=0,
        source_unity_sampling_parity_qualified=False, normal_decode_qualified=False,
        studio_verified=False, full_import=False)
    save('RUN-RESULT.v1.json', state)
    try:
        prepared = read(ROOT / 'PREPARED.v1.json')
        check(prepared['input_pins'])
        env = os.environ.copy()
        env.update(TEMP=str(ROOT), TMP=str(ROOT), CARGO_TARGET_DIR=str(TARGET), CARGO_INCREMENTAL='0')
        with (ROOT / 'build.log').open('xb') as log:
            process = subprocess.run(['C:/Users/karen/.cargo/bin/cargo.exe', '+1.98.0', 'build', '--offline', '-j1'],
                cwd=ROOT, env=env, stdout=log, stderr=log, creationflags=subprocess.CREATE_NO_WINDOW)
        state['build_exit_code'] = process.returncode
        assert process.returncode == 0, 'GPU probe build refused; preserve build.log'
        check(prepared['input_pins'])
        executable = ROOT / 'gpu-texture-probe.immutable.v1.exe'
        shutil.copyfile(TARGET / 'debug/openrpg-delta-foliage-texture-gpu-v1.exe', executable)
        state.update(status='sampling_eighteen_textures_on_native_gpu', executable_sha256=sha(executable))
        save('RUN-RESULT.v1.json', state)
        with (ROOT / 'execution.stdout.log').open('xb') as out, (ROOT / 'execution.stderr.log').open('xb') as err:
            process = subprocess.run([str(executable), str(ROOT / 'INPUT.v1.json'), str(ROOT / 'samples')],
                cwd=ROOT, env=env, stdout=out, stderr=err, creationflags=subprocess.CREATE_NO_WINDOW)
        state['execution_exit_code'] = process.returncode
        assert process.returncode == 0, 'GPU sampling refused; preserve partial buffers and exact stderr'
        with (ROOT / 'reopening.log').open('xb') as log:
            process = subprocess.run([sys.executable, '-B', str(ROOT / 'verify_gpu.py')], cwd=ROOT,
                env=env, stdout=log, stderr=log, creationflags=subprocess.CREATE_NO_WINDOW)
        state['verifier_exit_code'] = process.returncode
        assert process.returncode == 0
        check(prepared['input_pins'])
        report = read(ROOT / 'GPU-REOPEN.v1.json')
        assert report['verified_gpu_textures'] == 18 and report['verified_mip_levels'] == 225
        state.update(status='eighteen_textures_and_225_mips_gpu_sampled_and_reopened',
            gpu_textures_verified=18, gpu_mips_verified=225, adapter_name=report['adapter_name'],
            backend=report['backend'], gpu_reopen_sha256=sha(ROOT / 'GPU-REOPEN.v1.json'))
    except Exception as error:
        state.update(status='refused_or_unverified', error=type(error).__name__ + ': ' + str(error))
    state['finished_utc'] = datetime.datetime.now(datetime.timezone.utc).isoformat()
    save('RUN-RESULT.v1.json', state)


if __name__ == '__main__':
    run() if len(sys.argv) > 1 and sys.argv[1] == 'run' else prepare()
