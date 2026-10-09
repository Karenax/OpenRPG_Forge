"""Compare native render displacement with scaled requested-time remainder.

This is a measured hypothesis, not a replacement for autonomous simulation.
"""
from pathlib import Path
import datetime
import hashlib
import json
import math

W=Path('F:/OpenRPG_ImportWork')
ROOT=W/'delta-firepit-simulation-residual-v1'
SOURCE=W/'unity-delta-firepit-particle-geometry-v1/GEOMETRY.v1.json'
UPSTREAM=W/'delta-firepit-render-velocity-drift-v1/RESULT.v1.json'

def sha(p): return hashlib.sha256(p.read_bytes()).hexdigest()
def read(p): return json.loads(p.read_text('utf-8-sig'))
def check(pins):
    for p,digest in pins.items(): assert sha(Path(p))==digest,'Changed input: '+p

def main():
    assert not ROOT.exists(); upstream=read(UPSTREAM); assert upstream['observations_verified']==336
    pins=dict(upstream['input_pins'])
    for p in [UPSTREAM,SOURCE,Path(__file__).resolve()]: pins[str(p)]=sha(p)
    check(pins); capture=read(SOURCE); results=[]
    for case in capture['cases']:
        duration=case['original_duration']; speed=case['original_simulation_speed']
        assert duration>0 and speed>0 and case['original_loop']
        for row in case['rows']:
            phase=math.fmod(row['requested_time']*speed,duration)
            residual=phase-row['native_system_time']
            if residual<0: residual+=duration
            assert math.isfinite(residual)
            for geometry in row['geometry']:
                assert geometry['vertices']==row['count']*4
                maximum_error=0.0
                for index,particle in enumerate(row['particles']):
                    quad=geometry['positions'][index*4:index*4+4]
                    for k in 'xyz':
                        center=sum(p[k] for p in quad)/4
                        predicted=particle['position'][k]+particle['velocity'][k]*residual
                        maximum_error=max(maximum_error,abs(center-predicted))
                results.append(dict(node_path=case['node_path'],seed=row['seed'],requested_time=row['requested_time'],
                    repetition=row['repetition'],fixture=geometry['fixture'],native_system_time=row['native_system_time'],
                    scaled_requested_phase=phase,measured_clock_residual=residual,
                    maximum_native_center_vs_residual_prediction_error=maximum_error))
    assert len(results)==336; check(pins); ROOT.mkdir()
    node_results=[]
    for node in sorted({r['node_path'] for r in results}):
        rows=[r for r in results if r['node_path']==node]
        node_results.append(dict(node_path=node,observations=len(rows),
            maximum_residual_prediction_error=max(r['maximum_native_center_vs_residual_prediction_error'] for r in rows),
            residual_min=min(r['measured_clock_residual'] for r in rows),residual_max=max(r['measured_clock_residual'] for r in rows)))
    result=dict(status='scaled_source_clock_residual_prediction_measured',input_pins=pins,observations=results,
        observations_verified=336,node_measurements=node_results,
        fixed_step_remainder_cause_qualified=False,original_unity_version_qualified=False,
        force_acceleration_or_native_internal_clock_mapping_qualified=False,
        no_renderer_time_correction_installed=True,autonomous_runtime_qualified=False,full_import=False,
        finished_utc=datetime.datetime.now(datetime.timezone.utc).isoformat())
    (ROOT/'RESULT.v1.json').write_text(json.dumps(result,indent=2),'utf-8')
    print(json.dumps(dict(observations_verified=336,node_measurements=node_results,full_import=False)))

if __name__=='__main__': main()
