"""Read actual HDRP shadow GPU records; retain original reference pixels."""
from pathlib import Path
import runpy,sys
BASE=Path(__file__).with_name('queue_firepit_source_directional_gpu_v1.py')
loaded=runpy.run_path(str(BASE),run_name='source_shadow_gpu')
g=loaded['g'];original_helper=g['helper'];popen=loaded['popen']
BUFFER=r'''
 static string ShadowBytes(ComputeBuffer buffer,Type layout,int valid,out string records) {
  int stride=System.Runtime.InteropServices.Marshal.SizeOf(layout);
  if(buffer==null||buffer.stride!=stride||valid<0||valid>buffer.count)throw new Exception("Shadow GPU buffer/count/layout invalid");
  var readback=AsyncGPUReadback.Request(buffer);readback.WaitForCompletion();if(readback.hasError)throw new Exception("Shadow GPU readback refused");
  var bytes=readback.GetData<byte>().ToArray();if(bytes.Length!=buffer.count*stride)throw new Exception("Shadow GPU byte length invalid");
  var result=new System.Text.StringBuilder("[");var pinned=System.Runtime.InteropServices.GCHandle.Alloc(bytes,System.Runtime.InteropServices.GCHandleType.Pinned);
  try{for(int i=0;i<valid;i++){if(i>0)result.Append(",");result.Append(JsonUtility.ToJson(System.Runtime.InteropServices.Marshal.PtrToStructure(IntPtr.Add(pinned.AddrOfPinnedObject(),i*stride),layout)));}}finally{pinned.Free();}
  result.Append("]");records=result.ToString();return Convert.ToBase64String(bytes);
 }
'''
CAPTURE=r'''
    var shadowType=typeof(HDRenderPipeline).Assembly.GetType("UnityEngine.Rendering.HighDefinition.HDShadowManager",true);
    var shadowManager=shadowType.GetProperty("instance",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.Public).GetValue(null);
    var shadowBuffer=(ComputeBuffer)shadowType.GetField("m_ShadowDataBuffer",flags).GetValue(shadowManager);
    var directionalShadowBuffer=(ComputeBuffer)shadowType.GetField("m_DirectionalShadowDataBuffer",flags).GetValue(shadowManager);
    int shadowCount=Convert.ToInt32(shadowType.GetField("m_ShadowRequestCount",flags).GetValue(shadowManager));
    int cascadeCount=Convert.ToInt32(shadowType.GetField("m_CascadeCount",flags).GetValue(shadowManager));
    var shadowLayout=typeof(HDRenderPipeline).Assembly.GetType("UnityEngine.Rendering.HighDefinition.HDShadowData",true);
    var directionalShadowLayout=typeof(HDRenderPipeline).Assembly.GetType("UnityEngine.Rendering.HighDefinition.HDDirectionalShadowData",true);
    string shadowJson,directionalShadowJson;
    var shadowBytes=ShadowBytes(shadowBuffer,shadowLayout,shadowCount,out shadowJson);
    var directionalShadowBytes=ShadowBytes(directionalShadowBuffer,directionalShadowLayout,1,out directionalShadowJson);
'''
def helper():
    text=original_helper().replace('SourceDirectionalGpuV1','SourceShadowGpuV1')
    declaration='public uint directional_light_gpu_count;';assert text.count(declaration)==1
    text=text.replace(declaration,'public int shadow_gpu_stride,shadow_gpu_buffer_count,shadow_gpu_live_count,shadow_gpu_cascade_count,directional_shadow_gpu_stride;public string shadow_gpu_json,shadow_gpu_base64,directional_shadow_gpu_json,directional_shadow_gpu_base64;'+declaration)
    marker='directionalJson.Append("]");';assert text.count(marker)==1
    text=text.replace(marker,marker+CAPTURE)
    marker='directional_light_gpu_base64=Convert.ToBase64String(lightBytes)};';assert text.count(marker)==1
    text=text.replace(marker,'directional_light_gpu_base64=Convert.ToBase64String(lightBytes),shadow_gpu_stride=shadowBuffer.stride,shadow_gpu_buffer_count=shadowBuffer.count,shadow_gpu_live_count=shadowCount,shadow_gpu_cascade_count=cascadeCount,directional_shadow_gpu_stride=directionalShadowBuffer.stride,shadow_gpu_json=shadowJson,shadow_gpu_base64=shadowBytes,directional_shadow_gpu_json=directionalShadowJson,directional_shadow_gpu_base64=directionalShadowBytes};')
    marker=' static string Hash(';assert text.count(marker)==1
    return text.replace(marker,BUFFER+marker)
def corrected_popen(args,*a,**kw):
    if isinstance(args,list) and args and str(args[0])==str(g['UNITY']):
        args=list(args);i=args.index('-executeMethod')+1
        assert args[i]=='OpenRPGSourceShadowReferenceV8.Export';args[i]='OpenRPGSourceShadowGpuV1.Export'
    return popen(args,*a,**kw)
g['subprocess'].Popen=corrected_popen
g.update(ROOT=g['W']/'firepit-source-shadow-gpu-v1',HELPER=g['PROJECT']/'Assets/Editor/SourceShadowGpuV1.cs',helper=helper,__file__=str(Path(__file__).resolve()))
if __name__=='__main__':
    if sys.argv[-1]=='prepare':g['prepare']()
    else:
        g['run']();state=g['read'](g['ROOT']/'RUN-RESULT.v1.json')
        paths=[g['ROOT']/f'SOURCE-STATE-{view}.json' for view in range(2)]
        if state.get('actual_exit_code')==0 and all(p.exists() for p in paths):
            rows=[g['read'](p) for p in paths];assert all(r['source_history_channel']=='CustomUserHistory0' and r['shadow_gpu_stride']>0 and r['directional_shadow_gpu_stride']>0 for r in rows)
            state.update(status='actual_source_shadow_gpu_buffers_and_live_cascades_captured',context_files={str(p):g['sha'](p) for p in paths})
        else:state.update(status='source_shadow_gpu_capture_refused')
        g['save']('RUN-RESULT.v1.json',state)
