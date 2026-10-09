"""Compile URP bootstrap and original-material capture against installed Unity APIs."""
import datetime
import hashlib
import json
from pathlib import Path
import re
import subprocess

W = Path('F:/OpenRPG_ImportWork')
ROOT = W / 'villa-urp-native-helper-api-v1'
TEMPLATE = W / 'unity-dungeon-original-shadergraph-hdrp-startup-v4/project/Assets/Editor/OriginalGraphMaterialSnapshotHDRP.cs'
BASE = W / 'unity-dungeon-original-shadergraph-hdrp-context-v3/api-check.v3.rsp'
URP = W / 'unity-dungeon-original-shadergraph-compiler-v1/project/Library/ScriptAssemblies/Unity.RenderPipelines.Universal.Runtime.dll'
PLAN = W / 'villa-urp-native-capture-inputs-v1/RESULT.v1.json'
SDK = Path('F:/Unity/6000.5.8f1/Editor/Data/DotNetSdk')

BOOTSTRAP = '''#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
namespace OpenRPGOriginalGraphURP {
 public static class PrepareURPContext {
  public static void Export() {
   const string folder="Assets/OpenRPGProbe";
   const string pipelinePath=folder+"/SourcePassURPContext.asset";
   if(AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(pipelinePath)!=null)
    throw new Exception("Existing URP probe context preserved");
   if(!AssetDatabase.IsValidFolder(folder))AssetDatabase.CreateFolder("Assets","OpenRPGProbe");
   var renderer=ScriptableObject.CreateInstance<UniversalRendererData>();
   AssetDatabase.CreateAsset(renderer,folder+"/SourcePassURPRenderer.asset");
   var pipeline=UniversalRenderPipelineAsset.Create(renderer);
   AssetDatabase.CreateAsset(pipeline,pipelinePath);
   PlayerSettings.colorSpace=ColorSpace.Linear;
   GraphicsSettings.defaultRenderPipeline=pipeline;
   var previous=QualitySettings.GetQualityLevel();
   for(int i=0;i<QualitySettings.names.Length;i++){
    QualitySettings.SetQualityLevel(i,false);QualitySettings.renderPipeline=pipeline;
   }
   QualitySettings.SetQualityLevel(previous,false);
   AssetDatabase.SaveAssets();
   if(GraphicsSettings.currentRenderPipeline!=pipeline)throw new Exception("Selected URP probe absent");
   File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(),"URP-CONTEXT.CREATED.json"),
    "{\\"pipeline_guid\\":\\""+AssetDatabase.AssetPathToGUID(pipelinePath)+"\\",\\"original_project_pipeline_configuration\\":false}");
  }
 }
}
#endif
'''


def sha(path):
    with Path(path).open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


def main():
    assert not ROOT.exists(), 'Existing API proof preserved'
    plan = json.loads(PLAN.read_text('utf-8-sig'))
    assert len(plan['requests']) == 6
    pins = {str(p): sha(p) for p in [TEMPLATE, BASE, URP, PLAN, Path(__file__).resolve()]}
    source = TEMPLATE.read_text('utf-8-sig')
    assert source.count('UnityEngine.Rendering.HighDefinition.HDRenderPipelineAsset') == 1
    source = source.replace('UnityEngine.Rendering.HighDefinition.HDRenderPipelineAsset',
                            'UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset')
    source = source.replace('OpenRPGOriginalGraphHDRP', 'OpenRPGOriginalGraphURP')
    source = source.replace('OriginalGraphMaterialSnapshotHDRP', 'OriginalGraphMaterialSnapshotURP')
    source = source.replace('HDRenderPipeline', 'UniversalPipeline')
    source = source.replace('HDRP', 'URP')
    arguments = [line for line in BASE.read_text('utf-8-sig').splitlines()
                 if line.startswith('-') and not line.startswith('-out:')]
    if not any(str(URP) in line for line in arguments):
        arguments.append(f'-reference:"{URP}"')
    for line in arguments:
        if line.startswith('-reference:'):
            path = Path(line.split(':', 1)[1].strip('"'))
            pins[str(path)] = sha(path)
    compilers = list((SDK / 'sdk').glob('*/Roslyn/bincore/csc.dll'))
    assert len(compilers) == 1
    pins[str(compilers[0])] = sha(compilers[0])
    pins[str(SDK / 'dotnet.exe')] = sha(SDK / 'dotnet.exe')
    ROOT.mkdir()
    rows = []
    for name, text in [('PrepareURPContext', BOOTSTRAP), ('OriginalGraphMaterialSnapshotURP', source)]:
        helper = ROOT / (name + '.cs')
        helper.write_text(text, 'utf-8')
        rsp = ROOT / (name + '.rsp')
        rsp.write_text('\n'.join(arguments + [f'-out:"{ROOT / (name + ".api.dll")}"', f'"{helper}"']), 'utf-8')
        with (ROOT / (name + '.stdout.log')).open('xb') as out, (ROOT / (name + '.stderr.log')).open('xb') as err:
            exit_code = subprocess.run([str(SDK / 'dotnet.exe'), str(compilers[0]), '@' + str(rsp)],
                                       cwd=ROOT, stdout=out, stderr=err).returncode
        rows.append(dict(helper=str(helper), sha256=sha(helper), response_file=str(rsp),
                         response_sha256=sha(rsp), actual_exit_code=exit_code))
    changed = [p for p, h in pins.items() if sha(p) != h]
    result = dict(status='urp_bootstrap_and_capture_helpers_api_compiled' if not changed and all(r['actual_exit_code']==0 for r in rows)
                  else 'api_compile_refused', helpers=rows, changed_inputs=changed, input_pins=pins,
                  native_capture_executed=False, native_pipeline_created=False, gpu_verified=False,
                  original_project_pipeline_configuration=False, assets_delivered=0, full_import=False,
                  recorded_utc=datetime.datetime.now(datetime.timezone.utc).isoformat())
    (ROOT / 'RESULT.v1.json').write_text(json.dumps(result, indent=2), 'utf-8')
    print(json.dumps(dict(status=result['status'], actual_exit_codes=[r['actual_exit_code'] for r in rows])))
    if result['status']=='api_compile_refused':
        raise SystemExit(1)


if __name__ == '__main__':
    main()
