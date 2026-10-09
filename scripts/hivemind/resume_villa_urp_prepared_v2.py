"""Six original URP materials in a separate, explicitly initialized context."""
import os
from pathlib import Path
import subprocess
import sys

import queue_hdrp_snapshot_v4 as common
from verify_native_urp_material_snapshot import reconcile_urp

ROOT = common.W / 'unity-villa-prepared-native-urp-v2'
OLD = common.W / 'unity-villa-six-original-urp-materials-v1'
API = common.W / 'villa-urp-native-helper-api-v1'
PLAN = common.W / 'villa-urp-native-capture-inputs-v1/RESULT.v1.json'
SOURCE = common.W / 'hivemind-run/villa-forge/variants/51e32236d3832c1f/source'
common.ROOT = ROOT


def no_unity():
    assert int(subprocess.check_output([str(common.PS), '-NoProfile', '-Command',
        '@(Get-Process -Name Unity -ErrorAction SilentlyContinue).Count'], text=True).strip()) == 0, 'Unity queue active'


def prepare():
    assert not ROOT.exists(), 'Existing version preserved'
    no_unity()
    old = common.read(OLD / 'PREPARED.v1.json')
    common.check(old['input_pins'])
    plan = common.read(PLAN)
    common.check(plan['input_pins'])
    assert len(plan['requests']) == 6 and len(plan['native_default_observations']) == 10
    project = ROOT / 'project'
    helper_text = (API / 'OriginalGraphMaterialSnapshotURP.cs').read_text('utf-8-sig')
    helper_text = helper_text.replace('namespace OpenRPGOriginalGraphURP', 'namespace OpenRPGPreparedURPV2')
    marker = '   foreach(var item in request.cases){var path='
    assert helper_text.count(marker) == 1
    helper_text = helper_text.replace(marker, '''   var preparePipeline=typeof(UnityEngine.Rendering.RenderPipelineManager).GetMethod("TryPrepareRenderPipeline",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Public,null,new Type[]{typeof(UnityEngine.Rendering.RenderPipelineAsset)},null);
   if(preparePipeline==null||preparePipeline.ReturnType!=typeof(bool))throw new Exception("Exact native pipeline preparation method unavailable");
   if(!(bool)preparePipeline.Invoke(null,new object[]{pipeline})||UnityEngine.Rendering.RenderPipelineManager.currentPipeline==null||UnityEngine.Rendering.RenderPipelineManager.currentPipeline.GetType().FullName!="UnityEngine.Rendering.Universal.UniversalRenderPipeline")throw new Exception("Native URP instance not initialized; no fallback");
''' + marker, 1)
    bootstrap = (API / 'PrepareURPContext.cs').read_text('utf-8-sig').replace('namespace OpenRPGOriginalGraphURP', 'namespace OpenRPGPreparedURPV2')
    bootstrap = bootstrap.replace('  public static void Export() {', '''  static string ManifestPath(){var a=Environment.GetCommandLineArgs();for(int i=0;i+1<a.Length;i++)if(a[i]=="--openrpg-manifest")return a[i+1];throw new Exception("Manifest argument missing");}
  public static void Export() {''', 1)
    bootstrap = bootstrap.replace('Directory.GetCurrentDirectory()', 'Path.GetDirectoryName(ManifestPath())')
    ROOT.mkdir()
    inputs = dict(plan['input_pins'])
    for relative in ['Packages/manifest.json','Packages/packages-lock.json',
            'ProjectSettings/ProjectVersion.txt','ProjectSettings/ProjectSettings.asset']:
        source = Path(old['project']) / relative
        assert common.sha(source) == old['input_pins'][str(source)]
        target = project / relative
        target.parent.mkdir(parents=True, exist_ok=True)
        with target.open('xb') as stream:
            stream.write(source.read_bytes())
        inputs[str(source)] = common.sha(source)
    api_codes = []
    sdk = Path('F:/Unity/6000.5.8f1/Editor/Data/DotNetSdk')
    compilers = list((sdk / 'sdk').glob('*/Roslyn/bincore/csc.dll'))
    assert len(compilers) == 1
    for name, contents in [('PrepareURPContext', bootstrap), ('OriginalGraphMaterialSnapshotURP', helper_text)]:
        source = API / (name + '.cs')
        assert common.sha(source) == old['input_pins'][str(source)]
        helper = project / 'Assets/Editor' / (name + '.cs')
        helper.parent.mkdir(parents=True, exist_ok=True)
        helper.write_text(contents, encoding='utf-8')
        inputs[str(source)] = common.sha(source)
        inputs[str(helper)] = common.sha(helper)
        flags = [line for line in (API / (name + '.rsp')).read_text('utf-8-sig').splitlines()
            if line.startswith('-') and not line.startswith('-out:')]
        rsp = ROOT / (name + '.rsp')
        rsp.write_text('\n'.join(flags + [f'-out:"{ROOT / (name + ".api.dll")}"',f'"{helper}"']),encoding='utf-8')
        with (ROOT / (name + '.api.log')).open('xb') as log:
            code = subprocess.run([str(sdk / 'dotnet.exe'),str(compilers[0]),'@'+str(rsp)],
                cwd=ROOT,stdout=log,stderr=log,creationflags=subprocess.CREATE_NO_WINDOW).returncode
        api_codes.append(code)
        assert code == 0, 'Prepared URP helper API compilation refused'
    common.save('manifest.v2.json',dict(output_path=str(ROOT / 'NATIVE-RESULT.v2.json'),cases=plan['requests']))
    for path in [PLAN,Path(__file__).resolve(),Path(common.__file__).resolve(),
            Path(sys.modules[reconcile_urp.__module__].__file__).resolve(), ROOT / 'manifest.v2.json']:
        inputs[str(path)] = common.sha(path)
    common.check(inputs)
    common.save('PREPARED.v2.json',dict(project=str(project),input_pins=inputs,
        api_compile_exit_codes=api_codes,requested_materials=6,requested_defaults=10,
        original_graph_references_without_source=plan.get('unresolved_graph_references',[]),
        original_graph_semantics_rewritten=False,source_scripts_executed=False,full_import=False))
    with (ROOT / 'controller.log').open('xb') as log:
        child = subprocess.Popen([sys.executable,'-B',str(Path(__file__).resolve()),'run'],
            cwd=ROOT,stdout=log,stderr=log,stdin=subprocess.DEVNULL,creationflags=subprocess.CREATE_NO_WINDOW)
    common.save('JOB.v2.json',dict(controller_pid=child.pid,created_utc=common.now()))
    print(dict(controller_pid=child.pid,materials=6,defaults=10,api_exit_codes=api_codes))


