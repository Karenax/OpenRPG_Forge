"""Resume the 42 material captures after the measured native pipeline diagnosis.

Keep v4 helpers, material migrations and failures. Reuse only the private warm
project, add a distinct trusted helper and emit all v5 evidence under F:.
"""
import os
from pathlib import Path
import subprocess
import sys

import queue_hdrp_snapshot_v4 as common

ROOT = common.W / 'unity-dungeon-native-prepared-pipeline-v5'
V4 = common.W / 'unity-dungeon-original-shadergraph-hdrp-startup-v4'
DIAG = common.W / 'hdrp-native-context-diagnostic-v1'
common.ROOT = ROOT


def no_unity():
    result = subprocess.check_output([str(common.PS), '-NoProfile', '-Command',
        '@(Get-Process -Name Unity -ErrorAction SilentlyContinue).Count'], text=True)
    assert int(result.strip()) == 0, 'Existing Unity must finish; do not overlap projects'


def prepare():
    assert not ROOT.exists(), 'Preserve previous v5 preparation'
    no_unity()
    run = common.read(DIAG / 'RUN-RESULT.v1.json')
    native = common.read(DIAG / 'NATIVE-DIAGNOSTIC.v1.json')
    assert run['actual_unity_exit_code'] == 0
    assert common.sha(DIAG / 'NATIVE-DIAGNOSTIC.v1.json') == run['native_result_sha256']
    assert native['prepare_returned_true'] and not native['prepare_error']
    assert native['after']['pipeline_instance_type'] == 'UnityEngine.Rendering.HighDefinition.HDRenderPipeline'
    assert native['after']['global_shader_tag'] == 'HDRenderPipeline'
    assert len(native['after']['materials']) == 42
    assert all(x['selected_tag'] == 'HDRenderPipeline' and not x['error'] and
               not x['shader_has_errors'] and x['shader_supported'] and
               all(p and not p.startswith('<Unnamed') for p in x['pass_names'])
               for x in native['after']['materials'])
    diagnostic_preparation = common.read(DIAG / 'PREPARED.v1.json')
    common.check(diagnostic_preparation['input_pins'])
    project = Path(diagnostic_preparation['project'])
    assert project.resolve() == (V4 / 'project').resolve()
    old_helper = project / 'Assets/Editor/OriginalGraphMaterialSnapshotHDRP.cs'
    v4prep = common.read(V4 / 'PREPARED.v4.json')
    assert common.sha(old_helper) == v4prep['input_pins'][str(old_helper)]
    original_pins = {p: h for p, h in diagnostic_preparation['input_pins'].items()
                     if str(common.W / 'hivemind-run') in p}
    assert original_pins, 'Original source identity pins required'
    common.check(original_pins)
    helper = project / 'Assets/Editor/OriginalGraphMaterialSnapshotPreparedHDRPV5.cs'
    assert not helper.exists(), 'Existing project helper retained'
    content = old_helper.read_text('utf-8')
    content = content.replace('OriginalGraphMaterialSnapshotHDRP {',
                              'OriginalGraphMaterialSnapshotPreparedHDRPV5 {')
    marker = '   foreach(var item in request.cases){var path=AssetDatabase.GUIDToAssetPath'
    assert content.count(marker) == 1
    preparation = '''   // Measured diagnosis: selected asset alone leaves the batch pipeline uninitialized.
   var prepareMethod=typeof(UnityEngine.Rendering.RenderPipelineManager).GetMethod("TryPrepareRenderPipeline",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic,null,new Type[]{typeof(UnityEngine.Rendering.RenderPipelineAsset)},null);
   if(prepareMethod==null||prepareMethod.ReturnType!=typeof(bool))throw new Exception("Exact native pipeline preparation API unavailable; no substitute");
   if(!(bool)prepareMethod.Invoke(null,new object[]{pipeline}))throw new Exception("Native pipeline preparation refused");
   var actualPipeline=UnityEngine.Rendering.RenderPipelineManager.currentPipeline;
   if(actualPipeline==null||actualPipeline.GetType().FullName!="UnityEngine.Rendering.HighDefinition.HDRenderPipeline")throw new Exception("Actual HDRP pipeline instance absent");
'''
    content = content.replace(marker, preparation + marker)
    ROOT.mkdir()
    helper.write_text(content, encoding='utf-8')
    copied_materials = {}
    for path in sorted((project / 'Assets').rglob('*.mat')):
        for entry in [path, Path(str(path) + '.meta')]:
            assert entry.is_file() and not entry.is_symlink()
            before = common.sha(entry)
            backup = ROOT / 'pre-capture-private-materials' / entry.relative_to(project)
            backup.parent.mkdir(parents=True, exist_ok=True)
            backup.write_bytes(entry.read_bytes())
            assert common.sha(entry) == common.sha(backup) == before
            copied_materials[str(entry)] = dict(sha256=before, backup=str(backup))
    manifest = common.read(V4 / 'manifest.v4.json')
    assert len(manifest['cases']) == 42
    manifest['output_path'] = str(ROOT / 'NATIVE-RESULT.v5.json')
    common.save('manifest.v5.json', manifest)
    rsp = ROOT / 'api-check.v5.rsp'
    arguments = [line for line in (V4 / 'api-check.v4.rsp').read_text('utf-8-sig').splitlines()
                 if line.startswith('-') and not line.startswith('-out:')]
    arguments += [f'-out:"{ROOT / "Snapshot.api.dll"}"', f'"{helper}"']
    rsp.write_text('\n'.join(arguments), encoding='utf-8')
    sdk = Path('F:/Unity/6000.5.8f1/Editor/Data/DotNetSdk')
    compilers = list((sdk / 'sdk').glob('*/Roslyn/bincore/csc.dll'))
    assert len(compilers) == 1
    with (ROOT / 'api.stdout.log').open('xb') as out, (ROOT / 'api.stderr.log').open('xb') as err:
        code = subprocess.run([str(sdk / 'dotnet.exe'), str(compilers[0]), '@' + str(rsp)],
                              cwd=ROOT, stdout=out, stderr=err).returncode
    common.save('API-COMPILE.v5.json', dict(actual_exit_code=code, helper=str(helper),
                                           helper_sha256=common.sha(helper)))
    assert code == 0, 'Actual Unity API compile refused'
    pins = dict(original_pins)
    for path in [Path(__file__).resolve(), Path(common.__file__).resolve(), old_helper,
                 helper, ROOT / 'manifest.v5.json', DIAG / 'NATIVE-DIAGNOSTIC.v1.json',
                 DIAG / 'RUN-RESULT.v1.json']:
        pins[str(path)] = common.sha(path)
    common.check(pins)
    common.save('PREPARED.v5.json', dict(project=str(project), input_pins=pins,
        private_material_preimages=copied_materials, original_project_pipeline_configuration=False,
        diagnostic_selected_hdrp_materials=42, historical_failures_preserved=True))
    with (ROOT / 'controller.log').open('xb') as log:
        child = subprocess.Popen([sys.executable, '-B', str(Path(__file__).resolve()), 'run'],
            cwd=ROOT, stdout=log, stderr=log, stdin=subprocess.DEVNULL,
            creationflags=subprocess.CREATE_NO_WINDOW)
    common.save('JOB.v5.json', dict(controller_pid=child.pid, created_utc=common.now()))
    print(dict(controller_pid=child.pid, actual_api_compile_exit_code=code, requested=42))


