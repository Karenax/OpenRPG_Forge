"""Measure native cube sampling on axes, seams, corners and fractional LODs."""
from pathlib import Path
import itertools,runpy,sys
loaded=runpy.run_path(str(Path(__file__).with_name('queue_firepit_source_sky_reference_v1.py')),run_name='sky_sampling_source')
g=loaded['g'];original_helper=loaded['original_helper'];popen=loaded['popen']
DIRECTIONS=[list(v) for v in itertools.product([-1,0,1],repeat=3) if any(v)]
LODS=[0.0,0.25,1.5,3.75,5.5,5.99,6.0]
COMPUTE=loaded['COMPUTE']+'''
#pragma kernel SampleSourceSky
TextureCubeArray<float4> SourceSky;
SamplerState s_trilinear_clamp_sampler;
StructuredBuffer<float4> DirectionLod;
uint CaseCount;
[numthreads(64,1,1)]
void SampleSourceSky(uint3 id:SV_DispatchThreadID){
 if(id.x<CaseCount){float4 item=DirectionLod[id.x];Output[id.x]=SourceSky.SampleLevel(s_trilinear_clamp_sampler,float4(normalize(item.xyz),0.0),item.w);}
}
'''
META=loaded['META'].replace('SourceSkyMipMetadataV1','SourceSkySamplingMipMetadataV1')+r'''
[Serializable] public class SourceSkySamplingMetadataV1 {
 public int view,cases,bytes;public string raw_sha256,source_history_channel,sampler,source_filter;
 public bool gpu_native_cube_sampling,full_import;
}
'''
def sampling_capture():
    directions=','.join('new Vector3('+','.join(str(v)+'.0f' for v in row)+')' for row in DIRECTIONS)
    lods=','.join(str(v)+'f' for v in LODS)
    return r'''
    var directions=new Vector3[]{DIRECTION_VALUES};var lods=new float[]{LOD_VALUES};
    var sampleCases=new Vector4[directions.Length*lods.Length];int index=0;
    foreach(var d in directions)foreach(var lod in lods)sampleCases[index++]=new Vector4(d.x,d.y,d.z,lod);
    var sampleInput=new ComputeBuffer(sampleCases.Length,16);var sampleOutput=new ComputeBuffer(sampleCases.Length,16);
    try {
     sampleInput.SetData(sampleCases);int sampleKernel=skyCompute.FindKernel("SampleSourceSky");
     skyCompute.SetTexture(sampleKernel,"SourceSky",sky);skyCompute.SetBuffer(sampleKernel,"DirectionLod",sampleInput);skyCompute.SetBuffer(sampleKernel,"Output",sampleOutput);skyCompute.SetInt("CaseCount",sampleCases.Length);skyCompute.Dispatch(sampleKernel,(sampleCases.Length+63)/64,1,1);
     var samples=new Vector4[sampleCases.Length];sampleOutput.GetData(samples);var bytes=new byte[samples.Length*16];
     for(int i=0;i<samples.Length;i++){var v=samples[i];if(!float.IsFinite(v.x)||!float.IsFinite(v.y)||!float.IsFinite(v.z)||!float.IsFinite(v.w))throw new Exception("Nonfinite sky sample");Buffer.BlockCopy(BitConverter.GetBytes(v.x),0,bytes,i*16,4);Buffer.BlockCopy(BitConverter.GetBytes(v.y),0,bytes,i*16+4,4);Buffer.BlockCopy(BitConverter.GetBytes(v.z),0,bytes,i*16+8,4);Buffer.BlockCopy(BitConverter.GetBytes(v.w),0,bytes,i*16+12,4);}
     var path=Path.Combine(Path.GetDirectoryName(request.output),"source-sky-samples-"+view+".rgba32f");if(File.Exists(path))throw new Exception("Preserve prior sky sampling evidence");File.WriteAllBytes(path,bytes);
     File.WriteAllText(path+".json",JsonUtility.ToJson(new SourceSkySamplingMetadataV1{view=view,cases=samples.Length,bytes=bytes.Length,raw_sha256=Hash(bytes),source_history_channel="CustomUserHistory0",sampler="s_trilinear_clamp_sampler",source_filter=sky.filterMode.ToString(),gpu_native_cube_sampling=true,full_import=false},true));
    }finally{sampleInput.Release();sampleOutput.Release();}
'''.replace('DIRECTION_VALUES',directions).replace('LOD_VALUES',lods)
def helper():
    text=original_helper().replace('SourceShadowReferenceV11','SourceSkySamplingReferenceV1')
    compute=g['PROJECT']/'Assets/Editor/SourceSkySamplingReadbackV1.compute';assert not compute.exists()
    compute.write_text(COMPUTE,'utf-8')
    sdk=g['PROJECT']/'Library/PackageCache/com.unity.render-pipelines.high-definition@fc55a8abadc2/Runtime/Sky/SkyManager.cs'
    g['save']('COMPUTE-PREPARED.v1.json',dict(path=str(compute),sha256=g['sha'](compute),sdk=str(sdk),sdk_sha256=g['sha'](sdk)))
    g['save']('SAMPLING-PROTOCOL.v1.json',dict(source_directions=DIRECTIONS,lods=LODS,expected_cases=len(DIRECTIONS)*len(LODS),case_order='direction then LOD',native_cube_slice=0,source_sampler='s_trilinear_clamp_sampler',source_coordinates=True,target_sampling_qualified=False))
    capture=loaded['CAPTURE'].replace('SourceSkyReadbackV1.compute','SourceSkySamplingReadbackV1.compute').replace('SourceSkyMipMetadataV1','SourceSkySamplingMipMetadataV1')
    marker='var packedProbe=new Vector4[7];actualBuffer.GetData(packedProbe);';assert text.count(marker)==1
    return text.replace(marker,marker+'\n'+capture+'\n'+sampling_capture())+'\n'+META
def corrected_popen(args,*a,**kw):
    if isinstance(args,list) and args and str(args[0])==str(g['UNITY']):
        args=list(args);i=args.index('-executeMethod')+1;assert args[i]=='OpenRPGSourceShadowReferenceV8.Export';args[i]='OpenRPGSourceSkySamplingReferenceV1.Export'
    return popen(args,*a,**kw)
g['subprocess'].Popen=corrected_popen
g.update(ROOT=g['W']/'firepit-source-sky-sampling-v1',HELPER=g['PROJECT']/'Assets/Editor/SourceSkySamplingReferenceV1.cs',helper=helper,__file__=str(Path(__file__).resolve()))
if __name__=='__main__':
    if sys.argv[-1]=='prepare':g['prepare']()
    else:
        g['run']();state=g['read'](g['ROOT']/'RUN-RESULT.v1.json')
        files=[g['ROOT']/f'source-sky-samples-{view}.rgba32f{suffix}' for view in range(2) for suffix in ['', '.json']]
        if state.get('actual_exit_code')==0 and all(p.exists() for p in files):
            state.update(status='native_source_sky_182_sampling_cases_gpu_captured',sampling_files={str(p):g['sha'](p) for p in files})
        else:state.update(status='source_sky_sampling_capture_refused')
        g['save']('RUN-RESULT.v1.json',state)
