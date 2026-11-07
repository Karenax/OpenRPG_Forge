"""Read all layers and mips from the actual HDRP request sky reflection texture."""
from pathlib import Path
import subprocess
import sys

S = Path(__file__).parent
TARGET = S / 'queue_townsmith_sky_capture_v8.py'
assert not TARGET.exists()
text = (S / 'queue_townsmith_render_context_v7.py').read_text(encoding='utf-8')
text = text.replace('townsmith-door-source-render-context-v7', 'townsmith-door-source-sky-capture-v8')
text = text.replace('SourceTownsmithRenderContextV7', 'SourceTownsmithSkyCaptureV8')
anchor = 'public string actual_camera_history;'
assert text.count(anchor) == 1
text = text.replace(anchor, anchor + ' public int sky_cubemap_count,sky_mip_count; public string sky_source_format; public int[] sky_layer_counts,sky_layer_bytes;')
anchor = '    var reflectionDescription=skyReflection==null?'
assert text.count(anchor) == 1
injection = '''    if(!(skyReflection is CubemapArray skyArray))throw new Exception("Actual sky reflection is not the captured CubemapArray type");
    if(skyArray.width!=256||skyArray.height!=256||skyArray.cubemapCount<=0)throw new Exception("Actual sky reflection dimensions absent or unexpected");
    var skyLayerCounts=new int[skyArray.mipmapCount];var skyLayerBytes=new int[skyArray.mipmapCount];
    for(int mip=0;mip<skyArray.mipmapCount;mip++){
      var skyReadback=AsyncGPUReadback.Request(skyArray,mip,TextureFormat.RGBAFloat);skyReadback.WaitForCompletion();
      if(skyReadback.hasError)throw new Exception("Actual HDRP sky GPU readback failed mip "+mip);
      skyLayerCounts[mip]=skyReadback.layerCount;skyLayerBytes[mip]=skyReadback.layerDataSize;
      var side=Math.Max(1,skyArray.width>>mip);
      if(skyReadback.layerCount!=skyArray.cubemapCount*6||skyReadback.layerDataSize!=side*side*16)throw new Exception("Sky GPU readback layer layout differs from actual texture");
      using(var writer=new BinaryWriter(File.Open(Path.Combine(root,item.id+".sky-mip-"+mip+".rgba32f"),FileMode.CreateNew))){
        for(int layer=0;layer<skyReadback.layerCount;layer++){
          var values=skyReadback.GetData<float>(layer);
          if(values.Length!=side*side*4)throw new Exception("Sky GPU float count differs");
          for(int i=0;i<values.Length;i++){var value=values[i];if(float.IsNaN(value)||float.IsInfinity(value))throw new Exception("Nonfinite sky capture");writer.Write(value);}
        }
      }
    }
'''
text = text.replace(anchor, injection + anchor)
anchor = 'id=item.id,actual_gpu_ambient_coefficients='
assert text.count(anchor) == 1
text = text.replace(anchor, 'id=item.id,sky_cubemap_count=skyArray.cubemapCount,sky_mip_count=skyArray.mipmapCount,sky_source_format=skyArray.format.ToString(),sky_layer_counts=skyLayerCounts,sky_layer_bytes=skyLayerBytes,actual_gpu_ambient_coefficients=')
compile(text, str(TARGET), 'exec')
TARGET.write_text(text, encoding='utf-8')
subprocess.run([sys.executable, '-B', str(TARGET)], check=True)
