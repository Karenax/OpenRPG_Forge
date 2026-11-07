"""Preserve unassigned MeshCollider components without inventing collider meshes."""
from pathlib import Path
recipe=Path(__file__).with_name('run_house_prefab_geometry_v648.py')
code=recipe.read_text('utf-8-sig').replace('house-native-prefab-geometry-v648','house-native-prefab-geometry-v651').replace('HousePrefabGeometryV648','HousePrefabGeometryV651')
code=code.replace("ap=W/'house-native-sampling-policy-v644/HELPER-OWNERSHIP.v1.json'","ap=W/'house-native-mesh-catalog-v650/HELPER-OWNERSHIP.v1.json'").replace("len(audit['helpers'])==32","len(audit['helpers'])==35")
needle="    text=text.rstrip();assert text.endswith('}');text=text[:-1]+addition+'\\n}\\n';base="
replacement="""    text=text.rstrip();assert text.endswith('}');text=text[:-1]+addition+'\\n}\\n'
    old='var collider=t.GetComponent<MeshCollider>();if(collider!=null)add(collider.sharedMesh,"mesh_collider");';assert text.count(old)==1;text=text.replace(old,'var collider=t.GetComponent<MeshCollider>();if(collider!=null&&collider.sharedMesh!=null)add(collider.sharedMesh,"mesh_collider");')
    text=text.replace('public string unity_version,prefab_guid,card_id;','public string unity_version,prefab_guid,card_id;public EmptyMeshCollider[] unassigned_mesh_colliders;')
    text=text.replace('component_snapshots=before,nodes=','component_snapshots=before,unassigned_mesh_colliders=EmptyColliders(p.transform,"root").ToArray(),nodes=')
    extra='''
 [Serializable]public class EmptyMeshCollider{public string path,local_file_id,serialized;public bool enabled,convex,is_trigger;}
 static IEnumerable<EmptyMeshCollider> EmptyColliders(Transform t,string path){var c=t.GetComponent<MeshCollider>();if(c!=null&&c.sharedMesh==null)yield return new EmptyMeshCollider{path=path,local_file_id=Id(c).file_id,serialized=EditorJsonUtility.ToJson(c,true),enabled=c.enabled,convex=c.convex,is_trigger=c.isTrigger};for(int i=0;i<t.childCount;i++)foreach(var e in EmptyColliders(t.GetChild(i),path+"/"+i))yield return e;}
'''
    text=text.rstrip();text=text[:-1]+extra+'\\n}\\n';base="""
assert code.count(needle)==1;code=code.replace(needle,replacement)
code=code.replace('for p in [Path(__file__),package,ap,','for p in [Path(__file__),recipe,package,ap,')
exec(compile(code,str(recipe)+'#explicit-empty-collider-preservation','exec'),globals())
