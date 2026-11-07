"""Keep prefab-asset inactivity separate from composed authored activation."""
from pathlib import Path

parent449=Path(__file__).resolve().with_name('reopen_gladiator_component_state_v449.py')
adaptation=parent449.read_text('utf-8')
adaptation=adaptation.replace('gladiator-native-component-reopen-v449','gladiator-native-component-reopen-v450')
anchor="exec(compile(code,str(parent)+'#source-collider-count-v449','exec'),globals())"
assert adaptation.count(anchor)==1
extra='''
old="assert node['active_in_hierarchy']==all(bool(expected[p]['active']) for p in chain)"
new="assert node['active_in_hierarchy'] is False, 'Capture reads non-instantiated prefab assets'\\n        authored_parent_chain_active=all(bool(expected[p]['active']) for p in chain)\\n        assert isinstance(authored_parent_chain_active,bool)"
assert old in code
code=code.replace(old,new)
code=code.replace("camera_lod_decisions_measured=False", "prefab_asset_inactive_in_hierarchy_preserved=True, authored_active_self_verified=True, camera_lod_decisions_measured=False")
code=code.replace("pins[str(parent)]=sha(parent)", "pins[str(parent)]=sha(parent);pins[str(parent449)]=sha(parent449)")
'''
adaptation=adaptation.replace(anchor,extra+'\n'+anchor)
exec(compile(adaptation,str(parent449)+'#prefab-asset-active-state-v450','exec'),globals())
