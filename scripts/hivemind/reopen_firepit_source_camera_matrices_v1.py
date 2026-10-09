"""Reopen camera evidence without accepting renderer parity."""
from pathlib import Path
from datetime import datetime,timezone
import json,hashlib,math
W=Path('F:/OpenRPG_ImportWork');CAP=W/'firepit-source-camera-matrices-v1';ROOT=W/'firepit-source-camera-matrices-reopen-v1'
def read(p):return json.loads(p.read_text('utf-8-sig'))
def sha(p):
    with p.open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def main():
    assert not ROOT.exists()
    run=read(CAP/'RUN-RESULT.v1.json');prep=read(CAP/'PREPARED.v1.json');native=read(CAP/'NATIVE-RESULT.v1.json')
    assert run['actual_exit_code']==0 and run['source_inputs_unchanged'] and not run['private_changed']
    for p,h in prep['input_pins'].items():assert sha(Path(p))==h,p
    assert len(prep['dependency_paths'])==24
    case=read(W/'firepit-surface-native-case-v1/CASE.v1.json')
    rows=[];files=[CAP/'RUN-RESULT.v1.json',CAP/'PREPARED.v1.json',CAP/'NATIVE-RESULT.v1.json']
    for view in range(2):
        p=CAP/f'SOURCE-STATE-{view}.json';s=read(p);assert s['source_history_channel']=='CustomUserHistory0'
        camera=read_json=s['camera_matrices_json'];camera=json.loads(read_json);history=json.loads(s['history_view_constants_json'])
        assert camera['aspect']==1 and camera['position']==case['source_camera']['position']
        assert camera['projection']==camera['non_jittered_projection']
        assert history['projMatrix']==history['nonJitteredProjMatrix']
        for m in [camera['world_to_camera'],camera['projection'],camera['local_to_world'],history['viewMatrix'],history['projMatrix']]:
            assert all(math.isfinite(v) for v in m.values()) and len(m)==16
        assert abs(camera['projection']['e00']-1/math.tan(math.radians(15)))<1e-6
        assert history['projMatrix']['e11']==-camera['projection']['e11']
        assert all(history['viewMatrix'][f'e{i}3']==0 for i in range(3))
        for a,b in [(CAP/f'images/{view}.png',W/f'firepit-shadow-source-reference-v8/images/{view}.png'),(CAP/f'linear-{view}.rgba16f',W/f'firepit-shadow-source-reference-v8/linear-{view}.rgba16f')]:
            assert sha(a)==sha(b);files.extend([a,b])
        rows.append(dict(view=view,camera=camera,request_history=history));files.append(p)
    assert rows[0]['camera']==rows[1]['camera']
    result=dict(status='source_camera_matrices_no_jitter_and_reference_images_verified',finished_utc=datetime.now(timezone.utc).isoformat(),source_dependencies_verified=24,views_verified=2,reference_images_byte_identical=True,source_projection_fov_degrees=30,source_history_camera_relative=True,source_render_target_projection_y_inverted=True,target_camera_gpu_matrices_verified=False,render_fidelity_qualified=False,full_import=False,editor_visual_verified=False,rows=rows,input_pins={str(p):sha(p) for p in [Path(__file__).resolve(),*files]},next_action='Read native camera/mesh transforms and project source positions; resolve silhouette discrepancy without pixel alignment or fitted data.')
    ROOT.mkdir();(ROOT/'RESULT.v1.json').write_text(json.dumps(result,indent=2),'utf-8');print(json.dumps({k:v for k,v in result.items() if k not in ['input_pins','rows']}))
if __name__=='__main__':main()
