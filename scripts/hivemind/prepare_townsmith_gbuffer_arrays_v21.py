"""Read every layer of the observed source globals, retaining physical layout."""
from pathlib import Path
import subprocess
import sys

S = Path(__file__).parent
target = S / 'queue_townsmith_gbuffer_arrays_v21.py'
assert not target.exists()
text = (S / 'queue_townsmith_gbuffer_capture_v20.py').read_text(encoding='utf-8')
text = text.replace('townsmith-door-source-gbuffer-capture-v20', 'townsmith-door-source-gbuffer-arrays-v21')
text = text.replace('SourceTownsmithGBufferCaptureV20', 'SourceTownsmithGBufferArraysV21')
text = text.replace('SourceTownsmithFgdReadbackV20', 'SourceTownsmithFgdReadbackV21')
kernel = '''#pragma kernel ReadArray
Texture2DArray<float4> SourceArray;
[numthreads(8,8,1)]
void ReadArray(uint3 id:SV_DispatchThreadID){if(id.x<Width&&id.y<Height)Output[(id.z*Height+id.y)*Width+id.x]=SourceArray.Load(int4(id.xy,id.z,0));}
'''
anchor = "    text=(S/'candidates/SourceTownsmithShadowModesV1.cs').read_text(encoding='utf-8')"
assert text.count(anchor) == 1
text = text.replace(anchor, '    compute_path.write_text(compute_path.read_text(encoding="utf-8")+' + repr(kernel) + ',encoding="utf-8")\n' + anchor)
patch = '''
    text=text.replace('public int width,height,float_count;','public int width,height,float_count,layer_count;')
    text=text.replace('public string id;public GlobalBufferCapture[] buffers;', 'public string id;public int actual_viewport_width,actual_viewport_height;public GlobalBufferCapture[] buffers;')
    text=text.replace('record.width=actualTexture.width;record.height=actualTexture.height;', 'record.width=actualTexture.width;record.height=actualTexture.height;record.layer_count=actualTexture is RenderTexture actualRt?actualRt.volumeDepth:1;')
    text=text.replace('actualTexture.dimension!=TextureDimension.Tex2D||actualTexture.width<1||actualTexture.height<1||actualTexture.width*actualTexture.height>2097152', '(actualTexture.dimension!=TextureDimension.Tex2D&&actualTexture.dimension!=TextureDimension.Tex2DArray)||actualTexture.width<1||actualTexture.height<1||record.layer_count<1||record.layer_count>8||actualTexture.width*actualTexture.height*record.layer_count>4194304')
    text=text.replace('actualBuffer=new ComputeBuffer(record.width*record.height,16);', 'actualBuffer=new ComputeBuffer(record.width*record.height*record.layer_count,16);var captureKernel=actualTexture.dimension==TextureDimension.Tex2DArray?sourceCompute.FindKernel("ReadArray"):sourceKernel;')
    text=text.replace('sourceCompute.SetTexture(sourceKernel,"SourceFgd",actualTexture);sourceCompute.SetBuffer(sourceKernel,"Output",actualBuffer);', 'sourceCompute.SetTexture(captureKernel,actualTexture.dimension==TextureDimension.Tex2DArray?"SourceArray":"SourceFgd",actualTexture);sourceCompute.SetBuffer(captureKernel,"Output",actualBuffer);')
    text=text.replace('sourceCompute.Dispatch(sourceKernel,(record.width+7)/8,(record.height+7)/8,1);', 'sourceCompute.Dispatch(captureKernel,(record.width+7)/8,(record.height+7)/8,record.layer_count);')
    text=text.replace('var bufferValues=new Vector4[record.width*record.height];', 'var bufferValues=new Vector4[record.width*record.height*record.layer_count];')
    text=text.replace('new GlobalBufferInventory{id=item.id,buffers=', 'new GlobalBufferInventory{id=item.id,actual_viewport_width=sourceHdCamera.actualWidth,actual_viewport_height=sourceHdCamera.actualHeight,buffers=')
'''
anchor = "    CS.write_text(text,encoding='utf-8');HELPER.write_bytes(CS.read_bytes())"
assert text.count(anchor) == 1
text = text.replace(anchor, patch + anchor)
compile(text, str(target), 'exec')
target.write_text(text, encoding='utf-8')
subprocess.run([sys.executable, '-B', str(target)], check=True)
