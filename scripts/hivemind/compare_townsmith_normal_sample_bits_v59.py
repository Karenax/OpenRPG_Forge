"""Compare original source normal samples with float32 native bit readbacks."""
from pathlib import Path
from datetime import datetime,timezone
import json,hashlib,struct
import numpy as np
w=Path('F:/OpenRPG_ImportWork');source=w/'townsmith-door-source-normal-textures-v32';root=w/'townsmith-normal-sample-bits-comparison-v59';assert not root.exists()
def read(p):return json.loads(p.read_text(encoding='utf-8-sig'))
def sha(p):
 with p.open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
pins={}
def pin(p,expected=None):
 h=sha(p)
 if expected is not None:assert h==expected,str(p)
 pins[str(p)]=h;return h
result=dict(full_import=False,material_fidelity_qualified=False,forge_editor_verified=False,complete_assets_delivered_increment=0)
try:
 batch=w/'townsmith-native-normal-bits-batch-v59/BATCH.v1.json';b=read(batch);assert b['status']=='eight_native_normal_bit_channels_drawn_with_stable_readbacks' and b['completed_channels']==list(range(8)),b;pin(batch)
 prior=w/'townsmith-normal-texture-comparison-v32/RESULT.v1.json';old=read(prior);assert old['status']=='original_normal_texture_source_native_samples_compared';pin(prior)
 depth=source/'door-nopost-linear.depth-3.r32f';pin(depth,old['input_pins'][str(depth)]);mask=(np.fromfile(depth,dtype='<f4').reshape(512,512)>0)[::-1];assert mask.sum()==34445
 currentdepth=w/'townsmith-door-source-material-values-v41/door-nopost-linear.depth-3.r32f';assert sha(currentdepth)==sha(depth);pin(currentdepth)
 samples=[];descs=[]
 for group in range(2):
  raw=source/f'door-nopost-linear.normal-texture-{6+group}.rgba32f';repeat=source/f'door-nopost-linear.normal-texture-{4+group}.rgba32f';meta=source/f'door-nopost-linear.normal-texture-{6+group}.json';desc=source/f'door-nopost-linear.normal-resource-{group}.json'
  assert pin(raw,old['input_pins'][str(raw)])==pin(repeat,old['input_pins'][str(repeat)])
  for p in [meta,desc]:pin(p,old['input_pins'][str(p)])
  m=read(meta);assert m['actual_opaque_depth_bound'] and m['mode']==group and m['format']=='R32G32B32A32_SFloat'
  a=np.fromfile(raw,dtype='<f4').reshape(m['height'],m['width'],4)[:512,:512][::-1];assert np.isfinite(a).all() and np.array_equal(np.any(a!=0,axis=2),mask);samples.append(a[mask]);descs.append(read(desc))
 rows=[];decoded=[];native_backend=None
 for ch in range(8):
  work=w/f'townsmith-native-normal-bits-channel-{ch}-v59';run=read(work/'RUN-RESULT.v1.json');assert run['actual_exit_code']==0 and run['gpu_draw_executed'],run
  receipt=read(work/'NATIVE-RESULT.v1.json');assert receipt['diagnostic_channel']==ch and receipt['consecutive_equal_readbacks']>=3;native_backend=receipt['backend']
  prep=read(work/'PREPARED.v1.json')
  for key,h in prep['input_pins'].items():pin(Path(key),h)
  for name in ['RUN-RESULT.v1.json','PREPARED.v1.json','NATIVE-RESULT.v1.json']:pin(work/name)
  image=work/'FORGE-LINEAR.rgba16f';pin(image,run['forge_image_sha256']);transport=np.fromfile(image,dtype='<f2').astype(np.float32).reshape(512,512,4)[mask];assert np.isfinite(transport).all() and np.all((transport>=0)&(transport<=1))
  codes=transport*255;rounded=np.rint(codes);distance=np.abs(codes-rounded);assert distance.max()<=255/2048+1e-5 and distance.max()<.5;bytes4=rounded.astype(np.uint32);bits=bytes4[:,0]|(bytes4[:,1]<<8)|(bytes4[:,2]<<16)|(bytes4[:,3]<<24);value=bits.astype('<u4').view('<f4');assert np.isfinite(value).all();decoded.append(value)
  a=samples[ch//4][:,ch%4];error=np.abs(a.astype(np.float64)-value.astype(np.float64));rows.append(dict(texture_index=6+ch//4,component='rgba'[ch%4],covered_pixels=34445,mean_absolute_error=float(error.mean()),max_absolute_error=float(error.max()),exact_float32_bits=int(np.sum(a.view('<u4')==bits)),pixels_over_0001=int(np.sum(error>.0001)),pixels_over_001=int(np.sum(error>.001)),transport_max_byte_rounding_error=float(distance.max()),transport_rounding_unambiguous=True))
 root.mkdir();output=root/'native-covered-pixels.8xf32';np.stack(decoded,axis=1).astype('<f4').tofile(output);pin(output)
 glb=w/'townsmith-native-normal-bits-carrier-v59/SM_Door-Single-B.normal-bits.candidate.glb';raw=glb.read_bytes();n=struct.unpack_from('<I',raw,12)[0];textures=json.loads(raw[20:20+n])['materials'][0]['extensions']['OPENRPG_source_graph']['textures'];resources=[]
 for group,desc in enumerate(descs):
  storage=textures[group+6]['imported_storage'];assert desc['width']==storage['width'] and desc['height']==storage['height'];resources.append(dict(texture_index=group+6,uv_scale=1 if group==0 else 30,source_resource=desc,native_storage=storage))
 # Retain exact pinned engine and shader sources after checking their current hashes.
 r=Path('E:/OpenRPG_Forge/OpenRPG_Forge-Rust');epoch=root/'source-input-epoch';snapshots={}
 for key,h in list(pins.items()):
  p=Path(key)
  if p.is_relative_to(r):
   target=epoch/p.relative_to(r);target.parent.mkdir(parents=True,exist_ok=True);target.write_bytes(p.read_bytes());assert sha(target)==h;snapshots[str(p)]=dict(snapshot=str(target),sha256=h)
 metadata=root/'SOURCE-EPOCH.v1.json';metadata.write_text(json.dumps(dict(source_snapshots=snapshots),indent=2),encoding='utf-8');pin(metadata);pin(Path(__file__))
 result.update(status='original_normal_texture_float32_samples_compared',rows=rows,resources=resources,input_pins=pins,rehashed_pins=len(pins),source_snapshot_files=len(snapshots),source_backend='Direct3D11',native_backend=native_backend,api_parity_qualified=False,required_roi_unchanged=True,source_last_two_full_buffers_byte_equal=True,scope='Original BC3 normal texture samples in source depth-bound draw versus unchanged native hardware samplers at UV0 and UV0*30, lossless float32 transport. Not original complete shader or runtime qualification.')
except Exception as exc:
 result.update(status='normal_sample_bits_comparison_refused',error=repr(exc),traceback=__import__('traceback').format_exc());root.mkdir(exist_ok=True)
result['finished_utc']=datetime.now(timezone.utc).isoformat();(root/'RESULT.v1.json').write_text(json.dumps(result,indent=2),encoding='utf-8');print(json.dumps({k:v for k,v in result.items() if k not in ['input_pins','resources']}))
if result['status'].endswith('refused'):raise SystemExit(1)
