"""Read actual native point records and six view matrices; no fidelity upgrade."""
import base64,datetime,hashlib,json,math,struct
from pathlib import Path
W=Path('F:/OpenRPG_ImportWork');CAP=W/'firepit-point-shadow-diagnostics-native-v1';ROOT=W/'firepit-point-shadow-diagnostics-reopen-v1'
SDK=Path('C:/Users/karen/.cargo/registry/src/index.crates.io-1949cf8c6b5b557f/bevy_pbr-0.19.1/src')
def read(p):return json.loads(Path(p).read_text('utf-8-sig'))
def sha(p):return hashlib.sha256(Path(p).read_bytes()).hexdigest()
def bits(v):return struct.pack('<f',v)
def main():
    assert not ROOT.exists()
    prep=read(CAP/'PREPARED.v1.json');run=read(CAP/'RUN-RESULT.v1.json');assert run['actual_exit_code']==0
    for p,h in prep['input_pins'].items():assert sha(p)==h,p
    source_proof=W/'firepit-source-all-lights-gpu-reopen-v2/RESULT.v1.json';source=read(source_proof)
    for p,h in source['input_pins'].items():assert sha(p)==h,p
    state_path=W/'firepit-source-all-lights-gpu-v1/SOURCE-STATE-0.json';state=read(state_path)
    point=json.loads(state['punctual_light_gpu_json'])[0];shadows=json.loads(state['shadow_gpu_json'])[4:];assert len(shadows)==6
    assert base64.b64decode(state['punctual_light_gpu_base64'])[:224]
    files=[Path(__file__),source_proof,state_path,CAP/'PREPARED.v1.json',CAP/'RUN-RESULT.v1.json',SDK/'cluster/mod.rs',SDK/'render/mesh_view_types.wgsl',SDK/'render/light.rs',SDK/'render/shadows.wgsl']
    observations=[];image_equal=[];scalars=0
    for view in range(2):
        rp=CAP/f'POINT-RECORD-GPU-{view}.bin';data=rp.read_bytes();assert len(data)==80
        fs=struct.unpack('<20f',data);us=struct.unpack('<20I',data)
        assert all(math.isfinite(v) for i,v in enumerate(fs) if i not in [12,18])
        assert all(bits(fs[4+i])==bits(point['color'][k]) for i,k in enumerate('xyz'))
        assert bits(fs[7])==bits(1/(point['range']*point['range'])) and bits(fs[19])==bits(point['range'])
        frame_path=CAP/f'POINT-LIGHT-FRAME-{view}.json';frame=read(frame_path)
        assert all(bits(a)==bits(b) for a,b in zip(fs[8:11],frame['world']))
        assert us[12]&1 and us[18]==0xffffffff
        vp=CAP/f'POINT-SHADOW-VIEW-GPU-{view}.json';views=read(vp);rows=views['point_faces']
        assert len(rows)==6 and sorted(r['cascade'] for r in rows)==list(range(6))
        for row in rows:
            p=CAP/f'POINT-SHADOW-VIEW-{view}-{row["cascade"]}.float32';gpu=struct.unpack('<112f',p.read_bytes())
            assert all(math.isfinite(v) for v in gpu)
            for offset,field in [(0,'cpu_clip_from_world'),(48,'cpu_world_from_view'),(80,'cpu_clip_from_view')]:
                assert all(bits(a)==bits(b) for a,b in zip(gpu[offset:offset+16],row[field]))
                scalars+=16
            files.append(p)
        pp=CAP/f'POINT-SHADOW-PHASES-{view}.json';phases=read(pp)
        assert len(phases['point_shadow_views'])==6 and phases['clustered_entity_mapping_count_cpu']==1
        nonempty=[r['face'] for r in phases['point_shadow_views'] if sum((r[k] or 0) for k in ['multidraw_bins','batchable_bins','unbatchable_bins','non_mesh_bins'])>0]
        assert len(nonempty)==(5 if view==0 else 0)
        assert phases['shadow_texture']==[{'format':'Depth32Float','height':1024,'layers':6,'width':1024}]
        raw=CAP/f'gpu-{view}.rgba16f';old=W/'firepit-source-receiver-coordinates-native-v3'/raw.name
        image_equal.append(sha(raw)==sha(old));assert image_equal[-1]
        files.extend([rp,frame_path,vp,pp,raw,old])
        observations.append({'view':view,'gpu_record_color_bit_exact_to_source':True,'gpu_record_position_bit_exact_to_current_transform':True,'shadow_enabled_flag':bool(us[12]&1),'flags':us[12],'native_projection_custom':fs[:4],'world_depth_offset_m':fs[13],'normal_bias_scaled':fs[14],'shadow_near_z':fs[17],'range':fs[19],'nonempty_shadow_phase_faces':nonempty,'bin_counts_are_not_draw_counts':True})
    result={'status':'actual_point_gpu_record_six_views_and_phase_participation_reopened_source_shadow_mismatch_measured','finished_utc':datetime.datetime.now(datetime.timezone.utc).isoformat(),'sub_lot':'L01.OMBRES.PONCTUELLES.GPU','current_capture_input_pins_verified':len(prep['input_pins']),'point_gpu_records_reopened':2,'point_shadow_views_per_observation':6,'gpu_matrix_scalars_compared_bit_exact':scalars,'gpu_matrix_scalars_read':1344,'color_and_inverse_square_range_match_source':True,'images_byte_identical_to_prior_control':image_equal,'native_observations':observations,'native_point_texture':{'format':'Depth32Float','width':1024,'height':1024,'layers':6},'source_point_shadow_record_projection':shadows[0]['proj'],'source_shadow_map_size_declared':shadows[0]['shadowMapSize'],'source_normal_bias':shadows[0]['normalBias'],'source_world_texel_size':shadows[0]['worldTexelSize'],'source_projection_and_native_projection_not_equivalent':True,'native_world_depth_offset_absent_in_source_punctual_formula':True,'punctual_gpu_record_readback_verified':True,'punctual_gpu_count_counter_readback_verified':False,'world_depth_offset_causal_effect_not_yet_measured':True,'full_import':False,'editor_visual_verified':False,'complete_assets_delivered_increment':0,'next_action':'Measure a private shader-only control removing the native world-space depth offset; preserve native normal bias, projection/filter and original source thresholds. Then implement and qualify the complete measured source punctual policy.','input_pins':{str(p):sha(p) for p in files}}
    ROOT.mkdir();(ROOT/'RESULT.v1.json').write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n','utf-8')
    print(json.dumps({k:v for k,v in result.items() if k not in ['input_pins','native_observations']},ensure_ascii=False))
if __name__=='__main__':main()
