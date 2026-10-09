using System;
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class OpenRPGSourceTownsmithCompressedStorageV1 {
 [Serializable] public class InputRow { public string guid; public int authored_texture_index; }
 [Serializable] public class Input { public InputRow[] rows; }
 [Serializable] public class Mip { public int level,width,height,bytes; public string file; }
 [Serializable] public class Row {
  public string guid,asset_path,graphics_format,format,settings_before,settings_restored,whole_file,source_samples,readable_samples,error;
  public int authored_texture_index,width,height,mip_count,whole_bytes,sample_count,filter_mode,wrap_u,wrap_v,wrap_w,aniso;
  public float mip_bias;
  public bool private_readability_override,settings_restored_equal,gpu_samples_bit_exact;
  public Mip[] mips;
 }
 [Serializable] public class Result {
  public string status,unity_version,graphics_api;
  public bool original_sources_modified=false,full_import=false,full_texture_sampling_fidelity_qualified=false;
  public Row[] rows;
 }
 static byte[] Samples(Texture2D texture,ComputeShader shader,out int sampleCount) {
  var coordinates=new List<uint>();
  for(int mip=0;mip<texture.mipmapCount;mip++) {
   int w=Math.Max(1,texture.width>>mip),h=Math.Max(1,texture.height>>mip);
   foreach(var p in new[]{new Vector2Int(0,0),new Vector2Int(w-1,0),new Vector2Int(0,h-1),new Vector2Int(w-1,h-1),new Vector2Int(w/2,h/2),new Vector2Int(w/4,h/4),new Vector2Int(3*w/4,3*h/4),new Vector2Int(w/2,0),new Vector2Int(0,h/2)})
    coordinates.AddRange(new uint[]{(uint)p.x,(uint)p.y,(uint)mip,0});
  }
  sampleCount=coordinates.Count/4;
  using(var input=new ComputeBuffer(sampleCount,16))using(var output=new ComputeBuffer(sampleCount,16)) {
   input.SetData(coordinates.ToArray());int kernel=shader.FindKernel("Capture");
   shader.SetTexture(kernel,"SourceTexture",texture);shader.SetBuffer(kernel,"Coordinates",input);shader.SetBuffer(kernel,"Values",output);shader.SetInt("SampleCount",sampleCount);
   shader.Dispatch(kernel,(sampleCount+63)/64,1,1);
   Vector4[] vectors=new Vector4[sampleCount];output.GetData(vectors);
   float[] values=new float[sampleCount*4];for(int i=0;i<sampleCount;i++){values[4*i]=vectors[i].x;values[4*i+1]=vectors[i].y;values[4*i+2]=vectors[i].z;values[4*i+3]=vectors[i].w;}
   byte[] bytes=new byte[values.Length*4];Buffer.BlockCopy(values,0,bytes,0,bytes.Length);return bytes;
  }
 }
 static bool Equal(byte[] a,byte[] b) {if(a.Length!=b.Length)return false;for(int i=0;i<a.Length;i++)if(a[i]!=b[i])return false;return true;}
 public static void Export() {
  if(!BitConverter.IsLittleEndian)throw new Exception("Little endian capture required");
  string root=Environment.GetEnvironmentVariable("OPENRPG_COMPRESSED_OUT");
  var input=JsonUtility.FromJson<Input>(File.ReadAllText(Path.Combine(root,"INPUT.v1.json")));
  if(input.rows.Length!=8)throw new Exception("Eight compressed textures required");
  var shader=AssetDatabase.LoadAssetAtPath<ComputeShader>("Assets/Editor/SourceImportedTextureMipLoadV1.compute");
  if(shader==null)throw new Exception("Mip sampling probe unavailable");
  var rows=new List<Row>();var result=new Result{status="isolated_compressed_texture_capture_running",unity_version=Application.unityVersion,graphics_api=SystemInfo.graphicsDeviceType.ToString()};
  foreach(var item in input.rows) {
   var row=new Row{guid=item.guid,authored_texture_index=item.authored_texture_index};TextureImporter importer=null;string originalJson=null;
   try {
    row.asset_path=AssetDatabase.GUIDToAssetPath(item.guid);
    var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(row.asset_path);importer=AssetImporter.GetAtPath(row.asset_path) as TextureImporter;
    if(texture==null||importer==null||texture.isReadable||importer.isReadable)throw new Exception("Exact unreadable imported source required");
    if(!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(texture,out string guid,out long id)||guid!=item.guid)throw new Exception("Original GUID differs");
    originalJson=EditorJsonUtility.ToJson(importer);row.settings_before=originalJson;
    row.graphics_format=texture.graphicsFormat.ToString();row.format=texture.format.ToString();row.width=texture.width;row.height=texture.height;row.mip_count=texture.mipmapCount;
    if(row.format!="DXT1"&&row.format!="DXT5")throw new Exception("Compressed format differs");
    row.filter_mode=(int)texture.filterMode;row.wrap_u=(int)texture.wrapModeU;row.wrap_v=(int)texture.wrapModeV;row.wrap_w=(int)texture.wrapModeW;row.aniso=texture.anisoLevel;row.mip_bias=texture.mipMapBias;
    byte[] before=Samples(texture,shader,out row.sample_count);row.source_samples=Path.Combine(root,"t"+item.authored_texture_index.ToString("D2")+"-source.samples.float32");File.WriteAllBytes(row.source_samples,before);
    importer.isReadable=true;row.private_readability_override=true;importer.SaveAndReimport();texture=AssetDatabase.LoadAssetAtPath<Texture2D>(row.asset_path);
    if(texture.graphicsFormat.ToString()!=row.graphics_format||texture.width!=row.width||texture.height!=row.height||texture.mipmapCount!=row.mip_count||texture.filterMode!=(FilterMode)row.filter_mode||texture.wrapModeU!=(TextureWrapMode)row.wrap_u||texture.wrapModeV!=(TextureWrapMode)row.wrap_v||texture.wrapModeW!=(TextureWrapMode)row.wrap_w||texture.anisoLevel!=row.aniso||texture.mipMapBias!=row.mip_bias)throw new Exception("Readable import changed native resource profile");
    byte[] after=Samples(texture,shader,out int count);row.readable_samples=Path.Combine(root,"t"+item.authored_texture_index.ToString("D2")+"-readable.samples.float32");File.WriteAllBytes(row.readable_samples,after);
    row.gpu_samples_bit_exact=count==row.sample_count&&Equal(before,after);if(!row.gpu_samples_bit_exact)throw new Exception("Readability reimport changed sampled texels");
    byte[] whole=texture.GetRawTextureData<byte>().ToArray();row.whole_bytes=whole.Length;row.whole_file=Path.Combine(root,"t"+item.authored_texture_index.ToString("D2")+"-whole.native.bin");File.WriteAllBytes(row.whole_file,whole);
    var mips=new List<Mip>();int sum=0;
    for(int level=0;level<row.mip_count;level++) {
     byte[] bytes=texture.GetPixelData<byte>(level).ToArray();int w=Math.Max(1,row.width>>level),h=Math.Max(1,row.height>>level);
     if(bytes.Length!=Math.Max(1,(w+3)/4)*Math.Max(1,(h+3)/4)*(row.format=="DXT1"?8:16))throw new Exception("Native compressed block length differs");
     string file=Path.Combine(root,"t"+item.authored_texture_index.ToString("D2")+"-mip"+level.ToString("D2")+".native.bin");File.WriteAllBytes(file,bytes);sum+=bytes.Length;
     mips.Add(new Mip{level=level,width=w,height=h,bytes=bytes.Length,file=file});
    }
    if(sum!=whole.Length)throw new Exception("Mip chain does not cover native storage");row.mips=mips.ToArray();
   } catch(Exception e) {row.error=e.ToString();}
   finally {
    if(importer!=null&&originalJson!=null&&row.private_readability_override) {
     EditorJsonUtility.FromJsonOverwrite(originalJson,importer);importer.SaveAndReimport();
     row.settings_restored=EditorJsonUtility.ToJson(importer);row.settings_restored_equal=row.settings_restored==row.settings_before;
    }
   }
   rows.Add(row);result.rows=rows.ToArray();File.WriteAllText(Path.Combine(root,"NATIVE-RESULT.v1.json"),JsonUtility.ToJson(result,true));
  }
  bool complete=true;foreach(var row in rows)if(!string.IsNullOrEmpty(row.error)||!row.settings_restored_equal||!row.gpu_samples_bit_exact||row.mips==null)complete=false;
  result.status=complete?"eight_compressed_native_mip_chains_extracted_in_isolated_readable_copy":"isolated_compressed_texture_extraction_refusals_retained";
  File.WriteAllText(Path.Combine(root,"NATIVE-RESULT.v1.json"),JsonUtility.ToJson(result,true));
 }
}
