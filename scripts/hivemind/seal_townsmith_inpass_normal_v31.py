"""Qualify camera normal binding and compare earlier global observation per viewport."""
from pathlib import Path
from datetime import datetime,timezone
import hashlib,json
import numpy as np
w=Path('F:/OpenRPG_ImportWork')
source=w/'townsmith-door-source-inpass-normal-v31'
out=w/'townsmith-door-source-inpass-normal-reopen-v31'
assert not out.exists()
out.mkdir()
def read(p): return json.loads(p.read_text(encoding='utf-8-sig'))
def sha(p):
    with p.open('rb') as stream: return hashlib.file_digest(stream,'sha256').hexdigest()
pins={}
result=dict(full_import=False,forge_editor_verified=False,material_fidelity_qualified=False)
try:
    run=read(source/'RUN-RESULT.v1.json')
    assert run['actual_exit_code']==0 and run['neutral_reference_byte_equal'] and run['source_inputs_unchanged']
    prep=read(source/'PREPARED.v1.json')
    for field in ['input_pins','private_before_pins']:
        for key,value in prep[field].items():
            assert sha(Path(key))==value,key
            pins[key]=value
    rows=[]
    for case in ['door-neutral','door-nopost-linear','door-noaniso-nopost-linear']:
        buffers=[]
        for index in range(4):
            raw=source/f'{case}.inpass-normal-{index}.rgba32f'
            meta=source/f'{case}.inpass-normal-{index}.json'
            m=read(meta)
            assert m['actual_camera_normal_buffer'] and m['injection']=='BeforePostProcess'
            assert m['viewportWidth']==512 and m['viewportHeight']==512 and m['format']=='R8G8B8A8_UNorm'
            a=np.fromfile(raw,dtype='<f4').reshape(m['height'],m['width'],4)
            assert np.isfinite(a).all() and np.all((a>=0)&(a<=1))
            buffers.append(a[:512,:512])
            pins[str(raw)]=sha(raw);pins[str(meta)]=sha(meta)
        assert sha(source/f'{case}.inpass-normal-2.rgba32f')==sha(source/f'{case}.inpass-normal-3.rgba32f')
        observed=source/f'{case}._GBufferTexture1.rgba32f'
        assert np.array_equal(buffers[3],np.fromfile(observed,dtype='<f4').reshape(512,640,4)[:,:512])
        pins[str(observed)]=sha(observed)
        # Reconfirm actual depth coverage before comparing material buffers.
        depth=source/f'{case}.depth-3.r32f'
        uv_meta=read(source/f'{case}.uv-control-9.json')
        uv_path=source/f'{case}.uv-control-9.rgba32f'
        uv=np.fromfile(uv_path,dtype='<f4').reshape(uv_meta['height'],uv_meta['width'],4)[:512,:512]
        mask=np.fromfile(depth,dtype='<f4').reshape(512,512)>0
        assert np.array_equal(uv[:,:,3]==1,mask) and int(mask.sum())==34445
        for p in [depth,uv_path,source/f'{case}.uv-control-9.json']:pins[str(p)]=sha(p)
        rows.append(dict(case=case,captures=4,viewport_global_observation_matches_inpass_camera_normal=True,
                         last_two_full_buffers_byte_equal=True,depth_covered_pixels=int(mask.sum()),
                         format=m['format'],texture_name=m['textureName']))
    for p in [Path(__file__),source/'RUN-RESULT.v1.json',source/'NATIVE-RESULT.v1.json']:pins[str(p)]=sha(p)
    result.update(status='actual_inpass_camera_normal_binding_and_viewport_observation_verified',
                  rows=rows,input_pins=pins,rehashed_pins=len(pins),actual_camera_normal_binding_qualified=True,
                  scope='Actual source camera normal buffer read inside BeforePostProcess; earlier same-camera global GBuffer1 observation matches in its viewport. Other globals, full material fidelity and Studio remain open.')
except Exception as exc:result.update(status='inpass_camera_normal_capture_refused',error=repr(exc),traceback=__import__('traceback').format_exc())
result['finished_utc']=datetime.now(timezone.utc).isoformat()
(out/'RESULT.v1.json').write_text(json.dumps(result,indent=2),encoding='utf-8')
print(json.dumps({k:v for k,v in result.items() if k!='input_pins'}))
if result['status'].endswith('refused'):raise SystemExit(1)
t=w/'SUIVI_LOTS_HIVEMIND'
message=f"\n- {result['finished_utc']} — **L03.TOWNSMITH.MATERIAUX.REFERENCE.NORMALES vérifié** : 12 lectures GPU pendant BeforePostProcess, vraies normales de caméra ; répétitions finales byte-equal. L'observation GBuffer1 après rendu est identique dans le viewport des trois contrôles, {len(pins)} épingles recontrôlées. Fidélité matériau et Studio ouverts. [Preuve](../townsmith-door-source-inpass-normal-reopen-v31/RESULT.v1.json). Aucun nouvel asset complet qualifié.\n"
for name in ['JOURNAL.md','AVANCEMENT_ACTUEL.md','SUIVI_LISIBLE.md']:
    with (t/name).open('a',encoding='utf-8') as stream:stream.write(message)
p=t/'SUIVI_SIMPLE.v1.json';data=read(p)
data.update(updated_utc=result['finished_utc'],state=result['status'],last_technical_batch='L03.TOWNSMITH.MATERIAUX.REFERENCE.NORMALES',last_technical_proof=str(out/'RESULT.v1.json'),current_work_receipt=str(source/'RUN-RESULT.v1.json'),next_action='Use the qualified in-pass camera normal reference to isolate texture filtering and tangent-frame residuals; do not substitute diagnostic fine gradients for authored sampling.')
tmp=p.with_suffix('.normal31.tmp');tmp.write_text(json.dumps(data,ensure_ascii=False,indent=2),encoding='utf-8');tmp.replace(p)
