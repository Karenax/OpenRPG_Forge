"""Record a causal private quality control, without changing source references."""
from pathlib import Path
from datetime import datetime,timezone
import json
import hashlib

W=Path('F:/OpenRPG_ImportWork');ROOT=W/'firepit-sampling-quality-diagnostic-v1'

def read(p):return json.loads(p.read_text('utf-8-sig'))
def sha(p):
    with p.open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()

def main():
    assert not ROOT.exists()
    prior_path=W/'firepit-surface-packed-hdr-control-reopen-v1/RESULT.v1.json'
    control_path=W/'firepit-no-anisotropy-source-control-comparison-v1/RESULT.v1.json'
    control_source=W/'firepit-source-no-anisotropy-control-v1'
    prior=read(prior_path);control=read(control_path);quality=read(control_source/'QUALITY-STATE.json')
    assert quality['anisotropic_filtering']=='ForceEnable' and quality['control']=='Disable'
    assert control['private_source_quality_override'] and not control['source_original_render_fidelity_qualified']
    comparisons=[g for row in control['rows'] for group in row['groups'].values() for g in group]
    assert len(comparisons)==20 and all(g['within_predeclared_mae'] for g in comparisons)
    for p,expected in control['input_pins'].items():assert sha(Path(p))==expected,p
    refs=read(control_source/'RUN-RESULT.v1.json');prep=read(control_source/'PREPARED.v1.json')
    assert refs['source_inputs_unchanged'] and not refs['private_changed'] and len(prep['dependency_paths'])==24
    for p,expected in prep['input_pins'].items():assert sha(Path(p))==expected,p
    result=dict(status='source_capture_scene_global_anisotropy_explains_surface_sampling_discrepancy',finished_utc=datetime.now(timezone.utc).isoformat(),
        source_quality=quality,source_quality_is_capture_scene_context=True,authored_texture_settings_not_overwritten=True,
        private_control_comparisons_within_threshold=20,private_control_model=control['rows'][0]['groups']['model'],
        original_reference_model=prior['rows'][0]['groups']['model'],original_reference_fidelity_still_open=True,
        source_dependencies_verified=24,source_main_reference_images_byte_identical=True,
        source_sampling_policy_reference='https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Texture-anisoLevel.html',
        proposed_private_native_policy=dict(declared_texture_anisotropy=1,source_quality='ForceEnable',effective_anisotropy=9,mipmap_filter='Linear',graphics_api='source D3D11; target DX12'),
        full_import=False,editor_visual_verified=False,vault_assets_updated=0,complete_assets_delivered_increment=0,
        input_pins={str(p):sha(p) for p in [Path(__file__).resolve(),prior_path,control_path,control_source/'QUALITY-STATE.json',control_source/'RUN-RESULT.v1.json',control_source/'PREPARED.v1.json']},
        next_action='Reopen native ForceEnable policy against unchanged original source references, then admit an explicit scene sampling context instead of rewriting authored texture metadata.')
    ROOT.mkdir();(ROOT/'RESULT.v1.json').write_text(json.dumps(result,indent=2),'utf-8')
    print(json.dumps({k:v for k,v in result.items() if k not in ['input_pins','original_reference_model','private_control_model']}))

if __name__=='__main__':main()
