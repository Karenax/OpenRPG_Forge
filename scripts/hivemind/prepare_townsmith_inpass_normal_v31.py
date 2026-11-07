"""Read actual camera normal RT inside BeforePostProcess, not a later global."""
from pathlib import Path
import subprocess,sys
s=Path(__file__).parent
target=s/'queue_townsmith_inpass_normal_v31.py'
assert not target.exists()
q=(s/'queue_townsmith_fine_uv_source_v28.py').read_text(encoding='utf-8')
q=q.replace('v28','v31').replace('V28','V31')
q=q.replace('townsmith-door-source-fine-uv-v31','townsmith-door-source-inpass-normal-v31')
custom='''
public sealed class SourceOwnedNormalCaptureV31 : CustomPass {
 public Camera sourceCamera;public ComputeShader shader;public string root,id;
 public int submitted,completed;public List<string> errors=new List<string>();
 [Serializable] public class NormalRecord {public string id,format,textureName,injection="BeforePostProcess";public int capture,width,height,viewportWidth,viewportHeight;public bool actual_camera_normal_buffer=true,full_import=false;}
 protected override void Execute(CustomPassContext ctx){
  if(ctx.hdCamera.camera!=sourceCamera)return;
  var texture=ctx.cameraNormalBuffer.rt;
  if(texture==null||texture.dimension!=TextureDimension.Tex2DArray||texture.volumeDepth!=1)throw new Exception("Actual normal target layout differs");
  int width=texture.width,height=texture.height,index=submitted++;
  int kernel=shader.FindKernel("ReadArray");var output=new ComputeBuffer(width*height,16);
  ctx.cmd.SetComputeIntParam(shader,"Width",width);ctx.cmd.SetComputeIntParam(shader,"Height",height);
  ctx.cmd.SetComputeTextureParam(shader,kernel,"SourceArray",ctx.cameraNormalBuffer);
  ctx.cmd.SetComputeBufferParam(shader,kernel,"Output",output);
  ctx.cmd.DispatchCompute(shader,kernel,(width+7)/8,(height+7)/8,1);
  string path=Path.Combine(root,id+".inpass-normal-"+index+".rgba32f");
  File.WriteAllText(Path.Combine(root,id+".inpass-normal-"+index+".json"),JsonUtility.ToJson(new NormalRecord{id=id,capture=index,width=width,height=height,viewportWidth=ctx.hdCamera.actualWidth,viewportHeight=ctx.hdCamera.actualHeight,format=texture.graphicsFormat.ToString(),textureName=texture.name},true));
  ctx.cmd.RequestAsyncReadback(output,request=>{
   try{if(request.hasError)throw new Exception("Actual camera normal GPU readback failed");var data=request.GetData<float>();if(data.Length!=width*height*4)throw new Exception("Actual normal readback size differs");
    using(var writer=new BinaryWriter(File.Open(path,FileMode.CreateNew)))foreach(float value in data){if(float.IsNaN(value)||float.IsInfinity(value))throw new Exception("Nonfinite camera normal");writer.Write(value);}completed++;
   }catch(Exception error){errors.Add(error.ToString());}finally{output.Release();}
  });
 }
}
'''
anchor='SourceOwnedUvCaptureV31 sourceUvPass=null;'
assert q.count(anchor)==1
q=q.replace(anchor,anchor+'SourceOwnedNormalCaptureV31 sourceNormalPass=null;')
anchor='CustomPassVolume.RegisterGlobalCustomPass(CustomPassInjectionPoint.AfterOpaqueDepthAndNormal,sourceUvPass);'
assert q.count(anchor)==1
q=q.replace(anchor,anchor+'\\n    sourceNormalPass=new SourceOwnedNormalCaptureV31{sourceCamera=camera,shader=AssetDatabase.LoadAssetAtPath<ComputeShader>("Assets/Editor/SourceTownsmithFgdReadbackV31.compute"),root=root,id=item.id};\\n    CustomPassVolume.RegisterGlobalCustomPass(CustomPassInjectionPoint.BeforePostProcess,sourceNormalPass);')
anchor='if(sourceUvPass.submitted<2'
assert q.count(anchor)==1
q=q.replace(anchor,'if(sourceNormalPass.submitted<2||sourceNormalPass.completed!=sourceNormalPass.submitted||sourceNormalPass.errors.Count!=0)throw new Exception("Actual normal capture incomplete: "+string.Join(";",sourceNormalPass.errors));\\n    '+anchor)
anchor='}finally{if(sourceUvPass!=null)'
assert q.count(anchor)==1
q=q.replace(anchor,'}finally{if(sourceNormalPass!=null){CustomPassVolume.UnregisterGlobalCustomPass(sourceNormalPass);AsyncGPUReadback.WaitAllRequests();}if(sourceUvPass!=null)')
anchor="    CS.write_text(text,encoding='utf-8');HELPER.write_bytes(CS.read_bytes())"
assert q.count(anchor)==1
q=q.replace(anchor,'    text+='+repr(custom)+'\n'+anchor)
compile(q,str(target),'exec');target.write_text(q,encoding='utf-8')
subprocess.run([sys.executable,'-B',str(target)],check=True)
