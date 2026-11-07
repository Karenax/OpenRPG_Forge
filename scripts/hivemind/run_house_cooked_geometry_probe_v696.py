"""Inspect supported Unity serialized cooking data and public geometry APIs; no native pointer access."""
from pathlib import Path

parent=Path(__file__).with_name('run_house_native_mesh_metrics_v684.py')
recipe=parent.read_text('utf-8-sig').replace('v684','v696').replace('V684','V696')
recipe=recipe.replace('house-native-component-state-v681/HELPER-OWNERSHIP.v1.json',
                      'house-native-component-state-v694/HELPER-OWNERSHIP.v1.json')
recipe=recipe.replace("len(audit['helpers'])==40","len(audit['helpers'])==45")
anchor='OWNED_MESH_CODE = CODE'
injection=r'''
CODE=CODE.replace('public bool convex;','public MeshProperty[] serialized_mesh_properties;public string[] convex_geometry_public_api;public string native_geometry_type;public bool convex;')
insert=r"""
[Serializable]public class MeshProperty{public string path;public string type;public int array_size;public string byte_dump;public bool byte_dump_supported;}
static MeshProperty[] InspectMesh(Mesh mesh,string output){
 string before=EditorJsonUtility.ToJson(mesh,true);string guid;long file;if(!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(mesh,out guid,out file))throw new Exception("Mesh identity absent");
 var rows=new List<MeshProperty>();var serialized=new SerializedObject(mesh);var property=serialized.GetIterator();bool enter=true;
 while(property.Next(enter)){
  enter=!property.isArray;
  var row=new MeshProperty{path=property.propertyPath,type=property.propertyType.ToString(),array_size=property.isArray?property.arraySize:-1};
  if(property.isArray&&(property.propertyPath.IndexOf("Baked",StringComparison.OrdinalIgnoreCase)>=0||property.propertyPath.IndexOf("Collision",StringComparison.OrdinalIgnoreCase)>=0)){
   byte[] data=new byte[property.arraySize];bool supported=true;
   for(int i=0;i<data.Length;i++){var item=property.GetArrayElementAtIndex(i);if(item.propertyType!=SerializedPropertyType.Integer||item.intValue<0||item.intValue>255){supported=false;break;}data[i]=(byte)item.intValue;}
   row.byte_dump_supported=supported;
   if(supported&&data.Length>0){string name=guid+"-"+file.ToString()+"-"+property.propertyPath.Replace('.','_').Replace('/','_')+".bin";string path=Path.Combine(output,name);if(File.Exists(path)){if(!File.ReadAllBytes(path).SequenceEqual(data))throw new Exception("Repeated cooked bytes differ");}else File.WriteAllBytes(path,data);row.byte_dump=path;}
  }
  rows.Add(row);
 }
 if(before!=EditorJsonUtility.ToJson(mesh,true))throw new Exception("Mesh changed during serialized read");return rows.ToArray();
}
"""
needle='static float[] Vec(Vector3 v)'
assert CODE.count(needle)==1
CODE=CODE.replace(needle,insert+needle)
needle='new Box{convex=source.convex,'
assert CODE.count(needle)==1
CODE=CODE.replace(needle,'new Box{serialized_mesh_properties=InspectMesh(box.sharedMesh,output),convex_geometry_public_api=typeof(UnityEngine.LowLevelPhysics.ConvexMeshGeometry).GetMembers(System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Static).Select(m=>m.ToString()).ToArray(),native_geometry_type=box.GeometryHolder.Type.ToString(),convex=source.convex,')
'''
assert recipe.count(anchor)==1
recipe=recipe.replace(anchor,injection+'\n'+anchor)
exec(compile(recipe,str(parent)+'#read-only-cooked-geometry-probe-v696','exec'),globals())
