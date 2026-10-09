"""Sample the exact active RA texture in Unity without modifying its importer."""
from pathlib import Path
import ast
import datetime
import importlib.util
import json
import os
import shutil
import struct
import math
import subprocess
import sys

HERE = Path(__file__).resolve().parent
W = Path('F:/OpenRPG_ImportWork')
ROOT = W / 'unity-delta-firepit-ra-sampling-v1'
CAPTURE = W / 'unity-delta-firepit-active-texture-storage-v1'
PROJECT = W / 'unity-villa-texture-source-probe-linear-v2/project'
BASE = HERE / 'queue_delta_firepit_effect_materials_v1.py'
spec = importlib.util.spec_from_file_location('native_sampling_job', BASE)
job = importlib.util.module_from_spec(spec); spec.loader.exec_module(job); job.ROOT = ROOT

def prepare():
    assert not ROOT.exists(); job.idle()
    capture = job.read(CAPTURE / 'RUN-RESULT.v1.json')
    assert capture['textures_verified'] == 1 and capture['mips_verified'] == 11
    proof = job.read(CAPTURE / 'INDEPENDENT-REOPEN.v1.json')
    pins = dict(proof['input_pins']); pins.update(proof['output_pins'])
    owned_meta = Path(job.read(CAPTURE / 'PREPARED.v1.json')['owned_private_meta'])
    # Pin the actual post-import copy, without reverting its retained metadata.
    pins[str(owned_meta)] = job.sha(owned_meta)
    for p in (PROJECT / 'Assets').rglob('*'):
        if p.is_file(): pins[str(p)] = job.sha(p)
    for relative in ['ProjectSettings/ProjectSettings.asset', 'ProjectSettings/GraphicsSettings.asset',
                     'ProjectSettings/QualitySettings.asset', 'Packages/manifest.json', 'Packages/packages-lock.json']:
        p = PROJECT / relative
        if p.exists(): pins[str(p)] = job.sha(p)
    broker_helper = HERE / 'queue_delta_firepit_particle_behavior_v4.py'
    for p in [Path(__file__).resolve(), BASE, broker_helper, CAPTURE / 'RUN-RESULT.v1.json', CAPTURE / 'INDEPENDENT-REOPEN.v1.json']:
        pins[str(p)] = job.sha(p)
    job.check(pins); ROOT.mkdir(); (ROOT / 'temp').mkdir()
    for name in ['FirePitRaSampling.cs', 'FirePitRaSampling.compute']:
        source = HERE / 'candidates' / name; destination = PROJECT / 'Assets/Editor' / name
        assert not destination.exists(); shutil.copyfile(source, destination)
        pins[str(source)] = job.sha(source); pins[str(destination)] = job.sha(destination)
    texture = Path(str(owned_meta)[:-5])
    job.save('manifest.json', dict(asset_path=texture.relative_to(PROJECT).as_posix(),
        compute_path='Assets/Editor/FirePitRaSampling.compute', output_root=str(ROOT / 'export'),
        expected_guid='63f624c8bd42f35409dde06b85d2d900', expected_file_id='2800000'))
    pins[str(ROOT / 'manifest.json')] = job.sha(ROOT / 'manifest.json'); job.check(pins)
    job.save('PREPARED.v1.json', dict(input_pins=pins, expected_textures=1, expected_mips=11,
        importer_changed=False, source_unity_version_parity_qualified=False, full_import=False))
    with (ROOT / 'controller.log').open('xb') as out:
        child = subprocess.Popen([sys.executable, '-B', str(Path(__file__).resolve()), 'run'],
            cwd=ROOT, stdout=out, stderr=out, stdin=subprocess.DEVNULL, creationflags=subprocess.CREATE_NO_WINDOW)
    job.save('JOB.v1.json', dict(controller_pid=child.pid))
    print(json.dumps(dict(controller_pid=child.pid, expected_mips=11, full_import=False)))

