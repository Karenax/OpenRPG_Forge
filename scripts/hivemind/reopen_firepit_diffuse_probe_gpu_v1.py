"""Reopen six independent polynomial witnesses from the actual GPU buffer."""
from pathlib import Path
from datetime import datetime,timezone
import json,hashlib,struct,math
W=Path('F:/OpenRPG_ImportWork');CAP=W/'firepit-diffuse-probe-gpu-v1';CASE=W/'firepit-diffuse-probe-gpu-case-v1';ROOT=W/'firepit-diffuse-probe-gpu-reopen-v1'
def read(p):return json.loads(p.read_text('utf-8-sig'))
def sha(p):
    with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def evaluate(c,n):
    x,y,z=n;linear=[x,y,z,1];quadratic=[x*y,y*z,z*z,z*x]
    return [sum(c[channel][j]*linear[j] for j in range(4))+sum(c[channel+3][j]*quadratic[j] for j in range(4))+c[6][channel]*(x*x-y*y) for channel in range(3)]
def main():
    assert not ROOT.exists()
    run=read(CAP/'RUN-RESULT.v1.json');pair=read(CAP/'GPU-PAIR.v1.json');prep=read(CAP/'PREPARED.v1.json');protocol=read(CASE/'RESULT.v1.json');case=read(CASE/'CASE.v1.json')
    assert run['actual_exit_code']==0 and pair['six_direction_diffuse_probe_witness'] and pair['diffuse_probe_bound_from_case']
    for files in [prep['input_pins'],protocol['input_pins']]:
        for p,h in files.items():assert sha(p)==h,p
    c=[[row[k] for k in ['x','y','z','w']] for row in case['diffuse_probe_coefficients']]
    # Target directions, followed by the explicitly reflected source x basis.
    directions=[(-1,0,0),(1,0,0),(0,1,0),(0,-1,0),(0,0,1),(0,0,-1)]
    expected=[evaluate(c,n) for n in directions];rows=[];files=[]
    for view in range(2):
        raw=CAP/f'gpu-{view}.rgba16f';meta=read(CAP/f'gpu-{view}.metadata.json');assert meta['format']=='Rgba16Float' and meta['bytes']==512*512*8
        groups=[dict(samples=0,maximum_absolute_error=0.0,maximum_fraction_of_limit=0.0) for _ in range(6)]
        failures=0
        for index,pixel in enumerate(struct.iter_unpack('<4e',raw.read_bytes())):
            assert all(math.isfinite(v) for v in pixel)
            if pixel[3]!=protocol['witness_alpha']:continue
            x=index%512;y=index//512;direction=(x+y)%6;group=groups[direction];group['samples']+=1
            for actual,wanted in zip(pixel[:3],expected[direction]):
                error=abs(actual-wanted);limit=protocol['absolute_channel_error_floor']+protocol['relative_channel_error_limit']*abs(wanted)
                group['maximum_absolute_error']=max(group['maximum_absolute_error'],error);group['maximum_fraction_of_limit']=max(group['maximum_fraction_of_limit'],error/limit)
                failures+=error>limit
        rows.append(dict(view=view,directions=groups,channels_outside_predeclared_limit=failures))
        files += [raw,CAP/f'gpu-{view}.metadata.json']
    passed=all(row['channels_outside_predeclared_limit']==0 and all(g['samples']>=protocol['minimum_samples_per_direction'] for g in row['directions']) for row in rows)
    result=dict(status='actual_gpu_diffuse_probe_six_direction_evaluation_verified' if passed else 'diffuse_probe_gpu_witness_refused',finished_utc=datetime.now(timezone.utc).isoformat(),images_reopened=2,source_pins_verified=len(prep['input_pins']),expected_source_irradiance=expected,rows=rows,thresholds_defined_before_pixels=True,
        gpu_witness_only=True,brdf_qualified=False,exposure_qualified=False,render_fidelity_qualified=False,full_import=False,editor_visual_verified=False,
        input_pins={str(p):sha(p) for p in [Path(__file__).resolve(),CAP/'RUN-RESULT.v1.json',CAP/'GPU-PAIR.v1.json',CAP/'PREPARED.v1.json',CASE/'RESULT.v1.json',CASE/'CASE.v1.json',*files]},
        next_action='Connect diffuse irradiance through the actual source BRDF/occlusion/exposure with captured FGD and HDRI, then repeat original scene comparison' if passed else 'Inspect raw alpha and directional errors; retain refusal without fitting coefficients or tolerances')
    ROOT.mkdir();(ROOT/'RESULT.v1.json').write_text(json.dumps(result,indent=2),'utf-8')
    print(json.dumps({k:v for k,v in result.items() if k!='input_pins'}))
if __name__=='__main__':main()
