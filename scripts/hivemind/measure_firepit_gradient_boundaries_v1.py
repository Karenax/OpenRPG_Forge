"""Measure color paths against all reopened native boundary quads.

Keep exact/one-byte outcomes separate; do not install an approximation.
"""
from pathlib import Path
import datetime
import hashlib
import importlib.util
import json
import math

HERE=Path(__file__).resolve().parent
W=Path('F:/OpenRPG_ImportWork')
ROOT=W/'firepit-gradient-boundary-color-paths-v1'
CAPTURE=W/'unity-delta-firepit-gradient-boundaries-v2/GRADIENT-BOUNDARIES.v1.json'
PROOF=W/'firepit-gradient-boundaries-independent-reopen-v4/INDEPENDENT-REOPEN.v1.json'
HELPER=HERE/'measure_firepit_gradient_lookup_v1.py'
def sha(p): return hashlib.sha256(p.read_bytes()).hexdigest()
def read(p): return json.loads(p.read_text('utf-8-sig'))

def main():
    assert not ROOT.exists()
    proof=read(PROOF); assert proof['boundary_frames_verified']==4096 and proof['float32_ulp_corruption_refused']
    assert sha(CAPTURE)==proof['capture_sha256']
    spec=importlib.util.spec_from_file_location('source_gradient_math',HELPER)
    math_source=importlib.util.module_from_spec(spec); spec.loader.exec_module(math_source)
    f32,linear,evaluate=math_source.f32,math_source.linear,math_source.evaluate
    pins=dict(proof['input_pins'])
    for p in [PROOF,HELPER,Path(__file__).resolve()]: pins[str(p)]=sha(p)
    math_source.check(pins)
    grids=['continuous','floor_64','floor_128','floor_256','floor_127_div127','floor_128_div127','floor_255_div255','floor_256_div255']
    transfers=['raw','linear_before_gradient','linear_after_gradient']
    quantizers=['nearest','truncate']
    ROOT.mkdir()
    (ROOT/'PREDECLARED.v1.json').write_text(json.dumps(dict(clocks=['native_age','requested_age'],grids=grids,
        transfers=transfers,quantizers=quantizers,channel_tolerance_bytes=1,no_runtime_correction=True,full_import=False),indent=2),'utf-8')
    pins[str(ROOT/'PREDECLARED.v1.json')]=sha(ROOT/'PREDECLARED.v1.json')
    results={}; count=0
    def measure(name,values,actual,node):
        error=max(abs(values[k]-actual[k]) for k in 'rgba')
        state=results.setdefault(name,dict(samples=0,exact=0,within_one_byte=0,maximum_byte_error=0,by_node={}))
        for stats in [state,state['by_node'].setdefault(node,dict(samples=0,exact=0,within_one_byte=0,maximum_byte_error=0))]:
            stats['samples']+=1; stats['exact']+=int(error==0); stats['within_one_byte']+=int(error<=1); stats['maximum_byte_error']=max(stats['maximum_byte_error'],error)
    def quant(values,transfer,quantizer):
        return {k:min(255,max(0,math.floor(f32((linear(v) if transfer=='linear_after_gradient' and k!='a' else v)*255)+(0.5 if quantizer=='nearest' else 0)))) for k,v in values.items()}
    for case in read(CAPTURE)['cases']:
        gradient=json.loads(case['native_particle_json'])['ParticleSystem']['ColorModule']['gradient']['maxGradient']
        tables={}
        for transfer in transfers:
            tables[transfer]={}
            for channel in 'rgba':
                keys=[(f32(gradient[('atime' if channel=='a' else 'ctime')+str(i)]/65535), f32(gradient['key'+str(i)][channel]))
                    for i in range(gradient['m_NumAlphaKeys' if channel=='a' else 'm_NumColorKeys'])]
                if transfer=='linear_before_gradient' and channel!='a': keys=[(t,linear(v)) for t,v in keys]
                tables[transfer][channel]=keys
        for row in case['rows']:
            actual=row['geometry'][0]['colors'][0]; count+=1
            for path in ['gradient_evaluation','current_color']:
                values=row[path] if path=='gradient_evaluation' else row['particles'][0][path]
                for transfer in ['raw','linear_after_gradient']:
                    for quantizer in quantizers: measure('|'.join([path,transfer,quantizer]),quant(values,transfer,quantizer),actual,case['node_path'])
            for clock in ['native_age','requested_age']:
                age=f32(row['normalized_age_from_particle' if clock=='native_age' else 'requested_normalized_age'])
                for grid in grids:
                    time=age
                    if grid!='continuous':
                        terms=grid.split('_'); scale=int(terms[1]); divisor=int(terms[2][3:]) if len(terms)==3 else scale
                        time=min(1,max(0,f32(math.floor(age*scale)/divisor)))
                    for transfer in transfers:
                        values={k:evaluate(tables[transfer][k],time) for k in 'rgba'}
                        for quantizer in quantizers:
                            measure('|'.join([clock,grid,transfer,quantizer]),quant(values,transfer,quantizer),actual,case['node_path'])
    assert count==4096 and all(v['samples']==4096 for v in results.values())
    math_source.check(pins)
    ranked=sorted(results,key=lambda n:(-results[n]['exact'],results[n]['maximum_byte_error'],-results[n]['within_one_byte']))
    result=dict(status='native_boundary_color_paths_measured',observed_utc=datetime.datetime.now(datetime.timezone.utc).isoformat(),
        input_pins=pins,native_quads=4096,hypotheses=results,ranked_hypotheses=ranked,
        exact_matches=[n for n in ranked if results[n]['maximum_byte_error']==0],
        within_one_byte_matches=[n for n in ranked if results[n]['maximum_byte_error']<=1],
        native_internal_lookup_cause_qualified=False,runtime_qualified=False,no_runtime_correction_installed=True,full_import=False)
    (ROOT/'RESULT.v1.json').write_text(json.dumps(result,indent=2),'utf-8')
    print(json.dumps(dict(native_quads=count,exact_matches=result['exact_matches'],within_one_byte_matches=result['within_one_byte_matches'],
        best=[dict(name=n,**results[n]) for n in ranked[:4]],full_import=False)))
if __name__=='__main__': main()
