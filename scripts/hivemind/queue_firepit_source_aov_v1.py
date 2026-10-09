"""Capture original HDRP surface diagnostics through the SDK AOV API."""
from pathlib import Path
import runpy,sys
BASE=Path(__file__).with_name('queue_firepit_shadow_reference_v10.py')
loaded=runpy.run_path(str(BASE),run_name='source_aov_probe')
g=loaded['g'];original_helper=g['helper'];popen=loaded['popen']
AOV=r'''
   var diagnostics=new[]{"Albedo","Normal","Smoothness","AmbientOcclusion","Metal","Specular","DirectDiffuseOnly","IndirectDiffuseOnly"};
   foreach(var diagnostic in diagnostics){
    var requestAov=AOVRequest.NewDefault();
    if(diagnostic.EndsWith("Only"))requestAov.SetFullscreenOutput((LightingProperty)Enum.Parse(typeof(LightingProperty),diagnostic));
    else requestAov.SetFullscreenOutput((UnityEngine.Rendering.HighDefinition.Attributes.MaterialSharedProperty)Enum.Parse(typeof(UnityEngine.Rendering.HighDefinition.Attributes.MaterialSharedProperty),diagnostic));
    RTHandle handle=null;RenderTexture aovTarget=null;Texture2D aovRaw=null;AOVRequestDataCollection collection=null;
    int callbacks=0;var previousActive=RenderTexture.active;
    try{
     handle=RTHandles.Alloc(512,512,colorFormat:UnityEngine.Experimental.Rendering.GraphicsFormat.R16G16B16A16_SFloat);
     aovTarget=new RenderTexture(512,512,0,RenderTextureFormat.ARGBHalf,RenderTextureReadWrite.Linear);aovTarget.Create();
     collection=new AOVRequestBuilder().Add(requestAov,id=>handle,null,new[]{AOVBuffers.Color},(cmd,textures,properties)=>{if(textures.Count!=1)throw new Exception("Unexpected AOV buffer count");cmd.CopyTexture(textures[0].rt,aovTarget);callbacks++;}).Build();
     extra.SetAOVRequests(collection);var renderAov=new RenderPipeline.StandardRequest{destination=target};
     for(int warm=0;warm<3;warm++)RenderPipeline.SubmitRenderRequest(camera,renderAov);
     if(callbacks<1)throw new Exception("No AOV GPU callback: "+diagnostic);
     RenderTexture.active=aovTarget;aovRaw=new Texture2D(512,512,TextureFormat.RGBAHalf,false,true);aovRaw.ReadPixels(new Rect(0,0,512,512),0,0,false);aovRaw.Apply(false,false);
     byte[] bytesAov=aovRaw.GetRawTextureData<byte>().ToArray();if(bytesAov.Length!=512*512*8)throw new Exception("Unexpected AOV raw length");
     var rawAovPath=Path.Combine(Path.GetDirectoryName(request.output),"AOV-"+diagnostic+".rgba16f");if(File.Exists(rawAovPath))throw new Exception("Preserve AOV evidence");File.WriteAllBytes(rawAovPath,bytesAov);
     File.WriteAllText(rawAovPath+".json","{\"role\":\""+diagnostic+"\",\"source_shadows\":false,\"width\":512,\"height\":512,\"gpu_callbacks\":"+callbacks+",\"raw_sha256\":\""+Hash(bytesAov)+"\",\"buffer\":\"AOVBuffers.Color\",\"source_lod0_diagnostic_only\":true,\"shader_surface_debug_encoding_requires_sdk_interpretation\":true,\"full_import\":false}");
    }finally{extra.SetAOVRequests(null);if(collection!=null)collection.Dispose();RenderTexture.active=previousActive;if(aovRaw!=null)UnityEngine.Object.DestroyImmediate(aovRaw);if(handle!=null)RTHandles.Release(handle);if(aovTarget!=null){aovTarget.Release();UnityEngine.Object.DestroyImmediate(aovTarget);}}
   }
'''
def helper():
    text=original_helper().replace('SourceShadowReferenceV10','SourceSurfaceAovV1')
    needle='   if(sourceBefore!=EditorJsonUtility.ToJson(source,true))'
    assert text.count(needle)==1
    return text.replace(needle,AOV+'\n'+needle)
def corrected_popen(args,*a,**kw):
    if isinstance(args,list) and args and str(args[0])==str(g['UNITY']):
        args=list(args);i=args.index('-executeMethod')+1
        assert args[i]=='OpenRPGSourceShadowReferenceV8.Export'
        args[i]='OpenRPGSourceSurfaceAovV1.Export'
    return popen(args,*a,**kw)
g['subprocess'].Popen=corrected_popen
g.update(ROOT=g['W']/'firepit-source-surface-aov-v1',HELPER=g['PROJECT']/'Assets/Editor/SourceSurfaceAovV1.cs',helper=helper,__file__=str(Path(__file__).resolve()))
if __name__=='__main__':
    if sys.argv[-1]=='prepare':g['prepare']()
    else:
        g['run']();state=g['read'](g['ROOT']/'RUN-RESULT.v1.json')
        roles=['Albedo','Normal','Smoothness','AmbientOcclusion','Metal','Specular','DirectDiffuseOnly','IndirectDiffuseOnly']
        missing=[role for role in roles if not (g['ROOT']/f'AOV-{role}.rgba16f.json').exists()]
        if state.get('actual_exit_code')==0 and not missing:
            state.update(status='original_hdrp_surface_and_lighting_aov_captured',aov_roles=roles,aov_images=8,aov_files={str(p):g['sha'](p) for role in roles for p in [g['ROOT']/f'AOV-{role}.rgba16f',g['ROOT']/f'AOV-{role}.rgba16f.json']})
        else:state.update(status='source_aov_capture_refused',missing_aov_roles=missing)
        g['save']('RUN-RESULT.v1.json',state)
