"""Reopen the live-CBuffer SDK witness; original scene parity remains unproven."""
from pathlib import Path
from datetime import datetime,timezone
import hashlib,json,math,struct
W=Path('F:/OpenRPG_ImportWork');CAP=W/'firepit-source-specular-sdk-v2';ROOT=W/'firepit-source-specular-sdk-reopen-v2'
def read(p):return json.loads(p.read_text('utf-8-sig'))
def sha(p):
    with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def main():
    assert not ROOT.exists()
    run=read(CAP/'RUN-RESULT.v1.json');prep=read(CAP/'PREPARED.v1.json');sdk=read(CAP/'SDK-PREPARED.v1.json');protocol=read(CAP/'SDK-CASES.v1.json')
    assert run['actual_exit_code']==0 and run['source_inputs_unchanged'] and not run['private_changed']
    assert protocol['output_vectors_per_case']==9 and len(protocol['cases'])==756
    for pins in [prep['input_pins'],prep['private_before_pins'],sdk['input_pins'],run['sdk_files']]:
        for p,h in pins.items():assert sha(p)==h,p
    native=read(CAP/'NATIVE-RESULT.v1.json');old=read(W/'firepit-shadow-source-reference-v8/NATIVE-RESULT.v1.json')
    assert [i['raw_sha256'] for i in native['images']]==[i['raw_sha256'] for i in old['images']]
    a=CAP/'source-specular-sdk-0.rgba32f';b=CAP/'source-specular-sdk-1.rgba32f';assert a.read_bytes()==b.read_bytes()
    metadata=[]
    for view in range(2):
        m=read(CAP/f'source-specular-sdk-{view}.rgba32f.json');metadata.append(m)
        assert m['cases']==756 and m['vectors_per_case']==9 and m['bytes']==756*9*16
        assert m['case_storage_format']==m['actual_normal_buffer_format']=='R8G8B8A8_UNorm'
        assert m['source_history_channel']=='CustomUserHistory0' and m['source_parameters_from_live_pipeline_cbuffer']
        assert m['source_specular_occlusion_blend']==1.0 and m['source_specular_fade']==0.0 and not m['preexposure_applied']
    values=list(struct.iter_unpack('<4f',a.read_bytes()));assert len(values)==756*9
    assert all(math.isfinite(v) for row in values for v in row)
    rows=[];maximum_product_relative_error=0.0
    for index,case in enumerate(protocol['cases']):
        block=values[index*9:(index+1)*9];decoded_r=block[1][3]
        wanted_r=round(min(1,max(0,case['authored_perceptual_roughness']))*255)/255
        assert abs(decoded_r-wanted_r)<=3e-7,(index,decoded_r,wanted_r)
        assert abs(sum(v*v for v in block[1][:3])-1)<=3e-6
        assert all(0<=v<=1 for v in block[2]) and block[4][3]>0 and block[7][3]==1.0
        assert abs(block[3][3]-(decoded_r*(1.7-0.7*decoded_r))*6)<=4e-6
        for channel in range(3):
            wanted=block[4][channel]*block[7][channel]*block[5][channel]*block[6][channel]
            error=abs(wanted-block[8][channel])/max(1.0,abs(wanted));maximum_product_relative_error=max(maximum_product_relative_error,error)
            assert error<=4e-6,(index,channel,error)
        rows.append(dict(**case,source_gpu={name:list(v) for name,v in zip(protocol['output_fields'],block,strict=True)}))
    paths=[Path(__file__).resolve(),CAP/'RUN-RESULT.v1.json',CAP/'PREPARED.v1.json',CAP/'SDK-PREPARED.v1.json',CAP/'SDK-CASES.v1.json',CAP/'NATIVE-RESULT.v1.json',a,b,*[CAP/f'source-specular-sdk-{v}.rgba32f.json' for v in range(2)]]
    pins={str(p):sha(p) for p in paths};pins.update(sdk['input_pins'])
    receipt=dict(status='native_source_deferred_specular_sdk_756_cases_live_context_verified',
        verified_utc=datetime.now(timezone.utc).isoformat(),cases_verified=756,observations_verified=1512,
        vectors_verified=13608,channels_verified=54432,source_dependencies_verified=len(prep['dependency_paths']),
        original_source_images_unchanged=True,actual_normal_buffer_format='R8G8B8A8_UNorm',
        source_specular_occlusion_blend=1.0,source_specular_fade=0.0,
        source_parameters_from_live_pipeline_cbuffer=True,first_global_scalar_capture_not_qualified=True,
        authored_values_preserved=True,hardware_lighting_roughness_projection_verified=True,
        maximum_product_relative_error=maximum_product_relative_error,rows=rows,input_pins=pins,
        target_specular_kernel_qualified=False,original_scene_specular_applied=False,
        render_fidelity_qualified=False,full_import=False,editor_visual_verified=False,
        next_action='Port exact Deferred normal/F0/specular-AO storage and sky BRDF/energy formulas; verify target GPU, then apply to the original scene without editing authored values.')
    ROOT.mkdir();(ROOT/'RESULT.v1.json').write_text(json.dumps(receipt,indent=2),'utf-8')
    print(json.dumps({k:v for k,v in receipt.items() if k not in ['rows','input_pins']}))
if __name__=='__main__':main()
