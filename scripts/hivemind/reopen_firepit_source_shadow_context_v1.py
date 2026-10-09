"""Verify explicit shadow-context inputs reached the actual GPU buffer."""
from pathlib import Path
import datetime,hashlib,json,struct
W=Path('F:/OpenRPG_ImportWork');ROOT=W/'firepit-source-shadow-context-reopen-v1'
def read(p):return json.loads(p.read_text('utf-8-sig'))
def sha(p):
    with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def main():
    assert not ROOT.exists()
    cap=W/'firepit-source-shadow-context-native-v1';run=read(cap/'RUN-RESULT.v1.json')
    assert run['actual_exit_code']==0
    for p,h in run['input_pins'].items():assert sha(p)==h,p
    case_path=W/'firepit-source-shadow-context-case-v1/CASE.v1.json';case=read(case_path)
    context=case['source_shadow_context_replay'];applied=cap/'SOURCE-SHADOW-CONTEXT-APPLIED.json';assert read(applied)==context
    files=[Path(__file__).resolve(),case_path,applied,cap/'RUN-RESULT.v1.json']
    observed=[];count=0
    for view in range(2):
        cpu_path=cap/f'NATIVE-SHADOW-CASCADES-{view}.json';meta_path=cap/f'NATIVE-LIGHTS-GPU-{view}.json';raw_path=cap/f'NATIVE-LIGHTS-GPU-{view}.bin'
        cpu=read(cpu_path);meta=read(meta_path);raw=raw_path.read_bytes()
        assert cpu['shadow_settings_changed'] and cpu['directional_shadow_map_size']==context['directional_map_size']
        assert cpu['minimum_distance']==context['minimum_distance'] and cpu['overlap_proportion']==context['native_overlap_proportion']
        assert cpu['selected_cascade_cpu']==3 and len(cpu['cascades'])==4
        assert meta['gpu_buffer_copied'] and meta['cpu_gpu_scalar_bits_equal'] and meta['cascade_stride']==80
        for i,cascade in enumerate(cpu['cascades']):
            assert struct.pack('<f',cascade['far_bound'])==struct.pack('<f',context['cascade_bounds'][i])
            values=[*cascade['clip_from_world'],cascade['world_texel_size'],cascade['far_bound']]
            assert raw[i*80:i*80+72]==struct.pack('<18f',*values);count+=18
        observed.append(cpu);files.extend([cpu_path,meta_path,raw_path])
    assert {k:v for k,v in observed[0].items() if k!='view'}=={k:v for k,v in observed[1].items() if k!='view'}
    comparison_path=W/'firepit-source-shadow-context-comparison-v1/RESULT.v1.json';comparison=read(comparison_path)
    files.append(comparison_path)
    ROOT.mkdir()
    result=dict(status='explicit_source_bounds_and_resolution_replay_gpu_verified_fidelity_open',finished_utc=datetime.datetime.now(datetime.timezone.utc).isoformat(),
        views_verified=2,gpu_scalar_bits_verified=count,native_source_pins_verified=len(run['input_pins']),
        cascade_bounds=context['cascade_bounds'],directional_map_size=context['directional_map_size'],selected_cascade=3,
        native_selected_world_texel_size=observed[0]['cascades'][3]['world_texel_size'],comparison_status=comparison['status'],shadow_mask_iou=comparison['shadow_mask_iou'],
        source_filter_matched=False,source_bias_matched=False,source_cascade_borders_matched=False,source_shadow_matrices_matched=False,
        scene_fidelity_qualified=False,full_import=False,complete_assets_delivered_increment=0,editor_visual_verified=False,
        input_pins={str(p):sha(p) for p in files},next_action='Compare the actual source and native shadow projection matrices and sampling kernels before admitting the common source shadow context.')
    (ROOT/'RESULT.v1.json').write_text(json.dumps(result,indent=2),'utf-8');print(json.dumps(result))
if __name__=='__main__':main()
