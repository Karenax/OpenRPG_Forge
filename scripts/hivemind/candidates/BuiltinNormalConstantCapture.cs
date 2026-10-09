#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;
namespace OpenRPGBuiltinNormalProbe {
 [Serializable] public class Result {
  public string unity_version,graphics_api,graphics_device,shader_path;
  public Vector4[] inputs,decoded_normals,platform_flags;
  public bool original_material_render_parity_qualified=false,full_import=false;
 }
 public static class Capture {
  static string Arg(string name) { var a=Environment.GetCommandLineArgs();for(int i=0;i+1<a.Length;i++)if(a[i]==name)return a[i+1];throw new Exception("Missing "+name); }
  public static void Export() {
   string path=Arg("--openrpg-output");if(File.Exists(path))throw new Exception("Preserve existing native proof");
   if(Application.unityVersion!="6000.5.8f1" || SystemInfo.graphicsDeviceType!=UnityEngine.Rendering.GraphicsDeviceType.Direct3D11 || !SystemInfo.supportsComputeShaders)throw new Exception("Actual installed D3D11 compute backend required");
   const string shaderPath="Assets/Probe/BuiltinNormalConstant.compute";
   var shader=AssetDatabase.LoadAssetAtPath<ComputeShader>(shaderPath);if(shader==null||!shader.HasKernel("Capture"))throw new Exception("Exact own shader unavailable");
   Vector4[] inputs={new Vector4(1,1,1,1),new Vector4(0.25f,0.75f,0,0.5f),new Vector4(0.5f,0.5f,1,1),new Vector4(0.125f,0.875f,0.25f,0.25f)};
   using(var source=new ComputeBuffer(inputs.Length,16))using(var results=new ComputeBuffer(inputs.Length,16))using(var flags=new ComputeBuffer(inputs.Length,16)) {
    source.SetData(inputs);int kernel=shader.FindKernel("Capture");shader.SetBuffer(kernel,"Inputs",source);shader.SetBuffer(kernel,"Normals",results);shader.SetBuffer(kernel,"Flags",flags);shader.Dispatch(kernel,inputs.Length,1,1);
    var output=new Vector4[inputs.Length];var platform=new Vector4[inputs.Length];results.GetData(output);flags.GetData(platform);
    foreach(var v in output)foreach(var value in new[]{v.x,v.y,v.z,v.w})if(float.IsNaN(value)||float.IsInfinity(value))throw new Exception("Nonfinite native normal");
    var result=new Result{unity_version=Application.unityVersion,graphics_api=SystemInfo.graphicsDeviceType.ToString(),graphics_device=SystemInfo.graphicsDeviceName,shader_path=shaderPath,inputs=inputs,decoded_normals=output,platform_flags=platform};
    File.WriteAllText(path,JsonUtility.ToJson(result,true));
   }
  }
 }
}
#endif
