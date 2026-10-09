using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEditor;

public static class OpenRPGFirePitParticleBehavior {
 [Serializable] public class Case {public string node_path,renderer_file_id,material_guid,inherited_prefab_guid,inherited_particle_file_id,inherited_renderer_file_id;public Matrix4x4 source_local_to_world;}
 [Serializable] public class Manifest {public string output,prefab_path,prefab_guid;public Case[] cases;public uint[] seeds;public float[] times;public int repetitions;}
 [Serializable] public class Particle {public uint random_seed;public Vector3 position,velocity,rotation,angular_velocity,start_size,current_size;public float start_lifetime,remaining_lifetime;public Color32 start_color;public Color current_color;}
 [Serializable] public class Frame {public uint seed;public float requested_time,native_system_time;public int repetition,count;public Particle[] particles;public Matrix4x4 local_to_world;public string native_simulation_space,native_scaling_mode;}
 [Serializable] public class Result {public string node_path,unity_version,active_color_space,prefab_guid,particle_file_id,renderer_file_id,material_guid,material_file_id,native_particle_json,native_renderer_json,simulation_protocol;public uint original_random_seed;public bool original_auto_random_seed,original_loop,original_prewarm,original_play_on_awake;public float original_duration,original_simulation_speed;public Frame[] rows;public bool original_project_version_qualified=false,render_qualified=false,runtime_converted=false,full_import=false;}
 [Serializable] public class Report {public Result[] cases;public bool full_import=false;}
 static Transform Resolve(Transform root,string path){var parts=path.Split('/');if(parts[0]!="root")throw new Exception("Invalid source node path");foreach(var part in parts.Skip(1)){int index=int.Parse(part,CultureInfo.InvariantCulture);root=root.GetChild(index);}return root;}
 static string Identity(UnityEngine.Object asset,string guid,string expected=null){string actual;long fid;if(!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset,out actual,out fid)||actual!=guid||(expected!=null&&fid.ToString(CultureInfo.InvariantCulture)!=expected))throw new Exception("Native source identity differs: "+asset.GetType().Name);return fid.ToString(CultureInfo.InvariantCulture);}
 static void MatrixCheck(Matrix4x4 actual,Matrix4x4 expected){for(int i=0;i<16;i++){if(float.IsNaN(actual[i])||float.IsInfinity(actual[i])||Math.Abs(actual[i]-expected[i])>1e-6f)throw new Exception("Source effect transform differs");}}
 public static void Export(){
  var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,"--openrpg-manifest");if(i<0)throw new Exception("Missing manifest");var m=JsonUtility.FromJson<Manifest>(File.ReadAllText(args[i+1]));if(File.Exists(m.output))throw new Exception("Preserve prior capture");
  var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(m.prefab_path);if(prefab==null)throw new Exception("Original complete prefab not imported");Identity(prefab,m.prefab_guid);
  if(prefab.GetComponentsInChildren<Transform>(true).Length!=9||prefab.GetComponentsInChildren<ParticleSystem>(true).Length!=2||m.cases.Length!=2)throw new Exception("Complete original hierarchy differs");
  if(prefab.GetComponentsInChildren<Component>(true).Any(c=>c==null))throw new Exception("Missing source component");
  var results=new List<Result>();
  foreach(var c in m.cases){
   var node=Resolve(prefab.transform,c.node_path);var source=node.GetComponent<ParticleSystem>();var renderer=node.GetComponent<ParticleSystemRenderer>();if(source==null||renderer==null)throw new Exception("Exact source effect node absent");
   string particleId=Identity(source,m.prefab_guid),rendererId=Identity(renderer,m.prefab_guid,c.renderer_file_id);MatrixCheck(source.transform.localToWorldMatrix,c.source_local_to_world);
   Identity(PrefabUtility.GetCorrespondingObjectFromSource(source),c.inherited_prefab_guid,c.inherited_particle_file_id);Identity(PrefabUtility.GetCorrespondingObjectFromSource(renderer),c.inherited_prefab_guid,c.inherited_renderer_file_id);
   var materials=renderer.sharedMaterials;if(materials.Length!=1||materials[0]==null)throw new Exception("Original effect material absent");string materialId=Identity(materials[0],c.material_guid,"2100000");
   string sourceJson=EditorJsonUtility.ToJson(source,true),rendererJson=EditorJsonUtility.ToJson(renderer,true);var main=source.main;var rows=new List<Frame>();
   for(int repetition=0;repetition<m.repetitions;repetition++)foreach(uint seed in m.seeds)foreach(float time in m.times){
    if(time<0||float.IsNaN(time)||float.IsInfinity(time))throw new Exception("Invalid requested time");UnityEngine.Random.InitState(1729);
    var instance=UnityEngine.Object.Instantiate(prefab);instance.hideFlags=HideFlags.HideAndDontSave;
    try {
     foreach(var sibling in instance.GetComponentsInChildren<ParticleSystem>(true)){sibling.Stop(false,ParticleSystemStopBehavior.StopEmittingAndClear);sibling.useAutoRandomSeed=false;sibling.randomSeed=seed;}
     var ps=Resolve(instance.transform,c.node_path).GetComponent<ParticleSystem>();MatrixCheck(ps.transform.localToWorldMatrix,c.source_local_to_world);ps.Simulate(time,false,true,true);
     var buffer=new ParticleSystem.Particle[ps.main.maxParticles];int count=ps.GetParticles(buffer);var particles=new Particle[count];
     for(int n=0;n<count;n++){var p=buffer[n];particles[n]=new Particle{random_seed=p.randomSeed,position=p.position,velocity=p.velocity,rotation=p.rotation3D,angular_velocity=p.angularVelocity3D,start_size=p.startSize3D,current_size=p.GetCurrentSize3D(ps),start_lifetime=p.startLifetime,remaining_lifetime=p.remainingLifetime,start_color=p.startColor,current_color=p.GetCurrentColor(ps)};}
     rows.Add(new Frame{seed=seed,requested_time=time,native_system_time=ps.time,repetition=repetition,count=count,particles=particles,local_to_world=ps.transform.localToWorldMatrix,native_simulation_space=ps.main.simulationSpace.ToString(),native_scaling_mode=ps.main.scalingMode.ToString()});
    }finally{UnityEngine.Object.DestroyImmediate(instance);}
   }
   if(EditorJsonUtility.ToJson(source,true)!=sourceJson||EditorJsonUtility.ToJson(renderer,true)!=rendererJson)throw new Exception("Original effective component mutated");
   results.Add(new Result{node_path=c.node_path,unity_version=Application.unityVersion,active_color_space=QualitySettings.activeColorSpace.ToString(),prefab_guid=m.prefab_guid,particle_file_id=particleId,renderer_file_id=rendererId,material_guid=c.material_guid,material_file_id=materialId,native_particle_json=sourceJson,native_renderer_json=rendererJson,simulation_protocol="isolated complete-parent clone; Random.InitState(1729); original inherited transforms retained; Stop(clear) both siblings; useAutoRandomSeed=false; requested seed; Simulate(time,withChildren=false,restart=true,fixedTimeStep=true); no autonomous Play/prewarm or simultaneous-system parity claim",original_random_seed=source.randomSeed,original_auto_random_seed=source.useAutoRandomSeed,original_loop=main.loop,original_prewarm=main.prewarm,original_play_on_awake=main.playOnAwake,original_duration=main.duration,original_simulation_speed=main.simulationSpeed,rows=rows.ToArray()});
  }
  File.WriteAllText(m.output,JsonUtility.ToJson(new Report{cases=results.ToArray()},true));
 }
}
