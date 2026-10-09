"""Retain focused native geometry and increase readback to 128 square pixels."""
from pathlib import Path
import ast
import importlib.util
import json
import sys

HERE = Path(__file__).resolve().parent
ROOT = Path('F:/OpenRPG_ImportWork/unity-source-null-mask-surface-v10')
spec = importlib.util.spec_from_file_location('retained_native_protocol', HERE/'queue_source_null_mask_hdrp_v7.py')
base = importlib.util.module_from_spec(spec)
spec.loader.exec_module(base)
base.ROOT = ROOT
base.probe.ROOT = ROOT
base.probe.probe.ROOT = ROOT
base.probe.probe.probe.ROOT = ROOT

def configure():
    ns = base.configure()
    ns['__file__'] = str(Path(__file__).resolve())
    original = (HERE/'queue_source_null_mask_hdrp_v1.py').read_text('utf-8-sig')
    for name in ('prepare', 'run'):
        node = next(node for node in ast.parse(original).body if isinstance(node, ast.FunctionDef) and node.name == name)
        text = ast.unparse(node)
        text = text.replace('OpenRPGSourceNullMaskHdrpProbe.Export', 'OpenRPGSourceNullMaskHdrpSurfaceV10Probe.Export')
        text = text.replace('SourceNullMaskHdrpProbe.cs', 'SourceNullMaskHdrpSurfaceV10Probe.cs')
        if name == 'prepare':
            old = "assert sha(target) == digest, 'Existing private source differs; retain it: ' + str(target)"
            assert text.count(old) == 1
            text = text.replace(old, 'if sha(target) != digest: digest = retain_private_context(origin, target, digest)')
            text = text.replace('expected_images=12', 'expected_images=36')
        else:
            text = text.replace("report = read(ROOT / 'NATIVE.v1.json')", "report = read(ROOT / 'NATIVE.v1.json')\n        assert report['surface_data_only'] and report['debug_material_settings_restored']\n        assert report['diagnostic_preview_scene_isolated'] and report['active_editor_scene_preserved']\n        assert report['targeted_submesh_indices'] > 0\n        assert all(row['width'] == 128 and row['height'] == 128 and row['effective_hd_camera_time'] == 0 and row['same_hd_request_camera'] and row['native_debug_mode_active'] and row['debug_surface_mode'] == (1005 if row['camera'] < 3 else 1006) for row in report['rows'])")
            text = text.replace("len(report['rows']) == 12", "len(report['rows']) == 36")
            text = text.replace('len(raw) == 8192', 'len(raw) == 131072')
            text = text.replace('assert set(groups) == {0, 1, 2}', 'assert set(groups) == set(range(6))')
            text = text.replace('pixels_reopened=12288', 'pixels_reopened=589824')
            text = text.replace('images_verified=12', 'images_verified=36').replace('pixels_verified=12288', 'pixels_verified=589824')
        exec(compile(text, str(Path(__file__).resolve()), 'exec'), ns)
    inherited_save = ns['save']
    def save(name, data):
        inherited_save(name, data)
        if name in {'PREPARED.v1.json', 'RUN-RESULT.v1.json', 'COMPARISON.v1.json'}:
            data = ns['read'](ROOT/name)
            data.update(warmup_images_retained=12, comparison_images=24, surface_data_only=True,
                lit_render_qualified=False, expected_width=128, expected_height=128,
                minimum_positive_pixels_per_surface=32)
            temporary = ROOT/(name+'.resolution.tmp')
            temporary.write_text(json.dumps(data, indent=2), 'utf-8')
            temporary.replace(ROOT/name)
    ns['save'] = save
    return ns

if __name__ == '__main__':
    ns = configure()
    if len(sys.argv)>1 and sys.argv[1]=='run':
        ns['run']()
    else:
        assert not ROOT.exists()
        prior = ns['read'](ROOT.parent/'unity-source-null-mask-surface-v9/RUN-RESULT.v1.json')
        assert prior['unity_exit_code'] == 0 and prior['finished_utc']
        helper = HERE/'candidates/SourceNullMaskHdrpSurfaceV10Probe.cs'
        assert not helper.exists()
        text = (HERE/'candidates/SourceNullMaskHdrpSurfaceV9Probe.cs').read_text('utf-8-sig')
        text = text.replace('SurfaceV9', 'SurfaceV10')
        for old,new in [('width=32,height=32', 'width=128,height=128'),
            ('new RenderTexture(32,32,', 'new RenderTexture(128,128,'),
            ('new Texture2D(32,32,', 'new Texture2D(128,128,'),
            ('new Rect(0,0,32,32)', 'new Rect(0,0,128,128)')]:
            assert text.count(old) == 1
            text = text.replace(old,new)
        helper.write_text(text, 'utf-8')
        ns['HELPER'] = helper
        ns['prepare']()
