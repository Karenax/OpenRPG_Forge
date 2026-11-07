"""Read source GBuffer3 by its current RenderGraph handle in the camera pass."""
from pathlib import Path
import subprocess
import sys

S = Path(__file__).parent
target = S / 'queue_townsmith_inpass_gbuffer3_v74.py'
assert not target.exists()
q = (S / 'queue_townsmith_inpass_normal_v31.py').read_text(encoding='utf-8')
q = q.replace('v31', 'v74').replace('V31', 'V74')
q = q.replace('townsmith-door-source-inpass-normal-v74', 'townsmith-door-source-inpass-gbuffer3-v74')
anchor = "    CS.write_text(text,encoding='utf-8');HELPER.write_bytes(CS.read_bytes())"
assert q.count(anchor) == 1
buffer = '''var pipeline=RenderPipelineManager.currentPipeline;
  if(!(pipeline is HDRenderPipeline))throw new Exception("Actual pipeline is not HDRP");
  var field=typeof(HDRenderPipeline).GetField("m_GBufferOutput",BindingFlags.Instance|BindingFlags.NonPublic);
  if(field==null)throw new Exception("Pinned GBuffer output field absent");
  var outputState=field.GetValue(pipeline);var mrtField=outputState.GetType().GetField("mrt",BindingFlags.Public|BindingFlags.Instance);
  if(mrtField==null)throw new Exception("Pinned MRT field absent");
  var handles=(TextureHandle[])mrtField.GetValue(outputState);
  if(handles==null||handles.Length<=3||!handles[3].IsValid())throw new Exception("Current GBuffer3 RenderGraph handle absent");
  RTHandle actualBuffer=handles[3];var texture=actualBuffer==null?null:actualBuffer.rt;
  if(texture==null||texture.graphicsFormat!=UnityEngine.Experimental.Rendering.GraphicsFormat.B10G11R11_UFloatPack32)throw new Exception("Actual GBuffer3 HDR format differs");'''
changes = [
    ('CustomPassInjectionPoint.BeforePostProcess,sourceNormalPass', 'CustomPassInjectionPoint.BeforePreRefraction,sourceNormalPass'),
    ('injection="BeforePostProcess"', 'injection="BeforePreRefraction"'),
    ('.inpass-normal-', '.inpass-gbuffer3-'),
    ('actual_camera_normal_buffer=true', 'actual_rendergraph_gbuffer3_buffer=true'),
    ('var texture=ctx.cameraNormalBuffer.rt;', buffer),
    ('ctx.cmd.SetComputeTextureParam(shader,kernel,"SourceArray",ctx.cameraNormalBuffer);', 'ctx.cmd.SetComputeTextureParam(shader,kernel,"SourceArray",actualBuffer);'),
]
insert = "    text='using UnityEngine.Rendering.RenderGraphModule;\\n'+text\n"
for before, after in changes:
    insert += '    assert '+repr(before)+' in text\n'
    insert += '    text=text.replace('+repr(before)+','+repr(after)+')\n'
q = q.replace(anchor, insert + anchor)
compile(q, str(target), 'exec')
target.write_text(q, encoding='utf-8')
subprocess.run([sys.executable, '-B', str(target)], check=True)
