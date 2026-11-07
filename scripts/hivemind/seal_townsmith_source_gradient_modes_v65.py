from pathlib import Path
from datetime import datetime,timezone
import json,hashlib
import numpy as np
w=Path('F:/OpenRPG_ImportWork');source=w/'townsmith-door-source-gradient-modes-v65';root=w/'townsmith-source-gradient-modes-reopen-v65';assert not root.exists()
def read(p):return json.loads(p.read_text(encoding='utf-8-sig'))
def sha(p):
 with p.open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
pins={}
def pin(p,h=None):
 value=sha(p)
 if h is not None:assert value==h,str(p)
 pins[str(p)]=value;return value
run=read(source/'RUN-RESULT.v1.json');assert run['actual_exit_code']==0 and run['source_inputs_unchanged'] and run['neutral_reference_byte_equal']
prep=read(source/'PREPARED.v1.json')
for field in ['input_pins','private_before_pins']:
 for key,h in prep[field].items():pin(Path(key),h)
for name in ['RUN-RESULT.v1.json','PREPARED.v1.json','NATIVE-RESULT.v1.json']:pin(source/name)
oldproof=w/'townsmith-normal-texture-comparison-v32/RESULT.v1.json';old=read(oldproof);pin(oldproof)
rows=[]
for case in ['door-neutral','door-nopost-linear','door-noaniso-nopost-linear']:
 depth=source/(case+'.depth-3.r32f');mask=np.fromfile(depth,dtype='<f4').reshape(512,512)>0;assert mask.sum()==34445;pin(depth)
 for mode in range(6):
  a=source/f'{case}.gradient-mode-{12+mode}.rgba32f';b=source/f'{case}.gradient-mode-{18+mode}.rgba32f';meta=source/f'{case}.gradient-mode-{18+mode}.json';assert pin(a)==pin(b);pin(meta);m=read(meta);assert m['actual_opaque_depth_bound'] and m['mode']==mode and m['format']=='R32G32B32A32_SFloat'
  data=np.fromfile(b,dtype='<f4').reshape(m['height'],m['width'],4)[:512,:512];assert np.isfinite(data).all() and np.array_equal(np.any(data!=0,axis=2),mask)
  rows.append(dict(case=case,mode=mode,covered_pixels=34445,last_two_full_buffers_byte_equal=True))
 for mode in range(6):
  for index in range(4):
   p=source/f'{case}.gradient-mode-{index*6+mode}.rgba32f';pin(p);pin(p.with_suffix('.json'))
 for name in ['context.json','normal-resource-0.json','normal-resource-1.json']:pin(source/(case+'.'+name))
comparisons=[]
for case in ['door-neutral','door-nopost-linear','door-noaniso-nopost-linear']:
 mask=np.fromfile(source/(case+'.depth-3.r32f'),dtype='<f4').reshape(512,512)>0
 for texture in range(2):
  base=source/f'{case}.gradient-mode-{18+texture}.rgba32f';meta=read(source/f'{case}.gradient-mode-{18+texture}.json');a=np.fromfile(base,dtype='<f4').reshape(meta['height'],meta['width'],4)[:512,:512]
  for label,delta in [('fine',2),('coarse',4)]:
   other=source/f'{case}.gradient-mode-{18+texture+delta}.rgba32f';b=np.fromfile(other,dtype='<f4').reshape(meta['height'],meta['width'],4)[:512,:512];error=np.abs(a.astype(np.float64)-b.astype(np.float64))[mask];comparisons.append(dict(case=case,texture_index=texture+6,mode=label,full_buffer_byte_equal=sha(base)==sha(other),rgba_mean_absolute_error=error.mean(0).tolist(),rgba_max_absolute_error=error.max(0).tolist(),exact_channels_fraction=float((error==0).mean())))
for texture in range(2):
 oldraw=w/f'townsmith-door-source-normal-textures-v32/door-nopost-linear.normal-texture-{6+texture}.rgba32f';pin(oldraw,old['input_pins'][str(oldraw)]);assert sha(oldraw)==sha(source/f'door-nopost-linear.gradient-mode-{18+texture}.rgba32f')

reference=w/'townsmith-door-source-normal-textures-v32/door-nopost-linear.depth-3.r32f';pin(reference,old['input_pins'][str(reference)]);assert sha(reference)==sha(source/'door-nopost-linear.depth-3.r32f')
root.mkdir();epoch=root/'owned-source-epoch';snapshots={}
for key,h in list(pins.items()):
 p=Path(key)
 if p.suffix.lower() in ['.py','.cs','.shader','.compute','.wgsl','.hlsl']:
  dest=epoch/(hashlib.sha256(key.encode()).hexdigest()[:12]+'-'+p.name);dest.parent.mkdir(parents=True,exist_ok=True);dest.write_bytes(p.read_bytes());assert sha(dest)==h;snapshots[str(p)]=dict(snapshot=str(dest),sha256=h)
record=root/'SOURCE-EPOCH.v1.json';record.write_text(json.dumps(dict(source_snapshots=snapshots),indent=2),encoding='utf-8');pin(record);pin(Path(__file__))
result=dict(status='source_implicit_fine_and_coarse_gradient_modes_compared',finished_utc=datetime.now(timezone.utc).isoformat(),rows=rows,input_pins=pins,rehashed_pins=len(pins),source_snapshot_files=len(snapshots),raw_captures=72,source_inputs_unchanged=True,neutral_reference_byte_equal=True,source_mode_comparisons=comparisons,original_implicit_samples_byte_equal=True,compiled_derivative_mode_generalization_qualified=False,prior_opaque_depth_byte_equal=True,full_import=False,material_fidelity_qualified=False)
(root/'RESULT.v1.json').write_text(json.dumps(result,indent=2),encoding='utf-8');print(json.dumps({k:v for k,v in result.items() if k not in ['input_pins','rows']}))
