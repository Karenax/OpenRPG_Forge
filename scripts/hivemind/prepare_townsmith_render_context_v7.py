"""Capture the actual HDRP ambient GPU buffer from the existing request history."""
from pathlib import Path
import subprocess
import sys

S = Path(__file__).parent
target = S / 'queue_townsmith_render_context_v7.py'
assert not target.exists()
text = (S / 'queue_townsmith_render_context_v6.py').read_text(encoding='utf-8')
text = text.replace('townsmith-door-source-render-context-v6', 'townsmith-door-source-render-context-v7')
text = text.replace('SourceTownsmithRenderContextV6', 'SourceTownsmithRenderContextV7')
anchor = 'public string actual_camera_history;'
assert text.count(anchor) == 1
text = text.replace(anchor, anchor + ' public float[] actual_gpu_ambient_coefficients; public int ambient_buffer_count,ambient_buffer_stride;')
anchor = '    var reflectionMethod=manager.GetType().GetMethod("GetSkyReflection",'
assert text.count(anchor) == 1
injection = '''    var gpuProbeMethod=manager.GetType().GetMethod("GetDiffuseAmbientProbeBuffer",instanceFlags,null,new[]{typeof(HDCamera)},null);if(gpuProbeMethod==null)throw new Exception("Pinned HDRP diffuse GPU buffer method absent");
    var gpuProbe=gpuProbeMethod.Invoke(manager,new object[]{sourceHdCamera}) as GraphicsBuffer;
    if(gpuProbe==null||!gpuProbe.IsValid()||gpuProbe.count!=7||gpuProbe.stride!=16)throw new Exception("Actual HDRP GPU ambient buffer absent or unexpected layout");
    var gpuVectors=new Vector4[7];gpuProbe.GetData(gpuVectors);
    var gpuCoefficients=new float[28];for(int i=0;i<7;i++)for(int ch=0;ch<4;ch++)gpuCoefficients[i*4+ch]=gpuVectors[i][ch];
'''
text = text.replace(anchor, injection + anchor)
anchor = 'id=item.id,ambient_probe=ambientCoefficients,'
assert text.count(anchor) == 1
text = text.replace(anchor, 'id=item.id,actual_gpu_ambient_coefficients=gpuCoefficients,ambient_buffer_count=gpuProbe.count,ambient_buffer_stride=gpuProbe.stride,ambient_probe=ambientCoefficients,')
compile(text, str(target), 'exec')
target.write_text(text, encoding='utf-8')
subprocess.run([sys.executable, '-B', str(target)], check=True)
