from pathlib import Path
from datetime import datetime,timezone
import json,hashlib,numpy as np
from PIL import Image,ImageDraw
W=Path('F:/OpenRPG_ImportWork');O=W/'townsmith-native-srgb-store-comparison-v82';assert not O.exists();pins={}
def read(p):return json.loads(p.read_text(encoding='utf-8-sig'))
def pin(p,h=None):
 with p.open('rb') as f:v=hashlib.file_digest(f,'sha256').hexdigest()
 assert h is None or v==h,str(p);pins[str(p)]=v;return v
inst=W/'townsmith-declared-gbuffer0-raw-instrumentation-v82/RESULT.v1.json';i=read(inst);assert i['status']=='declared_gbuffer0_raw_capture_terminal' and i['sdk_restored_byte_equal'] and not i['sdk_restore_pending'];pin(inst);pin(Path(i['sdk_path']),i['sdk_original_sha256'])
for key,h in i['input_pins'].items():pin(Path(key),h)
root=W/'townsmith-door-source-declared-gbuffer0-raw-v82';run=read(root/'RUN-RESULT.v1.json');prep=read(root/'PREPARED.v1.json');assert run['actual_exit_code']==0 and run['neutral_reference_byte_equal'] and run['source_inputs_unchanged']
for field in ['input_pins','private_before_pins']:
 for key,h in prep[field].items():pin(Path(key),h)
for file in ['RUN-RESULT.v1.json','NATIVE-RESULT.v1.json','PREPARED.v1.json']:pin(root/file)
oldproof=W/'townsmith-door-source-declared-gbuffer0-reopen-v79/RESULT.v1.json';old=read(oldproof);pin(oldproof);rows=[];source_codes=None;mask=None;loaded=None;inverse_max=0
for case in ['door-neutral','door-nopost-linear','door-noaniso-nopost-linear']:
 for index in range(4):
  mp=root/f'{case}.inpass-gbuffer0-{index}.json';m=read(mp);assert m['actual_rendergraph_gbuffer0_buffer'] and m['format']=='R8G8B8A8_SRGB' and m['viewportWidth']==512 and m['viewportHeight']==512;pin(mp)
  raw=root/f'{case}.inpass-gbuffer0-{index}.rgba8srgb';flo=root/f'{case}.inpass-gbuffer0-{index}.rgba32f';pin(raw);pin(flo);assert raw.stat().st_size==m['width']*m['height']*4
  if index==3:
   code=np.fromfile(raw,dtype='u1').reshape(m['height'],m['width'],4)[:512,:512][::-1];a=np.fromfile(flo,dtype='<f4').reshape(m['height'],m['width'],4)[:512,:512][::-1]
   assert np.isfinite(a).all();prev=W/f'townsmith-door-source-declared-gbuffer0-v79/{case}.inpass-gbuffer0-3.rgba32f';pin(prev,old['input_pins'][str(prev)]);assert flo.read_bytes()==prev.read_bytes()
 for suffix in ['rgba8srgb','rgba32f']:assert (root/f'{case}.inpass-gbuffer0-2.{suffix}').read_bytes()==(root/f'{case}.inpass-gbuffer0-3.{suffix}').read_bytes()
 depth=root/f'{case}.depth-3.r32f';pin(depth);mk=(np.fromfile(depth,dtype='<f4').reshape(512,512)>0)[::-1];assert mk.sum()==34445
 # Validate the interpretation of earlier loaded floats against actual raw source bytes.
 x=a[:,:,:3][mk].astype('float64');enc=np.where(x<=.0031308,12.92*x,1.055*np.power(x,1/2.4)-.055)*255;inferred=np.rint(enc).astype('int32');assert np.array_equal(inferred,code[:,:,:3][mk]);inverse_max=max(inverse_max,float(np.abs(enc-inferred).max()))
 rows.append(dict(case=case,raw_captures=4,loaded_captures=4,last_two_full_raw_buffers_byte_equal=True,last_two_full_loaded_buffers_byte_equal=True,prior_qualified_loaded_buffer_byte_equal=True,raw_source_codes_confirm_prior_code_identification=True,depth_covered_pixels=34445))
 if case=='door-nopost-linear':source_codes=code;mask=mk;loaded=a
