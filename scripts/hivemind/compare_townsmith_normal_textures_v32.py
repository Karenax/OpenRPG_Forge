"""Reopen actual original normal texture samples and native imported texture draw."""
from pathlib import Path
from datetime import datetime,timezone
import hashlib,json,struct
import numpy as np
w=Path('F:/OpenRPG_ImportWork')
source=w/'townsmith-door-source-normal-textures-v32'
out=w/'townsmith-normal-texture-comparison-v32'
assert not out.exists();out.mkdir()
pins={}
def read(p):return json.loads(p.read_text(encoding='utf-8-sig'))
def sha(p):
    with p.open('rb') as stream:return hashlib.file_digest(stream,'sha256').hexdigest()
def reopen(p):
    d=read(p)
    for key in ['input_pins','private_before_pins']:
        for path,expected in d.get(key,{}).items():
            assert sha(Path(path))==expected,path
            pins[path]=expected
    pins[str(p)]=sha(p)
    return d
def pin(p):pins[str(p)]=sha(p)
result=dict(full_import=False,complete_assets_delivered_increment=0,material_fidelity_qualified=False,forge_editor_verified=False)
try:
    run=read(source/'RUN-RESULT.v1.json')
    assert run['actual_exit_code']==0 and run['neutral_reference_byte_equal'] and run['source_inputs_unchanged']
    reopen(source/'PREPARED.v1.json');pin(source/'RUN-RESULT.v1.json');pin(source/'NATIVE-RESULT.v1.json')
    batch=w/'townsmith-native-normal-textures-batch-v32/BATCH.v1.json'
    assert read(batch)['status']=='two_native_normal_texture_channels_drawn_with_stable_readbacks'
    pin(batch)
    carrier=w/'townsmith-native-normal-textures-carrier-v32'
    reopen(carrier/'RESULT.v1.json')
    glb=carrier/'SM_Door-Single-B.uv-control.candidate.glb'
    raw=glb.read_bytes();length=struct.unpack_from('<I',raw,12)[0]
    doc=json.loads(raw[20:20+length]);textures=doc['materials'][0]['extensions']['OPENRPG_source_graph']['textures']
    depth=source/'door-nopost-linear.depth-3.r32f'
    source_mask=np.fromfile(depth,dtype='<f4').reshape(512,512)>0
    assert int(source_mask.sum())==34445;pin(depth)
    rows=[]
    for channel in range(2):
        paths=[source/f'door-nopost-linear.normal-texture-{i}.rgba32f' for i in [4+channel,6+channel]]
        assert sha(paths[0])==sha(paths[1])
        meta=source/f'door-nopost-linear.normal-texture-{6+channel}.json'
        m=read(meta)
        assert m['actual_opaque_depth_bound'] and m['mode']==channel and m['format']=='R32G32B32A32_SFloat'
        actual=np.fromfile(paths[1],dtype='<f4').reshape(m['height'],m['width'],4)[:512,:512]
        assert np.isfinite(actual).all() and np.array_equal(np.any(actual!=0,axis=2),source_mask)
        resource=source/f'door-nopost-linear.normal-resource-{channel}.json'
        desc=read(resource)
        stored=textures[6+channel]['imported_storage']
        assert desc['width']==stored['width'] and desc['height']==stored['height']
        native_root=w/f'townsmith-native-normal-textures-channel-{channel}-v32'
        nr=read(native_root/'RUN-RESULT.v1.json')
        assert nr['actual_exit_code']==0 and nr['gpu_draw_executed']
        reopen(native_root/'PREPARED.v1.json')
        native_receipt=read(native_root/'NATIVE-RESULT.v1.json')
        assert native_receipt['diagnostic_channel']==channel and native_receipt['consecutive_equal_readbacks']>=3
        image=native_root/'FORGE-LINEAR.rgba16f'
        assert sha(image)==nr['forge_image_sha256']
        native=np.fromfile(image,dtype='<f2').astype(np.float32).reshape(512,512,4)
        assert np.isfinite(native).all()
        mask=source_mask[::-1];assert np.array_equal(np.any(native!=0,axis=2),mask)
        a=actual[::-1][mask].astype(np.float64);b=native[mask].astype(np.float64)
        error=np.abs(a-b)
        rows.append(dict(texture_index=6+channel,uv_scale=1 if channel==0 else 30,
                         source_resource=desc,native_storage_format=stored['format'],
                         covered_pixels=int(mask.sum()),rgba_mean_absolute_error=error.mean(axis=0).tolist(),
                         rgba_max_absolute_error=error.max(axis=0).tolist(),
                         half_transport_maximum_one_ulp_at_unit_interval=1/2048,
                         rgba_channels_outside_one_half_ulp=(error>1/2048).sum(axis=0).tolist(),
                         source_last_two_full_buffers_byte_equal=True))
        for p in [*paths,meta,resource,image,native_root/'RUN-RESULT.v1.json',native_root/'NATIVE-RESULT.v1.json']:pin(p)
    pin(Path(__file__))
    result.update(status='original_normal_texture_source_native_samples_compared',rows=rows,input_pins=pins,rehashed_pins=len(pins),
                  diagnostic_scope='Raw original normal textures 6/7 sampled in private depth-bound source draw; native common GLB importer draw. Does not yet prove original graph output or full material.')
except Exception as exc:result.update(status='normal_texture_comparison_refused',error=repr(exc),traceback=__import__('traceback').format_exc())
result['finished_utc']=datetime.now(timezone.utc).isoformat()
(out/'RESULT.v1.json').write_text(json.dumps(result,indent=2),encoding='utf-8')
print(json.dumps({k:v for k,v in result.items() if k!='input_pins'}))
if result['status'].endswith('refused'):raise SystemExit(1)
t=w/'SUIVI_LOTS_HIVEMIND'
message=f"\n- {result['finished_utc']} — **L03.TOWNSMITH.MATERIAUX.TEXTURES.NORMALES comparé** : deux textures originales, samples bruts UV×1 et UV×30, {len(pins)} épingles recontrôlées. Formats source et natifs et erreurs RGBA consignés ; fidélité matériau ouverte. [Preuve](../townsmith-normal-texture-comparison-v32/RESULT.v1.json). Aucun nouvel asset complet qualifié.\n"
for name in ['JOURNAL.md','AVANCEMENT_ACTUEL.md','SUIVI_LISIBLE.md']:
    with (t/name).open('a',encoding='utf-8') as stream:stream.write(message)
p=t/'SUIVI_SIMPLE.v1.json';data=read(p)
data.update(updated_utc=result['finished_utc'],last_technical_batch='L03.TOWNSMITH.MATERIAUX.TEXTURES.NORMALES',last_technical_proof=str(out/'RESULT.v1.json'),state=result['status'],next_action='Use measured source/native raw normal samples to isolate filtering/import storage from tangent-frame transformation; preserve original material and full scope.')
tmp=p.with_suffix('.normal32.tmp');tmp.write_text(json.dumps(data,ensure_ascii=False,indent=2),encoding='utf-8');tmp.replace(p)
