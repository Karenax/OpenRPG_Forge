"""Read all native prefiltered sky faces/mips from the actual request camera."""
from pathlib import Path
import runpy,sys
loaded=runpy.run_path(str(Path(__file__).with_name('queue_firepit_shadow_reference_v11.py')),run_name='source_sky_probe')
g=loaded['g'];original_helper=g['helper'];popen=loaded['popen']
COMPUTE='''#pragma kernel ReadSkyFaces
Texture2DArray<float4> SourceSkyFaces;
RWStructuredBuffer<float4> Output;
uint Width,Height,LayerCount,Mip;
[numthreads(8,8,1)]
void ReadSkyFaces(uint3 id:SV_DispatchThreadID){
 if(id.x<Width&&id.y<Height&&id.z<LayerCount)
  Output[(id.z*Height+id.y)*Width+id.x]=SourceSkyFaces.Load(int4(id.xy,id.z,Mip));
}
'''
META=r'''
[Serializable] public class SourceSkyMipMetadataV1 {
 public int view,mip,width,height,base_width,base_height,cubemap_count,layer_count,mipmap_count,bytes;
 public string native_texture_format,readback_layout,source_history_channel,raw_sha256;
 public string[] face_order;
 public bool gpu_copy_then_load_readback,source_preexposure_applied,full_import;
}
'''
CAPTURE=r'''
    var skyMethod=skyManager.GetType().GetMethod("GetSkyReflection",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Public,null,new[]{typeof(HDCamera)},null);
    if(skyMethod==null)throw new Exception("Pinned sky reflection method missing");
    var sky=skyMethod.Invoke(skyManager,new object[]{hc}) as CubemapArray;
    if(sky==null||sky.dimension!=TextureDimension.CubeArray||sky.graphicsFormat!=UnityEngine.Experimental.Rendering.GraphicsFormat.R16G16B16A16_SFloat||sky.width!=sky.height||sky.width>2048||sky.width<1||sky.cubemapCount<1||sky.cubemapCount>4)throw new Exception("Unexpected native sky reflection storage");
    if(!BitConverter.IsLittleEndian)throw new Exception("Require declared little endian sky storage");
    var skyCompute=AssetDatabase.LoadAssetAtPath<ComputeShader>("Assets/Editor/SourceSkyReadbackV1.compute");if(skyCompute==null)throw new Exception("Sky compute missing");
    var layers=sky.cubemapCount*6;
    var faces=new Texture2DArray(sky.width,sky.height,layers,sky.graphicsFormat,UnityEngine.Experimental.Rendering.TextureCreationFlags.MipChain);
    try {
     if(faces.mipmapCount!=sky.mipmapCount)throw new Exception("Sky mip layout differs from private copy");
     // SDK SkyManager uses destination element 6*bsdfIdx+face. Preserve that order.
     for(int layer=0;layer<layers;layer++)for(int mip=0;mip<sky.mipmapCount;mip++)Graphics.CopyTexture(sky,layer,mip,faces,layer,mip);
     int kernel=skyCompute.FindKernel("ReadSkyFaces");
     for(int mip=0;mip<sky.mipmapCount;mip++){
      int width=Math.Max(1,sky.width>>mip),height=Math.Max(1,sky.height>>mip);int count=checked(width*height*layers);
      var buffer=new ComputeBuffer(count,16);
      try {
       skyCompute.SetTexture(kernel,"SourceSkyFaces",faces);skyCompute.SetBuffer(kernel,"Output",buffer);skyCompute.SetInt("Width",width);skyCompute.SetInt("Height",height);skyCompute.SetInt("LayerCount",layers);skyCompute.SetInt("Mip",mip);skyCompute.Dispatch(kernel,(width+7)/8,(height+7)/8,layers);
       var values=new Vector4[count];buffer.GetData(values);var bytes=new byte[checked(count*16)];
       for(int i=0;i<count;i++){var v=values[i];if(!float.IsFinite(v.x)||!float.IsFinite(v.y)||!float.IsFinite(v.z)||!float.IsFinite(v.w))throw new Exception("Nonfinite source sky texel");Buffer.BlockCopy(BitConverter.GetBytes(v.x),0,bytes,i*16,4);Buffer.BlockCopy(BitConverter.GetBytes(v.y),0,bytes,i*16+4,4);Buffer.BlockCopy(BitConverter.GetBytes(v.z),0,bytes,i*16+8,4);Buffer.BlockCopy(BitConverter.GetBytes(v.w),0,bytes,i*16+12,4);}
       var path=Path.Combine(Path.GetDirectoryName(request.output),"source-sky-"+view+"-mip-"+mip+".rgba32f");if(File.Exists(path))throw new Exception("Preserve prior sky capture");File.WriteAllBytes(path,bytes);
       var meta=new SourceSkyMipMetadataV1{view=view,mip=mip,width=width,height=height,base_width=sky.width,base_height=sky.height,cubemap_count=sky.cubemapCount,layer_count=layers,mipmap_count=sky.mipmapCount,bytes=bytes.Length,native_texture_format=sky.graphicsFormat.ToString(),readback_layout="BSDF cube then CubemapFace then y then x; linear RGBA32F little endian",source_history_channel="CustomUserHistory0",raw_sha256=Hash(bytes),face_order=Enumerable.Range(0,6).Select(i=>((CubemapFace)i).ToString()).ToArray(),gpu_copy_then_load_readback=true,source_preexposure_applied=false,full_import=false};
       File.WriteAllText(path+".json",JsonUtility.ToJson(meta,true));
      }finally{buffer.Release();}
     }
    }finally{UnityEngine.Object.DestroyImmediate(faces);}
'''
def helper():
    text=original_helper().replace('SourceShadowReferenceV11','SourceSkyReferenceV1')
    compute=g['PROJECT']/'Assets/Editor/SourceSkyReadbackV1.compute';assert not compute.exists()
    compute.write_text(COMPUTE,'utf-8')
    sdk=g['PROJECT']/'Library/PackageCache/com.unity.render-pipelines.high-definition@fc55a8abadc2/Runtime/Sky/SkyManager.cs'
    g['save']('COMPUTE-PREPARED.v1.json',dict(path=str(compute),sha256=g['sha'](compute),sdk=str(sdk),sdk_sha256=g['sha'](sdk)))
    marker='var packedProbe=new Vector4[7];actualBuffer.GetData(packedProbe);';assert text.count(marker)==1
    return text.replace(marker,marker+'\n'+CAPTURE)+'\n'+META
