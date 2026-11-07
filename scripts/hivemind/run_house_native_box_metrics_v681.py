"""Measure Unity box closest-point/ray controls and implicit physics properties on disposable clones."""
from pathlib import Path
import ast

S=Path(__file__).resolve().parent
METRIC=S/'run_gladiator_native_box_metric_v471.py'
tree=ast.parse(METRIC.read_text('utf-8-sig'))
assignment=next(n for n in tree.body if isinstance(n,ast.Assign) and any(isinstance(t,ast.Name) and t.id=='CODE' for t in n.targets))
CODE=ast.literal_eval(assignment.value).replace('OpenRPGGladiatorNativeBoxMetricV471','OpenRPGHouseNativeBoxMetricV681')
PARENT=S/'run_house_component_state_v672.py'
recipe=PARENT.read_text('utf-8-sig').replace('v672','v681').replace('V672','V681')
recipe=recipe.replace('house-wire-native-storage-v668/HELPER-OWNERSHIP.v1.json','house-native-component-state-v673/HELPER-OWNERSHIP.v1.json').replace("len(audit['helpers'])==37","len(audit['helpers'])==39")
anchor="exec(compile(code,str(BASE)+'#house-owned-component-capture','exec'),globals())"
addition='''
needle="helper=PROJECT/'Assets/Editor/HouseComponentStateV681.cs'"
assert code.count(needle)==1
code=code.replace(needle,"code=CODE.replace('SOURCE_PATHS',','.join(json.dumps(p) for p in paths))\\n"+needle)
code=code.replace('OPENRPG_COMPONENT_STATE_OUTPUT','OPENRPG_BOX_METRIC_OUTPUT').replace('OpenRPGHouseComponentStateV681.Capture.Export','OpenRPGHouseNativeBoxMetricV681.Capture.Export')
old="assert len(native['prefabs'])==24 and sum(len(n['renderers']) for p in native['prefabs'] for n in p['nodes'])==24\\n    assert sum(len(n['colliders']) for p in native['prefabs'] for n in p['nodes'])==24"
assert code.count(old)==1
code=code.replace(old,"assert len(native['boxes'])==3 and sum(len(b['points']) for b in native['boxes'])==63 and sum(len(b['rays']) for b in native['boxes'])==18\\n    assert native['original_components_unchanged'] and not native['purchased_scripts_executed'] and not native['physical_simulation_stepped']")
code=code.replace('prefabs_instantiated=False','prefabs_instantiated=True').replace('twentyfour_house_prefab_component_states_captured_review_pending','three_house_native_box_metrics_captured_review_pending')
'''
assert recipe.count(anchor)==1
recipe=recipe.replace(anchor,addition+anchor)
exec(compile(recipe,str(PARENT)+'#house-native-box-metric','exec'),globals())
