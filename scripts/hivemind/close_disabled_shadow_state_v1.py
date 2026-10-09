"""Close only the authored-disabled technical correction, retaining render refusal."""
from pathlib import Path
from datetime import datetime, timezone
import json, hashlib
W=Path('F:/OpenRPG_ImportWork')
ROOT=W/'source-shadow-disabled-state-fix-v1'
def read(p): return json.loads(p.read_text('utf-8-sig'))
def sha(p):
    with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def main():
    out=ROOT/'RESULT.v1.json';assert not out.exists()
    run=W/'firepit-forge-shadow-pair-v4/RUN-RESULT.v1.json'
    pair=W/'firepit-forge-shadow-pair-v4/GPU-PAIR.v1.json'
    web=W/'studio-current-source-wasm-check-v16/RUN-RESULT.v1.json'
    comparison=W/'firepit-forge-shadow-pair-comparison-v2/RESULT.v1.json'
    r,p,w,c=map(read,[run,pair,web,comparison])
    assert r['actual_exit_code']==w['actual_exit_code']==0 and not w['changed_source_files']
    assert p['author_disabled_shadow_frame_allowed'] and p['unsafe_disabled_shadow_parent_and_status_refused_atomically']
    for receipt in [r,w]:
        for file,h in receipt['input_pins'].items():assert sha(file)==h,file
    assert c['gpu_images_reopened']==2 and c['status']=='forge_source_shadow_comparison_refused'
    result=dict(status='authored_disabled_shadow_state_native_gpu_and_web_verified',finished_utc=datetime.now(timezone.utc).isoformat(),
        author_disabled_frame_allowed=True,unsafe_disabled_parent_refused_atomically=True,refused_disabled_status_refused_atomically=True,
        native_exit_code=0,web_exit_code=0,native_source_pins_verified=len(r['input_pins']),web_source_pins_verified=len(w['input_pins']),
        gpu_images_read_back=2,source_pipeline_states_ready=3,render_comparison_status=c['status'],shadow_mask_iou=c['shadow_mask_iou'],
        full_import=False,editor_visual_verified=False,render_fidelity_qualified=False,
        input_pins={str(file):sha(file) for file in [Path(__file__).resolve(),run,pair,web,comparison,ROOT/'host-before.rs',ROOT/'shadow-routing-before.rs']},
        next_action='Capture effective Unity camera volumes, ambient probe and shadow settings; resolve measured render discrepancy')
    out.write_text(json.dumps(result,indent=2,ensure_ascii=False),'utf-8')
    print(json.dumps({k:v for k,v in result.items() if k!='input_pins'}))
if __name__=='__main__':main()
