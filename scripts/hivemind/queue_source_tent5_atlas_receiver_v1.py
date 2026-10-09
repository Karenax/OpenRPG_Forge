"""Capture PCF5 on the independently rendered Unity D16 atlas."""
from pathlib import Path
import sys
BASE=Path(__file__).with_name('queue_source_static_caster_depth_control_v1.py')
source=BASE.read_text(encoding='utf-8')
source=source.replace("if __name__=='__main__':prepare() if sys.argv[-1]=='prepare' else run()","")
source=source.replace(" save('PREPARED.v1.json',", " for p in EXTRA_PINS:pins[str(p)]=sha(p)\n save('PREPARED.v1.json',")
g={'__file__':str(Path(__file__).resolve()),'__name__':'source_tent5_receiver'}
exec(compile(source,str(BASE),'exec'),g)
W=g['W'];PROJECT=g['PROJECT']
SDK=PROJECT/'Library/PackageCache/com.unity.render-pipelines.high-definition@fc55a8abadc2/Runtime/Lighting/Shadow/HDShadowSampling.hlsl'
CORE=PROJECT/'Library/PackageCache/com.unity.render-pipelines.core@a4c9a9793789/ShaderLibrary/Shadow/ShadowSamplingTent.hlsl'
CASES=W/'firepit-punctual-d16-tent5-receiver-v1/receiver-cases.float32'
sampling=SDK.read_text(encoding='utf-8')
start=sampling.index('real SampleShadow_PCF_Tent_5x5(')
end=sampling.index('real SampleShadow_PCF_Tent_7x7(',start)
sampling=sampling[start:end]
compute='''#pragma kernel ReadDepth
#pragma kernel Receiver
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Shadow/ShadowSamplingTent.hlsl"
#define SHADOW_USE_DEPTH_BIAS 1
#define SHADOW_OPTIMIZE_REGISTER_USAGE 0
Texture2D<float> Depth;
SamplerComparisonState s_linear_clamp_compare_sampler;
RWStructuredBuffer<float> Values;
StructuredBuffer<float4> ReceiverCases;
RWStructuredBuffer<float4> ReceiverOutput;
'''+sampling+'''
[numthreads(8,8,1)] void ReadDepth(uint3 id:SV_DispatchThreadID) {
 Values[id.y*4096+id.x]=Depth.Load(int3(id.xy,0));
}
[numthreads(64,1,1)] void Receiver(uint3 id:SV_DispatchThreadID) {
 if(id.x>=1458)return;
 float4 item=ReceiverCases[id.x];
 float visibility=SampleShadow_PCF_Tent_5x5(float4(1.0/4096.0,1.0/4096.0,4096.0,4096.0),item.xyz,Depth,s_linear_clamp_compare_sampler,1.0/65536.0);
 ReceiverOutput[id.x]=float4(visibility,item.xyz);
}
'''
cs=g['CS'].replace('OpenRPGStaticCasterDepthControlV1','OpenRPGSourceTent5AtlasReceiverV1')
marker=' try {foreach(bool active in new[]{true,false}){'
assert cs.count(marker)==1
setup=r'''
 var caseBytes=File.ReadAllBytes("F:/OpenRPG_ImportWork/firepit-punctual-d16-tent5-receiver-v1/receiver-cases.float32");
 if(caseBytes.Length!=1458*16)throw new Exception("Receiver case count invalid");
 var rawCases=new float[1458*4];Buffer.BlockCopy(caseBytes,0,rawCases,0,caseBytes.Length);
 var cases=new Vector4[1458];for(int i=0;i<1458;i++)cases[i]=new Vector4(rawCases[4*i],rawCases[4*i+1],rawCases[4*i+2],rawCases[4*i+3]);
 var receiverInput=new ComputeBuffer(1458,16);receiverInput.SetData(cases);
 var receiverOutput=new ComputeBuffer(1458,16);int receiverKernel=compute.FindKernel("Receiver");
'''
cs=cs.replace(marker,setup+marker)
marker=' Graphics.ExecuteCommandBuffer(cmd);cmd.Release();'
assert cs.count(marker)==1
cs=cs.replace(marker,r'''
 cmd.SetComputeTextureParam(compute,receiverKernel,"Depth",rt);
 cmd.SetComputeBufferParam(compute,receiverKernel,"ReceiverCases",receiverInput);
 cmd.SetComputeBufferParam(compute,receiverKernel,"ReceiverOutput",receiverOutput);
 cmd.DispatchCompute(compute,receiverKernel,(1458+63)/64,1,1);
'''+marker)
marker='\n }\n File.WriteAllText(Path.Combine(root,"SOURCE-GPU.v1.json")'
assert cs.count(marker)==1
capture=r'''
 var receiverRead=AsyncGPUReadback.Request(receiverOutput);receiverRead.WaitForCompletion();if(receiverRead.hasError)throw new Exception("Receiver readback failed");
 var receiverValues=receiverRead.GetData<Vector4>().ToArray();var receiverBytes=new byte[1458*16];
 for(int i=0;i<1458;i++){var v=receiverValues[i];if(!float.IsFinite(v.x)||v.x < -2e-6f||v.x>1f+2e-6f)throw new Exception("Receiver visibility invalid");if(!active&&Mathf.Abs(v.x-1f)>2e-6f)throw new Exception("Inactive reversed comparison invalid");Buffer.BlockCopy(BitConverter.GetBytes(v.x),0,receiverBytes,i*16,4);Buffer.BlockCopy(BitConverter.GetBytes(v.y),0,receiverBytes,i*16+4,4);Buffer.BlockCopy(BitConverter.GetBytes(v.z),0,receiverBytes,i*16+8,4);Buffer.BlockCopy(BitConverter.GetBytes(v.w),0,receiverBytes,i*16+12,4);}
 File.WriteAllBytes(Path.Combine(root,active?"active-receiver.rgba32f":"inactive-receiver.rgba32f"),receiverBytes);
'''
cs=cs.replace(marker,capture+marker)
cs=cs.replace('}finally{buffer.Release();','}finally{receiverInput.Release();receiverOutput.Release();buffer.Release();')
g.update(ROOT=W/'firepit-source-d16-tent5-receiver-v1',ASSET=PROJECT/'Assets/Editor/OpenRPGSourceTent5AtlasReceiverV1',CS=cs,COMPUTE=compute,EXTRA_PINS=[BASE,SDK,CORE,CASES])
# The controller's executeMethod must select this unique private control.
original_popen=g['subprocess'].Popen
def popen(args,*a,**kw):
 if isinstance(args,list) and args and str(args[0])==str(g['UNITY']):
  args=list(args);i=args.index('-executeMethod')+1
  assert args[i]=='OpenRPGStaticCasterDepthControlV1.Export'
  args[i]='OpenRPGSourceTent5AtlasReceiverV1.Export'
 return original_popen(args,*a,**kw)
g['subprocess'].Popen=popen
if __name__=='__main__':g['prepare']() if sys.argv[-1]=='prepare' else g['run']()