def run():
    state = dict(status='prechecking_native_prepared_hdrp_capture', full_import=False,
                 gpu_render_parity_qualified=False, studio_verified=False)
    common.save('RUN-RESULT.v5.json', state)
    try:
        prep = common.read(ROOT / 'PREPARED.v5.json')
        common.check(prep['input_pins'])
        no_unity()
        env = os.environ.copy()
        env.update(TEMP=str(ROOT), TMP=str(ROOT), UPM_CACHE_ROOT=str(common.OLD / 'upm-cache'))
        args = ['F:/Unity/6000.5.8f1/Editor/Unity.exe', '-batchmode', '-force-d3d11', '-quit',
                '-projectPath', prep['project'], '-executeMethod',
                'OpenRPGOriginalGraphHDRP.OriginalGraphMaterialSnapshotPreparedHDRPV5.Export',
                '--openrpg-manifest', str(ROOT / 'manifest.v5.json'), '-logFile', str(ROOT / 'unity.log')]
        with (ROOT / 'unity.stdout.log').open('xb') as out, (ROOT / 'unity.stderr.log').open('xb') as err:
            unity = subprocess.Popen(args, cwd=ROOT, env=env, stdout=out, stderr=err,
                stdin=subprocess.DEVNULL, creationflags=subprocess.CREATE_NO_WINDOW)
            state.update(status='capturing_42_material_passes_after_native_hdrp_preparation', unity_pid=unity.pid)
            common.save('RUN-RESULT.v5.json', state)
            state['actual_unity_exit_code'] = unity.wait()
        common.check(prep['input_pins'])
        native = common.read(ROOT / 'NATIVE-RESULT.v5.json')
        state.update(captured=native['captured'], failed=native['failed'],
                     native_result_sha256=common.sha(ROOT / 'NATIVE-RESULT.v5.json'))
        assert state['actual_unity_exit_code'] == 0 and native['requested'] == native['captured'] == 42
        assert not native['failed'] and len(native['cases']) == 42 and native['graphics_api'] != 'Null'
        assert native['selected_pipeline_type'] == 'UnityEngine.Rendering.HighDefinition.HDRenderPipelineAsset'
        for expected, row in zip(common.read(ROOT / 'manifest.v5.json')['cases'], native['cases']):
            assert row['material_guid'] == expected['material_guid'] and row['actual_material_file_id'] == '2100000'
            assert row['shader_guid'] == expected['expected_shader_guid'] and row['shader_file_id'] == expected['expected_shader_file_id']
            assert row['active_render_pipeline_tag'] == 'HDRenderPipeline' and row['shader_supported']
            assert not row['error'] and not row['shader_has_errors'] and row['compile_passes_executed'] and row['passes']
            assert all(p['compiled'] and p['name'] and not p['name'].startswith('<Unnamed') for p in row['passes'])
        state.update(status='42_original_material_passes_captured_with_prepared_native_hdrp',
                     original_sources_unchanged=True, independent_reopen_verified=False,
                     original_project_pipeline_configuration=False)
    except Exception as error:
        state.update(status='refused', error=str(error))
    state['finished_utc'] = common.now()
    common.save('RUN-RESULT.v5.json', state)
    print(state)


if __name__ == '__main__':
    run() if len(sys.argv) > 1 and sys.argv[1] == 'run' else prepare()
