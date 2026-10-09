"""Close the causal point-offset control without promoting private fidelity."""
from pathlib import Path
import datetime,hashlib,json
W=Path('F:/OpenRPG_ImportWork');CAP=W/'firepit-point-world-bias-control-native-v1';OLD=W/'firepit-point-shadow-diagnostics-native-v1';ROOT=W/'firepit-point-world-bias-control-reopen-v1';IMAGE=W/'firepit-point-world-bias-control-image-comparison-v1'
def read(p):return json.loads(Path(p).read_text('utf-8-sig'))
def sha(p):return hashlib.sha256(Path(p).read_bytes()).hexdigest()
def main():
    assert not ROOT.exists()
    prep=read(CAP/'PREPARED.v1.json');run=read(CAP/'RUN-RESULT.v1.json');image=read(IMAGE/'RESULT.v1.json');assert run['actual_exit_code']==0
    for p,h in {**prep['input_pins'],**image['input_pins']}.items():assert sha(p)==h,p
    case=read(CAP/'CASE.v1.json');policy=case['private_point_world_depth_offset_control'];assert policy['shader_world_offset_m']==0.0 and policy['normal_bias_projection_filter_and_point_gpu_record_preserved']
    files=[Path(__file__),CAP/'PREPARED.v1.json',CAP/'RUN-RESULT.v1.json',CAP/'CASE.v1.json',IMAGE/'RESULT.v1.json',IMAGE/'PROTOCOL.v1.json'];comparisons=[]
    for view in range(2):
        for name in [f'POINT-RECORD-GPU-{view}.bin',f'NATIVE-SHADOW-DEPTH-{view}.float32',*[f'POINT-SHADOW-VIEW-{view}-{face}.float32' for face in range(6)]]:
            a=CAP/name;b=OLD/name;assert sha(a)==sha(b),name
            files.extend([a,b]);comparisons.append(name)
    prior=read(W/'firepit-source-receiver-coordinates-image-comparison-v4/RESULT.v1.json');files.append(W/'firepit-source-receiver-coordinates-image-comparison-v4/RESULT.v1.json')
    result={'status':'point_world_depth_offset_causal_control_reopened_fidelity_open','finished_utc':datetime.datetime.now(datetime.timezone.utc).isoformat(),'sub_lot':'L01.OMBRES.PONCTUELLES.BIAIS.CONTROLE','current_input_pins_reverified':len(prep['input_pins']),'point_gpu_records_shadow_matrices_and_directional_depth_byte_identical':comparisons,'native_world_offset_removed_in_private_forward_shader_only':True,'prior_shadow_iou':prior['shadow_mask_iou'],'control_shadow_iou':image['shadow_mask_iou'],'control_color_mae':[v['mae'] for v in image['linear_color_errors']],'prior_source_response_pixels':prior['source_shadow_response_pixels'],'control_native_response_pixels':image['forge_shadow_response_pixels'],'original_thresholds_unchanged':True,'source_punctual_filter_and_projection_still_unmatched':True,'full_import':False,'editor_visual_verified':False,'complete_assets_delivered_increment':0,'input_pins':{str(p):sha(p) for p in files}}
    ROOT.mkdir();(ROOT/'RESULT.v1.json').write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n','utf-8');print(json.dumps({k:v for k,v in result.items() if k not in ['input_pins','point_gpu_records_shadow_matrices_and_directional_depth_byte_identical']},ensure_ascii=False))
if __name__=='__main__':main()
