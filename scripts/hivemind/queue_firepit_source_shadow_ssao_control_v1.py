"""Keep original source pair, then isolate SSAO in a private scene only."""
from pathlib import Path
from datetime import datetime,timezone
import runpy,sys,json
BASE=Path(__file__).with_name('queue_firepit_source_surface_debug_v1.py')
loaded=runpy.run_path(str(BASE),run_name='shadow_ssao_control')
g=loaded['g'];original_helper=g['helper'];popen=loaded['popen']
CONTROL=r'''
   if(presentationProfile.Has<ScreenSpaceAmbientOcclusion>())throw new Exception("Refuse preexisting private SSAO override");
   try {
    foreach(var mode in new[]{"OriginalRepeat","NoSSAO"}) {
     if(mode=="NoSSAO") {var aoOverride=presentationProfile.Add<ScreenSpaceAmbientOcclusion>();aoOverride.intensity.Override(0f);}
     for(int view=0;view<2;view++) {
      foreach(var draw in sourceDraws)draw.shadowCastingMode=view==0?ShadowCastingMode.On:ShadowCastingMode.Off;
      var controlRequest=new RenderPipeline.StandardRequest{destination=target};
      for(int warm=0;warm<3;warm++)RenderPipeline.SubmitRenderRequest(camera,controlRequest);
      var historyType=typeof(HDCamera).GetNestedType("HistoryChannel",System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic);
      var historyMethod=typeof(HDCamera).GetMethod("GetOrCreate",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic,null,new[]{typeof(Camera),typeof(int),historyType},null);
      if(historyType==null||historyMethod==null)throw new Exception("Pinned actual history API missing");
      var hc=(HDCamera)historyMethod.Invoke(null,new object[]{camera,0,Enum.Parse(historyType,"CustomUserHistory0")});
      var effective=hc.volumeStack.GetComponent<ScreenSpaceAmbientOcclusion>();
      if(mode=="NoSSAO"&&effective.intensity.value!=0f)throw new Exception("SSAO control not applied to actual rendered history");
      if(mode=="OriginalRepeat"&&effective.intensity.value<=0f)throw new Exception("Original SSAO unexpectedly disabled");
      var prior=RenderTexture.active;Texture2D pixels=null;
      try {
       RenderTexture.active=target;pixels=new Texture2D(512,512,TextureFormat.RGBAHalf,false,true);pixels.ReadPixels(new Rect(0,0,512,512),0,0,false);pixels.Apply(false,false);
       var bytesControl=pixels.GetRawTextureData<byte>().ToArray();if(bytesControl.Length!=512*512*8)throw new Exception("Unexpected control image size");
       var path=Path.Combine(Path.GetDirectoryName(request.output),"CONTROL-"+mode+"-"+view+".rgba16f");if(File.Exists(path))throw new Exception("Preserve control evidence");File.WriteAllBytes(path,bytesControl);
       File.WriteAllText(path+".json",JsonUtility.ToJson(new OcclusionControl {mode=mode,view=view,source_shadows=view==0,intensity=effective.intensity.value,temporal=effective.temporalAccumulation.value,ssao_frame_enabled=hc.frameSettings.IsEnabled(FrameSettingsField.SSAO),history="CustomUserHistory0",quality_anisotropy=QualitySettings.anisotropicFiltering.ToString(),raw_sha256=Hash(bytesControl)},true));
      }finally{RenderTexture.active=prior;if(pixels!=null)UnityEngine.Object.DestroyImmediate(pixels);}
     }
    }
   }finally{presentationProfile.Remove<ScreenSpaceAmbientOcclusion>();}
'''
def helper():
    text=original_helper().replace('SourceSurfaceDebugV1','SourceShadowSsaoControlV1')
    declaration=' [Serializable] public class Row'
    assert text.count(declaration)==1
    text=text.replace(declaration,' [Serializable] public class OcclusionControl {public string mode,history,quality_anisotropy,raw_sha256;public int view;public bool source_shadows,temporal,ssao_frame_enabled;public float intensity;}\n'+declaration)
    assert text.count(loaded['DEBUG'])==1
    return text.replace(loaded['DEBUG'],CONTROL)
def corrected_popen(args,*a,**kw):
    if isinstance(args,list) and args and str(args[0])==str(g['UNITY']):
        args=list(args);i=args.index('-executeMethod')+1
        assert args[i]=='OpenRPGSourceShadowReferenceV8.Export'
        args[i]='OpenRPGSourceShadowSsaoControlV1.Export'
    return popen(args,*a,**kw)
g['subprocess'].Popen=corrected_popen
g.update(ROOT=g['W']/'firepit-source-shadow-ssao-control-v1',HELPER=g['PROJECT']/'Assets/Editor/SourceShadowSsaoControlV1.cs',helper=helper,__file__=str(Path(__file__).resolve()))
if __name__=='__main__':
    if sys.argv[-1]=='prepare':
        plan=g['W']/'firepit-source-shadow-ssao-control-plan-v1'
        assert not plan.exists() and not g['ROOT'].exists()
        plan.mkdir();(plan/'PROTOCOL.v1.json').write_text(json.dumps(dict(created_utc=datetime.now(timezone.utc).isoformat(),
            source_control_only=True,original_reference_pair_captured_first=True,original_references_not_replaced=True,
            modes=['OriginalRepeat','NoSSAO'],views_per_mode=2,mask_linear_channel_delta=1e-4,
            changed_parameter='private presentation volume SSAO intensity = 0',actual_history_channel='CustomUserHistory0',
            source_material_and_camera_unchanged=True,thresholds_not_fitted_after_pixels=True,full_import=False),indent=2),'utf-8')
        g['prepare']()
    else:
        g['run']();state=g['read'](g['ROOT']/'RUN-RESULT.v1.json')
        files=[g['ROOT']/f'CONTROL-{mode}-{view}.rgba16f' for mode in ['OriginalRepeat','NoSSAO'] for view in range(2)]
        if state.get('actual_exit_code')==0 and all(p.exists() and p.with_suffix(p.suffix+'.json').exists() for p in files):
            state.update(status='original_source_shadow_pair_and_private_ssao_control_captured',control_files={str(p):g['sha'](p) for raw in files for p in [raw,raw.with_suffix(raw.suffix+'.json')]})
        else:state.update(status='source_shadow_ssao_control_capture_refused')
        g['save']('RUN-RESULT.v1.json',state)
