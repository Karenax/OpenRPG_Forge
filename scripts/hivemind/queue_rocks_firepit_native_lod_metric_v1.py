from pathlib import Path
import hashlib,json,datetime,os,subprocess,sys
import queue_rocks_firepit_native_component_state_v1 as base
W=Path('F:/OpenRPG_ImportWork');ROOT=W/'rocks-firepit-native-lod-metric-v1';PROJECT=base.PROJECT
HELPER=PROJECT/'Assets/Editor/RocksFirePitNativeLODMetricV1.cs'
CODE=r"""
using System;using System.IO;using System.Linq;using System.Collections.Generic;using System.Reflection;using UnityEditor;using UnityEngine;
namespace OpenRPGRocksFirePitNativeLODMetricV1 {
public static class Capture {
[Serializable]public class Value{public string name;public string type;public float float_value;public string float_bits;public int integer_value;}
[Serializable]public class Sample{public string prefab_path;public string node_path;public bool group_enabled;public float source_size;public float[] local_reference_point;public float[] world_reference_point;public float[] lossy_scale;public float field_of_view;public float aspect;public float requested_relative_height;public float distance;public string distance_bits;public float[] camera_position;public Value[] native_fields;}
[Serializable]public class Result{public string unity_version;public string method_signature;public string distance_method_signature;public string primary_api_reference;public float captured_project_lod_bias;public int captured_project_maximum_lod_level;public Sample[] samples;public bool actual_gpu_visibility_measured;public bool particle_behavior_measured;}
static string Bits(float v){return BitConverter.ToUInt32(BitConverter.GetBytes(v),0).ToString("X8");}
static float[] Vec(Vector3 v){return new[]{v.x,v.y,v.z};}
static string PathOf(Transform t,Transform root){if(t==root)return "root";return PathOf(t.parent,root)+"/"+t.GetSiblingIndex().ToString("D4");}
static Value[] Fields(object info){var rows=new List<Value>();foreach(var field in info.GetType().GetFields(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic)){var value=field.GetValue(info);var row=new Value{name=field.Name,type=field.FieldType.FullName};if(value is float){row.float_value=(float)value;row.float_bits=Bits(row.float_value);}else if(value is int)row.integer_value=(int)value;else continue;rows.Add(row);}return rows.ToArray();}
public static void Export(){
 string output=Environment.GetEnvironmentVariable("OPENRPG_LOD_METRIC_OUTPUT");if(string.IsNullOrEmpty(output))throw new Exception("Explicit output required");
 var flags=BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
 var calculate=typeof(LODUtility).GetMethod("CalculateVisualizationData",flags,null,new[]{typeof(Camera),typeof(LODGroup),typeof(int)},null);
 var distance=typeof(LODUtility).GetMethod("CalculateDistance",flags,null,new[]{typeof(Camera),typeof(float),typeof(LODGroup)},null);
 if(calculate==null||distance==null)throw new Exception("Installed native LOD metric API unavailable; no approximate fallback");
 string[] paths={"Assets/Hivemind/NativeAmericanVillage/HDRP(Default)/Art/Prefabs/SM_rocks_002.prefab","Assets/Hivemind/NativeAmericanVillage/HDRP(Default)/Art/Prefabs/SM_FirePit_01.prefab"};
 var samples=new List<Sample>();var cameraObject=new GameObject("OpenRPG native LOD measurement camera");cameraObject.hideFlags=HideFlags.HideAndDontSave;var camera=cameraObject.AddComponent<Camera>();camera.enabled=false;camera.aspect=16f/9f;camera.orthographic=false;camera.usePhysicalProperties=false;
 try{foreach(string path in paths){var original=AssetDatabase.LoadAssetAtPath<GameObject>(path);if(original==null)throw new Exception("Original prefab missing");var clone=UnityEngine.Object.Instantiate(original);clone.hideFlags=HideFlags.HideAndDontSave;
  try{foreach(var group in clone.GetComponentsInChildren<LODGroup>(true)){var point=group.transform.TransformPoint(group.localReferencePoint);
   foreach(float fov in new[]{30f,60f,90f}){camera.fieldOfView=fov;
    foreach(float height in new[]{0.01f,0.03f,0.1f,0.18f,0.25f,0.35f,0.6f}){
     float d=(float)distance.Invoke(null,new object[]{camera,height,group});if(float.IsNaN(d)||float.IsInfinity(d)||d<=0)throw new Exception("Native LOD distance invalid");
     camera.transform.position=point+new Vector3(0,0,d);camera.transform.LookAt(point);
     var info=calculate.Invoke(null,new object[]{camera,group,-1});if(info==null)throw new Exception("Native LOD info absent");
     samples.Add(new Sample{prefab_path=path,node_path=PathOf(group.transform,clone.transform),group_enabled=group.enabled,source_size=group.size,local_reference_point=Vec(group.localReferencePoint),world_reference_point=Vec(point),lossy_scale=Vec(group.transform.lossyScale),field_of_view=fov,aspect=camera.aspect,requested_relative_height=height,distance=d,distance_bits=Bits(d),camera_position=Vec(camera.transform.position),native_fields=Fields(info)});
    }
   }
  }}finally{UnityEngine.Object.DestroyImmediate(clone);}
 }}finally{UnityEngine.Object.DestroyImmediate(cameraObject);}
 var result=new Result{unity_version=Application.unityVersion,method_signature=calculate.ToString(),distance_method_signature=distance.ToString(),primary_api_reference="https://github.com/Unity-Technologies/UnityCsReference/blob/master/Editor/Mono/LODUtility.bindings.cs",captured_project_lod_bias=QualitySettings.lodBias,captured_project_maximum_lod_level=QualitySettings.maximumLODLevel,samples=samples.ToArray(),actual_gpu_visibility_measured=false,particle_behavior_measured=false};
 File.WriteAllText(Path.Combine(output,"NATIVE-RESULT.v1.json"),JsonUtility.ToJson(result,true));
}
}}
"""
def sha(p):return hashlib.file_digest(Path(p).open('rb'),'sha256').hexdigest()
def read(p):return json.loads(p.read_text('utf-8-sig'))
def save(name,v):
 p=ROOT/(name+'.tmp');p.write_text(json.dumps(v,indent=2),'utf-8');p.replace(ROOT/name)
