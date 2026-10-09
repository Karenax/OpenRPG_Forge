using System;
using System.IO;
using System.Collections.Generic;
using System.Globalization;
using UnityEditor;
using UnityEngine;

public static class OpenRPGFirePitEffectMaterialIdentity {
 [Serializable] public class Request {public string asset_path,material_guid;}
 [Serializable] public class Manifest {public string output;public Request[] cases;}
 [Serializable] public class Property {public string name,type,texture_guid,texture_file_id;public float number;public Vector4 vector,texture_scale_offset;}
 [Serializable] public class Row {public string material_guid,material_file_id,shader_guid,shader_file_id,shader_name,native_material_json;public int render_queue;public bool shader_supported;public string[] keywords,pass_names;public Property[] properties;public bool rendered=false;}
 [Serializable] public class Report {public string unity_version;public Row[] cases;public bool gpu_rendered=false,runtime_converted=false,full_import=false;}
 public static void Export(){
  var args=Environment.GetCommandLineArgs();int index=Array.IndexOf(args,"--openrpg-manifest");if(index<0)throw new Exception("Manifest absent");
  var manifest=JsonUtility.FromJson<Manifest>(File.ReadAllText(args[index+1]));if(manifest.cases.Length!=2)throw new Exception("Exactly two original materials required");
  var rows=new List<Row>();
  foreach(var request in manifest.cases){
   var material=AssetDatabase.LoadAssetAtPath<Material>(request.asset_path);if(material==null)throw new Exception("Original material absent");
   string guid;long id;if(!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(material,out guid,out id)||guid!=request.material_guid||id!=2100000)throw new Exception("Original material identity differs");
   var shader=material.shader;if(shader==null)throw new Exception("Original shader absent");string shaderGuid;long shaderId;if(!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(shader,out shaderGuid,out shaderId))throw new Exception("Shader identity unavailable");
   if(shaderGuid!="0000000000000000f000000000000000"||shaderId!=203)throw new Exception("Serialized original built-in shader identity differs");
   string before=EditorJsonUtility.ToJson(material);var properties=new List<Property>();
   for(int p=0;p<ShaderUtil.GetPropertyCount(shader);p++){
    string name=ShaderUtil.GetPropertyName(shader,p);var type=ShaderUtil.GetPropertyType(shader,p);var property=new Property{name=name,type=type.ToString()};
    switch(type){
     case ShaderUtil.ShaderPropertyType.Color:property.vector=material.GetColor(name);break;
     case ShaderUtil.ShaderPropertyType.Vector:property.vector=material.GetVector(name);break;
     case ShaderUtil.ShaderPropertyType.Float:case ShaderUtil.ShaderPropertyType.Range:property.number=material.GetFloat(name);break;
     case ShaderUtil.ShaderPropertyType.TexEnv:
      var texture=material.GetTexture(name);if(texture!=null){string tg;long ti;if(!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(texture,out tg,out ti))throw new Exception("Texture identity unavailable");property.texture_guid=tg;property.texture_file_id=ti.ToString(CultureInfo.InvariantCulture);}
      var scale=material.GetTextureScale(name);var offset=material.GetTextureOffset(name);property.texture_scale_offset=new Vector4(scale.x,scale.y,offset.x,offset.y);break;
     default:throw new Exception("Unsupported native property type "+type);
    }
    properties.Add(property);
   }
   var passes=new List<string>();for(int p=0;p<material.passCount;p++)passes.Add(material.GetPassName(p));
   if(before!=EditorJsonUtility.ToJson(material))throw new Exception("Observation changed original material");
   rows.Add(new Row{material_guid=guid,material_file_id=id.ToString(CultureInfo.InvariantCulture),shader_guid=shaderGuid,shader_file_id=shaderId.ToString(CultureInfo.InvariantCulture),shader_name=shader.name,native_material_json=before,render_queue=material.renderQueue,shader_supported=shader.isSupported,keywords=material.shaderKeywords,pass_names=passes.ToArray(),properties=properties.ToArray()});
  }
  File.WriteAllText(manifest.output,JsonUtility.ToJson(new Report{unity_version=Application.unityVersion,cases=rows.ToArray()},true));
 }
}
