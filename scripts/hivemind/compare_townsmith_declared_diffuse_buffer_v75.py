"""Compare verified in-pass source diffuse storage with retained GPU diagnostics."""
from pathlib import Path
from datetime import datetime,timezone
import json,hashlib
import numpy as np
W=Path('F:/OpenRPG_ImportWork');OUT=W/'townsmith-declared-diffuse-buffer-comparison-v75';assert not OUT.exists()
def read(p):return json.loads(p.read_text(encoding='utf-8-sig'))
def sha(p):
 with p.open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
pins={}
def pin(p,h=None):
 value=sha(p)
 if h is not None:assert value==h,str(p)
 pins[str(p)]=value
 return value
proof=W/'townsmith-door-source-declared-gbuffer3-reopen-v75/RESULT.v1.json';r=read(proof);assert r['actual_rendergraph_gbuffer3_binding_qualified'] and r['sdk_restored_byte_equal'] and r['original_lit_reference_byte_equal'];pin(proof)
source=W/'townsmith-door-source-declared-gbuffer3-v75';raw=source/'door-nopost-linear.inpass-gbuffer3-3.rgba32f';meta=source/'door-nopost-linear.inpass-gbuffer3-3.json';pin(raw,r['input_pins'][str(raw)]);pin(meta,r['input_pins'][str(meta)]);m=read(meta)
a=np.fromfile(raw,dtype='<f4').reshape(m['height'],m['width'],4)[:512,:512,:3][::-1]
depth=source/'door-nopost-linear.depth-3.r32f';pin(depth,r['input_pins'][str(depth)]);mask=(np.fromfile(depth,dtype='<f4').reshape(512,512)>0)[::-1];assert mask.sum()==34445 and np.isfinite(a).all()
reference=W/'townsmith-door-source-direct-controls-v18/door-debug-IndirectDiffuseLighting-nopost-linear.rgba16f';reference_proof=W/'townsmith-door-source-direct-controls-reopen-v18/RESULT.v1.json';prior=read(reference_proof);pin(reference_proof);pin(reference,prior['input_pins'][str(reference)]);b=np.fromfile(reference,dtype='<f2').reshape(512,512,4)[::-1,:,:3].astype('float32')
job=W/'townsmith-native-material-quad-difference-layer-2-v68';run=read(job/'RUN-RESULT.v1.json');native=read(job/'NATIVE-RESULT.v1.json');assert run['actual_exit_code']==0 and run['gpu_draw_executed'] and native['consecutive_equal_readbacks']>=3
for file in ['RUN-RESULT.v1.json','NATIVE-RESULT.v1.json','PREPARED.v1.json']:pin(job/file)
packed=job/'FORGE-PACKED.rg11b10';pin(packed,run['forge_packed_image_sha256']);words=np.fromfile(packed,dtype='<u4').reshape(512,512);channels=[]
for shift,bits in [(0,6),(11,6),(22,5)]:
 value=(words>>shift)&((1<<(bits+5))-1);mant=value&((1<<bits)-1);exponent=value>>bits;assert not np.any(exponent==31);channels.append(np.ldexp(mant.astype('float64')/(1<<bits)+np.where(exponent==0,0.,1.),np.where(exponent==0,-14,exponent.astype('int32')-15)).astype('float32'))
c=np.stack(channels,-1);rows=[]
for name,target in [('source_indirect_diffuse_debug',b),('native_retained_indirect_diffuse',c)]:
 delta=np.abs(a-target)[mask];rows.append(dict(target=name,mean_rgb_error=float(delta.mean()),max_rgb_error=float(delta.max()),exact_channels_fraction=float((delta==0).mean()),pixels_over_001=int((delta.max(axis=1)>.001).sum()),source_mean=float(a[mask].mean()),target_mean=float(target[mask].mean())))
pin(Path(__file__));OUT.mkdir();result=dict(status='declared_source_diffuse_storage_compared_to_retained_gpu_controls',finished_utc=datetime.now(timezone.utc).isoformat(),rows=rows,covered_pixels=34445,input_pins=pins,source_storage_equals_debug_on_roi=bool(np.array_equal(a[mask],b[mask])),native_previous_draw_only=True,no_new_native_gpu_draw=True,source_pixels_applied_to_native=False,pixel_fit_or_threshold_relaxation=False,installed_in_product=False,material_fidelity_qualified=False,forge_editor_verified=False,full_import=False,complete_assets_delivered_increment=0)
(OUT/'RESULT.v1.json').write_text(json.dumps(result,indent=2)+'\n',encoding='utf-8');print(json.dumps({k:v for k,v in result.items() if k!='input_pins'}))
