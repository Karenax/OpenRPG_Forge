"""Persist exact allocation observations, retaining the first refused capture."""
from pathlib import Path
import sys
base=Path(__file__).with_name('queue_firepit_source_shadow_raster_v6.py')
source=base.read_text('utf-8').replace('SourceShadowRasterV6','SourcePointShadowRasterV2').replace('firepit-source-shadow-raster-v6','firepit-source-point-shadow-raster-v2')
source=source.replace('"m_CascadeAtlas"','"m_Atlas"').replace('"_CascadeShadowAtlas"','"_ShadowmapAtlas"').replace('atlas_width=1024,atlas_height=1024','atlas_width=4096,atlas_height=4096')
source=source.replace("if __name__=='__main__':","if __name__=='unused_helper_bootstrap':")
exec(compile(source,str(base),'exec'),globals())
point_base_helper=helper

def point_helper():
    text=point_base_helper()
    marker=' static string Hash(';assert text.count(marker)==1
    classes=' [Serializable] class PointAtlasAllocation {public string name,format;public int width,height;public bool created;}\n [Serializable] class PointAtlasAllocationProbe {public string descriptor_name,declared_depth_bits;public int declared_width,declared_height,matching_count;public PointAtlasAllocation[] allocations;}\n'
    text=text.replace(marker,classes+marker)
    guard='    if(rasterMatching!=1||rasterChosen==null||!rasterChosen.IsCreated()||rasterChosen.depthStencilFormat!=UnityEngine.Experimental.Rendering.GraphicsFormat.D32_SFloat)throw new Exception("Depth capture requires unique live D32 allocation");'
    assert text.count(guard)==1
    observation='''    var allocationProbe=new PointAtlasAllocationProbe{descriptor_name=rasterName,declared_depth_bits=RasterField(rasterDesc,"depthBufferBits").ToString(),declared_width=Convert.ToInt32(RasterField(rasterDesc,"width")),declared_height=Convert.ToInt32(RasterField(rasterDesc,"height")),matching_count=rasterMatching,allocations=Resources.FindObjectsOfTypeAll<RenderTexture>().Where(t=>t.width>=1024||t.name.IndexOf("Shadow",StringComparison.OrdinalIgnoreCase)>=0).Select(t=>new PointAtlasAllocation{name=t.name,format=t.depthStencilFormat.ToString(),width=t.width,height=t.height,created=t.IsCreated()}).ToArray()};
    File.WriteAllText(Path.Combine(Path.GetDirectoryName(request.output),"POINT-ATLAS-ALLOCATION-"+view+".json"),JsonUtility.ToJson(allocationProbe,true));
'''
    # Observe either actual D32 float allocation, with or without a stencil plane.
    # Preserve its exact format in the receipt; do not relabel a stencil format.
    revised='    if(rasterMatching!=1||rasterChosen==null||!rasterChosen.IsCreated()||RasterField(rasterDesc,"depthBufferBits").ToString()!="Depth32"||(rasterChosen.depthStencilFormat!=UnityEngine.Experimental.Rendering.GraphicsFormat.D32_SFloat&&rasterChosen.depthStencilFormat!=UnityEngine.Experimental.Rendering.GraphicsFormat.D32_SFloat_S8_UInt))throw new Exception("Point atlas allocation/declared D32 format mismatch; inspect allocation probe");'
    return text.replace(guard,observation+revised)

g.update(helper=point_helper,__file__=str(Path(__file__).resolve()))
if __name__=='__main__':
    if sys.argv[-1]=='prepare':g['prepare']()
    else:
        g['run']();state=g['read'](g['ROOT']/'RUN-RESULT.v1.json')
        paths=[g['ROOT']/f'SOURCE-SHADOW-RASTER-{i}.json' for i in range(2)]+[g['ROOT']/f'SOURCE-SHADOW-DEPTH-{i}.float32' for i in range(2)]
        state.update(status='source_point_atlas_depth_and_live_raster_requests_captured' if state.get('actual_exit_code')==0 and all(p.exists() for p in paths) else 'source_point_atlas_capture_refused_observation_preserved')
        g['save']('RUN-RESULT.v1.json',state)
