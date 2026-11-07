"""Measure source occlusion and indirect-light controls rather than assuming visibility."""
from pathlib import Path
import subprocess
import sys

S = Path(__file__).parent
target = S / 'queue_townsmith_occlusion_v11.py'
assert not target.exists()
text = (S / 'queue_townsmith_camera_brdf_v10.py').read_text(encoding='utf-8')
text = text.replace('townsmith-door-source-camera-brdf-v10', 'townsmith-door-source-occlusion-v11')
text = text.replace('SourceTownsmithCameraBrdfV10', 'SourceTownsmithOcclusionV11')
text = text.replace('SourceTownsmithFgdReadbackV10', 'SourceTownsmithFgdReadbackV11')
anchor = 'public string actual_camera_history;'
assert text.count(anchor) == 1
text = text.replace(anchor, anchor + ' public float actual_ssao_intensity,actual_ssao_radius,actual_ssao_specular_blend,actual_indirect_diffuse_multiplier,actual_reflection_multiplier; public Vector4 actual_global_ao_parameters; public int actual_hdri_width,actual_hdri_mip_count; public float actual_hdri_exposure,actual_hdri_multiplier; public string actual_hdri_intensity_mode;')
anchor = '    var activeGradient=sourceHdCamera.volumeStack.GetComponent<GradientSky>();'
assert text.count(anchor) == 1
text = text.replace(anchor, anchor + '''
    var activeOcclusion=sourceHdCamera.volumeStack.GetComponent<ScreenSpaceAmbientOcclusion>();
    var activeIndirect=sourceHdCamera.volumeStack.GetComponent<IndirectLightingController>();
    if(activeOcclusion==null||activeIndirect==null)throw new Exception("Actual HDRP indirect-light controls absent");
    File.WriteAllText(Path.Combine(root,item.id+".active-ssao.json"),EditorJsonUtility.ToJson(activeOcclusion,true));
    File.WriteAllText(Path.Combine(root,item.id+".active-indirect.json"),EditorJsonUtility.ToJson(activeIndirect,true));
''')
anchor = 'id=item.id,actual_lit_shader_mode='
assert text.count(anchor) == 1
text = text.replace(anchor, 'id=item.id,actual_ssao_intensity=activeOcclusion.intensity.value,actual_ssao_radius=activeOcclusion.radius.value,actual_ssao_specular_blend=activeOcclusion.specularOcclusion.value,actual_indirect_diffuse_multiplier=activeIndirect.indirectDiffuseLightingMultiplier.value,actual_reflection_multiplier=activeIndirect.reflectionLightingMultiplier.value,actual_global_ao_parameters=Shader.GetGlobalVector("_AmbientOcclusionParam"),actual_hdri_width=activeHdri.hdriSky.value==null?0:activeHdri.hdriSky.value.width,actual_hdri_mip_count=activeHdri.hdriSky.value==null?0:activeHdri.hdriSky.value.mipmapCount,actual_hdri_exposure=activeHdri.exposure.value,actual_hdri_multiplier=activeHdri.multiplier.value,actual_hdri_intensity_mode=activeHdri.skyIntensityMode.value.ToString(),actual_lit_shader_mode=')
compile(text, str(target), 'exec')
target.write_text(text, encoding='utf-8')
subprocess.run([sys.executable, '-B', str(target)], check=True)
