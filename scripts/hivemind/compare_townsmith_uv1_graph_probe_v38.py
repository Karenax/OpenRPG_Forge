"""Compare the actual generated source graph to the native graph before TBN."""
from pathlib import Path
from datetime import datetime,timezone
import json,hashlib
import numpy as np
w=Path('F:/OpenRPG_ImportWork');source=w/'townsmith-door-source-graph-probe-v34';out=w/'townsmith-uv1-graph-intermediate-comparison-v38'
assert not out.exists();out.mkdir()
def read(p):return json.loads(p.read_text(encoding='utf-8-sig'))
def sha(p):
    with p.open('rb') as stream:return hashlib.file_digest(stream,'sha256').hexdigest()
pins={}
def pin(p):pins[str(p)]=sha(p)
def reopen(p):
    d=read(p)
    for field in ['input_pins','private_before_pins']:
        for key,value in d.get(field,{}).items():
            assert sha(Path(key))==value,key
            pins[key]=value
    pin(p);return d
result=dict(full_import=False,material_fidelity_qualified=False,forge_editor_verified=False,complete_assets_delivered_increment=0)
try:
    run=read(source/'RUN-RESULT.v1.json')
    assert run['actual_exit_code']==0 and run['neutral_reference_byte_equal'] and run['source_inputs_unchanged']
    reopen(source/'PREPARED.v1.json');pin(source/'RUN-RESULT.v1.json');pin(source/'NATIVE-RESULT.v1.json')
    batch=w/'townsmith-native-graph-probe-batch-v38/BATCH.v1.json'
    assert read(batch)['status']=='three_native_graph_probe_channels_drawn_with_stable_readbacks';pin(batch)
    reopen(w/'townsmith-native-graph-probe-carrier-v38/RESULT.v1.json')
    reopen(w/'townsmith-door-source-inpass-normal-reopen-v31/RESULT.v1.json')
    depth=source/'door-nopost-linear.depth-3.r32f';mask=np.fromfile(depth,dtype='<f4').reshape(512,512)>0
    assert int(mask.sum())==34445;pin(depth)
    rows=[]
    for channel,name in enumerate(['normal_ts','blend1','blend2']):
        prior=source/f'door-nopost-linear.graph-probe-{6+channel}.rgba32f'
        raw=source/f'door-nopost-linear.graph-probe-{9+channel}.rgba32f'
        meta=source/f'door-nopost-linear.graph-probe-{9+channel}.json'
        assert sha(prior)==sha(raw)
        m=read(meta)
        assert m['actual_opaque_depth_bound'] and m['mode']==channel and m['format']=='R32G32B32A32_SFloat'
        a=np.fromfile(raw,dtype='<f4').reshape(m['height'],m['width'],4)[:512,:512]
        assert np.isfinite(a).all()
        if channel==0:assert np.array_equal(a[:,:,3]==1,mask)
        work=w/f'townsmith-native-graph-probe-channel-{channel}-v38'
        run=read(work/'RUN-RESULT.v1.json');assert run['actual_exit_code']==0 and run['gpu_draw_executed']
        reopen(work/'PREPARED.v1.json')
        receipt=read(work/'NATIVE-RESULT.v1.json')
        assert receipt['diagnostic_channel']==channel and receipt['consecutive_equal_readbacks']>=3
        image=work/'FORGE-LINEAR.rgba16f';assert sha(image)==run['forge_image_sha256']
        b=np.fromfile(image,dtype='<f2').astype(np.float32).reshape(512,512,4)
        assert np.isfinite(b).all()
        if channel==0:assert np.array_equal(b[:,:,3]==1,mask[::-1])
        a=a[::-1][mask[::-1]].astype(np.float64);b=b[mask[::-1]].astype(np.float64)
        error=np.abs(a-b)
        row=dict(name=name,covered_pixels=34445,rgba_mean_absolute_error=error.mean(axis=0).tolist(),rgba_max_absolute_error=error.max(axis=0).tolist(),
                 rgba_channels_outside_unit_interval_half_ulp=(error>1/2048).sum(axis=0).tolist(),source_last_two_full_buffers_byte_equal=True,
                 source_min=a.min(axis=0).tolist(),source_max=a.max(axis=0).tolist(),native_min=b.min(axis=0).tolist(),native_max=b.max(axis=0).tolist())
        if channel==0:
            assert np.all(np.linalg.norm(a[:,:3],axis=1)>0) and np.all(np.linalg.norm(b[:,:3],axis=1)>0)
            sa=a[:,:3]/np.linalg.norm(a[:,:3],axis=1)[:,None];nb=b[:,:3]/np.linalg.norm(b[:,:3],axis=1)[:,None]
            angles=np.degrees(np.arccos(np.clip((sa*nb).sum(axis=1),-1,1)))
            row.update(mean_normalized_angle_degrees=float(angles.mean()),max_normalized_angle_degrees=float(angles.max()))
        rows.append(row)
        for p in [prior,raw,meta,image,work/'RUN-RESULT.v1.json',work/'NATIVE-RESULT.v1.json']:pin(p)
    pin(Path(__file__))
    result.update(status='generated_source_and_native_graph_intermediates_compared',rows=rows,input_pins=pins,rehashed_pins=len(pins),
                  scope='Exact generated META graph blocks and copied original material properties in private source shader, before HDRP TBN/decal transform. Native typed graph intermediates before TBN. Diagnostic extraction is not original complete shader parity.')
except Exception as exc:result.update(status='graph_intermediate_comparison_refused',error=repr(exc),traceback=__import__('traceback').format_exc())
result['finished_utc']=datetime.now(timezone.utc).isoformat();(out/'RESULT.v1.json').write_text(json.dumps(result,indent=2),encoding='utf-8')
print(json.dumps({k:v for k,v in result.items() if k!='input_pins'}))
if result['status'].endswith('refused'):raise SystemExit(1)
t=w/'SUIVI_LOTS_HIVEMIND'
message=f"\n- {result['finished_utc']} — **L03.TOWNSMITH.MATERIAUX.UV1.GRAPHE.INTERMEDIAIRES comparé** : vraie fonction générée source, deux poids de mélange et normale tangentielle ; trois dessins natifs stables, {len(pins)} épingles contrôlées. Angle tangent moyen {rows[0]['mean_normalized_angle_degrees']:.6f}°, maximal {rows[0]['max_normalized_angle_degrees']:.6f}°. Fidélité complète et Studio ouverts. [Preuve](../townsmith-uv1-graph-intermediate-comparison-v38/RESULT.v1.json). Aucun nouvel asset complet qualifié.\n"
for name in ['JOURNAL.md','AVANCEMENT_ACTUEL.md','SUIVI_LISIBLE.md']:
    with (t/name).open('a',encoding='utf-8') as stream:stream.write(message)
p=t/'SUIVI_SIMPLE.v1.json';data=read(p)
data.update(updated_utc=result['finished_utc'],state=result['status'],last_technical_batch='L03.TOWNSMITH.MATERIAUX.UV1.GRAPHE.INTERMEDIAIRES',last_technical_proof=str(out/'RESULT.v1.json'),current_work_receipt=str(batch),next_action='Inspect remaining sampling and scene-lighting errors, then prefab and Studio qualification; preserve original source graph.')
tmp=p.with_suffix('.graph33.tmp');tmp.write_text(json.dumps(data,ensure_ascii=False,indent=2),encoding='utf-8');tmp.replace(p)
