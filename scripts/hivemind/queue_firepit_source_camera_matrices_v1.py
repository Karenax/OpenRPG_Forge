"""Measure source camera and request-history matrices; preserve reference images."""
from pathlib import Path
import runpy,sys
BASE=Path(__file__).with_name('queue_firepit_shadow_reference_v11.py')
loaded=runpy.run_path(str(BASE),run_name='source_camera_matrices_probe')
g=loaded['g'];original_helper=g['helper'];popen=loaded['popen']
def helper():
    text=original_helper().replace('SourceShadowReferenceV11','SourceCameraMatricesV1')
    marker='public string source_history_channel;';assert text.count(marker)==1
    text=text.replace(marker,marker+'public string camera_matrices_json,history_view_constants_json;')
    marker='source_history_channel="CustomUserHistory0"};';assert text.count(marker)==1
    replacement='source_history_channel="CustomUserHistory0",camera_matrices_json=JsonUtility.ToJson(new SourceCameraMatrices{world_to_camera=camera.worldToCameraMatrix,projection=camera.projectionMatrix,non_jittered_projection=camera.nonJitteredProjectionMatrix,local_to_world=camera.transform.localToWorldMatrix,position=camera.transform.position,rotation=camera.transform.rotation,aspect=camera.aspect}),history_view_constants_json=JsonUtility.ToJson(hc.mainViewConstants)};'
    text=text.replace(marker,replacement)
    marker='[Serializable] public class EnvironmentState';assert text.count(marker)==1
    text=text.replace(marker,'[Serializable] public class SourceCameraMatrices {public Matrix4x4 world_to_camera,projection,non_jittered_projection,local_to_world;public Vector3 position;public Quaternion rotation;public float aspect;}\n '+marker)
    return text
def corrected_popen(args,*a,**kw):
    if isinstance(args,list) and args and str(args[0])==str(g['UNITY']):
        args=list(args);i=args.index('-executeMethod')+1
        assert args[i]=='OpenRPGSourceShadowReferenceV8.Export'
        args[i]='OpenRPGSourceCameraMatricesV1.Export'
    return popen(args,*a,**kw)
g['subprocess'].Popen=corrected_popen
g.update(ROOT=g['W']/'firepit-source-camera-matrices-v1',HELPER=g['PROJECT']/'Assets/Editor/SourceCameraMatricesV1.cs',helper=helper,__file__=str(Path(__file__).resolve()))
if __name__=='__main__':
    if sys.argv[-1]=='prepare':g['prepare']()
    else:
        g['run']();state=g['read'](g['ROOT']/'RUN-RESULT.v1.json')
        paths=[g['ROOT']/f'SOURCE-STATE-{v}.json' for v in range(2)]
        if state.get('actual_exit_code')==0 and all(p.exists() for p in paths):
            rows=[g['read'](p) for p in paths]
            assert all(r['source_history_channel']=='CustomUserHistory0' and r['camera_matrices_json'] and r['history_view_constants_json'] for r in rows)
            state.update(status='source_camera_and_request_history_matrices_captured',context_files={str(p):g['sha'](p) for p in paths})
        else:state.update(status='source_camera_matrices_capture_refused')
        g['save']('RUN-RESULT.v1.json',state)
