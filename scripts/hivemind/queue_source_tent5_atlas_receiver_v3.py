"""Use the SDK texture type and reject source shader compilation errors."""
from pathlib import Path
import runpy,sys
BASE=Path(__file__).with_name('queue_source_tent5_atlas_receiver_v2.py')
loaded=runpy.run_path(str(BASE),run_name='source_tent5_receiver_v3')
g=loaded['g'];g['__file__']=str(Path(__file__).resolve())
g['ROOT']=g['W']/'firepit-source-d16-tent5-receiver-v3'
g['ASSET']=g['PROJECT']/'Assets/Editor/OpenRPGSourceTent5AtlasReceiverV3'
g['CS']=g['CS'].replace('OpenRPGSourceTent5AtlasReceiverV2','OpenRPGSourceTent5AtlasReceiverV3')
marker=' if(shader==null||!shader.isSupported||compute==null)throw new Exception("Control shader unavailable");'
assert g['CS'].count(marker)==1
g['CS']=g['CS'].replace(marker,marker+'\n foreach(var diagnostic in ShaderUtil.GetComputeShaderMessages(compute))if(diagnostic.severity.ToString()=="Error")throw new Exception("Source kernel compilation refused: "+diagnostic.message);')
assert g['COMPUTE'].count('Texture2D<float> Depth;')==1
g['COMPUTE']=g['COMPUTE'].replace('Texture2D<float> Depth;','Texture2D Depth;').replace('Depth.Load(int3(id.xy,0));','Depth.Load(int3(id.xy,0)).x;')
g['EXTRA_PINS'] += [BASE]
previous=loaded['previous']
def popen(args,*a,**kw):
 if isinstance(args,list) and args and str(args[0])==str(g['UNITY']):
  args=list(args);i=args.index('-executeMethod')+1
  assert args[i]=='OpenRPGStaticCasterDepthControlV1.Export'
  args[i]='OpenRPGSourceTent5AtlasReceiverV3.Export'
 return previous(args,*a,**kw)
g['subprocess'].Popen=popen
if __name__=='__main__':g['prepare']() if sys.argv[-1]=='prepare' else g['run']()
