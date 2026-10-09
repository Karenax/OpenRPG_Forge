"""Close only the common scene sampling technical slice, never an asset."""
from pathlib import Path
from datetime import datetime, timezone
import json,hashlib
W=Path('F:/OpenRPG_ImportWork')
CAP=W/'firepit-surface-common-sampling-v2'
COMPARE=W/'firepit-surface-common-sampling-reopen-v2'
ROOT=W/'source-scene-common-sampling-technical-v2'
def read(p):return json.loads(p.read_text('utf-8-sig'))
def sha(p):
    with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def main():
    assert not ROOT.exists()
    run=read(CAP/'RUN-RESULT.v1.json');assert run['actual_exit_code']==0
    comparison=read(COMPARE/'RESULT.v1.json')
    controls=[v for row in comparison['rows'] for values in row['groups'].values() for v in values]
    assert len(controls)==20 and all(v['within_predeclared_mae'] for v in controls)
    samplers=read(CAP/'SAMPLING-POLICY.json');assert len(samplers)==3
    assert all(v['common_mount'] and v['effective_anisotropy']==9 and v['effective_mipmap_filter']=='Linear' and v['mips']==12 for v in samplers)
    refusals=read(CAP/'SAMPLING-REFUSALS.json');assert all(refusals.values()) and len(refusals)==3
    prepared=read(CAP/'PREPARED.v1.json')
    for p,h in prepared['input_pins'].items():assert sha(p)==h,p
    for p,h in comparison['input_pins'].items():assert sha(p)==h,p
    files=[Path(__file__).resolve(),CAP/'RUN-RESULT.v1.json',CAP/'PREPARED.v1.json',CAP/'SAMPLING-POLICY.json',CAP/'SAMPLING-REFUSALS.json',COMPARE/'RESULT.v1.json']
    result=dict(status='actual_common_studio_scene_sampling_gpu_mean_checks_verified',finished_utc=datetime.now(timezone.utc).isoformat(),
        sub_lot='L01.MATERIAUX.SAMPLER.COMMUN',surface_mean_comparisons_expected=20,surface_mean_comparisons_verified=20,
        original_texture_images_verified=3,mips_per_original_image=12,authored_metadata_preserved=True,
        original_image_storage_integrity_checked_by_common_loader=True,common_host_mount_executed=True,
        missing_actual_adapter_refused=True,changed_sampling_hides_host=True,images_owned_per_scene_not_shader_cache=True,
        maximum_local_channel_error=max(v['maximum_absolute_error'] for v in controls),
        common_native_sampling_verified=True,web_compile_verified=False,editor_visual_verified=False,
        full_scene_lighting_and_shadows_qualified=False,local_pixel_fidelity_qualified=False,
        full_import=False,complete_assets_delivered_increment=0,vault_assets_updated=0,
        input_pins={str(p):sha(p) for p in files},
        next_action='Recompare the unchanged full scene with the explicit common sampling context; resolve shadow and lighting residuals, then qualify editor usage.')
    ROOT.mkdir();(ROOT/'RESULT.v1.json').write_text(json.dumps(result,indent=2),'utf-8')
    print(json.dumps({k:v for k,v in result.items() if k!='input_pins'}))
if __name__=='__main__':main()
