"""Close private GPU sampling correction, with unchanged original references."""
from pathlib import Path
from datetime import datetime,timezone
import hashlib
import json

W=Path('F:/OpenRPG_ImportWork');ROOT=W/'firepit-source-sampling-policy-technical-v1'
def read(p):return json.loads(p.read_text('utf-8-sig'))
def sha(p):
    with p.open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()

def main():
    assert not ROOT.exists()
    comparison_path=W/'firepit-surface-force-anisotropy-reopen-v1/RESULT.v1.json'
    native_root=W/'firepit-surface-force-anisotropy-v2'
    native=read(native_root/'RUN-RESULT.v1.json');comparison=read(comparison_path)
    policy=read(native_root/'SAMPLING-POLICY.json')
    assert native['actual_exit_code']==0
    assert len(policy)==3 and all(r['effective_anisotropy']==9 and r['effective_mipmap_filter']=='Linear' and r['mips']==12 and r['original_texel_bytes_unchanged'] for r in policy)
    assert [r['format'] for r in policy]==['Bc1RgbaUnormSrgb','Bc3RgbaUnorm','Bc3RgbaUnorm']
    assert 'assert_eq!(copy.data,image.data)' in (native_root/'src/main.rs').read_text('utf-8')
    for p,expected in native['input_pins'].items():assert sha(Path(p))==expected,p
    for p,expected in comparison['input_pins'].items():assert sha(Path(p))==expected,p
    values=[r for view in comparison['rows'] for group in view['groups'].values() for r in group]
    assert len(values)==20 and all(r['within_predeclared_mae'] for r in values)
    prior_path=W/'firepit-surface-packed-hdr-control-reopen-v1/RESULT.v1.json';prior=read(prior_path)
    result=dict(status='private_source_quality_sampling_policy_verified_against_original_unity_surfaces',finished_utc=datetime.now(timezone.utc).isoformat(),
        views_verified=2,surface_mean_comparisons_verified=20,model_comparisons=comparison['rows'][0]['groups']['model'],previous_model_comparisons=prior['rows'][0]['groups']['model'],
        maximum_encoded_surface_mae=comparison['maximum_encoded_surface_mae'],maximum_local_channel_error=max(r['maximum_absolute_error'] for r in values),
        local_pixel_fidelity_qualified=False,original_source_references_unchanged=True,authored_texture_settings_not_overwritten=True,
        source_sampling_quality_is_scene_context=True,original_texture_images_verified=3,original_mips_per_texture=12,native_sampling_policy=policy,
        common_renderer_installation_verified=False,scene_lighting_and_shadows_qualified=False,editor_visual_verified=False,full_import=False,
        vault_assets_updated=0,complete_assets_delivered_increment=0,
        input_pins={str(p):sha(p) for p in [Path(__file__).resolve(),comparison_path,prior_path,native_root/'RUN-RESULT.v1.json',native_root/'SAMPLING-POLICY.json',native_root/'src/main.rs',W/'firepit-sampling-quality-diagnostic-v1/RESULT.v1.json']},
        next_action='Admit an explicit capture-scene sampling policy in the common loader, preserve authored sampler metadata and cache identity, then recompute the full scene against unchanged source images.')
    ROOT.mkdir();(ROOT/'RESULT.v1.json').write_text(json.dumps(result,indent=2),'utf-8')
    print(json.dumps({k:v for k,v in result.items() if k not in ['input_pins','model_comparisons','previous_model_comparisons','native_sampling_policy']}))

if __name__=='__main__':main()
