"""Keep stage differences as diagnosis, never as qualification of the refused run."""
from pathlib import Path
from datetime import datetime,timezone
import json,hashlib,struct,math
W=Path('F:/OpenRPG_ImportWork');CAP=W/'firepit-specular-stage-gpu-v1';CASE=W/'firepit-specular-stage-gpu-case-v1';ROOT=W/'firepit-specular-stage-gpu-reopen-v1'
def read(p):return json.loads(p.read_text('utf-8-sig'))
def sha(p):
    with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def main():
    assert not ROOT.exists()
    run=read(CAP/'RUN-RESULT.v1.json');pair=read(CAP/'GPU-PAIR.v1.json');prep=read(CAP/'PREPARED.v1.json');protocol=read(CASE/'RESULT.v1.json')
    assert run['actual_exit_code']==0 and pair['source_specular_cases_expected']==8
    for pins in [prep['input_pins'],protocol['input_pins']]:
        for p,h in pins.items():assert sha(p)==h,p
    source=read(CASE/'CASE.v1.json')['specular_sdk_protocol']['rows']
    names=['decoded_normal_roughness','specular_fgd_reflectivity','sampled_sky','reflected_light_f90']
    rows=[];files=[]
    for view in range(2):
        raw=CAP/f'gpu-{view}.rgba16f';meta=CAP/f'gpu-{view}.metadata.json';m=read(meta)
        assert m['format']=='Rgba16Float' and m['bytes']==512*512*8
        groups=[dict(samples=0,actual_rgb=None,maximum_absolute_error=0.0) for _ in range(8)]
        for i,pixel in enumerate(struct.iter_unpack('<4e',raw.read_bytes())):
            assert all(math.isfinite(v) for v in pixel)
            if pixel[3]!=.5:continue
            k=i%8;g=groups[k];g['samples']+=1;actual=list(pixel[:3]);expected=source[[93,105][k//4]]['source_gpu'][names[k%4]][:3]
            if g['actual_rgb'] is not None:assert g['actual_rgb']==actual
            g.update(actual_rgb=actual,expected_source_rgb=expected,source_case_id=source[[93,105][k//4]]['case_id'],stage=names[k%4])
            g['maximum_absolute_error']=max(g['maximum_absolute_error'],max(abs(a-b) for a,b in zip(actual,expected)))
        assert all(g['samples']>=8 for g in groups)
        rows.append(dict(view=view,stages=groups));files.extend([raw,meta])
    result=dict(status='two_refused_specular_cases_intermediate_gpu_diagnostic_reopened',finished_utc=datetime.now(timezone.utc).isoformat(),rows=rows,full_import=False,render_fidelity_qualified=False,original_refusal_resolved=False,input_pins={str(p):sha(p) for p in [Path(__file__).resolve(),CAP/'RUN-RESULT.v1.json',CAP/'GPU-PAIR.v1.json',CASE/'RESULT.v1.json',CASE/'CASE.v1.json',*files]})
    ROOT.mkdir();(ROOT/'RESULT.v1.json').write_text(json.dumps(result,indent=2),'utf-8')
    print(json.dumps({k:v for k,v in result.items() if k!='input_pins'}))
if __name__=='__main__':main()
