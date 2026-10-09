"""Capture the retained 13 Delta materials using the now measured warm HDRP context."""
import os
from pathlib import Path
import re
import subprocess
import sys

import queue_hdrp_snapshot_v4 as common
from verify_native_material_snapshot import reconcile

ROOT = common.W / 'unity-delta-warm-prepared-native-v3'
OLD = common.W / 'unity-delta-null-material-defaults-v2'
COMPAT = common.W / 'delta-warm-hdrp-project-compatibility-v1/RESULT.v1.json'
V6 = common.W / 'unity-dungeon-native-prepared-pipeline-v6'
common.ROOT = ROOT


def no_unity():
    assert int(subprocess.check_output([str(common.PS), '-NoProfile', '-Command',
        '@(Get-Process -Name Unity -ErrorAction SilentlyContinue).Count'], text=True).strip()) == 0


def prepare():
    assert not ROOT.exists(), 'Existing preparation preserved'
    no_unity()
    capture = common.read(V6 / 'RUN-RESULT.v6.json')
    reopened = common.read(V6 / 'INDEPENDENT-REOPEN.v1.json')
    assert capture['captured'] == reopened['native_materials_verified'] == 42 and not capture['failed']
    assert common.sha(V6 / 'NATIVE-RESULT.v6.json') == capture['native_result_sha256']
    prepared = common.read(V6 / 'PREPARED.v6.json')
    common.check(prepared['input_pins'])
    project = Path(prepared['project'])
    old = common.read(OLD / 'PREPARED.v1.json')
    common.check(old['source_pins'])
    compatibility = common.read(COMPAT)
    assert not compatibility['conflicts'] and compatibility['checked_prepared_files'] == 108
    guid_index = {}
    for meta in (project / 'Assets').rglob('*.meta'):
        match = re.search(r'^guid: ([0-9a-f]{32})\s*$', meta.read_text('utf-8-sig'), re.M)
        if match:
            guid_index.setdefault(match[1], []).append(str(meta))
    inputs = dict(old['source_pins'])
    copies = []
    for row in compatibility['rows']:
        source, target = Path(row['source']), Path(row['target'])
        assert common.sha(source) == row['source_sha256']
        if source.suffix == '.cs' or source.name.endswith('.cs.meta'):
            continue
        assert target.resolve().is_relative_to((project / 'Assets').resolve())
        assert not source.is_symlink() and not target.is_symlink()
        if target.exists():
            assert common.sha(target) == row['source_sha256'], 'Existing private content differs'
        if row['guid']:
            assert all(Path(p).resolve() == target.resolve() for p in guid_index.get(row['guid'], [])), 'GUID collision'
        inputs[str(source)] = row['source_sha256']
        copies.append(dict(source=str(source), target=str(target), sha256=row['source_sha256'], existed=target.exists()))
    requests = []
    for item in old['projects']:
        requests.extend(common.read(item['manifest'])['cases'])
    assert len(requests) == len({r['material_guid'] for r in requests}) == 13
    ROOT.mkdir()
    for row in copies:
        target = Path(row['target'])
        if not row['existed']:
            target.parent.mkdir(parents=True, exist_ok=True)
            with target.open('xb') as stream:
                stream.write(Path(row['source']).read_bytes())
        assert common.sha(target) == row['sha256']
    common.save('manifest.v3.json', dict(output_path=str(ROOT / 'NATIVE-RESULT.v3.json'), cases=requests))
    for path in [Path(__file__).resolve(), Path(common.__file__).resolve(), COMPAT,
                 V6 / 'RUN-RESULT.v6.json', V6 / 'INDEPENDENT-REOPEN.v1.json',
                 ROOT / 'manifest.v3.json', project / 'Assets/Editor/OriginalGraphMaterialSnapshotPreparedHDRPV6.cs']:
        inputs[str(path)] = common.sha(path)
    common.check(inputs)
    common.save('PREPARED.v3.json', dict(project=str(project), input_pins=inputs,
        copies=copies, requested_materials=13, previous_deferred_receipt=str(OLD / 'RUN-RESULT.v1.json'),
        source_scripts_executed=False, original_pack_version_parity_qualified=False, full_import=False))
    with (ROOT / 'controller.log').open('xb') as log:
        child = subprocess.Popen([sys.executable, '-B', str(Path(__file__).resolve()), 'run'], cwd=ROOT,
            stdout=log, stderr=log, stdin=subprocess.DEVNULL, creationflags=subprocess.CREATE_NO_WINDOW)
    common.save('JOB.v3.json', dict(controller_pid=child.pid, created_utc=common.now()))
    print(dict(controller_pid=child.pid, requested=13, copied_files=len(copies)))


def run():
    state = dict(status='prechecking_delta_warm_native_capture', native_materials_verified=0,
        independent_reopen_verified=False, gpu_render_parity_qualified=False, studio_verified=False, full_import=False)
    common.save('RUN-RESULT.v3.json', state)
    try:
        prep = common.read(ROOT / 'PREPARED.v3.json')
        common.check(prep['input_pins'])
        no_unity()
        env = os.environ.copy()
        env.update(TEMP=str(ROOT), TMP=str(ROOT), UPM_CACHE_ROOT=str(common.OLD / 'upm-cache'))
        args = ['F:/Unity/6000.5.8f1/Editor/Unity.exe', '-batchmode', '-force-d3d11', '-quit',
            '-projectPath', prep['project'], '-executeMethod',
            'OpenRPGPreparedHDRPV6.OriginalGraphMaterialSnapshotPreparedHDRPV6.Export',
            '--openrpg-manifest', str(ROOT / 'manifest.v3.json'), '-logFile', str(ROOT / 'unity.log')]
        with (ROOT / 'unity.stdout.log').open('xb') as out, (ROOT / 'unity.stderr.log').open('xb') as err:
            child = subprocess.Popen(args, cwd=ROOT, env=env, stdout=out, stderr=err,
                stdin=subprocess.DEVNULL, creationflags=subprocess.CREATE_NO_WINDOW)
            state.update(status='capturing_13_delta_materials_in_prepared_warm_hdrp', unity_pid=child.pid)
            common.save('RUN-RESULT.v3.json', state)
            state['actual_unity_exit_code'] = child.wait()
        common.check(prep['input_pins'])
        assert state['actual_unity_exit_code'] == 0
        native = common.read(ROOT / 'NATIVE-RESULT.v3.json')
        rows = reconcile(common.read(ROOT / 'manifest.v3.json'), native)
        assert len(rows) == 13
        state.update(status='13_delta_native_material_passes_captured', native_materials_verified=13,
            native_result_sha256=common.sha(ROOT / 'NATIVE-RESULT.v3.json'), original_sources_unchanged=True)
    except Exception as error:
        state.update(status='refused', error=str(error))
    state['finished_utc'] = common.now()
    common.save('RUN-RESULT.v3.json', state)


if __name__ == '__main__':
    run() if len(sys.argv) > 1 and sys.argv[1] == 'run' else prepare()
