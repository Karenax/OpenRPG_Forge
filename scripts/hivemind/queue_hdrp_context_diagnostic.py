"""Observe the failed HDRP context after the existing exact Unity queue completes."""
import os
from pathlib import Path
import subprocess
import sys

import queue_hdrp_snapshot_v4 as common
from recover_native_scene_check_queue import wait_exact

ROOT = common.W / 'hdrp-native-context-diagnostic-v1'
CAPTURE = common.W / 'unity-dungeon-original-shadergraph-hdrp-startup-v4'
API = common.W / 'hdrp-context-diagnostic-api-v1/RESULT.v1.json'
FAILURE = common.W / 'hdrp-v4-failure-and-material-copy-analysis-v1/RESULT.v1.json'
common.ROOT = ROOT


def prepare():
    assert not ROOT.exists(), 'Existing diagnostic retained'
    api, failure = common.read(API), common.read(FAILURE)
    assert api['actual_exit_code'] == 0 and common.sha(api['helper']) == api['helper_sha256']
    assert failure['original_materials_unchanged'] == 42
    pins = {p: h for p,h in failure['input_pins'].items() if str(common.W / 'hivemind-run') in p}
    for path in [API, FAILURE, CAPTURE / 'manifest.v4.json', Path(api['helper']),
                 Path(__file__).resolve(), Path(common.__file__).resolve(),
                 Path(__file__).with_name('recover_native_scene_check_queue.py')]:
        pins[str(path)] = common.sha(path)
    common.check(pins)
    waits = []
    for pid in [40648,9548,34996]:
        command = f'$p=Get-Process -Id {pid} -ErrorAction SilentlyContinue;if($p){{$p.StartTime.ToUniversalTime().Ticks}}else{{0}}'
        birth = int(subprocess.check_output([str(common.PS), '-NoProfile', '-Command', command], text=True).strip())
        if birth:
            waits.append([pid,birth])
    assert [40648,639269781244301185] in waits, 'Exact URP capture queue identity absent'
    ROOT.mkdir()
    common.save('PREPARED.v1.json', dict(input_pins=pins, waits=waits, project=str(CAPTURE / 'project'),
                                       helper=api['helper'], helper_sha256=api['helper_sha256'],
                                       original_project_pipeline_configuration=False))
    with (ROOT / 'controller.log').open('xb') as output:
        child = subprocess.Popen([sys.executable, '-B', str(Path(__file__).resolve()), 'run'], cwd=ROOT,
                                  stdout=output, stderr=output, stdin=subprocess.DEVNULL,
                                  creationflags=subprocess.CREATE_NO_WINDOW)
    common.save('JOB.v1.json', dict(pid=child.pid, created_utc=common.now()))
    print(dict(pid=child.pid, requested_materials=42, diagnostic_executed=False))


def run():
    state = dict(status='waiting_for_exact_existing_unity_and_reopen_controllers',
                 native_diagnostic_executed=False, native_materials_verified=0,
                 gpu_verified=False, studio_verified=False, full_import=False)
    common.save('RUN-RESULT.v1.json', state)
    try:
        prep = common.read(ROOT / 'PREPARED.v1.json')
        for pid,birth in prep['waits']:
            wait_exact(pid,birth)
        subprocess.run([str(common.PS), '-NoProfile', '-Command',
                        'while(Get-Process -Name Unity -ErrorAction SilentlyContinue){Start-Sleep -Seconds 30}'],
                       check=True, creationflags=subprocess.CREATE_NO_WINDOW)
        common.check(prep['input_pins'])
        project = Path(prep['project'])
        helper = project / 'Assets/Editor/HdrpSubshaderContextDiagnostic.cs'
        assert not helper.exists(), 'Existing project helper retained'
        helper.write_bytes(Path(prep['helper']).read_bytes())
        assert common.sha(helper) == prep['helper_sha256']
        env = os.environ.copy()
        env.update(TEMP=str(ROOT), TMP=str(ROOT), UPM_CACHE_ROOT=str(common.OLD / 'upm-cache'))
        args = ['F:/Unity/6000.5.8f1/Editor/Unity.exe', '-batchmode', '-force-d3d11', '-quit',
                '-projectPath', str(project), '-executeMethod', 'OpenRPGHdrpContextDiagnostic.Probe.Export',
                '--openrpg-manifest', str(CAPTURE / 'manifest.v4.json'),
                '--openrpg-diagnostic-output', str(ROOT / 'NATIVE-DIAGNOSTIC.v1.json'),
                '-logFile', str(ROOT / 'unity.log')]
        with (ROOT / 'unity.stdout.log').open('xb') as out, (ROOT / 'unity.stderr.log').open('xb') as err:
            unity = subprocess.Popen(args, cwd=ROOT, env=env, stdout=out, stderr=err,
                                     stdin=subprocess.DEVNULL, creationflags=subprocess.CREATE_NO_WINDOW)
            state.update(status='observing_actual_pipeline_and_42_subshader_identities', unity_pid=unity.pid)
            common.save('RUN-RESULT.v1.json', state)
            code = unity.wait()
        state['actual_unity_exit_code'] = code
        common.check(prep['input_pins'])
        assert common.sha(helper) == prep['helper_sha256']
        native = common.read(ROOT / 'NATIVE-DIAGNOSTIC.v1.json')
        assert code == 0 and native['graphics_api'] != 'Null'
        assert len(native['before']['materials']) == len(native['after']['materials']) == 42
        state.update(status='native_hdrp_context_and_subshader_observations_retained',
                     native_diagnostic_executed=True, native_result_sha256=common.sha(ROOT / 'NATIVE-DIAGNOSTIC.v1.json'),
                     before_pipeline_instance=native['before']['pipeline_instance_type'],
                     after_pipeline_instance=native['after']['pipeline_instance_type'],
                     prepare_error=native['prepare_error'], source_materials_unchanged=True,
                     previous_18_private_material_upgrades_preserved=True)
    except Exception as error:
        state.update(status='diagnostic_refused', error=str(error))
    state['finished_utc'] = common.now()
    common.save('RUN-RESULT.v1.json', state)


if __name__ == '__main__':
    prepare() if len(sys.argv) == 1 else run()
