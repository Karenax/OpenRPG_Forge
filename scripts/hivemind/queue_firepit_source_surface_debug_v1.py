"""Use public HDRP debug display; StandardRequest explicitly disables AOV."""
from pathlib import Path
import runpy,sys
BASE=Path(__file__).with_name('queue_firepit_source_aov_v1.py')
loaded=runpy.run_path(str(BASE),run_name='hdrp_debug_probe')
g=loaded['g'];original_helper=g['helper'];popen=loaded['popen']
ROLES=['Albedo','Normal','Smoothness','AmbientOcclusion','Metal','Specular','DirectDiffuseLighting','IndirectDiffuseLighting']
DEBUG=r'''
   var debug=((HDRenderPipeline)RenderPipelineManager.currentPipeline).debugDisplaySettings;
   if(debug.IsDebugDisplayEnabled())throw new Exception("Refuse preexisting debug settings");
   var diagnostics=new[]{"Albedo","Normal","Smoothness","AmbientOcclusion","Metal","Specular","DirectDiffuseLighting","IndirectDiffuseLighting"};
   try {foreach(var diagnostic in diagnostics){
    debug.SetDebugViewCommonMaterialProperty(UnityEngine.Rendering.HighDefinition.Attributes.MaterialSharedProperty.None);debug.SetDebugLightingMode(DebugLightingMode.None);
    if(diagnostic.EndsWith("Lighting"))debug.SetDebugLightingMode((DebugLightingMode)Enum.Parse(typeof(DebugLightingMode),diagnostic));
    else debug.SetDebugViewCommonMaterialProperty((UnityEngine.Rendering.HighDefinition.Attributes.MaterialSharedProperty)Enum.Parse(typeof(UnityEngine.Rendering.HighDefinition.Attributes.MaterialSharedProperty),diagnostic));
    if(!debug.IsDebugDisplayEnabled())throw new Exception("Diagnostic not enabled: "+diagnostic);
    var renderDebug=new RenderPipeline.StandardRequest{destination=target};for(int warm=0;warm<3;warm++)RenderPipeline.SubmitRenderRequest(camera,renderDebug);
    var previousActive=RenderTexture.active;Texture2D debugRaw=null;
    try {
     RenderTexture.active=target;debugRaw=new Texture2D(512,512,TextureFormat.RGBAHalf,false,true);debugRaw.ReadPixels(new Rect(0,0,512,512),0,0,false);debugRaw.Apply(false,false);
     byte[] bytesDebug=debugRaw.GetRawTextureData<byte>().ToArray();if(bytesDebug.Length!=512*512*8)throw new Exception("Unexpected diagnostic raw length");
     var rawDebugPath=Path.Combine(Path.GetDirectoryName(request.output),"DEBUG-"+diagnostic+".rgba16f");if(File.Exists(rawDebugPath))throw new Exception("Preserve debug evidence");File.WriteAllBytes(rawDebugPath,bytesDebug);
     File.WriteAllText(rawDebugPath+".json","{\"role\":\""+diagnostic+"\",\"source_shadows\":false,\"width\":512,\"height\":512,\"native_hdrp_debug_render\":true,\"raw_sha256\":\""+Hash(bytesDebug)+"\",\"shader_surface_debug_encoding_requires_sdk_interpretation\":true,\"full_import\":false}");
    }finally{RenderTexture.active=previousActive;if(debugRaw!=null)UnityEngine.Object.DestroyImmediate(debugRaw);}
   }}finally{debug.SetDebugViewCommonMaterialProperty(UnityEngine.Rendering.HighDefinition.Attributes.MaterialSharedProperty.None);debug.SetDebugLightingMode(DebugLightingMode.None);}
'''
def helper():
    text=original_helper().replace('SourceSurfaceAovV1','SourceSurfaceDebugV1')
    assert text.count(loaded['AOV'])==1
    return text.replace(loaded['AOV'],DEBUG)
def corrected_popen(args,*a,**kw):
    if isinstance(args,list) and args and str(args[0])==str(g['UNITY']):
        args=list(args);i=args.index('-executeMethod')+1
        assert args[i]=='OpenRPGSourceShadowReferenceV8.Export'
        args[i]='OpenRPGSourceSurfaceDebugV1.Export'
    return popen(args,*a,**kw)
g['subprocess'].Popen=corrected_popen
g.update(ROOT=g['W']/'firepit-source-surface-debug-v1',HELPER=g['PROJECT']/'Assets/Editor/SourceSurfaceDebugV1.cs',helper=helper,__file__=str(Path(__file__).resolve()))
if __name__=='__main__':
    if sys.argv[-1]=='prepare':g['prepare']()
    else:
        g['run']();state=g['read'](g['ROOT']/'RUN-RESULT.v1.json')
        missing=[role for role in ROLES if not (g['ROOT']/f'DEBUG-{role}.rgba16f.json').exists()]
        if state.get('actual_exit_code')==0 and not missing:
            state.update(status='original_hdrp_surface_and_lighting_debug_captured',debug_roles=ROLES,debug_images=8,debug_files={str(p):g['sha'](p) for role in ROLES for p in [g['ROOT']/f'DEBUG-{role}.rgba16f',g['ROOT']/f'DEBUG-{role}.rgba16f.json']})
        else:state.update(status='source_surface_debug_capture_refused',missing_debug_roles=missing)
        g['save']('RUN-RESULT.v1.json',state)
