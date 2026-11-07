"""Measure the same original Unity material with direct light disabled, without fitting."""
from pathlib import Path

parent = Path(__file__).with_name('run_house_controlled_unity_render_v688.py')
recipe = parent.read_text('utf-8-sig').replace('v688','v692').replace('V688','V692')
recipe = recipe.replace('house-native-component-state-v687/HELPER-OWNERSHIP.v1.json',
                        'house-native-component-state-v688/HELPER-OWNERSHIP.v1.json')
recipe = recipe.replace("len(audit['helpers'])==42", "len(audit['helpers'])==43")
anchor = "CODE=CODE.replace('public bool source_object_unchanged,'"
offset = recipe.index(anchor)
end = recipe.index('\n',offset)
addition = '''
assert CODE.count('light.shadows=LightShadows.None;')==1
CODE=CODE.replace('light.shadows=LightShadows.None;','light.shadows=LightShadows.None;light.enabled=false;')
CODE=CODE.replace('public bool source_object_unchanged,','public bool actual_directional_light_enabled;public string visual_environment_state;public string hdri_sky_state;public string indirect_lighting_state;public string exposure_state;public bool source_object_unchanged,')
CODE=CODE.replace('actual_directional_light_intensity=light.intensity,','actual_directional_light_intensity=light.intensity,actual_directional_light_enabled=light.enabled,visual_environment_state=EditorJsonUtility.ToJson(VolumeManager.instance.stack.GetComponent<VisualEnvironment>(),true),hdri_sky_state=EditorJsonUtility.ToJson(VolumeManager.instance.stack.GetComponent<HDRISky>(),true),indirect_lighting_state=EditorJsonUtility.ToJson(VolumeManager.instance.stack.GetComponent<IndirectLightingController>(),true),exposure_state=EditorJsonUtility.ToJson(VolumeManager.instance.stack.GetComponent<Exposure>(),true),')
'''
recipe = recipe[:end] + addition + recipe[end:]
exec(compile(recipe,str(parent)+'#original-light-off-probe-v692','exec'),globals())
