#if UNITY_EDITOR
using System;
using System.IO;
using System.Reflection;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace OpenRPGHdrpContextDiagnostic {
 [Serializable] public class Request { public string material_guid,asset_path,expected_shader_guid,expected_shader_file_id; }
 [Serializable] public class Manifest { public Request[] cases; }
 [Serializable] public class Message { public string severity,message,file,platform; public int line; }
 [Serializable] public class Row {
  public string material_guid,material_file_id,shader_guid,shader_file_id,shader_name,selected_tag,error;
  public bool shader_supported,shader_has_errors; public string[] subshader_tags,pass_names;
  public Message[] compiler_messages;
 }
 [Serializable] public class Observation {
  public string stage,asset_type,asset_shader_tag,global_shader_tag,pipeline_instance_type;
  public Row[] materials;
 }
 [Serializable] public class Result {
  public string unity_version,graphics_api,prepare_method_signature,prepare_error;
  public string[] available_prepare_methods;
  public bool prepare_attempted,prepare_returned_true,gpu_render_parity_qualified,studio_verified;
  public Observation before,after;
 }
 public static class Probe {
  static string Arg(string name) {
   var args=Environment.GetCommandLineArgs();
   for(int i=0;i+1<args.Length;i++)if(args[i]==name)return args[i+1];
   throw new Exception("Missing argument: "+name);
  }
  static Observation Observe(string stage,Manifest manifest) {
   var asset=GraphicsSettings.currentRenderPipeline;
   var pipeline=RenderPipelineManager.currentPipeline;
   var result=new Observation {stage=stage,asset_type=asset==null?"":asset.GetType().FullName,
    asset_shader_tag=asset==null?"":asset.renderPipelineShaderTag,global_shader_tag=Shader.globalRenderPipeline,
    pipeline_instance_type=pipeline==null?"":pipeline.GetType().FullName};
   var rows=new List<Row>();
   foreach(var request in manifest.cases) {
    var row=new Row {material_guid=request.material_guid};
    try {
     var material=AssetDatabase.LoadAssetAtPath<Material>(request.asset_path);
     if(material==null)throw new Exception("Material missing");
     string guid;long id;
     if(!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(material,out guid,out id)||guid!=request.material_guid||id!=2100000)
      throw new Exception("Material identity differs");
     row.material_file_id=id.ToString(System.Globalization.CultureInfo.InvariantCulture);
     var shader=material.shader;
     if(shader==null||!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(shader,out guid,out id)||guid!=request.expected_shader_guid||id.ToString(System.Globalization.CultureInfo.InvariantCulture)!=request.expected_shader_file_id)
      throw new Exception("Original shader identity differs");
     row.shader_guid=guid;row.shader_file_id=id.ToString(System.Globalization.CultureInfo.InvariantCulture);
     row.shader_name=shader.name;row.selected_tag=material.GetTag("RenderPipeline",false,"");
     row.shader_supported=shader.isSupported;row.shader_has_errors=ShaderUtil.ShaderHasError(shader);
     var tags=new List<string>();
     for(int i=0;i<shader.subshaderCount;i++)tags.Add(shader.FindSubshaderTagValue(i,new ShaderTagId("RenderPipeline")).name);
     row.subshader_tags=tags.ToArray();
     var passes=new List<string>();
     for(int i=0;i<material.passCount;i++)passes.Add(material.GetPassName(i));
     row.pass_names=passes.ToArray();
     var messages=new List<Message>();
     foreach(var message in ShaderUtil.GetShaderMessages(shader))messages.Add(new Message {
      severity=message.severity.ToString(),message=message.message,file=message.file,line=message.line,platform=message.platform.ToString()});
     row.compiler_messages=messages.ToArray();
    } catch(Exception error) {row.error=error.ToString();}
    rows.Add(row);
   }
   result.materials=rows.ToArray();return result;
  }
  public static void Export() {
   var path=Arg("--openrpg-diagnostic-output");
   if(File.Exists(path))throw new Exception("Existing diagnostic preserved");
   var manifest=JsonUtility.FromJson<Manifest>(File.ReadAllText(Arg("--openrpg-manifest")));
   if(manifest.cases==null||manifest.cases.Length!=42)throw new Exception("Exact 42-case scope required");
   var result=new Result {unity_version=Application.unityVersion,graphics_api=SystemInfo.graphicsDeviceType.ToString()};
   result.before=Observe("before_native_pipeline_preparation",manifest);
   File.WriteAllText(path+".before.json",JsonUtility.ToJson(result,true));
   var names=new List<string>();
   foreach(var method in typeof(RenderPipelineManager).GetMethods(BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic))
    if(method.Name.IndexOf("Prepare",StringComparison.Ordinal)>=0)names.Add(method.ToString());
   result.available_prepare_methods=names.ToArray();
   try {
    var method=typeof(RenderPipelineManager).GetMethod("TryPrepareRenderPipeline",BindingFlags.Static|BindingFlags.NonPublic,
     null,new Type[]{typeof(RenderPipelineAsset)},null);
    if(method==null||method.ReturnType!=typeof(bool))throw new Exception("Exact native TryPrepareRenderPipeline API unavailable; no substitute");
    result.prepare_method_signature=method.ToString();result.prepare_attempted=true;
    result.prepare_returned_true=(bool)method.Invoke(null,new object[]{GraphicsSettings.currentRenderPipeline});
   } catch(Exception error) {result.prepare_error=error.ToString();}
   result.after=Observe("after_native_pipeline_preparation_attempt",manifest);
   File.WriteAllText(path,JsonUtility.ToJson(result,true));
  }
 }
}
#endif