def run():
    state = dict(status='preparing_native_ra_sampling', mips_verified=0, sampling_parity_qualified=False, full_import=False)
    job.save('RUN-RESULT.v1.json', state); broker = None; namespace = None
    try:
        pins = job.read(ROOT / 'PREPARED.v1.json')['input_pins']; job.check(pins); job.idle()
        helper = HERE / 'queue_delta_firepit_particle_behavior_v4.py'
        tree = ast.parse(helper.read_text('utf-8-sig'))
        selected = [n for n in tree.body if isinstance(n, (ast.Import, ast.ImportFrom)) or
            isinstance(n, ast.FunctionDef) and n.name in {'sha', 'read', 'write_state', 'stop_broker', 'start_broker'}]
        namespace = dict(ROOT=ROOT, W=W, UPM=Path('F:/Unity/6000.5.8f1/Editor/Data/Resources/PackageManager/Server/UnityPackageManager.exe'), LOG_HANDLES=[])
        exec(compile(ast.Module(body=selected, type_ignores=[]), str(helper), 'exec'), namespace)
        broker, label = namespace['start_broker'](state)
        env = os.environ.copy(); env.update(TEMP=str(ROOT / 'temp'), TMP=str(ROOT / 'temp'), UPM_CACHE_ROOT=str(W / 'unity-package-cache'))
        with (ROOT / 'unity.stdout.log').open('xb') as out, (ROOT / 'unity.stderr.log').open('xb') as err:
            child = subprocess.Popen([str(job.UNITY), '-batchmode', '-force-d3d11', '-quit', '-upmIpcPath', label,
                '-projectPath', str(PROJECT), '-executeMethod', 'OpenRPGFirePitRaSampling.Export.Run',
                '--openrpg-manifest', str(ROOT / 'manifest.json'), '-logFile', str(ROOT / 'unity.log')],
                cwd=ROOT, env=env, stdout=out, stderr=err, stdin=subprocess.DEVNULL, creationflags=subprocess.CREATE_NO_WINDOW)
            state.update(status='sampling_original_ra_texture_in_unity_gpu', unity_pid=child.pid)
            job.save('RUN-RESULT.v1.json', state); state['unity_exit_code'] = child.wait()
        assert state['unity_exit_code'] == 0, 'Native sampling refused'
        job.check(pins)
        native = job.read(ROOT / 'export/RESULT.json')
        assert native['unity_version'] == '6000.5.8f1' and native['color_space'] == 'Linear'
        assert native['graphics_device_type'] == 'Direct3D11' and native['graphics_device_name']
        assert native['unity_guid'] == '63f624c8bd42f35409dde06b85d2d900' and native['unity_file_id'] == '2800000'
        old = job.read(CAPTURE / 'export/RESULT.json')['cases'][0]
        for key in ['graphics_format', 'width', 'height', 'mip_count', 'filter_mode', 'wrap_u', 'wrap_v', 'wrap_w', 'anisotropy', 'mip_bias']:
            assert native[key] == old[key], 'Native texture/sampler drift: ' + key
        assert len(native['mips']) == 11
        outputs = {str(ROOT / 'export/RESULT.json'): job.sha(ROOT / 'export/RESULT.json')}
        for level, mip in enumerate(native['mips']):
            assert mip['level'] == level and mip['grid'] == 64
            p = Path(mip['rgba_f32']); assert p.resolve().is_relative_to((ROOT / 'export').resolve())
            data = p.read_bytes(); assert len(data) == 65536
            values = struct.unpack('<16384f', data)
            assert all(math.isfinite(v) and -0.000001 <= v <= 1.000001 for v in values)
            outputs[str(p)] = job.sha(p)
        job.check(pins); job.check(outputs)
        job.save('INDEPENDENT-REOPEN.v1.json', dict(input_pins=pins, output_pins=outputs,
            mips_verified=11, sampled_texels=45056, importer_unchanged=True, full_import=False))
        state.update(status='eleven_native_ra_gpu_mips_reopened', mips_verified=11, sampled_texels=45056,
            importer_unchanged=True, independent_receipt_sha256=job.sha(ROOT / 'INDEPENDENT-REOPEN.v1.json'))
    except Exception as error:
        import traceback
        state.update(status='refused_or_unverified', error=type(error).__name__ + ': ' + str(error), diagnostic_traceback=traceback.format_exc())
    finally:
        if broker is not None: namespace['stop_broker'](broker, state)
    state['finished_utc'] = datetime.datetime.now(datetime.timezone.utc).isoformat(); job.save('RUN-RESULT.v1.json', state)

if __name__ == '__main__':
    run() if len(sys.argv) > 1 and sys.argv[1] == 'run' else prepare()
