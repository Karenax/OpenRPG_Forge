using System;
using System.IO;
using System.Linq;
using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

public static class OpenRPGSourceNullMaskHdrpStableV4Probe {
 [Serializable] public class Manifest { public string output,prefab_path,prefab_guid,renderer_file_id,node_path,material_path,material_guid,shader_guid; public int slot; }
 [Serializable] public class Row { public string variant,path,sha256; public int camera,width=32,height=32; public Vector4 source_time_before,source_time_after; }
 [Serializable] public class Report { public string unity_version,gpu,pipeline,material_guid,renderer_file_id,mesh_guid,mesh_file_id,shader_guid,shader_file_id,default_name; public int mesh_submeshes,material_slots; public bool diagnostic_camera_postprocess_disabled=true,diagnostic_camera_exposure_disabled=true; public bool source_material_unchanged,source_renderer_unchanged,source_mask_null,full_import=false; public Row[] rows; }
 static Transform Resolve(Transform root,string path) { foreach(var part in path.Split('/').Skip(1))root=root.GetChild(int.Parse(part,CultureInfo.InvariantCulture)); return root; }
 static string Identity(UnityEngine.Object value,string expected) { string guid;long id; if(!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(value,out guid,out id)||guid!=expected)throw new Exception("Original identity differs");return id.ToString(CultureInfo.InvariantCulture); }
 static string Hash(byte[] bytes) { using(var h=SHA256.Create())return BitConverter.ToString(h.ComputeHash(bytes)).Replace("-","").ToLowerInvariant(); }
 public static void Export() {
  var args=Environment.GetCommandLineArgs(); int arg=Array.IndexOf(args,"--openrpg-manifest"); if(arg<0)throw new Exception("Missing manifest");
  var request=JsonUtility.FromJson<Manifest>(File.ReadAllText(args[arg+1])); if(File.Exists(request.output))throw new Exception("Preserve earlier measurement");
  if(PlayerSettings.colorSpace!=ColorSpace.Linear||SystemInfo.graphicsDeviceType==GraphicsDeviceType.Null)throw new Exception("Native Linear GPU required");
  var pipeline=GraphicsSettings.currentRenderPipeline as HDRenderPipelineAsset; if(pipeline==null)throw new Exception("HDRP selected at startup required");
  var prepare=typeof(RenderPipelineManager).GetMethod("TryPrepareRenderPipeline",BindingFlags.Static|BindingFlags.NonPublic,null,new[]{typeof(RenderPipelineAsset)},null);
  if(prepare==null||!(bool)prepare.Invoke(null,new object[]{pipeline}))throw new Exception("Native HDRP initialization refused");
  var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(request.prefab_path); Identity(prefab,request.prefab_guid);
  var original=Resolve(prefab.transform,request.node_path).GetComponent<MeshRenderer>(); if(Identity(original,request.prefab_guid)!=request.renderer_file_id)throw new Exception("Wrong source renderer");
  var mesh=original.GetComponent<MeshFilter>().sharedMesh; string meshGuid;long meshId;
  if(!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(mesh,out meshGuid,out meshId))throw new Exception("Original mesh identity unavailable");
  var material=AssetDatabase.LoadAssetAtPath<Material>(request.material_path); Identity(material,request.material_guid);
  if(original.sharedMaterials[request.slot]!=material||material.GetTexture("_Mask")!=null)throw new Exception("Original slot or null binding differs");
  string shaderId=Identity(material.shader,request.shader_guid); int property=material.shader.FindPropertyIndex("_Mask");
  if(property<0||material.shader.GetPropertyTextureDefaultName(property)!="white")throw new Exception("Original declared default differs");
  string materialBefore=EditorJsonUtility.ToJson(material,true),rendererBefore=EditorJsonUtility.ToJson(original,true);
  var fixture=new GameObject("Original mesh renderer measurement only"); fixture.hideFlags=HideFlags.HideAndDontSave;
  fixture.transform.SetPositionAndRotation(original.transform.position,original.transform.rotation);fixture.transform.localScale=original.transform.lossyScale;
  fixture.AddComponent<MeshFilter>().sharedMesh=mesh; var renderer=fixture.AddComponent<MeshRenderer>();
  var originalSlots=original.sharedMaterials; renderer.sharedMaterials=originalSlots; renderer.shadowCastingMode=original.shadowCastingMode; renderer.receiveShadows=original.receiveShadows;
  var cameraObject=new GameObject("Native default measurement camera");cameraObject.hideFlags=HideFlags.HideAndDontSave;
  var camera=cameraObject.AddComponent<Camera>();camera.enabled=false;camera.orthographic=true;camera.aspect=1;camera.nearClipPlane=.01f;
  var extra=cameraObject.AddComponent<HDAdditionalCameraData>();extra.clearColorMode=HDAdditionalCameraData.ClearColorMode.Color;extra.backgroundColorHDR=new Color(.03125f,.0625f,.125f,1);
  // Diagnostic camera only; retain original materials and shader behavior.
  extra.customRenderingSettings=true;extra.antialiasing=HDAdditionalCameraData.AntialiasingMode.None;
  foreach(var field in new[]{FrameSettingsField.Postprocess,FrameSettingsField.ExposureControl}) {
   extra.renderingPathCustomFrameSettings.SetEnabled(field,false);
   extra.renderingPathCustomFrameSettingsOverrideMask.mask[(uint)field]=true;
  }

  var lightObject=new GameObject("Measurement directional light");lightObject.hideFlags=HideFlags.HideAndDontSave;
  var light=lightObject.AddComponent<Light>();light.type=LightType.Directional;light.color=Color.white;light.intensity=10000;lightObject.AddComponent<HDAdditionalLightData>();
  var target=new RenderTexture(32,32,24,RenderTextureFormat.ARGBHalf,RenderTextureReadWrite.Linear);target.Create();
  var rows=new System.Collections.Generic.List<Row>();var center=renderer.bounds.center;float radius=Math.Max(renderer.bounds.extents.magnitude,.01f);
  var directions=new[]{new Vector3(0,0,-1),new Vector3(0,0,1),new Vector3(-1,.4f,-1).normalized};
  try {
   for(int view=0;view<directions.Length;view++) {
    camera.transform.position=center+directions[view]*radius*3;camera.transform.LookAt(center);camera.orthographicSize=radius*1.1f;camera.farClipPlane=radius*10;
    light.transform.rotation=camera.transform.rotation;
    foreach(string variant in new[]{"original_null","explicit_white_control","explicit_black_control","original_null_repeat"}) {
     var clone=new Material(material); clone.hideFlags=HideFlags.HideAndDontSave;
     try {
      if(variant=="explicit_white_control")clone.SetTexture("_Mask",Texture2D.whiteTexture);
      if(variant=="explicit_black_control")clone.SetTexture("_Mask",Texture2D.blackTexture);
      var slots=(Material[])originalSlots.Clone();slots[request.slot]=clone;renderer.sharedMaterials=slots;
      var renderRequest=new RenderPipeline.StandardRequest{destination=target};
      if(!RenderPipeline.SupportsRenderRequest(camera,renderRequest))throw new Exception("Native HDRP standard render request unsupported");
      Vector4 timeBefore=Shader.GetGlobalVector("_Time");RenderPipeline.SubmitRenderRequest(camera,renderRequest);Vector4 timeAfter=Shader.GetGlobalVector("_Time");
      var previous=RenderTexture.active;var pixels=new Texture2D(32,32,TextureFormat.RGBAHalf,false,true);byte[] raw;
      try {RenderTexture.active=target;pixels.ReadPixels(new Rect(0,0,32,32),0,0);pixels.Apply();raw=pixels.GetRawTextureData<byte>().ToArray();}
      finally {RenderTexture.active=previous;UnityEngine.Object.DestroyImmediate(pixels);}
      string path=Path.Combine(Path.GetDirectoryName(request.output),"native-pixels",view+"-"+variant+".rgba.f16");Directory.CreateDirectory(Path.GetDirectoryName(path));if(File.Exists(path))throw new Exception("Preserve prior pixels");File.WriteAllBytes(path,raw);
      rows.Add(new Row{variant=variant,camera=view,path=path,sha256=Hash(raw),source_time_before=timeBefore,source_time_after=timeAfter});
     }finally {UnityEngine.Object.DestroyImmediate(clone);}
    }
   }
   if(materialBefore!=EditorJsonUtility.ToJson(material,true)||rendererBefore!=EditorJsonUtility.ToJson(original,true))throw new Exception("Original source material or renderer changed");
   var report=new Report{unity_version=Application.unityVersion,gpu=SystemInfo.graphicsDeviceType.ToString(),pipeline=RenderPipelineManager.currentPipeline.GetType().FullName,
    material_guid=request.material_guid,renderer_file_id=request.renderer_file_id,mesh_guid=meshGuid,mesh_file_id=meshId.ToString(CultureInfo.InvariantCulture),shader_guid=request.shader_guid,shader_file_id=shaderId,
    default_name="white",mesh_submeshes=mesh.subMeshCount,material_slots=originalSlots.Length,source_mask_null=true,source_material_unchanged=true,source_renderer_unchanged=true,rows=rows.ToArray()};
   File.WriteAllText(request.output,JsonUtility.ToJson(report,true));
  }finally {target.Release();UnityEngine.Object.DestroyImmediate(target);UnityEngine.Object.DestroyImmediate(lightObject);UnityEngine.Object.DestroyImmediate(cameraObject);UnityEngine.Object.DestroyImmediate(fixture);}
 }
}
