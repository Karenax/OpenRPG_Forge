"""Isolate original source material emission without native debug-mode ambiguity."""
from pathlib import Path
import subprocess
import sys

S = Path(__file__).parent
target = S / 'queue_townsmith_dark_control_v14.py'
assert not target.exists()
text = (S / 'queue_townsmith_emission_control_v13.py').read_text(encoding='utf-8')
text = text.replace('townsmith-door-source-emission-control-v13', 'townsmith-door-source-dark-control-v14')
text = text.replace('SourceTownsmithEmissionControlV13', 'SourceTownsmithDarkControlV14')
text = text.replace('SourceTownsmithFgdReadbackV13', 'SourceTownsmithFgdReadbackV14')
text = text.replace('request.cases.Length!=10', 'request.cases.Length!=11')
text = text.replace('expected_cases=10', 'expected_cases=11').replace("len(native['rows'])==10", "len(native['rows'])==11").replace('cases_captured=10', 'cases_captured=11')
anchor = "    save('REQUEST.v1.json',dict(output=str(ROOT/'NATIVE-RESULT.v1.json'),cases=cases))"
assert text.count(anchor) == 1
text = text.replace(anchor, "    cases.append(dict(base,id='door-emission-dark-linear'))\n" + anchor)
anchor = 'public string actual_camera_history;'
assert text.count(anchor) == 1
text = text.replace(anchor, anchor + ' public bool dark_source_control,actual_fog_enabled;')
anchor = 'id=item.id,actual_debug_lighting_mode='
assert text.count(anchor) == 1
text = text.replace(anchor, 'id=item.id,dark_source_control=item.id=="door-emission-dark-linear",actual_fog_enabled=sourceHdCamera.volumeStack.GetComponent<Fog>().enabled.value,actual_debug_lighting_mode=')
anchor = "    CS.write_text(text,encoding='utf-8');HELPER.write_bytes(CS.read_bytes())"
assert text.count(anchor) == 1
source_anchor = '    target=new RenderTexture(512,512,24,RenderTextureFormat.ARGBHalf,RenderTextureReadWrite.Linear);'
insertion = '''    if(item.id=="door-emission-dark-linear"){
      light.intensity=0;
      var darkCamera=camera.GetComponent<HDAdditionalCameraData>();darkCamera.clearColorMode=HDAdditionalCameraData.ClearColorMode.Color;darkCamera.backgroundColorHDR=new Color(.025f,.025f,.025f,1);
      var darkIndirect=profile.Add<IndirectLightingController>();darkIndirect.indirectDiffuseLightingMultiplier.Override(0);darkIndirect.reflectionLightingMultiplier.Override(0);darkIndirect.reflectionProbeIntensityMultiplier.Override(0);
      var darkOcclusion=profile.Add<ScreenSpaceAmbientOcclusion>();darkOcclusion.intensity.Override(0);
      var darkFog=profile.Add<Fog>();darkFog.enabled.Override(false);darkFog.enableVolumetricFog.Override(false);
    }
'''
prepare_code = '    assert text.count(' + repr(source_anchor) + ')==1\n    text=text.replace(' + repr(source_anchor) + ',' + repr(insertion + source_anchor) + ')\n'
text = text.replace(anchor, prepare_code + anchor)
compile(text, str(target), 'exec')
target.write_text(text, encoding='utf-8')
subprocess.run([sys.executable, '-B', str(target)], check=True)
