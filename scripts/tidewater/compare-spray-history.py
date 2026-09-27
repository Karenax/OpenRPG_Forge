"""Measure native FFT/particle history; report divergences without declaring parity."""
from pathlib import Path
import hashlib,json,sys
import numpy as np
assert len(sys.argv) in [5,7]
source,native,repeat,output=map(Path,sys.argv[1:5]);assert not output.exists()
meta=json.loads((source/'spray.json').read_text());probe=json.loads((native/'probe.json').read_text())
native_budget=probe['mode']=='native_history_120_native_budget'
assert native_budget or probe['mode']=='native_history_120_source_budget'
assert probe['source_budget_schedule'] is (not native_budget)
if native_budget:assert probe['native_budget_feedback'] is True
assert probe['source_fft_textures_uploaded'] is False and probe['source_particle_results_uploaded'] is False
assert probe.get('source_acquisition_manifest_sha256',probe['source_manifest_sha256'])==hashlib.sha256((source/'spray.json').read_bytes()).hexdigest()
def records(root):
 arrays=[]
 for n in ['pos','vel','info']:
  p=root/f'frame-0120-{n}.bin';raw=p.read_bytes()
  if root==source:assert hashlib.sha256(raw).hexdigest()==meta['files'][p.name]
  a=np.frombuffer(raw,'<f4').reshape(40960,4);assert np.isfinite(a).all();arrays.append(a)
 a=np.concatenate(arrays,axis=1);assert not (a[32768:,9]>0).any();a=a[a[:,9]>0]
 keys={tuple(row[[3,8,11]]):row for row in a};assert len(keys)==len(a),'ambiguous identities'
 return keys
ka,kb,kc=map(records,[source,native,repeat]);assert kb.keys()==kc.keys() and all(np.array_equal(kb[k],kc[k]) for k in kb)
common=ka.keys()&kb.keys();assert common
a=np.array([ka[k] for k in common]);b=np.array([kb[k] for k in common]);metrics={}
for name,cols in [('position_m',[0,1,2]),('velocity_m_s',[4,5,6]),('radius_m',[7]),('water_height_m',[10])]:
 d=np.abs(a[:,cols].astype('float64')-b[:,cols]);metrics[name]={'mean':float(d.mean()),'max':float(d.max()),'p99':float(np.quantile(d,.99))}
ha=np.fromfile(source/'history-head.u32.bin','<u4')[:120].astype('int64');hb=np.fromfile(native/'history-head.u32.bin','<u4').astype('int64');assert hb.size==120
assert (native/'history-head.u32.bin').read_bytes()==(repeat/'history-head.u32.bin').read_bytes()
da=np.fromfile(source/'frame-0120-deposit.bin','<u4').astype('int64');db=np.fromfile(native/'frame-0120-deposit.bin','<u4').astype('int64');assert (native/'frame-0120-deposit.bin').read_bytes()==(repeat/'frame-0120-deposit.bin').read_bytes()
report={'source_active':len(ka),'native_active':len(kb),'paired_identities':len(common),'missing_identities':len(ka.keys()-kb.keys()),'extra_identities':len(kb.keys()-ka.keys()),'source_emitted':int(ha[-1]),'native_emitted':int(hb[-1]),'first_emission_count_difference_step':int(np.flatnonzero(ha!=hb)[0]+1) if np.any(ha!=hb) else None,'native_repeat_exact_modulo_slots':True,'metrics':metrics,'deposits':{'source_drops':int(da.sum()),'native_drops':int(db.sum()),'different_texels':int(np.count_nonzero(da!=db)),'absolute_drop_difference':int(np.abs(da-db).sum())},'limits':['120 native FFT and particle steps from zero','source effective budget schedule replayed','shore deposit buffer cleared each step without shore state update','remaining identity and trajectory discrepancies; not parity','no sprite rendering']}
if native_budget:report['limits'][1]='native adaptive budget with GPU head feedback; controlled one-step simulation latency'
if probe.get('shore_coupled',False):
 assert len(sys.argv)==7 and probe['source_shore_state_uploaded'] is False
 assert json.loads((repeat/'probe.json').read_text())['shore_coupled'] is True
 source_base,native_base=map(Path,sys.argv[5:7])
 name='frame-0120-shore.rgba16.bin'
 raw=(source/name).read_bytes();assert hashlib.sha256(raw).hexdigest()==meta['files'][name]
 assert (native/name).read_bytes()==(repeat/name).read_bytes(),'coupled shore repeat differs'
 def state(p):
  a=np.fromfile(p,'<f2').reshape(768,768,4).astype('float64');assert np.isfinite(a).all();return a
 sa=state(source/name);sb=state(native/name)
 za=state(source_base/'frame-0120-state.rgba16.bin');zb=state(native_base/'frame-0120-state.rgba16.bin')
 channels={}
 for i,n in enumerate(['foam','wetness','residue','flow']):
  d=np.abs(sa[:,:,i]-sb[:,:,i]);ea=sa[:,:,i]-za[:,:,i];eb=sb[:,:,i]-zb[:,:,i]
  channels[n]={'mean_error':float(d.mean()),'max_error':float(d.max()),'different_texels':int(np.count_nonzero(d)),'source_effect_texels':int(np.count_nonzero(ea)),'native_effect_texels':int(np.count_nonzero(eb)),'effect_mean_error':float(np.abs(ea-eb).mean()),'effect_max_error':float(np.abs(ea-eb).max())}
 assert channels['foam']['native_effect_texels']>0,'spray did not reach shore foam'
 assert channels['wetness']['native_effect_texels']==channels['flow']['native_effect_texels']==0
 report['coupled_shore']={'repeat_exact':True,'channels':channels}
 report['limits'][2]='shore consumes previous deposit before emission; final deposit remains for next step'
output.write_text(json.dumps(report,indent=2));print(json.dumps(report,indent=2))
