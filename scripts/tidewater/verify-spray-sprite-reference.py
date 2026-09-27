"""Verify original sprite assets and a scene capture with matching simulation wind."""
from pathlib import Path
import hashlib,json,sys
import numpy as np
from PIL import Image

assert len(sys.argv)==8
source,repeat,scene_source,image,image_repeat,before,output=map(Path,sys.argv[1:])
assert not output.exists()
hash_bytes=lambda b:hashlib.sha256(b).hexdigest()
metas=[]
for root in [source,repeat,scene_source]:
 m=json.loads((root/'spray.json').read_text())
 assert m['source_commit']=='1438b1abfcaee3267092b75573014f4d9b4a983c'
 for n,h in m['files'].items():
  assert Path(n).name==n and '/' not in n and '\\' not in n
  assert hash_bytes((root/n).read_bytes())==h,n
 metas.append(m)
sprite_names=[n for n in metas[0]['files'] if n.startswith('sprite-')]
assert len(sprite_names)==23
for n in sprite_names:assert len({m['files'][n] for m in metas})==1,n
assert metas[0]['sprite']==metas[1]['sprite']==metas[2]['sprite']
scene=json.loads(Path(str(image)+'.json').read_text())
base=json.loads(Path(str(before)+'.json').read_text())
assert scene['camera']==base['camera'] and scene['lighting']==base['lighting']
assert scene['spray']['global_wind']==metas[2]['global_wind']=={'speed':6,'direction':[1,0]}
assert scene['shore_sim']['deposit_schedule']=='gpu_spray_previous_step'
schedule_raw=Path(str(image)+'.spray-schedule.json').read_bytes()
assert hash_bytes(schedule_raw)==scene['spray']['schedule_sha256']
schedule=json.loads(schedule_raw);assert len(schedule)==120
for a,b in zip(schedule,metas[2]['schedule'][:120]):
 assert a=={k:b[k] for k in a}

def particles(paths):
 a=np.concatenate([np.fromfile(p,'<f4').reshape(40960,4) for p in paths],axis=1)
 assert np.isfinite(a).all() and not (a[32768:,9]>0).any()
 return sorted(row.tobytes() for row in a[a[:,9]>0])
reference=particles([scene_source/f'frame-0120-{n}.bin' for n in ['pos','vel','info']])
for img in [image,image_repeat]:
 assert particles([Path(str(img)+f'.spray-{n}.bin') for n in ['pos','vel','info']])==reference
 assert Path(str(img)+'.shore-state.rgba16.bin').read_bytes()==(scene_source/'frame-0120-shore.rgba16.bin').read_bytes()
 assert Path(str(img)+'.spray-head.bin').read_bytes()==(scene_source/'frame-0120-head.bin').read_bytes()
a=np.asarray(Image.open(image)).astype('int16');b=np.asarray(Image.open(image_repeat)).astype('int16');z=np.asarray(Image.open(before)).astype('int16')
assert a.shape==b.shape==z.shape==(1080,1920,4) and np.array_equal(a,b)
d=np.abs(a[:,:,:3]-z[:,:,:3]);effect=np.max(d,axis=2)>3
assert effect.any()
report={'source_pin':metas[0]['source_commit'],'sprite_files_exact':len(sprite_names),'sprite':metas[0]['sprite'],'scene_wind':metas[2]['global_wind'],'scene_particles_exact_modulo_slots':len(reference),'scene_shore_exact':True,'scene_budget_schedule_exact':True,'source_png_sha256':hash_bytes(image.read_bytes()),'source_repeat_pixel_exact':True,'effect_pixels_over_3':int(effect.sum()),'effect_mean_channel_delta':float(d[effect].mean()),'limits':['fixed camera and time 2 seconds','source scene with spray and its foam feedback versus without both','global wind differs from default numerical fixture','no native sprite rendering verdict','no clouds, CPU emitters or body collisions']}
output.write_text(json.dumps(report,indent=2));print(json.dumps(report,indent=2))
