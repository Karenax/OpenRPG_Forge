"""Measure original/repeated/SSAO-disabled shadow deltas without promotion."""
from pathlib import Path
from datetime import datetime,timezone
import json,hashlib,struct,math,runpy
W=Path('F:/OpenRPG_ImportWork');CAP=W/'firepit-source-shadow-ssao-control-v1'
ROOT=W/'firepit-source-shadow-ssao-control-reopen-v1'
def read(p):return json.loads(p.read_text('utf-8-sig'))
def sha(p):
    with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def pixels(p,flip=False):
    data=p.read_bytes();assert len(data)==512*512*8
    values=[v[:3] for v in struct.iter_unpack('<4e',data)]
    assert all(math.isfinite(x) for v in values for x in v)
    return [v for y in range(511,-1,-1) for v in values[y*512:(y+1)*512]] if flip else values
def main():
    assert not ROOT.exists()
    run=read(CAP/'RUN-RESULT.v1.json');prep=read(CAP/'PREPARED.v1.json')
    assert run['actual_exit_code']==0 and run['source_inputs_unchanged'] and not run['private_changed']
    assert len(prep['dependency_paths'])==24
    for p,h in prep['input_pins'].items():assert sha(p)==h,p
    proto=W/'firepit-source-shadow-ssao-control-plan-v1/PROTOCOL.v1.json'
    threshold=read(proto)['mask_linear_channel_delta'];assert threshold==1e-4
    masks={};pairs={};files=[Path(__file__).resolve(),CAP/'RUN-RESULT.v1.json',CAP/'PREPARED.v1.json',CAP/'NATIVE-RESULT.v1.json',proto]
    for mode in ['OriginalInitial','OriginalRepeat','NoSSAO']:
        pair=[]
        for view in range(2):
            if mode=='OriginalInitial':
                path=CAP/f'linear-{view}.rgba16f';reference=W/f'firepit-shadow-source-reference-v8/linear-{view}.rgba16f'
                assert sha(path)==sha(reference);files.append(reference)
            else:
                path=CAP/f'CONTROL-{mode}-{view}.rgba16f';meta=path.with_suffix(path.suffix+'.json');state=read(meta)
                assert state['mode']==mode and state['view']==view and state['source_shadows']==(view==0)
                assert state['history']=='CustomUserHistory0' and state['quality_anisotropy']=='ForceEnable'
                assert state['raw_sha256']==sha(path)
                assert state['intensity']==0 if mode=='NoSSAO' else state['intensity']>0
                files.append(meta)
            files.append(path);pair.append(pixels(path,True))
        pairs[mode]=pair
        masks[mode]=[max(abs(x-y) for x,y in zip(a,b))>threshold for a,b in zip(*pair)]
    def overlap(a,b):
        intersection=sum(x and y for x,y in zip(a,b));union=sum(x or y for x,y in zip(a,b))
        return dict(intersection=intersection,union=union,iou=intersection/union if union else 0)
    repeat_errors=[sum(abs(x-y) for a,b in zip(pairs['OriginalInitial'][view],pairs['OriginalRepeat'][view]) for x,y in zip(a,b))/(512*512*3) for view in range(2)]
    result=dict(status='source_shadow_response_ssao_causal_control_measured',finished_utc=datetime.now(timezone.utc).isoformat(),
        original_main_reference_images_unchanged=True,source_dependencies_verified=24,source_inputs_unchanged=True,
        actual_history_channel='CustomUserHistory0',mask_linear_channel_delta=threshold,
        response_pixels={mode:sum(mask) for mode,mask in masks.items()},
        initial_vs_repeat_mask_overlap=overlap(masks['OriginalInitial'],masks['OriginalRepeat']),
        repeat_vs_no_ssao_mask_overlap=overlap(masks['OriginalRepeat'],masks['NoSSAO']),
        repeated_original_pixel_mae=repeat_errors,
        repeat_response_pixels_absent_when_ssao_disabled=sum(a and not b for a,b in zip(masks['OriginalRepeat'],masks['NoSSAO'])),
        no_ssao_response_pixels_absent_in_repeat=sum(b and not a for a,b in zip(masks['OriginalRepeat'],masks['NoSSAO'])),
        ssao_control_is_diagnostic_not_replacement_reference=True,full_shadow_fidelity_qualified=False,full_import=False,
        complete_assets_delivered_increment=0,editor_visual_verified=False,
        input_pins={str(p):sha(p) for p in files},next_action='Use the measured causal control to separate temporal SSAO changes from source shadow map response; retain the full original scene refusal.')
    ROOT.mkdir();(ROOT/'RESULT.v1.json').write_text(json.dumps(result,indent=2),'utf-8')
    print(json.dumps({k:v for k,v in result.items() if k!='input_pins'}))
if __name__=='__main__':main()
