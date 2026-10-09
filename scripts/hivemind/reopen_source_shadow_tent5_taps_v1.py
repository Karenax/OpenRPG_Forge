"""Identify which PCF5 tap differs, retaining atlas/scene fidelity refusals."""
from pathlib import Path
from datetime import datetime,timezone
import hashlib,json
import numpy as np
W=Path('F:/OpenRPG_ImportWork')
N=W/'firepit-punctual-d16-tent5-taps-v1'
S=W/'firepit-source-d16-tent5-taps-v1'
OUT=W/'source-shadow-tent5-taps-reopen-v1'
def sha(p):
 with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def read(p):return json.loads(p.read_text(encoding='utf-8'))
def main():
 assert not OUT.exists()
 assert int((N/'gpu.exit.txt').read_text())==0
 assert read(S/'RUN-RESULT.v1.json')['actual_exit_code']==0
 assert read(S/'RUN-RESULT.v1.json')['input_pins_unchanged']
 log=(S/'Unity.log').read_text(encoding='utf-8')
 assert 'Shader error' not in log and 'is invalid' not in log
 case=read(N/'CASE.v1.json')
 pins={**read(S/'PREPARED.v1.json')['input_pins'],**case['input_pins']}
 for p,h in pins.items():assert sha(p)==h,p
 for root,old in [(N,W/'firepit-punctual-d16-tent5-receiver-v1'),(S,W/'firepit-source-d16-tent5-receiver-v3')]:
  for state in ['active','inactive']:assert sha(root/f'{state}-depth.float32')==sha(old/f'{state}-depth.float32')
 rows=[];isolated=[]
 for state in ['active','inactive']:
  a=np.fromfile(S/f'{state}-receiver.rgba32f',dtype='<f4').reshape(1458,9,4)
  b=np.fromfile(N/f'{state}-receiver.rgba32f',dtype='<f4').reshape(1458,9,4)
  assert np.isfinite(a).all() and np.isfinite(b).all()
  delta=np.abs(a-b)
  math_limit=case['absolute_weight_uv_threshold']
  assert np.max(delta[:,:,:3])<=math_limit
  bad=np.argwhere(delta[:,:,3]>case['absolute_comparison_threshold'])
  if state=='inactive':assert len(bad)==0 and (a[:,:,3]==1).all() and (b[:,:,3]==1).all()
  prior_s=np.fromfile(W/f'firepit-source-d16-tent5-receiver-v3/{state}-receiver.rgba32f',dtype='<f4').reshape(1458,4)
  prior_n=np.fromfile(W/f'firepit-punctual-d16-tent5-receiver-v1/{state}-receiver.rgba32f',dtype='<f4').reshape(1458,4)
  sums_s=np.sum(a[:,:,0].astype('f8')*a[:,:,3],axis=1)
  sums_n=np.sum(b[:,:,0].astype('f8')*b[:,:,3],axis=1)
  for c,t in bad:
   c=int(c);t=int(t)
   isolated.append({'state':state,'case_index':c,'face':c//243,'tap_index':t,'weight_source':float(a[c,t,0]),'weight_native':float(b[c,t,0]),'uv_source':[float(v) for v in a[c,t,1:3]],'uv_native':[float(v) for v in b[c,t,1:3]],'comparison_source':float(a[c,t,3]),'comparison_native':float(b[c,t,3]),'weighted_visibility_delta':float(b[c,t,0]*b[c,t,3]-a[c,t,0]*a[c,t,3]),'prior_visibility_delta':float(prior_n[c,0]-prior_s[c,0])})
  rows.append({'state':state,'cases':1458,'taps':13122,'max_weight_error':float(np.max(delta[:,:,0])),'max_uv_error':float(np.max(delta[:,:,1:3])),'comparison_taps_outside_threshold':len(bad),'comparison_cases_outside_threshold':len(set(int(c) for c,_ in bad)),'max_comparison_error':float(np.max(delta[:,:,3])),'source_sum_reconstruction_max_error':float(np.max(np.abs(sums_s-prior_s[:,0]))),'native_sum_reconstruction_max_error':float(np.max(np.abs(sums_n-prior_n[:,0])))})
 audit=read(W/'source-shadow-tent5-atlas-difference-audit-v1/RESULT.v1.json')
 assert isolated and {r['case_index'] for r in isolated}=={audit['case_index']}
 for root in [S,N]:
  for pattern in ['*.json','*.log','*.txt','*.float32','*.rgba32f']:
   for p in root.glob(pattern):pins[str(p)]=sha(p)
 for p in [Path(__file__),W/'source-shadow-tent5-atlas-difference-audit-v1/RESULT.v1.json']:pins[str(p)]=sha(p)
 result={'status':'nine_tap_weights_uv_verified_single_depth_comparison_difference_retained','sub_lot':'L01.PCF5.3.TAPS','finished_utc':datetime.now(timezone.utc).isoformat(),'measurements':rows,'isolated_taps':isolated,'atlas_depths_unchanged':True,'weight_uv_math_qualified_for_grid':True,'comparison_sampling_fidelity_qualified':False,'source_pixels_used_as_render_input':False,'depth_fidelity_qualified':False,'scene_fidelity_qualified':False,'editor_verified':False,'full_import':False,'complete_assets_delivered_increment':0,'next_action':'Relier le tap refuse au texel de profondeur absent dans Forge, puis raccorder le recepteur commun a la scene source et comparer les images sans injecter les profondeurs Unity','input_pins':pins}
 OUT.mkdir();(OUT/'RESULT.v1.json').write_text(json.dumps(result,indent=2,ensure_ascii=False),encoding='utf-8')
 print(json.dumps({k:result[k] for k in ['status','measurements','isolated_taps']},ensure_ascii=True))
if __name__=='__main__':main()
