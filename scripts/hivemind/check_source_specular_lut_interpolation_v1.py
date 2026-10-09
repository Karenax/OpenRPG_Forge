"""Compare all prior GPU factors to CPU float and 8-bit interpolation, no fitting."""
from pathlib import Path
from datetime import datetime,timezone
import json,struct,math,hashlib
W=Path('F:/OpenRPG_ImportWork');ROOT=W/'source-specular-lut-interpolation-v1'
def read(p):return json.loads(p.read_text('utf-8-sig'))
def sha(p):
    with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def f32(x):return struct.unpack('<f',struct.pack('<f',x))[0]
def main():
    assert not ROOT.exists()
    sdk_path=W/'firepit-source-specular-sdk-reopen-v2/RESULT.v1.json';sdk=read(sdk_path)
    assert sdk['status']=='native_source_deferred_specular_sdk_756_cases_live_context_verified'
    table_path=W/'source-brdf-lookup-case-v1/CASE.v1.json';table=read(table_path)['inputs']
    raw=Path(table['rgba32f']);assert sha(raw)==table['sha256']
    pixels=list(struct.iter_unpack('<4f',raw.read_bytes()));assert len(pixels)==4096
    errors={'float_fraction':0.0,'nearest_256_fraction':0.0};rows=[]
    for case in sdk['rows']:
        gpu=case['source_gpu'];ndv=gpu['energy_factor_ndotv'][3];r=gpu['decoded_normal_roughness'][3]
        x=f32(f32(math.sqrt(min(max(ndv,.0001),1.0)))*63.0);y=f32(r*63.0);ix=int(math.floor(x));iy=int(math.floor(y))
        ax=f32(x-ix);ay=f32(y-iy);f0=gpu['decoded_fresnel_material_specular_ao'][:3];reference=gpu['specular_fgd_reflectivity']
        row=dict(case_id=case['case_id'],source_factor=reference,source_ndotv=ndv,decoded_roughness=r)
        for mode,fx,fy in [('float_fraction',ax,ay),('nearest_256_fraction',round(ax*256)/256,round(ay*256)/256)]:
            def sample(dx,dy):return pixels[min(63,max(0,iy+dy))*64+min(63,max(0,ix+dx))]
            a,b,c,d=sample(0,0),sample(1,0),sample(0,1),sample(1,1)
            lut=[(a[k]*(1-fx)+b[k]*fx)*(1-fy)+(c[k]*(1-fx)+d[k]*fx)*fy for k in range(4)]
            factor=[(1-v)*lut[0]+v*lut[1] for v in f0]+[lut[1]]
            error=max(abs(v-w) for v,w in zip(factor,reference));errors[mode]=max(errors[mode],error)
            row[mode]=dict(factor=factor,maximum_absolute_error=error)
        rows.append(row)
    result=dict(status='prior_source_gpu_lut_factors_interpolation_diagnostic_measured',finished_utc=datetime.now(timezone.utc).isoformat(),cases=len(rows),maximum_absolute_errors=errors,rows=rows,source_data_fitted=False,target_gpu_verified=False,full_import=False,input_pins={str(p):sha(p) for p in [Path(__file__).resolve(),sdk_path,table_path,raw]})
    ROOT.mkdir();(ROOT/'RESULT.v1.json').write_text(json.dumps(result,indent=2),'utf-8')
    print(json.dumps({k:v for k,v in result.items() if k not in ['input_pins','rows']}))
if __name__=='__main__':main()
