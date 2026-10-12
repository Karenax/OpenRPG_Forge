using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

// Original mesh assets and material slot arrays; isolated diagnostic instances.
public static class OpenRPGSourceTownsmithRenderContextV2 {
 [Serializable] public class Ref {public string guid,file_id,asset_path;}
 [Serializable] public class Case {public string id,control_mode;public Ref mesh;public Ref[] materials;public int expected_submeshes,affected_bindings;}
 [Serializable] public class Request {public string output;public Case[] cases;}
 [Serializable] public class Pass {public string material,shader,name;public int index;public bool compiled;}
 [Serializable] public class Draw {public int event_index;public string mesh_subset,pass_name,original_shader,real_shader,component,index_count;}
 [Serializable] public class Row {public string id,mesh_identity,image,raw_image;public int vertices,submeshes,material_slots,event_count,matching_mesh_events;public bool native_draw_mapping_observed;public string[] material_identities;public Pass[] compiled_passes;public Draw[] draws;public string frame_debugger_error;public string[] material_before_paths,material_after_compile_paths,material_after_render_paths;public int source_material_runtime_states_changed;public bool source_material_runtime_states_restored;public bool original_material_runtime_states_unchanged;public bool private_material_instances_used=true;public float max_linear_channel;}
 [Serializable] public class Context {
  public string id,anisotropic_filtering,ambient_mode,tonemapping,skybox_identity;
  public Vector3 bounds_center,bounds_size,camera_position; public Quaternion camera_rotation;
  public Matrix4x4 camera_world_to_view,camera_projection; public Color ambient_light,ambient_sky,ambient_equator,ambient_ground,light_color;
  public float radius,fov,near,far,exposure_ev,light_intensity,ambient_intensity; public int msaa_samples,width=512,height=512;
  public bool source_prefab_transform_context=false,source_original_material=true,full_import=false;
 }
 [Serializable] public class Result {public string gpu,backend,pipeline;public bool private_material_instances_used=true;public bool frame_debugger_locally_supported,source_objects_unchanged,original_materials_preserved=true,isolated_mesh_renderer_context=true,original_prefab_transform_context=false,full_import=false,forge_editor_verified=false;public Row[] rows;}
 static BindingFlags flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static;
 static string Identity(UnityEngine.Object obj) {if(obj==null||!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(obj,out string guid,out long id))throw new Exception("Source identity absent");return guid+":"+id;}
 static T Load<T>(Ref r) where T:UnityEngine.Object {if(AssetDatabase.AssetPathToGUID(r.asset_path)!=r.guid)throw new Exception("Source path/GUID changed: "+r.asset_path);return AssetDatabase.LoadAllAssetsAtPath(r.asset_path).OfType<T>().Single(x=>Identity(x)==r.guid+":"+r.file_id);}
 static string Field(object value,string name) {return value.GetType().GetField(name,BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance)?.GetValue(value)?.ToString()??"";}
 static bool LocalSupported(Type utility) {return (bool)(utility.GetProperty("locallySupported",flags)?.GetValue(null)??false);}
 public static void Export() {
  string root=Environment.GetEnvironmentVariable("OPENRPG_TOWNSMITH_DRAW_OUT");if(string.IsNullOrEmpty(root))throw new Exception("Output absent");
  var request=JsonUtility.FromJson<Request>(File.ReadAllText(Path.Combine(root,"REQUEST.v1.json")));if(request.cases.Length!=2)throw new Exception("Two fixed neutral/raw-linear reference controls required");
  if(File.Exists(request.output))throw new Exception("Preserve evidence");
  AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
  if(PlayerSettings.colorSpace!=ColorSpace.Linear||SystemInfo.graphicsDeviceType==GraphicsDeviceType.Null)throw new Exception("Linear native GPU required");
  var pipeline=GraphicsSettings.currentRenderPipeline as HDRenderPipelineAsset;if(pipeline==null)throw new Exception("HDRP required");
  var prepare=typeof(RenderPipelineManager).GetMethod("TryPrepareRenderPipeline",flags,null,new[]{typeof(RenderPipelineAsset)},null);
  if(prepare==null||!(bool)prepare.Invoke(null,new object[]{pipeline}))throw new Exception("HDRP initialization failed");
  var utility=typeof(Editor).Assembly.GetType("UnityEditorInternal.FrameDebuggerInternal.FrameDebuggerUtility",true);
  bool supported=LocalSupported(utility);var rows=new List<Row>();var ownedMaterials=new List<Material>();var ownedMeshes=new List<Mesh>();bool priorAsync=ShaderUtil.allowAsyncCompilation;ShaderUtil.allowAsyncCompilation=false;
  try {foreach(var item in request.cases) {
   var mesh=Load<Mesh>(item.mesh);var sourceMaterials=item.materials.Select(Load<Material>).ToArray();
   var sourceBefore=sourceMaterials.Select(x=>EditorJsonUtility.ToJson(x,true)).ToArray();
   var instances=sourceMaterials.Distinct().ToDictionary(x=>x,x=>new Material(x){hideFlags=HideFlags.HideAndDontSave});ownedMaterials.AddRange(instances.Values);
   var materials=sourceMaterials.Select(x=>instances[x]).ToArray();
   if(mesh.subMeshCount!=item.expected_submeshes)throw new Exception("Source submesh count changed");
   var before=materials.Select(x=>EditorJsonUtility.ToJson(x,true)).ToArray();var passes=new List<Pass>();
   foreach(var material in materials.Distinct()) {
    if(material.shader==null||!material.shader.isSupported||ShaderUtil.ShaderHasError(material.shader)||material.shader.name=="Hidden/InternalErrorShader")throw new Exception("Original shader unresolved: "+Identity(material));
    for(int i=0;i<material.passCount;i++) {
     string name=material.GetPassName(i);if(!material.GetShaderPassEnabled(name)||name.IndexOf("RayTracing",StringComparison.OrdinalIgnoreCase)>=0)continue;
     ShaderUtil.CompilePass(material,i,true);
     if(!ShaderUtil.IsPassCompiled(material,i)||ShaderUtil.ShaderHasError(material.shader))throw new Exception("Source pass compilation refused: "+name);
     passes.Add(new Pass{material=Identity(sourceMaterials[Array.IndexOf(materials,material)]),shader=Identity(material.shader),index=i,name=name,compiled=true});
    }
   }
   var afterCompile=materials.Select(x=>EditorJsonUtility.ToJson(x,true)).ToArray();
   var scene=EditorSceneManager.NewPreviewScene();GameObject fixture=null,co=null,lo=null,vo=null;VolumeProfile profile=null;RenderTexture target=null;
   try {
    fixture=new GameObject("original-mesh-slot-control");fixture.hideFlags=HideFlags.HideAndDontSave;UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(fixture,scene);
    fixture.AddComponent<MeshFilter>().sharedMesh=mesh;var renderer=fixture.AddComponent<MeshRenderer>();renderer.sharedMaterials=materials;
    var bounds=renderer.bounds;float radius=bounds.extents.magnitude;if(!float.IsFinite(radius)||radius<=0)throw new Exception("Source bounds invalid");
    bool sourceRenderer=item.control_mode.StartsWith("source_");
    if(!new[]{"source_default","visible_default","visible_cast_off","source_shadowed","visible_shadowed"}.Contains(item.control_mode))throw new Exception("Unknown shadow-mode control");
    MeshRenderer actualRenderer=renderer;
    if(!sourceRenderer) {
     renderer.enabled=false;
     var part=UnityEngine.Object.Instantiate(mesh);part.hideFlags=HideFlags.HideAndDontSave;ownedMeshes.Add(part);
     for(int sub=1;sub<part.subMeshCount;sub++)part.SetSubMesh(sub,new SubMeshDescriptor(0,0,mesh.GetTopology(sub)),MeshUpdateFlags.DontRecalculateBounds);
     part.bounds=mesh.bounds;
     if(!part.GetIndices(0,true).SequenceEqual(mesh.GetIndices(0,true))||!part.vertices.SequenceEqual(mesh.vertices)||!part.normals.SequenceEqual(mesh.normals)||!part.tangents.SequenceEqual(mesh.tangents)||!part.uv.SequenceEqual(mesh.uv)||!part.GetVertexAttributes().SequenceEqual(mesh.GetVertexAttributes()))throw new Exception("Original selected geometry changed");
     var child=new GameObject("visible-original-submesh");child.hideFlags=HideFlags.HideAndDontSave;UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(child,scene);child.transform.SetParent(fixture.transform,false);
     child.AddComponent<MeshFilter>().sharedMesh=part;actualRenderer=child.AddComponent<MeshRenderer>();actualRenderer.sharedMaterials=materials;
    }
    actualRenderer.shadowCastingMode=item.control_mode=="visible_cast_off"?ShadowCastingMode.Off:ShadowCastingMode.On;
    co=new GameObject("source-camera");UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(co,scene);var camera=co.AddComponent<Camera>();camera.scene=scene;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.025f,.025f,.025f);camera.fieldOfView=30;camera.nearClipPlane=Mathf.Max(.0001f,radius*.01f);camera.farClipPlane=radius*12;camera.transform.position=bounds.center+new Vector3(1.4f,.8f,1.8f).normalized*radius*4;camera.transform.LookAt(bounds.center);camera.gameObject.AddComponent<HDAdditionalCameraData>();
    lo=new GameObject("source-sun");UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(lo,scene);var light=lo.AddComponent<Light>();light.type=LightType.Directional;light.intensity=15000;light.transform.rotation=camera.transform.rotation;var hdLight=lo.AddComponent<HDAdditionalLightData>();
    if(item.control_mode.EndsWith("_shadowed"))light.shadows=LightShadows.Soft;
    File.WriteAllText(Path.Combine(root,item.id+".light.json"),EditorJsonUtility.ToJson(light,true));
    File.WriteAllText(Path.Combine(root,item.id+".hdlight.json"),EditorJsonUtility.ToJson(hdLight,true));
    File.WriteAllText(Path.Combine(root,item.id+".control.txt"),"mode="+item.control_mode+"; light shadows="+light.shadows+"; renderer shadow casting="+actualRenderer.shadowCastingMode+"; source renderer="+sourceRenderer+"; original selected geometry exact");
    vo=new GameObject("source-volume");UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(vo,scene);var volume=vo.AddComponent<Volume>();volume.isGlobal=true;volume.priority=10000;profile=ScriptableObject.CreateInstance<VolumeProfile>();volume.sharedProfile=profile;var exposure=profile.Add<Exposure>();exposure.mode.Override(ExposureMode.Fixed);exposure.fixedExposure.Override(8);var tone=profile.Add<Tonemapping>();tone.mode.Override(item.id.EndsWith("-linear")?TonemappingMode.None:TonemappingMode.Neutral);
    target=new RenderTexture(512,512,24,RenderTextureFormat.ARGBHalf,RenderTextureReadWrite.Linear);if(!target.Create())throw new Exception("Target creation failed");
    var render=new RenderPipeline.StandardRequest{destination=target};if(!RenderPipeline.SupportsRenderRequest(camera,render))throw new Exception("HDRP request unavailable");
    for(int warm=0;warm<3;warm++)RenderPipeline.SubmitRenderRequest(camera,render);
    var row=new Row{id=item.id,mesh_identity=Identity(mesh),vertices=mesh.vertexCount,submeshes=mesh.subMeshCount,material_slots=materials.Length,material_identities=sourceMaterials.Select(Identity).ToArray(),compiled_passes=passes.ToArray()};var draws=new List<Draw>();
    if(supported) {
     try {
      var enable=utility.GetMethod("SetEnabled",flags,null,new[]{typeof(bool),typeof(int)},null);if(enable==null)throw new Exception("Enable signature absent");enable.Invoke(null,new object[]{true,0});
      RenderPipeline.SubmitRenderRequest(camera,render);
      var events=utility.GetMethod("GetFrameEvents",flags)?.Invoke(null,null) as Array;row.event_count=events?.Length??0;
      var dataMethod=utility.GetMethods(flags).SingleOrDefault(m=>m.Name=="GetFrameEventData"&&m.GetParameters().Length==2&&m.GetParameters()[0].ParameterType==typeof(int));
      if(dataMethod==null)throw new Exception("Event data signature absent");
      for(int index=0;index<row.event_count;index++) {
       var parameter=dataMethod.GetParameters()[1].ParameterType;if(parameter.IsByRef)parameter=parameter.GetElementType();
       var data=Activator.CreateInstance(parameter,true);object[] args={index,data};var ok=dataMethod.Invoke(null,args);if(ok is bool valid&&!valid)continue;data=args[1];
       var actualMesh=data.GetType().GetField("m_Mesh",BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance)?.GetValue(data) as Mesh;if(actualMesh!=mesh)continue;
       draws.Add(new Draw{event_index=index,mesh_subset=Field(data,"m_MeshSubset"),pass_name=Field(data,"m_PassName"),original_shader=Field(data,"m_OriginalShaderName"),real_shader=Field(data,"m_RealShaderName"),component=Field(data,"m_ComponentEntityId"),index_count=Field(data,"m_IndexCount")});
      }
      row.native_draw_mapping_observed=draws.Count>0;
     }catch(Exception e){row.frame_debugger_error=e.ToString();}
     finally{utility.GetMethod("SetEnabled",flags,null,new[]{typeof(bool),typeof(int)},null)?.Invoke(null,new object[]{false,0});}
    } else row.frame_debugger_error="Installed native FrameDebugger locallySupported=false; no draw observation claimed";
    row.matching_mesh_events=draws.Count;row.draws=draws.ToArray();
    RenderPipeline.SubmitRenderRequest(camera,render);
    File.WriteAllText(Path.Combine(root,item.id+".context.json"),JsonUtility.ToJson(new Context{
      id=item.id,bounds_center=bounds.center,bounds_size=bounds.size,radius=radius,
      camera_position=camera.transform.position,camera_rotation=camera.transform.rotation,
      camera_world_to_view=camera.worldToCameraMatrix,camera_projection=camera.projectionMatrix,
      fov=camera.fieldOfView,near=camera.nearClipPlane,far=camera.farClipPlane,exposure_ev=8,
      light_intensity=light.intensity,light_color=light.color,
      anisotropic_filtering=QualitySettings.anisotropicFiltering.ToString(),msaa_samples=QualitySettings.antiAliasing,
      ambient_mode=RenderSettings.ambientMode.ToString(),ambient_intensity=RenderSettings.ambientIntensity,
      ambient_light=RenderSettings.ambientLight,ambient_sky=RenderSettings.ambientSkyColor,
      ambient_equator=RenderSettings.ambientEquatorColor,ambient_ground=RenderSettings.ambientGroundColor,
      skybox_identity=RenderSettings.skybox==null?"null":"runtime-object:"+RenderSettings.skybox.name+";shader="+RenderSettings.skybox.shader.name,
      tonemapping=item.id.EndsWith("-linear")?"None":"Neutral"},true));
    if(RenderSettings.skybox!=null)File.WriteAllText(Path.Combine(root,item.id+".skybox-runtime.json"),EditorJsonUtility.ToJson(RenderSettings.skybox,true));
    var prior=RenderTexture.active;Texture2D raw=null,png=null;
    try {
     RenderTexture.active=target;raw=new Texture2D(512,512,TextureFormat.RGBAHalf,false,true);raw.ReadPixels(new Rect(0,0,512,512),0,0);raw.Apply();var colors=raw.GetPixels();foreach(var c in colors)if(!float.IsFinite(c.r)||!float.IsFinite(c.g)||!float.IsFinite(c.b))throw new Exception("Nonfinite pixels");
     row.max_linear_channel=colors.Max(c=>Math.Max(c.r,Math.Max(c.g,c.b)));row.raw_image=Path.Combine(root,item.id+".rgba16f");File.WriteAllBytes(row.raw_image,raw.GetRawTextureData<byte>().ToArray());
     for(int i=0;i<colors.Length;i++)colors[i]=new Color(Mathf.LinearToGammaSpace(colors[i].r),Mathf.LinearToGammaSpace(colors[i].g),Mathf.LinearToGammaSpace(colors[i].b),1);
     png=new Texture2D(512,512,TextureFormat.RGBA32,false,false);png.SetPixels(colors);png.Apply();row.image=Path.Combine(root,item.id+".png");File.WriteAllBytes(row.image,png.EncodeToPNG());
    }finally{RenderTexture.active=prior;if(raw!=null)UnityEngine.Object.DestroyImmediate(raw);if(png!=null)UnityEngine.Object.DestroyImmediate(png);}
    var afterRender=materials.Select(x=>EditorJsonUtility.ToJson(x,true)).ToArray();
    row.material_before_paths=new string[materials.Length];row.material_after_compile_paths=new string[materials.Length];row.material_after_render_paths=new string[materials.Length];
    for(int i=0;i<materials.Length;i++) {
     row.material_before_paths[i]=Path.Combine(root,item.id+".material-"+i+".before.json");File.WriteAllText(row.material_before_paths[i],before[i]);
     row.material_after_compile_paths[i]=Path.Combine(root,item.id+".material-"+i+".after-compile.json");File.WriteAllText(row.material_after_compile_paths[i],afterCompile[i]);
     row.material_after_render_paths[i]=Path.Combine(root,item.id+".material-"+i+".after-render.json");File.WriteAllText(row.material_after_render_paths[i],afterRender[i]);
     if(before[i]!=afterRender[i])row.source_material_runtime_states_changed++;
     if(sourceBefore[i]!=EditorJsonUtility.ToJson(sourceMaterials[i],true))throw new Exception("Original source material runtime changed despite private instance");
    }
    row.source_material_runtime_states_restored=false;row.original_material_runtime_states_unchanged=true;
    rows.Add(row);File.WriteAllText(Path.Combine(root,"PROGRESS.v1.json"),JsonUtility.ToJson(new Result{gpu=SystemInfo.graphicsDeviceName,backend=SystemInfo.graphicsDeviceType.ToString(),pipeline=pipeline.GetType().FullName,frame_debugger_locally_supported=supported,source_objects_unchanged=true,rows=rows.ToArray()},true));
   }finally{if(target!=null){target.Release();UnityEngine.Object.DestroyImmediate(target);}foreach(var obj in new[]{fixture,co,lo,vo})if(obj!=null)UnityEngine.Object.DestroyImmediate(obj);if(profile!=null)UnityEngine.Object.DestroyImmediate(profile);EditorSceneManager.ClosePreviewScene(scene);}
  }
  File.WriteAllText(request.output,JsonUtility.ToJson(new Result{gpu=SystemInfo.graphicsDeviceName,backend=SystemInfo.graphicsDeviceType.ToString(),pipeline=pipeline.GetType().FullName,frame_debugger_locally_supported=supported,source_objects_unchanged=true,rows=rows.ToArray()},true));
  }finally{foreach(var owned in ownedMeshes)UnityEngine.Object.DestroyImmediate(owned);foreach(var owned in ownedMaterials)UnityEngine.Object.DestroyImmediate(owned);ShaderUtil.allowAsyncCompilation=priorAsync;}
 }
}
