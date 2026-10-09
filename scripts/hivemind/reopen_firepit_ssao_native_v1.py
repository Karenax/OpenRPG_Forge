"""Compare raw native visibility with source visibility on the same fixed camera."""
from pathlib import Path
from datetime import datetime,timezone
import json,struct,math,hashlib
W=Path('F:/OpenRPG_ImportWork');CAP=W/'firepit-ssao-native-v1';CASE=W/'firepit-ssao-native-case-v1';ROOT=W/'firepit-ssao-native-reopen-v1'
def read(p):return json.loads(p.read_text('utf-8-sig'))
def sha(p):
    with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def stats(values):
    assert values
    s=sorted(values);n=len(s)
    return dict(samples=n,minimum=s[0],maximum=s[-1],mean=sum(s)/n,p10=s[int((n-1)*.1)],median=s[(n-1)//2],p90=s[int((n-1)*.9)],pixels_below_0999=sum(v<.999 for v in values),pixels_below_05=sum(v<.5 for v in values))
def main():
    assert not ROOT.exists()
    run=read(CAP/'RUN-RESULT.v1.json');pair=read(CAP/'GPU-PAIR.v1.json');prep=read(CAP/'PREPARED.v1.json');protocol=read(CASE/'RESULT.v1.json')
    source_proof=W/'firepit-source-ssao-debug-reopen-v1/RESULT.v1.json';source=read(source_proof)
    assert run['actual_exit_code']==0 and pair['actual_native_ssao_visibility_diagnostic'] and pair['native_ssao_compute_ready']
    assert all(p['state']=='ready' for p in pair['source_pipelines']) and len(pair['source_pipelines'])>=5
    for pins in [prep['input_pins'],protocol['input_pins'],source['input_pins']]:
        for p,h in pins.items():assert sha(p)==h,p
    source_raw=W/'firepit-source-ssao-debug-v1/DEBUG-ScreenSpaceAmbientOcclusion.rgba16f'
    src=list(struct.iter_unpack('<4e',source_raw.read_bytes()));assert len(src)==512*512
    src=[p for y in range(511,-1,-1) for p in src[y*512:(y+1)*512]]
    assert all(p[0]==p[1]==p[2] and all(math.isfinite(x) for x in p) and 0<=p[0]<=1 for p in src)
    bounds_path=W/'firepit-shadow-source-measurement-v8/RESULT.v1.json';box=read(bounds_path)['projected_source_bounds_box'];x0,y0,x1,y1=box;top=511-y1;bottom=511-y0
    rows=[];files=[];views=[]
    for view in range(2):
        raw=CAP/f'gpu-{view}.rgba16f';meta=CAP/f'gpu-{view}.metadata.json';m=read(meta);assert m['format']=='Rgba16Float' and m['bytes']==512*512*8
        target=list(struct.iter_unpack('<4e',raw.read_bytes()));assert len(target)==512*512;views.append(target)
        target_values=[];source_values=[];region_target=[];region_source=[];errors=[];region_errors=[];darker=0
        for i,p in enumerate(target):
            assert all(math.isfinite(v) for v in p)
            if p[3]!=protocol['witness_alpha']:continue
            assert p[0]==p[1]==p[2] and protocol['minimum_visibility']<=p[0]<=protocol['maximum_visibility']
            actual=p[0];wanted=src[i][0];target_values.append(actual);source_values.append(wanted);errors.append(abs(actual-wanted));darker+=actual<wanted
            x=i%512;y=i//512
            if x0<=x<=x1 and top<=y<=bottom:region_target.append(actual);region_source.append(wanted);region_errors.append(abs(actual-wanted))
        rows.append(dict(view=view,target_visibility=stats(target_values),source_visibility_at_target_fragments=stats(source_values),visibility_mae=sum(errors)/len(errors),target_lower_than_source_pixels=darker,projected_bounds_target_visibility=stats(region_target),projected_bounds_source_visibility=stats(region_source),projected_bounds_visibility_mae=sum(region_errors)/len(region_errors)))
        files.extend([raw,meta])
    differences=[abs(a[0]-b[0]) for a,b in zip(*views) if a[3]==b[3]==.5]
    result=dict(status='actual_native_ssao_fragment_visibility_and_source_comparison_measured',finished_utc=datetime.now(timezone.utc).isoformat(),rows=rows,native_between_views_visibility_mae=sum(differences)/len(differences),source_pins_verified=len(prep['input_pins']),projected_bounds_source_bottom_up=box,projected_bounds_target_top_down=[x0,top,x1,bottom],region_is_rectangle_not_object_segmentation=True,native_ssao_settings_changed=False,source_ssao_pixels_used_as_target_input=False,source_algorithm_parity_verified=False,render_fidelity_qualified=False,full_import=False,editor_visual_verified=False,input_pins={str(p):sha(p) for p in [Path(__file__).resolve(),CAP/'RUN-RESULT.v1.json',CAP/'GPU-PAIR.v1.json',CAP/'PREPARED.v1.json',CASE/'RESULT.v1.json',source_proof,source_raw,bounds_path,*files]},next_action='Use measured occlusion differences to inspect SDK/native settings and normal/depth encodings; do not fit exposure or material values.')
    ROOT.mkdir();(ROOT/'RESULT.v1.json').write_text(json.dumps(result,indent=2),'utf-8')
    print(json.dumps({k:v for k,v in result.items() if k!='input_pins'}))
if __name__=='__main__':main()
