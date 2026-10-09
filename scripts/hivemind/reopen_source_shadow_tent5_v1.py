"""Reopen the bounded PCF5 source capture; no Forge rendering verdict."""
from pathlib import Path
from datetime import datetime,timezone
import hashlib,json
import numpy as np
W=Path('F:/OpenRPG_ImportWork');ROOT=W/'firepit-source-shadow-tent5-v1';OUT=W/'source-shadow-tent5-source-reopen-v1'
def read(p):return json.loads(p.read_text(encoding='utf-8'))
def sha(p):
 with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def main():
 assert not OUT.exists()
 run=read(ROOT/'RUN-RESULT.v1.json');assert run['actual_exit_code']==0 and run['source_inputs_unchanged'] and not run['private_changed']
 protocol=read(ROOT/'TENT5-PROTOCOL.v1.json');assert protocol['expected_cases']==25 and protocol['taps_per_case']==9
 pins=read(ROOT/'PREPARED.v1.json')['input_pins'].copy()
 pins[protocol['source_sdk']]=protocol['source_sdk_sha256'];pins[protocol['compute']]=protocol['compute_sha256']
 for path,h in pins.items():assert sha(path)==h,path
 captures=[];arrays=[]
 for view in range(2):
  p=ROOT/f'SOURCE-TENT5-{view}.rgba32f';a=np.fromfile(p,dtype='<f4').reshape(25,9,4)
  assert np.isfinite(a).all() and (a[:,:,0]>=0).all() and (a[:,:,3]==0).all()
  sums=a[:,:,0].sum(axis=1);assert np.max(np.abs(sums-1))<2e-6
  captures.append({'view':view,'observations':225,'scalars':int(a.size),'weight_sum_max_error':float(np.max(np.abs(sums-1))),'sha256':sha(p)})
  arrays.append(a);pins[str(p)]=sha(p)
 assert np.array_equal(arrays[0].view('u4'),arrays[1].view('u4'))
 for p in [ROOT/'RUN-RESULT.v1.json',ROOT/'TENT5-PROTOCOL.v1.json',ROOT/'PREPARED.v1.json',Path(__file__)]:pins[str(p)]=sha(p)
 result={'status':'source_pcf5_gpu_capture_reopened','sub_lot':'L01.PCF5.1','finished_utc':datetime.now(timezone.utc).isoformat(),'cases':25,'taps_per_case':9,'gpu_observations_reopened':450,'captures':captures,'source_dependencies_unchanged':True,'private_changed':[],'two_captures_bit_identical':True,'forge_gpu_math_verified':False,'atlas_receiver_integrated':False,'depth_fidelity_qualified':False,'editor_verified':False,'full_import':False,'complete_assets_delivered_increment':0,'next_action':'L01.PCF5.2: comparer les poids et UV du calcul Forge sur GPU aux seuils du protocole source; conserver le refus de profondeur','input_pins':pins}
 OUT.mkdir();(OUT/'RESULT.v1.json').write_text(json.dumps(result,indent=2,ensure_ascii=False),encoding='utf-8');print(json.dumps({k:result[k] for k in ['status','gpu_observations_reopened','complete_assets_delivered_increment']},ensure_ascii=True))
if __name__=='__main__':main()
