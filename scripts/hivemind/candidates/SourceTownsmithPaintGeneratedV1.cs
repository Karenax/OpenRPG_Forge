using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections;
using UnityEditor;
using UnityEngine;

public static class OpenRPGSourceTownsmithPaintGeneratedV1 {
 [Serializable] public class Result {
  public string status,shader_guid,shader_file_id,asset_path,shader_name,generated_source;
  public int generated_characters,configured_texture_entries;
  public bool native_shader_supported,native_shader_errors,material_render_qualified=false,full_import=false;
 }
 public static void Export() {
  var root=Environment.GetEnvironmentVariable("OPENRPG_PAINT_OUT");
  if(string.IsNullOrEmpty(root)||File.Exists(Path.Combine(root,"NATIVE-RESULT.v1.json")))throw new Exception("Output must be fresh");
  var guid="2118bbc334c1aae4aa194cc048d10ff4";
  var path=AssetDatabase.GUIDToAssetPath(guid);
  if(string.IsNullOrEmpty(path)||!path.EndsWith(".shadergraph"))throw new Exception("Original graph unavailable");
  var shader=AssetDatabase.LoadAssetAtPath<Shader>(path);
  if(shader==null||!shader.isSupported||ShaderUtil.ShaderHasError(shader))throw new Exception("Native shader unresolved");
  var importer=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("UnityEditor.ShaderGraph.ShaderGraphImporter")).SingleOrDefault(t=>t!=null);
  if(importer==null)throw new Exception("Installed graph importer type absent");
  var method=importer.GetMethods(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static).Single(m=>m.Name=="GetShaderText"&&m.GetParameters().Length==2&&m.GetParameters()[0].ParameterType==typeof(string));
  object[] args={path,null};
  var source=method.Invoke(null,args) as string;
  if(string.IsNullOrEmpty(source)||!source.Contains("SurfaceDescriptionFunction")||!source.Contains("HLSLPROGRAM"))throw new Exception("Generated source absent");
  int textures=0;if(args[1] is IEnumerable entries)foreach(var entry in entries)textures++;
  if(!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(shader,out string actualGuid,out long id)||actualGuid!=guid)throw new Exception("Original shader identity changed");
  string output=Path.Combine(root,"S_Paint.generated.shader.txt");File.WriteAllText(output,source);
  var result=new Result{status="original_shadergraph_sdk_source_generated",shader_guid=actualGuid,shader_file_id=id.ToString(),asset_path=path,shader_name=shader.name,generated_source=output,generated_characters=source.Length,configured_texture_entries=textures,native_shader_supported=shader.isSupported,native_shader_errors=ShaderUtil.ShaderHasError(shader)};
  File.WriteAllText(Path.Combine(root,"NATIVE-RESULT.v1.json"),JsonUtility.ToJson(result,true));
 }
}
