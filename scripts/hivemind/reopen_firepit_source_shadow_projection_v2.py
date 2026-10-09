"""Verify source projection transport into receiver GPU and caster view fields."""
from pathlib import Path
import datetime,hashlib,json,struct
W=Path('F:/OpenRPG_ImportWork');ROOT=W/'firepit-source-shadow-projection-reopen-v1'
def read(p):return json.loads(p.read_text('utf-8-sig'))
def sha(p):
    with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def packed(values):return struct.pack('<'+'f'*len(values),*values)
def main():
    assert not ROOT.exists()
    cap=W/'firepit-source-shadow-projection-native-v2';run=read(cap/'RUN-RESULT.v1.json');assert run['actual_exit_code']==0
    for p,h in run['input_pins'].items():assert sha(p)==h,p
    case_path=W/'firepit-source-shadow-projection-case-v1/CASE.v1.json';case=read(case_path)
    projection=case['source_shadow_projection_replay'];assert read(cap/'SOURCE-SHADOW-PROJECTIONS-APPLIED.json')==projection
    files=[Path(__file__).resolve(),cap/'RUN-RESULT.v1.json',case_path,cap/'SOURCE-SHADOW-PROJECTIONS-APPLIED.json']
    scalar_bits=0;caster_fields=0
    for view in range(2):
        cpu_path=cap/f'NATIVE-SHADOW-CASCADES-{view}.json';gpu_path=cap/f'NATIVE-LIGHTS-GPU-{view}.bin';camera_path=cap/f'CAMERA-STATE-{view}.json'
        cpu=read(cpu_path);raw=gpu_path.read_bytes();extracted=read(camera_path)['extracted_views']
        assert cpu['selected_cascade_cpu']==3 and cpu['directional_shadow_map_size']==512
        for index,row in enumerate(projection['cascades']):
            actual=cpu['cascades'][index]
            assert packed(actual['clip_from_world'])==packed(row['clip_from_world'])
            assert packed([actual['world_texel_size']])==packed([row['texel_size']])
            fields=[*row['clip_from_world'],row['texel_size'],case['source_shadow_context_replay']['cascade_bounds'][index]]
            assert raw[index*80:index*80+72]==packed(fields);scalar_bits+=18
            matching=[v for v in extracted if packed(v['clip_from_view'])==packed(row['clip_from_cascade']) and packed(v['world_from_view'])==packed(row['world_from_cascade'])]
            assert len(matching)==1,'Caster projection/view fields absent or ambiguous';caster_fields+=32
        files.extend([cpu_path,gpu_path,camera_path,cap/f'NATIVE-LIGHTS-GPU-{view}.json'])
    comparison_path=W/'firepit-source-shadow-projection-comparison-v2/RESULT.v1.json';comparison=read(comparison_path);files.append(comparison_path)
    ROOT.mkdir()
    result=dict(status='source_projection_receiver_gpu_and_caster_view_fields_verified_fidelity_refused',
        finished_utc=datetime.datetime.now(datetime.timezone.utc).isoformat(),views_verified=2,
        receiver_gpu_scalar_bits_verified=scalar_bits,caster_extracted_field_bits_verified=caster_fields,
        caster_view_uniform_gpu_readback_verified=False,native_source_pins_verified=len(run['input_pins']),
        shadow_mask_iou=comparison['shadow_mask_iou'],comparison_status=comparison['status'],
        source_texel_convention='HDRP diagonal; native edge equals diagonal divided by sqrt(2)',
        source_filter_matched=False,source_bias_matched=False,source_cascade_borders_matched=False,
        scene_fidelity_qualified=False,full_import=False,complete_assets_delivered_increment=0,editor_visual_verified=False,
        input_pins={str(p):sha(p) for p in files},next_action='Port and verify the source PCF tent 7x7 sampling kernel and source bias semantics, then recompare the original reference pixels.')
    (ROOT/'RESULT.v1.json').write_text(json.dumps(result,indent=2),'utf-8');print(json.dumps(result))
if __name__=='__main__':main()
