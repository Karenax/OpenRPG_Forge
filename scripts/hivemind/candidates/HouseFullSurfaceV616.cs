using System;using System.IO;using System.Collections.Generic;using UnityEngine;using UnityEditor;using UnityEngine.Rendering;
public static class OpenRPGHouseFullSurfaceV616{
 [Serializable]public class Item{public string guid,input,raw_output,folder;public int count;}
 [Serializable]public class Request{public string output,shader_path;public string[] uniform_names;public Item[] materials;}
 [Serializable]public class TextureRow{public string property,guid,raw_output,format,graphics_format,filter,wrap_u,wrap_v;public int width,height,mips,aniso;}
 [Serializable]public class Row{public string guid,raw_output;public int samples,output_bytes;public TextureRow[] textures;public bool source_material_unchanged;}
 [Serializable]public class Report{public string unity_version,graphics_api,gpu,color_space;public Row[] materials;public bool source_materials_unchanged,full_import=false,mesh_render_verified=false,implicit_derivative_lod_verified=false;}
 static string Guid(UnityEngine.Object o){string g;long id;if(!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(o,out g,out id))throw new Exception("Identity missing");return g;}
 static byte[] Bytes(Vector4[] v){var bytes=new byte[v.Length*16];for(int i=0;i<v.Length;i++)for(int j=0;j<4;j++){float f=v[i][j];if(float.IsNaN(f)||float.IsInfinity(f))throw new Exception("Nonfinite GPU result");Buffer.BlockCopy(BitConverter.GetBytes(f),0,bytes,i*16+j*4,4);}return bytes;}
 public static void Export(){
  var args=Environment.GetCommandLineArgs();int p=Array.IndexOf(args,"--openrpg-manifest");if(p<0)throw new Exception("Manifest absent");var r=JsonUtility.FromJson<Request>(File.ReadAllText(args[p+1]));if(File.Exists(r.output))throw new Exception("Preserve evidence");var shader=AssetDatabase.LoadAssetAtPath<ComputeShader>(r.shader_path);if(shader==null)throw new Exception("Owned shader absent");int kernel=shader.FindKernel("CSMain");var rows=new List<Row>();
  foreach(var item in r.materials){
   if(File.Exists(item.raw_output))throw new Exception("Prior output exists");var m=AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(item.guid));if(m==null||Guid(m.shader)!="7dcd0e3bb8cbf2345a1e124f5a095198")throw new Exception("Source identity differs");string before=EditorJsonUtility.ToJson(m,true);
   foreach(string name in r.uniform_names){int index=m.shader.FindPropertyIndex(name);if(index<0)continue;var kind=m.shader.GetPropertyType(index);if(kind==ShaderPropertyType.Float||kind==ShaderPropertyType.Range)shader.SetFloat(name,m.GetFloat(name));else if(kind==ShaderPropertyType.Int)shader.SetInt(name,m.GetInteger(name));else if(kind==ShaderPropertyType.Vector)shader.SetVector(name,m.GetVector(name));else if(kind==ShaderPropertyType.Color)shader.SetVector(name,(Vector4)m.GetColor(name));}
   var textures=new List<TextureRow>();foreach(string name in new[]{"_L1_Height","_L1_Albedo","_L1_Normal","_L2_Height","_L2_Albedo","_L2_Normal","_BlendMap"}){
    var t=m.GetTexture(name) as Texture2D;if(t==null)throw new Exception("Explicit source texture required: "+name);shader.SetTexture(kernel,name,t);shader.SetVector(name+"_TexelSize",new Vector4(1f/t.width,1f/t.height,t.width,t.height));
    if(!name.StartsWith("_L1_",StringComparison.Ordinal))continue;string path=Path.Combine(item.folder,name+"-mip0-rgba.f32");if(File.Exists(path))throw new Exception("Preserve texture output");using(var buffer=new ComputeBuffer(t.width*t.height,16)){
     int k=shader.FindKernel("CopyTexture");shader.SetTexture(k,"_ProbeTexture",t);shader.SetBuffer(k,"_Plane",buffer);shader.SetInt("_Width",t.width);shader.SetInt("_HeightSize",t.height);shader.Dispatch(k,(t.width+7)/8,(t.height+7)/8,1);var data=new Vector4[t.width*t.height];buffer.GetData(data);File.WriteAllBytes(path,Bytes(data));
    }
    textures.Add(new TextureRow{property=name,guid=Guid(t),raw_output=path,width=t.width,height=t.height,mips=t.mipmapCount,aniso=t.anisoLevel,format=t.format.ToString(),graphics_format=t.graphicsFormat.ToString(),filter=t.filterMode.ToString(),wrap_u=t.wrapModeU.ToString(),wrap_v=t.wrapModeV.ToString()});
   }
   var bytes=File.ReadAllBytes(item.input);if(bytes.Length!=item.count*80)throw new Exception("Input stride");var v=new Vector4[item.count*5];for(int i=0;i<v.Length;i++){int a=i*16;v[i]=new Vector4(BitConverter.ToSingle(bytes,a),BitConverter.ToSingle(bytes,a+4),BitConverter.ToSingle(bytes,a+8),BitConverter.ToSingle(bytes,a+12));}
   using(var input=new ComputeBuffer(v.Length,16))using(var output=new ComputeBuffer(item.count*5,16)){input.SetData(v);shader.SetInt("_Count",item.count);shader.SetBuffer(kernel,"_Input",input);shader.SetBuffer(kernel,"_Output",output);shader.Dispatch(kernel,(item.count+63)/64,1,1);var values=new Vector4[item.count*5];output.GetData(values);File.WriteAllBytes(item.raw_output,Bytes(values));}
   if(before!=EditorJsonUtility.ToJson(m,true))throw new Exception("Source material mutated");rows.Add(new Row{guid=item.guid,raw_output=item.raw_output,samples=item.count,output_bytes=item.count*80,textures=textures.ToArray(),source_material_unchanged=true});
  }
  File.WriteAllText(r.output,JsonUtility.ToJson(new Report{unity_version=Application.unityVersion,graphics_api=SystemInfo.graphicsDeviceType.ToString(),gpu=SystemInfo.graphicsDeviceName,color_space=QualitySettings.activeColorSpace.ToString(),materials=rows.ToArray(),source_materials_unchanged=true},true));
 }
}
