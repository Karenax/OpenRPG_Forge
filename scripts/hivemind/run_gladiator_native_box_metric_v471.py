"""Measure actual SDK box queries and implicit physics properties on disposable clones."""
from pathlib import Path
CODE=r'''
using System;using System.IO;using System.Linq;using System.Collections.Generic;using UnityEditor;using UnityEngine;
namespace OpenRPGGladiatorNativeBoxMetricV471 {
public static class Capture {
[Serializable]public class Point{public int axis;public float factor;public float[] local_query;public float[] world_query;public float[] closest_world;}
[Serializable]public class RaySample{public int axis;public int sign;public float[] origin;public float[] direction;public float max_distance;public bool hit;public float distance;public float[] point;public float[] normal;}
[Serializable]public class Material{public bool original_reference_null;public float dynamic_friction;public float static_friction;public float bounciness;public string friction_combine;public string bounce_combine;}
[Serializable]public class Box{public string prefab_path;public string node_path;public string source_guid;public string source_file_id;public int game_object_layer;public string game_object_tag;public float[] local_center;public float[] local_size;public float[] world_position;public float[] world_rotation;public float[] lossy_scale;public float contact_offset;public bool enabled;public bool is_trigger;public bool provides_contacts;public Material implicit_material;public Point[] points;public RaySample[] rays;public bool disabled_raycast_hit;public float[] disabled_query;public float[] disabled_closest;}
[Serializable]public class Result{public string unity_version;public float project_default_contact_offset;public float fixed_delta_time;public int solver_iterations;public int solver_velocity_iterations;public Box[] boxes;public bool original_components_unchanged;public bool purchased_scripts_executed;public bool physical_simulation_stepped;}
static float[] Vec(Vector3 v){return new[]{v.x,v.y,v.z};}
static string NodePath(Transform t,Transform root){return t==root?"root":NodePath(t.parent,root)+"/"+t.GetSiblingIndex().ToString();}
public static void Export(){
 string output=Environment.GetEnvironmentVariable("OPENRPG_BOX_METRIC_OUTPUT");if(string.IsNullOrEmpty(output))throw new Exception("Explicit output required");
 string[] paths={SOURCE_PATHS};var rows=new List<Box>();
 foreach(var path in paths){var original=AssetDatabase.LoadAssetAtPath<GameObject>(path);if(original==null)throw new Exception("Original prefab missing");
  var components=original.GetComponentsInChildren<Component>(true);if(components.Any(c=>c==null||c is MonoBehaviour))throw new Exception("Missing or purchased script component refused");var snapshots=components.Select(c=>EditorJsonUtility.ToJson(c,true)).ToArray();
  var sourceBoxes=original.GetComponentsInChildren<BoxCollider>(true);var clone=UnityEngine.Object.Instantiate(original);clone.hideFlags=HideFlags.HideAndDontSave;
  try{var cloneBoxes=clone.GetComponentsInChildren<BoxCollider>(true);if(sourceBoxes.Length!=cloneBoxes.Length)throw new Exception("Cloned box count differs");Physics.SyncTransforms();
   for(int index=0;index<sourceBoxes.Length;index++){var source=sourceBoxes[index];var box=cloneBoxes[index];string sourcePath=NodePath(source.transform,original.transform);if(sourcePath!=NodePath(box.transform,clone.transform))throw new Exception("Cloned box identity path differs");string guid;long fileId;if(!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(source,out guid,out fileId))throw new Exception("Source collider identity absent");
    if(!box.enabled||!box.gameObject.activeInHierarchy)throw new Exception("Native active box required; disabled source not approximated");var points=new List<Point>();var rays=new List<RaySample>();var center=box.center;var half=box.size*0.5f;
    for(int axis=0;axis<3;axis++)foreach(float factor in new[]{-2f,-1f,-0.999f,0f,0.999f,1f,2f}){var local=center;local[axis]+=half[axis]*factor;var world=box.transform.TransformPoint(local);points.Add(new Point{axis=axis,factor=factor,local_query=Vec(local),world_query=Vec(world),closest_world=Vec(box.ClosestPoint(world))});}
    for(int axis=0;axis<3;axis++)foreach(int sign in new[]{-1,1}){var local=center;local[axis]+=half[axis]*2f*sign;var unit=Vector3.zero;unit[axis]=-sign;var origin=box.transform.TransformPoint(local);var direction=box.transform.TransformDirection(unit).normalized;float maximum=box.transform.TransformVector(unit*half[axis]*4f).magnitude;RaycastHit hit;bool found=box.Raycast(new Ray(origin,direction),out hit,maximum);rays.Add(new RaySample{axis=axis,sign=sign,origin=Vec(origin),direction=Vec(direction),max_distance=maximum,hit=found,distance=found?hit.distance:0f,point=found?Vec(hit.point):new float[0],normal=found?Vec(hit.normal):new float[0]});}
    var shared=box.sharedMaterial;if(shared!=null)throw new Exception("Unexpected explicit physics material; preserve for dedicated qualification");var material=box.material;Material properties;
    try{properties=new Material{original_reference_null=true,dynamic_friction=material.dynamicFriction,static_friction=material.staticFriction,bounciness=material.bounciness,friction_combine=material.frictionCombine.ToString(),bounce_combine=material.bounceCombine.ToString()};}
    finally{box.sharedMaterial=shared;UnityEngine.Object.DestroyImmediate(material);}
    var disabledPoint=box.transform.TransformPoint(center+box.size*2f);box.enabled=false;Physics.SyncTransforms();var disabledClosest=box.ClosestPoint(disabledPoint);RaycastHit disabledHit;bool disabledRay=box.Raycast(new Ray(new Vector3(rays[0].origin[0],rays[0].origin[1],rays[0].origin[2]),new Vector3(rays[0].direction[0],rays[0].direction[1],rays[0].direction[2])),out disabledHit,rays[0].max_distance);box.enabled=source.enabled;Physics.SyncTransforms();var rotation=box.transform.rotation;
    rows.Add(new Box{prefab_path=path,node_path=sourcePath,source_guid=guid,source_file_id=fileId.ToString(),game_object_layer=source.gameObject.layer,game_object_tag=source.gameObject.tag,local_center=Vec(center),local_size=Vec(box.size),world_position=Vec(box.transform.position),world_rotation=new[]{rotation.x,rotation.y,rotation.z,rotation.w},lossy_scale=Vec(box.transform.lossyScale),contact_offset=box.contactOffset,enabled=source.enabled,is_trigger=source.isTrigger,provides_contacts=source.providesContacts,implicit_material=properties,points=points.ToArray(),rays=rays.ToArray(),disabled_raycast_hit=disabledRay,disabled_query=Vec(disabledPoint),disabled_closest=Vec(disabledClosest)});
   }
  }finally{UnityEngine.Object.DestroyImmediate(clone);for(int q=0;q<components.Length;q++)if(snapshots[q]!=EditorJsonUtility.ToJson(components[q],true))throw new Exception("Source component changed during box queries");}
 }
 var result=new Result{unity_version=Application.unityVersion,project_default_contact_offset=Physics.defaultContactOffset,fixed_delta_time=Time.fixedDeltaTime,solver_iterations=Physics.defaultSolverIterations,solver_velocity_iterations=Physics.defaultSolverVelocityIterations,boxes=rows.ToArray(),original_components_unchanged=true,purchased_scripts_executed=false,physical_simulation_stepped=false};File.WriteAllText(Path.Combine(output,"NATIVE-RESULT.v1.json"),JsonUtility.ToJson(result,true));
}
}}
'''
parent=Path(__file__).with_name('run_gladiator_component_state_v447.py')
code=parent.read_text('utf-8').replace('gladiator-native-component-state-v447','gladiator-native-box-metric-v471')
start=code.index("code=code.replace('OpenRPGRocksFirePitComponentStateV1'")
end=code.index('helper=PROJECT/',start)
code=code[:start]+"code=CODE.replace('SOURCE_PATHS',','.join(json.dumps(p) for p in paths))\n"+code[end:]
code=code.replace('GladiatorComponentStateV447.cs','GladiatorNativeBoxMetricV471.cs').replace('OPENRPG_COMPONENT_STATE_OUTPUT','OPENRPG_BOX_METRIC_OUTPUT').replace('OpenRPGGladiatorComponentStateV447.Capture.Export','OpenRPGGladiatorNativeBoxMetricV471.Capture.Export')
old="assert len(native['prefabs'])==5 and sum(len(n['renderers']) for p in native['prefabs'] for n in p['nodes'])==17\n    assert sum(len(n['colliders']) for p in native['prefabs'] for n in p['nodes'])==5"
assert old in code
code=code.replace(old,"assert len(native['boxes'])==3 and sum(len(b['points']) for b in native['boxes'])==63 and sum(len(b['rays']) for b in native['boxes'])==18")
code=code.replace('prefabs_instantiated=False','prefabs_instantiated=True')
code=code.replace('five_prefab_renderer_lod_and_collider_states_captured_review_pending','three_native_box_query_sets_captured_review_pending').replace('gladiator_component_capture_prepared','gladiator_box_metric_prepared').replace('gladiator_native_component_capture_running','gladiator_native_box_metric_running').replace('gladiator_component_state_refusal_retained','gladiator_native_box_metric_refusal_retained')
exec(compile(code,str(parent)+'#native-box-metric-v471','exec'),globals())
