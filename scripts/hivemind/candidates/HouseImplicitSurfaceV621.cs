using System;using System.IO;using System.Collections.Generic;using UnityEngine;using UnityEditor;
public static class OpenRPGHouseImplicitSurfaceV621{
 [Serializable]public class Request{public string output,shader_path,folder;public string[] material_guids;public float[] tiles;public Vector4[] views;public int size;}
 [Serializable]public class Frame{public string material_guid,folder;public float tile;public Vector4 view;public string[] planes;public int width,height;public bool source_material_unchanged;}
 [Serializable]public class Report{public string unity_version,graphics_api,gpu,color_space;public Frame[] frames;public bool source_materials_unchanged,full_import=false,mesh_lighting_render_verified=false,host_depth_verified=false;}
 static string Guid(UnityEngine.Object o){string g;long i;if(!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(o,out g,out i))throw new Exception("Identity absent");return g;}
 public static void Export(){
  var args=Environment.GetCommandLineArgs();int p=Array.IndexOf(args,"--openrpg-manifest");if(p<0)throw new Exception("Manifest absent");var r=JsonUtility.FromJson<Request>(File.ReadAllText(args[p+1]));if(File.Exists(r.output))throw new Exception("Preserve evidence");var shader=AssetDatabase.LoadAssetAtPath<Shader>(r.shader_path);if(shader==null||!shader.isSupported)throw new Exception("Owned shader unavailable");var frames=new List<Frame>();
  foreach(string guid in r.material_guids){
   var source=AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));if(source==null||Guid(source.shader)!="7dcd0e3bb8cbf2345a1e124f5a095198")throw new Exception("Source material differs");string before=EditorJsonUtility.ToJson(source,true);
   for(int t=0;t<r.tiles.Length;t++)for(int v=0;v<r.views.Length;v++){
    string folder=Path.Combine(r.folder,guid,"tile-"+t+"-view-"+v);Directory.CreateDirectory(folder);var probe=new Material(shader);probe.CopyPropertiesFromMaterial(source);probe.SetVector("_ProbeTiles",new Vector4(r.tiles[t],r.tiles[t],0.125f,0.25f));probe.SetVector("_ProbeView",r.views[v]);probe.SetVector("_ProbeScale",new Vector4(1,1,0,0));var rt=new RenderTexture(r.size,r.size,0,RenderTextureFormat.ARGBFloat,RenderTextureReadWrite.Linear);rt.Create();var pixels=new Texture2D(r.size,r.size,TextureFormat.RGBAFloat,false,true);var active=RenderTexture.active;var paths=new List<string>();
    try{for(int mode=0;mode<6;mode++){
     string path=Path.Combine(folder,"plane-"+mode+".f32");if(File.Exists(path))throw new Exception("Preserve prior frame");probe.SetFloat("_ProbeMode",mode);Graphics.SetRenderTarget(rt);GL.Clear(false,true,Color.clear);if(!probe.SetPass(0))throw new Exception("Probe pass refused");Graphics.DrawProceduralNow(MeshTopology.Triangles,3,1);RenderTexture.active=rt;pixels.ReadPixels(new Rect(0,0,r.size,r.size),0,0,false);pixels.Apply(false,false);var data=pixels.GetRawTextureData<byte>().ToArray();foreach(var c in pixels.GetPixels())if(float.IsNaN(c.r)||float.IsNaN(c.g)||float.IsNaN(c.b)||float.IsNaN(c.a)||float.IsInfinity(c.r)||float.IsInfinity(c.g)||float.IsInfinity(c.b)||float.IsInfinity(c.a))throw new Exception("Nonfinite fragment output");File.WriteAllBytes(path,data);paths.Add(path);
    }}finally{RenderTexture.active=active;rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(pixels);UnityEngine.Object.DestroyImmediate(probe);}
    if(before!=EditorJsonUtility.ToJson(source,true))throw new Exception("Source changed");frames.Add(new Frame{material_guid=guid,folder=folder,tile=r.tiles[t],view=r.views[v],planes=paths.ToArray(),width=r.size,height=r.size,source_material_unchanged=true});
   }
  }
  File.WriteAllText(r.output,JsonUtility.ToJson(new Report{unity_version=Application.unityVersion,graphics_api=SystemInfo.graphicsDeviceType.ToString(),gpu=SystemInfo.graphicsDeviceName,color_space=QualitySettings.activeColorSpace.ToString(),frames=frames.ToArray(),source_materials_unchanged=true},true));
 }
}
