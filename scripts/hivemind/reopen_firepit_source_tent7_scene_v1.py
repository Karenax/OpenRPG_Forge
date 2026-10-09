"""Verify source projection transport into receiver GPU and caster view fields."""
from pathlib import Path
import datetime,hashlib,json,struct
W=Path('F:/OpenRPG_ImportWork');ROOT=W/'firepit-source-tent7-scene-reopen-v1'
def read(p):return json.loads(p.read_text('utf-8-sig'))
def sha(p):
    with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def packed(values):return struct.pack('<'+'f'*len(values),*values)
def main():
    assert not ROOT.exists()
    cap=W/'firepit-source-tent7-scene-native-v6';run=read(cap/'RUN-RESULT.v1.json');assert run['actual_exit_code']==0
    for p,h in run['input_pins'].items():assert sha(p)==h,p
    case_path=W/'firepit-source-tent7-scene-case-v5/CASE.v1.json';case=read(case_path)
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
    comparison_path=W/'firepit-source-tent7-scene-comparison-v5/RESULT.v1.json';comparison=read(comparison_path);files.append(comparison_path)
    # Reopen both GLBs independently; no geometry or original binary prefix may change.
    original_protocol=read(W/'firepit-source-shadow-projection-case-v1/RESULT.v1.json')
    original=Path(original_protocol['private_fixture']);derived=Path(case['fixture'])
    assert sha(derived)==case['fixture_sha256']
    def glb(path):
        data=path.read_bytes();length=struct.unpack_from('<I',data,12)[0]
        return json.loads(data[20:20+length]),data[28+length:]
    original_doc,original_binary=glb(original);derived_doc,derived_binary=glb(derived)
    assert derived_binary[:len(original_binary)]==original_binary
    for field in ['nodes','meshes','accessors','scenes','scene']:
        assert original_doc[field]==derived_doc[field],field
    assert derived_doc['bufferViews'][:len(original_doc['bufferViews'])]==original_doc['bufferViews']
    for old,new in zip(original_doc['materials'],derived_doc['materials']):
        a=old['extensions']['OPENRPG_source_graph'];b=new['extensions']['OPENRPG_source_graph']
        assert a['prepass']==b['prepass'] and a['textures']==b['textures']
    files.extend([original,derived,W/'firepit-source-shadow-projection-case-v1/RESULT.v1.json'])
    ROOT.mkdir()
    result=dict(status='source_tent7_filter_applied_source_projection_and_geometry_verified_fidelity_refused',
        finished_utc=datetime.datetime.now(datetime.timezone.utc).isoformat(),views_verified=2,
        receiver_gpu_scalar_bits_verified=scalar_bits,caster_extracted_field_bits_verified=caster_fields,
        caster_view_uniform_gpu_readback_verified=False,native_source_pins_verified=len(run['input_pins']),
        shadow_mask_iou=comparison['shadow_mask_iou'],comparison_status=comparison['status'],
        source_texel_convention='HDRP diagonal; native edge equals diagonal divided by sqrt(2)',
        source_filter_matched=False,source_bias_matched=False,source_cascade_borders_matched=False,source_tent7_filter_applied=True,original_geometry_and_binary_prefix_verified=True,source_receiver_bias_numeric_gpu_readback_verified=False,
        scene_fidelity_qualified=False,full_import=False,complete_assets_delivered_increment=0,editor_visual_verified=False,
        input_pins={str(p):sha(p) for p in files},next_action='Qualify source receiver and raster bias, atlas/cascade boundaries and GPU presentation; retain original thresholds.')
    (ROOT/'RESULT.v1.json').write_text(json.dumps(result,indent=2),'utf-8');print(json.dumps(result))
if __name__=='__main__':main()
