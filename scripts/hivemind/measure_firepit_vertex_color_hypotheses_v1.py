"""Measure raw/sRGB-linear color hypotheses on existing real BakeMesh buffers.

No native recapture, particle remapping, tolerance inflation, or runtime admission.
"""
from pathlib import Path
import datetime
import hashlib
import json
import math

W=Path('F:/OpenRPG_ImportWork')
ROOT=W/'delta-firepit-vertex-color-hypotheses-v1'
SOURCE=W/'unity-delta-firepit-particle-geometry-v1/GEOMETRY.v1.json'
UPSTREAM=W/'delta-firepit-geometry-buffers-v2/RESULT.v1.json'
TOLERANCE=1.0/255.0

def sha(p): return hashlib.sha256(p.read_bytes()).hexdigest()
def read(p): return json.loads(p.read_text('utf-8-sig'))
def check(pins):
    for p,digest in pins.items(): assert sha(Path(p))==digest,'Changed native buffer: '+p
def linear(v): return v/12.92 if v<=0.04045 else ((v+0.055)/1.055)**2.4

def main():
    assert not ROOT.exists()
    proof=read(UPSTREAM); assert proof['observations_verified']==336
    pins=dict(proof['input_pins'])
    for p in [SOURCE,UPSTREAM,Path(__file__).resolve()]: pins[str(p)]=sha(p)
    check(pins); capture=read(SOURCE); observations=[]; examples=[]; samples=0
    maxima={name:{k:0.0 for k in 'rgba'} for name in ['raw_get_current_color','linearized_get_current_color']}
    rejected={name:0 for name in maxima}
    for case in capture['cases']:
        for row in case['rows']:
            for geometry in row['geometry']:
                assert geometry['vertices']==row['count']*4
                local={name:0.0 for name in maxima}
                for index,particle in enumerate(row['particles']):
                    colors=geometry['colors'][index*4:index*4+4]
                    assert len(colors)==4 and all(c==colors[0] for c in colors)
                    actual={k:colors[0][k]/255.0 for k in 'rgba'}
                    current=particle['current_color']; assert all(math.isfinite(current[k]) for k in 'rgba')
                    expected={'raw_get_current_color':current,
                        'linearized_get_current_color':{k:(current[k] if k=='a' else linear(current[k])) for k in 'rgba'}}
                    differences={}
                    for name,predicted in expected.items():
                        errors={k:abs(actual[k]-min(1.0,max(0.0,predicted[k]))) for k in 'rgba'}
                        for k,error in errors.items(): maxima[name][k]=max(maxima[name][k],error)
                        worst=max(errors.values()); local[name]=max(local[name],worst)
                        if worst>TOLERANCE: rejected[name]+=1
                        differences[name]=worst
                    if len(examples)<8 and differences['linearized_get_current_color']>TOLERANCE:
                        examples.append(dict(node_path=case['node_path'],seed=row['seed'],requested_time=row['requested_time'],
                            fixture=geometry['fixture'],particle_index=index,get_current_color=current,baked_color32=colors[0],errors=differences))
                    samples+=1
                observations.append(dict(node_path=case['node_path'],seed=row['seed'],requested_time=row['requested_time'],
                    repetition=row['repetition'],fixture=geometry['fixture'],quad_samples=row['count'],maximum_errors=local))
    assert len(observations)==336
    check(pins); ROOT.mkdir()
    matching=[name for name,count in rejected.items() if count==0]
    result=dict(status='native_vertex_color_hypotheses_measured',input_pins=pins,observations=observations,
        observations_verified=336,quad_color_samples=samples,maximum_channel_errors=maxima,
        rejected_quad_counts=rejected,hypothesis_tolerance_predeclared=TOLERANCE,
        matching_hypotheses=matching,unique_color_mapping_qualified=len(matching)==1,
        mismatch_examples=examples,particle_index_mapping_not_reordered=True,
        interpolation_cause_qualified=False,autonomous_runtime_qualified=False,full_import=False,
        finished_utc=datetime.datetime.now(datetime.timezone.utc).isoformat())
    (ROOT/'RESULT.v1.json').write_text(json.dumps(result,indent=2),'utf-8')
    print(json.dumps({k:result[k] for k in ['observations_verified','quad_color_samples','maximum_channel_errors','rejected_quad_counts','matching_hypotheses','full_import']}))

if __name__=='__main__': main()
