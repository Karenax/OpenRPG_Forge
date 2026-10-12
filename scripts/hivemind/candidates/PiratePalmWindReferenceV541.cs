using System;
using System.IO;
using System.Runtime.InteropServices;
using UnityEditor;
using UnityEngine;
public static class OpenRPGPiratePalmWindReferenceV541 {
 [Serializable] public class Request { public string output,input,raw_output,shader_path; public int count; public float wind_speed,wind_strength; }
 [StructLayout(LayoutKind.Sequential)] public struct InputCase { public Vector4 object_position,world_position,normal,tangent,uv,time; }
 [Serializable] public class MaterialRow { public string guid; public bool source_material_unchanged; public float wind_speed,wind_strength; }
 [Serializable] public class Report { public string unity_version,graphics_api,gpu,raw_output; public int samples,output_bytes; public bool source_materials_unchanged,full_import=false,actual_mesh_samples=false; public MaterialRow[] materials; }
 public static void Export() {
  var args=Environment.GetCommandLineArgs();int index=Array.IndexOf(args,"--openrpg-manifest");if(index<0)throw new Exception("Manifest absent");
  var r=JsonUtility.FromJson<Request>(File.ReadAllText(args[index+1]));if(File.Exists(r.output)||File.Exists(r.raw_output))throw new Exception("Preserve previous evidence");
  if(!BitConverter.IsLittleEndian||!SystemInfo.supportsComputeShaders)throw new Exception("Required native compute backend absent");
  const string guid="abc00000000004284671107665666980";
  var m=AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));if(m==null)throw new Exception("Source material absent");
  string sg;long sid;if(!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(m.shader,out sg,out sid)||sg!="8a71d1ff6799ee7429ba5af1b299c193")throw new Exception("Source shader differs");
  string before=EditorJsonUtility.ToJson(m,true);float speed=m.GetFloat("_WindSpeed"),strength=m.GetFloat("_WindStrength");if(speed!=r.wind_speed||strength!=r.wind_strength)throw new Exception("Native material wind binding differs");
  byte[] bytes=File.ReadAllBytes(r.input);if(bytes.Length!=r.count*96||Marshal.SizeOf(typeof(InputCase))!=96)throw new Exception("Input layout mismatch");
  var cases=new InputCase[r.count];for(int i=0;i<cases.Length;i++){
   Vector4[] v=new Vector4[6];for(int j=0;j<6;j++){int p=i*96+j*16;v[j]=new Vector4(BitConverter.ToSingle(bytes,p),BitConverter.ToSingle(bytes,p+4),BitConverter.ToSingle(bytes,p+8),BitConverter.ToSingle(bytes,p+12));}
   cases[i]=new InputCase{object_position=v[0],world_position=v[1],normal=v[2],tangent=v[3],uv=v[4],time=v[5]};
  }
  var shader=AssetDatabase.LoadAssetAtPath<ComputeShader>(r.shader_path);if(shader==null)throw new Exception("Owned reference compute shader absent");
  var input=new ComputeBuffer(r.count,96);var output=new ComputeBuffer(r.count*3,16);
  try {input.SetData(cases);int kernel=shader.FindKernel("CSMain");shader.SetInt("_Count",r.count);shader.SetFloat("_WindSpeed",speed);shader.SetFloat("_WindStrength",strength);shader.SetBuffer(kernel,"_Input",input);shader.SetBuffer(kernel,"_Output",output);shader.Dispatch(kernel,(r.count+63)/64,1,1);
   var result=new Vector4[r.count*3];output.GetData(result);var raw=new byte[result.Length*16];for(int i=0;i<result.Length;i++)for(int c=0;c<4;c++){float f=result[i][c];if(float.IsNaN(f)||float.IsInfinity(f))throw new Exception("Nonfinite native GPU output");Buffer.BlockCopy(BitConverter.GetBytes(f),0,raw,i*16+c*4,4);}File.WriteAllBytes(r.raw_output,raw);
   if(before!=EditorJsonUtility.ToJson(m,true))throw new Exception("Source material mutated");
   File.WriteAllText(r.output,JsonUtility.ToJson(new Report{unity_version=Application.unityVersion,graphics_api=SystemInfo.graphicsDeviceType.ToString(),gpu=SystemInfo.graphicsDeviceName,samples=r.count,output_bytes=raw.Length,raw_output=r.raw_output,source_materials_unchanged=true,materials=new[]{new MaterialRow{guid=guid,source_material_unchanged=true,wind_speed=speed,wind_strength=strength}}},true));
  } finally {input.Release();output.Release();}
 }
}
