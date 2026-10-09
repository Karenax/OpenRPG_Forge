"""Reconstruct source directional projections from retained GPU packets."""
from pathlib import Path
import datetime,hashlib,json,math
W=Path('F:/OpenRPG_ImportWork');ROOT=W/'firepit-source-shadow-projection-case-v1'
HDRP=W/'unity-dungeon-original-shadergraph-hdrp-startup-v4/project/Library/PackageCache/com.unity.render-pipelines.high-definition@fc55a8abadc2/Runtime'
def read(p):return json.loads(p.read_text('utf-8-sig'))
def sha(p):
    with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def mul(a,b):return [[sum(a[r][k]*b[k][c] for k in range(4)) for c in range(4)] for r in range(4)]
def inverse(a):
    v=[[*row,*[float(i==j) for j in range(4)]] for i,row in enumerate(a)]
    for i in range(4):
        pivot=max(range(i,4),key=lambda j:abs(v[j][i]));assert abs(v[pivot][i])>1e-12
        v[i],v[pivot]=v[pivot],v[i];scale=v[i][i];v[i]=[x/scale for x in v[i]]
        for j in range(4):
            if j!=i:
                scale=v[j][i];v[j]=[a-scale*b for a,b in zip(v[j],v[i])]
    return [row[4:] for row in v]
def cols(a):return [a[r][c] for c in range(4) for r in range(4)]
def fromcols(a):return [[a[c*4+r] for c in range(4)] for r in range(4)]
def main():
    assert not ROOT.exists()
    old=W/'firepit-source-shadow-context-case-v1';case=read(old/'CASE.v1.json');protocol=read(old/'RESULT.v1.json')
    state_path=W/'firepit-source-all-lights-gpu-v1/SOURCE-STATE-0.json';state=read(state_path)
    source_proof_path=W/'firepit-source-all-lights-gpu-reopen-v2/RESULT.v1.json';proof=read(source_proof_path)
    for p,h in proof['input_pins'].items():assert sha(p)==h,p
    camera_path=W/'firepit-source-shadow-context-native-v1/CAMERA-STATE-0.json';camera=read(camera_path)
    transforms=[row['source_world_from_target_world'] for row in camera['source_uniform_rows']];assert all(t==transforms[0] for t in transforms)
    source_world_from_target=fromcols(transforms[0]);translation=[[float(r==c) for c in range(4)] for r in range(4)]
    source_eye=case['source_shadow_context_replay']['source_culling_inputs']['origin']
    for i,key in enumerate(['x','y','z']):translation[i][3]=-source_eye[key]
    rws_from_target=mul(translation,source_world_from_target)
    flip=[[float(r==c)*(-1 if r==1 else 1) for c in range(4)] for r in range(4)]
    rows=json.loads(state['shadow_gpu_json']);prepared=[];maximum_inverse_error=0.;maximum_texel_error=0.
    for index,sd in enumerate(rows[:4]):
        view=[[sd[f'rot{r}'][k] for k in ['x','y','z']]+[sd['pos'][['x','y','z'][r]]] for r in range(3)]+[[0.,0.,0.,1.]]
        p=sd['proj'];projection=[[p['x'],0,0,0],[0,p['y'],0,0],[0,0,p['z'],p['w']],[0,0,0,1]]
        clip=mul(projection,view)
        # SDK stores transpose(inverse(viewProjection)) in shadowToWorld.
        stored=sd['shadowToWorld'];inv_stored=[[stored[f'e{c}{r}'] for c in range(4)] for r in range(4)]
        recovered=inverse(inv_stored)
        error=max(abs(a-b) for a,b in zip(cols(clip),cols(recovered)));maximum_inverse_error=max(maximum_inverse_error,error)
        assert error<1e-5,'Source compressed matrix differs from retained inverse projection'
        diagonal=2/p['x']/sd['shadowMapSize']['x']*math.sqrt(2)
        texel_error=abs(diagonal-sd['worldTexelSize']);maximum_texel_error=max(maximum_texel_error,texel_error);assert texel_error<1e-8
        target_view=mul(view,rws_from_target);target_projection=mul(flip,projection);target_clip=mul(target_projection,target_view)
        reconstructed=mul(target_projection,inverse(inverse(target_view)))
        assert max(abs(a-b) for a,b in zip(cols(target_clip),cols(reconstructed)))<1e-8
        prepared.append(dict(index=index,world_from_cascade=cols(inverse(target_view)),clip_from_cascade=cols(target_projection),clip_from_world=cols(target_clip),
            texel_size=sd['worldTexelSize']/math.sqrt(2),source_diagonal_texel_size=sd['worldTexelSize'],source_shadow_record=sd))
    case['source_shadow_projection_replay']={'schema_version':1,'cascades':prepared,'target_ndc_y_flip':True,'source_camera_relative':True,'source_filter_matched':False,'source_bias_matched':False}
    files=[Path(__file__).resolve(),state_path,source_proof_path,camera_path,old/'CASE.v1.json',old/'RESULT.v1.json',HDRP/'Lighting/Shadow/HDShadowAlgorithms.hlsl',HDRP/'Lighting/Shadow/HDShadowUtils.cs',HDRP/'Lighting/Shadow/HDShadowManager.cs',HDRP/'Lighting/Light/HDGpuLightsBuilder.LightLoop.cs']
    ROOT.mkdir();(ROOT/'CASE.v1.json').write_text(json.dumps(case,indent=2),'utf-8')
    protocol.update(status='source_compressed_shadow_projection_and_texel_conventions_verified_replay_prepared',finished_utc=datetime.datetime.now(datetime.timezone.utc).isoformat(),
        source_shadow_projection_replay=case['source_shadow_projection_replay'],maximum_inverse_projection_error=maximum_inverse_error,maximum_diagonal_texel_reconstruction_error=maximum_texel_error,
        input_pins={**protocol['input_pins'],**{str(p):sha(p) for p in files}},full_import=False)
    (ROOT/'RESULT.v1.json').write_text(json.dumps(protocol,indent=2),'utf-8')
    print(json.dumps({'root':str(ROOT),'cascades':4,'maximum_inverse_projection_error':maximum_inverse_error,'maximum_diagonal_texel_error':maximum_texel_error,'full_import':False}))
if __name__=='__main__':main()
