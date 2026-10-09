"""Isolate native-effective mask measurement in a private preview scene.

Retains original mesh, slots and transforms; records HDRP time globals. This
does not alter, replace, close or qualify the original editor scenes.
"""
from pathlib import Path
import ast
import importlib.util
import json
import sys

HERE=Path(__file__).resolve().parent
BASE=HERE/'queue_source_null_mask_hdrp_v4.py'
spec=importlib.util.spec_from_file_location('native_effective_mask_v4',BASE)
probe=importlib.util.module_from_spec(spec);spec.loader.exec_module(probe)
ROOT=Path('F:/OpenRPG_ImportWork/unity-source-null-mask-hdrp-v5')
probe.ROOT=ROOT


def once(text,old,new):
    assert text.count(old)==1,'Native protocol shape differs: '+old
    return text.replace(old,new)


def configure():
    namespace=probe.configure()
    namespace['__file__']=str(Path(__file__).resolve())
    original=probe.probe.BASE.read_text('utf-8-sig')
    for name in ('prepare','run'):
        node=next(n for n in ast.parse(original).body if isinstance(n,ast.FunctionDef) and n.name==name)
        text=ast.unparse(node).replace('OpenRPGSourceNullMaskHdrpProbe.Export','OpenRPGSourceNullMaskHdrpPreviewV5Probe.Export')
        text=text.replace('SourceNullMaskHdrpProbe.cs','SourceNullMaskHdrpPreviewV5Probe.cs')
        if name=='prepare':
            text=once(text,"assert sha(target) == digest, 'Existing private source differs; retain it: ' + str(target)",
                'if sha(target) != digest: digest = retain_private_context(origin, target, digest)')
        else:
            text=once(text,"report = read(ROOT / 'NATIVE.v1.json')",
                "report = read(ROOT / 'NATIVE.v1.json')\n        assert report['diagnostic_preview_scene_isolated'] and report['active_editor_scene_preserved']\n        assert report['diagnostic_camera_postprocess_disabled'] and report['diagnostic_camera_exposure_disabled']")
        exec(compile(text,str(Path(__file__).resolve()),'exec'),namespace)
    return namespace


if __name__=='__main__':
    namespace=configure()
    if len(sys.argv)>1 and sys.argv[1]=='run':namespace['run']()
    else:
        assert not ROOT.exists()
        previous=json.loads((ROOT.parent/'unity-source-null-mask-hdrp-v4/RUN-RESULT.v1.json').read_text('utf-8-sig'))
        assert previous.get('finished_utc') and previous['unity_exit_code']==0
        assert previous['error']=='AssertionError: Native original render not repeatable'
        helper=HERE/'candidates/SourceNullMaskHdrpPreviewV5Probe.cs';assert not helper.exists()
        text=(HERE/'candidates/SourceNullMaskHdrpStableV4Probe.cs').read_text('utf-8-sig')
        text=text.replace('OpenRPGSourceNullMaskHdrpStableV4Probe','OpenRPGSourceNullMaskHdrpPreviewV5Probe')
        text=once(text,'public Vector4 source_time_before,source_time_after;',
            'public Vector4 source_time_before,source_time_after,source_time_parameters_before,source_time_parameters_after;')
        text=once(text,'public bool diagnostic_camera_postprocess_disabled=true,',
            'public bool diagnostic_preview_scene_isolated=true,active_editor_scene_preserved; public bool diagnostic_camera_postprocess_disabled=true,')
        text=once(text,'var fixture=new GameObject(',
            'var activeSceneBefore=UnityEngine.SceneManagement.SceneManager.GetActiveScene();\n  var previewScene=UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();\n  var fixture=new GameObject(')
        text=once(text,'var rows=new System.Collections.Generic.List<Row>();',
            '''UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(fixture,previewScene);
  UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(cameraObject,previewScene);
  UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(lightObject,previewScene);
  camera.scene=previewScene;camera.overrideSceneCullingMask=UnityEditor.SceneManagement.EditorSceneManager.GetSceneCullingMask(previewScene);
  if(camera.scene!=previewScene)throw new Exception("Diagnostic preview scene isolation refused");
  var rows=new System.Collections.Generic.List<Row>();''')
        text=once(text,'Vector4 timeBefore=Shader.GetGlobalVector("_Time");',
            'Vector4 timeParametersBefore=Shader.GetGlobalVector("_TimeParameters");Vector4 timeBefore=Shader.GetGlobalVector("_Time");')
        text=once(text,'source_time_before=timeBefore,source_time_after=timeAfter',
            'source_time_before=timeBefore,source_time_after=timeAfter,source_time_parameters_before=timeParametersBefore,source_time_parameters_after=Shader.GetGlobalVector("_TimeParameters")')
        text=once(text,'source_mask_null=true,source_material_unchanged=true,',
            'active_editor_scene_preserved=activeSceneBefore==UnityEngine.SceneManagement.SceneManager.GetActiveScene(),source_mask_null=true,source_material_unchanged=true,')
        text=once(text,'UnityEngine.Object.DestroyImmediate(fixture);}',
            'UnityEngine.Object.DestroyImmediate(fixture);UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(previewScene);}')
        helper.write_text(text,'utf-8')
        namespace['HELPER']=helper
        namespace['prepare']()
