"""Queue isolated URP setup, restart, and six original-material observations."""
import os
from pathlib import Path
import subprocess
import sys

import queue_hdrp_snapshot_v4 as common

ROOT = common.W / 'unity-villa-six-original-urp-materials-v1'
API = common.W / 'villa-urp-native-helper-api-v1/RESULT.v1.json'
PLAN = common.W / 'villa-urp-native-capture-inputs-v1/RESULT.v1.json'
SOURCE = common.W / 'hivemind-run/villa-forge/variants/51e32236d3832c1f/source'
TEMPLATE = common.W / 'unity-dungeon-original-shadergraph-hdrp-startup-v4/project'
UNITY = Path('F:/Unity/6000.5.8f1/Editor/Unity.exe')
common.ROOT = ROOT


def prepare():
    assert not ROOT.exists(), 'Existing capture preserved'
    api, plan = common.read(API), common.read(PLAN)
    assert api['status'] == 'urp_bootstrap_and_capture_helpers_api_compiled'
    assert not api['changed_inputs'] and all(r['actual_exit_code'] == 0 for r in api['helpers'])
    common.check(plan['input_pins'])
    common.check(api['input_pins'])
    pins = {**plan['input_pins'], **api['input_pins'], str(API): common.sha(API),
            str(PLAN): common.sha(PLAN), str(Path(__file__).resolve()): common.sha(__file__),
            str(Path(common.__file__).resolve()): common.sha(common.__file__), str(UNITY): common.sha(UNITY)}
    ROOT.mkdir()
    project = ROOT / 'project'
    context_sources = {}
    for relative in ['Packages/manifest.json', 'Packages/packages-lock.json',
                     'ProjectSettings/ProjectVersion.txt', 'ProjectSettings/ProjectSettings.asset']:
        source, target = TEMPLATE / relative, project / relative
        expected = common.sha(source)
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_bytes(source.read_bytes())
        assert common.sha(source) == expected and common.sha(target) == expected
        context_sources[str(source)] = expected
        pins[str(target)] = expected
    for helper in api['helpers']:
        source = Path(helper['helper'])
        assert common.sha(source) == helper['sha256']
        target = project / 'Assets/Editor' / source.name
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_bytes(source.read_bytes())
        pins[str(source)] = helper['sha256']
        pins[str(target)] = helper['sha256']
    common.save('manifest.v1.json', dict(output_path=str(ROOT / 'NATIVE-RESULT.v1.json'), cases=plan['requests']))
    pins[str(ROOT / 'manifest.v1.json')] = common.sha(ROOT / 'manifest.v1.json')
    common.check(pins)
    common.save('PREPARED.v1.json', dict(project=str(project), input_pins=pins,
                                       template_context_snapshot=context_sources,
                                       plan=str(PLAN), api_proof=str(API),
                                       original_project_pipeline_configuration=False,
                                       source_assets_added_only_after_persisted_urp=True))
    with (ROOT / 'controller.log').open('xb') as log:
        controller = subprocess.Popen([sys.executable, '-B', str(Path(__file__).resolve()), 'run'],
                                      cwd=ROOT, stdout=log, stderr=log, stdin=subprocess.DEVNULL,
                                      creationflags=subprocess.CREATE_NO_WINDOW)
    common.save('JOB.v1.json', dict(controller_pid=controller.pid, created_utc=common.now()))
    print(dict(queued_pid=controller.pid, materials=6, native_observations=0))


