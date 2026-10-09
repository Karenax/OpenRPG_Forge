"""Retain two cold/warm frames per view, then compare native mask controls.

Exact repeat remains mandatory. Cold-start and full-prefab behavior are separate
unqualified requirements; no failed images are removed or counted as assets.
"""
from pathlib import Path
import ast
import importlib.util
import json
import sys

HERE=Path(__file__).resolve().parent
BASE=HERE/'queue_source_null_mask_hdrp_v6.py'
spec=importlib.util.spec_from_file_location('camera_clock_v6',BASE)
probe=importlib.util.module_from_spec(spec);spec.loader.exec_module(probe)
ROOT=Path('F:/OpenRPG_ImportWork/unity-source-null-mask-hdrp-v7')
probe.ROOT=ROOT;probe.probe.ROOT=ROOT;probe.probe.probe.ROOT=ROOT


def configure():
    namespace=probe.configure();namespace['__file__']=str(Path(__file__).resolve())
    original=probe.probe.probe.probe.BASE.read_text('utf-8-sig')
    for name in ('prepare','run'):
        node=next(n for n in ast.parse(original).body if isinstance(n,ast.FunctionDef) and n.name==name)
        text=ast.unparse(node).replace('OpenRPGSourceNullMaskHdrpProbe.Export','OpenRPGSourceNullMaskHdrpWarmV7Probe.Export')
        text=text.replace('SourceNullMaskHdrpProbe.cs','SourceNullMaskHdrpWarmV7Probe.cs')
        if name=='prepare':
            text=probe.probe.once(text,"assert sha(target) == digest, 'Existing private source differs; retain it: ' + str(target)",
                'if sha(target) != digest: digest = retain_private_context(origin, target, digest)')
            text=text.replace('expected_images=12','expected_images=18')
        else:
            text=probe.probe.once(text,"report = read(ROOT / 'NATIVE.v1.json')",
                "report = read(ROOT / 'NATIVE.v1.json')\n        assert report['diagnostic_preview_scene_isolated'] and report['active_editor_scene_preserved']\n        assert report['diagnostic_camera_postprocess_disabled'] and report['diagnostic_camera_exposure_disabled']\n        assert all(row['effective_hd_camera_time'] == 0 and row['same_hd_request_camera'] for row in report['rows'])\n        assert sum(row['variant'].startswith('original_null_warmup_') for row in report['rows']) == 6")
            text=text.replace("len(report['rows']) == 12","len(report['rows']) == 18")
            text=text.replace('pixels_reopened=12288','pixels_reopened=18432')
            text=text.replace('images_verified=12','images_verified=18').replace('pixels_verified=12288','pixels_verified=18432')
        exec(compile(text,str(Path(__file__).resolve()),'exec'),namespace)
    inherited_save=namespace['save']
    sdk=namespace['PROJECT']/'Library/PackageCache/com.unity.render-pipelines.high-definition@fc55a8abadc2/Runtime/RenderPipeline'
    def scoped_save(name,data):
        if name=='PREPARED.v1.json':
            for path in [BASE,sdk/'Camera/HDCamera.cs',sdk/'HDRenderPipeline.cs']:
                data['input_pins'][str(path)]=namespace['sha'](path)
        if name in {'PREPARED.v1.json','RUN-RESULT.v1.json','COMPARISON.v1.json'}:
            data.update(warmup_images_retained=6,comparison_images=12,
                cold_start_qualified=False,autonomous_behavior_qualified=False)
        inherited_save(name,data)
        if name=='PREPARED.v1.json':
            # The retained v2 context writer describes its older strict-target
            # protocol. Replace that field only with this explicit v7 scope.
            corrected=namespace['read'](ROOT/name)
            corrected.update(target_material_and_geometry_require_original_hashes=False,
                geometry_requires_original_hashes=True,native_effective_target_requires_exact_diagnosed_hash=True)
            path=ROOT/(name+'.scope.tmp');path.write_text(json.dumps(corrected,indent=2),'utf-8');path.replace(ROOT/name)
    namespace['save']=scoped_save
    return namespace


if __name__=='__main__':
    namespace=configure()
    if len(sys.argv)>1 and sys.argv[1]=='run':namespace['run']()
    else:
        assert not ROOT.exists()
        previous=json.loads((ROOT.parent/'unity-source-null-mask-hdrp-v6/RUN-RESULT.v1.json').read_text('utf-8-sig'))
        assert previous.get('finished_utc') and previous['unity_exit_code']==0
        assert previous['error']=='AssertionError: Native original render not repeatable'
        helper=HERE/'candidates/SourceNullMaskHdrpWarmV7Probe.cs';assert not helper.exists()
        text=(HERE/'candidates/SourceNullMaskHdrpClockV6Probe.cs').read_text('utf-8-sig')
        text=text.replace('OpenRPGSourceNullMaskHdrpClockV6Probe','OpenRPGSourceNullMaskHdrpWarmV7Probe')
        text=probe.probe.once(text,'new[]{"original_null","explicit_white_control","explicit_black_control","original_null_repeat"}',
            'new[]{"original_null_warmup_0","original_null_warmup_1","original_null","explicit_white_control","explicit_black_control","original_null_repeat"}')
        helper.write_text(text,'utf-8');namespace['HELPER']=helper;namespace['prepare']()
