"""Compare each surface role at fixed pixels; model/receiver alpha masks are explicit."""
from pathlib import Path
from datetime import datetime,timezone
import json,struct,math,hashlib
W=Path('F:/OpenRPG_ImportWork');CAP=W/'firepit-surface-native-v1';CASE=W/'firepit-surface-native-case-v1';ROOT=W/'firepit-surface-native-reopen-v1'
def read(p):return json.loads(p.read_text('utf-8-sig'))
def sha(p):
    with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def inverse_debug(x):return x*12.92 if x<=.0031308 else 1.055*max(x,0)**(1/2.4)-.055
def main():
    assert not ROOT.exists()
    run=read(CAP/'RUN-RESULT.v1.json');pair=read(CAP/'GPU-PAIR.v1.json');prep=read(CAP/'PREPARED.v1.json');protocol=read(CASE/'RESULT.v1.json')
    assert run['actual_exit_code']==0 and pair['actual_source_surface_debug_diagnostic']
    for pins in [prep['input_pins'],protocol['input_pins']]:
        for p,h in pins.items():assert sha(p)==h,p
    refs=protocol['source_surface_debug_refs'];assert len(refs)==5
    sources=[];files=[]
    for ref in refs:
        p=Path(ref['path']);assert sha(p)==ref['sha256'];data=list(struct.iter_unpack('<4e',p.read_bytes()));assert len(data)==512*512
        sources.append([v for y in range(511,-1,-1) for v in data[y*512:(y+1)*512]]);files.append(p)
    rows=[]
    for view in range(2):
        raw=CAP/f'gpu-{view}.rgba16f';meta=CAP/f'gpu-{view}.metadata.json';m=read(meta);assert m['format']=='Rgba16Float' and m['bytes']==512*512*8
        groups={label:[dict(samples=0,absolute_error_sum=0.0,maximum_absolute_error=0.0,target_rgb_sum=[0.0]*3,source_rgb_sum=[0.0]*3,normal_angle_sum_degrees=0.0,source_red_normal_failure_pixels=0) for _ in refs] for label in ['model','receiver']}
        for i,p in enumerate(struct.iter_unpack('<4e',raw.read_bytes())):
            assert all(math.isfinite(v) for v in p)
            label='model' if p[3]==protocol['model_alpha'] else 'receiver' if p[3]==protocol['receiver_alpha'] else None
            if label is None:continue
            mode=i%5;g=groups[label][mode];s=sources[mode][i];assert all(math.isfinite(v) for v in s)
            g['samples']+=1
            for k in range(3):
                error=abs(p[k]-s[k]);g['absolute_error_sum']+=error;g['maximum_absolute_error']=max(g['maximum_absolute_error'],error);g['target_rgb_sum'][k]+=p[k];g['source_rgb_sum'][k]+=s[k]
            if refs[mode]['role']=='Normal':
                g['source_red_normal_failure_pixels']+=s[:3]==(1.,0.,0.)
                a=[inverse_debug(v)*2-1 for v in p[:3]];b=[inverse_debug(v)*2-1 for v in s[:3]]
                denominator=math.sqrt(sum(v*v for v in a)*sum(v*v for v in b));assert denominator>0
                g['normal_angle_sum_degrees']+=math.degrees(math.acos(max(-1.,min(1.,sum(x*y for x,y in zip(a,b))/denominator))))
        for label,data in groups.items():
            for mode,g in enumerate(data):
                n=g['samples'];assert n>0
                g.update(role=refs[mode]['role'],encoded_rgb_mae=g['absolute_error_sum']/(3*n),target_rgb_mean=[v/n for v in g['target_rgb_sum']],source_rgb_mean=[v/n for v in g['source_rgb_sum']],within_predeclared_mae=g['absolute_error_sum']/(3*n)<=protocol['maximum_encoded_surface_mae'])
                if g['role']=='Normal':g['mean_normal_angle_degrees']=g['normal_angle_sum_degrees']/n
                for k in ['absolute_error_sum','target_rgb_sum','source_rgb_sum','normal_angle_sum_degrees']:g.pop(k)
        rows.append(dict(view=view,groups=groups));files.extend([raw,meta])
    passed=all(g['within_predeclared_mae'] for row in rows for group in row['groups'].values() for g in group)
    result=dict(status='native_source_five_surface_debug_roles_within_predeclared_mae' if passed else 'native_source_surface_debug_comparison_refused',finished_utc=datetime.now(timezone.utc).isoformat(),rows=rows,source_pins_verified=len(prep['input_pins']),source_debug_encoding_applied=True,model_and_receiver_masks_explicit=True,maximum_encoded_surface_mae=protocol['maximum_encoded_surface_mae'],thresholds_not_fitted_after_pixels=True,render_fidelity_qualified=False,full_import=False,editor_visual_verified=False,input_pins={str(p):sha(p) for p in [Path(__file__).resolve(),CAP/'RUN-RESULT.v1.json',CAP/'GPU-PAIR.v1.json',CAP/'PREPARED.v1.json',CASE/'RESULT.v1.json',*files]},next_action='Inspect role-specific material, texture sampling and normal-frame discrepancies before direct BRDF changes.' if not passed else 'Compare direct BRDF and effective shadow filtering; preserve verified surface outputs.')
    ROOT.mkdir();(ROOT/'RESULT.v1.json').write_text(json.dumps(result,indent=2),'utf-8');print(json.dumps({k:v for k,v in result.items() if k!='input_pins'}))
if __name__=='__main__':main()