ref=root/'door-nopost-linear.rgba16f';prev_ref=W/'townsmith-door-source-microshadow-inputs-v55/door-nopost-linear.rgba16f';pin(ref);pin(prev_ref);assert ref.read_bytes()==prev_ref.read_bytes()
job=W/'townsmith-native-srgb-store-channel-0-v81';nr=read(job/'RUN-RESULT.v1.json');nn=read(job/'NATIVE-RESULT.v1.json');assert nr['actual_exit_code']==0 and nn['consecutive_equal_readbacks']>=3 and nn['target_format']=='Rgba8UnormSrgb' and nn['camera_hdr']==False and nn['native_main_pass_formats']==['Rgba8UnormSrgb']
for key,h in read(job/'PREPARED.v1.json')['input_pins'].items():pin(Path(key),h)
for file in ['RUN-RESULT.v1.json','NATIVE-RESULT.v1.json','PREPARED.v1.json']:pin(job/file)
fmt=W/'townsmith-srgb-store-format-epoch-v81/RESULT.v1.json';fr=read(fmt);assert fr['rustfmt_equivalence_verified'];pin(fmt)
for key,h in fr['input_pins'].items():pin(Path(key),h)
builder=W/'townsmith-native-srgb-store-carrier-v81/RESULT.v1.json';bp=read(builder)
for key,h in bp['input_pins'].items():
 if key.endswith('verify_source_glb_srgb_store_v81.rs'):pin(W/'townsmith-srgb-store-format-epoch-v81/example.before-rustfmt.rs',h)
 else:pin(Path(key),h)
raw=job/'FORGE-STORED.rgba8srgb';pin(raw,nr['forge_image_sha256']);native=np.fromfile(raw,dtype='u1').reshape(512,512,4);assert np.array_equal(native[:,:,3]==255,mask)
previous=W/'townsmith-gbuffer-input-comparison-v80/RESULT.v1.json';p=read(previous);pin(previous);prevvals=W/'townsmith-gbuffer-input-comparison-v80/NATIVE-INPUTS.npy';pin(prevvals,p['input_pins'][str(prevvals)]);vv=np.load(prevvals)[:,:3].astype('float64');ve=np.where(vv<=.0031308,12.92*vv,1.055*np.power(vv,1/2.4)-.055)*255;oldcodes=np.rint(ve).astype('int32');assert np.abs(ve-oldcodes).max()<.5
sc=source_codes[:,:,:3][mask].astype('int32');nc=native[:,:,:3][mask].astype('int32');measure=[]
for name,codes in [('analytic_shader_projection',oldcodes),('actual_native_srgb_render_targets',nc)]:
 delta=np.abs(sc-codes);measure.append(dict(control=name,equal_rgb_pixels=int(np.all(delta==0,axis=1).sum()),equal_channel_counts=(delta==0).sum(axis=0).tolist(),max_code_error=int(delta.max()),mean_code_error=float(delta.mean()),differing_rgb_pixels=int(np.any(delta!=0,axis=1).sum())))
O.mkdir();canvas=Image.new('RGB',(1536,544),(35,35,35));draw=ImageDraw.Draw(canvas)
for col,title,img in [(0,'Unity - source raw sRGB',source_codes[:,:,:3]),(1,'Forge - actual sRGB targets',native[:,:,:3]),(2,'Code difference x64',np.clip(np.abs(source_codes[:,:,:3].astype('int32')-native[:,:,:3].astype('int32'))*64,0,255).astype('uint8'))]:
 image=img.copy();image[~mask]=35;canvas.paste(Image.fromarray(image),(512*col,32));draw.text((512*col+8,10),title,fill='white')
canvas.save(O/'srgb-storage-controls.png');pin(O/'srgb-storage-controls.png');pin(Path(__file__))
res=dict(status='actual_native_and_source_srgb_storage_bytes_compared',finished_utc=datetime.now(timezone.utc).isoformat(),lot='L03.TOWNSMITH.MATERIAUX.STOCKAGE.SRGB.NATIF',source_cases=rows,source_raw_byte_captures=12,source_loaded_float_captures=12,source_raw_readback_format='R8G8B8A8_SRGB',native_main_pass_formats=nn['native_main_pass_formats'],native_target_format=nn['target_format'],consecutive_equal_native_readbacks=nn['consecutive_equal_readbacks'],current_native_alpha_coverage_matches_source_depth=True,source_code_identification_confirmed_by_raw_bytes=True,previous_inverse_code_max_distance=inverse_max,measurements=measure,covered_pixels=34445,sdk_restored_byte_equal=True,original_lit_reference_byte_equal=True,rehashed_pins=len(pins),input_pins=pins,source_pixels_applied_to_native=False,pixel_fit_or_threshold_relaxation=False,installed_in_product=False,full_material_fidelity_qualified=False,forge_editor_verified=False,full_import=False,complete_assets_delivered_increment=0)
(O/'RESULT.v1.json').write_text(json.dumps(res,indent=2)+'\n');print(json.dumps({k:v for k,v in res.items() if k!='input_pins'}))
