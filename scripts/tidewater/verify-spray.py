"""Verify spray acquisition while allowing atomic ring slot permutations."""
from pathlib import Path
import hashlib,json,sys
import numpy as np
assert len(sys.argv)==5
source,repeat,shore,output=map(Path,sys.argv[1:]);assert not output.exists()
metas=[]
for root in [source,repeat]:
 m=json.loads((root/'spray.json').read_text());metas.append(m)
 assert m['schema']=='forge.tidewater.spray' and m['version']==1 and m['source_commit']=='1438b1abfcaee3267092b75573014f4d9b4a983c'
 assert m['capacities']=={'gpu':32768,'cpu':8192,'total':40960}
 assert m['options']==dict(spray=True,clouds=False,cpu_emitters=False,body=False,shore_deposit=True)
 assert len(m['schedule'])==540
 for i,s in enumerate(m['schedule'],1):
  assert s['frame']==i and abs(s['time']-i/60)<1e-10 and s['emitter_seed']==i-1 and s['update_seed']==i and .05<=s['budget']<=1
 for n,h in m['files'].items():
  assert Path(n).name==n and '/' not in n and '\\' not in n
  assert hashlib.sha256((root/n).read_bytes()).hexdigest()==h,n
assert metas[0]['schedule']==metas[1]['schedule']
different=[n for n in metas[0]['files'] if metas[0]['files'][n]!=metas[1]['files'][n]]
assert all(n.endswith(('-pos.bin','-vel.bin','-info.bin')) for n in different),different
reports=[]
for step in [1,120,540]:
 prefix=f'frame-{step:04}';active_sets=[];counts=[]
 for root in [source,repeat]:
  arrays=[np.fromfile(root/(prefix+'-'+n+'.bin'),'<f4').reshape(40960,4) for n in ['pos','vel','info']]
  assert all(np.isfinite(a).all() for a in arrays)
  pos,vel,info=arrays;active=info[:,1]>0
  assert not active[32768:].any(),'CPU emitters were supposed to be disabled'
  assert np.all(np.isin(info[active,0],[0,1,2,3])) and np.all(vel[active,3]>0)
  records=np.concatenate(arrays,axis=1)[active]
  active_sets.append(sorted(row.tobytes() for row in records));counts.append(int(active.sum()))
 assert active_sets[0]==active_sets[1],'Active particle multiset differs beyond atomic ordering'
 deposit=np.fromfile(source/(prefix+'-deposit.bin'),'<u4');assert deposit.size==768*768
 state=np.fromfile(source/(prefix+'-shore.rgba16.bin'),'<f2').reshape(768,768,4).astype('float32')
 baseline=np.fromfile(shore/(prefix+'-state.rgba16.bin'),'<f2').reshape(768,768,4).astype('float32')
 assert np.isfinite(state).all() and np.array_equal(state[:,:,[1,3]],baseline[:,:,[1,3]]),'Spray should not change wetness or flow'
 change=state[:,:,0]-baseline[:,:,0]
 reports.append(dict(frame=step,active_particles=counts[0],active_multiset_exact=True,active_multiset_sha256=hashlib.sha256(b''.join(active_sets[0])).hexdigest(),head=int(np.fromfile(source/(prefix+'-head.bin'),'<u4')[0]),pending_deposit_drops=int(deposit.astype('uint64').sum()),deposit_texels=int(np.count_nonzero(deposit)),residue_changed_texels=int(np.count_nonzero(state[:,:,2]-baseline[:,:,2])),foam_changed_texels=int(np.count_nonzero(change)),foam_mean_increase=float(change.mean()),foam_max_increase=float(change.max())))
report=dict(source=str(source),repeat=str(repeat),exact_files=len(metas[0]['files'])-len(different),permuted_particle_files=different,schedule_exact=True,snapshots=reports,limits=['original coupled GPU simulation only','ring slot ordering is nondeterministic','this repeat had identical budget schedule; other asynchronous schedules may differ','no native spray or rendered spray qualification'])
output.write_text(json.dumps(report,indent=2));print(json.dumps(report,indent=2))
