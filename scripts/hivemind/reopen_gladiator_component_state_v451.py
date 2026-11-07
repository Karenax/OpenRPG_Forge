"""Admit JsonUtility's empty optional LOD object only with explicit absence proof."""
from pathlib import Path

parent450=Path(__file__).resolve().with_name('reopen_gladiator_component_state_v450.py')
code=parent450.read_text('utf-8')
code=code.replace('gladiator-native-component-reopen-v450','gladiator-native-component-reopen-v451')
anchor="adaptation=adaptation.replace(anchor,extra+'\\n'+anchor)"
assert code.count(anchor)==1
addition='''
extra += "\\ncode=code.replace(\\\"group=node.get('lod_group')\\\", \\\"group=node.get('lod_group')\\\\n        if group and not group['identity']['guid']:\\\\n            assert 'UnityEngine.LODGroup' not in node['component_types'] and not group['levels'] and not group['enabled'], 'Empty optional LOD object must prove absence'\\\\n            group=None\\\")\\n"
extra += "code=code.replace(\\\"pins[str(parent449)]=sha(parent449)\\\", \\\"pins[str(parent449)]=sha(parent449);pins[str(parent450)]=sha(parent450)\\\")\\n"
'''
code=code.replace(anchor,addition+'\n'+anchor)
exec(compile(code,str(parent450)+'#optional-lod-null-v451','exec'),globals())
