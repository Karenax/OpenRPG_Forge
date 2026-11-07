"""Measure original AO contribution without fitting or importing source pixels."""
from pathlib import Path
from datetime import datetime, timezone
import hashlib
import json
import numpy as np
from PIL import Image

W=Path('F:/OpenRPG_ImportWork')
SOURCE=W/'townsmith-door-source-ao-controls-v40'
DRAW=W/'townsmith-native-source-uv1-lighting-v37'
OUT=W/'townsmith-source-ao-comparison-v40'
assert not OUT.exists()
def read(p):return json.loads(p.read_text(encoding='utf-8-sig'))
def sha(p):
 with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
pins={}
def reopen(p):
 d=read(p)
 for field in ['input_pins','private_before_pins']:
  for name,h in d.get(field,{}).items():
   assert sha(Path(name))==h,name
   pins[name]=h
 pins[str(p)]=sha(p)
 return d
run=read(SOURCE/'RUN-RESULT.v1.json')
assert run['actual_exit_code']==0 and run['neutral_reference_byte_equal'] and run['source_inputs_unchanged']
assert sha(SOURCE/'NATIVE-RESULT.v1.json')==run['native_receipt_sha256']
reopen(SOURCE/'PREPARED.v1.json')
reopen(DRAW/'PREPARED.v1.json')
native_run=read(DRAW/'RUN-RESULT.v1.json')
assert native_run['actual_exit_code']==0 and native_run['gpu_draw_executed']
assert sha(DRAW/'FORGE-LINEAR.rgba16f')==native_run['forge_image_sha256']
assert sha(DRAW/'NATIVE-RESULT.v1.json')==native_run['native_receipt_sha256']
assert sha(SOURCE/'door-nopost-linear.rgba16f')==sha(W/'townsmith-door-source-inpass-normal-v31/door-nopost-linear.rgba16f')
on='door-nopost-linear';off='door-noao-nopost-linear'
ao=[]
for name,expected in [(on,.5),(off,0)]:
 p=SOURCE/(name+'.active-ssao.json');settings=read(p)['MonoBehaviour']
 assert settings['intensity']['m_Value']==expected
 assert settings['directLightingStrength']['m_Value']==0
 assert read(SOURCE/(name+'.context.json'))['anisotropic_filtering']=='ForceEnable'
 ao.append(settings)
 for suffix in ['.rgba16f','.context.json','.active-ssao.json','.depth-3.r32f','.inpass-normal-2.rgba32f','.inpass-normal-3.rgba32f']:
  p=SOURCE/(name+suffix);pins[str(p)]=sha(p)
 assert sha(SOURCE/(name+'.inpass-normal-2.rgba32f'))==sha(SOURCE/(name+'.inpass-normal-3.rgba32f'))
assert {k:v for k,v in ao[0].items() if k!='intensity'}=={k:v for k,v in ao[1].items() if k!='intensity'}
assert sha(SOURCE/(on+'.depth-3.r32f'))==sha(SOURCE/(off+'.depth-3.r32f'))
norm=lambda name:np.fromfile(SOURCE/(name+'.inpass-normal-3.rgba32f'),dtype='<f4').reshape(512,640,4)[:,:512]
assert np.array_equal(norm(on),norm(off))
mask=np.flipud(np.fromfile(SOURCE/(on+'.depth-3.r32f'),dtype='<f4').reshape(512,512)>0)
assert int(mask.sum())==34445
pixels=lambda p:np.fromfile(p,dtype='<f2').reshape(512,512,4).astype(np.float32)
a=np.flipud(pixels(SOURCE/(on+'.rgba16f')))
b=np.flipud(pixels(SOURCE/(off+'.rgba16f')))
n=pixels(DRAW/'FORGE-LINEAR.rgba16f')
assert all(np.isfinite(x).all() for x in [a,b,n])
assert np.array_equal(n[:,:,3]!=0,mask)
rows=[]
for label,x,y in [('source_original_vs_native',a,n),('source_no_scene_ao_vs_native',b,n),('source_scene_ao_contribution',a,b)]:
 e=np.abs(x[:,:,:3]-y[:,:,:3])[mask]
 rows.append(dict(control=label,mean_rgb_error=float(e.mean()),max_rgb_error=float(e.max()),pixels_over_001=int((e.max(axis=1)>.001).sum())))
OUT.mkdir()
panel=Image.new('RGB',(1536,512),'black')
for i,x in enumerate([a,b,n]):
 rgb=np.maximum(x[:,:,:3],0);rgb=rgb/(1+rgb)
 rgb=np.where(rgb<=.0031308,rgb*12.92,1.055*rgb**(1/2.4)-.055)
 panel.paste(Image.fromarray(np.round(np.clip(rgb,0,1)*255).astype('uint8')),(i*512,0))
panel.save(OUT/'source-ao-on-off-native.png')
for p in [Path(__file__),SOURCE/'RUN-RESULT.v1.json',SOURCE/'NATIVE-RESULT.v1.json',DRAW/'RUN-RESULT.v1.json',DRAW/'NATIVE-RESULT.v1.json',DRAW/'FORGE-LINEAR.rgba16f',OUT/'source-ao-on-off-native.png']:
 pins[str(p)]=sha(p)
result=dict(status='source_scene_ao_effect_measured_with_original_reference_preserved',finished_utc=datetime.now(timezone.utc).isoformat(),lot='L03.TOWNSMITH.MATERIAUX.SCENE.AO',input_pins=pins,rehashed_pins=len(pins),rows=rows,covered_pixels=34445,source_ao_intensity_on=.5,source_ao_intensity_off=0,source_force_enable_sampling_preserved=True,source_depth_and_viewport_normals_unchanged=True,original_no_post_reference_byte_equal=True,ao_disabled_case_is_diagnostic_only=True,source_pixels_applied_to_native=False,pixel_fit_or_threshold_relaxation=False,full_import=False,forge_editor_verified=False,complete_assets_delivered_increment=0,next_action='Separate native direct, diffuse-indirect and reflection components, then correct common lighting against original AO-enabled source.')
(OUT/'RESULT.v1.json').write_text(json.dumps(result,indent=2),encoding='utf-8')
print(json.dumps({k:v for k,v in result.items() if k!='input_pins'}))
