"""Read the observed exact D16 punctual allocation; keep D32 refusals intact."""
from pathlib import Path
base=Path(__file__).with_name('queue_firepit_source_point_raster_v2.py')
source=base.read_text('utf-8').replace('SourcePointShadowRasterV2','SourcePointShadowRasterV3').replace('firepit-source-point-shadow-raster-v2','firepit-source-point-shadow-raster-v3')
guard='RasterField(rasterDesc,"depthBufferBits").ToString()!="Depth32"||(rasterChosen.depthStencilFormat!=UnityEngine.Experimental.Rendering.GraphicsFormat.D32_SFloat&&rasterChosen.depthStencilFormat!=UnityEngine.Experimental.Rendering.GraphicsFormat.D32_SFloat_S8_UInt)'
assert source.count(guard)==1
source=source.replace(guard,'RasterField(rasterDesc,"depthBufferBits").ToString()!="Depth16"||rasterChosen.depthStencilFormat!=UnityEngine.Experimental.Rendering.GraphicsFormat.D16_UNorm')
source=source.replace('Point atlas allocation/declared D32 format mismatch','Point atlas allocation/declared D16 format mismatch')
exec(compile(source,str(base),'exec'),globals())
