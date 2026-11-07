"""Compare decoded native float bits against independently captured source UV."""
from pathlib import Path
from datetime import datetime, timezone
import hashlib, json
import numpy as np

w=Path('F:/OpenRPG_ImportWork')
root=w/'townsmith-depth-bound-uv-comparison-v27'
assert not root.exists()
root.mkdir()
pins={}
def read(p): return json.loads(p.read_text(encoding='utf-8-sig'))
def sha(p):
    with p.open('rb') as stream: return hashlib.file_digest(stream,'sha256').hexdigest()
def pin(p): pins[str(p)]=sha(p)
def reopen(p):
    proof=read(p)
    for name in ['input_pins','private_before_pins']:
        for key,value in proof.get(name,{}).items():
            assert sha(Path(key))==value,key
            pins[key]=value
    pin(p)
    return proof
result=dict(full_import=False,source_material_fidelity_qualified=False,editor_visual_verified=False)
try:
    batch=w/'townsmith-native-uv-batch-v26/BATCH.v1.json'
    assert read(batch)['status']=='six_native_uv_channels_drawn_with_stable_readbacks'
    pin(batch)
    reopen(w/'townsmith-native-uv-carrier-v26/RESULT.v1.json')
    reopen(w/'townsmith-door-source-depth-bound-uv-reopen-v27/RESULT.v1.json')
    source=w/'townsmith-door-source-depth-bound-uv-v27'
    # Same axis normalization established independently with actual source/native depth.
    reopen(w/'townsmith-door-native-depth-comparison-v6/RESULT.v1.json')
    depth=w/'townsmith-paint-door-native-depth-draw-v6/FORGE-DEPTH.r32f'
    mask=np.fromfile(depth,dtype='<f4').reshape(512,512)>0
    pin(depth)
    source_depth=source/'door-nopost-linear.depth-3.r32f'
    assert np.array_equal(mask,np.fromfile(source_depth,dtype='<f4').reshape(512,512)[::-1]>0)
    pin(source_depth)
    uvpath=source/'door-nopost-linear.uv-control-6.rgba32f'
    dpath=source/'door-nopost-linear.uv-control-7.rgba32f'
    record=read(source/'door-nopost-linear.uv-control-6.json')
    assert record['actual_opaque_depth_bound']
    uv=np.fromfile(uvpath,dtype='<f4').reshape(record['height'],record['width'],4)[:512,:512][::-1]
    derivatives=np.fromfile(dpath,dtype='<f4').reshape(record['height'],record['width'],4)[:512,:512][::-1]
    assert np.array_equal(uv[:,:,3]==1,mask)
    pin(uvpath);pin(dpath)
    expected=[uv[:,:,0],uv[:,:,1],derivatives[:,:,0],derivatives[:,:,1],-derivatives[:,:,2],-derivatives[:,:,3]]
    names=['u','v','du_dx','dv_dx','du_dy','dv_dy']
    rows=[]
    for channel,name in enumerate(names):
        work=w/f'townsmith-native-uv-channel-{channel}-v26'
        run=read(work/'RUN-RESULT.v1.json')
        assert run['actual_exit_code']==0 and run['gpu_draw_executed']
        reopen(work/'PREPARED.v1.json')
        native=read(work/'NATIVE-RESULT.v1.json')
        assert native['diagnostic_channel']==channel and native['consecutive_equal_readbacks']>=3
        assert native['diagnostic_resource_flags_describe_installed_resources_only']
        image=work/'FORGE-LINEAR.rgba16f'
        assert sha(image)==run['forge_image_sha256']
        values=np.fromfile(image,dtype='<f2').astype(np.float64).reshape(512,512,4)
        assert np.isfinite(values).all() and np.all((values>=0)&(values<=1))
        code_error=float(np.abs(values*255-np.rint(values*255)).max())
        assert code_error<=255/2048 and code_error<0.5
        codes=np.rint(values*255).astype(np.uint32)
        bits=codes[:,:,0]|(codes[:,:,1]<<8)|(codes[:,:,2]<<16)|(codes[:,:,3]<<24)
        decoded=np.ascontiguousarray(bits.astype('<u4')).view('<f4')
        assert np.isfinite(decoded).all()
        error=np.abs(decoded[mask].astype(np.float64)-expected[channel][mask].astype(np.float64))
        raw_out=root/f'{name}.native.r32f'
        decoded.tofile(raw_out)
        rows.append(dict(channel=channel,name=name,covered_pixels=int(mask.sum()),
                         exact_value_pixels=int(np.count_nonzero(decoded[mask]==expected[channel][mask])),
                         mean_absolute_error=float(error.mean()),max_absolute_error=float(error.max()),
                         p99_absolute_error=float(np.quantile(error,.99)),
                         byte_transport_max_code_error=code_error,
                         source_derivative_axis_factor=-1 if channel>=4 else 1))
        for p in [work/'RUN-RESULT.v1.json',work/'NATIVE-RESULT.v1.json',image,raw_out]: pin(p)
    pin(Path(__file__))
    result.update(status='native_uv_and_derivatives_compared_to_source',rows=rows,input_pins=pins,
                  checked_pins=len(pins),integer_byte_transport_unambiguous=True,
                  comparison_orientation='Source rows reversed to native axes, independently established by actual depth. Source vertical derivatives negate with row-axis reversal.',
                  scope='Diagnostic of original isolated LOD0 submesh0 with actual source opaque depth selection; equality is reported per channel, not presumed. Material, full prefab and Studio remain open.')
except Exception as exc:
    result.update(status='native_uv_comparison_refused',error=repr(exc),traceback=__import__('traceback').format_exc())
result['finished_utc']=datetime.now(timezone.utc).isoformat()
(root/'RESULT.v1.json').write_text(json.dumps(result,indent=2))
print(json.dumps({k:v for k,v in result.items() if k!='input_pins'}))
if result['status'].endswith('refused'): raise SystemExit(1)
t=w/'SUIVI_LOTS_HIVEMIND'
summary='; '.join(f"{row['name']} écart moyen {row['mean_absolute_error']:.9g}, maximal {row['max_absolute_error']:.9g}" for row in rows)
message=f"\n- {result['finished_utc']} — **L03.TOWNSMITH.MATERIAUX.UV.COMPARAISON.PROFONDEUR vérifiée** : six canaux natifs, transport des bits non ambigu, {len(pins)} épingles contrôlées. {summary}. Fidélité matériau et Studio ouverts ; aucun nouvel asset complet qualifié. [Preuve](../townsmith-depth-bound-uv-comparison-v27/RESULT.v1.json).\n"
for name in ['JOURNAL.md','AVANCEMENT_ACTUEL.md','SUIVI_LISIBLE.md']:
    with (t/name).open('a',encoding='utf-8') as stream: stream.write(message)
p=t/'SUIVI_SIMPLE.v1.json'
state=read(p)
state.update(updated_utc=result['finished_utc'],state=result['status'],
             last_technical_batch='L03.TOWNSMITH.MATERIAUX.UV.COMPARAISON.PROFONDEUR',last_technical_proof=str(root/'RESULT.v1.json'),
             current_work_receipt=str(batch),next_action='Use measured UV and derivative differences to resolve actual material sampling fidelity; then prefab and Studio.')
tmp=p.with_suffix('.uv26.tmp');tmp.write_text(json.dumps(state,ensure_ascii=False,indent=2),encoding='utf-8');tmp.replace(p)
