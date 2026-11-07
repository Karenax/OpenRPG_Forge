"""One-shot complete-parent seeded native measurements after hierarchy v139."""
from pathlib import Path
from datetime import datetime, timezone
import ast
import copy
import ctypes
import hashlib
import importlib.util
import json
import os
import shutil
import struct
import subprocess
import sys
import traceback

S = Path(__file__).parent
W = Path('F:/OpenRPG_ImportWork')
O = W/'medieval-torch-native-particle-behavior-v140'
PROJECT = W/'unity-dungeon-original-shadergraph-hdrp-startup-v4/project'
PS = 'C:/Users/karen/.cache/codex-runtimes/codex-primary-runtime/dependencies/native/powershell/pwsh.exe'


def sha(path):
    with Path(path).open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


def read(path):
    return json.loads(Path(path).read_text('utf-8-sig'))


def f32(value):
    return struct.unpack('<f', struct.pack('<f', value))[0]


def save(state):
    state.update(updated_utc=datetime.now(timezone.utc).isoformat(),
                 recurring_automation=False, full_import=False)
    temporary = O/'RUN-RESULT.tmp'
    temporary.write_text(json.dumps(state, indent=2, allow_nan=False), 'utf-8')
    temporary.replace(O/'RUN-RESULT.v1.json')


def check(pins):
    for path, digest in pins.items():
        assert sha(path) == digest, path


state = dict(status='waiting_for_exact_verified_hierarchy_controller',
             waiting_pid=23300, native_behavior_verified=False,
             rendered=False, studio_verified=False)