def run():
    state = dict(status='waiting_for_exact_delta_controller', native_materials_verified=0,
                 gpu_verified=False, studio_verified=False, full_import=False, started_utc=common.now())
    common.save('RUN-RESULT.v1.json', state)
    try:
        wait = "$ErrorActionPreference='Stop';while($true){$p=Get-Process -Id 36720 -ErrorAction SilentlyContinue;if(!$p -or $p.StartTime.ToUniversalTime().Ticks -ne 639269657574011425L){break};$p.WaitForExit(45000)|Out-Null};while(Get-Process -Name Unity -ErrorAction SilentlyContinue){Start-Sleep -Seconds 30}"
        subprocess.run([str(common.PS), '-NoProfile', '-Command', wait], check=True, creationflags=subprocess.CREATE_NO_WINDOW)
        prep, plan = common.read(ROOT / 'PREPARED.v1.json'), common.read(PLAN)
        common.check(prep['input_pins'])
        project = Path(prep['project'])
        env = os.environ.copy()
        env.update(TEMP=str(ROOT), TMP=str(ROOT), UPM_CACHE_ROOT=str(common.OLD / 'upm-cache'))

        def launch(method, stage):
            args = [str(UNITY), '-batchmode', '-force-d3d11', '-quit', '-projectPath', str(project),
                    '-executeMethod', method, '--openrpg-manifest', str(ROOT / 'manifest.v1.json'),
                    '-logFile', str(ROOT / (stage + '.unity.log'))]
            with (ROOT / (stage + '.stdout.log')).open('xb') as out, (ROOT / (stage + '.stderr.log')).open('xb') as err:
                child = subprocess.Popen(args, cwd=ROOT, env=env, stdout=out, stderr=err,
                                         stdin=subprocess.DEVNULL, creationflags=subprocess.CREATE_NO_WINDOW)
                state.update(status=stage, unity_pid=child.pid)
                common.save('RUN-RESULT.v1.json', state)
                code = child.wait()
            state[stage + '_exit_code'] = code
            if code != 0:
                raise ValueError('Unity stage refused: ' + stage)

        launch('OpenRPGOriginalGraphURP.PrepareURPContext.Export', 'creating_isolated_urp_context')
        context = common.read(ROOT / 'URP-CONTEXT.CREATED.json')
        assert len(context['pipeline_guid']) == 32
        pipeline = project / 'Assets/OpenRPGProbe/SourcePassURPContext.asset'
        assert pipeline.is_file()
        settings = (project / 'ProjectSettings/GraphicsSettings.asset').read_text('utf-8-sig')
        assert context['pipeline_guid'] in settings
        common.save('URP-STARTUP-CONTEXT.v1.json', dict(pipeline_guid=context['pipeline_guid'],
                                                      pipeline_sha256=common.sha(pipeline),
                                                      graphics_settings_sha256=common.sha(project / 'ProjectSettings/GraphicsSettings.asset'),
                                                      original_project_pipeline_configuration=False))
        copied = {}
        common.check(plan['input_pins'])
        for row in plan['source_files']:
            for name in ['source', 'meta_source']:
                source = Path(row[name])
                target = project / source.relative_to(SOURCE)
                expected = plan['input_pins'][str(source)]
                assert source.suffix.lower() not in ['.cs', '.dll', '.exe']
                assert not target.exists(), 'Existing source copy preserved'
                target.parent.mkdir(parents=True, exist_ok=True)
                target.write_bytes(source.read_bytes())
                assert common.sha(target) == expected
                copied[str(target)] = expected
        common.save('COPIED-SOURCES.v1.json', copied)
        launch('OpenRPGOriginalGraphURP.OriginalGraphMaterialSnapshotURP.Export', 'capturing_six_original_urp_materials')
        common.check(plan['input_pins'])
        common.check({p: h for p, h in copied.items() if not p.endswith('.meta')})
        native = common.read(ROOT / 'NATIVE-RESULT.v1.json')
        assert native['requested'] == native['captured'] == len(native['cases']) == 6 and native['failed'] == 0
        assert native['graphics_api'] != 'Null'
        assert native['selected_pipeline_type'] == 'UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset'
        for request, row in zip(plan['requests'], native['cases']):
            assert row['material_guid'] == request['material_guid'] and row['actual_material_file_id'] == '2100000'
            assert row['shader_guid'] == request['expected_shader_guid'] and row['shader_file_id'] == request['expected_shader_file_id']
            assert row['active_render_pipeline_tag'] == 'UniversalPipeline'
            assert not row['error'] and not row['shader_has_errors'] and row['shader_supported'] and row['compile_passes_executed']
            assert row['passes'] and all(p['compiled'] and p['name'] and not p['name'].startswith('<Unnamed') for p in row['passes'])
            properties = {p['name']: p for p in row['properties']}
            assert len(properties) == len(row['properties'])
            for required in request['required_textures']:
                actual = properties[required['property']]
                assert not actual['texture_null'] and actual['texture_exact_identity']
                assert actual['texture_guid'] == required['guid'] and actual['texture_file_id'] == required['file_id']
            for default in plan['native_default_observations']:
                if default['material_guid'] == request['material_guid']:
                    assert properties[default['property']]['type'] == 'Texture'
        state.update(status='six_original_urp_material_passes_and_defaults_captured', native_materials_verified=6,
                     native_result_sha256=common.sha(ROOT / 'NATIVE-RESULT.v1.json'), independent_reopening_required=True)
    except Exception as error:
        state.update(status='refused', error=str(error))
    state['finished_utc'] = common.now()
    common.save('RUN-RESULT.v1.json', state)


if __name__ == '__main__':
    {'prepare': prepare, 'run': run}[sys.argv[1]]()
