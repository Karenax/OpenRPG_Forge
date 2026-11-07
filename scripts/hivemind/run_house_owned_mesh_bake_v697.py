"""Bake owned copies with the source policy; preserve every original mesh and component."""
from pathlib import Path

S=Path(__file__).resolve().parent
source_helper=S/'candidates/HouseComponentStateV696.cs'
CODE=source_helper.read_text('utf-8-sig').replace('V696','V697')
CODE=CODE.replace('public MeshProperty[] serialized_mesh_properties;',
                  'public MeshProperty[] baked_copy_properties;public MeshProperty[] serialized_mesh_properties;')
helper=r'''
static MeshProperty[] BakeOwnedCopy(Mesh source,bool convex,MeshColliderCookingOptions options,string output){
 string before=EditorJsonUtility.ToJson(source,true);string guid;long file;
 if(!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(source,out guid,out file))throw new Exception("Source mesh identity absent");
 string folder="Assets/OpenRPGOwnedPhysicsBakeV697";
 if(!AssetDatabase.IsValidFolder(folder))AssetDatabase.CreateFolder("Assets","OpenRPGOwnedPhysicsBakeV697");
 string path=folder+"/"+guid+"-"+file.ToString()+"-"+(convex?"convex":"triangle")+".asset";
 Mesh owned=AssetDatabase.LoadAssetAtPath<Mesh>(path);
 if(owned==null){
  owned=UnityEngine.Object.Instantiate(source);owned.hideFlags=HideFlags.None;
  AssetDatabase.CreateAsset(owned,path);
  if(!source.vertices.SequenceEqual(owned.vertices)||!source.triangles.SequenceEqual(owned.triangles))throw new Exception("Owned copy geometry differs");
  Physics.BakeMesh(owned.GetEntityId(),convex,options);
  EditorUtility.SetDirty(owned);AssetDatabase.SaveAssetIfDirty(owned);
 }
 var result=InspectMesh(owned,output);
 if(before!=EditorJsonUtility.ToJson(source,true))throw new Exception("Original mesh changed during copy bake");
 return result;
}
'''
needle='static MeshProperty[] InspectMesh'
assert CODE.count(needle)==1
CODE=CODE.replace(needle,helper+needle)
needle='serialized_mesh_properties=InspectMesh(box.sharedMesh,output),'
assert CODE.count(needle)==1
CODE=CODE.replace(needle,'baked_copy_properties=BakeOwnedCopy(box.sharedMesh,source.convex,source.cookingOptions,output),'+needle)
parent=S/'run_house_component_state_v672.py'
recipe=parent.read_text('utf-8-sig').replace('v672','v697').replace('V672','V697')
recipe=recipe.replace('house-wire-native-storage-v668/HELPER-OWNERSHIP.v1.json',
                      'house-native-component-state-v696/HELPER-OWNERSHIP.v1.json').replace("len(audit['helpers'])==37","len(audit['helpers'])==46")
anchor="exec(compile(code,str(BASE)+'#house-owned-component-capture','exec'),globals())"
addition='''
needle="helper=PROJECT/'Assets/Editor/HouseComponentStateV697.cs'"
assert code.count(needle)==1
code=code.replace(needle,"code=OWNED_BAKE_CODE\\n"+needle)
code=code.replace('OpenRPGHouseComponentStateV697.Capture.Export','OpenRPGHouseNativeMeshMetricV697.Capture.Export')
old="assert len(native['prefabs'])==24 and sum(len(n['renderers']) for p in native['prefabs'] for n in p['nodes'])==24\\n    assert sum(len(n['colliders']) for p in native['prefabs'] for n in p['nodes'])==24"
assert code.count(old)==1
code=code.replace(old,"assert len(native['boxes'])==19 and native['original_components_unchanged'] and not native['purchased_scripts_executed'] and not native['physical_simulation_stepped']")
code=code.replace('prefabs_instantiated=False','prefabs_instantiated=True')
code=code.replace('twentyfour_house_prefab_component_states_captured_review_pending','owned_mesh_cooking_probe_retained')
code=code.replace('pins={str(p):sha(p) for p in [Path(__file__),parent,bridge_path,closure_path]}','pins={str(p):sha(p) for p in [Path(__file__),parent,bridge_path,closure_path,source_helper]}')
'''
assert recipe.count(anchor)==1
recipe=recipe.replace(anchor,addition+anchor)
OWNED_BAKE_CODE=CODE
exec(compile(recipe,str(parent)+'#owned-copy-native-bake-v697','exec'),globals())
