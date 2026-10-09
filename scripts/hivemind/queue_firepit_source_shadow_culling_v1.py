"""Capture source camera culling inputs and effective Volume split parameters."""
from pathlib import Path
import runpy,sys
BASE=Path(__file__).with_name('queue_firepit_source_all_lights_gpu_v1.py')
loaded=runpy.run_path(str(BASE),run_name='source_shadow_culling')
g=loaded['g'];original_helper=loaded['helper'];popen=loaded['popen']
def helper():
    text=original_helper().replace('SourceAllLightsGpuV1','SourceShadowCullingV1')
    marker=' static string Hash(';assert text.count(marker)==1
    text=text.replace(marker,' [Serializable] class CullingContextState {public float camera_near,camera_far,camera_api_shadow_distance,hdrp_shadow_distance_from_sdk_rule,shadow_near_plane_offset;public float[] cascade_split_ratios,cascade_borders;public int cascade_count;public Vector3 origin;public Matrix4x4 culling_matrix;public bool actual_culling_result_readback=false;}\n'+marker)
    marker='File.WriteAllText(environmentPath,JsonUtility.ToJson(environment,true));';assert text.count(marker)==1
    extra=r'''
    if(!camera.TryGetCullingParameters(out var culling))throw new Exception("Source camera culling parameters unavailable");
    var shadowContext=stack.GetComponent<HDShadowSettings>();
    var cullingState=new CullingContextState{camera_near=camera.nearClipPlane,camera_far=camera.farClipPlane,
      camera_api_shadow_distance=culling.shadowDistance,hdrp_shadow_distance_from_sdk_rule=Mathf.Min(shadowContext.maxShadowDistance.value,culling.shadowDistance),
      shadow_near_plane_offset=QualitySettings.shadowNearPlaneOffset,cascade_count=shadowContext.cascadeShadowSplitCount.value,
      cascade_split_ratios=new[]{shadowContext.cascadeShadowSplit0.value,shadowContext.cascadeShadowSplit1.value,shadowContext.cascadeShadowSplit2.value},
      cascade_borders=new[]{shadowContext.cascadeShadowBorder0.value,shadowContext.cascadeShadowBorder1.value,shadowContext.cascadeShadowBorder2.value,shadowContext.cascadeShadowBorder3.value},
      origin=culling.origin,culling_matrix=culling.cullingMatrix};
    var cullingPath=Path.Combine(Path.GetDirectoryName(request.output),"SOURCE-CULLING-"+view+".json");
    if(File.Exists(cullingPath))throw new Exception("Preserve source culling evidence");File.WriteAllText(cullingPath,JsonUtility.ToJson(cullingState,true));
'''
    return text.replace(marker,marker+extra)
def corrected_popen(args,*a,**kw):
    if isinstance(args,list) and args and str(args[0])==str(g['UNITY']):
        args=list(args);i=args.index('-executeMethod')+1
        assert args[i]=='OpenRPGSourceShadowReferenceV8.Export';args[i]='OpenRPGSourceShadowCullingV1.Export'
    return popen(args,*a,**kw)
g['subprocess'].Popen=corrected_popen
g.update(ROOT=g['W']/'firepit-source-shadow-culling-v1',HELPER=g['PROJECT']/'Assets/Editor/SourceShadowCullingV1.cs',helper=helper,__file__=str(Path(__file__).resolve()))
if __name__=='__main__':
    if sys.argv[-1]=='prepare':g['prepare']()
    else:
        g['run']();state=g['read'](g['ROOT']/'RUN-RESULT.v1.json')
        paths=[g['ROOT']/f'SOURCE-CULLING-{view}.json' for view in range(2)]
        if state.get('actual_exit_code')==0 and all(p.exists() for p in paths):state.update(status='source_camera_culling_inputs_and_effective_volume_splits_captured',culling_files={str(p):g['sha'](p) for p in paths})
        else:state.update(status='source_camera_culling_capture_refused')
        g['save']('RUN-RESULT.v1.json',state)
