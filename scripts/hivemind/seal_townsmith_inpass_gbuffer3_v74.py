"""Qualify camera normal binding and compare earlier global observation per viewport."""
from pathlib import Path
from datetime import datetime,timezone
import hashlib,json
import numpy as np
w=Path('F:/OpenRPG_ImportWork')
source=w/'townsmith-door-source-inpass-gbuffer3-v74'
out=w/'townsmith-door-source-inpass-gbuffer3-reopen-v74'
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
    reference=w/'townsmith-door-source-microshadow-inputs-v55/door-nopost-linear.rgba16f'
    prior_proof=w/'townsmith-source-microshadow-inputs-reopen-v55/RESULT.v1.json'
    prior=read(prior_proof);assert sha(reference)==prior['input_pins'][str(reference)]
    assert sha(source/'door-nopost-linear.rgba16f')==sha(reference)
    for path in [reference,prior_proof,source/'door-nopost-linear.rgba16f']:pins[str(path)]=sha(path)
    rows=[]
    for case in ['door-neutral','door-nopost-linear','door-noaniso-nopost-linear']:
        buffers=[]
        for index in range(4):
            raw=source/f'{case}.inpass-gbuffer3-{index}.rgba32f'
            meta=source/f'{case}.inpass-gbuffer3-{index}.json'
            m=read(meta)
            assert m['actual_rendergraph_gbuffer3_buffer'] and m['injection']=='BeforePreRefraction'
            assert m['viewportWidth']==512 and m['viewportHeight']==512 and m['format']=='B10G11R11_UFloatPack32'
            a=np.fromfile(raw,dtype='<f4').reshape(m['height'],m['width'],4)
            assert np.isfinite(a).all() and np.all(a>=0)
            buffers.append(a[:512,:512])
            pins[str(raw)]=sha(raw);pins[str(meta)]=sha(meta)
        assert sha(source/f'{case}.inpass-gbuffer3-2.rgba32f')==sha(source/f'{case}.inpass-gbuffer3-3.rgba32f')
        observed=source/f'{case}._GBufferTexture3.rgba32f'
        assert np.array_equal(buffers[3],np.fromfile(observed,dtype='<f4').reshape(m['height'],m['width'],4)[:,:512])
        pins[str(observed)]=sha(observed)
        # Reconfirm actual depth coverage before comparing material buffers.
        depth=source/f'{case}.depth-3.r32f'
        uv_meta=read(source/f'{case}.uv-control-9.json')
        uv_path=source/f'{case}.uv-control-9.rgba32f'
        uv=np.fromfile(uv_path,dtype='<f4').reshape(uv_meta['height'],uv_meta['width'],4)[:512,:512]
        mask=np.fromfile(depth,dtype='<f4').reshape(512,512)>0
        assert np.array_equal(uv[:,:,3]==1,mask) and int(mask.sum())==34445
        for p in [depth,uv_path,source/f'{case}.uv-control-9.json']:pins[str(p)]=sha(p)
        rows.append(dict(case=case,captures=4,viewport_global_observation_matches_inpass_rendergraph_gbuffer3=True,
                         last_two_full_buffers_byte_equal=True,depth_covered_pixels=int(mask.sum()),
                         format=m['format'],texture_name=m['textureName']))
    for p in [Path(__file__),source/'RUN-RESULT.v1.json',source/'NATIVE-RESULT.v1.json']:pins[str(p)]=sha(p)
    result.update(status='actual_inpass_rendergraph_gbuffer3_binding_and_viewport_observation_verified',
                  rows=rows,input_pins=pins,rehashed_pins=len(pins),actual_rendergraph_gbuffer3_binding_qualified=True,
                  scope='Source GBuffer3 read via the current RenderGraph handle in BeforePreRefraction for the actual request camera. Final global viewport observation checked. Pre-storage float values, full material fidelity and Studio remain open.')
except Exception as exc:result.update(status='inpass_rendergraph_gbuffer3_capture_refused',error=repr(exc),traceback=__import__('traceback').format_exc())
result['finished_utc']=datetime.now(timezone.utc).isoformat()
(out/'RESULT.v1.json').write_text(json.dumps(result,indent=2),encoding='utf-8')
print(json.dumps({k:v for k,v in result.items() if k!='input_pins'}))
if result['status'].endswith('refused'):raise SystemExit(1)
