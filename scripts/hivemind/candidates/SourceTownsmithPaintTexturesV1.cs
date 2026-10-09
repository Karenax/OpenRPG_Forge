using System;
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public static class OpenRPGSourceTownsmithPaintTexturesV1 {
 [Serializable] public class InputRow { public string guid; public int authored_texture_index; }
 [Serializable] public class Input { public InputRow[] rows; }
 [Serializable] public class Mip { public int level,width,height,bytes; public string file,error; public bool native_readback_ok; }
 [Serializable] public class Row {
  public string guid,asset_path,format,graphics_format,filter_mode,wrap_u,wrap_v,wrap_w,texture_type,compression,platform_format;
  public int authored_texture_index,width,height,mipmap_count,aniso_level,compression_quality;
  public float mip_map_bias;
  public bool source_readable,srgb,mipmap_enabled,convert_to_normal,platform_overridden;
  public Mip[] mips;
 }
 [Serializable] public class Result {
  public string status,unity_version,graphics_device,graphics_api;
  public bool async_readback_supported,native_readback_complete,source_importer_modified=false,asset_render_qualified=false,full_import=false;
  public Row[] rows;
 }
 public static void Export() {
  string root=Environment.GetEnvironmentVariable("OPENRPG_PAINT_OUT");
  if(string.IsNullOrEmpty(root)||File.Exists(Path.Combine(root,"NATIVE-RESULT.v1.json")))throw new Exception("Fresh output required");
  var input=JsonUtility.FromJson<Input>(File.ReadAllText(Path.Combine(root,"TEXTURES-INPUT.v1.json")));
  if(input.rows==null||input.rows.Length!=10)throw new Exception("Exactly ten active source textures required");
  var rows=new List<Row>();var textures=new List<Texture2D>();var ids=new HashSet<string>();
  foreach(var item in input.rows) {
   if(!ids.Add(item.guid))throw new Exception("Repeated source GUID");
   string path=AssetDatabase.GUIDToAssetPath(item.guid);
   var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(path);
   var importer=AssetImporter.GetAtPath(path) as TextureImporter;
   if(texture==null||importer==null||!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(texture,out string guid,out long id)||guid!=item.guid)throw new Exception("Native texture identity unresolved");
   var platform=importer.GetPlatformTextureSettings("Standalone");
   rows.Add(new Row{guid=guid,asset_path=path,authored_texture_index=item.authored_texture_index,
    width=texture.width,height=texture.height,mipmap_count=texture.mipmapCount,format=texture.format.ToString(),graphics_format=texture.graphicsFormat.ToString(),
    filter_mode=texture.filterMode.ToString(),wrap_u=texture.wrapModeU.ToString(),wrap_v=texture.wrapModeV.ToString(),wrap_w=texture.wrapModeW.ToString(),
    aniso_level=texture.anisoLevel,mip_map_bias=texture.mipMapBias,source_readable=texture.isReadable,
    srgb=importer.sRGBTexture,mipmap_enabled=importer.mipmapEnabled,convert_to_normal=importer.convertToNormalmap,texture_type=importer.textureType.ToString(),
    compression=importer.textureCompression.ToString(),compression_quality=importer.compressionQuality,platform_overridden=platform.overridden,platform_format=platform.format.ToString()});
   textures.Add(texture);
  }
  var result=new Result{status="ten_native_imported_texture_profiles_captured_storage_pending",unity_version=Application.unityVersion,
   graphics_device=SystemInfo.graphicsDeviceName,graphics_api=SystemInfo.graphicsDeviceType.ToString(),async_readback_supported=SystemInfo.supportsAsyncGPUReadback,rows=rows.ToArray()};
  string output=Path.Combine(root,"NATIVE-RESULT.v1.json");
  File.WriteAllText(output,JsonUtility.ToJson(result,true));
  bool complete=SystemInfo.supportsAsyncGPUReadback;
  for(int i=0;i<rows.Count;i++) {
   var row=rows[i];var texture=textures[i];var mips=new List<Mip>();
   for(int level=0;level<texture.mipmapCount;level++) {
    var mip=new Mip{level=level,width=Math.Max(1,texture.width>>level),height=Math.Max(1,texture.height>>level)};
    try {
     if(!SystemInfo.supportsAsyncGPUReadback)throw new Exception("Async native readback unavailable");
     var request=AsyncGPUReadback.Request(texture,level);
     request.WaitForCompletion();
     if(request.hasError)throw new Exception("Native-format mip readback refused");
     byte[] data=request.GetData<byte>().ToArray();
     if(data.Length==0)throw new Exception("Empty native-format mip");
     mip.file=Path.Combine(root,"t"+row.authored_texture_index.ToString("D2")+"-mip"+level.ToString("D2")+".native.bin");
     File.WriteAllBytes(mip.file,data);mip.bytes=data.Length;mip.native_readback_ok=true;
    } catch(Exception e) {mip.error=e.ToString();complete=false;}
    mips.Add(mip);
   }
   row.mips=mips.ToArray();File.WriteAllText(output,JsonUtility.ToJson(result,true));
  }
  result.native_readback_complete=complete;
  result.status=complete?"ten_native_imported_texture_profiles_and_all_mips_captured":"ten_native_imported_texture_profiles_captured_native_storage_refusals_retained";
  File.WriteAllText(output,JsonUtility.ToJson(result,true));
 }
}
