"""Inventory all live source lights, not just the directional capture light."""
from pathlib import Path
import runpy,sys
BASE=Path(__file__).with_name('queue_firepit_source_shadow_gpu_v1.py')
loaded=runpy.run_path(str(BASE),run_name='source_all_lights_gpu')
g=loaded['g'];original_helper=g['helper'];popen=loaded['popen']
CAPTURE=r'''
    uint punctualCount=Convert.ToUInt32(globals.GetType().GetField("_PunctualLightCount",flags).GetValue(globals));
    var punctualBuffer=(GraphicsBuffer)lightLoopData.GetType().GetProperty("lightData",flags).GetValue(lightLoopData);
    var punctualLayout=typeof(HDRenderPipeline).Assembly.GetType("UnityEngine.Rendering.HighDefinition.LightData",true);
    int punctualStride=System.Runtime.InteropServices.Marshal.SizeOf(punctualLayout);
    if(punctualBuffer==null||punctualBuffer.stride!=punctualStride||punctualCount>punctualBuffer.count)throw new Exception("Punctual GPU buffer/count/layout invalid");
    var punctualReadback=AsyncGPUReadback.Request(punctualBuffer);punctualReadback.WaitForCompletion();if(punctualReadback.hasError)throw new Exception("Punctual GPU readback refused");
    var punctualBytes=punctualReadback.GetData<byte>().ToArray();if(punctualBytes.Length!=punctualBuffer.count*punctualStride)throw new Exception("Punctual GPU byte length invalid");
    var punctualJson=new System.Text.StringBuilder("[");var punctualPinned=System.Runtime.InteropServices.GCHandle.Alloc(punctualBytes,System.Runtime.InteropServices.GCHandleType.Pinned);
    try{for(int i=0;i<punctualCount;i++){if(i>0)punctualJson.Append(",");punctualJson.Append(JsonUtility.ToJson(System.Runtime.InteropServices.Marshal.PtrToStructure(IntPtr.Add(punctualPinned.AddrOfPinnedObject(),i*punctualStride),punctualLayout)));}}finally{punctualPinned.Free();}
    punctualJson.Append("]");
    var prefabLights=new System.Text.StringBuilder("[");int prefabLightIndex=0;
    foreach(var prefabLight in fixture.GetComponentsInChildren<Light>(true)){
     if(prefabLightIndex++>0)prefabLights.Append(",");
     prefabLights.Append("{\"light\":").Append(EditorJsonUtility.ToJson(prefabLight));
     var hd=prefabLight.GetComponent<HDAdditionalLightData>();prefabLights.Append(",\"hd_light\":").Append(hd==null?"null":EditorJsonUtility.ToJson(hd)).Append("}");
    }prefabLights.Append("]");
'''
def helper():
    text=original_helper().replace('SourceShadowGpuV1','SourceAllLightsGpuV1')
    marker='public uint directional_light_gpu_count;';assert text.count(marker)==1
    text=text.replace(marker,'public uint punctual_light_gpu_count;public int punctual_light_gpu_stride,punctual_light_gpu_buffer_count;public string punctual_light_gpu_json,punctual_light_gpu_base64,prefab_light_components_json;'+marker)
    marker='var directionalShadowBytes=ShadowBytes(directionalShadowBuffer,directionalShadowLayout,1,out directionalShadowJson);';assert text.count(marker)==1
    text=text.replace(marker,marker+CAPTURE)
    marker='directional_shadow_gpu_base64=directionalShadowBytes};';assert text.count(marker)==1
    return text.replace(marker,'directional_shadow_gpu_base64=directionalShadowBytes,punctual_light_gpu_count=punctualCount,punctual_light_gpu_stride=punctualStride,punctual_light_gpu_buffer_count=punctualBuffer.count,punctual_light_gpu_json=punctualJson.ToString(),punctual_light_gpu_base64=Convert.ToBase64String(punctualBytes),prefab_light_components_json=prefabLights.ToString()};')
def corrected_popen(args,*a,**kw):
    if isinstance(args,list) and args and str(args[0])==str(g['UNITY']):
        args=list(args);i=args.index('-executeMethod')+1
        assert args[i]=='OpenRPGSourceShadowReferenceV8.Export';args[i]='OpenRPGSourceAllLightsGpuV1.Export'
    return popen(args,*a,**kw)
g['subprocess'].Popen=corrected_popen
g.update(ROOT=g['W']/'firepit-source-all-lights-gpu-v1',HELPER=g['PROJECT']/'Assets/Editor/SourceAllLightsGpuV1.cs',helper=helper,__file__=str(Path(__file__).resolve()))
if __name__=='__main__':
    if sys.argv[-1]=='prepare':g['prepare']()
    else:
        g['run']();state=g['read'](g['ROOT']/'RUN-RESULT.v1.json')
        paths=[g['ROOT']/f'SOURCE-STATE-{view}.json' for view in range(2)]
        if state.get('actual_exit_code')==0 and all(p.exists() for p in paths):
            state.update(status='actual_source_directional_punctual_lights_and_shadow_buffers_captured',context_files={str(p):g['sha'](p) for p in paths})
        else:state.update(status='source_all_lights_gpu_capture_refused')
        g['save']('RUN-RESULT.v1.json',state)