def prepare():
 # Reuse the original-source and absent-bought-script guards, not its capture producer.
 base.ROOT=ROOT;base.HELPER=HELPER;base.CODE=CODE;base.__file__=str(Path(__file__).resolve());base.prepare()
def run():
 state=dict(status='querying_actual_native_editor_lod_metrics',full_import=False)
 try:
  pins=read(ROOT/'PREPARED.v1.json')['input_pins']
  for p,h in pins.items():assert sha(p)==h,p
  env=os.environ.copy();env.update(TEMP=str(ROOT),TMP=str(ROOT),OPENRPG_LOD_METRIC_OUTPUT=str(ROOT),UPM_CACHE_ROOT=str(W/'unity-dungeon-original-shadergraph-hdrp-startup-v4/upm-cache'))
  with (ROOT/'unity.stdout.log').open('xb') as out,(ROOT/'unity.stderr.log').open('xb') as err:
   child=subprocess.Popen(['F:/Unity/6000.5.8f1/Editor/Unity.exe','-batchmode','-nographics','-quit','-projectPath',str(PROJECT),'-executeMethod','OpenRPGRocksFirePitNativeLODMetricV1.Capture.Export','-logFile',str(ROOT/'unity.log')],cwd=ROOT,env=env,stdout=out,stderr=err,creationflags=subprocess.CREATE_NO_WINDOW);state['unity_pid']=child.pid;save('RUN-RESULT.v1.json',state);state['actual_unity_exit_code']=child.wait()
  assert state['actual_unity_exit_code']==0,'Native metric API refused; preserve diagnostic'
  actual=read(ROOT/'NATIVE-RESULT.v1.json');assert len(actual['samples'])==294
  for p,h in pins.items():assert sha(p)==h,p
  state.update(status='actual_native_lod_metrics_captured_reopen_pending',samples=len(actual['samples']),input_pins_unchanged=True,native_result_sha256=sha(ROOT/'NATIVE-RESULT.v1.json'))
 except Exception as e:state.update(status='refused_or_unverified',error=repr(e))
 state['finished_utc']=datetime.datetime.now(datetime.timezone.utc).isoformat();save('RUN-RESULT.v1.json',state)
if __name__=='__main__':run() if len(sys.argv)>1 and sys.argv[1]=='run' else prepare()
