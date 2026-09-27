"""Compare emitted particles by their unique kind/tag, not atomic ring slot order."""
from pathlib import Path
import hashlib,json,sys
import numpy as np
assert len(sys.argv)==5
source,native,repeat,output=map(Path,sys.argv[1:]);assert not output.exists()
meta=json.loads((source/'spray.json').read_text());probe=json.loads((native/'probe.json').read_text())
assert probe['schema']=='forge.tidewater.native-spray-emission-probe' and probe['mode'] in ['isolated_source_fft_frame1','isolated_source_fft_integrated_frame1']
integrated=probe['mode']=='isolated_source_fft_integrated_frame1'
prefix='frame-0001' if integrated else 'emitted-0001'
assert probe['source_particle_results_uploaded'] is False and probe['source_fft_textures_uploaded'] is True
assert probe['source_manifest_sha256']==hashlib.sha256((source/'spray.json').read_bytes()).hexdigest()
def rows(root):
 arrays=[]
 for name in ['pos','vel','info']:
  path=root/f'{prefix}-{name}.bin';data=path.read_bytes()
  if root==source:assert hashlib.sha256(data).hexdigest()==meta['files'][path.name]
  a=np.frombuffer(data,'<f4').reshape(40960,4);assert np.isfinite(a).all();arrays.append(a)
 a=np.concatenate(arrays,axis=1);active=a[:,9]>0;assert not active[32768:].any()
 a=a[active];keys=a[:,[8,11]];assert len(np.unique(keys,axis=0))==len(keys),'Ambiguous particle identity'
 return a[np.lexsort((a[:,8],a[:,11]))]
a,b,c=map(rows,[source,native,repeat]);assert len(a)==len(b)==len(c)>0
assert np.array_equal(a[:,[8,11]],b[:,[8,11]])
assert np.array_equal(b,c),'Native repeat differs beyond slot order'
heads=[int(np.fromfile(p/(prefix+'-head.bin'),'<u4')[0]) for p in [source,native,repeat]];assert heads==[len(a)]*3
metrics={}
for name,cols in [('position_m',[0,1,2]),('age_s',[3]),('velocity_m_s',[4,5,6]),('radius_m',[7]),('life_s',[9]),('water_height_m',[10])]:
 d=np.abs(a[:,cols].astype('float64')-b[:,cols]);metrics[name]={'mean':float(d.mean()),'max':float(d.max()),'p99':float(np.quantile(d,.99))}
report={'source':str(source),'native':str(native),'repeat':str(repeat),'particles':len(a),'kinds':{str(int(k)):int(np.sum(a[:,8]==k)) for k in np.unique(a[:,8])},'identity_exact':True,'native_repeat_multiset_exact':True,'native_active_sha256':hashlib.sha256(b.tobytes()).hexdigest(),'metrics':metrics,'limits':['first emission and integration step only' if integrated else 'first emission step only','source FFT inputs','no coupled shore update or rendering; zero deposits at this initial step' if integrated else 'no native integration deposits or rendering','positions are not bit exact']}
if integrated:
 deposits=[np.fromfile(p/'frame-0001-deposit.bin','<u4') for p in [source,native,repeat]]
 assert all(d.size==768*768 for d in deposits) and all(np.array_equal(deposits[0],d) for d in deposits[1:])
 report['deposit_exact']=True;report['deposit_sum']=int(deposits[0].astype('uint64').sum())
output.write_text(json.dumps(report,indent=2));print(json.dumps(report,indent=2))
