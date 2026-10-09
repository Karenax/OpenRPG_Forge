"""Measure displacement along native velocity; do not assume its cause."""
from pathlib import Path
import datetime
import hashlib
import json
import math
import statistics

W=Path('F:/OpenRPG_ImportWork')
ROOT=W/'delta-firepit-render-velocity-drift-v1'
SOURCE=W/'unity-delta-firepit-particle-geometry-v1/GEOMETRY.v1.json'
UPSTREAM=W/'delta-firepit-geometry-buffers-v2/RESULT.v1.json'

def sha(p): return hashlib.sha256(p.read_bytes()).hexdigest()
def read(p): return json.loads(p.read_text('utf-8-sig'))
def check(pins):
    for p,digest in pins.items(): assert sha(Path(p))==digest,'Changed native input: '+p

def main():
    assert not ROOT.exists()
    proof=read(UPSTREAM); assert proof['observations_verified']==336
    pins=dict(proof['input_pins'])
    for p in [SOURCE,UPSTREAM,Path(__file__).resolve()]: pins[str(p)]=sha(p)
    check(pins); capture=read(SOURCE); measurements=[]; camera_differences=[]; all_deltas=[]
    for case in capture['cases']:
        for row in case['rows']:
            camera_centers=[]
            for geometry in row['geometry']:
                assert geometry['vertices']==row['count']*4
                centers=[]; deltas=[]; residuals=[]; zero_velocity_errors=[]
                for index,particle in enumerate(row['particles']):
                    quad=geometry['positions'][index*4:index*4+4]
                    center=[sum(p[k] for p in quad)/4 for k in 'xyz']; centers.append(center)
                    drift=[center[i]-particle['position'][k] for i,k in enumerate('xyz')]
                    velocity=[particle['velocity'][k] for k in 'xyz']; norm2=sum(v*v for v in velocity)
                    if norm2<=1e-16:
                        zero_velocity_errors.append(math.sqrt(sum(d*d for d in drift))); continue
                    delta=sum(d*v for d,v in zip(drift,velocity,strict=True))/norm2
                    residual=math.sqrt(sum((d-delta*v)**2 for d,v in zip(drift,velocity,strict=True)))
                    assert math.isfinite(delta) and math.isfinite(residual)
                    deltas.append(delta); residuals.append(residual); all_deltas.append(delta)
                camera_centers.append(centers)
                measurements.append(dict(node_path=case['node_path'],seed=row['seed'],requested_time=row['requested_time'],
                    native_system_time=row['native_system_time'],repetition=row['repetition'],fixture=geometry['fixture'],
                    particles=row['count'],nonzero_velocity_particles=len(deltas),
                    projected_delta_min=min(deltas,default=None),projected_delta_max=max(deltas,default=None),
                    projected_delta_median=statistics.median(deltas) if deltas else None,
                    maximum_orthogonal_residual=max(residuals,default=0.0),
                    maximum_zero_velocity_drift=max(zero_velocity_errors,default=0.0)))
            assert len(camera_centers)==3
            differences=[max(abs(a-b) for a,b in zip(center,other,strict=True))
                for centers in camera_centers[1:] for center,other in zip(camera_centers[0],centers,strict=True)]
            camera_differences.append(max(differences,default=0.0))
    assert len(measurements)==336; check(pins); ROOT.mkdir()
    result=dict(status='native_render_centers_velocity_projection_measured',input_pins=pins,
        measurements=measurements,observations_verified=336,projected_quad_samples=len(all_deltas),
        projected_delta_min=min(all_deltas),projected_delta_max=max(all_deltas),
        projected_delta_median=statistics.median(all_deltas),
        maximum_orthogonal_residual=max(row['maximum_orthogonal_residual'] for row in measurements),
        maximum_camera_center_difference=max(camera_differences),
        interpolation_cause_qualified=False,time_mapping_to_forge_qualified=False,
        geometry_indices_not_remapped=True,autonomous_runtime_qualified=False,full_import=False,
        finished_utc=datetime.datetime.now(datetime.timezone.utc).isoformat())
    (ROOT/'RESULT.v1.json').write_text(json.dumps(result,indent=2),'utf-8')
    print(json.dumps({k:result[k] for k in ['observations_verified','projected_quad_samples','projected_delta_min',
        'projected_delta_max','projected_delta_median','maximum_orthogonal_residual','maximum_camera_center_difference','full_import']}))

if __name__=='__main__': main()
