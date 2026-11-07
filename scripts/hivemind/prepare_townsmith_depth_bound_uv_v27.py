"""Bind source diagnostic to actual opaque depth; keep the v25 observation."""
from pathlib import Path
import subprocess, sys

s=Path(__file__).parent
target=s/'queue_townsmith_depth_bound_uv_v27.py'
assert not target.exists()
q=(s/'queue_townsmith_uv_derivatives_v25.py').read_text()
q=q.replace('v25','v27').replace('V25','V27')
q=q.replace('townsmith-door-source-uv-derivatives-v27','townsmith-door-source-depth-bound-uv-v27')
assert q.count('Cull Back ZWrite Off ZTest Always')==1
q=q.replace('Cull Back ZWrite Off ZTest Always','Cull Back ZWrite Off ZTest Equal')
replacements={
 'public int capture,mode,width,height;':'public int capture,mode,width,height,viewportWidth,viewportHeight;public bool actual_opaque_depth_bound=true;',
 'int width=ctx.hdCamera.actualWidth,height=ctx.hdCamera.actualHeight;':'int viewportWidth=ctx.hdCamera.actualWidth,viewportHeight=ctx.hdCamera.actualHeight;int width=ctx.cameraDepthBuffer.rt.width,height=ctx.cameraDepthBuffer.rt.height;',
 'if(width!=512||height!=512)throw new Exception("UV viewport differs");':'if(viewportWidth!=512||viewportHeight!=512||ctx.cameraDepthBuffer.rt.dimension!=TextureDimension.Tex2DArray||ctx.cameraDepthBuffer.rt.volumeDepth!=1)throw new Exception("UV viewport/depth layout differs");',
 'hideFlags=HideFlags.HideAndDontSave};\\n   if(!target.Create())':'hideFlags=HideFlags.HideAndDontSave,dimension=TextureDimension.Tex2DArray,volumeDepth=1};\\n   if(!target.Create())',
 'ctx.cmd.SetRenderTarget(target);ctx.cmd.SetViewport(new Rect(0,0,width,height));':'ctx.cmd.SetRenderTarget(new RenderTargetIdentifier(target,0,CubemapFace.Unknown,0),ctx.cameraDepthBuffer.nameID);ctx.cmd.SetViewport(new Rect(0,0,viewportWidth,viewportHeight));',
 'mode=mode,width=width,height=height,format=target.graphicsFormat.ToString()':'mode=mode,width=width,height=height,viewportWidth=viewportWidth,viewportHeight=viewportHeight,format=target.graphicsFormat.ToString()',
}
for before,after in replacements.items():
    assert q.count(before)==1,(before,q.count(before))
    q=q.replace(before,after)
compile(q,str(target),'exec')
target.write_text(q)
subprocess.run([sys.executable,'-B',str(target)],check=True)
