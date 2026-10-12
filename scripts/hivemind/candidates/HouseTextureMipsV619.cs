using System;using System.IO;using System.Collections.Generic;using UnityEditor;using UnityEngine;
public static class OpenRPGHouseTextureMipsV619{
 [Serializable]public class Item{public string guid,folder,mip0_path,format,graphics_format;public int width,height,mips;}
 [Serializable]public class Request{public string output,shader_path;public Item[] textures;}
 [Serializable]public class Mip{public int level,width,height,bytes;public string path;public bool reused_existing_proof;}
 [Serializable]public class Row{public string guid,format,graphics_format;public int native_mips;public Mip[] levels;public bool texture_settings_unchanged;}
 [Serializable]public class Report{public string unity_version,graphics_api,gpu;public Row[] textures;public bool source_texture_settings_unchanged,full_import=false,implicit_derivative_render_verified=false;}
 public static void Export(){
  var args=Environment.GetCommandLineArgs();int p=Array.IndexOf(args,"--openrpg-manifest");if(p<0)throw new Exception("Manifest absent");var r=JsonUtility.FromJson<Request>(File.ReadAllText(args[p+1]));if(File.Exists(r.output))throw new Exception("Preserve evidence");var shader=AssetDatabase.LoadAssetAtPath<ComputeShader>(r.shader_path);if(shader==null)throw new Exception("Owned compute missing");int k=shader.FindKernel("CopyMip");var result=new List<Row>();
  foreach(var item in r.textures){
   var t=AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GUIDToAssetPath(item.guid));if(t==null||t.width!=item.width||t.height!=item.height||t.mipmapCount!=item.mips||t.format.ToString()!=item.format||t.graphicsFormat.ToString()!=item.graphics_format)throw new Exception("Native texture differs");string before=EditorJsonUtility.ToJson(t,true);var levels=new List<Mip>();if(new FileInfo(item.mip0_path).Length!=item.width*item.height*16)throw new Exception("Retained mip zero bytes differ");levels.Add(new Mip{level=0,width=t.width,height=t.height,bytes=t.width*t.height*16,path=item.mip0_path,reused_existing_proof=true});
   for(int level=1;level<t.mipmapCount;level++){
    int width=Math.Max(1,t.width>>level),height=Math.Max(1,t.height>>level);string path=Path.Combine(item.folder,"mip-"+level+"-rgba.f32");if(File.Exists(path))throw new Exception("Preserve prior mip output");
    using(var buffer=new ComputeBuffer(width*height,16)){shader.SetTexture(k,"_Texture",t);shader.SetBuffer(k,"_Output",buffer);shader.SetInt("_Width",width);shader.SetInt("_Height",height);shader.SetInt("_Mip",level);shader.Dispatch(k,(width+7)/8,(height+7)/8,1);var values=new Vector4[width*height];buffer.GetData(values);var data=new byte[values.Length*16];for(int i=0;i<values.Length;i++)for(int c=0;c<4;c++){float f=values[i][c];if(float.IsNaN(f)||float.IsInfinity(f))throw new Exception("Nonfinite native mip");Buffer.BlockCopy(BitConverter.GetBytes(f),0,data,i*16+c*4,4);}File.WriteAllBytes(path,data);levels.Add(new Mip{level=level,width=width,height=height,bytes=data.Length,path=path,reused_existing_proof=false});}
   }
   if(before!=EditorJsonUtility.ToJson(t,true))throw new Exception("Native texture settings changed");result.Add(new Row{guid=item.guid,format=t.format.ToString(),graphics_format=t.graphicsFormat.ToString(),native_mips=t.mipmapCount,levels=levels.ToArray(),texture_settings_unchanged=true});
  }
  File.WriteAllText(r.output,JsonUtility.ToJson(new Report{unity_version=Application.unityVersion,graphics_api=SystemInfo.graphicsDeviceType.ToString(),gpu=SystemInfo.graphicsDeviceName,textures=result.ToArray(),source_texture_settings_unchanged=true},true));
 }
}
