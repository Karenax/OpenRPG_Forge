"""Read the actual HDRP preintegrated BRDF texture through GPU Load."""
from pathlib import Path
import runpy,sys
BASE=Path(__file__).with_name('queue_firepit_shadow_reference_v11.py')
loaded=runpy.run_path(str(BASE),run_name='source_fgd_probe')
g=loaded['g'];original_helper=g['helper'];popen=loaded['popen']
COMPUTE='''#pragma kernel ReadFgd
Texture2D<float4> SourceFgd;
RWStructuredBuffer<float4> Output;
uint Width,Height;
[numthreads(8,8,1)]
void ReadFgd(uint3 id:SV_DispatchThreadID){if(id.x<Width&&id.y<Height)Output[id.y*Width+id.x]=SourceFgd.Load(int3(id.xy,0));}
'''
CAPTURE=r'''
   var fgd=Shader.GetGlobalTexture("_PreIntegratedFGD_GGXDisneyDiffuse");
   if(fgd==null||fgd.dimension!=TextureDimension.Tex2D||fgd.width!=64||fgd.height!=64)throw new Exception("Unexpected native FGD texture");
   var compute=AssetDatabase.LoadAssetAtPath<ComputeShader>("Assets/Editor/SourceFgdReadbackV1.compute");if(compute==null)throw new Exception("FGD readback compute missing");
   var kernel=compute.FindKernel("ReadFgd");var buffer=new ComputeBuffer(fgd.width*fgd.height,16);
   try{
    compute.SetTexture(kernel,"SourceFgd",fgd);compute.SetBuffer(kernel,"Output",buffer);compute.SetInt("Width",fgd.width);compute.SetInt("Height",fgd.height);compute.Dispatch(kernel,8,8,1);
    var values=new Vector4[fgd.width*fgd.height];buffer.GetData(values);if(!BitConverter.IsLittleEndian)throw new Exception("Require declared little endian FGD storage");
    var bytes=new byte[values.Length*16];for(int i=0;i<values.Length;i++){var v=values[i];Buffer.BlockCopy(BitConverter.GetBytes(v.x),0,bytes,i*16,4);Buffer.BlockCopy(BitConverter.GetBytes(v.y),0,bytes,i*16+4,4);Buffer.BlockCopy(BitConverter.GetBytes(v.z),0,bytes,i*16+8,4);Buffer.BlockCopy(BitConverter.GetBytes(v.w),0,bytes,i*16+12,4);}
    var path=Path.Combine(Path.GetDirectoryName(request.output),"source-fgd.rgba32f");if(File.Exists(path))throw new Exception("Preserve FGD readback");File.WriteAllBytes(path,bytes);
    File.WriteAllText(path+".json","{\"width\":64,\"height\":64,\"gpu_load_readback\":true,\"native_texture_format\":\""+fgd.graphicsFormat+"\",\"raw_sha256\":\""+Hash(bytes)+"\",\"bytes\":"+bytes.Length+",\"diffuse_fgd_decode\":\"blue plus 0.5\",\"full_import\":false}");
   }finally{buffer.Release();}
'''
def helper():
    text=original_helper().replace('SourceShadowReferenceV11','SourceFgdReferenceV1')
    compute_path=g['PROJECT']/'Assets/Editor/SourceFgdReadbackV1.compute'
    assert not compute_path.exists()
    compute_path.write_text(COMPUTE,'utf-8')
    g['save']('COMPUTE-PREPARED.v1.json',dict(path=str(compute_path),sha256=g['sha'](compute_path),gpu_sampling='Texture2D.Load on the native view; no CPU gamma conversion'))
    needle='   if(sourceBefore!=EditorJsonUtility.ToJson(source,true))';assert text.count(needle)==1
    return text.replace(needle,CAPTURE+'\n'+needle)
def corrected_popen(args,*a,**kw):
    if isinstance(args,list) and args and str(args[0])==str(g['UNITY']):
        args=list(args);i=args.index('-executeMethod')+1;assert args[i]=='OpenRPGSourceShadowReferenceV8.Export';args[i]='OpenRPGSourceFgdReferenceV1.Export'
    return popen(args,*a,**kw)
g['subprocess'].Popen=corrected_popen
g.update(ROOT=g['W']/'firepit-source-fgd-reference-v1',HELPER=g['PROJECT']/'Assets/Editor/SourceFgdReferenceV1.cs',helper=helper,__file__=str(Path(__file__).resolve()))
if __name__=='__main__':
    if sys.argv[-1]=='prepare':g['prepare']()
    else:
        compute=g['read'](g['ROOT']/'COMPUTE-PREPARED.v1.json');assert g['sha'](compute['path'])==compute['sha256']
        g['run']();state=g['read'](g['ROOT']/'RUN-RESULT.v1.json');raw=g['ROOT']/'source-fgd.rgba32f';meta=raw.with_suffix(raw.suffix+'.json')
        if state.get('actual_exit_code')==0 and raw.exists() and meta.exists() and g['sha'](compute['path'])==compute['sha256']:
            state.update(status='native_hdrp_fgd_gpu_load_captured',fgd_files={str(p):g['sha'](p) for p in [raw,meta]},compute_input=compute)
        else:state.update(status='native_hdrp_fgd_capture_refused',fgd_raw_present=raw.exists(),fgd_metadata_present=meta.exists())
        g['save']('RUN-RESULT.v1.json',state)
