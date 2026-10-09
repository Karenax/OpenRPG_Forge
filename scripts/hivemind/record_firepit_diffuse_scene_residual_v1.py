"""Keep global improvement distinct from object-region and shadow residuals."""
from pathlib import Path
from datetime import datetime,timezone
import hashlib,json,runpy
W=Path('F:/OpenRPG_ImportWork');ROOT=W/'firepit-diffuse-scene-residual-v1'
def read(p):return json.loads(p.read_text('utf-8-sig'))
def sha(p):
    with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def main():
    assert not ROOT.exists()
    baseline=W/'firepit-forge-shadow-pair-comparison-v2/RESULT.v1.json'
    current=W/'firepit-diffuse-scene-comparison-v5/RESULT.v1.json'
    old=read(baseline);new=read(current)
    for p,h in new['input_pins'].items():assert sha(p)==h,p
    cap=W/'firepit-diffuse-scene-v5';pair=read(cap/'GPU-PAIR.v1.json')
    assert pair['measured_diffuse_applied_on_object_and_receiver'] and pair['native_ssao_compute_ready']
    assert new['status']=='forge_source_shadow_comparison_refused'
    measurement=W/'firepit-shadow-source-measurement-v8/RESULT.v1.json'
    box=read(measurement)['projected_source_bounds_box']
    lib=runpy.run_path(str(Path(__file__).with_name('compare_firepit_diffuse_scene_v5.py')),run_name='residual_math')
    case=read(cap/'CASE.v1.json');rows=[];pins=[baseline,current,measurement,cap/'CASE.v1.json',cap/'GPU-PAIR.v1.json',Path(__file__).resolve()]
    # Source bounds are in bottom-up coordinates. This is a rectangle, not an
    # object segmentation mask, and is a diagnostic only (no changed thresholds).
    x0,y0,x1,y1=box;top=511-y1;bottom=511-y0
    for view in range(2):
        src=Path(case['source_images'][view]['raw_path']);raw=lib['pixels'](src)
        source=[p for y in range(511,-1,-1) for p in raw[y*512:(y+1)*512]]
        region=[]
        for label,folder in [('before',W/'firepit-forge-shadow-pair-v4'),('after',cap)]:
            p=folder/f'gpu-{view}.rgba16f';pixels=lib['pixels'](p);error=0.;count=0
            for y in range(top,bottom+1):
                for x in range(x0,x1+1):
                    i=y*512+x
                    error+=sum(abs(a-lib['neutral'](b)) for a,b in zip(source[i],pixels[i]));count+=3
            region.append(dict(stage=label,projected_bounds_channel_mae=error/count,channels=count))
            pins.append(p)
        before=old['linear_color_errors'][view]['mae'];after=new['linear_color_errors'][view]['mae']
        rows.append(dict(view=view,global_mae_before=before,global_mae_after=after,
            global_mae_reduction_fraction=(before-after)/before,projected_bounds=region))
        pins.append(src)
    result=dict(status='actual_scene_diffuse_progress_measured_full_render_still_refused',
        finished_utc=datetime.now(timezone.utc).isoformat(),rows=rows,
        projected_bounds_source_bottom_up=box,projected_bounds_target_top_down=[x0,top,x1,bottom],
        region_is_rectangle_not_object_segmentation=True,shadow_mask_iou_before=old['shadow_mask_iou'],
        shadow_mask_iou_after=new['shadow_mask_iou'],native_ssao_compute_ready=True,
        source_ssao_algorithm_matched=False,render_fidelity_qualified=False,
        full_import=False,editor_visual_verified=False,input_pins={str(p):sha(p) for p in pins},
        agent_image_review='Receiver is closer; object remains visibly dark and source shadows are broader/softer. This is not editor or human visual acceptance.',
        next_action='Capture source prefiltered specular HDRI and effective shadow filtering; qualify common scene dependencies and target SSAO before claiming render parity.')
    ROOT.mkdir();(ROOT/'RESULT.v1.json').write_text(json.dumps(result,indent=2),'utf-8')
    print(json.dumps({k:v for k,v in result.items() if k!='input_pins'}))
if __name__=='__main__':main()
