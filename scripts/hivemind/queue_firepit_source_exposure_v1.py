"""Read HDRP's actual exposure texture for each CustomUserHistory0 capture."""
from pathlib import Path
import runpy
import sys

BASE=Path(__file__).with_name('queue_firepit_shadow_reference_v11.py')
loaded=runpy.run_path(str(BASE),run_name='source_exposure_probe')
g=loaded['g'];original_helper=g['helper'];popen=loaded['popen']
COMPUTE='''#pragma kernel ReadExposure
Texture2D<float4> SourceExposure;
RWStructuredBuffer<float4> Output;
[numthreads(1,1,1)]
void ReadExposure(uint3 id:SV_DispatchThreadID){Output[0]=SourceExposure.Load(int3(0,0,0));}
'''
CAPTURE=r'''
    var exposureMethod=typeof(HDRenderPipeline).GetMethod("GetExposureTexture",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic,null,new[]{typeof(HDCamera)},null);
    if(exposureMethod==null)throw new Exception("Pinned HDRP exposure method missing");
    var exposureHandle=(RTHandle)exposureMethod.Invoke(currentPipeline,new object[]{hc});
    if(exposureHandle==null||exposureHandle.rt==null)throw new Exception("Actual camera exposure texture missing");
    var exposureTexture=exposureHandle.rt;
    if(exposureTexture.width!=1||exposureTexture.height!=1||exposureTexture.dimension!=TextureDimension.Tex2D||exposureTexture.graphicsFormat!=UnityEngine.Experimental.Rendering.GraphicsFormat.R32G32_SFloat)throw new Exception("Unexpected HDRP exposure texture layout");
    var compressionProperty=typeof(HDCamera).GetProperty("probeRangeCompressionFactor",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
    if(compressionProperty==null)throw new Exception("Pinned HDRP probe compression property missing");
    float compression=(float)compressionProperty.GetValue(hc);if(!float.IsFinite(compression))throw new Exception("Nonfinite probe compression");
    var exposureCompute=AssetDatabase.LoadAssetAtPath<ComputeShader>("Assets/Editor/SourceExposureReadbackV1.compute");if(exposureCompute==null)throw new Exception("Exposure compute missing");
    var exposureKernel=exposureCompute.FindKernel("ReadExposure");var exposureBuffer=new ComputeBuffer(1,16);
    try {
     exposureCompute.SetTexture(exposureKernel,"SourceExposure",exposureTexture);exposureCompute.SetBuffer(exposureKernel,"Output",exposureBuffer);exposureCompute.Dispatch(exposureKernel,1,1,1);
     var values=new Vector4[1];exposureBuffer.GetData(values);var value=values[0];if(!float.IsFinite(value.x)||!float.IsFinite(value.y)||value.x<=0)throw new Exception("Invalid measured exposure");
     if(!BitConverter.IsLittleEndian)throw new Exception("Require declared little endian exposure storage");
     var bytes=new byte[16];Buffer.BlockCopy(BitConverter.GetBytes(value.x),0,bytes,0,4);Buffer.BlockCopy(BitConverter.GetBytes(value.y),0,bytes,4,4);Buffer.BlockCopy(BitConverter.GetBytes(value.z),0,bytes,8,4);Buffer.BlockCopy(BitConverter.GetBytes(value.w),0,bytes,12,4);
     var exposurePath=Path.Combine(Path.GetDirectoryName(request.output),"source-exposure-"+view+".rgba32f");if(File.Exists(exposurePath))throw new Exception("Preserve prior exposure readback");File.WriteAllBytes(exposurePath,bytes);
     var meta="{\"width\":1,\"height\":1,\"gpu_load_readback\":true,\"native_texture_format\":\""+exposureTexture.graphicsFormat+"\",\"source_history_channel\":\"CustomUserHistory0\",\"probe_range_compression_factor\":"+compression.ToString("R",System.Globalization.CultureInfo.InvariantCulture)+",\"pre_exposition_shader_option\":1,\"raw_sha256\":\""+Hash(bytes)+"\",\"bytes\":16,\"full_import\":false}";
     File.WriteAllText(exposurePath+".json",meta);
    }finally {exposureBuffer.Release();}
'''


def helper():
    text=original_helper().replace('SourceShadowReferenceV11','SourceExposureReferenceV1')
    config=g['PROJECT']/'Library/PackageCache/com.unity.render-pipelines.high-definition-config@77f033cbf9c5/Runtime/ShaderConfig.cs.hlsl'
    assert '#define SHADEROPTIONS_PRE_EXPOSITION (1)' in config.read_text('utf-8')
    compute=g['PROJECT']/'Assets/Editor/SourceExposureReadbackV1.compute'
    assert not compute.exists()
    compute.write_text(COMPUTE,'utf-8')
    g['save']('COMPUTE-PREPARED.v1.json',dict(path=str(compute),sha256=g['sha'](compute),shader_config=str(config),shader_config_sha256=g['sha'](config)))
    needle='var packedProbe=new Vector4[7];actualBuffer.GetData(packedProbe);'
    assert text.count(needle)==1
    return text.replace(needle,needle+'\n'+CAPTURE)


def corrected_popen(args,*a,**kw):
    if isinstance(args,list) and args and str(args[0])==str(g['UNITY']):
        args=list(args);i=args.index('-executeMethod')+1
        assert args[i]=='OpenRPGSourceShadowReferenceV8.Export'
        args[i]='OpenRPGSourceExposureReferenceV1.Export'
    return popen(args,*a,**kw)


g['subprocess'].Popen=corrected_popen
g.update(ROOT=g['W']/'firepit-source-exposure-reference-v1',
    HELPER=g['PROJECT']/'Assets/Editor/SourceExposureReferenceV1.cs',helper=helper,
    __file__=str(Path(__file__).resolve()))
if __name__=='__main__':
    if sys.argv[-1]=='prepare':
        g['prepare']()
    else:
        compute=g['read'](g['ROOT']/'COMPUTE-PREPARED.v1.json')
        assert g['sha'](compute['path'])==compute['sha256']
        assert g['sha'](compute['shader_config'])==compute['shader_config_sha256']
        g['run']();state=g['read'](g['ROOT']/'RUN-RESULT.v1.json')
        files=[g['ROOT']/f'source-exposure-{view}.rgba32f{suffix}' for view in range(2) for suffix in ['', '.json']]
        if state.get('actual_exit_code')==0 and all(p.exists() for p in files) and g['sha'](compute['path'])==compute['sha256']:
            state.update(status='native_hdrp_actual_camera_exposure_gpu_captured',exposure_files={str(p):g['sha'](p) for p in files},compute_input=compute)
        else:
            state.update(status='native_hdrp_exposure_capture_refused',missing_exposure_files=[str(p) for p in files if not p.exists()])
        g['save']('RUN-RESULT.v1.json',state)
