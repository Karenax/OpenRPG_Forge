"""Verify actual native sky samples against identical captured cube storage."""
from pathlib import Path
from datetime import datetime,timezone
import hashlib,json,math,struct
W=Path('F:/OpenRPG_ImportWork');CAP=W/'firepit-source-sky-sampling-v3';ROOT=W/'firepit-source-sky-sampling-reopen-v3'
def read(p):return json.loads(p.read_text('utf-8-sig'))
def sha(p):
    with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def main():
    assert not ROOT.exists()
    run=read(CAP/'RUN-RESULT.v1.json');prep=read(CAP/'PREPARED.v1.json');protocol=read(CAP/'SAMPLING-PROTOCOL.v1.json')
    assert run['actual_exit_code']==0 and run['source_inputs_unchanged'] and not run['private_changed']
    for pins in [prep['input_pins'],prep['private_before_pins'],run['sampling_files']]:
        for p,h in pins.items():assert sha(p)==h,p
    compute=read(CAP/'COMPUTE-PREPARED.v1.json');assert sha(compute['path'])==compute['sha256'] and sha(compute['sdk'])==compute['sdk_sha256']
    assert protocol['expected_cases']==182 and len(protocol['source_directions'])==26 and len(protocol['lods'])==7
    native=read(CAP/'NATIVE-RESULT.v1.json');original=read(W/'firepit-shadow-source-reference-v8/NATIVE-RESULT.v1.json')
    assert [v['raw_sha256'] for v in native['images']]==[v['raw_sha256'] for v in original['images']]
    storage=read(W/'firepit-source-sky-reopen-v1/RESULT.v1.json');files=[]
    for row in storage['rows']:
        for view in range(2):
            p=CAP/f'source-sky-{view}-mip-{row["mip"]}.rgba32f';assert sha(p)==row['rgba32f_sha256'];files.append(p)
    raw0=CAP/'source-sky-samples-0.rgba32f';raw1=CAP/'source-sky-samples-1.rgba32f'
    assert raw0.read_bytes()==raw1.read_bytes()
    for view in range(2):
        meta=read(CAP/f'source-sky-samples-{view}.rgba32f.json');assert meta['cases']==182 and meta['bytes']==182*16
        assert meta['gpu_native_cube_sampling'] and meta['source_history_channel']=='CustomUserHistory0'
        assert meta['sampler']=='s_trilinear_clamp_sampler' and meta['source_filter']=='Trilinear'
        files.extend([CAP/f'source-sky-samples-{view}.rgba32f',CAP/f'source-sky-samples-{view}.rgba32f.json'])
    values=list(struct.iter_unpack('<4f',raw0.read_bytes()));assert len(values)==182
    assert all(all(math.isfinite(v) for v in row) and all(v>0 for v in row[:3]) for row in values)
    rows=[];i=0
    for direction in protocol['source_directions']:
        for lod in protocol['lods']:
            rows.append(dict(case_id=f'sky-sample-{i:04d}',source_direction=direction,source_lod=lod,source_gpu_rgba=list(values[i])));i+=1
    pins=[Path(__file__).resolve(),CAP/'RUN-RESULT.v1.json',CAP/'PREPARED.v1.json',CAP/'SAMPLING-PROTOCOL.v1.json',CAP/'COMPUTE-PREPARED.v1.json',CAP/'NATIVE-RESULT.v1.json',CAP/'Unity.log',Path(compute['path']),Path(compute['sdk']),W/'firepit-source-sky-reopen-v1/RESULT.v1.json',*files]
    result=dict(status='native_source_sky_182_cases_two_views_and_storage_verified',finished_utc=datetime.now(timezone.utc).isoformat(),
        sampling_cases_verified=182,sampling_observations_verified=364,captured_cube_mips_identical_to_verified_reference=True,
        original_source_images_unchanged=True,source_dependencies_verified=len(prep['dependency_paths']),rows=rows,
        target_cube_sampling_qualified=False,render_fidelity_qualified=False,editor_visual_verified=False,full_import=False,
        input_pins={str(p):sha(p) for p in pins},next_action='Admit captured half-float cube storage once, then compare target GPU axes/seams/corners and fractional LODs against these source samples.')
    ROOT.mkdir();(ROOT/'RESULT.v1.json').write_text(json.dumps(result,indent=2),'utf-8')
    print(json.dumps({k:v for k,v in result.items() if k not in ['input_pins','rows']}))
if __name__=='__main__':main()
