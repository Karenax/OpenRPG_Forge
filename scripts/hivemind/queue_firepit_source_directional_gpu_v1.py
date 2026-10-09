"""Read actual source GPU directional records and live light count after each request."""
from pathlib import Path
import runpy,sys
BASE=Path(__file__).with_name('queue_firepit_shadow_reference_v11.py')
loaded=runpy.run_path(str(BASE),run_name='source_directional_gpu_probe')
g=loaded['g'];original_helper=g['helper'];popen=loaded['popen']
def helper():
    text=original_helper().replace('SourceShadowReferenceV11','SourceDirectionalGpuV1')
    declaration='public string source_history_channel;';assert text.count(declaration)==1
    text=text.replace(declaration,declaration+'public uint directional_light_gpu_count;public int directional_light_gpu_stride,directional_light_buffer_count;public string directional_light_gpu_json,directional_light_gpu_base64;')
    marker='actualBuffer.GetData(packedProbe);';assert text.count(marker)==1
    capture=r'''
    var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Public;
    var globals=typeof(HDRenderPipeline).GetField("m_ShaderVariablesGlobalCB",flags).GetValue(currentPipeline);
    uint directionalCount=Convert.ToUInt32(globals.GetType().GetField("_DirectionalLightCount",flags).GetValue(globals));
    var lightLoopData=typeof(HDRenderPipeline).GetField("m_LightLoopLightData",flags).GetValue(currentPipeline);
    var lightBuffer=(GraphicsBuffer)lightLoopData.GetType().GetProperty("directionalLightData",flags).GetValue(lightLoopData);
    var lightType=typeof(HDRenderPipeline).Assembly.GetType("UnityEngine.Rendering.HighDefinition.DirectionalLightData",true);
    int lightStride=System.Runtime.InteropServices.Marshal.SizeOf(lightType);
    if(lightBuffer==null||lightBuffer.stride!=lightStride||directionalCount==0||directionalCount>lightBuffer.count)throw new Exception("Directional GPU buffer/count/layout invalid");
    var lightRequest=AsyncGPUReadback.Request(lightBuffer);lightRequest.WaitForCompletion();if(lightRequest.hasError)throw new Exception("Directional GPU readback refused");
    var lightBytes=lightRequest.GetData<byte>().ToArray();if(lightBytes.Length!=lightBuffer.count*lightStride)throw new Exception("Directional GPU byte length invalid");
    var directionalJson=new System.Text.StringBuilder("[");
    var pinned=System.Runtime.InteropServices.GCHandle.Alloc(lightBytes,System.Runtime.InteropServices.GCHandleType.Pinned);
    try{for(int index=0;index<directionalCount;index++){if(index>0)directionalJson.Append(",");var value=System.Runtime.InteropServices.Marshal.PtrToStructure(IntPtr.Add(pinned.AddrOfPinnedObject(),index*lightStride),lightType);directionalJson.Append(JsonUtility.ToJson(value));}}finally{pinned.Free();}
    directionalJson.Append("]");
'''
    text=text.replace(marker,marker+capture)
    marker='source_history_channel="CustomUserHistory0"};';assert text.count(marker)==1
    text=text.replace(marker,'source_history_channel="CustomUserHistory0",directional_light_gpu_count=directionalCount,directional_light_gpu_stride=lightStride,directional_light_buffer_count=lightBuffer.count,directional_light_gpu_json=directionalJson.ToString(),directional_light_gpu_base64=Convert.ToBase64String(lightBytes)};')
    return text
def corrected_popen(args,*a,**kw):
    if isinstance(args,list) and args and str(args[0])==str(g['UNITY']):
        args=list(args);i=args.index('-executeMethod')+1;assert args[i]=='OpenRPGSourceShadowReferenceV8.Export';args[i]='OpenRPGSourceDirectionalGpuV1.Export'
    return popen(args,*a,**kw)
g['subprocess'].Popen=corrected_popen
g.update(ROOT=g['W']/'firepit-source-directional-gpu-v1',HELPER=g['PROJECT']/'Assets/Editor/SourceDirectionalGpuV1.cs',helper=helper,__file__=str(Path(__file__).resolve()))
if __name__=='__main__':
    if sys.argv[-1]=='prepare':g['prepare']()
    else:
        g['run']();state=g['read'](g['ROOT']/'RUN-RESULT.v1.json');paths=[g['ROOT']/f'SOURCE-STATE-{view}.json' for view in range(2)]
        if state.get('actual_exit_code')==0 and all(p.exists() for p in paths):
            rows=[g['read'](p) for p in paths];assert all(r['source_history_channel']=='CustomUserHistory0' and r['directional_light_gpu_count']>0 and r['directional_light_gpu_stride']>0 for r in rows)
            state.update(status='actual_source_directional_gpu_buffer_and_live_cbuffer_count_captured',context_files={str(p):g['sha'](p) for p in paths})
        else:state.update(status='source_directional_gpu_capture_refused')
        g['save']('RUN-RESULT.v1.json',state)
