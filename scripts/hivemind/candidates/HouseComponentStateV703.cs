
using System;using System.IO;using System.Linq;using System.Collections.Generic;using UnityEditor;using UnityEngine;
namespace OpenRPGHouseNativeMeshMetricV703 {
public static class Capture {
[Serializable]public class Point{public int axis;public float factor;public float[] local_query;public float[] world_query;public float[] closest_world;}
[Serializable]public class RaySample{public int axis;public int sign;public float[] origin;public float[] direction;public float max_distance;public bool hit;public float distance;public float[] point;public float[] normal;}
[Serializable]public class Material{public bool original_reference_null;public float dynamic_friction;public float static_friction;public float bounciness;public string friction_combine;public string bounce_combine;}
[Serializable]public class Box{public FaceProbe[] native_face_probes;public bool convex;public string mesh_guid;public string mesh_file_id;public int cooking_options;public string prefab_path;public string node_path;public string source_guid;public string source_file_id;public int game_object_layer;public string game_object_tag;public float[] local_center;public float[] local_size;public float[] world_position;public float[] world_rotation;public float[] lossy_scale;public float contact_offset;public bool enabled;public bool is_trigger;public bool provides_contacts;public Material implicit_material;public Point[] points;public RaySample[] rays;public bool disabled_raycast_hit;public float[] disabled_query;public float[] disabled_closest;}
[Serializable]public class Result{public string unity_version;public float project_default_contact_offset;public float fixed_delta_time;public int solver_iterations;public int solver_velocity_iterations;public Box[] boxes;public bool original_components_unchanged;public bool purchased_scripts_executed;public bool physical_simulation_stepped;}

[Serializable]public class FaceProbe{public string family;public int sample;public float[] origin;public float[] direction;public float max_distance;public bool hit;public float[] point;public float[] normal;public float distance;}
static FaceProbe[] ProbePlanes(MeshCollider collider){
 if(!collider.convex)return new FaceProbe[0];
 var mesh=collider.sharedMesh;var vertices=mesh.vertices;var triangles=mesh.triangles;
 var center=mesh.bounds.center;float radius=mesh.bounds.extents.magnitude;if(!float.IsFinite(radius)||radius<=0)throw new Exception("Invalid convex bounds");
 var rows=new List<FaceProbe>();
 Action<string,int,Vector3,Vector3> probe=(family,sample,target,normal)=>{
  var origin=collider.transform.TransformPoint(target+normal*radius*4);var direction=collider.transform.TransformDirection(-normal).normalized;
  float maximum=collider.transform.TransformVector(normal*radius*8).magnitude;
  RaycastHit hit;bool found=collider.Raycast(new Ray(origin,direction),out hit,maximum);
  rows.Add(new FaceProbe{family=family,sample=sample,origin=Vec(origin),direction=Vec(direction),max_distance=maximum,hit=found,point=found?Vec(hit.point):new float[0],normal=found?Vec(hit.normal):new float[0],distance=found?hit.distance:0});
 };
 for(int t=0;t<triangles.Length;t+=3){
  var a=vertices[triangles[t]];var b=vertices[triangles[t+1]];var c=vertices[triangles[t+2]];var normal=Vector3.Cross(b-a,c-a);
  if(normal.sqrMagnitude<=1e-20f)continue;normal.Normalize();var target=(a+b+c)/3f;if(Vector3.Dot(normal,target-center)<0)normal=-normal;
  probe("source_triangle_face",t/3,target,normal);
 }
 for(int s=0;s<16384;s++){
  double y=1.0-2.0*(s+0.5)/16384.0;double r=Math.Sqrt(Math.Max(0,1-y*y));double angle=s*Math.PI*(3-Math.Sqrt(5));
  var normal=new Vector3((float)(r*Math.Cos(angle)),(float)y,(float)(r*Math.Sin(angle))).normalized;
  probe("training_fibonacci_direction",s,center,normal);
 }
 
 var rotation=Quaternion.Euler(17,31,53);
 for(int s=0;s<1024;s++){
  double y=1.0-2.0*(s+0.25)/1024.0;double r=Math.Sqrt(Math.Max(0,1-y*y));double angle=(s+0.375)*Math.PI*(3-Math.Sqrt(5));
  var normal=rotation*new Vector3((float)(r*Math.Cos(angle)),(float)y,(float)(r*Math.Sin(angle))).normalized;
  probe("independent_rotated_fibonacci",s,center,normal.normalized);
 }
return rows.ToArray();
}
static float[] Vec(Vector3 v){return new[]{v.x,v.y,v.z};}
static string NodePath(Transform t,Transform root){return t==root?"root":NodePath(t.parent,root)+"/"+t.GetSiblingIndex().ToString();}
public static void Export(){
 string output=Environment.GetEnvironmentVariable("OPENRPG_BOX_METRIC_OUTPUT");if(string.IsNullOrEmpty(output))throw new Exception("Explicit output required");
 string[] paths={"Assets/Hivemind/HouseOnaHill/HDRP/Art/Prefabs/SM_Fence_03.prefab","Assets/Hivemind/HouseOnaHill/HDRP/Art/Prefabs/SM_Pot_05.prefab","Assets/Hivemind/HouseOnaHill/HDRP/Art/Prefabs/SM_WateringPot_01.prefab","Assets/Hivemind/HouseOnaHill/HDRP/Art/Prefabs/SM_Bench_01.prefab","Assets/Hivemind/HouseOnaHill/HDRP/Art/Prefabs/SM_Pot_02.prefab","Assets/Hivemind/HouseOnaHill/HDRP/Art/Prefabs/SM_Gravestone_01.prefab","Assets/Hivemind/HouseOnaHill/HDRP/Art/Prefabs/SM_PotPlate_01.prefab","Assets/Hivemind/HouseOnaHill/HDRP/Art/Prefabs/SM_PotPlate_02.prefab","Assets/Hivemind/HouseOnaHill/HDRP/Art/Prefabs/SM_Rake_01.prefab","Assets/Hivemind/HouseOnaHill/HDRP/Art/Prefabs/SM_Swing.prefab","Assets/Hivemind/HouseOnaHill/HDRP/Art/Prefabs/SM_Fence_02.prefab","Assets/Hivemind/HouseOnaHill/HDRP/Art/Prefabs/SM_SwingAttachment.prefab","Assets/Hivemind/HouseOnaHill/HDRP/Art/Prefabs/SM_Spade_01.prefab","Assets/Hivemind/HouseOnaHill/HDRP/Art/Prefabs/SM_Lamp_01.prefab","Assets/Hivemind/HouseOnaHill/HDRP/Art/Prefabs/SM_GroundPlates_01.prefab","Assets/Hivemind/HouseOnaHill/HDRP/Art/Prefabs/SM_Fence_01.prefab","Assets/Hivemind/HouseOnaHill/HDRP/Art/Prefabs/SM_Pot_04.prefab","Assets/Hivemind/HouseOnaHill/HDRP/Art/Prefabs/SM_Trough_01.prefab","Assets/Hivemind/HouseOnaHill/HDRP/Art/Prefabs/SM_WoodBeam_01.prefab","Assets/Hivemind/HouseOnaHill/HDRP/Art/Prefabs/SM_WoodBeam_02.prefab","Assets/Hivemind/HouseOnaHill/HDRP/Art/Prefabs/SM_Pot_01.prefab","Assets/Hivemind/HouseOnaHill/HDRP/Art/Prefabs/SM_WoodBeam_03.prefab","Assets/Hivemind/HouseOnaHill/HDRP/Art/Prefabs/SM_SwingSeat.prefab","Assets/Hivemind/HouseOnaHill/HDRP/Art/Prefabs/SM_Pot_03.prefab"};var rows=new List<Box>();
 foreach(var path in paths){var original=AssetDatabase.LoadAssetAtPath<GameObject>(path);if(original==null)throw new Exception("Original prefab missing");
  var components=original.GetComponentsInChildren<Component>(true);if(components.Any(c=>c==null||c is MonoBehaviour))throw new Exception("Missing or purchased script component refused");var snapshots=components.Select(c=>EditorJsonUtility.ToJson(c,true)).ToArray();
  var sourceBoxes=original.GetComponentsInChildren<MeshCollider>(true);var clone=UnityEngine.Object.Instantiate(original);clone.hideFlags=HideFlags.HideAndDontSave;
  try{var cloneBoxes=clone.GetComponentsInChildren<MeshCollider>(true);if(sourceBoxes.Length!=cloneBoxes.Length)throw new Exception("Cloned box count differs");Physics.SyncTransforms();
   for(int index=0;index<sourceBoxes.Length;index++){var source=sourceBoxes[index];var box=cloneBoxes[index];string sourcePath=NodePath(source.transform,original.transform);if(sourcePath!=NodePath(box.transform,clone.transform))throw new Exception("Cloned box identity path differs");string guid;long fileId;if(!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(source,out guid,out fileId))throw new Exception("Source collider identity absent");
    if(!box.enabled||!box.gameObject.activeInHierarchy)throw new Exception("Native active box required; disabled source not approximated");if(box.sharedMesh==null)continue;string meshGuid;long meshFileId;if(!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(source.sharedMesh,out meshGuid,out meshFileId))throw new Exception("Mesh identity absent");var points=new List<Point>();var rays=new List<RaySample>();var center=box.sharedMesh.bounds.center;var size=box.sharedMesh.bounds.size;var half=size*0.5f;if(half.x<=0||half.y<=0||half.z<=0)throw new Exception("Degenerate mesh bounds refused");
    if(box.convex)for(int axis=0;axis<3;axis++)foreach(float factor in new[]{-2f,-1f,-0.999f,0f,0.999f,1f,2f}){var local=center;local[axis]+=half[axis]*factor;var world=box.transform.TransformPoint(local);points.Add(new Point{axis=axis,factor=factor,local_query=Vec(local),world_query=Vec(world),closest_world=Vec(box.ClosestPoint(world))});}
    for(int axis=0;axis<3;axis++)foreach(int sign in new[]{-1,1}){var local=center;local[axis]+=half[axis]*2f*sign;var unit=Vector3.zero;unit[axis]=-sign;var origin=box.transform.TransformPoint(local);var direction=box.transform.TransformDirection(unit).normalized;float maximum=box.transform.TransformVector(unit*half[axis]*4f).magnitude;RaycastHit hit;bool found=box.Raycast(new Ray(origin,direction),out hit,maximum);rays.Add(new RaySample{axis=axis,sign=sign,origin=Vec(origin),direction=Vec(direction),max_distance=maximum,hit=found,distance=found?hit.distance:0f,point=found?Vec(hit.point):new float[0],normal=found?Vec(hit.normal):new float[0]});}
    var shared=box.sharedMaterial;if(shared!=null)throw new Exception("Unexpected explicit physics material; preserve for dedicated qualification");var material=box.material;Material properties;
    try{properties=new Material{original_reference_null=true,dynamic_friction=material.dynamicFriction,static_friction=material.staticFriction,bounciness=material.bounciness,friction_combine=material.frictionCombine.ToString(),bounce_combine=material.bounceCombine.ToString()};}
    finally{box.sharedMaterial=shared;UnityEngine.Object.DestroyImmediate(material);}
    var disabledPoint=box.transform.TransformPoint(center+size*2f);box.enabled=false;Physics.SyncTransforms();var disabledClosest=box.convex?Vec(box.ClosestPoint(disabledPoint)):new float[0];RaycastHit disabledHit;bool disabledRay=box.Raycast(new Ray(new Vector3(rays[0].origin[0],rays[0].origin[1],rays[0].origin[2]),new Vector3(rays[0].direction[0],rays[0].direction[1],rays[0].direction[2])),out disabledHit,rays[0].max_distance);box.enabled=source.enabled;Physics.SyncTransforms();var rotation=box.transform.rotation;
    rows.Add(new Box{native_face_probes=ProbePlanes(box),convex=source.convex,mesh_guid=meshGuid,mesh_file_id=meshFileId.ToString(),cooking_options=(int)source.cookingOptions,prefab_path=path,node_path=sourcePath,source_guid=guid,source_file_id=fileId.ToString(),game_object_layer=source.gameObject.layer,game_object_tag=source.gameObject.tag,local_center=Vec(center),local_size=Vec(size),world_position=Vec(box.transform.position),world_rotation=new[]{rotation.x,rotation.y,rotation.z,rotation.w},lossy_scale=Vec(box.transform.lossyScale),contact_offset=box.contactOffset,enabled=source.enabled,is_trigger=source.isTrigger,provides_contacts=source.providesContacts,implicit_material=properties,points=points.ToArray(),rays=rays.ToArray(),disabled_raycast_hit=disabledRay,disabled_query=Vec(disabledPoint),disabled_closest=disabledClosest});
   }
  }finally{UnityEngine.Object.DestroyImmediate(clone);for(int q=0;q<components.Length;q++)if(snapshots[q]!=EditorJsonUtility.ToJson(components[q],true))throw new Exception("Source component changed during box queries");}
 }
 var result=new Result{unity_version=Application.unityVersion,project_default_contact_offset=Physics.defaultContactOffset,fixed_delta_time=Time.fixedDeltaTime,solver_iterations=Physics.defaultSolverIterations,solver_velocity_iterations=Physics.defaultSolverVelocityIterations,boxes=rows.ToArray(),original_components_unchanged=true,purchased_scripts_executed=false,physical_simulation_stepped=false};File.WriteAllText(Path.Combine(output,"NATIVE-RESULT.v1.json"),JsonUtility.ToJson(result,true));
}
}}
