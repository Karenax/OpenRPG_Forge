using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;

// Measurement fixtures only: retain the real shader and bound texture on a
// temporary material clone, retaining the actual source texture and sampler.
public static class OpenRPGMedievalTorchTintVariantV128 {
 [Serializable] public class Input {public string asset_path,material_guid,variant_source_sha256;public Color variant_tint;public Color32[] baked_colors;}
 [Serializable] public class Manifest {public string output;public Input[] cases;}
 [Serializable] public class Row {public string pixels_path,pixels_sha256;public int width=16,height=16;public Color32 baked_color;public Color background,result;public bool alpha_channel_preserved;}
 [Serializable] public class Case {public string texture_guid,texture_file_id,graphics_format,texture_format,filter_mode,wrap_mode_u,wrap_mode_v;public int texture_width,texture_height,mipmap_count,anisotropy;public float mip_bias;public string material_guid,shader_guid,shader_file_id,shader_name,original_material_json;public Color get_color_tint;public string variant_source_sha256;public bool private_tint_override;public string[] original_keywords;public Row[] rows;}
 [Serializable] public class Report {public string render_target_format="ARGBHalf";public string unity_version,color_space,gpu_name,gpu_type,source_render_pipeline,measurement_render_pipeline;public bool pipeline_settings_restored;public Case[] cases;public string fixture_scope="private material clone with exact requested tint variant; real builtin shader and RA texture; diagnostic Color32 fixtures inherited from the earlier probe; no authored torch color, whole-effect, soft/fog or Forge qualification";public bool source_materials_unchanged,source_shader_fixture_gpu_captured;public bool full_import=false;}
 static void Identity(UnityEngine.Object source,string expectedGuid,string expectedId){string guid;long id;if(!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(source,out guid,out id)||guid!=expectedGuid||id.ToString()!=expectedId)throw new Exception("Original GPU probe identity differs");}
 public static void Export(){
  var args=Environment.GetCommandLineArgs();int index=Array.IndexOf(args,"--openrpg-manifest");if(index<0)throw new Exception("Manifest absent");
  var manifest=JsonUtility.FromJson<Manifest>(File.ReadAllText(args[index+1]));if(File.Exists(manifest.output))throw new Exception("Preserve prior GPU capture");
  if(SystemInfo.graphicsDeviceType==GraphicsDeviceType.Null||!SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGBHalf))throw new Exception("Actual float GPU renderer unavailable");
  var originalGraphics=GraphicsSettings.defaultRenderPipeline;var originalQuality=QualitySettings.renderPipeline;Report capturedReport=null;var originalFog=RenderSettings.fog;var fogKeys=new[]{"FOG_LINEAR","FOG_EXP","FOG_EXP2","SOFTPARTICLES_ON","SOFTPARTICLES_OFF"};var originalKeys=fogKeys.Select(Shader.IsKeywordEnabled).ToArray();
  var results=new List<Case>();bool observedSourceFragment=false;
  var mesh=new Mesh();mesh.vertices=new[]{new Vector3(-1,-1,-1),new Vector3(1,-1,-1),new Vector3(1,1,-1),new Vector3(-1,1,-1)};mesh.uv=new[]{Vector2.zero,Vector2.right,Vector2.one,Vector2.up};mesh.triangles=new[]{0,1,2,0,2,3};
  var target=new RenderTexture(16,16,24,RenderTextureFormat.ARGBHalf,RenderTextureReadWrite.Linear);target.useMipMap=false;target.autoGenerateMips=false;target.antiAliasing=1;if(!target.Create())throw new Exception("Float render target refused");
  var readback=new Texture2D(16,16,TextureFormat.RGBAHalf,false,true);var previous=RenderTexture.active;
  try{
   GraphicsSettings.defaultRenderPipeline=null;QualitySettings.renderPipeline=null;if(GraphicsSettings.currentRenderPipeline!=null)throw new Exception("Supported Built-in measurement context unavailable");RenderSettings.fog=false;foreach(string key in fogKeys)Shader.DisableKeyword(key);Shader.EnableKeyword("SOFTPARTICLES_OFF");
   foreach(var input in manifest.cases){
    var source=AssetDatabase.LoadAssetAtPath<Material>(input.asset_path);if(source==null)throw new Exception("Original material absent");Identity(source,input.material_guid,"2100000");Identity(source.shader,"0000000000000000f000000000000000","203");
    if(source.shader.name!="Legacy Shaders/Particles/Alpha Blended"||source.passCount!=1)throw new Exception("Original legacy source pass differs");
    var texture=source.GetTexture("_MainTex") as Texture2D;if(texture==null)throw new Exception("Actual source RA texture missing");Identity(texture,"63f624c8bd42f35409dde06b85d2d900","2800000");
    string before=EditorJsonUtility.ToJson(source,true);var material=new Material(source);material.SetColor("_TintColor",input.variant_tint);var actualVariantTint=material.GetColor("_TintColor");if(actualVariantTint.r!=input.variant_tint.r||actualVariantTint.g!=input.variant_tint.g||actualVariantTint.b!=input.variant_tint.b||actualVariantTint.a!=input.variant_tint.a)throw new Exception("Private variant tint differs");var rows=new List<Row>();
    try{
     // Original cloned material retains its actual bound texture.
     material.DisableKeyword("SOFTPARTICLES_ON");material.EnableKeyword("SOFTPARTICLES_OFF");foreach(string key in new[]{"FOG_LINEAR","FOG_EXP","FOG_EXP2"})material.DisableKeyword(key);
     foreach(var color in input.baked_colors)foreach(var background in new[]{new Color(0,0,0,0.25f),new Color(0.125f,0.25f,0.5f,0.75f)}){
      mesh.colors32=new[]{color,color,color,color};
      var commands=new CommandBuffer();
      try{
       commands.SetRenderTarget(target);commands.ClearRenderTarget(true,true,background);
       commands.SetViewProjectionMatrices(Matrix4x4.identity,GL.GetGPUProjectionMatrix(Matrix4x4.Ortho(-1,1,-1,1,0.1f,10),true));
       commands.DrawMesh(mesh,Matrix4x4.identity,material,0,0);Graphics.ExecuteCommandBuffer(commands);
      }finally{commands.Release();}
      RenderTexture.active=target;readback.ReadPixels(new Rect(0,0,16,16),0,0,false);readback.Apply(false,false);Color actual=readback.GetPixel(8,8);
      if(float.IsNaN(actual.r)||float.IsInfinity(actual.r)||float.IsNaN(actual.g)||float.IsInfinity(actual.g)||float.IsNaN(actual.b)||float.IsInfinity(actual.b)||float.IsNaN(actual.a)||float.IsInfinity(actual.a))throw new Exception("Nonfinite native GPU result");
      if(actual.a!=background.a)throw new Exception("Original shader did not preserve destination alpha");
      var pixels=readback.GetPixels();if(pixels.Length!=256)throw new Exception("Native pixel count differs");
      foreach(var pixel in pixels){if(float.IsNaN(pixel.r)||float.IsInfinity(pixel.r)||float.IsNaN(pixel.g)||float.IsInfinity(pixel.g)||float.IsNaN(pixel.b)||float.IsInfinity(pixel.b)||float.IsNaN(pixel.a)||float.IsInfinity(pixel.a)||pixel.a!=background.a)throw new Exception("Native HDR pixels/alpha differ");if(background.r==0&&(pixel.r>0||pixel.g>0||pixel.b>0))observedSourceFragment=true;}
      var raw=readback.GetRawTextureData<byte>().ToArray();if(!BitConverter.IsLittleEndian||raw.Length!=2048)throw new Exception("Native half pixel storage differs");
      var directory=Path.Combine(Path.GetDirectoryName(manifest.output),"native-pixels");Directory.CreateDirectory(directory);string pixelPath=Path.Combine(directory,input.material_guid+"-"+rows.Count.ToString("D2")+".rgba.f16");if(File.Exists(pixelPath))throw new Exception("Preserve earlier native pixels");File.WriteAllBytes(pixelPath,raw);
      string pixelSha;using(var hashing=System.Security.Cryptography.SHA256.Create())pixelSha=BitConverter.ToString(hashing.ComputeHash(raw)).Replace("-","").ToLowerInvariant();
      rows.Add(new Row{pixels_path=pixelPath,pixels_sha256=pixelSha,baked_color=color,background=background,result=actual,alpha_channel_preserved=true});
     }
    }finally{UnityEngine.Object.DestroyImmediate(material);}
    if(before!=EditorJsonUtility.ToJson(source,true))throw new Exception("Original source material mutated");
    results.Add(new Case{texture_guid="63f624c8bd42f35409dde06b85d2d900",texture_file_id="2800000",graphics_format=texture.graphicsFormat.ToString(),texture_format=texture.format.ToString(),filter_mode=texture.filterMode.ToString(),wrap_mode_u=texture.wrapModeU.ToString(),wrap_mode_v=texture.wrapModeV.ToString(),texture_width=texture.width,texture_height=texture.height,mipmap_count=texture.mipmapCount,anisotropy=texture.anisoLevel,mip_bias=texture.mipMapBias,material_guid=input.material_guid,shader_guid="0000000000000000f000000000000000",shader_file_id="203",shader_name=source.shader.name,original_material_json=before,get_color_tint=actualVariantTint,variant_source_sha256=input.variant_source_sha256,private_tint_override=true,original_keywords=source.shaderKeywords,rows=rows.ToArray()});
   }
   if(!observedSourceFragment)throw new Exception("No source shader fragment observed");
   capturedReport=new Report{source_render_pipeline=(originalQuality!=null?originalQuality:originalGraphics)?.GetType().FullName??"Builtin",measurement_render_pipeline="Builtin",unity_version=Application.unityVersion,color_space=QualitySettings.activeColorSpace.ToString(),gpu_name=SystemInfo.graphicsDeviceName,gpu_type=SystemInfo.graphicsDeviceType.ToString(),cases=results.ToArray(),source_materials_unchanged=true,source_shader_fixture_gpu_captured=true};
  }finally{
   GraphicsSettings.defaultRenderPipeline=originalGraphics;QualitySettings.renderPipeline=originalQuality;RenderTexture.active=previous;RenderSettings.fog=originalFog;for(int i=0;i<fogKeys.Length;i++){if(originalKeys[i])Shader.EnableKeyword(fogKeys[i]);else Shader.DisableKeyword(fogKeys[i]);}
   target.Release();UnityEngine.Object.DestroyImmediate(target);UnityEngine.Object.DestroyImmediate(readback);UnityEngine.Object.DestroyImmediate(mesh);
  }
  if(GraphicsSettings.defaultRenderPipeline!=originalGraphics||QualitySettings.renderPipeline!=originalQuality)throw new Exception("Measurement pipeline restoration differs");
  if(capturedReport==null)throw new Exception("Original GPU report absent");capturedReport.pipeline_settings_restored=true;File.WriteAllText(manifest.output,JsonUtility.ToJson(capturedReport,true));
 }
}
