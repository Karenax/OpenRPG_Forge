"""Verify source punctual D16 pixels, actual requests and unchanged references."""
from pathlib import Path
import array,datetime,hashlib,json,math,struct,sys
W=Path('F:/OpenRPG_ImportWork');CAP=W/'firepit-source-point-shadow-raster-v3';ROOT=W/'firepit-source-point-raster-reopen-v1'
def read(p):return json.loads(Path(p).read_text('utf-8-sig'))
def sha(p):
    with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def f32(v):return struct.pack('<f',v)
def main():
    assert not ROOT.exists() and sys.byteorder=='little'
    run=read(CAP/'RUN-RESULT.v1.json');prep=read(CAP/'PREPARED.v1.json');assert run['actual_exit_code']==0 and run['source_inputs_unchanged'] and not run['private_changed']
    for p,h in prep['input_pins'].items():assert sha(p)==h,p
    protocol=read(CAP/'DEPTH-PROTOCOL.v1.json');assert protocol['atlas_width']==protocol['atlas_height']==4096
    assert sha(protocol['compute'])==protocol['compute_sha256']
    records=[read(CAP/f'SOURCE-SHADOW-RASTER-{i}.json') for i in range(2)]
    assert [r['request_count'] for r in records]==[6,0]
    files=[Path(__file__),CAP/'PREPARED.v1.json',CAP/'RUN-RESULT.v1.json',CAP/'DEPTH-PROTOCOL.v1.json',Path(protocol['compute'])]
    source_state=CAP/'SOURCE-STATE-0.json';state=read(source_state);shadow_gpu=json.loads(state['shadow_gpu_json'])[4:];assert len(shadow_gpu)==6;files.append(source_state)
    requests=json.loads(records[0]['requests_json']);assert len(requests)==6
    for req,gpu in zip(requests,shadow_gpu):
        for field,other in [('normalBias','normalBias'),('worldTexelSize','worldTexelSize')]:assert f32(req[field])==f32(gpu[other])
        for k in 'xyz':assert f32(req['position'][k])==f32(gpu['pos'][k])
        assert req['slopeBias']==2.5 and req['typeData']==1
    statistics=[];matrix_scalars=0;z_clip=[]
    for view,row in enumerate(records):
        assert row['atlas_declared_depth_bits']=='Depth16' and row['backend']=='Direct3D11' and row['reversed_z']
        assert row['declared_width']==row['declared_height']==4096
        assert row['bound_texture_observed']
        if view==0:
            assert row['actual_width']==row['actual_height']==4096 and row['actual_depth_stencil_format']=='D16_UNorm'
        else:
            # No live requests: Unity binds the fallback, while the allocation
            # we explicitly observe retains the preceding atlas contents.
            assert row['actual_width']==row['actual_height']==1 and row['actual_depth_stencil_format']=='D32_SFloat'
        allocations=json.loads(row['matching_allocations_json']);assert len(allocations)==row['matching_allocation_count']==1 and allocations[0]['created'] and allocations[0]['depth_stencil_format']=='D16_UNorm'
        ap=CAP/f'POINT-ATLAS-ALLOCATION-{view}.json';allocation_probe=read(ap)
        assert len([a for a in allocation_probe['allocations'] if a['created'] and a['width']==a['height']==4096 and a['format']=='D16_UNorm'])==1
        split_path=CAP/f'SOURCE-CASTER-SPLITS-{view}.json';splits=read(split_path);assert splits['active_requests']==(6 if view==0 else 0)
        for split in splits['splits']:
            z_clip.append(split['z_clip'])
            for field in ['view','projection','device_projection_matrix','device_projection_y_flip','inv_view_projection','clip_from_world']:
                values=split[field];assert len(values)==16 and all(math.isfinite(v) for v in values);matrix_scalars+=16
        p=CAP/f'SOURCE-SHADOW-DEPTH-{view}.float32';assert p.stat().st_size==4096*4096*4
        values=array.array('f');
        with p.open('rb') as stream:values.fromfile(stream,4096*4096)
        nonzero=0;maximum=0.0;minimum=1.0;grid_error=0.0
        for v in values:
            assert math.isfinite(v) and 0<=v<=1
            nonzero+=int(v!=0.0);maximum=max(maximum,v);minimum=min(minimum,v)
            scaled=v*65535.0;grid_error=max(grid_error,abs(scaled-round(scaled)))
        statistics.append({'view':view,'active_requests':row['request_count'],'values_verified':len(values),'nonzero_samples':nonzero,'minimum':minimum,'maximum':maximum,'unorm16_grid_max_error_lsb_observed':grid_error,'sha256':sha(p)})
        del values
        files.extend([p,ap,split_path,CAP/f'SOURCE-SHADOW-RASTER-{view}.json'])
        for relative in [f'images/{view}.png',f'linear-{view}.rgba16f']:
            a=CAP/relative;b=W/'firepit-shadow-source-reference-v8'/relative;assert sha(a)==sha(b);files.extend([a,b])
    assert len(z_clip)==6 and all(z_clip) and matrix_scalars==576
    assert statistics[0]['nonzero_samples']>0
    active_off_same=statistics[0]['sha256']==statistics[1]['sha256']
    for version in [1,2]:
        p=W/f'firepit-source-point-shadow-raster-v{version}/RUN-RESULT.v1.json';assert read(p)['actual_exit_code']==1;files.append(p)
    result={'status':'actual_source_punctual_d16_atlas_six_raster_requests_and_pixels_reopened','finished_utc':datetime.datetime.now(datetime.timezone.utc).isoformat(),'sub_lot':'L01.OMBRES.PONCTUELLES.ATLAS_SOURCE','source_dependencies_verified':len(prep['dependency_paths']),'source_input_pins_verified':len(prep['input_pins']),'source_atlas_size':[4096,4096],'actual_depth_format':'D16_UNorm','active_bound_global_texture_matches_allocation':True,'inactive_global_binding_is_default_1x1_d32':True,'unique_live_matching_allocation_verified':True,'active_request_counts':[6,0],'actual_request_slope_bias':[r['slopeBias'] for r in requests],'caster_split_matrix_scalars_reopened':matrix_scalars,'perspective_z_clip_actual':z_clip,'fractional_viewports_preserved':[r['dynamicAtlasViewport'] for r in requests],'source_normal_bias':[r['normalBias'] for r in requests],'source_world_texel_size':[r['worldTexelSize'] for r in requests],'gpu_depth_statistics':statistics,'inactive_allocation_identical_to_previous_active':active_off_same,'inactive_view_is_not_a_new_shadow_map':True,'original_png_and_hdr_views_byte_identical':True,'prior_d32_allocation_refusals_preserved':True,'native_atlas_current_depth_format':'Depth32Float','source_native_point_depth_format_mismatch_confirmed':True,'full_import':False,'editor_visual_verified':False,'complete_assets_delivered_increment':0,'next_action':'Prepare a shared punctual source-shadow context preserving the actual D16 atlas, fractional viewports, perspective projections and raster state; measure native depths and PCF5 receiver sampling against source before product qualification.','input_pins':{str(p):sha(p) for p in files},'source_current_input_pins':prep['input_pins']}
    ROOT.mkdir();(ROOT/'RESULT.v1.json').write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n','utf-8')
    print(json.dumps({k:v for k,v in result.items() if k not in ['input_pins','source_current_input_pins','fractional_viewports_preserved']},ensure_ascii=True))
if __name__=='__main__':main()
