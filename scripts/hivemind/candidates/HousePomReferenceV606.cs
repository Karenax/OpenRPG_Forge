using System;
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
public static class OpenRPGHousePomReferenceV606 {
 [Serializable] public class Item {public string guid,height_guid,input,raw_output,height_output;public int count;public float amplitude;public Vector2 texture_scale,texture_offset;}
 [Serializable] public class Request {public string output,shader_path;public Item[] cases;}
 [Serializable] public class Row {public string guid,height_guid,height_path,raw_output,height_output,format,graphics_format,filter,wrap_u,wrap_v;public int width,height,mips,aniso,samples,output_bytes;public float amplitude;public Vector2 texture_scale,texture_offset;public bool source_material_unchanged;}
 [Serializable] public class Report {public string unity_version,graphics_api,gpu;public Row[] materials;public bool source_materials_unchanged,full_import=false,actual_prefab_render_verified=false,actual_generated_graph_precision_verified=false;}
 static string Guid(UnityEngine.Object obj){string g;long id;return AssetDatabase.TryGetGUIDAndLocalFileIdentifier(obj,out g,out id)?g:"";}
 static byte[] Bytes(Vector4[] values){var raw=new byte[values.Length*16];for(int i=0;i<values.Length;i++)for(int j=0;j<4;j++){float f=values[i][j];if(float.IsNaN(f)||float.IsInfinity(f))throw new Exception("Nonfinite GPU output");Buffer.BlockCopy(BitConverter.GetBytes(f),0,raw,i*16+j*4,4);}return raw;}
 public static void Export(){
  var args=Environment.GetCommandLineArgs();int p=Array.IndexOf(args,"--openrpg-manifest");if(p<0)throw new Exception("Manifest absent");var r=JsonUtility.FromJson<Request>(File.ReadAllText(args[p+1]));if(File.Exists(r.output))throw new Exception("Preserve evidence");
  if(!BitConverter.IsLittleEndian||!SystemInfo.supportsComputeShaders)throw new Exception("Compute required");var shader=AssetDatabase.LoadAssetAtPath<ComputeShader>(r.shader_path);if(shader==null)throw new Exception("Owned compute missing");var rows=new List<Row>();
  foreach(var c in r.cases){
   if(File.Exists(c.raw_output)||File.Exists(c.height_output))throw new Exception("Preserve previous outputs");var m=AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(c.guid));if(m==null||Guid(m.shader)!="7dcd0e3bb8cbf2345a1e124f5a095198")throw new Exception("Source material/shader differs");
   string before=EditorJsonUtility.ToJson(m,true);var texture=m.GetTexture("_L1_Height") as Texture2D;if(texture==null||Guid(texture)!=c.height_guid)throw new Exception("Height binding differs "+c.guid);
   if(m.GetFloat("_PomHeight")!=c.amplitude||m.GetTextureScale("_L1_Height")!=c.texture_scale||m.GetTextureOffset("_L1_Height")!=c.texture_offset)throw new Exception("Source POM parameters differ");
   var raw=File.ReadAllBytes(c.input);if(raw.Length!=c.count*80)throw new Exception("Input stride");var values=new Vector4[c.count*5];for(int i=0;i<values.Length;i++){int o=i*16;values[i]=new Vector4(BitConverter.ToSingle(raw,o),BitConverter.ToSingle(raw,o+4),BitConverter.ToSingle(raw,o+8),BitConverter.ToSingle(raw,o+12));}
   using(var input=new ComputeBuffer(values.Length,16))using(var output=new ComputeBuffer(c.count,16)){
    input.SetData(values);int k=shader.FindKernel("CSMain");shader.SetInt("_Count",c.count);shader.SetBuffer(k,"_Input",input);shader.SetBuffer(k,"_Output",output);shader.SetTexture(k,"_Height",texture);shader.Dispatch(k,(c.count+63)/64,1,1);var result=new Vector4[c.count];output.GetData(result);File.WriteAllBytes(c.raw_output,Bytes(result));
   }
   // Read the actual GPU-decoded red plane at mip zero; never change the importer.
   using(var plane=new ComputeBuffer(texture.width*texture.height,4)){
    int k=shader.FindKernel("CopyHeight");shader.SetInt("_Width",texture.width);shader.SetInt("_HeightSize",texture.height);shader.SetTexture(k,"_Height",texture);shader.SetBuffer(k,"_Plane",plane);shader.Dispatch(k,(texture.width+7)/8,(texture.height+7)/8,1);var result=new float[texture.width*texture.height];plane.GetData(result);var data=new byte[result.Length*4];for(int i=0;i<result.Length;i++){if(float.IsNaN(result[i])||float.IsInfinity(result[i]))throw new Exception("Nonfinite height");Buffer.BlockCopy(BitConverter.GetBytes(result[i]),0,data,i*4,4);}File.WriteAllBytes(c.height_output,data);
   }
   if(before!=EditorJsonUtility.ToJson(m,true))throw new Exception("Material changed");rows.Add(new Row{guid=c.guid,height_guid=Guid(texture),height_path=AssetDatabase.GetAssetPath(texture),raw_output=c.raw_output,height_output=c.height_output,width=texture.width,height=texture.height,mips=texture.mipmapCount,aniso=texture.anisoLevel,format=texture.format.ToString(),graphics_format=texture.graphicsFormat.ToString(),filter=texture.filterMode.ToString(),wrap_u=texture.wrapModeU.ToString(),wrap_v=texture.wrapModeV.ToString(),samples=c.count,output_bytes=c.count*16,amplitude=c.amplitude,texture_scale=c.texture_scale,texture_offset=c.texture_offset,source_material_unchanged=true});
  }
  File.WriteAllText(r.output,JsonUtility.ToJson(new Report{unity_version=Application.unityVersion,graphics_api=SystemInfo.graphicsDeviceType.ToString(),gpu=SystemInfo.graphicsDeviceName,materials=rows.ToArray(),source_materials_unchanged=true},true));
 }
}
