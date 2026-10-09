"""Capture all nine source PCF5 weights, UVs and hardware comparisons."""
from pathlib import Path
import runpy,sys
BASE=Path(__file__).with_name('queue_source_tent5_atlas_receiver_v3.py')
loaded=runpy.run_path(str(BASE),run_name='source_tent5_atlas_taps')
g=loaded['g'];g['__file__']=str(Path(__file__).resolve())
g['ROOT']=g['W']/'firepit-source-d16-tent5-taps-v1'
g['ASSET']=g['PROJECT']/'Assets/Editor/OpenRPGSourceTent5AtlasTapsV1'
g['CS']=g['CS'].replace('OpenRPGSourceTent5AtlasReceiverV3','OpenRPGSourceTent5AtlasTapsV1')
g['CS']=g['CS'].replace('receiverOutput=new ComputeBuffer(1458,16)','receiverOutput=new ComputeBuffer(1458*9,16)')
g['CS']=g['CS'].replace('receiverBytes=new byte[1458*16]','receiverBytes=new byte[1458*9*16]')
g['CS']=g['CS'].replace('for(int i=0;i<1458;i++){var v=receiverValues[i];','for(int i=0;i<1458*9;i++){var v=receiverValues[i];')
g['CS']=g['CS'].replace('if(!active&&Mathf.Abs(v.x-1f)>2e-6f)','if(!float.IsFinite(v.w)||v.w<0f||v.w>1f)throw new Exception("Comparison tap invalid");if(!active&&Mathf.Abs(v.w-1f)>2e-6f)')
start=g['COMPUTE'].index(' float visibility=SampleShadow_PCF_Tent_5x5')
end=g['COMPUTE'].index('\n}',start)
g['COMPUTE']=g['COMPUTE'][:start]+'''
 real weights[9];real2 uv[9];
 SampleShadow_ComputeSamples_Tent_5x5(float4(1.0/4096.0,1.0/4096.0,4096.0,4096.0),item.xy,weights,uv);
 for(uint i=0;i<9;i++){
  float comparison=SAMPLE_TEXTURE2D_SHADOW(Depth,s_linear_clamp_compare_sampler,float3(uv[i],item.z+1.0/65536.0)).x;
  ReceiverOutput[id.x*9+i]=float4(weights[i],uv[i],comparison);
 }
'''+g['COMPUTE'][end:]
g['EXTRA_PINS'] += [BASE]
previous=loaded['previous']
def popen(args,*a,**kw):
 if isinstance(args,list) and args and str(args[0])==str(g['UNITY']):
  args=list(args);i=args.index('-executeMethod')+1
  assert args[i]=='OpenRPGStaticCasterDepthControlV1.Export'
  args[i]='OpenRPGSourceTent5AtlasTapsV1.Export'
 return previous(args,*a,**kw)
g['subprocess'].Popen=popen
if __name__=='__main__':g['prepare']() if sys.argv[-1]=='prepare' else g['run']()
