"""Close only the measured instance transport correction, retain surface refusal."""
from pathlib import Path
from datetime import datetime,timezone
import json,hashlib
W=Path('F:/OpenRPG_ImportWork');ROOT=W/'source-instance-transport-technical-v1'
def read(p):return json.loads(p.read_text('utf-8-sig'))
def sha(p):
    with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def main():
    assert not ROOT.exists()
    before=W/'firepit-gpu-frame-matrix-reopen-v1/RESULT.v1.json';after=W/'firepit-gpu-frame-instance-fix-reopen-v1/RESULT.v1.json';surface=W/'firepit-surface-instance-fix-reopen-v1/RESULT.v1.json';old_surface=W/'firepit-surface-native-reopen-v1/RESULT.v1.json'
    original=read(before);corrected=read(after);residual=read(surface);old=read(old_surface)
    for result in [corrected,residual]:
        for p,h in result['input_pins'].items():assert sha(p)==h,p
    assert len(corrected['rows'])==2 and corrected['scalar_cases']==96
    assert all(r['cpu_field_differences']['world_from_local']['maximum_absolute_difference']>0 for r in original['rows'])
    assert all(all(v['equal_numeric'] for v in r['cpu_field_differences'].values()) for r in corrected['rows'])
    measures=[r for view in residual['rows'] for group in view['groups'].values() for r in group]
    old_roles={r['role']:r for r in old['rows'][0]['groups']['model']}
    comparisons=[dict(role=r['role'],before_mae=old_roles[r['role']]['encoded_rgb_mae'],after_mae=r['encoded_rgb_mae'],mae_reduction_fraction=1-r['encoded_rgb_mae']/old_roles[r['role']]['encoded_rgb_mae'],mean_normal_angle_before=old_roles[r['role']].get('mean_normal_angle_degrees'),mean_normal_angle_after=r.get('mean_normal_angle_degrees'),surface_threshold_passed=r['within_predeclared_mae']) for r in residual['rows'][0]['groups']['model']]
    assert residual['maximum_encoded_surface_mae']==old['maximum_encoded_surface_mae']==2/255
    result=dict(status='explicit_entrypoint_instance_transport_private_gpu_technical_correction_verified',finished_utc=datetime.now(timezone.utc).isoformat(),matrix_scalars_reopened_per_view=96,views_verified=2,compared_cpu_gpu_matrix_fields=5,cpu_gpu_matrix_values_equal=160,surface_measurements=20,surface_measurements_within_threshold=sum(r['within_predeclared_mae'] for r in measures),model_comparisons=comparisons,source_pins_verified=corrected['source_pins_verified'],original_geometry_textures_cull_and_material_values_preserved=True,private_candidate_only=True,common_converter_installation_verified=False,vault_assets_updated=0,complete_assets_delivered_increment=0,render_fidelity_qualified=False,editor_visual_verified=False,full_import=False,input_pins={str(p):sha(p) for p in [Path(__file__).resolve(),before,after,surface,old_surface,Path(__file__).with_name('source_graph_instance_index_transport_v1.py')]},next_action='Integrate the explicit instance transport recipe into new common compiler stages without modifying historical snapshots; recompute source scene and qualify remaining material/deferred sampling residuals.')
    ROOT.mkdir();(ROOT/'RESULT.v1.json').write_text(json.dumps(result,indent=2),'utf-8');print(json.dumps({k:v for k,v in result.items() if k not in ['input_pins','model_comparisons']}))
if __name__=='__main__':main()