broker = None
namespace = None
try:
    assert not (O/'RUN-RESULT.v1.json').exists(), 'Preserve prior controller receipt'
    state['controller_sha256'] = sha(__file__)
    save(state)
    command = subprocess.check_output([PS, '-NoProfile', '-Command',
        '(Get-CimInstance Win32_Process -Filter "ProcessId=23300").CommandLine'],
        text=True).strip()
    if command:
        assert 'run_medieval_torch_hierarchy_after_import_v139.py' in command
        kernel = ctypes.WinDLL('kernel32', use_last_error=True)
        kernel.OpenProcess.argtypes = [ctypes.c_ulong, ctypes.c_int, ctypes.c_ulong]
        kernel.OpenProcess.restype = ctypes.c_void_p
        kernel.WaitForSingleObject.argtypes = [ctypes.c_void_p, ctypes.c_ulong]
        kernel.CloseHandle.argtypes = [ctypes.c_void_p]
        handle = kernel.OpenProcess(0x100000, False, 23300)
        assert handle
        try:
            assert kernel.WaitForSingleObject(handle, 0xffffffff) == 0
        finally:
            kernel.CloseHandle(handle)
    prior = read(W/'medieval-torch-full-hierarchy-contract-v139/QUEUE.v1.json')
    assert prior['status'] == 'native_hierarchy_comparison_terminal_verified', prior
    hierarchy_path = W/'medieval-torch-full-hierarchy-contract-v139/RESULT.v1.json'
    assert sha(hierarchy_path) == prior['result_sha256']
    idle = subprocess.check_output([PS, '-NoProfile', '-Command',
        '@(Get-Process -Name Unity -ErrorAction SilentlyContinue).Count'], text=True).strip()
    assert idle == '0', 'Preserve existing Unity session'
    prep = read(O/'PREPARED.v1.json')
    check(prep['input_pins'])
    request = read(O/'manifest.json')
    native_path = W/'medieval-torch-native-prefab-import-v137/NATIVE-RESULT.v1.json'
    native_run_path = W/'medieval-torch-native-prefab-import-v137/RUN-RESULT.v1.json'
    native_run = read(native_run_path)
    assert native_run['native_import_verified'] and sha(native_path) == native_run['native_receipt_sha256']
    native = read(native_path)
    nodes = {node['path']: node for node in native['nodes']}
    for case in request['cases']:
        components = nodes[case['node_path']]['components']
        particle = next(c['identity'] for c in components
                        if c['identity']['type'] == 'UnityEngine.ParticleSystem')
        renderer = next(c['identity'] for c in components
                        if c['identity']['type'] == 'UnityEngine.ParticleSystemRenderer')
        assert particle['guid'] == renderer['guid'] == request['prefab_guid']
        case['renderer_file_id'] = renderer['file_id']
        case['expected_particle_file_id'] = particle['file_id']
    effective_path = O/'NATIVE-IDENTITY-MANIFEST.v1.json'
    effective_path.write_text(json.dumps(request, indent=2), 'utf-8')
    helper = O/'MedievalTorchParticleBehaviorV140.cs'
    target = PROJECT/'Assets/Editor/MedievalTorchParticleBehaviorV140.cs'
    assert not target.exists()
    shutil.copyfile(helper, target)
    runtime_pins = {str(p): sha(p) for p in [native_path, native_run_path,
                    hierarchy_path, effective_path, target, Path(__file__).resolve()]}
    (O/'RUNTIME-INPUT-PINS.v1.json').write_text(json.dumps(runtime_pins, indent=2), 'utf-8')
    broker_helper = S/'queue_delta_firepit_particle_behavior_v4.py'
    tree = ast.parse(broker_helper.read_text('utf-8-sig'))
    selected = [node for node in tree.body if isinstance(node, (ast.Import, ast.ImportFrom))
                or isinstance(node, ast.FunctionDef) and node.name in
                {'sha', 'read', 'write_state', 'stop_broker', 'start_broker'}]
    namespace = dict(ROOT=O, W=W,
        UPM=Path('F:/Unity/6000.5.8f1/Editor/Data/Resources/PackageManager/Server/UnityPackageManager.exe'),
        LOG_HANDLES=[])
    exec(compile(ast.Module(body=selected, type_ignores=[]), str(broker_helper), 'exec'), namespace)
    broker, label = namespace['start_broker'](state)
    env = os.environ.copy()
    env.update(TEMP=str(O/'temp'), TMP=str(O/'temp'), UPM_CACHE_ROOT=str(W/'unity-package-cache'))
    with (O/'unity.stdout.log').open('xb') as out, (O/'unity.stderr.log').open('xb') as err:
        child = subprocess.Popen(['F:/Unity/6000.5.8f1/Editor/Unity.exe',
            '-batchmode', '-nographics', '-quit', '-upmIpcPath', label,
            '-projectPath', str(PROJECT), '-executeMethod',
            'OpenRPGMedievalTorchParticleBehaviorV140.Export', '--openrpg-manifest',
            str(effective_path), '-logFile', str(O/'unity.log')], cwd=O,
            env=env, stdin=subprocess.DEVNULL, stdout=out, stderr=err,
            creationflags=subprocess.CREATE_NO_WINDOW)
        state.update(status='measuring_two_complete_parent_isolated_particle_systems', unity_pid=child.pid)
        save(state)
        state['unity_exit_code'] = child.wait()
    assert state['unity_exit_code'] == 0, 'Native measurement refused; retain logs'
    check(prep['input_pins'])
    check(runtime_pins)
    report_path = O/'BEHAVIOR.v1.json'
    report = read(report_path)
    assert len(report['cases']) == 2 and not report['full_import']
    spec = importlib.util.spec_from_file_location('torch_frame_verifier', O/'verify_frames.py')
    verifier = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(verifier)
    summaries = []
    controls = []
    assert {r['node_path'] for r in report['cases']} == {r['node_path'] for r in request['cases']}
    for case in request['cases']:
        result = next(r for r in report['cases'] if r['node_path'] == case['node_path'])
        source = copy.deepcopy(next(r['effective_particle_source'] for r in prep['source_cases']
                                   if r['node_path'] == case['node_path']))
        for key in ('lengthInSec', 'simulationSpeed'):
            source[key] = f32(source[key])
        scoped = dict(request, particle_file_id=case['expected_particle_file_id'],
                      renderer_file_id=case['renderer_file_id'], material_guid=case['material_guid'],
                      times=[f32(t) for t in request['times']])
        summary = verifier.check(result, scoped, source)
        summary['node_path'] = case['node_path']
        summaries.append(summary)
        bad = copy.deepcopy(result)
        row = next(r for r in bad['rows'] if r['requested_time'] > 0)
        bits = struct.unpack('<I', struct.pack('<f', row['requested_time']))[0]
        row['requested_time'] = struct.unpack('<f', struct.pack('<I', bits+1))[0]
        try:
            verifier.check(bad, scoped, source)
        except AssertionError:
            controls.append(dict(node=case['node_path'], adjacent_float32_time_refused=True))
        else:
            raise AssertionError('One-ULP time corruption admitted')
    assert sum(s['rows'] for s in summaries) == prep['expected_frames'] == 112
    state.update(status='two_isolated_complete_parent_native_particle_timelines_verified',
                 native_behavior_verified=True, summaries=summaries,
                 controls=controls, behavior_sha256=sha(report_path),
                 authoring_version_parity=False, simultaneous_systems_verified=False,
                 autonomous_prewarm_verified=False, runtime_converted=False)
except Exception as error:
    state.update(status='native_particle_behavior_refusal_retained',
                 error=repr(error), traceback=traceback.format_exc())
finally:
    if broker is not None:
        namespace['stop_broker'](broker, state)
    save(state)
