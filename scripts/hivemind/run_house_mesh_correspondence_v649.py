"""Prove exact native FBX/name matches and retain missing prefab references."""
from pathlib import Path
recipe=Path(__file__).with_name('run_house_prefab_geometry_v648.py')
code=recipe.read_text('utf-8-sig').replace('house-native-prefab-geometry-v648','house-native-mesh-correspondence-v649').replace('HousePrefabGeometryV648','HouseMeshCorrespondenceV649')
code=code.replace("ap=W/'house-native-sampling-policy-v644/HELPER-OWNERSHIP.v1.json'","ap=W/'house-native-prefab-geometry-v648/HELPER-OWNERSHIP.v1.json'").replace("len(audit['helpers'])==32","len(audit['helpers'])==33")
needle="    text=text.rstrip();assert text.endswith('}');text=text[:-1]+addition+'\\n}\\n';base="
replacement="""    text=text.rstrip();assert text.endswith('}');text=text[:-1]+addition+'\\n}\\n'
    text=text.replace('public int expected_mesh_filters;}','public int expected_mesh_filters;public MeshExpectation[] expected_meshes;}')
    text=text.replace('public string unity_version,prefab_guid,card_id;','public string unity_version,prefab_guid,card_id;public int missing_native_mesh_filter_references;public Resolved[] resolved_source_meshes;')
    text=text.replace('cases.Add(Capture(c))','cases.Add(CaptureCatalog(c))')
    catalog='''
 [Serializable]public class MeshExpectation{public string path,guid,name,declared_file_id,resolution,transform_file_id;}
 [Serializable]public class Resolved{public MeshExpectation source;public Identity native;public bool name_and_guid_exact,declared_file_id_matches_native;}
 static Report CaptureCatalog(Request r){var p=AssetDatabase.LoadAssetAtPath<GameObject>(r.prefab_path);if(p==null||Id(p).guid!=r.prefab_guid)throw new Exception("Wrong private prefab");var components=p.GetComponentsInChildren<Component>(true);if(components.Any(c=>c==null||c is MonoBehaviour||c is SkinnedMeshRenderer))throw new Exception("Non-static or missing source component");var before=components.Select(c=>EditorJsonUtility.ToJson(c,true)).ToArray();var meshes=new Dictionary<string,Mesh>();var resolved=new List<Resolved>();
  foreach(var e in r.expected_meshes){var candidates=AssetDatabase.LoadAllAssetsAtPath(e.path).OfType<Mesh>().Where(m=>m.name==e.name).ToArray();if(candidates.Length!=1)throw new Exception("Exact unique FBX name match required: "+e.path+"/"+e.name+"; found "+candidates.Length);var m=candidates[0];var id=Id(m);if(id.guid!=e.guid)throw new Exception("Source FBX GUID differs");meshes[id.guid+":"+id.file_id]=m;resolved.Add(new Resolved{source=e,native=id,name_and_guid_exact=true,declared_file_id_matches_native=id.file_id==e.declared_file_id});}
  if(!before.SequenceEqual(components.Select(c=>EditorJsonUtility.ToJson(c,true))))throw new Exception("Source components changed");return new Report{unity_version=Application.unityVersion,prefab_guid=r.prefab_guid,card_id=r.card_id,meshes=meshes.Values.Select(ExportMesh).ToArray(),bindings=new Binding[0],lod_groups=new LodSet[0],source_components_unchanged=true,component_snapshots=before,nodes=Nodes(p.transform,"root","").ToArray(),missing_native_mesh_filter_references=components.OfType<MeshFilter>().Count(f=>f.sharedMesh==null),resolved_source_meshes=resolved.ToArray()};
 }
'''
    text=text.rstrip();text=text[:-1]+catalog+'\\n}\\n';base="""
assert code.count(needle)==1;code=code.replace(needle,replacement)
needle="request=O/'REQUEST.v1.json';request.write_text"
replacement="""request=O/'REQUEST.v1.json'
    selected_by_id={r['id']:r for r in selected}
    for c in cases:
        c['expected_meshes']=[dict(path=m['mesh_source'],guid=m['mesh_guid'],name=m['mesh_name'],declared_file_id=str(m['mesh_fileid']),resolution=m['mesh_resolution'],transform_file_id=str(m['transform_id'])) for m in selected_by_id[c['card_id']]['components']]
    request.write_text"""
assert code.count(needle)==1;code=code.replace(needle,replacement)
code=code.replace("all(c['source_components_unchanged'] and len(c['meshes'])>0 and len(c['nodes'])>0 for c in native['cases'])","all(c['source_components_unchanged'] and len(c['meshes'])>0 and all(m['name_and_guid_exact'] for m in c['resolved_source_meshes']) for c in native['cases'])")
code=code.replace('house_native_prefab_geometry_prepared','house_native_mesh_correspondence_prepared').replace('house_native_prefab_geometry_running','house_native_mesh_correspondence_running').replace('house_native_prefab_geometry_captured_review_pending','house_native_mesh_correspondence_captured_review_pending').replace('house_native_prefab_geometry_refusal_retained','house_native_mesh_correspondence_refusal_retained')
code=code.replace('for p in [Path(__file__),package,ap,','for p in [Path(__file__),recipe,package,ap,')
exec(compile(code,str(recipe)+'#exact-native-mesh-correspondence','exec'),globals())
