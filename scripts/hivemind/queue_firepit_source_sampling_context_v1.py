"""Read live HDRP mip bias, color buffer format and material texture sampler state."""
from pathlib import Path
import runpy,sys
BASE=Path(__file__).with_name('queue_firepit_source_camera_matrices_v1.py')
loaded=runpy.run_path(str(BASE),run_name='source_sampling_context_probe');g=loaded['g'];original_helper=g['helper'];popen=loaded['popen']
def helper():
    text=original_helper().replace('SourceCameraMatricesV1','SourceSamplingContextV1')
    marker='public string camera_matrices_json,history_view_constants_json;';assert text.count(marker)==1
    text=text.replace(marker,marker+'public string sampling_context_json;')
    marker='[Serializable] public class EnvironmentState';assert text.count(marker)==1
    types='''[Serializable] public class TextureSamplerState {public string material,property,texture_path,graphics_format,filter,wrap_u,wrap_v,wrap_w;public int width,height,mips,anisotropy,loaded_mip,desired_mip;public float mip_bias;public bool streaming;}
 [Serializable] public class SamplingContext {public float global_mip_bias,global_mip_bias_pow2;public int quality_mipmap_limit;public string color_buffer_format;public TextureSamplerState[] textures;public bool[] renderer_property_blocks;}
 '''
    text=text.replace(marker,types+marker)
    marker='var packedProbe=new Vector4[7];actualBuffer.GetData(packedProbe);';assert text.count(marker)==1
    capture='''
    var samplingFlags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic;
    var samplingGlobals=typeof(HDRenderPipeline).GetField("m_ShaderVariablesGlobalCB",samplingFlags).GetValue(currentPipeline);
    var samplerRows=new System.Collections.Generic.List<TextureSamplerState>();
    foreach(var material in sourceDraws.SelectMany(r=>r.sharedMaterials).Distinct())foreach(var property in material.GetTexturePropertyNames()){
     var texture=material.GetTexture(property) as Texture2D;if(texture==null)continue;
     samplerRows.Add(new TextureSamplerState{material=Identity(material),property=property,texture_path=AssetDatabase.GetAssetPath(texture),graphics_format=texture.graphicsFormat.ToString(),filter=texture.filterMode.ToString(),wrap_u=texture.wrapModeU.ToString(),wrap_v=texture.wrapModeV.ToString(),wrap_w=texture.wrapModeW.ToString(),width=texture.width,height=texture.height,mips=texture.mipmapCount,anisotropy=texture.anisoLevel,mip_bias=texture.mipMapBias,streaming=texture.streamingMipmaps,loaded_mip=texture.loadedMipmapLevel,desired_mip=texture.desiredMipmapLevel});
    }
    var samplingContext=new SamplingContext{global_mip_bias=Convert.ToSingle(samplingGlobals.GetType().GetField("_GlobalMipBias",samplingFlags).GetValue(samplingGlobals)),global_mip_bias_pow2=Convert.ToSingle(samplingGlobals.GetType().GetField("_GlobalMipBiasPow2",samplingFlags).GetValue(samplingGlobals)),quality_mipmap_limit=QualitySettings.globalTextureMipmapLimit,color_buffer_format=typeof(HDRenderPipeline).GetMethod("GetColorBufferFormat",samplingFlags).Invoke(currentPipeline,null).ToString(),textures=samplerRows.ToArray(),renderer_property_blocks=sourceDraws.Select(r=>r.HasPropertyBlock()).ToArray()};
'''
    text=text.replace(marker,marker+capture)
    marker='history_view_constants_json=JsonUtility.ToJson(hc.mainViewConstants)};';assert text.count(marker)==1
    return text.replace(marker,'history_view_constants_json=JsonUtility.ToJson(hc.mainViewConstants),sampling_context_json=JsonUtility.ToJson(samplingContext)};')
def corrected_popen(args,*a,**kw):
    if isinstance(args,list) and args and str(args[0])==str(g['UNITY']):
        args=list(args);i=args.index('-executeMethod')+1;assert args[i]=='OpenRPGSourceShadowReferenceV8.Export';args[i]='OpenRPGSourceSamplingContextV1.Export'
    return popen(args,*a,**kw)
g['subprocess'].Popen=corrected_popen
g.update(ROOT=g['W']/'firepit-source-sampling-context-v1',HELPER=g['PROJECT']/'Assets/Editor/SourceSamplingContextV1.cs',helper=helper,__file__=str(Path(__file__).resolve()))
if __name__=='__main__':
    if sys.argv[-1]=='prepare':g['prepare']()
    else:
        g['run']();state=g['read'](g['ROOT']/'RUN-RESULT.v1.json');paths=[g['ROOT']/f'SOURCE-STATE-{v}.json' for v in range(2)]
        if state.get('actual_exit_code')==0 and all(p.exists() for p in paths):
            rows=[g['read'](p) for p in paths];assert all(r['sampling_context_json'] for r in rows);state.update(status='source_sampling_context_and_reference_images_captured',context_files={str(p):g['sha'](p) for p in paths})
        else:state.update(status='source_sampling_context_capture_refused')
        g['save']('RUN-RESULT.v1.json',state)
