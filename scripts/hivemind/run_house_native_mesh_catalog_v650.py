"""Inventory every native FBX mesh even when an earlier candidate name fails."""
from pathlib import Path
_parent=Path(__file__).with_name('run_house_mesh_correspondence_v649.py')
exec(compile(_parent.read_text('utf-8-sig').rsplit('\nexec(compile(code,',1)[0],str(_parent)+'#prepare-recipe','exec'),globals())
code=code.replace('house-native-mesh-correspondence-v649','house-native-mesh-catalog-v650').replace('HouseMeshCorrespondenceV649','HouseNativeMeshCatalogV650')
code=code.replace("ap=W/'house-native-prefab-geometry-v648/HELPER-OWNERSHIP.v1.json'","ap=W/'house-native-mesh-correspondence-v649/HELPER-OWNERSHIP.v1.json'").replace("len(audit['helpers'])==33","len(audit['helpers'])==34")
old='var candidates=AssetDatabase.LoadAllAssetsAtPath(e.path).OfType<Mesh>().Where(m=>m.name==e.name).ToArray();if(candidates.Length!=1)throw new Exception("Exact unique FBX name match required: "+e.path+"/"+e.name+"; found "+candidates.Length);var m=candidates[0];var id=Id(m);if(id.guid!=e.guid)throw new Exception("Source FBX GUID differs");meshes[id.guid+":"+id.file_id]=m;resolved.Add(new Resolved{source=e,native=id,name_and_guid_exact=true,declared_file_id_matches_native=id.file_id==e.declared_file_id});'
new='var candidates=AssetDatabase.LoadAllAssetsAtPath(e.path).OfType<Mesh>().ToArray();if(candidates.Length==0)throw new Exception("No native mesh in source FBX: "+e.path);foreach(var m in candidates){var id=Id(m);if(id.guid!=e.guid)throw new Exception("Source FBX GUID differs");meshes[id.guid+":"+id.file_id]=m;resolved.Add(new Resolved{source=e,native=id,name_and_guid_exact=id.name==e.name,declared_file_id_matches_native=id.file_id==e.declared_file_id});}'
assert code.count(old)==1;code=code.replace(old,new)
code=code.replace("all(m['name_and_guid_exact'] for m in c['resolved_source_meshes'])","len(c['resolved_source_meshes'])>0")
code=code.replace('house_native_mesh_correspondence_prepared','house_native_mesh_catalog_prepared').replace('house_native_mesh_correspondence_running','house_native_mesh_catalog_running').replace('house_native_mesh_correspondence_captured_review_pending','house_native_mesh_catalog_captured_review_pending').replace('house_native_mesh_correspondence_refusal_retained','house_native_mesh_catalog_refusal_retained')
code=code.replace('for p in [Path(__file__),recipe,package,ap,','for p in [Path(__file__),_parent,recipe,package,ap,')
exec(compile(code,str(_parent)+'#native-FBX-catalog','exec'),globals())