def corrected_popen(args,*a,**kw):
    if isinstance(args,list) and args and str(args[0])==str(g['UNITY']):
        args=list(args);i=args.index('-executeMethod')+1;assert args[i]=='OpenRPGSourceShadowReferenceV8.Export';args[i]='OpenRPGSourceSkyReferenceV1.Export'
    return popen(args,*a,**kw)
g['subprocess'].Popen=corrected_popen
g.update(ROOT=g['W']/'firepit-source-sky-reference-v1',HELPER=g['PROJECT']/'Assets/Editor/SourceSkyReferenceV1.cs',helper=helper,__file__=str(Path(__file__).resolve()))
if __name__=='__main__':
    if sys.argv[-1]=='prepare':g['prepare']()
    else:
        compute=g['read'](g['ROOT']/'COMPUTE-PREPARED.v1.json');assert g['sha'](compute['path'])==compute['sha256']
        g['run']();state=g['read'](g['ROOT']/'RUN-RESULT.v1.json')
        files=sorted(g['ROOT'].glob('source-sky-*.rgba32f*'))
        if state.get('actual_exit_code')==0 and files and g['sha'](compute['path'])==compute['sha256']:
            state.update(status='native_prefiltered_sky_cube_array_gpu_captured',sky_files={str(p):g['sha'](p) for p in files},compute_input=compute)
        else:state.update(status='source_prefiltered_sky_capture_refused')
        g['save']('RUN-RESULT.v1.json',state)
