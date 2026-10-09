using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;

// Measurement fixtures only: retain the real shader, override its texture on a
// temporary material clone to isolate native tint/blending/channel semantics.
public static class OpenRPGLegacyParticleShaderGpuProbeBuiltinHalf {
 [Serializable] public class Input {public string asset_path,material_guid;public Color32[] baked_colors;}
 [Serializable] public class Manifest {public string output;public Input[] cases;}
 [Serializable] public class Row {public Color32 baked_color;public Color background,result;public bool alpha_channel_preserved;}
 [Serializable] public class Case {public string material_guid,shader_guid,shader_file_id,shader_name,original_material_json;public Color get_color_tint;public string[] original_keywords;public Row[] rows;}
 [Serializable] public class Report {public string render_target_format="ARGBHalf";public string unity_version,color_space,gpu_name,gpu_type,source_render_pipeline,measurement_render_pipeline;public bool pipeline_settings_restored;public Case[] cases;public string fixture_scope="original shader pass zero; temporary white linear texture; measured original baked Color32; fog and soft-particle variants off; no source texture or whole-effect rendering qualification";public bool source_materials_unchanged,source_shader_fixture_gpu_captured;public bool full_import=false;}
 static void Identity(UnityEngine.Object source,string expectedGuid,string expectedId){string guid;long id;if(!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(source,out guid,out id)||guid!=expectedGuid||id.ToString()!=expectedId)throw new Exception("Original GPU probe identity differs");}
 public static void Export(){
  var args=Environment.GetCommandLineArgs();int index=Array.IndexOf(args,"--openrpg-manifest");if(index<0)throw new Exception("Manifest absent");
  var manifest=JsonUtility.FromJson<Manifest>(File.ReadAllText(args[index+1]));if(File.Exists(manifest.output))throw new Exception("Preserve prior GPU capture");
  if(SystemInfo.graphicsDeviceType==GraphicsDeviceType.Null||!SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGBHalf))throw new Exception("Actual float GPU renderer unavailable");
  var originalGraphics=GraphicsSettings.defaultRenderPipeline;var originalQuality=QualitySettings.renderPipeline;Report capturedReport=null;var originalFog=RenderSettings.fog;var fogKeys=new[]{"FOG_LINEAR","FOG_EXP","FOG_EXP2","SOFTPARTICLES_ON","SOFTPARTICLES_OFF"};var originalKeys=fogKeys.Select(Shader.IsKeywordEnabled).ToArray();
  var results=new List<Case>();var texture=new Texture2D(1,1,TextureFormat.RGBAFloat,false,true);texture.SetPixel(0,0,Color.white);texture.Apply(false,false);
  var mesh=new Mesh();mesh.vertices=new[]{new Vector3(-1,-1,-1),new Vector3(1,-1,-1),new Vector3(1,1,-1),new Vector3(-1,1,-1)};mesh.uv=new[]{Vector2.zero,Vector2.right,Vector2.one,Vector2.up};mesh.triangles=new[]{0,1,2,0,2,3};
  var target=new RenderTexture(16,16,24,RenderTextureFormat.ARGBHalf,RenderTextureReadWrite.Linear);target.useMipMap=false;target.autoGenerateMips=false;target.antiAliasing=1;if(!target.Create())throw new Exception("Float render target refused");
  var readback=new Texture2D(16,16,TextureFormat.RGBAFloat,false,true);var previous=RenderTexture.active;
  try{
   GraphicsSettings.defaultRenderPipeline=null;QualitySettings.renderPipeline=null;if(GraphicsSettings.currentRenderPipeline!=null)throw new Exception("Supported Built-in measurement context unavailable");RenderSettings.fog=false;foreach(string key in fogKeys)Shader.DisableKeyword(key);Shader.EnableKeyword("SOFTPARTICLES_OFF");
   foreach(var input in manifest.cases){
    var source=AssetDatabase.LoadAssetAtPath<Material>(input.asset_path);if(source==null)throw new Exception("Original material absent");Identity(source,input.material_guid,"2100000");Identity(source.shader,"0000000000000000f000000000000000","203");
    if(source.shader.name!="Legacy Shaders/Particles/Alpha Blended"||source.passCount!=1)throw new Exception("Original legacy source pass differs");
    string before=EditorJsonUtility.ToJson(source,true);var material=new Material(source);var rows=new List<Row>();
    try{
     material.SetTexture("_MainTex",texture);material.DisableKeyword("SOFTPARTICLES_ON");material.EnableKeyword("SOFTPARTICLES_OFF");foreach(string key in new[]{"FOG_LINEAR","FOG_EXP","FOG_EXP2"})material.DisableKeyword(key);
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
      rows.Add(new Row{baked_color=color,background=background,result=actual,alpha_channel_preserved=true});
     }
    }finally{UnityEngine.Object.DestroyImmediate(material);}
    if(before!=EditorJsonUtility.ToJson(source,true))throw new Exception("Original source material mutated");
    results.Add(new Case{material_guid=input.material_guid,shader_guid="0000000000000000f000000000000000",shader_file_id="203",shader_name=source.shader.name,original_material_json=before,get_color_tint=source.GetColor("_TintColor"),original_keywords=source.shaderKeywords,rows=rows.ToArray()});
   }
   if(!results.Any(c=>c.rows.Any(r=>r.background.r==0&&r.result.r>0)))throw new Exception("No source shader fragment observed");
   capturedReport=new Report{source_render_pipeline=(originalQuality!=null?originalQuality:originalGraphics)?.GetType().FullName??"Builtin",measurement_render_pipeline="Builtin",unity_version=Application.unityVersion,color_space=QualitySettings.activeColorSpace.ToString(),gpu_name=SystemInfo.graphicsDeviceName,gpu_type=SystemInfo.graphicsDeviceType.ToString(),cases=results.ToArray(),source_materials_unchanged=true,source_shader_fixture_gpu_captured=true};
  }finally{
   GraphicsSettings.defaultRenderPipeline=originalGraphics;QualitySettings.renderPipeline=originalQuality;RenderTexture.active=previous;RenderSettings.fog=originalFog;for(int i=0;i<fogKeys.Length;i++){if(originalKeys[i])Shader.EnableKeyword(fogKeys[i]);else Shader.DisableKeyword(fogKeys[i]);}
   target.Release();UnityEngine.Object.DestroyImmediate(target);UnityEngine.Object.DestroyImmediate(readback);UnityEngine.Object.DestroyImmediate(mesh);UnityEngine.Object.DestroyImmediate(texture);
  }
  if(GraphicsSettings.defaultRenderPipeline!=originalGraphics||QualitySettings.renderPipeline!=originalQuality)throw new Exception("Measurement pipeline restoration differs");
  if(capturedReport==null)throw new Exception("Original GPU report absent");capturedReport.pipeline_settings_restored=true;File.WriteAllText(manifest.output,JsonUtility.ToJson(capturedReport,true));
 }
}
