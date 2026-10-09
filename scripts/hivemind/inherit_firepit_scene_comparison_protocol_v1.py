"""Inherit existing thresholds verbatim; record late path binding honestly."""
from pathlib import Path
from datetime import datetime,timezone
import json,hashlib
W=Path('F:/OpenRPG_ImportWork');OLD=W/'firepit-source-ao-scene-comparison-v1/PROTOCOL.v1.json';ROOT=W/'firepit-source-scene-instance-fix-comparison-v1';CAP=W/'firepit-source-scene-instance-fix-v1'
def sha(p):
    with p.open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def main():
    assert not ROOT.exists()
    old=json.loads(OLD.read_text('utf-8'));run=json.loads((CAP/'RUN-RESULT.v1.json').read_text('utf-8'))
    assert old['maximum_comparison_mae']==2/255 and old['minimum_shadow_mask_iou']==.95 and old['mask_linear_channel_delta']==1e-4
    assert datetime.fromisoformat(old['created_utc'])<datetime.fromisoformat(run['finished_utc'])
    for p,h in old['input_pins'].items():assert sha(Path(p))==h,p
    derived={**old,'created_utc':datetime.now(timezone.utc).isoformat(),'threshold_definition_utc':old['created_utc'],'thresholds_inherited_from_protocol_before_current_capture':True,'gpu_pixels_absent_at_definition':False,'current_case_paths_bound_after_capture':True,'source_case_sha256':sha(CAP/'CASE.v1.json'),'parent_protocol':str(OLD),'parent_protocol_sha256':sha(OLD),'input_pins':{**old['input_pins'],str(OLD):sha(OLD),str(Path(__file__).resolve()):sha(Path(__file__).resolve()),str(Path(__file__).with_name('compare_firepit_source_scene_instance_fix_v1.py')):sha(Path(__file__).with_name('compare_firepit_source_scene_instance_fix_v1.py'))}}
    ROOT.mkdir();(ROOT/'PROTOCOL.v1.json').write_text(json.dumps(derived,indent=2),'utf-8');print('Seuils historiques inchangés ; chemins liés après capture, chronologie explicitée.')
if __name__=='__main__':main()
