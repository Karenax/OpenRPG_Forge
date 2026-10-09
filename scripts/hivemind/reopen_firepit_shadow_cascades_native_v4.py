"""Verify actual native GPU cascade bytes against retained CPU observations."""
from pathlib import Path
import datetime, hashlib, json, struct
W=Path('F:/OpenRPG_ImportWork');ROOT=W/'firepit-shadow-cascades-gpu-reopen-v1'
SDK=Path('C:/Users/karen/.cargo/registry/src/index.crates.io-1949cf8c6b5b557f')
def read(p):return json.loads(p.read_text('utf-8-sig'))
def sha(p):
    with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def main():
    assert not ROOT.exists()
    cap=W/'firepit-shadow-cascades-native-v4';run=read(cap/'RUN-RESULT.v1.json')
    assert run['actual_exit_code']==0
    for p,h in run['input_pins'].items():assert sha(p)==h,p
    cpu_path=W/'firepit-shadow-cascades-native-reopen-v1/RESULT.v1.json';cpu=read(cpu_path)
    for p,h in cpu['input_pins'].items():assert sha(p)==h,p
    files=[Path(__file__).resolve(),cap/'RUN-RESULT.v1.json',cpu_path]
    scalars_verified=0
    for view in range(2):
        meta_path=cap/f'NATIVE-LIGHTS-GPU-{view}.json';raw_path=cap/f'NATIVE-LIGHTS-GPU-{view}.bin';observed_path=cap/f'NATIVE-SHADOW-CASCADES-{view}.json'
        meta=read(meta_path);raw=raw_path.read_bytes();observed=read(observed_path)
        assert meta['view']==view and meta['view_offsets_count']==1
        assert meta['cascade_stride']==80 and meta['bytes']==len(raw)
        assert meta['gpu_buffer_copied'] and meta['cpu_gpu_scalar_bits_equal'] and not meta['shader_settings_changed']
        assert observed['selected_cascade_cpu']==cpu['native_selected_cascade']
        for index,cascade in enumerate(observed['cascades']):
            values=[*cascade['clip_from_world'],cascade['world_texel_size'],cascade['far_bound']]
            expected=struct.pack('<18f',*values);assert raw[index*80:index*80+72]==expected
            gpu=meta['cascades'][index];assert struct.pack('<18f',*gpu['clip_from_world'],gpu['world_texel_size'],gpu['far_bound'])==expected
            scalars_verified+=18
        old=W/f'firepit-punctual-scene-v2/gpu-{view}.rgba16f';new=cap/f'gpu-{view}.rgba16f'
        assert sha(old)==sha(new),'GPU instrumentation changed captured pixels'
        files.extend([meta_path,raw_path,observed_path,old,new])
    files.extend([SDK/'bevy_pbr-0.19.1/src/render/light.rs',SDK/'bevy_pbr-0.19.1/src/render/mesh_view_types.wgsl',SDK/'bevy_pbr-0.19.1/src/render/shadows.wgsl',SDK/'bevy_render-0.19.1/src/render_resource/uniform_buffer.rs'])
    ROOT.mkdir()
    result=dict(status='actual_native_gpu_cascade_matrices_texels_and_bounds_verified',
        finished_utc=datetime.datetime.now(datetime.timezone.utc).isoformat(),views_verified=2,
        cascades_verified_per_view=4,scalar_bits_verified=scalars_verified,
        gpu_uniform_buffer_readback_verified=True,original_gpu_images_byte_identical=True,
        native_source_pins_verified=len(run['input_pins']),
        native_selected_cascade_from_gpu_bounds_and_cpu_depth=cpu['native_selected_cascade'],
        shader_selected_cascade_index_readback_verified=False,
        native_world_texel_size=cpu['native_world_texel_size'],source_world_texel_size=cpu['source_world_texel_size'],
        source_selected_cascade=cpu['source_selected_cascade'],
        full_import=False,complete_assets_delivered_increment=0,editor_visual_verified=False,
        scene_fidelity_qualified=False,input_pins={str(p):sha(p) for p in files},
        next_action='Admit and replay the measured source shadow context explicitly; verify shadow matrices, resolution and filter before comparing original reference images.')
    (ROOT/'RESULT.v1.json').write_text(json.dumps(result,indent=2),'utf-8');print(json.dumps(result))
if __name__=='__main__':main()
