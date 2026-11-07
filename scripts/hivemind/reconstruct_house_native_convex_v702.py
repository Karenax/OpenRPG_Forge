"""Construct candidate halfspace hulls from native observations, keeping independent rays for verification."""
from pathlib import Path
import datetime,hashlib,itertools,json
import numpy as np
W=Path('F:/OpenRPG_ImportWork');O=W/'house-native-convex-candidate-v702'
assert not O.exists()
def read(p):return json.loads(Path(p).read_text('utf-8-sig'))
def sha(p):
    with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
run_path=W/'house-native-component-state-v701/RUN-RESULT.v1.json'
native_path=run_path.with_name('NATIVE-RESULT.v1.json')
run,native=map(read,[run_path,native_path])
assert run['unity_exit_code']==0 and not run['source_changes'] and not run['private_prefab_changes']
assert sha(native_path)==run['native_sha256']
pins={str(p):sha(p) for p in [Path(__file__),run_path,native_path]}
for p,h in run['input_pins'].items():assert sha(p)==h,p;pins[p]=h
O.mkdir();rows=[]
for case in native['boxes']:
    if not case['convex']:continue
    assert case['world_position']==[0,0,0] and case['world_rotation']==[0,0,0,1] and case['lossy_scale']==[1,1,1]
    training=[r for r in case['native_face_probes'] if r['family']=='source_triangle_face' or r['sample']%2==0]
    holdout=[r for r in case['native_face_probes'] if r['family']=='fixed_fibonacci_direction' and r['sample']%2==1]
    assert len(holdout)==512 and all(r['hit'] for r in training+holdout)
    groups={}
    for sample in training:groups.setdefault(tuple(sample['normal']),[]).append(sample)
    # Coverage is an observed check only, not a claim of globally complete geometry.
    observed={tuple(r['normal']) for r in case['native_face_probes']}
    assert set(groups)==observed,'A native plane was discovered only by the holdout set'
    normals=np.array(list(groups),dtype=np.float64)
    distances=[];spreads=[]
    for normal,samples in groups.items():
        offsets=np.array([np.dot(normal,s['point']) for s in samples])
        distances.append(float(np.median(offsets)));spreads.append(float(np.ptp(offsets)))
    distances=np.array(distances);spreads=np.array(spreads)
    assert np.isfinite(normals).all() and np.isfinite(distances).all()
    candidates=[]
    allowance=np.maximum(spreads,1e-7)
    for ids in itertools.combinations(range(len(normals)),3):
        matrix=normals[list(ids)]
        if abs(np.linalg.det(matrix))<1e-10:continue
        point=np.linalg.solve(matrix,distances[list(ids)])
        if np.all(normals@point<=distances+allowance):
            if not any(np.linalg.norm(point-v)<=1e-7 for v in candidates):candidates.append(point)
    assert len(candidates)>=4
    vertices=np.array(candidates,dtype=np.float64)
    assert np.linalg.matrix_rank(vertices-vertices.mean(axis=0))==3
    target=vertices.copy();target[:,2]*=-1
    target=target.astype('<f4');assert np.isfinite(target).all()
    folder=O/case['source_guid'];folder.mkdir()
    vertex_path=folder/'native-plane-candidate.positions-f32.bin';vertex_path.write_bytes(target.tobytes())
    row=dict(source_guid=case['source_guid'],source_file_id=case['source_file_id'],
             source_mesh_guid=case['mesh_guid'],source_mesh_file_id=case['mesh_file_id'],
             source_cooking_options=case['cooking_options'],native_observed_planes=len(groups),
             training_rays=len(training),holdout_rays=holdout,vertices=len(vertices),
             source_plane_normals=normals.tolist(),source_plane_distances=distances.tolist(),
             measured_plane_offset_spread=spreads.tolist(),maximum_offset_spread=float(spreads.max()),
             candidate_vertex_admission_allowance=allowance.tolist(),
             target_vertices=str(vertex_path),target_vertices_sha256=sha(vertex_path),
             original_geometry_replaced=False,rapier_query_parity=False,global_shape_equivalence_proven=False)
    rows.append(row)
assert len(rows)==17 and all(sha(p)==h for p,h in pins.items())
result=dict(schema='hivemind.house-native-convex-candidate.v702',utc=datetime.datetime.now(datetime.timezone.utc).isoformat(),
            status='native_plane_hull_candidates_prepared_not_qualified',rows=rows,input_pins=pins,
            heldout_rays=8704,source_geometry_unchanged=True,full_import=False)
(O/'RESULT.v1.json').write_text(json.dumps(result,indent=2),'utf-8')
print(json.dumps(dict(convex_candidates=17,heldout_rays=8704,planes=[r['native_observed_planes'] for r in rows],vertices=[r['vertices'] for r in rows])))
