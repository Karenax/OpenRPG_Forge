"""Capture original legacy shader tint/blending with explicit GPU fixtures."""
from pathlib import Path
import ast
import datetime
import importlib.util
import json
import os
import shutil
import subprocess
import sys

HERE = Path(__file__).resolve().parent
W = Path('F:/OpenRPG_ImportWork')
ROOT = W / 'unity-delta-firepit-legacy-shader-gpu-v1'
BASE = HERE / 'queue_delta_firepit_effect_materials_v1.py'
HELPER = HERE / 'candidates/LegacyParticleShaderGpuProbe.cs'
spec = importlib.util.spec_from_file_location('legacy_gpu_job', BASE)
job = importlib.util.module_from_spec(spec)
spec.loader.exec_module(job)
job.ROOT = ROOT

def prepare():
    assert not ROOT.exists()
    job.idle()
    state_path = W / 'unity-delta-firepit-bake-state-probe-v2/RUN-RESULT.v1.json'
    state = job.read(state_path)
    assert state.get('native_bake_state_probe_verified'), 'Preserve unfinished native state probe'
    source = W / 'unity-delta-firepit-particle-geometry-v1'
    result = job.read(source / 'RUN-RESULT.v1.json')
    receipt_path = source / 'INDEPENDENT-REOPEN.v1.json'
    assert job.sha(receipt_path) == result['independent_receipt_sha256']
    proof = job.read(receipt_path)
    job.check(proof['input_pins'])
    assert job.sha(source / 'GEOMETRY.v1.json') == proof['capture_sha256']
    ROOT.mkdir()
    (ROOT / 'temp').mkdir()
    shutil.copyfile(HELPER, ROOT / HELPER.name)
    destination = job.PROJECT / 'Assets/Editor/LegacyParticleShaderGpuProbe.cs'
    assert not destination.exists() and not Path(str(destination) + '.meta').exists()
    shutil.copyfile(HELPER, destination)
    capture = job.read(source / 'GEOMETRY.v1.json')
    original_manifest = job.read(W / 'unity-delta-firepit-effect-materials-v1/manifest.json')
    cases = []
    for case in capture['cases']:
        colors = []
        for row in case['rows']:
            for g in row['geometry']:
                for color in g['colors']:
                    if color['a'] > 0 and color not in colors:
                        colors.append(color)
                    if len(colors) == 4:
                        break
                if len(colors) == 4:
                    break
            if len(colors) == 4:
                break
        assert len(colors) == 4
        binding = next(c for c in original_manifest['cases'] if c['material_guid'] == case['material_guid'])
        cases.append(dict(**binding, baked_colors=colors))
    job.save('manifest.json', dict(output=str(ROOT / 'GPU.v1.json'), cases=cases))
    pins = {str(p): job.sha(p) for p in (job.PROJECT / 'Assets').rglob('*') if p.is_file()}
    pins.update(proof['input_pins'])
    for p in [Path(__file__).resolve(), BASE, HELPER, ROOT / HELPER.name, ROOT / 'manifest.json',
              state_path, receipt_path, source / 'GEOMETRY.v1.json',
              HERE / 'queue_delta_firepit_particle_behavior_v4.py',
              W / 'unity-delta-firepit-effect-materials-v1/MATERIALS.v1.json',
              job.PROJECT / 'Packages/manifest.json', job.PROJECT / 'Packages/packages-lock.json',
              job.PROJECT / 'ProjectSettings/ProjectSettings.asset']:
        pins[str(p)] = job.sha(p)
    job.check(pins)
    job.save('PREPARED.v1.json', dict(input_pins=pins, expected_materials=2,
        expected_original_shader_gpu_fixtures=16, source_texture_override_fixture_only=True,
        no_new_authored_3d_asset=True, full_import=False))
    with (ROOT / 'controller.log').open('xb') as log:
        child = subprocess.Popen([sys.executable, '-B', str(Path(__file__).resolve()), 'run'],
            cwd=ROOT, stdin=subprocess.DEVNULL, stdout=log, stderr=log,
            creationflags=subprocess.CREATE_NO_WINDOW)
    job.save('JOB.v1.json', dict(controller_pid=child.pid))
    print(json.dumps(dict(controller_pid=child.pid, native_gpu_fixtures=16, full_import=False)))

