#if UNITY_EDITOR
using System;
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEditor.PackageManager;
namespace OpenRPGOriginalGraphHDRP {
 [Serializable] public class Request {public string material_guid,asset_path,expected_shader_guid,expected_shader_file_id;}
 [Serializable] public class Manifest {public string output_path;public Request[] cases;}
 [Serializable] public class PackageRow {public string name,version,resolved_path,source;}
 [Serializable] public class PropertyRow {public string name,type,texture_guid,texture_file_id,texture_name,texture_asset_path,default_texture_name;public float native_float,default_float;public Vector4 native_vector,default_vector;public bool texture_null,texture_exact_identity;}
 [Serializable] public class PassRow {public string name;public bool enabled,compiled;}
 [Serializable] public class MessageRow {public string severity,message,file,platform;public int line;}
 [Serializable] public class Row {public string material_guid,actual_material_file_id,shader_guid,shader_file_id,shader_name,error,native_material_json;public int render_queue;public string[] keywords;public PropertyRow[] properties;public PassRow[] passes;public MessageRow[] messages;public bool shader_has_errors,shader_supported,compile_passes_executed,original_shader_render_parity_qualified;}
 [Serializable] public class Result {public string unity_version,graphics_device,graphics_api,selected_pipeline_type;public PackageRow[] packages;public Row[] cases;public int requested,captured,failed;public bool purchased_scripts_executed,gpu_render_parity_qualified,original_pack_unity_version_qualified;}
 public static class OriginalGraphMaterialSnapshotHDRP {
  static string Arg(string name){var a=Environment.GetCommandLineArgs();for(int i=0;i+1<a.Length;i++)if(a[i]==name)return a[i+1];throw new Exception("Missing "+name);}
  public static void Export(){
   var request=JsonUtility.FromJson<Manifest>(File.ReadAllText(Arg("--openrpg-manifest")));
   if(File.Exists(request.output_path))throw new Exception("Preserve prior native material snapshot");
   if(PlayerSettings.colorSpace!=ColorSpace.Linear||SystemInfo.graphicsDeviceType==UnityEngine.Rendering.GraphicsDeviceType.Null)throw new Exception("Actual Linear native GPU compiler required");
   var packages=new List<PackageRow>();bool hdrp=false,urp=false;
   foreach(var p in UnityEditor.PackageManager.PackageInfo.GetAllRegisteredPackages()){packages.Add(new PackageRow{name=p.name,version=p.version,resolved_path=p.resolvedPath,source=p.source.ToString()});if(p.name=="com.unity.render-pipelines.high-definition")hdrp=p.version=="17.5.0";if(p.name=="com.unity.render-pipelines.universal")urp=p.version=="17.5.0";}
   if(!hdrp||!urp)throw new Exception("Exact installed HDRP/URP17.5.0 required");
   // This asset selects an isolated HDRP compiler context; it is not the absent original project configuration.
   const string contextPath="Assets/OpenRPGProbe/SourcePassHDRPContext.asset";
   if(!AssetDatabase.IsValidFolder("Assets/OpenRPGProbe"))AssetDatabase.CreateFolder("Assets","OpenRPGProbe");
   var pipeline=AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.HighDefinition.HDRenderPipelineAsset>(contextPath);
   if(pipeline==null){pipeline=ScriptableObject.CreateInstance<UnityEngine.Rendering.HighDefinition.HDRenderPipelineAsset>();AssetDatabase.CreateAsset(pipeline,contextPath);}
   UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline=pipeline;
   QualitySettings.renderPipeline=pipeline;
   AssetDatabase.SaveAssets();
   if(UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline!=pipeline)throw new Exception("Actual selected HDRP context required");
   foreach(var item in request.cases){var path=AssetDatabase.GUIDToAssetPath(item.expected_shader_guid);AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceUpdate|ImportAssetOptions.ForceSynchronousImport);}
   var result=new Result{selected_pipeline_type=pipeline.GetType().FullName,unity_version=Application.unityVersion,graphics_device=SystemInfo.graphicsDeviceName,graphics_api=SystemInfo.graphicsDeviceType.ToString(),packages=packages.ToArray(),requested=request.cases.Length};var rows=new List<Row>();
   foreach(var item in request.cases){
    var row=new Row{material_guid=item.material_guid,original_shader_render_parity_qualified=false};
    try{
     if(!item.asset_path.StartsWith("Assets/",StringComparison.Ordinal)||item.asset_path.Contains(".."))throw new Exception("Unsafe original asset path");
     var material=AssetDatabase.LoadAssetAtPath<Material>(item.asset_path);if(material==null)throw new Exception("Original native Material unavailable");
     string guid;long id;if(!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(material,out guid,out id)||guid!=item.material_guid||id!=2100000)throw new Exception("Exact original Material identity required");row.actual_material_file_id=id.ToString(System.Globalization.CultureInfo.InvariantCulture);
     var shader=material.shader;if(shader==null)throw new Exception("Original shader unavailable; no fallback");
     if(!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(shader,out guid,out id)||guid!=item.expected_shader_guid||id.ToString(System.Globalization.CultureInfo.InvariantCulture)!=item.expected_shader_file_id)throw new Exception("Exact original shader GUID/fileID required; no fallback");
     row.shader_supported=shader.isSupported;if(!row.shader_supported)throw new Exception("Source shader unsupported in selected HDRP context");row.shader_guid=guid;row.shader_file_id=id.ToString(System.Globalization.CultureInfo.InvariantCulture);row.shader_name=shader.name;row.render_queue=material.renderQueue;row.keywords=material.shaderKeywords;row.native_material_json=EditorJsonUtility.ToJson(material);
     var properties=new List<PropertyRow>();
     for(int i=0;i<shader.GetPropertyCount();i++){
      var name=shader.GetPropertyName(i);var type=shader.GetPropertyType(i).ToString();var p=new PropertyRow{name=name,type=type};
      if(type=="Float"||type=="Range"){p.native_float=material.GetFloat(name);p.default_float=shader.GetPropertyDefaultFloatValue(i);}
      else if(type=="Vector"){p.native_vector=material.GetVector(name);p.default_vector=shader.GetPropertyDefaultVectorValue(i);}
      else if(type=="Color"){var c=material.GetColor(name);p.native_vector=new Vector4(c.r,c.g,c.b,c.a);p.default_vector=shader.GetPropertyDefaultVectorValue(i);}
      else if(type=="Texture"){
       var tex=material.GetTexture(name);p.texture_null=tex==null;p.default_texture_name=shader.GetPropertyTextureDefaultName(i);
       if(tex!=null){p.texture_name=tex.name;p.texture_asset_path=AssetDatabase.GetAssetPath(tex);p.texture_exact_identity=AssetDatabase.TryGetGUIDAndLocalFileIdentifier(tex,out guid,out id);if(p.texture_exact_identity){p.texture_guid=guid;p.texture_file_id=id.ToString(System.Globalization.CultureInfo.InvariantCulture);}}
      }else if(type=="Int"){p.native_float=material.GetInteger(name);p.default_float=shader.GetPropertyDefaultIntValue(i);}
      else throw new Exception("Unknown native shader property type: "+type);
      properties.Add(p);
     }
     row.properties=properties.ToArray();var passes=new List<PassRow>();
     if(material.passCount<=0)throw new Exception("Original shader has no native passes");
     for(int i=0;i<material.passCount;i++){var name=material.GetPassName(i);if(string.IsNullOrEmpty(name)||name.StartsWith("<Unnamed",StringComparison.Ordinal))throw new Exception("Unnamed source pass refused; fallback is not original HDRP coverage");ShaderUtil.CompilePass(material,i,true);var compiled=ShaderUtil.IsPassCompiled(material,i);if(!compiled)throw new Exception("Synchronous source pass not compiled: "+name);passes.Add(new PassRow{name=name,enabled=material.GetShaderPassEnabled(name),compiled=compiled});}
     row.passes=passes.ToArray();row.compile_passes_executed=true;row.shader_has_errors=ShaderUtil.ShaderHasError(shader);var messages=new List<MessageRow>();
     foreach(var m in ShaderUtil.GetShaderMessages(shader)){messages.Add(new MessageRow{severity=m.severity.ToString(),message=m.message,file=m.file,line=m.line,platform=m.platform.ToString()});if(m.severity.ToString()=="Error")row.shader_has_errors=true;}
     row.messages=messages.ToArray();if(row.shader_has_errors)throw new Exception("Original native shader compilation errors retained");result.captured++;
    }catch(Exception ex){row.error=ex.ToString();result.failed++;}
    rows.Add(row);result.cases=rows.ToArray();File.WriteAllText(request.output_path+".progress.json",JsonUtility.ToJson(result,true));
   }
   File.WriteAllText(request.output_path,JsonUtility.ToJson(result,true));if(result.failed!=0)throw new Exception("Original source material capture incomplete; inspect explicit failures");
  }
 }
}
#endif

