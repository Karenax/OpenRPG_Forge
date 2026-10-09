"""Match the HDRP shadow-map allocation; retain the v1 sampling refusal."""
from pathlib import Path
import runpy,sys
BASE=Path(__file__).with_name('queue_source_tent5_atlas_receiver_v1.py')
loaded=runpy.run_path(str(BASE),run_name='source_tent5_receiver_v2')
g=loaded['g']
g['__file__']=str(Path(__file__).resolve())
g['ROOT']=g['W']/'firepit-source-d16-tent5-receiver-v2'
g['ASSET']=g['PROJECT']/'Assets/Editor/OpenRPGSourceTent5AtlasReceiverV2'
g['CS']=g['CS'].replace('OpenRPGSourceTent5AtlasReceiverV1','OpenRPGSourceTent5AtlasReceiverV2')
needle='depthStencilFormat=GraphicsFormat.D16_UNorm,msaaSamples=1'
assert g['CS'].count(needle)==1
g['CS']=g['CS'].replace(needle,'depthStencilFormat=GraphicsFormat.D16_UNorm,shadowSamplingMode=ShadowSamplingMode.CompareDepths,msaaSamples=1')
g['CS']=g['CS'].replace('var rt=new RenderTexture(desc);','var rt=new RenderTexture(desc){filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};')
g['EXTRA_PINS'] += [BASE,g['PROJECT']/'Library/PackageCache/com.unity.render-pipelines.core@a4c9a9793789/Runtime/Textures/RTHandleSystem.cs',g['PROJECT']/'Library/PackageCache/com.unity.render-pipelines.high-definition@fc55a8abadc2/Runtime/Lighting/Shadow/HDShadowAtlas.cs']
previous=loaded['original_popen']
def popen(args,*a,**kw):
 if isinstance(args,list) and args and str(args[0])==str(g['UNITY']):
  args=list(args);i=args.index('-executeMethod')+1
  assert args[i]=='OpenRPGStaticCasterDepthControlV1.Export'
  args[i]='OpenRPGSourceTent5AtlasReceiverV2.Export'
 return previous(args,*a,**kw)
g['subprocess'].Popen=popen
if __name__=='__main__':g['prepare']() if sys.argv[-1]=='prepare' else g['run']()
