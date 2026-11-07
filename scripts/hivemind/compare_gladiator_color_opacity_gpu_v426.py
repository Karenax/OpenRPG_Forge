"""Compare all remaining sampled texture channels; no material parity claim."""
from pathlib import Path
from datetime import datetime,timezone
import hashlib,json,math,struct
W=Path('F:/OpenRPG_ImportWork');G=W/'gladiator-color-opacity-gpu-v424';U=W/'gladiator-color-opacity-reference-v423';O=W/'gladiator-color-opacity-comparison-v426'
def read(p):return json.loads(Path(p).read_text('utf-8-sig'))
def sha(p):
    with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
pins={}
def values(p,h):
    p=Path(p);assert sha(p)==h;data=p.read_bytes();assert len(data)==65536
    pins[str(p)]=h;v=struct.unpack('<16384f',data);assert all(math.isfinite(x) for x in v);return v
def errors(a,b,flip):
    if flip:b=tuple(x for y in range(63,-1,-1) for x in b[y*256:(y+1)*256])
    return [max(abs(x-y) for x,y in zip(a[c::4],b[c::4])) for c in range(4)]
assert not O.exists()
run=read(G/'RUN-RESULT.v1.json');assert run['build_exit_code']==run['raw_exit_code']==0 and 'finished_utc' in run
assert sha(G/'gpu-probe.immutable.v1.exe')==run['executable_sha256']
for p,h in read(G/'PREPARED.v1.json')['input_pins'].items():assert sha(p)==h
ref=read(U/'GPU-REFERENCE-REOPEN.v1.json');manifest=read(U/'manifest.json');source=read(U/'export/RESULT.json');gpu=read(G/'raw/RESULT.v1.json');inp=read(G/'INPUT.v1.json')
assert ref['verified_textures']==source['matched']==gpu['gpu_sampled_textures']==8 and gpu['sampled_mip_levels']==104 and gpu['backend']=='Dx12'
assert gpu['input_sha256']==sha(G/'INPUT.v1.json')
native={r['unity_guid']:r for r in gpu['rows']};expected={r['unity_guid']:r for r in inp['rows']}
assert len(native)==8 and set(native)==set(expected)=={r['expected_guid'] for r in manifest['cases']}
rows=[];common={'identity','flip_y'};levels=0
for ordinal,(request,sr,proof) in enumerate(zip(manifest['cases'],source['cases'],ref['rows'])):
    guid=request['expected_guid'];nr=native[guid];er=expected[guid]
    assert guid==sr['actual_guid'] and request['asset_id']==proof['asset_id']
    assert nr['storage_sha256']==er['storage_sha256'] and nr['gpu_format']==er['gpu_format']
    assert nr['unity_file_id']==request['expected_file_id']==sr['actual_file_id']=='2800000'
    assert nr['sampler_from_canonical_bevy_image'] and len(nr['mips'])==13
    maxima={name:[0.]*4 for name in ['identity','flip_y']};alpha_min=1.;alpha_max=0.;below=above=0
    for level,m in enumerate(nr['mips']):
        assert m['level']==level and m['grid']==64
        path=Path(m['rgba_f32']);assert path.resolve().is_relative_to((G/'raw').resolve());nv=values(path,m['sha256'])
        sourcepath=U/'export'/f'{ordinal:03}'/f'mip-{level}-rgba.f32';sv=values(sourcepath,proof['raw_rgba_and_both_decode_buffers_sha256'][str(sourcepath)])
        for name in maxima:
            e=errors(sv,nv,name=='flip_y');maxima[name]=[max(a,b) for a,b in zip(maxima[name],e)]
        alpha_min=min(alpha_min,min(sv[3::4]));alpha_max=max(alpha_max,max(sv[3::4]));below+=sum(a<.5 for a in sv[3::4]);above+=sum(a>=.5 for a in sv[3::4]);levels+=1
    admitted={name for name,e in maxima.items() if max(e)<=1e-6};common&=admitted
    rows.append(dict(guid=guid,gpu_format=nr['gpu_format'],channel_max_errors=maxima,passing_mappings=sorted(admitted),sampled_alpha_min=alpha_min,sampled_alpha_max=alpha_max,samples_below_alpha_half=below,samples_at_or_above_alpha_half=above))
assert levels==104
passed='identity' in common
for p in [G/'RUN-RESULT.v1.json',G/'PREPARED.v1.json',G/'INPUT.v1.json',G/'gpu-probe.immutable.v1.exe',G/'raw/RESULT.v1.json',U/'GPU-REFERENCE-REOPEN.v1.json',U/'export/RESULT.json',U/'manifest.json',Path(__file__)]:pins[str(p)]=sha(p)
O.mkdir();result=dict(status='eight_color_opacity_textures_gpu_sampling_compared' if passed else 'color_opacity_gpu_difference_retained',updated_utc=datetime.now(timezone.utc).isoformat(),rows=rows,input_pins=pins,common_measured_coordinate_mappings=sorted(common),identity_mapping_passed=passed,texture_count=8,mips=104,sample_coordinates=425984,channel_tolerance=1e-6,all_texture_pixels_sampled=False,material_render_parity_qualified=False,complete_asset_conversion_increment=0,full_import=False)
(O/'RESULT.v1.json').write_text(json.dumps(result,indent=2),'utf-8')
print(json.dumps({k:v for k,v in result.items() if k not in {'rows','input_pins'}}))
assert passed,'Retain differences; do not raise tolerance.'
