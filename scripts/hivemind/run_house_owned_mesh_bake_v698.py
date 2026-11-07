"""Repair the probe's output environment binding; retain v697 refusal unchanged."""
from pathlib import Path
parent=Path(__file__).with_name('run_house_owned_mesh_bake_v697.py')
recipe=parent.read_text('utf-8-sig').replace('v697','v698').replace('V697','V698')
recipe=recipe.replace('house-native-component-state-v696/HELPER-OWNERSHIP.v1.json',
                      'house-native-component-state-v697/HELPER-OWNERSHIP.v1.json').replace("len(audit['helpers'])==46","len(audit['helpers'])==47")
anchor="code=code.replace('prefabs_instantiated=False','prefabs_instantiated=True')"
assert recipe.count(anchor)==1
recipe=recipe.replace(anchor,"code=code.replace('OPENRPG_COMPONENT_STATE_OUTPUT','OPENRPG_BOX_METRIC_OUTPUT')\n"+anchor)
exec(compile(recipe,str(parent)+'#output-binding-repair-v698','exec'),globals())
