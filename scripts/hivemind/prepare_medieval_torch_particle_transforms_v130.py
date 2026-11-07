from pathlib import Path
from datetime import datetime,timezone
import copy,hashlib,json,math,struct
W=Path('F:/OpenRPG_ImportWork');O=W/'medieval-torch-particle-transform-contract-v130'
def read(p):return json.loads(p.read_text('utf-8-sig'))
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def f32(v):return struct.unpack('<f',struct.pack('<f',v))[0]
def trs(d):
 q=[f32(d['m_LocalRotation'][k]) for k in 'xyzw'];x,y,z,v=q;assert abs(sum(a*a for a in q)-1)<1e-5,'Source quaternion is not normalized'
 scale=[f32(d['m_LocalScale'][k]) for k in 'xyz'];position=[f32(d['m_LocalPosition'][k]) for k in 'xyz'];assert all(math.isfinite(a) for a in q+scale+position) and all(a!=0 for a in scale)
 r=[[1-2*(y*y+z*z),2*(x*y-z*v),2*(x*z+y*v)],[2*(x*y+z*v),1-2*(x*x+z*z),2*(y*z-x*v)],[2*(x*z-y*v),2*(y*z+x*v),1-2*(x*x+y*y)]]
 return [[r[i][j]*scale[j] for j in range(3)]+[position[i]] for i in range(3)]+[[0,0,0,1]]
def multiply(a,b):return [[sum(a[i][k]*b[k][j] for k in range(4)) for j in range(4)] for i in range(4)]
assert not O.exists();pp=W/'medieval-torch-effective-particles-v122/RESULT.v1.json';proof=read(pp);ip=Path(next(iter(proof['output_pins'])));assert sha(ip)==proof['output_pins'][str(ip)];ir=read(ip)
parents={d['file_id']:d for d in ir['parent_documents'] if d['type']=='Transform'};roots=[d for d in parents.values() if not d['stripped'] and d['data']['m_Father']['fileID']=='0'];assert len(roots)==1
identity=[[float(i==j) for j in range(4)] for i in range(4)]
def world(fid,seen=None):
 seen=set() if seen is None else set(seen);assert fid not in seen,'Transform cycle';seen.add(fid);d=parents[fid];assert not d['stripped'],'Stripped transform requires explicit source scope';local=trs(d['data']);father=d['data']['m_Father']['fileID'];return multiply(world(father,seen),local) if father!='0' else local
rows=[]
for case in ir['cases']:
 instance=case['parent_instance'];parent_id=instance['data']['m_Modification']['m_TransformParent']['fileID'];parent=parents[parent_id];assert parent_id==roots[0]['file_id'],'Nested parent hierarchy requires explicit path traversal'
 child_roots=[d for d in case['effective_documents'] if d['type']=='Transform' and d['data']['m_Father']['fileID']=='0'];assert len(child_roots)==1;child=child_roots[0]
 bridges=[d for d in parents.values() if d['stripped'] and d['data']['m_PrefabInstance']['fileID']==instance['file_id'] and d['data']['m_CorrespondingSourceObject']['guid']==case['source_guid'] and d['data']['m_CorrespondingSourceObject']['fileID']==child['file_id']];assert len(bridges)==1
 index=next(i for i,v in enumerate(parent['data']['m_Children']) if v['fileID']==bridges[0]['file_id']);assert child['data']['m_RootOrder']==index,'Serialized child order differs'
 unity=multiply(world(parent_id),trs(child['data']));conversion=[[1,0,0,0],[0,1,0,0],[0,0,-1,0],[0,0,0,1]];forge=multiply(multiply(conversion,unity),conversion)
 rows.append(dict(source_guid=case['source_guid'],instance_file_id=instance['file_id'],stripped_transform_file_id=bridges[0]['file_id'],inherited_transform_file_id=child['file_id'],source_parent_transform_file_id=parent_id,serialized_node_path='root/'+str(index),effective_source_local_transform=child['data'],source_world_matrix_row_major=[f32(v) for row in unity for v in row],forge_candidate_world_matrix_row_major=[f32(v) for row in forge for v in row],basis_conversion='C * M * C, C = diag(1,1,-1,1)',native_matrix_verified=False,engine_integration_verified=False))
controls=[]
root_id=roots[0]['file_id'];original=copy.deepcopy(parents[root_id]);parents[root_id]['data']['m_Father']['fileID']=root_id
try:world(root_id)
except AssertionError:controls.append('transform_cycle_refused')
else:raise AssertionError('Cycle admitted')
parents[root_id]=original;bad=copy.deepcopy(original['data']);bad['m_LocalScale']['x']=0
try:trs(bad)
except AssertionError:controls.append('singular_scale_refused')
else:raise AssertionError('Singular scale admitted')
pins={str(p):sha(p) for p in [pp,ip,Path(__file__).resolve()]};assert sha(ip)==proof['output_pins'][str(ip)]
O.mkdir();r=dict(status='two_particle_instance_transforms_and_serialized_child_paths_derived',finished_utc=datetime.now(timezone.utc).isoformat(),lot='L06.MEDIEVAL.TORCH.001',source_group_id=ir['source_group_id'],rows=rows,controls=controls,input_pins=pins,source_graph_only_not_native_measurement=True,native_matrix_verified=False,full_import=False);(O/'RESULT.v1.json').write_text(json.dumps(r,indent=2,allow_nan=False),'utf-8');print(json.dumps(dict(instances=len(rows),node_paths=[r['serialized_node_path'] for r in rows],source_world_positions=[[r['source_world_matrix_row_major'][i] for i in [3,7,11]] for r in rows],proof=str(O/'RESULT.v1.json'))))
