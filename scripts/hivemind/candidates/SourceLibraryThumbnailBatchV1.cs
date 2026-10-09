using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

public static class OpenRPGSourceLibraryThumbnailBatchV1 {
 [Serializable] public class Request { public string id,prefab_path,prefab_guid,output; }
 [Serializable] public class TextureBinding { public string material,property,identity; public int width,height; }
 [Serializable] public class CompiledPass { public string material,name; public int index; public bool compiled; }
 [Serializable] public class RendererInfo { public string type,mesh_guid,mesh_file_id; public string[] materials,shaders; public int vertices,submeshes; }
 [Serializable] public class Row { public string path,sha256; public int view,width=512,height=512,clipped_rgb_pixels;public float linear_max_channel; public Vector3 camera_position,target; }
 [Serializable] public class Report { public string id,prefab_guid,unity_version,gpu,pipeline; public bool source_object_unchanged,preview_scene_isolated=true,lod0_for_thumbnail_only=true,conversion_qualified=false,forge_editor_qualified=false; public float actual_directional_light_intensity;public float presentation_fixed_ev=8;public string presentation_tonemapping="Neutral";public bool presentation_only=true;public int warmup_render_requests_per_view=3; public bool camera_orthographic=false;public float camera_field_of_view=30;public bool synchronous_source_passes=true; public TextureBinding[] textures; public CompiledPass[] compiled_passes; public RendererInfo[] renderers; public Row[] images; }
 static string Hash(byte[] bytes) { using(var h=SHA256.Create())return BitConverter.ToString(h.ComputeHash(bytes)).Replace("-","").ToLowerInvariant(); }
 static string Identity(UnityEngine.Object obj) { string guid;long id; if(obj==null||!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(obj,out guid,out id))throw new Exception("Source identity absent"); return guid+":"+id; }
 [Serializable] public class BatchRequest { public Request[] cases; public string output; }
 [Serializable] public class Outcome { public string id,receipt,error; public bool captured; }
 [Serializable] public class BatchReport { public Outcome[] cases; public bool full_import=false; }
 public static void Export() {
  var args=Environment.GetCommandLineArgs();int arg=Array.IndexOf(args,"--openrpg-manifest");if(arg<0)throw new Exception("Manifest absent");
  var batch=JsonUtility.FromJson<BatchRequest>(File.ReadAllText(args[arg+1]));if(File.Exists(batch.output))throw new Exception("Preserve previous batch");
  var outcomes=new System.Collections.Generic.List<Outcome>();
  foreach(var request in batch.cases) {
   bool priorAsync=ShaderUtil.allowAsyncCompilation;
   try { Capture(request);outcomes.Add(new Outcome{id=request.id,receipt=request.output,captured=true}); }
   catch(Exception exc) { outcomes.Add(new Outcome{id=request.id,receipt=request.output,error=exc.ToString(),captured=false}); }
   finally { ShaderUtil.allowAsyncCompilation=priorAsync; }
   File.WriteAllText(batch.output+".progress",JsonUtility.ToJson(new BatchReport{cases=outcomes.ToArray()},true));
  }
  File.WriteAllText(batch.output,JsonUtility.ToJson(new BatchReport{cases=outcomes.ToArray()},true));
 }
 static void Capture(Request request) {
  if(File.Exists(request.output))throw new Exception("Preserve previous capture");
  if(PlayerSettings.colorSpace!=ColorSpace.Linear||SystemInfo.graphicsDeviceType==GraphicsDeviceType.Null)throw new Exception("Linear native GPU required");
  var pipeline=GraphicsSettings.currentRenderPipeline as HDRenderPipelineAsset;if(pipeline==null)throw new Exception("HDRP required");
  var prepare=typeof(RenderPipelineManager).GetMethod("TryPrepareRenderPipeline",BindingFlags.Static|BindingFlags.NonPublic,null,new[]{typeof(RenderPipelineAsset)},null);
  if(prepare==null||!(bool)prepare.Invoke(null,new object[]{pipeline}))throw new Exception("HDRP initialization failed");
  var source=AssetDatabase.LoadAssetAtPath<GameObject>(request.prefab_path);if(source==null||AssetDatabase.AssetPathToGUID(request.prefab_path)!=request.prefab_guid)throw new Exception("Wrong source prefab");
  string sourceBefore=EditorJsonUtility.ToJson(source,true);
  bool priorAsync=ShaderUtil.allowAsyncCompilation;ShaderUtil.allowAsyncCompilation=false;var textureInfos=new System.Collections.Generic.List<TextureBinding>();var passInfos=new System.Collections.Generic.List<CompiledPass>();
  var infos=source.GetComponentsInChildren<Renderer>(true).Select(r=>{
   var filter=r.GetComponent<MeshFilter>();var mesh=filter!=null?filter.sharedMesh:(r as SkinnedMeshRenderer)?.sharedMesh;
   if(mesh==null||r.sharedMaterials.Any(m=>m==null||m.shader==null||ShaderUtil.ShaderHasError(m.shader)||m.shader.name=="Hidden/InternalErrorShader"))throw new Exception("Source mesh/material/shader unresolved");
   string guid;long id;if(!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(mesh,out guid,out id))throw new Exception("Mesh identity absent");
   foreach(var material in r.sharedMaterials.Distinct()) {
    foreach(var property in material.GetTexturePropertyNames()) {
     var texture=material.GetTexture(property);if(texture==null)continue;
     textureInfos.Add(new TextureBinding{material=Identity(material),property=property,identity=Identity(texture),width=texture.width,height=texture.height});
    }
    for(int pass=0;pass<material.passCount;pass++) {
     string passName=material.GetPassName(pass);
     if(!material.GetShaderPassEnabled(passName)||passName.IndexOf("RayTracing",StringComparison.OrdinalIgnoreCase)>=0)continue;
     ShaderUtil.CompilePass(material,pass,true);
     if(!ShaderUtil.IsPassCompiled(material,pass)||ShaderUtil.ShaderHasError(material.shader))throw new Exception("Original source pass did not compile: "+passName);
     passInfos.Add(new CompiledPass{material=Identity(material),index=pass,name=passName,compiled=true});
    }
   }
   return new RendererInfo{type=r.GetType().FullName,mesh_guid=guid,mesh_file_id=id.ToString(),vertices=mesh.vertexCount,submeshes=mesh.subMeshCount,materials=r.sharedMaterials.Select(Identity).ToArray(),shaders=r.sharedMaterials.Select(m=>Identity(m.shader)).ToArray()};
  }).ToArray();if(infos.Length==0)throw new Exception("No source renderer");
  var preview=EditorSceneManager.NewPreviewScene();GameObject fixture=null,cameraObject=null,lightObject=null,volumeObject=null;VolumeProfile presentationProfile=null;RenderTexture target=null;
  try {
   fixture=UnityEngine.Object.Instantiate(source);fixture.hideFlags=HideFlags.HideAndDontSave;UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(fixture,preview);
   foreach(var lod in fixture.GetComponentsInChildren<LODGroup>(true))lod.ForceLOD(0);
   var visible=fixture.GetComponentsInChildren<Renderer>().Where(r=>r.enabled&&!r.forceRenderingOff).ToArray();if(visible.Length==0)throw new Exception("No visible original renderer");
   var bounds=visible[0].bounds;foreach(var r in visible.Skip(1))bounds.Encapsulate(r.bounds);float radius=bounds.extents.magnitude;if(!float.IsFinite(radius)||radius<=0)throw new Exception("Invalid original bounds");
   cameraObject=new GameObject("Source thumbnail camera");cameraObject.hideFlags=HideFlags.HideAndDontSave;var camera=cameraObject.AddComponent<Camera>();camera.enabled=false;camera.orthographic=false;camera.fieldOfView=30;camera.aspect=1;camera.nearClipPlane=.01f;camera.farClipPlane=Math.Max(10,radius*12);camera.orthographicSize=radius*1.1f;
   var extra=cameraObject.AddComponent<HDAdditionalCameraData>();extra.clearColorMode=HDAdditionalCameraData.ClearColorMode.Color;extra.backgroundColorHDR=new Color(.025f,.035f,.05f,1);extra.customRenderingSettings=true;extra.antialiasing=HDAdditionalCameraData.AntialiasingMode.None;
   foreach(var field in new[]{FrameSettingsField.Postprocess,FrameSettingsField.ExposureControl}) {extra.renderingPathCustomFrameSettings.SetEnabled(field,true);extra.renderingPathCustomFrameSettingsOverrideMask.mask[(uint)field]=true;}
   extra.volumeLayerMask=1<<31;extra.volumeAnchorOverride=camera.transform;extra.hasPersistentHistory=true;
   volumeObject=new GameObject("Native thumbnail exposure");volumeObject.hideFlags=HideFlags.HideAndDontSave;volumeObject.layer=31;UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(volumeObject,preview);
   var volume=volumeObject.AddComponent<Volume>();volume.isGlobal=true;volume.priority=float.MaxValue;volume.weight=1;presentationProfile=ScriptableObject.CreateInstance<VolumeProfile>();presentationProfile.hideFlags=HideFlags.HideAndDontSave;volume.sharedProfile=presentationProfile;
   var exposure=presentationProfile.Add<Exposure>(true);exposure.mode.Override(ExposureMode.Fixed);exposure.fixedExposure.Override(8);exposure.compensation.Override(0);
   var tone=presentationProfile.Add<Tonemapping>(true);tone.mode.Override(TonemappingMode.Neutral);
   lightObject=new GameObject("Thumbnail directional light");lightObject.hideFlags=HideFlags.HideAndDontSave;var light=lightObject.AddComponent<Light>();light.type=LightType.Directional;light.color=Color.white;light.intensity=1;lightObject.AddComponent<HDAdditionalLightData>();light.intensity=1;
   UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(cameraObject,preview);UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(lightObject,preview);camera.scene=preview;camera.overrideSceneCullingMask=EditorSceneManager.GetSceneCullingMask(preview);
   target=new RenderTexture(512,512,24,RenderTextureFormat.ARGBHalf,RenderTextureReadWrite.Linear);target.Create();var rows=new System.Collections.Generic.List<Row>();var directions=new[]{new Vector3(1,.65f,-1).normalized,new Vector3(-1,.5f,1).normalized};
   for(int view=0;view<directions.Length;view++) {
    camera.transform.position=bounds.center+directions[view]*radius*4;camera.transform.LookAt(bounds.center);light.transform.rotation=camera.transform.rotation;
    var render=new RenderPipeline.StandardRequest{destination=target};if(!RenderPipeline.SupportsRenderRequest(camera,render))throw new Exception("HDRP request unsupported");for(int warm=0;warm<3;warm++)RenderPipeline.SubmitRenderRequest(camera,render);
    var prior=RenderTexture.active;Texture2D raw=null,png=null;
    try {
     RenderTexture.active=target;raw=new Texture2D(512,512,TextureFormat.RGBAHalf,false,true);raw.ReadPixels(new Rect(0,0,512,512),0,0);raw.Apply();var colors=raw.GetPixels();
     float linearMax=colors.Max(c=>Math.Max(c.r,Math.Max(c.g,c.b)));int clipped=colors.Count(c=>c.r>=1&&c.g>=1&&c.b>=1);
     for(int i=0;i<colors.Length;i++){if(!float.IsFinite(colors[i].r)||!float.IsFinite(colors[i].g)||!float.IsFinite(colors[i].b))throw new Exception("Non-finite original pixels");colors[i]=new Color(Mathf.LinearToGammaSpace(colors[i].r),Mathf.LinearToGammaSpace(colors[i].g),Mathf.LinearToGammaSpace(colors[i].b),1);}
     png=new Texture2D(512,512,TextureFormat.RGBA32,false,false);png.SetPixels(colors);png.Apply();byte[] bytes=png.EncodeToPNG();string path=Path.Combine(Path.GetDirectoryName(request.output),"images",view+".png");Directory.CreateDirectory(Path.GetDirectoryName(path));if(File.Exists(path))throw new Exception("Preserve prior image");File.WriteAllBytes(path,bytes);
     rows.Add(new Row{path=path,sha256=Hash(bytes),view=view,linear_max_channel=linearMax,clipped_rgb_pixels=clipped,camera_position=camera.transform.position,target=bounds.center});
    }finally {RenderTexture.active=prior;if(raw!=null)UnityEngine.Object.DestroyImmediate(raw);if(png!=null)UnityEngine.Object.DestroyImmediate(png);}
   }
   if(sourceBefore!=EditorJsonUtility.ToJson(source,true))throw new Exception("Source prefab changed");
   File.WriteAllText(request.output,JsonUtility.ToJson(new Report{id=request.id,prefab_guid=request.prefab_guid,unity_version=Application.unityVersion,gpu=SystemInfo.graphicsDeviceType.ToString(),pipeline=pipeline.GetType().FullName,source_object_unchanged=true,actual_directional_light_intensity=light.intensity,textures=textureInfos.ToArray(),compiled_passes=passInfos.ToArray(),renderers=infos,images=rows.ToArray()},true));
  }finally {ShaderUtil.allowAsyncCompilation=priorAsync;if(target!=null){target.Release();UnityEngine.Object.DestroyImmediate(target);}if(lightObject!=null)UnityEngine.Object.DestroyImmediate(lightObject);if(cameraObject!=null)UnityEngine.Object.DestroyImmediate(cameraObject);if(volumeObject!=null)UnityEngine.Object.DestroyImmediate(volumeObject);if(presentationProfile!=null)UnityEngine.Object.DestroyImmediate(presentationProfile);if(fixture!=null)UnityEngine.Object.DestroyImmediate(fixture);EditorSceneManager.ClosePreviewScene(preview);}
 }
}
