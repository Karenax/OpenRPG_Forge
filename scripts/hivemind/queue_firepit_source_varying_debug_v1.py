"""Native HDRP UV/TBN varying diagnostics without replacing purchased shaders."""
from pathlib import Path
import runpy
import sys

BASE = Path(__file__).with_name('queue_firepit_source_surface_debug_v1.py')
loaded = runpy.run_path(str(BASE), run_name='source_varying_probe')
g = loaded['g']
original_helper = g['helper']
popen = loaded['popen']
ROLES = ['Texcoord0', 'VertexTangentWS', 'VertexBitangentWS', 'VertexNormalWS']

def helper():
    text = original_helper().replace('SourceSurfaceDebugV1', 'SourceVaryingDebugV1')
    before = 'new[]{"Albedo","Normal","Smoothness","AmbientOcclusion","Metal","Specular","DirectDiffuseLighting","IndirectDiffuseLighting"}'
    assert text.count(before) == 1
    text = text.replace(before, 'new[]{"Texcoord0","VertexTangentWS","VertexBitangentWS","VertexNormalWS"}')
    before = 'else debug.SetDebugViewCommonMaterialProperty((UnityEngine.Rendering.HighDefinition.Attributes.MaterialSharedProperty)Enum.Parse(typeof(UnityEngine.Rendering.HighDefinition.Attributes.MaterialSharedProperty),diagnostic));'
    assert text.count(before) == 1
    text = text.replace(before, 'else debug.SetDebugViewVarying((UnityEngine.Rendering.HighDefinition.Attributes.DebugViewVarying)Enum.Parse(typeof(UnityEngine.Rendering.HighDefinition.Attributes.DebugViewVarying),diagnostic));')
    text = text.replace('debug.SetDebugViewCommonMaterialProperty(UnityEngine.Rendering.HighDefinition.Attributes.MaterialSharedProperty.None);debug.SetDebugLightingMode(DebugLightingMode.None);', 'debug.SetDebugViewVarying(UnityEngine.Rendering.HighDefinition.Attributes.DebugViewVarying.None);debug.SetDebugViewCommonMaterialProperty(UnityEngine.Rendering.HighDefinition.Attributes.MaterialSharedProperty.None);debug.SetDebugLightingMode(DebugLightingMode.None);')
    return text

def corrected_popen(args, *a, **kw):
    if isinstance(args, list) and args and str(args[0]) == str(g['UNITY']):
        args = list(args)
        i = args.index('-executeMethod') + 1
        assert args[i] == 'OpenRPGSourceShadowReferenceV8.Export'
        args[i] = 'OpenRPGSourceVaryingDebugV1.Export'
    return popen(args, *a, **kw)

# The base launcher is retained before the imported wrappers alter subprocess.
g['subprocess'].Popen = corrected_popen
g.update(ROOT=g['W']/'firepit-source-varying-debug-v1', HELPER=g['PROJECT']/'Assets/Editor/SourceVaryingDebugV1.cs', helper=helper, __file__=str(Path(__file__).resolve()))

if __name__ == '__main__':
    if sys.argv[-1] == 'prepare':
        g['prepare']()
    else:
        g['run']()
        state = g['read'](g['ROOT']/'RUN-RESULT.v1.json')
        paths = [g['ROOT']/f'DEBUG-{role}.rgba16f' for role in ROLES]
        missing = [str(p) for p in paths if not p.exists()]
        if state.get('actual_exit_code') == 0 and not missing:
            state.update(status='source_uv_and_tbn_varying_debug_captured', debug_roles=ROLES, debug_files={str(p):g['sha'](p) for p in paths})
        else:
            state.update(status='source_varying_debug_capture_refused', missing=missing)
        g['save']('RUN-RESULT.v1.json', state)
