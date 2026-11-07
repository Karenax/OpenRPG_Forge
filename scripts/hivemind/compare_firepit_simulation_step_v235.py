"""Compare native fixed/variable-step geometry against each mode's live particles.

No fitted position/color correction; no tolerance or qualification promotion.
"""
from pathlib import Path
import datetime,hashlib,json
import numpy as np
W=Path('F:/OpenRPG_ImportWork');O=W/'firepit-particle-step-comparison-v235'
def read(p):return json.loads(p.read_text('utf-8-sig'))
def sha(p):
    with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def measure(capture):
    results=[]
    for case in capture['cases']:
        center_error=color_error=time_error=0.;observations=particles=color_differences=0
        details=[]
        for row in case['rows']:
            p=np.array([[v['position'][k] for k in 'xyz'] for v in row['particles']]);color=np.array([[v['current_color'][k] for k in 'rgba'] for v in row['particles']])
            expected=(row['requested_time']*case['original_simulation_speed'])%case['original_duration'];delta=abs(expected-row['native_system_time']);time_error=max(time_error,min(delta,abs(delta-case['original_duration'])))
            for g in row['geometry']:
                center=np.array([[v[k] for k in 'xyz'] for v in g['positions']]).reshape(-1,4,3).mean(axis=1)
                error=float(np.max(np.abs(center-p)))
                baked=np.array([[v[k] for k in 'rgba'] for v in g['colors']]).reshape(-1,4,4)
                assert np.all(baked==baked[:,0:1,:]);color_delta=np.abs(baked[:,0,:]/255-np.clip(color,0,1));ce=float(color_delta.max())
                center_error=max(center_error,error);color_error=max(color_error,ce);color_differences+=int((color_delta.max(axis=1)>1/255).sum());observations+=1;particles+=len(p)
                details.append(dict(seed=row['seed'],time=row['requested_time'],native_time=row['native_system_time'],repetition=row['repetition'],fixture=g['fixture'],count=len(p),maximum_center_error=error,maximum_raw_color_error=ce))
        results.append(dict(node_path=case['node_path'],observations=observations,particle_observations=particles,maximum_center_error=center_error,maximum_raw_color_error=color_error,particles_color_difference_over_one_byte=color_differences,maximum_wrapped_clock_difference=time_error,rows=details))
    return results
assert not O.exists();pins={str(Path(__file__)):sha(Path(__file__))};reports=[]
for name in ['firepit-particle-transform-native-v230','firepit-particle-variable-step-native-v234']:
    folder=W/name;state=read(folder/'RUN-RESULT.v1.json');assert state['unity_exit_code']==0 and state['native_geometry_verified']
    proof=folder/'INDEPENDENT-REOPEN.v1.json';assert sha(proof)==state['independent_receipt_sha256']
    capture=folder/'GEOMETRY.v1.json';assert sha(capture)==read(proof)['capture_sha256']
    for p in [folder/'RUN-RESULT.v1.json',proof,capture,folder/'TRANSFORM-MEASUREMENTS.v1.json']:pins[str(p)]=sha(p)
    reports.append(read(capture))
for fixed,variable in zip(*[r['cases'] for r in reports]):
    for key in fixed:
        if key not in ['rows','simulation_protocol']:assert fixed[key]==variable[key],key
    assert 'fixedTimeStep=true' in fixed['simulation_protocol'] and 'fixedTimeStep=false' in variable['simulation_protocol']
fixed=measure(reports[0]);variable=measure(reports[1]);O.mkdir()
result=dict(status='native_fixed_and_variable_simulation_steps_compared',input_pins=pins,fixed_step=fixed,variable_step=variable,only_clone_simulation_protocol_changed=True,original_component_configuration_unchanged=True,fitted_correction_applied=False,autonomous_simulation=False,source_original_runtime_qualified=False,full_import=False,finished_utc=datetime.datetime.now(datetime.timezone.utc).isoformat())
(O/'RESULT.v1.json').write_text(json.dumps(result,indent=2),'utf-8')
print(json.dumps(dict(status=result['status'],fixed=[{k:v for k,v in r.items() if k!='rows'} for r in fixed],variable=[{k:v for k,v in r.items() if k!='rows'} for r in variable]),indent=2))
