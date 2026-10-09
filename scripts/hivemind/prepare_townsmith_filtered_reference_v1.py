from pathlib import Path
S=Path(__file__).parent
def main():
 t=(S/'candidates/SourceNormalMipDecodeV1.compute').read_text(encoding='utf-8')
 t=t.replace('StructuredBuffer<uint4> Coordinates;','SamplerState sampler_SourceTexture;\nStructuredBuffer<float4> Coordinates;')
 t=t.replace('uint4 p=Coordinates[id.x];','float4 p=Coordinates[id.x];').replace('SourceTexture.Load(int3(p.xy,p.z))','SourceTexture.SampleLevel(sampler_SourceTexture,p.xy,p.z)')
 (S/'candidates/SourceFilteredTextureV1.compute').write_text(t,encoding='utf-8')
 t=(S/'candidates/SourceTownsmithNormalDecodeV1.cs').read_text(encoding='utf-8')
 t=t.replace('SourceTownsmithNormalDecodeV1','SourceTownsmithFilteredTextureV1').replace('SourceNormalMipDecodeV1.compute','SourceFilteredTextureV1.compute').replace('OPENRPG_NORMAL_OUT','OPENRPG_FILTERED_OUT')
 t=t.replace('native_normal_decode_capture','native_filtered_texture_capture')
 start=t.index('    var coordinates=new List<uint>();');end=t.index('    row.sample_count=coordinates.Count/4;',start)
 t=t[:start]+'''    var coordinates=new List<float>();
    for(int mip=0;mip<row.mip_count;mip++) {
     foreach(var p in new[]{new Vector2(-0.125f,0.375f),new Vector2(0,0),new Vector2(0.125f,0.875f),new Vector2(0.49937f,0.50391f),new Vector2(1,1),new Vector2(1.125f,-0.125f),new Vector2(0.99975f,0.00025f),new Vector2(-1.375f,2.625f),new Vector2(0.333333f,0.777777f)})
      coordinates.AddRange(new float[]{p.x,p.y,mip+0.25f,0});
    }
'''+t[end:]
 t=t.replace('"normal-"+row.normal_profile_index+".packed-decoded-mode.float32"','"texture-"+row.authored_texture_index.ToString("D2")+".filtered-decoded-mode.float32"')
 (S/'candidates/SourceTownsmithFilteredTextureV1.cs').write_text(t,encoding='utf-8')
 q=(S/'queue_townsmith_normal_decode_v1.py').read_text(encoding='utf-8')
 q=q.replace('townsmith-s-paint-native-normal-decode-v1','townsmith-s-paint-native-filtered-reference-v1').replace('SourceTownsmithNormalDecodeV1','SourceTownsmithFilteredTextureV1').replace('SourceNormalMipDecodeV1.compute','SourceFilteredTextureV1.compute').replace('OPENRPG_NORMAL_OUT','OPENRPG_FILTERED_OUT').replace('native_normal_decode_capture','native_filtered_texture_capture')
 q=q.replace("selected=[r for r in inventory['rows'] if r['normal_profile_index'] is not None];assert sorted(r['normal_profile_index'] for r in selected)==[0,1,2]","selected=inventory['rows'];assert len(selected)==10")
 anchor="  target=PROJECT/row['source_path'];assert sha(target)==row['source_sha256'];"
 assert q.count(anchor)==1
 q=q.replace(anchor,"""  target=PROJECT/row['source_path']
  if not target.exists():
   private=W/'unity-dungeon-original-shadergraph-hdrp-startup-v4/project'/row['source_path']
   assert sha(private)==row['source_sha256'];target.parent.mkdir(parents=True,exist_ok=True)
   shutil.copy2(private,target);private_meta=private.with_suffix(private.suffix+'.meta');shutil.copy2(private_meta,target.with_suffix(target.suffix+'.meta'))
   pins[str(private)]=sha(private);pins[str(private_meta)]=sha(private_meta)
  assert sha(target)==row['source_sha256'];""")
 q=q.replace("'normal_profile_index':r['normal_profile_index']","'normal_profile_index':r['normal_profile_index'] if r['normal_profile_index'] is not None else -1")
 q=q.replace("len(native['rows'])==3","len(native['rows'])==10")
 (S/'queue_townsmith_filtered_reference_v1.py').write_text(q,encoding='utf-8')
 print('Filtered source helpers and isolated Unity controller prepared')
if __name__=='__main__':main()
