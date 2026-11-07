"""Repeat the fixture without presentation postprocessing; retain v687 unchanged."""
from pathlib import Path

parent = Path(__file__).with_name('run_house_controlled_unity_render_v687.py')
recipe = parent.read_text('utf-8-sig').replace('v687', 'v688').replace('V687', 'V688')
recipe = recipe.replace('house-native-component-state-v684/HELPER-OWNERSHIP.v1.json',
                        'house-native-component-state-v687/HELPER-OWNERSHIP.v1.json')
recipe = recipe.replace("len(audit['helpers'])==41", "len(audit['helpers'])==42")
anchor = '# This remains a controlled fixture.'
insert = '''
old='extra.renderingPathCustomFrameSettings.SetEnabled(field,true);'
assert CODE.count(old)==1
CODE=CODE.replace(old,'extra.renderingPathCustomFrameSettings.SetEnabled(field,field==FrameSettingsField.ExposureControl);')
CODE=CODE.replace('target=bounds.center','target='+source_vector(context['bounds_center']))
CODE=CODE.replace('public bool source_object_unchanged,','public bool postprocessing_enabled=false; public bool source_object_unchanged,')
'''
assert recipe.count(anchor) == 1
recipe = recipe.replace(anchor, insert + anchor)
exec(compile(recipe, str(parent)+'#postprocess-disabled-v688', 'exec'), globals())
