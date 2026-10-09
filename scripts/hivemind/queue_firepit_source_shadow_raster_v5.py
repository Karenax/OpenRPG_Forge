"""Capture the actual HDRP cascade atlas and live shadow request raster state."""
from pathlib import Path
import runpy,sys
BASE=Path(__file__).with_name('queue_firepit_source_shadow_culling_v1.py')
loaded=runpy.run_path(str(BASE),run_name='source_shadow_raster');g=loaded['g'];original_helper=loaded['helper'];popen=loaded['popen']
def helper():
 compute=g['PROJECT']/'Assets/Editor/SourceShadowRasterV5.compute';assert not compute.exists()
 compute.write_text('''#pragma kernel CopyAtlasDepth
Texture2D<float> DepthAtlas;
RWStructuredBuffer<float> Output;
uint AtlasWidth;
uint AtlasHeight;
[numthreads(8,8,1)]void CopyAtlasDepth(uint3 id:SV_DispatchThreadID){if(id.x<AtlasWidth&&id.y<AtlasHeight)Output[id.y*AtlasWidth+id.x]=DepthAtlas.Load(int3(id.xy,0));}
''','utf-8')
 g['save']('DEPTH-PROTOCOL.v1.json',dict(atlas_width=1024,atlas_height=1024,expected_views=2,components_per_pixel=1,storage='float32_le',range_min=0.0,range_max=1.0,comparison_fit_allowed=False,allocation_must_match_unique_descriptor=True,bound_global_texture_claimed=False,compute=str(compute),compute_sha256=g['sha'](compute),full_import=False))
 text=original_helper().replace('SourceShadowCullingV1','SourceShadowRasterV5')
 marker=' static string Hash(';assert text.count(marker)==1
 utilities=r'''
 [Serializable] class ShadowRasterState {public string atlas_name,matching_allocations_json;public int matching_allocation_count;public bool bound_texture_observed;public string atlas_declared_depth_bits,actual_depth_stencil_format,actual_texture_type,requests_json,backend;public int declared_width,declared_height,actual_width,actual_height,request_count;public int[] storage_indices;public float[] slope_bias;public bool actual_texture_observed,reversed_z;}
 [Serializable] class RasterRenderSplit {public int storage_index,split_index;public bool z_clip;public float forward_offset;public float[] view,projection,device_projection_matrix,device_projection_y_flip,inv_view_projection,clip_from_world;}
 [Serializable] class RasterRenderSplitCapture {public int view,active_requests;public RasterRenderSplit[] splits;public bool cached_translation_applied;}
 static float[] RasterMatrix(Matrix4x4 m){var result=new float[16];for(int c=0;c<4;c++)for(int r=0;r<4;r++){float value=m[r,c];if(!float.IsFinite(value))throw new Exception("Nonfinite raster matrix");result[c*4+r]=value;}return result;}
 static object RasterField(object value,string name){for(Type t=value.GetType();t!=null;t=t.BaseType){var f=t.GetField(name,System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.DeclaredOnly);if(f!=null)return f.GetValue(value);var property=t.GetProperty(name,System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.DeclaredOnly);if(property!=null)return property.GetValue(value);}throw new Exception("Raster field missing: "+name);}
 static object RasterItem(object list,int index){var property=list.GetType().GetProperty("Item");if(property==null)throw new Exception("Native list indexer missing");return property.GetValue(list,new object[]{index});}
'''
 text=text.replace(marker,utilities+marker)
 capture=r'''
    var rasterAtlas=RasterField(shadowManager,"m_CascadeAtlas");
    var rasterRequests=RasterField(rasterAtlas,"m_ShadowRequests");int rasterCount=Convert.ToInt32(rasterRequests.GetType().GetProperty("Length").GetValue(rasterRequests));
    var rasterDatabaseType=typeof(HDRenderPipeline).Assembly.GetType("UnityEngine.Rendering.HighDefinition.HDShadowRequestDatabase",true);
    var rasterDatabase=rasterDatabaseType.GetProperty("instance",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic).GetValue(null);
    var rasterStorage=rasterDatabaseType.GetProperty("hdShadowRequestStorage",flags).GetValue(rasterDatabase);
    var rasterIndices=new int[rasterCount];var rasterSlopes=new float[rasterCount];var rasterJson=new System.Text.StringBuilder("[");
    var rasterRenderRows=new List<RasterRenderSplit>();
    for(int i=0;i<rasterCount;i++){var handle=RasterItem(rasterRequests,i);int storageIndex=Convert.ToInt32(handle.GetType().GetProperty("storageIndexForShadowRequest").GetValue(handle));var item=RasterItem(rasterStorage,storageIndex);rasterIndices[i]=storageIndex;rasterSlopes[i]=Convert.ToSingle(RasterField(item,"slopeBias"));if(!float.IsFinite(rasterSlopes[i]))throw new Exception("Nonfinite raster slope");if(i>0)rasterJson.Append(",");rasterJson.Append(JsonUtility.ToJson(item));var split=RasterField(item,"cullingSplit");var splitView=(Matrix4x4)RasterField(split,"view");var splitProjection=(Matrix4x4)RasterField(split,"deviceProjectionYFlip");rasterRenderRows.Add(new RasterRenderSplit{storage_index=storageIndex,split_index=Convert.ToInt32(RasterField(split,"splitIndex")),z_clip=Convert.ToBoolean(RasterField(item,"zClip")),forward_offset=Convert.ToSingle(RasterField(split,"forwardOffset")),view=RasterMatrix(splitView),projection=RasterMatrix((Matrix4x4)RasterField(split,"projection")),device_projection_matrix=RasterMatrix((Matrix4x4)RasterField(split,"deviceProjectionMatrix")),device_projection_y_flip=RasterMatrix(splitProjection),inv_view_projection=RasterMatrix((Matrix4x4)RasterField(split,"invViewProjection")),clip_from_world=RasterMatrix(splitProjection*splitView)});}rasterJson.Append("]");var renderSplitPath=Path.Combine(Path.GetDirectoryName(request.output),"SOURCE-CASTER-SPLITS-"+view+".json");if(File.Exists(renderSplitPath))throw new Exception("Preserve caster split matrix evidence");File.WriteAllText(renderSplitPath,JsonUtility.ToJson(new RasterRenderSplitCapture{view=view,active_requests=rasterCount,splits=rasterRenderRows.ToArray(),cached_translation_applied=false},true));
    var rasterTexture=Shader.GetGlobalTexture("_CascadeShadowAtlas");var rasterRt=rasterTexture as RenderTexture;
    var rasterDesc=rasterAtlas.GetType().GetMethod("GetShadowMapTextureDesc",flags).Invoke(rasterAtlas,null);

    RenderTexture rasterChosen=null;string rasterName=RasterField(rasterDesc,"name").ToString();int rasterMatching=0;var rasterAllocations=new System.Text.StringBuilder("[");
    foreach(var allocation in Resources.FindObjectsOfTypeAll<RenderTexture>()){
     if(allocation.width!=Convert.ToInt32(RasterField(rasterDesc,"width"))||allocation.height!=Convert.ToInt32(RasterField(rasterDesc,"height"))||allocation.name.IndexOf(rasterName,StringComparison.OrdinalIgnoreCase)<0)continue;
     rasterChosen=allocation;if(rasterMatching++>0)rasterAllocations.Append(",");rasterAllocations.Append("{\"name\":\"").Append(allocation.name.Replace("\\","\\\\").Replace("\"","\\\"")).Append("\",\"width\":").Append(allocation.width).Append(",\"height\":").Append(allocation.height).Append(",\"depth_stencil_format\":\"").Append(allocation.depthStencilFormat).Append("\",\"created\":").Append(allocation.IsCreated()?"true":"false").Append("}");
    }rasterAllocations.Append("]");

    if(rasterMatching!=1||rasterChosen==null||!rasterChosen.IsCreated()||rasterChosen.depthStencilFormat!=UnityEngine.Experimental.Rendering.GraphicsFormat.D32_SFloat)throw new Exception("Depth capture requires unique live D32 allocation");
    int rasterPixels=checked(rasterChosen.width*rasterChosen.height);var rasterOut=new ComputeBuffer(rasterPixels,4);var rasterCommands=new CommandBuffer();
    try{
     var rasterCompute=AssetDatabase.LoadAssetAtPath<ComputeShader>("Assets/Editor/SourceShadowRasterV5.compute");if(rasterCompute==null)throw new Exception("Depth copy shader absent");int kernel=rasterCompute.FindKernel("CopyAtlasDepth");
     rasterCommands.SetComputeTextureParam(rasterCompute,kernel,"DepthAtlas",new RenderTargetIdentifier(rasterChosen),0,RenderTextureSubElement.Depth);
     rasterCommands.SetComputeBufferParam(rasterCompute,kernel,"Output",rasterOut);rasterCommands.SetComputeIntParam(rasterCompute,"AtlasWidth",rasterChosen.width);rasterCommands.SetComputeIntParam(rasterCompute,"AtlasHeight",rasterChosen.height);rasterCommands.DispatchCompute(rasterCompute,kernel,(rasterChosen.width+7)/8,(rasterChosen.height+7)/8,1);Graphics.ExecuteCommandBuffer(rasterCommands);
     var rasterValues=new float[rasterPixels];rasterOut.GetData(rasterValues);foreach(float sample in rasterValues)if(!float.IsFinite(sample)||sample<0.0f||sample>1.0f)throw new Exception("Depth samples outside finite [0,1]");var rasterDepthBytes=new byte[rasterPixels*4];Buffer.BlockCopy(rasterValues,0,rasterDepthBytes,0,rasterDepthBytes.Length);var rasterDepthPath=Path.Combine(Path.GetDirectoryName(request.output),"SOURCE-SHADOW-DEPTH-"+view+".float32");if(File.Exists(rasterDepthPath))throw new Exception("Preserve source depth pixels");File.WriteAllBytes(rasterDepthPath,rasterDepthBytes);
    }finally{rasterOut.Release();rasterCommands.Release();}
    var rasterState=new ShadowRasterState{atlas_name=rasterName,matching_allocations_json=rasterAllocations.ToString(),matching_allocation_count=rasterMatching,bound_texture_observed=rasterRt!=null,atlas_declared_depth_bits=RasterField(rasterDesc,"depthBufferBits").ToString(),declared_width=Convert.ToInt32(RasterField(rasterDesc,"width")),declared_height=Convert.ToInt32(RasterField(rasterDesc,"height")),actual_depth_stencil_format=rasterRt==null?"unobserved":rasterRt.depthStencilFormat.ToString(),actual_texture_type=rasterTexture==null?"unobserved":rasterTexture.GetType().FullName,actual_width=rasterTexture==null?0:rasterTexture.width,actual_height=rasterTexture==null?0:rasterTexture.height,actual_texture_observed=rasterRt!=null,request_count=rasterCount,storage_indices=rasterIndices,slope_bias=rasterSlopes,requests_json=rasterJson.ToString(),backend=SystemInfo.graphicsDeviceType.ToString(),reversed_z=SystemInfo.usesReversedZBuffer};
    var rasterPath=Path.Combine(Path.GetDirectoryName(request.output),"SOURCE-SHADOW-RASTER-"+view+".json");if(File.Exists(rasterPath))throw new Exception("Preserve raster evidence");File.WriteAllText(rasterPath,JsonUtility.ToJson(rasterState,true));
'''
 marker='File.WriteAllText(cullingPath,JsonUtility.ToJson(cullingState,true));';assert text.count(marker)==1
 return text.replace(marker,marker+capture)
