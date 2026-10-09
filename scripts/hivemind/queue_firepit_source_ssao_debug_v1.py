"""Capture SDK full-screen SSAO diagnostic, separate from material AO."""
from pathlib import Path
import runpy
import sys

loaded = runpy.run_path(str(Path(__file__).with_name('queue_firepit_source_surface_debug_v1.py')), run_name='ssao_diagnostic')
g = loaded['g']
original_helper = g['helper']
popen = loaded['popen']

def helper():
    text = original_helper().replace('SourceSurfaceDebugV1', 'SourceSsaoDebugV1')
    before = 'var diagnostics=new[]{"Albedo","Normal","Smoothness","AmbientOcclusion","Metal","Specular","DirectDiffuseLighting","IndirectDiffuseLighting"};'
    assert text.count(before) == 1
    text = text.replace(before, 'var diagnostics=new[]{"ScreenSpaceAmbientOcclusion"};')
    before = 'if(diagnostic.EndsWith("Lighting"))debug.SetDebugLightingMode((DebugLightingMode)Enum.Parse(typeof(DebugLightingMode),diagnostic));'
    assert text.count(before) == 1
    text = text.replace(before, 'if(diagnostic=="ScreenSpaceAmbientOcclusion")debug.SetFullScreenDebugMode(FullScreenDebugMode.ScreenSpaceAmbientOcclusion);\n    else ' + before)
    before = 'finally{debug.SetDebugViewCommonMaterialProperty'
    assert text.count(before) == 1
    return text.replace(before, 'finally{debug.SetFullScreenDebugMode(FullScreenDebugMode.None);debug.SetDebugViewCommonMaterialProperty')

def corrected_popen(args, *a, **kw):
    if isinstance(args, list) and args and str(args[0]) == str(g['UNITY']):
        args = list(args)
        i = args.index('-executeMethod') + 1
        assert args[i] == 'OpenRPGSourceShadowReferenceV8.Export'
        args[i] = 'OpenRPGSourceSsaoDebugV1.Export'
    return popen(args, *a, **kw)

g['subprocess'].Popen = corrected_popen
g.update(ROOT=g['W']/'firepit-source-ssao-debug-v1',
    HELPER=g['PROJECT']/'Assets/Editor/SourceSsaoDebugV1.cs', helper=helper,
    __file__=str(Path(__file__).resolve()))

if __name__ == '__main__':
    if sys.argv[-1] == 'prepare':
        g['prepare']()
    else:
        g['run']()
        state = g['read'](g['ROOT']/'RUN-RESULT.v1.json')
        files = [g['ROOT']/f'DEBUG-ScreenSpaceAmbientOcclusion.{ext}' for ext in ['rgba16f','rgba16f.json']]
        if state.get('actual_exit_code') == 0 and all(p.exists() for p in files):
            state.update(status='source_ssao_fullscreen_gpu_diagnostic_captured',
                debug_files={str(p): g['sha'](p) for p in files}, ssao_transport_qualified=False)
        else:
            state.update(status='source_ssao_debug_capture_refused')
        g['save']('RUN-RESULT.v1.json',state)
