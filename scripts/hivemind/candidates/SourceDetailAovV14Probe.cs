using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Globalization;
using System.Security.Cryptography;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.Rendering.HighDefinition.Attributes;
using UnityEngine.Experimental.Rendering;
using UnityEngine.SceneManagement;

// Native diagnostic only: no authored mesh, material or prefab is saved.
public static class OpenRPGSourceDetailAovV14Probe {
 [Serializable] public class Manifest { public string output,prefab_path,prefab_guid,renderer_file_id,node_path,material_path,material_guid,shader_guid;public int slot; }
 [Serializable] public class Row { public string variant,path,sha256,aov_property,graphics_format;public int camera,debug_surface_mode,width=128,height=128;public float diagnostic_clone_roughness,diagnostic_clone_ao_intensity,effective_hd_camera_time;public bool native_aov_callback; }
 [Serializable] public class Report { public string unity_version,gpu,pipeline,material_guid,renderer_file_id,mesh_guid,mesh_file_id,shader_guid,shader_file_id;public bool source_material_unchanged,source_renderer_unchanged,active_editor_scene_preserved,editor_play_options_restored,native_default_rg_qualified=false,full_import=false;public int targeted_submesh_indices,mesh_submeshes,material_slots;public Row[] rows; }
 static Manifest request;static Material source,clone;static MeshRenderer original,renderer;
 static GameObject fixture,cameraObject;static Camera camera;static HDAdditionalCameraData extra;
 static RenderTexture target;static RTHandle aov;static Scene scene,activeBefore;
 static bool optionsEnabled,optionsCaptured;static EnterPlayModeOptions options;static string sourceBefore,rendererBefore;
 static string meshGuid,meshId,shaderId;static int index,indexCount;static bool waiting,finished,captured;
 static Vector3 center;static float radius;static double started;
 static readonly List<Row> rows=new List<Row>();
 static readonly string[] variants={"original_null_warmup_0","original_null_warmup_1","original_null","explicit_white_control","explicit_black_control","original_null_repeat"};
 static readonly Vector3[] directions={new Vector3(0,0,-1),new Vector3(0,0,1),new Vector3(-1,.4f,-1).normalized};
 static MethodInfo getCamera;static PropertyInfo clock,previousClock;static FieldInfo animated;static object history;
 static string Hash(byte[] bytes) {using(var h=SHA256.Create())return BitConverter.ToString(h.ComputeHash(bytes)).Replace("-","").ToLowerInvariant();}
 static string Identity(UnityEngine.Object value,string expected) {string guid;long id;if(!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(value,out guid,out id)||guid!=expected)throw new Exception("Original identity differs");return id.ToString(CultureInfo.InvariantCulture);}
 static Transform Resolve(Transform root,string path) {foreach(var p in path.Split('/').Skip(1))root=root.GetChild(int.Parse(p,CultureInfo.InvariantCulture));return root;}
 public static void Export() {
  try {
   var args=Environment.GetCommandLineArgs();int arg=Array.IndexOf(args,"--openrpg-manifest");if(arg<0)throw new Exception("Manifest absent");
   request=JsonUtility.FromJson<Manifest>(File.ReadAllText(args[arg+1]));if(File.Exists(request.output))throw new Exception("Preserve existing output");
   if(EditorApplication.isPlaying||PlayerSettings.colorSpace!=ColorSpace.Linear||SystemInfo.graphicsDeviceType!=GraphicsDeviceType.Direct3D11)throw new Exception("Isolated Linear D3D11 editor required");
   var pipeline=GraphicsSettings.currentRenderPipeline as HDRenderPipelineAsset;if(pipeline==null)throw new Exception("HDRP required");
   var prepare=typeof(RenderPipelineManager).GetMethod("TryPrepareRenderPipeline",BindingFlags.Static|BindingFlags.NonPublic,null,new[]{typeof(RenderPipelineAsset)},null);
   if(prepare==null||!(bool)prepare.Invoke(null,new object[]{pipeline}))throw new Exception("Native pipeline initialization failed");
   var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(request.prefab_path);Identity(prefab,request.prefab_guid);
   original=Resolve(prefab.transform,request.node_path).GetComponent<MeshRenderer>();if(Identity(original,request.prefab_guid)!=request.renderer_file_id)throw new Exception("Renderer differs");
   var mesh=original.GetComponent<MeshFilter>().sharedMesh;long id;if(!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(mesh,out meshGuid,out id))throw new Exception("Mesh identity absent");meshId=id.ToString(CultureInfo.InvariantCulture);
   source=AssetDatabase.LoadAssetAtPath<Material>(request.material_path);Identity(source,request.material_guid);shaderId=Identity(source.shader,request.shader_guid);
   if(original.sharedMaterials[request.slot]!=source||source.GetTexture("_Mask")!=null)throw new Exception("Original null binding differs");
   int property=source.shader.FindPropertyIndex("_Mask");if(property<0||source.shader.GetPropertyTextureDefaultName(property)!="white")throw new Exception("Declared default differs");
   if(mesh.subMeshCount!=original.sharedMaterials.Length)throw new Exception("Slot mapping not one-to-one");
   sourceBefore=EditorJsonUtility.ToJson(source,true);rendererBefore=EditorJsonUtility.ToJson(original,true);
   activeBefore=SceneManager.GetActiveScene();
   if(string.IsNullOrEmpty(activeBefore.path)) {
    if(activeBefore.rootCount!=0)throw new Exception("Private startup scene is not empty; preserve it");
    const string startup="Assets/OpenRPGDiagnosticEmptyStartupV14.unity";
    if(File.Exists(startup)||!EditorSceneManager.SaveScene(activeBefore,startup))throw new Exception("Private empty startup preservation refused");
   }
   scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);
   SceneManager.SetActiveScene(activeBefore);
   fixture=new GameObject("Original Detail native AOV measurement");fixture.hideFlags=HideFlags.DontSave;
   fixture.transform.SetPositionAndRotation(original.transform.position,original.transform.rotation);fixture.transform.localScale=original.transform.lossyScale;
   fixture.AddComponent<MeshFilter>().sharedMesh=mesh;renderer=fixture.AddComponent<MeshRenderer>();renderer.sharedMaterials=original.sharedMaterials;renderer.shadowCastingMode=original.shadowCastingMode;renderer.receiveShadows=original.receiveShadows;
   SceneManager.MoveGameObjectToScene(fixture,scene);
   var indices=mesh.GetIndices(request.slot,true);indexCount=indices.Length;if(indexCount==0)throw new Exception("Selected submesh empty");var vertices=mesh.vertices;
   var bounds=new Bounds(fixture.transform.TransformPoint(vertices[indices[0]]),Vector3.zero);foreach(int vertex in indices)bounds.Encapsulate(fixture.transform.TransformPoint(vertices[vertex]));center=bounds.center;radius=Math.Max(bounds.extents.magnitude,.01f);
   cameraObject=new GameObject("Native AOV camera");cameraObject.hideFlags=HideFlags.DontSave;camera=cameraObject.AddComponent<Camera>();camera.enabled=false;camera.orthographic=true;camera.aspect=1;camera.nearClipPlane=.01f;
   extra=cameraObject.AddComponent<HDAdditionalCameraData>();extra.clearColorMode=HDAdditionalCameraData.ClearColorMode.Color;extra.backgroundColorHDR=new Color(.03125f,.0625f,.125f,1);extra.customRenderingSettings=true;extra.antialiasing=HDAdditionalCameraData.AntialiasingMode.None;
   foreach(var field in new[]{FrameSettingsField.Postprocess,FrameSettingsField.ExposureControl}) {extra.renderingPathCustomFrameSettings.SetEnabled(field,false);extra.renderingPathCustomFrameSettingsOverrideMask.mask[(uint)field]=true;}
   SceneManager.MoveGameObjectToScene(cameraObject,scene);camera.scene=scene;
   target=new RenderTexture(128,128,24,RenderTextureFormat.ARGBHalf,RenderTextureReadWrite.Linear);target.Create();camera.targetTexture=target;
   aov=RTHandles.Alloc(128,128,colorFormat:GraphicsFormat.R16G16B16A16_SFloat,name:"Detail native AOV float output");
   var historyType=typeof(HDCamera).GetNestedType("HistoryChannel",BindingFlags.NonPublic|BindingFlags.Public);history=Enum.Parse(historyType,"RenderLoopHistory");
   getCamera=typeof(HDCamera).GetMethod("GetOrCreate",BindingFlags.Static|BindingFlags.NonPublic,null,new[]{typeof(Camera),typeof(int),historyType},null);
   clock=typeof(HDCamera).GetProperty("animateMaterialsTime",BindingFlags.Instance|BindingFlags.NonPublic);previousClock=typeof(HDCamera).GetProperty("animateMaterialsTimeLast",BindingFlags.Instance|BindingFlags.NonPublic);animated=typeof(HDCamera).GetField("animateMaterials",BindingFlags.Instance|BindingFlags.NonPublic);
   if(getCamera==null||clock==null||previousClock==null||animated==null)throw new Exception("Native clock API absent");
   optionsEnabled=EditorSettings.enterPlayModeOptionsEnabled;options=EditorSettings.enterPlayModeOptions;optionsCaptured=true;
   EditorSettings.enterPlayModeOptionsEnabled=true;EditorSettings.enterPlayModeOptions=EnterPlayModeOptions.DisableDomainReload|EnterPlayModeOptions.DisableSceneReload;
   EditorApplication.playModeStateChanged+=Mode;EditorApplication.update+=Tick;RenderPipelineManager.beginCameraRendering+=BeginCamera;
   started=EditorApplication.timeSinceStartup;Configure();EditorApplication.EnterPlaymode();
  } catch(Exception error) {Fail(error);}
 }
 static void Mode(PlayModeStateChange state) {
  if(state==PlayModeStateChange.EnteredPlayMode)camera.enabled=true;
  if(state==PlayModeStateChange.EnteredEditMode&&finished)FinalizeReport();
 }
 static void BeginCamera(ScriptableRenderContext context,Camera value) {
  if(value!=camera)return;
  var hd=getCamera.Invoke(null,new object[]{camera,0,history});clock.SetValue(hd,0f);previousClock.SetValue(hd,0f);
 }
 static void Configure() {
  captured=false;int view=index/6;string variant=variants[index%6];if(clone!=null)UnityEngine.Object.DestroyImmediate(clone);
  clone=new Material(source);clone.hideFlags=HideFlags.HideAndDontSave;clone.SetFloat("_Roughness",.125f);clone.SetFloat("_AOIntensity",.25f);
  if(clone.GetFloat("_Roughness")!=.125f||clone.GetFloat("_AOIntensity")!=.25f)throw new Exception("Diagnostic gains differ");
  if(variant=="explicit_white_control")clone.SetTexture("_Mask",Texture2D.whiteTexture);if(variant=="explicit_black_control")clone.SetTexture("_Mask",Texture2D.blackTexture);
  var slots=(Material[])original.sharedMaterials.Clone();slots[request.slot]=clone;renderer.sharedMaterials=slots;
  camera.transform.position=center+directions[view%3]*radius*3;camera.transform.LookAt(center);camera.orthographicSize=radius*1.1f;camera.farClipPlane=radius*10;
  var property=view<3?MaterialSharedProperty.Smoothness:MaterialSharedProperty.AmbientOcclusion;
  var native=AOVRequest.NewDefault();native.SetFullscreenOutput(property);native.SetOverrideRenderFormat(true);
  extra.SetAOVRequests(new AOVRequestBuilder().Add(native,buffer=>aov,null,new[]{AOVBuffers.Color},(cmd,textures,properties)=> {
   if(waiting||finished)return;waiting=true;
   if(textures.Count!=1||textures[0].rt.graphicsFormat!=GraphicsFormat.R16G16B16A16_SFloat) {Fail(new Exception("Native AOV format differs"));return;}
   var hd=(HDCamera)getCamera.Invoke(null,new object[]{camera,0,history});float effective=(bool)animated.GetValue(hd)?(float)clock.GetValue(hd):hd.time;
   if(effective!=0) {Fail(new Exception("Native AOV clock differs"));return;}
   cmd.RequestAsyncReadback(textures[0].rt,0,readback=> {
    try {
     if(readback.hasError)throw new Exception("Native AOV readback failed");byte[] bytes=readback.GetData<byte>().ToArray();if(bytes.Length!=128*128*8)throw new Exception("Native AOV size differs");
     string path=Path.Combine(Path.GetDirectoryName(request.output),"native-pixels",view+"-"+variant+".rgba.f16");Directory.CreateDirectory(Path.GetDirectoryName(path));if(File.Exists(path))throw new Exception("Preserve previous capture");File.WriteAllBytes(path,bytes);
     rows.Add(new Row{camera=view,variant=variant,path=path,sha256=Hash(bytes),aov_property=property.ToString(),graphics_format=GraphicsFormat.R16G16B16A16_SFloat.ToString(),debug_surface_mode=view<3?1005:1006,diagnostic_clone_roughness=.125f,diagnostic_clone_ao_intensity=.25f,effective_hd_camera_time=effective,native_aov_callback=true});index++;captured=true;
    }catch(Exception error){Fail(error);}
   });
  }).Build());waiting=false;
 }
 static void Tick() {
  if(finished)return;
  try {
   if(EditorApplication.timeSinceStartup-started>600)throw new Exception("Native AOV loop timed out; keep captures");
   if(index==36) {finished=true;camera.enabled=false;extra.SetAOVRequests(null);EditorApplication.ExitPlaymode();return;}
   if(captured)Configure();
  }catch(Exception error){Fail(error);}
 }
 static void Restore() {
  EditorApplication.update-=Tick;EditorApplication.playModeStateChanged-=Mode;RenderPipelineManager.beginCameraRendering-=BeginCamera;
  if(optionsCaptured){EditorSettings.enterPlayModeOptionsEnabled=optionsEnabled;EditorSettings.enterPlayModeOptions=options;}
  if(extra!=null)extra.SetAOVRequests(null);if(camera!=null){camera.enabled=false;camera.targetTexture=null;}
  if(aov!=null)aov.Release();if(target!=null){target.Release();UnityEngine.Object.DestroyImmediate(target);}
  if(clone!=null)UnityEngine.Object.DestroyImmediate(clone);if(fixture!=null)UnityEngine.Object.DestroyImmediate(fixture);if(cameraObject!=null)UnityEngine.Object.DestroyImmediate(cameraObject);
  if(scene.IsValid()&&scene.isLoaded)EditorSceneManager.CloseScene(scene,true);
 }
 static void FinalizeReport() {
  try {
   if(sourceBefore!=EditorJsonUtility.ToJson(source,true)||rendererBefore!=EditorJsonUtility.ToJson(original,true))throw new Exception("Source material/renderer changed");
   bool active=SceneManager.GetActiveScene()==activeBefore;Restore();if(!active)throw new Exception("Original active scene changed");
   var report=new Report{unity_version=Application.unityVersion,gpu=SystemInfo.graphicsDeviceType.ToString(),pipeline=RenderPipelineManager.currentPipeline.GetType().FullName,material_guid=request.material_guid,renderer_file_id=request.renderer_file_id,mesh_guid=meshGuid,mesh_file_id=meshId,shader_guid=request.shader_guid,shader_file_id=shaderId,source_material_unchanged=true,source_renderer_unchanged=true,active_editor_scene_preserved=active,editor_play_options_restored=EditorSettings.enterPlayModeOptionsEnabled==optionsEnabled&&EditorSettings.enterPlayModeOptions==options,targeted_submesh_indices=indexCount,mesh_submeshes=original.GetComponent<MeshFilter>().sharedMesh.subMeshCount,material_slots=original.sharedMaterials.Length,rows=rows.ToArray()};
   File.WriteAllText(request.output,JsonUtility.ToJson(report,true));EditorApplication.Exit(0);
  }catch(Exception error){Fail(error);}
 }
 static void Fail(Exception error) {
  finished=true;Debug.LogException(error);
  try {if(request!=null)File.WriteAllText(request.output+".failure.txt",error.ToString());Restore();}catch(Exception restoreError){Debug.LogException(restoreError);}
  EditorApplication.Exit(2);
 }
}
