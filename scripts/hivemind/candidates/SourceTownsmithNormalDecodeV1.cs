using System;
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
public static class OpenRPGSourceTownsmithNormalDecodeV1 {
 [Serializable] public class InputRow {public string guid;public int authored_texture_index,normal_profile_index;}
 [Serializable] public class Input {public InputRow[] rows;}
 [Serializable] public class Row {
  public string guid,asset_path,graphics_format,settings_before,settings_after,samples_file,error;
  public long file_id;
  public int authored_texture_index,normal_profile_index,width,height,mip_count,sample_count,shader_unpack_mode;
  public bool settings_unchanged;
 }
 [Serializable] public class Result {public string status,unity_version,graphics_api;public bool full_import=false,forge_gpu_equivalence_qualified=false;public Row[] rows;}
 public static void Export() {
  string root=Environment.GetEnvironmentVariable("OPENRPG_NORMAL_OUT");
  if(!BitConverter.IsLittleEndian)throw new Exception("Little endian required");
  var input=JsonUtility.FromJson<Input>(File.ReadAllText(Path.Combine(root,"INPUT.v1.json")));
  var shader=AssetDatabase.LoadAssetAtPath<ComputeShader>("Assets/Editor/SourceNormalMipDecodeV1.compute");
  if(shader==null)throw new Exception("SDK normal probe unavailable");
  var rows=new List<Row>();
  foreach(var item in input.rows) {
   var row=new Row{guid=item.guid,authored_texture_index=item.authored_texture_index,normal_profile_index=item.normal_profile_index};
   try {
    row.asset_path=AssetDatabase.GUIDToAssetPath(item.guid);
    var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(row.asset_path);
    var importer=AssetImporter.GetAtPath(row.asset_path) as TextureImporter;
    if(texture==null||importer==null||texture.isReadable)throw new Exception("Original unreadable texture required");
    if(!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(texture,out string guid,out long fileId)||guid!=item.guid)throw new Exception("Source identity differs");
    row.file_id=fileId;row.settings_before=EditorJsonUtility.ToJson(importer);
    row.graphics_format=texture.graphicsFormat.ToString();row.width=texture.width;row.height=texture.height;row.mip_count=texture.mipmapCount;
    var coordinates=new List<uint>();
    for(int mip=0;mip<row.mip_count;mip++) {
     int w=Math.Max(1,row.width>>mip),h=Math.Max(1,row.height>>mip);
     foreach(var p in new[]{new Vector2Int(0,0),new Vector2Int(w-1,0),new Vector2Int(0,h-1),new Vector2Int(w-1,h-1),new Vector2Int(w/2,h/2),new Vector2Int(w/4,h/4),new Vector2Int(3*w/4,3*h/4),new Vector2Int(w/2,0),new Vector2Int(0,h/2)})
      coordinates.AddRange(new uint[]{(uint)p.x,(uint)p.y,(uint)mip,0});
    }
    row.sample_count=coordinates.Count/4;
    using(var source=new ComputeBuffer(row.sample_count,16))using(var output=new ComputeBuffer(row.sample_count*3,16)) {
     source.SetData(coordinates.ToArray());int kernel=shader.FindKernel("Capture");
     shader.SetTexture(kernel,"SourceTexture",texture);shader.SetBuffer(kernel,"Coordinates",source);shader.SetBuffer(kernel,"Values",output);shader.SetInt("SampleCount",row.sample_count);
     shader.Dispatch(kernel,(row.sample_count+63)/64,1,1);
     var vectors=new Vector4[row.sample_count*3];output.GetData(vectors);row.shader_unpack_mode=(int)vectors[2].x;
     var values=new float[vectors.Length*4];
     for(int i=0;i<vectors.Length;i++) {
      if(i%3==2 && (vectors[i].x!=row.shader_unpack_mode||vectors[i].y!=0||vectors[i].z!=0||vectors[i].w!=0))throw new Exception("Native mode observation inconsistent");
      values[4*i]=vectors[i].x;values[4*i+1]=vectors[i].y;values[4*i+2]=vectors[i].z;values[4*i+3]=vectors[i].w;
     }
     var bytes=new byte[values.Length*4];Buffer.BlockCopy(values,0,bytes,0,bytes.Length);
     row.samples_file=Path.Combine(root,"normal-"+row.normal_profile_index+".packed-decoded-mode.float32");File.WriteAllBytes(row.samples_file,bytes);
    }
    row.settings_after=EditorJsonUtility.ToJson(importer);row.settings_unchanged=row.settings_before==row.settings_after;
    if(!row.settings_unchanged)throw new Exception("Importer changed during read-only capture");
   } catch(Exception e){row.error=e.ToString();}
   rows.Add(row);
  }
  bool complete=true;foreach(var row in rows)if(!string.IsNullOrEmpty(row.error)||!row.settings_unchanged)complete=false;
  var result=new Result{status=complete?"native_normal_decode_capture_complete":"native_normal_decode_capture_refusals_retained",unity_version=Application.unityVersion,graphics_api=SystemInfo.graphicsDeviceType.ToString(),rows=rows.ToArray()};
  File.WriteAllText(Path.Combine(root,"NATIVE-RESULT.v1.json"),JsonUtility.ToJson(result,true));
 }
}