def run():
    state = dict(status='prechecking_prepared_native_urp',native_materials_verified=0,
        native_defaults_verified=0,gpu_render_parity_qualified=False,studio_verified=False,full_import=False)
    common.save('RUN-RESULT.v2.json',state)
    try:
        prep,plan = common.read(ROOT / 'PREPARED.v2.json'),common.read(PLAN)
        common.check(prep['input_pins'])
        project = Path(prep['project'])
        env = os.environ.copy()
        env.update(TEMP=str(ROOT),TMP=str(ROOT),UPM_CACHE_ROOT=str(common.OLD / 'upm-cache'))
        def launch(method,stage):
            no_unity()
            with (ROOT / (stage + '.stdout.log')).open('xb') as out,(ROOT / (stage + '.stderr.log')).open('xb') as err:
                child = subprocess.Popen(['F:/Unity/6000.5.8f1/Editor/Unity.exe','-batchmode','-force-d3d11','-quit',
                    '-projectPath',str(project),'-executeMethod',method,'--openrpg-manifest',str(ROOT / 'manifest.v2.json'),
                    '-logFile',str(ROOT / (stage + '.unity.log'))],cwd=ROOT,env=env,stdout=out,stderr=err,
                    stdin=subprocess.DEVNULL,creationflags=subprocess.CREATE_NO_WINDOW)
                state.update(status=stage,unity_pid=child.pid)
                common.save('RUN-RESULT.v2.json',state)
                state[stage + '_exit_code'] = child.wait()
            assert state[stage + '_exit_code'] == 0, 'Unity stage refused: '+stage
        launch('OpenRPGPreparedURPV2.PrepareURPContext.Export','creating_separate_urp_context')
        context = common.read(ROOT / 'URP-CONTEXT.CREATED.json')
        assert len(context['pipeline_guid']) == 32
        assert context['pipeline_guid'] in (project / 'ProjectSettings/GraphicsSettings.asset').read_text('utf-8-sig')
        common.check(plan['input_pins'])
        copies = []
        for row in plan['source_files']:
            for key in ['source','meta_source']:
                source = Path(row[key])
                assert source.resolve().is_relative_to(SOURCE.resolve()) and not source.is_symlink()
                assert source.suffix.lower() not in ['.cs','.dll','.exe']
                target = project / source.relative_to(SOURCE)
                assert target.resolve().is_relative_to((project / 'Assets').resolve()) and not target.exists()
                expected = plan['input_pins'][str(source)]
                target.parent.mkdir(parents=True,exist_ok=True)
                with target.open('xb') as stream:
                    stream.write(source.read_bytes())
                assert common.sha(target) == expected
                copies.append(dict(source=str(source),target=str(target),sha256=expected))
        common.save('COPIED-SOURCES.v2.json',dict(copies=copies,source_scripts_executed=False))
        launch('OpenRPGPreparedURPV2.OriginalGraphMaterialSnapshotURP.Export','capturing_six_prepared_original_urp_materials')
        common.check(prep['input_pins'])
        native = common.read(ROOT / 'NATIVE-RESULT.v2.json')
        rows = reconcile_urp(plan,native)
        state.update(status='six_original_urp_materials_and_ten_defaults_captured',native_materials_verified=6,
            native_defaults_verified=10,native_result_sha256=common.sha(ROOT / 'NATIVE-RESULT.v2.json'),
            original_sources_unchanged=True,independent_reopen_required=True)
    except Exception as error:
        state.update(status='refused',error=f'{type(error).__name__}: {error}')
    state['finished_utc'] = common.now()
    common.save('RUN-RESULT.v2.json',state)


if __name__ == '__main__':
    run() if len(sys.argv)>1 and sys.argv[1]=='run' else prepare()
