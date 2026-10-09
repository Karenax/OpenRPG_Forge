"""Reopen native and source atlas sampling; retain the separate depth refusal."""
from pathlib import Path
from datetime import datetime,timezone
import hashlib,json
import numpy as np
W=Path('F:/OpenRPG_ImportWork')
OUT=W/'source-shadow-tent5-atlas-reopen-v1'
NATIVE=W/'firepit-punctual-d16-tent5-receiver-v1'
SOURCE=W/'firepit-source-d16-tent5-receiver-v3'
PROTOCOL=NATIVE/'RECEIVER-COMPARISON-PROTOCOL.v1.json'
def read(p):return json.loads(p.read_text(encoding='utf-8'))
def sha(p):
 with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def main():
 assert not OUT.exists()
 assert read(SOURCE/'RUN-RESULT.v1.json')['actual_exit_code']==0
 assert read(SOURCE/'RUN-RESULT.v1.json')['input_pins_unchanged']
 assert int((NATIVE/'gpu.exit.txt').read_text())==0
 protocol=read(PROTOCOL)
 pins=read(SOURCE/'PREPARED.v1.json')['input_pins'].copy()
 pins.update(read(NATIVE/'CASE.v1.json')['input_pins'])
 for p,h in pins.items():assert sha(p)==h,p
 log=(SOURCE/'Unity.log').read_text(encoding='utf-8')
 assert 'Shader error' not in log and 'is invalid' not in log
 source_depth=W/'firepit-source-point-shadow-raster-v3/SOURCE-SHADOW-DEPTH-0.float32'
 native_depth=W/'firepit-punctual-d16-caster-shader-mapped-v1/active-depth.float32'
 assert sha(SOURCE/'active-depth.float32')==sha(source_depth)
 assert sha(NATIVE/'active-depth.float32')==sha(native_depth)
 cases=np.fromfile(NATIVE/'receiver-cases.float32',dtype='<f4').reshape(1458,4)
 measurements=[]
 for state in ['active','inactive']:
  a=np.fromfile(SOURCE/f'{state}-receiver.rgba32f',dtype='<f4').reshape(1458,4)
  b=np.fromfile(NATIVE/f'{state}-receiver.rgba32f',dtype='<f4').reshape(1458,4)
  assert np.isfinite(a).all() and np.isfinite(b).all()
  assert np.array_equal(a[:,1:].view('u4'),cases[:,:3].view('u4'))
  assert np.array_equal(b[:,1:].view('u4'),cases[:,:3].view('u4'))
  assert (a[:,0]>=-2e-6).all() and (a[:,0]<=1+2e-6).all()
  delta=np.abs(a[:,0]-b[:,0]);limit=protocol['absolute_visibility_threshold']
  if state=='inactive':
   assert np.max(np.abs(a[:,0]-1))<2e-6 and np.max(np.abs(b[:,0]-1))<2e-6
   assert np.count_nonzero(np.fromfile(SOURCE/'inactive-depth.float32',dtype='<f4'))==0
   assert np.count_nonzero(np.fromfile(NATIVE/'inactive-depth.float32',dtype='<f4'))==0
  measurements.append({'state':state,'cases':1458,'max_absolute_visibility_error':float(delta.max()),'mean_absolute_visibility_error':float(delta.mean()),'cases_outside_threshold':int(np.count_nonzero(delta>limit)),'native_shadowed_cases':int(np.count_nonzero(b[:,0]<.99999)),'source_shadowed_cases':int(np.count_nonzero(a[:,0]<.99999))})
 refusals=[]
 for version in [1,2]:
  root=W/f'firepit-source-d16-tent5-receiver-v{version}'
  assert read(root/'RUN-RESULT.v1.json')['actual_exit_code']==1
  assert "cannot implicitly convert from 'Texture2D<float>' to 'Texture2D<float4>'" in (root/'Unity.log').read_text(encoding='utf-8')
  refusals.append(str(root))
 for root in [SOURCE,NATIVE,*[Path(r) for r in refusals]]:
  for pattern in ['*.json','*.float32','*.rgba32f','*.log','*.txt']:
   for p in root.glob(pattern):pins[str(p)]=sha(p)
 for p in [Path(__file__),PROTOCOL,source_depth,native_depth]:pins[str(p)]=sha(p)
 sampling_ok=all(r['cases_outside_threshold']==0 for r in measurements)
 result={'status':'native_source_pcf5_atlas_sampling_reopened' if sampling_ok else 'native_pcf5_atlas_receiver_executed_source_sampling_difference_retained','sub_lot':'L01.PCF5.3','finished_utc':datetime.now(timezone.utc).isoformat(),'native_receiver_executed':True,'source_receiver_executed':True,'measurements':measurements,'thresholds':protocol,'original_source_depth_unchanged':True,'native_depth_unchanged':True,'inactive_atlases_clear':True,'source_depth_pixels_used_as_render_input':False,'initial_compile_refusals_retained':refusals,'control_sampler_failure_was_not_a_valid_gpu_measurement':True,'atlas_control_sampling_qualified':sampling_ok,'source_receiver_face_projection_qualified':False,'depth_fidelity_qualified':False,'scene_fidelity_qualified':False,'editor_verified':False,'full_import':False,'complete_assets_delivered_increment':0,'next_action':'Raccorder le recepteur a la scene commune avec selection des faces, projection et biais normaux source; comparer les images originales, garder le refus de profondeur','input_pins':pins}
 OUT.mkdir();(OUT/'RESULT.v1.json').write_text(json.dumps(result,indent=2,ensure_ascii=False),encoding='utf-8')
 print(json.dumps({k:result[k] for k in ['status','measurements','atlas_control_sampling_qualified','complete_assets_delivered_increment']},ensure_ascii=True))
if __name__=='__main__':main()
