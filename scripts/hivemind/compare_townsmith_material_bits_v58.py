"""Compare actual float32 graph values transported losslessly as four bytes."""
from pathlib import Path
from datetime import datetime,timezone
import hashlib,json
import numpy as np
w=Path('F:/OpenRPG_ImportWork');source=w/'townsmith-door-source-material-values-v41';out=w/'townsmith-material-bits-comparison-v58'
def read(p):return json.loads(p.read_text(encoding='utf-8-sig'))
def sha(p):
 with p.open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
pins={}
def pin(p,expected=None):
 h=sha(p)
 if expected is not None:assert h==expected,str(p)
 pins[str(p)]=h
 return h
assert not out.exists()
result=dict(full_import=False,material_fidelity_qualified=False,forge_editor_verified=False,complete_assets_delivered_increment=0)
try:
 batch=w/'townsmith-native-graph-bits-batch-v58/BATCH.v1.json';b=read(batch)
 assert b['status']=='nine_native_graph_bit_channels_drawn_with_stable_readbacks' and b['completed_channels']==list(range(9)),b
 pin(batch)
 prior=w/'townsmith-material-values-comparison-v41/RESULT.v1.json';old=read(prior);assert old['status']=='generated_source_and_native_graph_intermediates_compared';pin(prior)
 depth=source/'door-nopost-linear.depth-3.r32f';pin(depth,old['input_pins'][str(depth)])
 mask=(np.fromfile(depth,dtype='<f4').reshape(512,512)>0)[::-1];assert mask.sum()==34445
 source_values=[]
 for group in range(3):
  raw=source/f'door-nopost-linear.graph-probe-{9+group}.rgba32f';repeat=source/f'door-nopost-linear.graph-probe-{6+group}.rgba32f';meta=source/f'door-nopost-linear.graph-probe-{9+group}.json'
  assert pin(raw,old['input_pins'][str(raw)])==pin(repeat,old['input_pins'][str(repeat)])
  pin(meta,old['input_pins'][str(meta)]);m=read(meta);assert m['actual_opaque_depth_bound'] and m['mode']==group and m['format']=='R32G32B32A32_SFloat'
  a=np.fromfile(raw,dtype='<f4').reshape(m['height'],m['width'],4)[:512,:512][::-1,:,:3];assert np.isfinite(a).all();source_values.append(a)
 rows=[];native_values=[]
 names=['base_r','base_g','base_b','metallic','smoothness','occlusion','normal_x','normal_y','normal_z']
 for channel,name in enumerate(names):
  work=w/f'townsmith-native-graph-bits-channel-{channel}-v58';run=read(work/'RUN-RESULT.v1.json');assert run['actual_exit_code']==0 and run['gpu_draw_executed'],run
  receipt=read(work/'NATIVE-RESULT.v1.json');assert receipt['diagnostic_channel']==channel and receipt['consecutive_equal_readbacks']>=3
  prep=read(work/'PREPARED.v1.json')
  for key,h in prep['input_pins'].items():pin(Path(key),h)
  for name2 in ['RUN-RESULT.v1.json','NATIVE-RESULT.v1.json','PREPARED.v1.json']:pin(work/name2)
  image=work/'FORGE-LINEAR.rgba16f';pin(image,run['forge_image_sha256'])
  transport=np.fromfile(image,dtype='<f2').astype(np.float32).reshape(512,512,4)[mask]
  assert np.isfinite(transport).all() and np.all((transport>=0)&(transport<=1))
  codes=transport*255.;rounded=np.rint(codes);err=np.abs(codes-rounded);assert err.max()<=255/2048+1e-5 and err.max()<.5
  bytes4=rounded.astype(np.uint32);bits=bytes4[:,0]|(bytes4[:,1]<<8)|(bytes4[:,2]<<16)|(bytes4[:,3]<<24);decoded=bits.astype('<u4').view('<f4');assert np.isfinite(decoded).all()
  native_values.append(decoded);a=source_values[channel//3][:,:,channel%3][mask];e=np.abs(a.astype(np.float64)-decoded.astype(np.float64))
  rows.append(dict(name=name,covered_pixels=int(mask.sum()),mean_absolute_error=float(e.mean()),max_absolute_error=float(e.max()),exact_float32_bits=int(np.sum(a.view('<u4')==bits)),transport_max_byte_rounding_error=float(err.max()),transport_rounding_unambiguous=True,source_min=float(a.min()),source_max=float(a.max()),native_min=float(decoded.min()),native_max=float(decoded.max())))
 sa=source_values[2][mask].astype(np.float64);nb=np.stack(native_values[6:9],axis=1).astype(np.float64);assert np.all(np.linalg.norm(sa,axis=1)>0) and np.all(np.linalg.norm(nb,axis=1)>0)
 angles=np.degrees(np.arccos(np.clip(np.sum((sa/np.linalg.norm(sa,axis=1)[:,None])*(nb/np.linalg.norm(nb,axis=1)[:,None]),axis=1),-1,1)))
 pin(Path(__file__))
 result.update(status='source_float32_and_native_lossless_graph_values_compared',rows=rows,normal_mean_angle_degrees=float(angles.mean()),normal_max_angle_degrees=float(angles.max()),input_pins=pins,rehashed_pins=len(pins),source_capture_epoch_verified=True,scope='Diagnostic graph outputs before TBN; float32 bits recovered from RGBA bytes. This is not full original shader or editor/runtime qualification.')
except Exception as exc:result.update(status='lossless_graph_comparison_refused',error=repr(exc),traceback=__import__('traceback').format_exc())
result['finished_utc']=datetime.now(timezone.utc).isoformat();out.mkdir();(out/'RESULT.v1.json').write_text(json.dumps(result,indent=2),encoding='utf-8')
print(json.dumps({k:v for k,v in result.items() if k!='input_pins'}))
if result['status'].endswith('refused'):raise SystemExit(1)
