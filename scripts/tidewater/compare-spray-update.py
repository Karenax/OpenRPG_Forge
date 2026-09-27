"""Compare one native particle update from an explicitly paired source initial state."""
from pathlib import Path
import hashlib,json,sys
import numpy as np
assert len(sys.argv)==4
source,native,output=map(Path,sys.argv[1:]);assert not output.exists()
meta=json.loads((source/'spray.json').read_text());probe=json.loads((native/'probe.json').read_text())
assert probe['mode']=='paired_source_particle_update' and probe['source_particle_initial_state_uploaded'] is True
assert probe['source_particle_expected_outputs_uploaded'] is False and probe['crest_kernel_executed'] is False
assert probe['source_manifest_sha256']==hashlib.sha256((source/'spray.json').read_bytes()).hexdigest()
step=probe['snapshots'][0];assert step in [120,540]
def load(root,prefix):
 arrays=[]
 for name in ['pos','vel','info']:
  p=root/f'{prefix}-{name}.bin';data=p.read_bytes()
  if root==source:assert hashlib.sha256(data).hexdigest()==meta['files'][p.name]
  a=np.frombuffer(data,'<f4').reshape(40960,4);assert np.isfinite(a).all();arrays.append(a)
 return np.concatenate(arrays,axis=1)
i=load(source,f'emitted-{step:04}');a=load(source,f'frame-{step:04}');b=load(native,f'frame-{step:04}')
active=i[:,9]>0;alive_a=a[:,9]>0;alive_b=b[:,9]>0
assert np.array_equal(alive_a,alive_b) and np.array_equal(a[:,[8,9,11]],b[:,[8,9,11]])
assert np.array_equal(a[~active],b[~active]) and not alive_b[32768:].any()
metrics={}
for name,cols in [('position_m',[0,1,2]),('age_s',[3]),('velocity_m_s',[4,5,6]),('radius_m',[7]),('water_height_m',[10])]:
 d=np.abs(a[active][:,cols].astype('float64')-b[active][:,cols]);metrics[name]={'mean':float(d.mean()),'max':float(d.max()),'p99':float(np.quantile(d,.99))}
prefix=f'frame-{step:04}'
da=np.fromfile(source/(prefix+'-deposit.bin'),'<u4');db=np.fromfile(native/(prefix+'-deposit.bin'),'<u4')
assert da.size==db.size==768*768 and np.array_equal(da,db) and da.sum()>0
assert (source/(prefix+'-head.bin')).read_bytes()==(native/(prefix+'-head.bin')).read_bytes()
report={'step':step,'initial_active':int(active.sum()),'final_active':int(alive_b.sum()),'particles_dying':int((active&~alive_b).sum()),'life_and_identity_exact':True,'deposit_exact':True,'deposit_drops':int(db.astype('uint64').sum()),'deposit_texels':int(np.count_nonzero(db)),'metrics':metrics,'limits':['source particle state and FFT supplied as paired inputs','one update only, not native history','shore consumes deposits on a subsequent step not run here','no rendered spray verdict']}
output.write_text(json.dumps(report,indent=2));print(json.dumps(report,indent=2))
