"""Independently reopen private controls; preserve failed fidelity verdicts."""
import copy, datetime, hashlib, json, struct
from pathlib import Path

W=Path('F:/OpenRPG_ImportWork');ROOT=W/'firepit-receiver-shadow-controls-reopen-v1'
def sha(p): return hashlib.sha256(Path(p).read_bytes()).hexdigest()
def read(p): return json.loads(Path(p).read_text('utf-8-sig'))
def glb(p):
    data=Path(p).read_bytes();n=struct.unpack_from('<I',data,12)[0]
    return json.loads(data[20:20+n]),data[28+n:]

def main():
    assert not ROOT.exists()
    baseline=W/'firepit-source-shadow-oriented-native-v1'
    original=read(W/'firepit-source-shadow-oriented-case-v1/RESULT.v1.json')
    original_doc,original_binary=glb(original['private_fixture'])
    original_geometry={k:original_doc[k] for k in ['nodes','meshes','accessors']}
    original_materials=copy.deepcopy(original_doc['materials'])
    for m in original_materials: m['extensions']['OPENRPG_source_graph'].pop('forward')
    roots=[('firepit-source-sphere-transition-case-v2','firepit-source-sphere-transition-native-v2','firepit-source-sphere-transition-image-comparison-v2'),('firepit-source-receiver-coordinates-case-v3','firepit-source-receiver-coordinates-native-v3','firepit-source-receiver-coordinates-image-comparison-v4')]
    pins={str(Path(__file__)):sha(Path(__file__))};rows=[]
    for case_name,capture_name,comparison_name in roots:
        c=W/case_name;n=W/capture_name;im=W/comparison_name
        proof=read(c/'RESULT.v1.json');prep=read(n/'PREPARED.v1.json');run=read(n/'RUN-RESULT.v1.json');image=read(im/'RESULT.v1.json')
        assert run['actual_exit_code']==0 and image['status']=='forge_source_shadow_comparison_refused'
        for p,h in {**proof['input_pins'],**prep['input_pins'],**image['input_pins']}.items(): assert sha(p)==h,p
        doc,binary=glb(proof['private_fixture'])
        assert original_geometry=={k:doc[k] for k in original_geometry}
        assert binary[:len(original_binary)]==original_binary
        materials=copy.deepcopy(doc['materials'])
        for m in materials:m['extensions']['OPENRPG_source_graph'].pop('forward')
        assert materials==original_materials
        depth_equal=[]
        for view in range(2):
            p=n/f'NATIVE-SHADOW-DEPTH-{view}.float32';b=baseline/p.name
            depth_equal.append(sha(p)==sha(b));assert depth_equal[-1]
            pins.update({str(p):sha(p),str(b):sha(b)})
        for filename in ['RESULT.v1.json','PROTOCOL.v1.json']:
            p=im/filename;pins[str(p)]=sha(p)
        for p in [c/'RESULT.v1.json',c/'CASE.v1.json',n/'RUN-RESULT.v1.json',n/'PREPARED.v1.json',Path(proof['private_fixture'])]:pins[str(p)]=sha(p)
        rows.append({'capture':str(n),'pins_reverified':len(prep['input_pins']),'source_depth_two_views_byte_identical_to_oriented_baseline':depth_equal,'geometry_and_prepass_material_state_unchanged':True,'original_binary_prefix_bytes_preserved':len(original_binary),'shadow_iou':image['shadow_mask_iou'],'color_mae':[e['mae'] for e in image['linear_color_errors']],'full_import':False})
    pair_identical=[]
    for view in range(2):
        a=W/roots[0][1]/f'gpu-{view}.rgba16f';b=W/roots[1][1]/a.name
        pair_identical.append(sha(a)==sha(b));assert pair_identical[-1]
        pins.update({str(a):sha(a),str(b):sha(b)})
    refusal=W/'firepit-source-sphere-selection-image-comparison-v1/REFUSAL.v1.json'
    failed=read(refusal);assert failed['nonfinite_rgb_pixels_by_view']==[22750,22750]
    for p,h in failed['input_pins'].items():assert sha(p)==h,p
    pins[str(refusal)]=sha(refusal)
    result={'status':'receiver_sphere_border_and_coordinate_controls_reopened_fidelity_refused','finished_utc':datetime.datetime.now(datetime.timezone.utc).isoformat(),'sub_lot':'L01.OMBRES.RECEPTEUR.CONTROLES','controls':rows,'coordinate_control_gpu_images_byte_identical_to_spherical_border_control':pair_identical,'source_spherical_selection_does_not_resolve_current_pixel_mismatch':True,'depth_coordinate_rejection_not_active_in_current_pixel_comparison':True,'first_control_nonfinite_refusal_preserved':True,'console_single_variable_control_label_not_valid_for_v2_v3':'Inherited preparation console label; actual CASE identifies spherical selector plus border formula. v3 additionally changes coordinate handling. These are private controls, not single-variable source-fidelity qualifications.','source_spherical_border_formula_replayed':True,'source_temporal_dither_not_qualified':True,'common_product_policy_installed':False,'full_import':False,'editor_visual_verified':False,'complete_assets_delivered_increment':0,'next_action':'Read back and compare punctual light shadow projection, sampling and active caster participation against the six measured Unity faces; preserve original comparisons and thresholds.','input_pins':pins}
    ROOT.mkdir();p=ROOT/'RESULT.v1.json';p.write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    print(json.dumps({k:v for k,v in result.items() if k not in ['input_pins','controls']},ensure_ascii=False))

if __name__=='__main__':main()