def corrected_popen(args,*a,**kw):
 if isinstance(args,list) and args and str(args[0])==str(g['UNITY']):
  args=list(args);i=args.index('-executeMethod')+1;assert args[i]=='OpenRPGSourceShadowReferenceV8.Export';args[i]='OpenRPGSourceShadowRasterV5.Export'
 return popen(args,*a,**kw)
g['subprocess'].Popen=corrected_popen
g.update(ROOT=g['W']/'firepit-source-shadow-raster-v5',HELPER=g['PROJECT']/'Assets/Editor/SourceShadowRasterV5.cs',helper=helper,__file__=str(Path(__file__).resolve()))
if __name__=='__main__':
 if sys.argv[-1]=='prepare':g['prepare']()
 else:
  g['run']();state=g['read'](g['ROOT']/'RUN-RESULT.v1.json');paths=[g['ROOT']/f'SOURCE-SHADOW-RASTER-{i}.json' for i in range(2)]+[g['ROOT']/f'SOURCE-SHADOW-DEPTH-{i}.float32' for i in range(2)]
  if state.get('actual_exit_code')==0 and all(p.exists() for p in paths):state.update(status='source_cascade_atlas_depth_gpu_pixels_and_live_raster_requests_captured',raster_files={str(p):g['sha'](p) for p in paths})
  else:state.update(status='source_cascade_raster_capture_refused')
  g['save']('RUN-RESULT.v1.json',state)