def verify(report):
    assert report['source_shader_fixture_gpu_captured'] and report['source_materials_unchanged']
    assert report['gpu_type'] != 'Null' and report['color_space'] == 'Linear'
    assert len(report['cases']) == 2 and not report['full_import']
    manifest = job.read(ROOT / 'manifest.json')
    identities = job.read(W / 'unity-delta-firepit-effect-materials-v1/MATERIALS.v1.json')
    observations = 0
    for case in report['cases']:
        source = next(c for c in identities['cases'] if c['material_guid'] == case['material_guid'])
        assert case['shader_guid'] == source['shader_guid'] and case['shader_file_id'] == '203'
        assert case['shader_name'] == 'Legacy Shaders/Particles/Alpha Blended'
        assert case['original_keywords'] == source['keywords']
        inputs = next(c for c in manifest['cases'] if c['material_guid'] == case['material_guid'])
        assert len(case['rows']) == 8
        for index, row in enumerate(case['rows']):
            assert row['baked_color'] == inputs['baked_colors'][index // 2]
            assert row['alpha_channel_preserved'] and row['result']['a'] == row['background']['a']
            assert all(__import__('math').isfinite(v) for v in row['result'].values())
            if index % 2 == 0:
                assert row['background'] == dict(r=0, g=0, b=0, a=0.25)
                assert all(row['result'][k] > 0 for k in 'rgb'), 'Missing or fallback shader fragment'
            else:
                assert row['background'] == dict(r=0.125, g=0.25, b=0.5, a=0.75)
            observations += 1
    assert observations == 16
    return observations

def run():
    state = dict(status='preparing_original_shader_gpu_capture', native_shader_fixture_gpu_verified=False,
        whole_effect_gpu_qualified=False, studio_verified=False, full_import=False)
    job.save('RUN-RESULT.v1.json', state)
    broker = None
    namespace = None
    try:
        prepared = job.read(ROOT / 'PREPARED.v1.json')
        job.check(prepared['input_pins'])
        job.idle()
        broker_helper = HERE / 'queue_delta_firepit_particle_behavior_v4.py'
        tree = ast.parse(broker_helper.read_text('utf-8-sig'))
        selected = [n for n in tree.body if isinstance(n, (ast.Import, ast.ImportFrom)) or
                    isinstance(n, ast.FunctionDef) and n.name in {'sha','read','write_state','stop_broker','start_broker'}]
        namespace = dict(ROOT=ROOT, W=W, UPM=Path('F:/Unity/6000.5.8f1/Editor/Data/Resources/PackageManager/Server/UnityPackageManager.exe'), LOG_HANDLES=[])
        exec(compile(ast.Module(body=selected, type_ignores=[]), str(broker_helper), 'exec'), namespace)
        broker, label = namespace['start_broker'](state)
        env = os.environ.copy()
        env.update(TEMP=str(ROOT / 'temp'), TMP=str(ROOT / 'temp'), UPM_CACHE_ROOT=str(W / 'unity-package-cache'))
        with (ROOT / 'unity.stdout.log').open('xb') as out, (ROOT / 'unity.stderr.log').open('xb') as err:
            child = subprocess.Popen([str(job.UNITY), '-batchmode', '-quit', '-force-d3d11',
                '-upmIpcPath', label, '-projectPath', str(job.PROJECT), '-executeMethod',
                'OpenRPGLegacyParticleShaderGpuProbe.Export', '--openrpg-manifest', str(ROOT / 'manifest.json'),
                '-logFile', str(ROOT / 'unity.log')], cwd=ROOT, env=env, stdin=subprocess.DEVNULL,
                stdout=out, stderr=err, creationflags=subprocess.CREATE_NO_WINDOW)
            state.update(status='capturing_16_original_legacy_shader_gpu_fixtures', unity_pid=child.pid)
            job.save('RUN-RESULT.v1.json', state)
            state['unity_exit_code'] = child.wait()
        assert state['unity_exit_code'] == 0, 'Original shader GPU capture refused'
        job.check(prepared['input_pins'])
        count = verify(job.read(ROOT / 'GPU.v1.json'))
        job.save('INDEPENDENT-REOPEN.v1.json', dict(input_pins=prepared['input_pins'],
            capture_sha256=job.sha(ROOT / 'GPU.v1.json'), gpu_fixtures_verified=count,
            original_destination_alpha_preserved=True, actual_source_texture_sampling_qualified=False,
            soft_particle_and_fog_variants_qualified=False, forge_gpu_parity_qualified=False,
            whole_effect_gpu_qualified=False, full_import=False))
        state.update(status='16_original_legacy_shader_gpu_fixtures_reopened',
            native_shader_fixture_gpu_verified=True, gpu_fixtures_verified=count,
            independent_receipt_sha256=job.sha(ROOT / 'INDEPENDENT-REOPEN.v1.json'))
    except Exception as error:
        import traceback
        state.update(status='refused_or_unverified', error=type(error).__name__ + ': ' + str(error),
            diagnostic_traceback=traceback.format_exc())
    finally:
        if broker is not None:
            namespace['stop_broker'](broker, state)
    state['finished_utc'] = datetime.datetime.now(datetime.timezone.utc).isoformat()
    job.save('RUN-RESULT.v1.json', state)

if __name__ == '__main__':
    run() if len(sys.argv) > 1 and sys.argv[1] == 'run' else prepare()
