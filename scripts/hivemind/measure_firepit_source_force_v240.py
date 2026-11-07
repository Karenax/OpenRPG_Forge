"""Measure native pending-time extrapolation; no fitted coefficients or promotion."""
from pathlib import Path
import datetime,hashlib,json,struct
import numpy as np
W=Path('F:/OpenRPG_ImportWork');I=W/'firepit-particle-playback-state-native-v238';O=W/'firepit-particle-source-force-v240'
def read(p):return json.loads(p.read_text('utf-8-sig'))
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
assert not O.exists()
run=read(I/'RUN-RESULT.v1.json');assert run['unity_exit_code']==0 and run['native_geometry_verified']
assert sha(I/'INDEPENDENT-REOPEN.v1.json')==run['independent_receipt_sha256']
assert sha(I/'GEOMETRY.v1.json')==read(I/'INDEPENDENT-REOPEN.v1.json')['capture_sha256']
d=read(I/'GEOMETRY.v1.json');results=[];fields=0
for case in d['cases']:
 rows=[];rawmax=predmax=0.;n=0
 source=json.loads(case['native_particle_json'])['ParticleSystem'];force=source['ForceModule']
 assert not force['inWorldSpace'] and not force['randomizePerFrame']
 assert all(force[k]['minMaxState']==0 for k in 'xyz')
 assert source['InitialModule']['gravityModifier']['scalar']==0
 assert all(not source[k]['enabled'] for k in ['VelocityModule','ExternalForcesModule','ClampVelocityModule','NoiseModule','InheritVelocityModule'])
 acceleration=np.array([force[k]['scalar'] for k in 'xyz']) if force['enabled'] else np.zeros(3)
 for row in case['rows']:
  assert row['playback_before_bake']==row['playback_after_bake']
  f={x['path']:x for x in row['playback_before_bake']};fields+=len(f)
  pending=struct.unpack('>f',bytes.fromhex(f['playback.m_AccumulatedDt']['bits']))[0]
  assert np.isfinite(pending) and pending>=0
  pos=np.array([[v['position'][k] for k in 'xyz'] for v in row['particles']]);vel=np.array([[v['velocity'][k] for k in 'xyz'] for v in row['particles']])
  for g in row['geometry']:
   center=np.array([[v[k] for k in 'xyz'] for v in g['positions']]).reshape(-1,4,3).mean(axis=1)
   raw=float(np.abs(center-pos).max());pred=float(np.abs(center-(pos+(vel+acceleration*row['fixed_delta_time'])*pending)).max())
   rawmax=max(rawmax,raw);predmax=max(predmax,pred);n+=len(pos)
   rows.append(dict(seed=row['seed'],time=row['requested_time'],repetition=row['repetition'],fixture=g['fixture'],native_pending_time=pending,raw_center_maximum_error=raw,native_source_force_next_step_prediction_maximum_error=pred))
 results.append(dict(node_path=case['node_path'],geometry_observations=len(rows),particle_observations=n,raw_center_maximum_error=rawmax,native_source_force_next_step_prediction_maximum_error=predmax,rows=rows))
O.mkdir();pins={str(p):sha(p) for p in [Path(__file__),I/'GEOMETRY.v1.json',I/'RUN-RESULT.v1.json',I/'INDEPENDENT-REOPEN.v1.json']}
result=dict(status='native_source_force_and_pending_time_measured',input_pins=pins,results=results,native_field_observations=fields,all_playback_fields_unchanged_by_bake=True,fitted_coefficients=False,coordinate_mapping_qualified=False,autonomous_simulation_qualified=False,full_import=False,finished_utc=datetime.datetime.now(datetime.timezone.utc).isoformat())
(O/'RESULT.v1.json').write_text(json.dumps(result,indent=2),'utf-8')
print(json.dumps([{k:v for k,v in r.items() if k!='rows'} for r in results],indent=2))
