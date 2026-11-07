"""Capture actual HDRP lighting/occlusion debug modes with unchanged reference controls."""
from pathlib import Path
import subprocess
import sys

S = Path(__file__).parent
target = S / 'queue_townsmith_lighting_controls_v12.py'
assert not target.exists()
text = (S / 'queue_townsmith_occlusion_v11.py').read_text(encoding='utf-8')
text = text.replace('townsmith-door-source-occlusion-v11', 'townsmith-door-source-lighting-controls-v12')
text = text.replace('SourceTownsmithOcclusionV11', 'SourceTownsmithLightingControlsV12')
text = text.replace('SourceTownsmithFgdReadbackV11', 'SourceTownsmithFgdReadbackV12')
text = text.replace('request.cases.Length!=3', 'request.cases.Length!=9')
text = text.replace('expected_cases=3', 'expected_cases=9').replace("len(native['rows'])==3", "len(native['rows'])==9").replace('cases_captured=3', 'cases_captured=9')
anchor = "    cases=[dict(base,id='door-neutral'),dict(base,id='door-linear'),dict(base,id='door-fixedbackground-linear')]"
assert text.count(anchor) == 1
text = text.replace(anchor, anchor + "\n    cases += [dict(base,id='door-debug-'+mode+'-linear') for mode in ['DirectDiffuseLighting','DirectSpecularLighting','IndirectDiffuseLighting','ReflectionLighting','IndirectDiffuseOcclusion','IndirectSpecularOcclusion']]")
anchor = 'public string actual_camera_history;'
assert text.count(anchor) == 1
text = text.replace(anchor, anchor + ' public string actual_debug_lighting_mode;')
anchor = 'id=item.id,actual_ssao_intensity='
assert text.count(anchor) == 1
text = text.replace(anchor, 'id=item.id,actual_debug_lighting_mode=sourceDebugSettings.GetDebugLightingMode().ToString(),actual_ssao_intensity=')
anchor = "    CS.write_text(text,encoding='utf-8');HELPER.write_bytes(CS.read_bytes())"
assert text.count(anchor) == 1
warm = '    for(int warm=0;warm<3;warm++)RenderPipeline.SubmitRenderRequest(camera,render);'
replacement = '''    for(int warm=0;warm<3;warm++){
      RenderPipeline.SubmitRenderRequest(camera,render);
      if(warm==0){
        var sourceDebugField=typeof(HDRenderPipeline).GetField("m_DebugDisplaySettings",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);
        if(sourceDebugField==null)throw new Exception("Pinned HDRP debug settings field absent");
        sourceDebugSettings=sourceDebugField.GetValue(RenderPipelineManager.currentPipeline) as DebugDisplaySettings;
        if(sourceDebugSettings==null)throw new Exception("Actual HDRP debug settings absent");
        priorDebugLighting=sourceDebugSettings.GetDebugLightingMode();
        if(priorDebugLighting!=DebugLightingMode.None)throw new Exception("Preserve unexpected preexisting debug lighting state");
        var requested=DebugLightingMode.None;
        if(item.id.StartsWith("door-debug-"))requested=(DebugLightingMode)Enum.Parse(typeof(DebugLightingMode),item.id.Substring(11,item.id.Length-18));
        sourceDebugSettings.SetDebugLightingMode(requested);
        if(sourceDebugSettings.GetDebugLightingMode()!=requested)throw new Exception("Native requested HDRP debug mode was not applied");
      }
    }'''
prepare_code = "    assert text.count(" + repr(warm) + ")==1\n    text=text.replace(" + repr(warm) + ',' + repr(replacement) + ')\n'
prepare_code += "    text=text.replace('RenderTexture target=null;', 'RenderTexture target=null;DebugDisplaySettings sourceDebugSettings=null;DebugLightingMode priorDebugLighting=DebugLightingMode.None;')\n"
prepare_code += "    text=text.replace('}finally{if(target!=null)', '}finally{if(sourceDebugSettings!=null)sourceDebugSettings.SetDebugLightingMode(priorDebugLighting);if(target!=null)')\n"
text = text.replace(anchor, prepare_code + anchor)
compile(text, str(target), 'exec')
target.write_text(text, encoding='utf-8')
subprocess.run([sys.executable, '-B', str(target)], check=True)
