"""Independent native force getter and per-channel color comparison."""
from pathlib import Path
import hashlib,json,struct,datetime
import numpy as np
W=Path('F:/OpenRPG_ImportWork');I=W/'firepit-particle-render-time-getters-v241';O=W/'firepit-particle-native-render-getters-v242'
def read(p):return json.loads(p.read_text('utf-8-sig'))
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
assert not O.exists();run=read(I/'RUN-RESULT.v1.json');assert run['unity_exit_code']==0 and run['native_geometry_verified']
assert sha(I/'INDEPENDENT-REOPEN.v1.json')==run['independent_receipt_sha256']
assert sha(I/'GEOMETRY.v1.json')==read(I/'INDEPENDENT-REOPEN.v1.json')['capture_sha256']
results=[]
for case in read(I/'GEOMETRY.v1.json')['cases']:
 source=json.loads(case['native_particle_json'])['ParticleSystem'];force=source['ForceModule'];centermax=0.;observations=0
 errors={k:np.zeros(4) for k in ['current','pending','next_fixed','current_linear','pending_linear','next_fixed_linear']}
 for row in case['rows']:
  assert row['native_force_enabled']==force['enabled'] and row['native_force_local']==(not force['inWorldSpace'])
  assert [row['native_constant_force'][k] for k in 'xyz']==[force[k]['scalar'] for k in 'xyz']
  acceleration=np.array([row['native_constant_force'][k] for k in 'xyz']) if row['native_force_enabled'] else np.zeros(3)
  f={x['path']:x for x in row['playback_before_bake']};dt=struct.unpack('>f',bytes.fromhex(f['playback.m_AccumulatedDt']['bits']))[0]
  pos=np.array([[v['position'][k] for k in 'xyz'] for v in row['particles']]);vel=np.array([[v['velocity'][k] for k in 'xyz'] for v in row['particles']])
  vals={'current':np.array([[p['current_color'][k] for k in 'rgba'] for p in row['particles']]),'pending':np.array([[p[k] for k in 'rgba'] for p in row['pending_colors']]),'next_fixed':np.array([[p[k] for k in 'rgba'] for p in row['next_fixed_colors']])}
  for k,a in list(vals.items()):
   linear=a.copy();linear[:,:3]=np.where(a[:,:3]<=.04045,a[:,:3]/12.92,((a[:,:3]+.055)/1.055)**2.4);vals[k+'_linear']=linear
  for g in row['geometry']:
   center=np.array([[v[k] for k in 'xyz'] for v in g['positions']]).reshape(-1,4,3).mean(1)
   centermax=max(centermax,float(np.abs(center-(pos+(vel+acceleration*row['fixed_delta_time'])*dt)).max()))
   baked=np.array([[v[k] for k in 'rgba'] for v in g['colors']]).reshape(-1,4,4);assert np.all(baked==baked[:,0:1,:]);b=baked[:,0,:]/255
   for k,a in vals.items():errors[k]=np.maximum(errors[k],np.abs(b-a).max(0))
   observations+=1
 results.append(dict(node_path=case['node_path'],observations=observations,native_force_getters_equal_source=True,native_next_step_center_maximum_error=centermax,color_maximum_channel_errors={k:v.tolist() for k,v in errors.items()},color_reconstruction_qualified=False))
O.mkdir();result=dict(status='native_force_getters_verified_and_color_hypotheses_measured',input_pins={str(p):sha(p) for p in [Path(__file__),I/'RUN-RESULT.v1.json',I/'INDEPENDENT-REOPEN.v1.json',I/'GEOMETRY.v1.json']},results=results,fitted_correction=False,autonomous_simulation_qualified=False,full_import=False,finished_utc=datetime.datetime.now(datetime.timezone.utc).isoformat())
(O/'RESULT.v1.json').write_text(json.dumps(result,indent=2),'utf-8');print(json.dumps(results,indent=2))
