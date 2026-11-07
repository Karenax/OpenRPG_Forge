"""Capture actual GameObject identities and native local/world matrices with component state."""
from pathlib import Path
PARENT = Path(__file__).with_name('run_house_component_state_v672.py')
recipe = PARENT.read_text('utf-8-sig').replace('v672','v673').replace('V672','V673')
recipe = recipe.replace('house-wire-native-storage-v668/HELPER-OWNERSHIP.v1.json','house-native-component-state-v672/HELPER-OWNERSHIP.v1.json').replace("len(audit['helpers'])==37","len(audit['helpers'])==38")
anchor = "code = BASE.read_text('utf-8-sig')"
addition = '''
old_capture='code=ast.literal_eval(assignment.value)'
extra_capture="""code=ast.literal_eval(assignment.value)
node_decl='public string node_path;'
assert code.count(node_decl)==1
code=code.replace(node_decl,node_decl+'public Identity gameobject;public float[] local_matrix,world_matrix;')
walk='node_path=path,active_self='
assert code.count(walk)==1
code=code.replace(walk,'node_path=path,gameobject=Id(t.gameObject),local_matrix=Enumerable.Range(0,16).Select(i=>Matrix4x4.TRS(t.localPosition,t.localRotation,t.localScale)[i]).ToArray(),world_matrix=Enumerable.Range(0,16).Select(i=>t.localToWorldMatrix[i]).ToArray(),active_self=')"""
assert code.count(old_capture)==1
code=code.replace(old_capture,extra_capture)
'''
assert recipe.count(anchor)==1
recipe=recipe.replace(anchor,anchor+addition)
exec(compile(recipe,str(PARENT)+'#native-gameobject-and-frame-capture','exec'),globals())
