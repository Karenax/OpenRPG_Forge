"""Inventory SDK geometry entrypoints; inspect metadata only, never invoke undocumented pointer operations."""
from pathlib import Path
parent=Path(__file__).with_name('run_house_native_mesh_metrics_v684.py')
recipe=parent.read_text('utf-8-sig').replace('v684','v700').replace('V684','V700')
recipe=recipe.replace('house-native-component-state-v681/HELPER-OWNERSHIP.v1.json',
                      'house-native-component-state-v698/HELPER-OWNERSHIP.v1.json').replace("len(audit['helpers'])==40","len(audit['helpers'])==48")
anchor='OWNED_MESH_CODE = CODE'
injection=r'''
CODE=CODE.replace('public string unity_version;','public string[] editor_geometry_api;public string[] mesh_geometry_api;public string[] collider_geometry_api;public string unity_version;')
needle='var result=new Result{unity_version='
assert CODE.count(needle)==1
flags='System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.DeclaredOnly'
editor='typeof(UnityEditor.PhysicsVisualizationSettings).Assembly.GetTypes().Where(t=>t.FullName.Contains("Physics")||t.Name.Contains("Collider")).SelectMany(t=>t.GetMethods('+flags+').Select(m=>t.FullName+" :: "+m.ToString())).Where(s=>s.IndexOf("Mesh",StringComparison.OrdinalIgnoreCase)>=0||s.IndexOf("Geometry",StringComparison.OrdinalIgnoreCase)>=0||s.IndexOf("Collider",StringComparison.OrdinalIgnoreCase)>=0).OrderBy(s=>s).ToArray()'
mesh='typeof(Mesh).GetMethods('+flags+').Select(m=>m.ToString()).Where(s=>s.IndexOf("Collision",StringComparison.OrdinalIgnoreCase)>=0||s.IndexOf("Convex",StringComparison.OrdinalIgnoreCase)>=0||s.IndexOf("Bake",StringComparison.OrdinalIgnoreCase)>=0).ToArray()'
collider='typeof(MeshCollider).GetMethods('+flags+').Concat(typeof(Collider).GetMethods('+flags+')).Select(m=>m.ToString()).OrderBy(s=>s).ToArray()'
CODE=CODE.replace(needle,'var result=new Result{editor_geometry_api='+editor+',mesh_geometry_api='+mesh+',collider_geometry_api='+collider+',unity_version=')
'''
assert recipe.count(anchor)==1
recipe=recipe.replace(anchor,injection+'\n'+anchor)
exec(compile(recipe,str(parent)+'#sdk-geometry-entrypoint-inventory-v700','exec'),globals())
