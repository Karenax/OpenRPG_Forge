"""Measure the source SDK tent5 weights/UVs on its actual GPU backend."""
from pathlib import Path
import runpy,sys,itertools,json
BASE=Path(__file__).with_name('queue_firepit_source_shadow_culling_v1.py')
loaded=runpy.run_path(str(BASE),run_name='source_shadow_tent5')
g=loaded['g'];original_helper=loaded['helper'];popen=loaded['popen']
CASES=[[128+x,192+y,4096.,4096.] for x,y in itertools.product([-.5,-.25,0.,.25,.499],repeat=2)]
COMPUTE='''#pragma kernel CaptureTent5
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Shadow/ShadowSamplingTent.hlsl"
StructuredBuffer<float4> InputCases;
RWStructuredBuffer<float4> Output;
uint CaseCount;
[numthreads(64,1,1)] void CaptureTent5(uint3 id:SV_DispatchThreadID) {
 if(id.x>=CaseCount)return;
 float4 item=InputCases[id.x];real weights[9];real2 uv[9];
 SampleShadow_ComputeSamples_Tent_5x5(float4(1.0/item.z,1.0/item.w,item.zw),item.xy/item.zw,weights,uv);
 for(uint i=0;i<9;i++)Output[id.x*9+i]=float4(weights[i],uv[i],0.0);
}
'''
def helper():
    text=original_helper().replace('SourceShadowCullingV1','SourceShadowTent5V1')
    compute=g['PROJECT']/'Assets/Editor/SourceShadowTent5V1.compute';assert not compute.exists();compute.write_text(COMPUTE,'utf-8')
    sdk=g['PROJECT']/'Library/PackageCache/com.unity.render-pipelines.core@a4c9a9793789/ShaderLibrary/Shadow/ShadowSamplingTent.hlsl'
    g['save']('TENT5-PROTOCOL.v1.json',dict(cases=CASES,expected_cases=len(CASES),taps_per_case=9,component_order=['weight','u','v','zero'],source_sdk=str(sdk),source_sdk_sha256=g['sha'](sdk),compute=str(compute),compute_sha256=g['sha'](compute),absolute_weight_threshold=2e-7,absolute_uv_threshold=2e-7,source_coefficient_not_normalized=True,full_import=False))
    values=','.join('new Vector4('+','.join(repr(v)+'f' for v in row)+')' for row in CASES)
    capture=r'''
    var tentCases=new Vector4[]{CASE_VALUES};var tentInput=new ComputeBuffer(tentCases.Length,16);var tentOutput=new ComputeBuffer(tentCases.Length*9,16);
    try{
     var tentShader=AssetDatabase.LoadAssetAtPath<ComputeShader>("Assets/Editor/SourceShadowTent5V1.compute");if(tentShader==null)throw new Exception("Source tent5 shader absent");
     tentInput.SetData(tentCases);int kernel=tentShader.FindKernel("CaptureTent5");tentShader.SetBuffer(kernel,"InputCases",tentInput);tentShader.SetBuffer(kernel,"Output",tentOutput);tentShader.SetInt("CaseCount",tentCases.Length);tentShader.Dispatch(kernel,(tentCases.Length+63)/64,1,1);
     var output=new Vector4[tentCases.Length*9];tentOutput.GetData(output);var tentRaw=new byte[output.Length*16];
     for(int i=0;i<output.Length;i++){var v=output[i];if(!float.IsFinite(v.x)||!float.IsFinite(v.y)||!float.IsFinite(v.z)||!float.IsFinite(v.w))throw new Exception("Nonfinite source tent5 output");Buffer.BlockCopy(BitConverter.GetBytes(v.x),0,tentRaw,i*16,4);Buffer.BlockCopy(BitConverter.GetBytes(v.y),0,tentRaw,i*16+4,4);Buffer.BlockCopy(BitConverter.GetBytes(v.z),0,tentRaw,i*16+8,4);Buffer.BlockCopy(BitConverter.GetBytes(v.w),0,tentRaw,i*16+12,4);}
     var path=Path.Combine(Path.GetDirectoryName(request.output),"SOURCE-TENT5-"+view+".rgba32f");if(File.Exists(path))throw new Exception("Preserve tent5 evidence");File.WriteAllBytes(path,tentRaw);
    }finally{tentInput.Release();tentOutput.Release();}
'''.replace('CASE_VALUES',values)
    marker='File.WriteAllText(cullingPath,JsonUtility.ToJson(cullingState,true));';assert text.count(marker)==1
    return text.replace(marker,marker+capture)
def corrected_popen(args,*a,**kw):
    if isinstance(args,list) and args and str(args[0])==str(g['UNITY']):
        args=list(args);i=args.index('-executeMethod')+1
        assert args[i]=='OpenRPGSourceShadowReferenceV8.Export';args[i]='OpenRPGSourceShadowTent5V1.Export'
    return popen(args,*a,**kw)
g['subprocess'].Popen=corrected_popen
g.update(ROOT=g['W']/'firepit-source-shadow-tent5-v1',HELPER=g['PROJECT']/'Assets/Editor/SourceShadowTent5V1.cs',helper=helper,__file__=str(Path(__file__).resolve()))
if __name__=='__main__':
    if sys.argv[-1]=='prepare':g['prepare']()
    else:
        g['run']();state=g['read'](g['ROOT']/'RUN-RESULT.v1.json');paths=[g['ROOT']/f'SOURCE-TENT5-{i}.rgba32f' for i in range(2)]
        if state.get('actual_exit_code')==0 and all(p.exists() for p in paths):state.update(status='source_sdk_tent5_weights_and_uv_gpu_captured',sampling_files={str(p):g['sha'](p) for p in paths})
        else:state.update(status='source_sdk_tent5_capture_refused')
        g['save']('RUN-RESULT.v1.json',state)
