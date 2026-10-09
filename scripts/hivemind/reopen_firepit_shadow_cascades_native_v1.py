"""Reopen CPU cascade observations; do not promote them to GPU readback."""
from pathlib import Path
import datetime, hashlib, json, math
W=Path('F:/OpenRPG_ImportWork');ROOT=W/'firepit-shadow-cascades-native-reopen-v1'
SDK=Path('C:/Users/karen/.cargo/registry/src/index.crates.io-1949cf8c6b5b557f')
def read(p):return json.loads(p.read_text('utf-8-sig'))
def sha(p):
    with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def main():
    assert not ROOT.exists()
    cap=W/'firepit-shadow-cascades-native-v1'
    run=read(cap/'RUN-RESULT.v1.json');assert run['actual_exit_code']==0
    for p,h in run['input_pins'].items():assert sha(p)==h,p
    source_path=W/'firepit-source-all-lights-gpu-reopen-v2/RESULT.v1.json';source=read(source_path)
    for p,h in source['input_pins'].items():assert sha(p)==h,p
    files=[Path(__file__).resolve(),source_path,cap/'RUN-RESULT.v1.json']
    observed=[]
    for view in range(2):
        p=cap/f'NATIVE-SHADOW-CASCADES-{view}.json';row=read(p)
        assert row['view']==view and not row['shadow_settings_changed'] and not row['gpu_uniform_readback']
        assert row['cascade_count']==len(row['cascades'])==4
        assert row['selected_cascade_cpu']==next(i for i,c in enumerate(row['cascades']) if -row['camera_view_z_at_model_center']<c['far_bound'])
        for cascade in row['cascades']:
            assert cascade['world_texel_size']>0
            for key in ['world_from_cascade','clip_from_cascade','clip_from_world']:
                assert len(cascade[key])==16 and all(math.isfinite(v) for v in cascade[key])
        observed.append(row);files.append(p)
        old=W/f'firepit-punctual-scene-v2/gpu-{view}.rgba16f';new=cap/f'gpu-{view}.rgba16f'
        assert sha(old)==sha(new),'Instrumentation changed captured pixels';files.extend([old,new])
    assert {k:v for k,v in observed[0].items() if k!='view'}=={k:v for k,v in observed[1].items() if k!='view'}
    files.extend([SDK/'bevy_light-0.19.1/src/cascade.rs',SDK/'bevy_pbr-0.19.1/src/render/shadows.wgsl',SDK/'bevy_pbr-0.19.1/src/render/shadow_sampling.wgsl'])
    row=observed[0];native_index=row['selected_cascade_cpu'];source_index=source['source_object_center_selected_cascade']
    native_texel=row['cascades'][native_index]['world_texel_size'];source_texel=source['directional_world_texel_sizes'][source_index]
    ROOT.mkdir()
    result=dict(status='native_cpu_shadow_cascade_context_mismatch_measured_without_pixel_changes',
        finished_utc=datetime.datetime.now(datetime.timezone.utc).isoformat(),views_verified=2,
        native_source_pins_verified=len(run['input_pins']),original_gpu_images_byte_identical=True,
        native_selected_cascade=native_index,source_selected_cascade=source_index,
        native_cascade_bounds=[c['far_bound'] for c in row['cascades']],
        native_directional_shadow_map_size=row['directional_shadow_map_size'],
        source_directional_resolution=source['directional_resolution'],
        native_world_texel_size=native_texel,source_world_texel_size=source_texel,
        source_to_native_world_texel_ratio=source_texel/native_texel,
        source_filter=source['directional_filter_from_sdk'],native_filter='Gaussian (unchanged native case)',
        gpu_uniform_readback_verified=False,causal_image_fidelity_fix_verified=False,
        full_import=False,complete_assets_delivered_increment=0,editor_visual_verified=False,
        input_pins={str(p):sha(p) for p in files},
        next_action='Read the actual GPU lights uniform buffer and verify its cascade matrices/texels against these CPU observations; then admit an explicit source shadow context.')
    (ROOT/'RESULT.v1.json').write_text(json.dumps(result,indent=2),'utf-8');print(json.dumps(result))
if __name__=='__main__':main()
