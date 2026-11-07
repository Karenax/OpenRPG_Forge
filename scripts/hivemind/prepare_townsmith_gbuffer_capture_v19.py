"""Inspect source Deferred globals and light metadata after the actual request."""
from pathlib import Path
import subprocess
import sys

S = Path(__file__).parent
target = S / 'queue_townsmith_gbuffer_capture_v19.py'
assert not target.exists()
text = (S / 'queue_townsmith_direct_controls_v18.py').read_text(encoding='utf-8')
text = text.replace('townsmith-door-source-direct-controls-v18', 'townsmith-door-source-gbuffer-capture-v19')
text = text.replace('SourceTownsmithDirectControlsV18', 'SourceTownsmithGBufferCaptureV19')
text = text.replace('SourceTownsmithFgdReadbackV18', 'SourceTownsmithFgdReadbackV19')
text = text.replace('request.cases.Length!=21', 'request.cases.Length!=5')
text = text.replace('expected_cases=21', 'expected_cases=5').replace("len(native['rows'])==21", "len(native['rows'])==5").replace('cases_captured=21', 'cases_captured=5')
anchor = "    save('REQUEST.v1.json',dict(output=str(ROOT/'NATIVE-RESULT.v1.json'),cases=cases))"
assert text.count(anchor) == 1
text = text.replace(anchor, "    cases=[dict(base,id=name) for name in ['door-neutral','door-linear','door-nopost-linear','door-debug-DirectDiffuseLighting-nopost-linear','door-debug-DirectSpecularLighting-nopost-linear']]\n" + anchor)
declarations = ''' [Serializable] public class GlobalBufferCapture {
 public string shader_property,texture_name,texture_dimension,graphics_format,error;
 public int width,height,instance_id,float_count;public bool present,gpu_loaded;
 public bool actual_lighting_binding_qualified=false;public string observation_scope="After actual SubmitRenderRequest; resource identity/lifetime requires separate verification.";
 }
 [Serializable] public class GlobalBufferInventory {public string id;public GlobalBufferCapture[] buffers;}
'''
capture = '''    File.WriteAllText(Path.Combine(root,item.id+".source-light.json"),EditorJsonUtility.ToJson(light,true));
    var sourceHdLight=light.GetComponent<HDAdditionalLightData>();
    if(sourceHdLight==null)throw new Exception("Actual additional light data absent");
    File.WriteAllText(Path.Combine(root,item.id+".source-hdlight.json"),EditorJsonUtility.ToJson(sourceHdLight,true));
    var sourceBuffers=new List<GlobalBufferCapture>();
    foreach(var property in new[]{"_GBufferTexture0","_GBufferTexture1","_GBufferTexture2","_GBufferTexture3","_CameraDepthTexture","_AmbientOcclusionTexture"}){
      var record=new GlobalBufferCapture{shader_property=property};sourceBuffers.Add(record);
      var actualTexture=Shader.GetGlobalTexture(property);record.present=actualTexture!=null;
      if(actualTexture==null){record.error="Global texture absent after request";continue;}
      record.texture_name=actualTexture.name;record.instance_id=actualTexture.GetInstanceID();
      record.texture_dimension=actualTexture.dimension.ToString();record.graphics_format=actualTexture.graphicsFormat.ToString();
      record.width=actualTexture.width;record.height=actualTexture.height;
      if(actualTexture.dimension!=TextureDimension.Tex2D||actualTexture.width<1||actualTexture.height<1||actualTexture.width*actualTexture.height>2097152){record.error="Unsupported or unbounded global layout";continue;}
      ComputeBuffer actualBuffer=null;
      try{
        actualBuffer=new ComputeBuffer(record.width*record.height,16);
        sourceCompute.SetTexture(sourceKernel,"SourceFgd",actualTexture);sourceCompute.SetBuffer(sourceKernel,"Output",actualBuffer);
        sourceCompute.SetInt("Width",record.width);sourceCompute.SetInt("Height",record.height);
        sourceCompute.Dispatch(sourceKernel,(record.width+7)/8,(record.height+7)/8,1);
        var bufferValues=new Vector4[record.width*record.height];actualBuffer.GetData(bufferValues);
        using(var writer=new BinaryWriter(File.Open(Path.Combine(root,item.id+"."+property+".rgba32f"),FileMode.CreateNew))){
          foreach(var vector in bufferValues)for(int ch=0;ch<4;ch++){var value=vector[ch];if(float.IsNaN(value)||float.IsInfinity(value))throw new Exception("Nonfinite source global readback");writer.Write(value);}
        }
        record.float_count=bufferValues.Length*4;record.gpu_loaded=true;
      }catch(Exception captureError){record.error=captureError.ToString();}
      finally{if(actualBuffer!=null)actualBuffer.Release();}
    }
    File.WriteAllText(Path.Combine(root,item.id+".global-buffers.json"),JsonUtility.ToJson(new GlobalBufferInventory{id=item.id,buffers=sourceBuffers.ToArray()},true));
'''
anchor = "    CS.write_text(text,encoding='utf-8');HELPER.write_bytes(CS.read_bytes())"
assert text.count(anchor) == 1
cs_anchor = '    var reflectionMethod=manager.GetType().GetMethod("GetSkyReflection",instanceFlags,null,new[]{typeof(HDCamera)},null);'
replacement = '    assert text.count(' + repr(cs_anchor) + ')==1\n'
replacement += '    text=text.replace(' + repr(cs_anchor) + ',' + repr(capture + cs_anchor) + ')\n'
replacement += '    text=text.replace(" [Serializable] public class Context {",' + repr(declarations + ' [Serializable] public class Context {') + ')\n' + anchor
text = text.replace(anchor, replacement)
compile(text, str(target), 'exec')
target.write_text(text, encoding='utf-8')
subprocess.run([sys.executable, '-B', str(target)], check=True)
