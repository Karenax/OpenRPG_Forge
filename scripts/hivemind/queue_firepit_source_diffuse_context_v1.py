"""Capture actual lit mode and SSAO state for the original camera history."""
from pathlib import Path
import runpy
import sys

BASE=Path(__file__).with_name('queue_firepit_shadow_reference_v11.py')
loaded=runpy.run_path(str(BASE),run_name='source_diffuse_context')
g=loaded['g'];original_helper=g['helper'];popen=loaded['popen']


def helper():
    text=original_helper().replace('SourceShadowReferenceV11','SourceDiffuseContextV1')
    declaration='public float[] ambient_probe,hdrp_ambient_probe;'
    assert text.count(declaration)==1
    text=text.replace(declaration,declaration+'public bool ssao_enabled;public string effective_lit_shader_mode,ambient_occlusion_volume;')
    initializer='source_history_channel="CustomUserHistory0"};'
    assert text.count(initializer)==1
    text=text.replace(initializer,
        'source_history_channel="CustomUserHistory0",ssao_enabled=hc.frameSettings.IsEnabled(FrameSettingsField.SSAO),effective_lit_shader_mode=hc.frameSettings.litShaderMode.ToString(),ambient_occlusion_volume=EditorJsonUtility.ToJson(stack.GetComponent<AmbientOcclusion>(),true)};')
    return text


def corrected_popen(args,*a,**kw):
    if isinstance(args,list) and args and str(args[0])==str(g['UNITY']):
        args=list(args);i=args.index('-executeMethod')+1
        assert args[i]=='OpenRPGSourceShadowReferenceV8.Export'
        args[i]='OpenRPGSourceDiffuseContextV1.Export'
    return popen(args,*a,**kw)


g['subprocess'].Popen=corrected_popen
g.update(ROOT=g['W']/'firepit-source-diffuse-context-v1',
    HELPER=g['PROJECT']/'Assets/Editor/SourceDiffuseContextV1.cs',helper=helper,
    __file__=str(Path(__file__).resolve()))
if __name__=='__main__':
    if sys.argv[-1]=='prepare':
        g['prepare']()
    else:
        g['run']();state=g['read'](g['ROOT']/'RUN-RESULT.v1.json')
        files=[g['ROOT']/f'SOURCE-STATE-{view}.json' for view in range(2)]
        if state.get('actual_exit_code')==0 and all(p.exists() for p in files):
            rows=[g['read'](p) for p in files]
            assert all(row['source_history_channel']=='CustomUserHistory0' for row in rows)
            state.update(status='actual_camera_lit_mode_and_ssao_context_captured',
                context_files={str(p):g['sha'](p) for p in files},
                effective_lit_modes=[row['effective_lit_shader_mode'] for row in rows],
                ssao_enabled=[row['ssao_enabled'] for row in rows])
        else:
            state.update(status='source_diffuse_context_capture_refused')
        g['save']('RUN-RESULT.v1.json',state)
