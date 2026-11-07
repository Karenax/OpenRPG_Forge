"""Measure a direct-only Unity fixture matching the Forge fixture's declared ambient policy."""
from pathlib import Path

parent=Path(__file__).with_name('run_house_unity_ambient_probe_v692.py')
recipe=parent.read_text('utf-8-sig').replace('v692','v694').replace('V692','V694')
recipe=recipe.replace('house-native-component-state-v688/HELPER-OWNERSHIP.v1.json',
                      'house-native-component-state-v692/HELPER-OWNERSHIP.v1.json')
recipe=recipe.replace("len(audit['helpers'])==43","len(audit['helpers'])==44")
anchor="CODE=CODE.replace('actual_directional_light_intensity=light.intensity,'"
offset=recipe.index(anchor)
addition='''
CODE=CODE.replace('light.enabled=false','light.enabled=true')
CODE=CODE.replace('VolumeManager.instance.stack','HDCamera.GetOrCreate(camera).volumeStack')
needle='var tone=presentationProfile.Add<Tonemapping>(true);'
assert CODE.count(needle)==1
CODE=CODE.replace(needle,'var indirect=presentationProfile.Add<IndirectLightingController>(true);indirect.indirectDiffuseLightingMultiplier.Override(0);indirect.reflectionLightingMultiplier.Override(0);indirect.reflectionProbeIntensityMultiplier.Override(0);'+needle)
'''
# Apply the camera-stack replacement after the receipt fields are inserted.
end=recipe.index('\n',offset)
recipe=recipe[:offset]+addition+recipe[offset:end]+"\nCODE=CODE.replace('VolumeManager.instance.stack','HDCamera.GetOrCreate(camera).volumeStack')"+recipe[end:]
exec(compile(recipe,str(parent)+'#direct-only-fixture-v694','exec'),globals())
