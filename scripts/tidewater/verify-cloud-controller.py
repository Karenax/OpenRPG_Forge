"""Compare every native CPU frame/settings/public word with the original acquisition."""
from pathlib import Path
import hashlib,json,sys
import numpy as np
assert len(sys.argv)==4
source,native,out=map(Path,sys.argv[1:]);assert not out.exists()
meta=json.loads((source/('controller.json' if (source/'controller.json').exists() else 'environment.json')).read_text());probe=json.loads((native/'probe.json').read_text())
assert probe['native_controller'] and not probe['source_frames_used_as_inputs']
steps=len(meta.get('controller_frames',[])) or 120
report={'steps':steps,'exact_files':0,'different_files':[],'source_frames_used_as_inputs':False}
for step in range(1,steps+1):
 for kind,size in [('frame',544),('settings',208),('public',80)]:
  name=f'cloud-step{step:03}-{kind}.bin';a=(source/name).read_bytes();b=(native/name).read_bytes()
  assert hashlib.sha256(a).hexdigest()==meta['files'][name] and len(a)==len(b)==size
  x=np.frombuffer(a,'<f4');y=np.frombuffer(b,'<f4');assert np.isfinite(x).all() and np.isfinite(y).all()
  if a==b:report['exact_files']+=1
  else:
   indices=np.where(np.frombuffer(a,'<u4')!=np.frombuffer(b,'<u4'))[0]
   report['different_files'].append({'file':name,'words':indices.tolist(),'max_absolute':float(np.abs(x.astype('float64')-y).max()),'source':x[indices].tolist(),'native':y[indices].tolist()})
out.write_text(json.dumps(report,indent=2));print(json.dumps({'exact_files':report['exact_files'],'different_files':len(report['different_files']),'first_differences':report['different_files'][:2]},indent=2))
