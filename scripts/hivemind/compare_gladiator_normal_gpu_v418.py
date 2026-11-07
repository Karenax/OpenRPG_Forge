"""Compare real GPU buffers with Unity without raising the existing tolerances."""
from pathlib import Path
from datetime import datetime,timezone
import hashlib,json,math,struct
W=Path('F:/OpenRPG_ImportWork');G=W/'gladiator-normal-gpu-v417';U=W/'gladiator-normal-reference-v415';O=W/'gladiator-normal-gpu-comparison-v418'
def read(p):return json.loads(Path(p).read_text('utf-8-sig'))
def sha(p):
    with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
pins={}
def values(p,h=None):
    p=Path(p);digest=sha(p)
    if h is not None:assert digest==h,p
    pins[str(p)]=digest;data=p.read_bytes();assert len(data)==65536
    result=struct.unpack('<16384f',data);assert all(math.isfinite(v) for v in result);return result
def errors(a,b,flip):
    assert len(a)==len(b)==16384
    if flip:b=tuple(v for y in range(63,-1,-1) for v in b[y*256:(y+1)*256])
    return [max(abs(x-y) for x,y in zip(a[c::4],b[c::4])) for c in range(4)]
def passing(e):return max(e[:2])<=1e-6 and e[2]<=5e-4 and e[3]<=1e-6
assert not O.exists()
run=read(G/'RUN-RESULT.v1.json');assert run['build_exit_code']==run['raw_exit_code']==run['decoded_exit_code']==0 and 'finished_utc' in run
assert sha(G/'gpu-probe.immutable.v1.exe')==run['executable_sha256']
for p,h in read(G/'PREPARED.v1.json')['input_pins'].items():assert sha(p)==h
reference=read(U/'GPU-REFERENCE-REOPEN.v1.json');manifest=read(U/'manifest.json');source=read(U/'export/RESULT.json')
assert reference['verified_textures']==source['matched']==4
raw=read(G/'raw/RESULT.v1.json');decoded=read(G/'decoded/RESULT.v1.json');inp=read(G/'INPUT.v1.json')
for j in [raw,decoded]:
    assert j['gpu_sampled_textures']==4 and j['sampled_mip_levels']==52 and j['backend']=='Dx12'
    assert j['input_sha256']==sha(G/'INPUT.v1.json')
    assert len(j['rows'])==4
rawrows={r['unity_guid']:r for r in raw['rows']};decodedrows={r['unity_guid']:r for r in decoded['rows']}
expected={r['unity_guid']:r for r in inp['rows']};assert set(rawrows)==set(decodedrows)==set(expected)=={r['expected_guid'] for r in manifest['cases']}
# Retain controls for orientation and altered channels, using actual grid geometry.
fixture=tuple(float(i%251)/251 for i in range(16384))
assert errors(fixture,fixture,False)==[0]*4 and max(errors(fixture,fixture,True))>1e-6
for channel,amount in [(0,.01),(2,.001),(3,.01)]:
    changed=list(fixture);changed[channel]+=amount;assert not passing(errors(fixture,changed,False))
rows=[];common={'identity','flip_y'};levels=0
for ordinal,(request,sr,proof) in enumerate(zip(manifest['cases'],source['cases'],reference['rows'])):
    guid=request['expected_guid'];rr=rawrows[guid];dr=decodedrows[guid];er=expected[guid]
    assert guid==sr['actual_guid'] and request['asset_id']==proof['asset_id']
    for target in [rr,dr]:
        assert target['storage_sha256']==er['storage_sha256'] and target['gpu_format']=='Bc3RgbaUnorm'
        assert target['unity_file_id']==request['expected_file_id']==sr['actual_file_id']=='2800000'
        assert target['sampler_from_canonical_bevy_image'] and len(target['mips'])==13
    mappings={name:dict(raw=[0.]*4,decoded=[0.]*4,builtin=[0.]*4) for name in ['identity','flip_y']}
    for level,(r,d) in enumerate(zip(rr['mips'],dr['mips'])):
        assert r['level']==d['level']==level and r['grid']==d['grid']==64
        assert Path(r['rgba_f32']).resolve().is_relative_to((G/'raw').resolve()) and Path(d['rgba_f32']).resolve().is_relative_to((G/'decoded').resolve())
        rv=values(r['rgba_f32'],r['sha256']);dv=values(d['rgba_f32'],d['sha256'])
        unity={}
        for lane in ['rgba','float-rgorag','unity-builtin-unpack']:
            p=U/'export'/f'{ordinal:03}'/f'mip-{level}-{lane}.f32';unity[lane]=values(p,proof['raw_rgba_and_both_decode_buffers_sha256'][str(p)])
        for name,m in mappings.items():
            for target,a,b in [('raw',unity['rgba'],rv),('decoded',unity['float-rgorag'],dv),('builtin',unity['unity-builtin-unpack'],dv)]:
                e=errors(a,b,name=='flip_y');m[target]=[max(x,y) for x,y in zip(m[target],e)]
        levels+=1
    measured={name for name,m in mappings.items() if max(m['raw'])<=1e-6};common&=measured
    rows.append(dict(guid=guid,mappings=mappings,raw_passing_mappings=sorted(measured)))
assert levels==52
mapping=next(iter(common)) if len(common)==1 else None
rawpass=mapping is not None;decodepass=rawpass and all(passing(r['mappings'][mapping]['decoded']) for r in rows)
for p in [G/'RUN-RESULT.v1.json',G/'INPUT.v1.json',G/'PREPARED.v1.json',G/'gpu-probe.immutable.v1.exe',G/'raw/RESULT.v1.json',G/'decoded/RESULT.v1.json',U/'GPU-REFERENCE-REOPEN.v1.json',U/'export/RESULT.json',U/'manifest.json',Path(__file__)]:pins[str(p)]=sha(p)
result=dict(status='four_normal_gpu_sampling_and_decoder_comparisons_passed' if decodepass else 'gpu_sampling_or_decoder_difference_retained',updated_utc=datetime.now(timezone.utc).isoformat(),rows=rows,common_measured_coordinate_mappings=sorted(common),normal_texture_raw_sampling_verified=rawpass,candidate_mode2_matches_float_rgorag_gpu_reference=decodepass,all_match_builtin_unpack_reference=rawpass and all(passing(r['mappings'][mapping]['builtin']) for r in rows),normal_textures=4,mips=52,sample_coordinates=212992,tolerances=dict(raw=1e-6,xy=1e-6,z=5e-4,alpha=1e-6),input_pins=pins,original_shadergraph_render_parity_qualified=False,all_texture_pixels_sampled=False,material_gpu_qualified=False,full_import=False)
O.mkdir();(O/'RESULT.v1.json').write_text(json.dumps(result,indent=2),'utf-8')
print(json.dumps({k:v for k,v in result.items() if k not in {'rows','input_pins'}}))
assert decodepass,'Inspect retained per-channel differences; do not raise thresholds.'
