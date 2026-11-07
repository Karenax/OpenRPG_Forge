"""Capture real camera depth inside HDRP instead of an absent global texture."""
from pathlib import Path
import subprocess
import sys

S = Path(__file__).parent
target = S / 'queue_townsmith_depth_capture_v22.py'
assert not target.exists()
text = (S / 'queue_townsmith_gbuffer_arrays_v21.py').read_text(encoding='utf-8')
text = text.replace('townsmith-door-source-gbuffer-arrays-v21', 'townsmith-door-source-depth-capture-v22')
text = text.replace('SourceTownsmithGBufferArraysV21', 'SourceTownsmithDepthCaptureV22')
text = text.replace('SourceTownsmithFgdReadbackV21', 'SourceTownsmithFgdReadbackV22')
kernel = '''#pragma kernel ReadDepth
Texture2DArray<float> SourceDepth;
RWStructuredBuffer<float> DepthOutput;
[numthreads(8,8,1)]
void ReadDepth(uint3 id:SV_DispatchThreadID){if(id.x<Width&&id.y<Height)DepthOutput[id.y*Width+id.x]=SourceDepth.Load(int4(id.xy,0,0));}
'''
anchor = "    text=(S/'candidates/SourceTownsmithShadowModesV1.cs').read_text(encoding='utf-8')"
assert text.count(anchor) == 1
text = text.replace(anchor, '    compute_path.write_text(compute_path.read_text(encoding="utf-8")+' + repr(kernel) + ',encoding="utf-8")\n' + anchor)
custom = '''
public sealed class SourceOwnedDepthCaptureV22 : CustomPass {
 public Camera sourceCamera;public ComputeShader compute;public string root,id;
 public int submitted,completed;public List<string> errors=new List<string>();
 [Serializable] public class DepthRecord {public string id,texture_name,format,dimension;public int capture,width,height,physical_width,physical_height,layers;public Matrix4x4 projection;public bool captured_inside_actual_camera_pass=true,geometry_parity_qualified=false;}
 protected override void Execute(CustomPassContext ctx){
  if(ctx.hdCamera.camera!=sourceCamera)return;
  var actual=ctx.cameraDepthBuffer.rt;if(actual==null||actual.dimension!=TextureDimension.Tex2DArray)throw new Exception("Expected actual HDRP array depth");
  int width=ctx.hdCamera.actualWidth,height=ctx.hdCamera.actualHeight,index=submitted++;
  if(width!=512||height!=512||actual.volumeDepth!=1)throw new Exception("Depth viewport differs from pinned fixture");
  var output=new ComputeBuffer(width*height,4);int kernel=compute.FindKernel("ReadDepth");
  ctx.cmd.SetComputeTextureParam(compute,kernel,"SourceDepth",ctx.cameraDepthBuffer);
  ctx.cmd.SetComputeBufferParam(compute,kernel,"DepthOutput",output);
  ctx.cmd.SetComputeIntParam(compute,"Width",width);ctx.cmd.SetComputeIntParam(compute,"Height",height);
  ctx.cmd.DispatchCompute(compute,kernel,(width+7)/8,(height+7)/8,1);
  var path=Path.Combine(root,id+".depth-"+index+".r32f");
  File.WriteAllText(Path.Combine(root,id+".depth-"+index+".json"),JsonUtility.ToJson(new DepthRecord{id=id,capture=index,width=width,height=height,physical_width=actual.width,physical_height=actual.height,layers=actual.volumeDepth,texture_name=actual.name,format=actual.graphicsFormat.ToString(),dimension=actual.dimension.ToString(),projection=ctx.hdCamera.mainViewConstants.projMatrix},true));
  ctx.cmd.RequestAsyncReadback(output,request=>{
   try{if(request.hasError)throw new Exception("Depth GPU readback failed");var values=request.GetData<float>();if(values.Length!=width*height)throw new Exception("Depth size differs");
    using(var writer=new BinaryWriter(File.Open(path,FileMode.CreateNew)))foreach(var value in values){if(float.IsNaN(value)||float.IsInfinity(value))throw new Exception("Nonfinite depth");writer.Write(value);}completed++;
   }catch(Exception error){errors.Add(error.ToString());}finally{output.Release();}
  });
 }
}
'''
patch = '''
    text=text.replace('RenderTexture target=null;DebugDisplaySettings', 'RenderTexture target=null;SourceOwnedDepthCaptureV22 sourceDepthPass=null;DebugDisplaySettings')
    text=text.replace('    for(int warm=0;warm<3;warm++){', '    sourceDepthPass=new SourceOwnedDepthCaptureV22{sourceCamera=camera,compute=AssetDatabase.LoadAssetAtPath<ComputeShader>("Assets/Editor/SourceTownsmithFgdReadbackV22.compute"),root=root,id=item.id};\\n    CustomPassVolume.RegisterGlobalCustomPass(CustomPassInjectionPoint.AfterOpaqueDepthAndNormal,sourceDepthPass);\\n    for(int warm=0;warm<3;warm++){')
    text=text.replace('    var historyType=typeof(HDCamera)', '    AsyncGPUReadback.WaitAllRequests();if(sourceDepthPass.submitted<1||sourceDepthPass.completed!=sourceDepthPass.submitted||sourceDepthPass.errors.Count!=0)throw new Exception("Actual depth capture incomplete: "+string.Join(";",sourceDepthPass.errors));\\n    var historyType=typeof(HDCamera)')
    text=text.replace('}finally{if(sourceDebugSettings!=null)', '}finally{if(sourceDepthPass!=null)CustomPassVolume.UnregisterGlobalCustomPass(sourceDepthPass);if(sourceDebugSettings!=null)')
'''
anchor = "    CS.write_text(text,encoding='utf-8');HELPER.write_bytes(CS.read_bytes())"
assert text.count(anchor) == 1
text = text.replace(anchor, patch + '    text+=' + repr(custom) + '\n' + anchor)
compile(text, str(target), 'exec')
target.write_text(text, encoding='utf-8')
subprocess.run([sys.executable, '-B', str(target)], check=True)
