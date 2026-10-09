"""Read the actual CustomUserHistory0 camera, not a new render-loop camera."""
from pathlib import Path
import runpy,sys
BASE=Path(__file__).with_name('queue_firepit_shadow_reference_v10.py')
loaded=runpy.run_path(str(BASE),run_name='actual_history_camera_probe')
g=loaded['g'];original_helper=g['helper'];popen=loaded['popen']
def helper():
    text=original_helper().replace('SourceShadowReferenceV10','SourceShadowReferenceV11')
    before='public float[] ambient_probe;'
    assert text.count(before)==1
    text=text.replace(before,'public float[] ambient_probe,hdrp_ambient_probe;public Vector4[] hdrp_diffuse_probe_buffer;public string source_history_channel;')
    before='var hc=HDCamera.GetOrCreate(camera);var stack=hc.volumeStack;'
    assert text.count(before)==1
    after=r'''var historyType=typeof(HDCamera).GetNestedType("HistoryChannel",System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic);
    if(historyType==null)throw new Exception("Pinned HDRP history enum missing");
    var historyMethod=typeof(HDCamera).GetMethod("GetOrCreate",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic,null,new[]{typeof(Camera),typeof(int),historyType},null);
    if(historyMethod==null)throw new Exception("Pinned HDRP history method missing");
    var history=Enum.Parse(historyType,"CustomUserHistory0");var hc=(HDCamera)historyMethod.Invoke(null,new object[]{camera,0,history});var stack=hc.volumeStack;
    var currentPipeline=(HDRenderPipeline)RenderPipelineManager.currentPipeline;
    var skyManager=typeof(HDRenderPipeline).GetProperty("skyManager",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(currentPipeline);
    var ambientMethod=skyManager.GetType().GetMethod("GetAmbientProbe",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic,null,new[]{typeof(HDCamera)},null);
    var actualProbe=(UnityEngine.Rendering.SphericalHarmonicsL2)ambientMethod.Invoke(skyManager,new object[]{hc});
    var actualCoefficients=new float[27];for(int channel=0;channel<3;channel++)for(int coefficient=0;coefficient<9;coefficient++)actualCoefficients[channel*9+coefficient]=actualProbe[channel,coefficient];
    var bufferMethod=skyManager.GetType().GetMethod("GetDiffuseAmbientProbeBuffer",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic,null,new[]{typeof(HDCamera)},null);
    var actualBuffer=(GraphicsBuffer)bufferMethod.Invoke(skyManager,new object[]{hc});if(actualBuffer==null||actualBuffer.count!=7||actualBuffer.stride!=16)throw new Exception("Unexpected HDRP diffuse SH buffer layout");var packedProbe=new Vector4[7];actualBuffer.GetData(packedProbe);'''
    text=text.replace(before,after)
    before='ambient_probe=probe};'
    assert text.count(before)==1
    return text.replace(before,'ambient_probe=probe,hdrp_ambient_probe=actualCoefficients,hdrp_diffuse_probe_buffer=packedProbe,source_history_channel="CustomUserHistory0"};')
def corrected_popen(args,*a,**kw):
    if isinstance(args,list) and args and str(args[0])==str(g['UNITY']):
        args=list(args);i=args.index('-executeMethod')+1
        assert args[i]=='OpenRPGSourceShadowReferenceV8.Export'
        args[i]='OpenRPGSourceShadowReferenceV11.Export'
    return popen(args,*a,**kw)
g['subprocess'].Popen=corrected_popen
g.update(ROOT=g['W']/'firepit-shadow-source-reference-v11',HELPER=g['PROJECT']/'Assets/Editor/SourceShadowReferenceV11.cs',helper=helper,__file__=str(Path(__file__).resolve()))
if __name__=='__main__':
    if sys.argv[-1]=='prepare':g['prepare']()
    else:
        g['run']();state=g['read'](g['ROOT']/'RUN-RESULT.v1.json')
        missing=[str(g['ROOT']/f'SOURCE-STATE-{i}.json') for i in range(2) if not (g['ROOT']/f'SOURCE-STATE-{i}.json').exists()]
        if state.get('actual_exit_code')==0 and not missing:
            states=[g['read'](g['ROOT']/f'SOURCE-STATE-{i}.json') for i in range(2)]
            assert all(s['source_history_channel']=='CustomUserHistory0' and len(s['hdrp_diffuse_probe_buffer'])==7 for s in states)
            state.update(status='actual_request_history_camera_and_hdrp_probe_captured',environment_files={str(g['ROOT']/f'SOURCE-STATE-{i}.json'):g['sha'](g['ROOT']/f'SOURCE-STATE-{i}.json') for i in range(2)})
        else:state.update(status='actual_history_environment_capture_refused',missing_environment_files=missing)
        g['save']('RUN-RESULT.v1.json',state)
