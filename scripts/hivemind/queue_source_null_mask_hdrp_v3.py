"""Separate deterministic-camera diagnostic; keep v2 repeat refusal intact.

Only the measurement camera disables exposure/postprocessing. Purchased materials,
meshes and the original full-scene requirements remain unchanged.
"""
from pathlib import Path
import ast
import importlib.util
import json

HERE = Path(__file__).resolve().parent
BASE = HERE/'queue_source_null_mask_hdrp_v2.py'
spec = importlib.util.spec_from_file_location('null_mask_context_v2', BASE)
probe = importlib.util.module_from_spec(spec)
spec.loader.exec_module(probe)
ROOT = Path('F:/OpenRPG_ImportWork/unity-source-null-mask-hdrp-v3')


def main():
    assert not ROOT.exists()
    previous = json.loads((ROOT.parent/'unity-source-null-mask-hdrp-v2/RUN-RESULT.v1.json').read_text('utf-8-sig'))
    assert previous.get('finished_utc') and previous['unity_exit_code']==0
    assert previous['error']=='AssertionError: Native original render not repeatable'
    helper = HERE/'candidates/SourceNullMaskHdrpStableProbe.cs'
    assert not helper.exists()
    source = (HERE/'candidates/SourceNullMaskHdrpProbe.cs').read_text('utf-8-sig')
    source = source.replace('OpenRPGSourceNullMaskHdrpProbe', 'OpenRPGSourceNullMaskHdrpStableProbe')
    old = 'extra.backgroundColorHDR=new Color(.03125f,.0625f,.125f,1);'
    assert source.count(old)==1
    source = source.replace(old, old+'''
  // Diagnostic camera only; retain original materials and shader behavior.
  extra.customRenderingSettings=true;extra.antialiasing=HDAdditionalCameraData.AntialiasingMode.None;
  foreach(var field in new[]{FrameSettingsField.Postprocess,FrameSettingsField.ExposureControl}) {
   extra.renderingPathCustomFrameSettings.SetEnabled(field,false);
   extra.renderingPathCustomFrameSettingsOverrideMask.mask[(uint)field]=true;
  }
''')
    source = source.replace('public bool source_material_unchanged,',
        'public bool diagnostic_camera_postprocess_disabled=true,diagnostic_camera_exposure_disabled=true; public bool source_material_unchanged,')
    helper.write_text(source,'utf-8')
    probe.ROOT = ROOT
    namespace = probe.namespace
    namespace.update(ROOT=ROOT,HELPER=helper,__file__=str(Path(__file__).resolve()))
    original = probe.BASE.read_text('utf-8-sig')
    for function_name in ('prepare','run'):
        node = next(n for n in ast.parse(original).body if isinstance(n,ast.FunctionDef) and n.name==function_name)
        text = ast.unparse(node).replace('OpenRPGSourceNullMaskHdrpProbe.Export','OpenRPGSourceNullMaskHdrpStableProbe.Export')
        text = text.replace('SourceNullMaskHdrpProbe.cs','SourceNullMaskHdrpStableProbe.cs')
        if function_name=='prepare':
            old_branch = "assert sha(target) == digest, 'Existing private source differs; retain it: ' + str(target)"
            assert text.count(old_branch)==1
            text = text.replace(old_branch, 'if sha(target) != digest: digest = retain_private_context(origin, target, digest)')
        exec(compile(text,str(Path(__file__).resolve()),'exec'),namespace)
    namespace['prepare']()


if __name__=='__main__':
    import sys
    if len(sys.argv)>1 and sys.argv[1]=='run':
        probe.ROOT=ROOT
        probe.namespace.update(ROOT=ROOT,__file__=str(Path(__file__).resolve()))
        node=next(n for n in ast.parse(probe.BASE.read_text('utf-8-sig')).body if isinstance(n,ast.FunctionDef) and n.name=='run')
        code=ast.unparse(node).replace('OpenRPGSourceNullMaskHdrpProbe.Export','OpenRPGSourceNullMaskHdrpStableProbe.Export')
        exec(compile(code,str(Path(__file__).resolve()),'exec'),probe.namespace)
        probe.namespace['run']()
    else: main()
