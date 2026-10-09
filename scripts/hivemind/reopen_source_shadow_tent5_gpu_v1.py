"""Compare actual Unity SDK and native WGSL GPU tent5 readbacks; limited math scope."""
from pathlib import Path
import datetime,hashlib,json,math,struct
W=Path('F:/OpenRPG_ImportWork');ROOT=W/'source-shadow-tent5-gpu-reopen-v1'
def sha(p):
 with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def read(p):return json.loads(p.read_text())
def main():
 assert not ROOT.exists();ROOT.mkdir()
 result={'status':'tent5_reopen_refused','full_import':False,'complete_assets_delivered_increment':0,'full_shadow_sampling_verified':False,'scene_fidelity_verified':False,'editor_visual_verified':False}
 try:
  source=W/'firepit-source-shadow-tent5-v1';native=W/'source-shadow-tent5-gpu-check-v1'
  sr=read(source/'RUN-RESULT.v1.json');nr=read(native/'RUN-RESULT.v1.json')
  assert sr['actual_exit_code']==0 and nr['actual_exit_code']==0
  protocol=read(source/'TENT5-PROTOCOL.v1.json');assert protocol['expected_cases']==25 and protocol['taps_per_case']==9
  pins={}
  for root in [source,native]:
   pins.update(read(root/'PREPARED.v1.json')['input_pins'])
  pins[protocol['source_sdk']]=protocol['source_sdk_sha256'];pins[protocol['compute']]=protocol['compute_sha256']
  changed=[p for p,h in pins.items() if sha(p)!=h];assert not changed,changed
  source_bytes=[(source/f'SOURCE-TENT5-{i}.rgba32f').read_bytes() for i in range(2)]
  target_bytes=(native/'NATIVE-TENT5.rgba32f').read_bytes()
  assert source_bytes[0]==source_bytes[1];assert all(len(b)==3600 for b in [*source_bytes,target_bytes])
  a=struct.unpack('<900f',source_bytes[0]);b=struct.unpack('<900f',target_bytes)
  assert all(math.isfinite(x) for x in [*a,*b]);assert all(a[i]==b[i]==0 for i in range(3,900,4))
  weight=max(abs(a[i]-b[i]) for i in range(0,900,4));uv=max(abs(a[i]-b[i]) for i in range(900) if i%4 in [1,2])
  assert weight<=protocol['absolute_weight_threshold'] and uv<=protocol['absolute_uv_threshold'],(weight,uv)
  original_images=[]
  for i in range(2):
   p=source/f'linear-{i}.rgba16f';q=W/f'firepit-shadow-source-reference-v8/linear-{i}.rgba16f'
   assert sha(p)==sha(q);original_images.append({'current':str(p),'original':str(q),'sha256':sha(p)})
  sums=[sum(a[c*36+i*4] for i in range(9)) for c in range(25)]
  result.update(sub_lot='L01.PCF5.2',status='actual_source_native_tent5_gpu_weights_uv_verified',cases_verified=25,taps_per_case=9,observations_verified=450,weights_per_view=225,uv_components_per_view=450,max_absolute_weight_error=weight,max_absolute_uv_error=uv,thresholds={'weight':protocol['absolute_weight_threshold'],'uv':protocol['absolute_uv_threshold']},source_weight_sum_range=[min(sums),max(sums)],source_coefficient_not_normalized=True,input_pins_verified=len(pins),input_pins=pins,source_dependency_count=len(read(source/'PREPARED.v1.json')['dependency_paths']),original_linear_images_unchanged=original_images,raw_sha256={str(source/f'SOURCE-TENT5-{i}.rgba32f'):sha(source/f'SOURCE-TENT5-{i}.rgba32f') for i in range(2)},native_raw_sha256=sha(native/'NATIVE-TENT5.rgba32f'),next_action='Apply verified tent5 weights/UV to explicit source shadow sampling; qualify comparison, normal and raster bias separately, then compare the original scene.')
 except Exception as error:result['error']=repr(error)
 result['finished_utc']=datetime.datetime.now(datetime.timezone.utc).isoformat();(ROOT/'RESULT.v1.json').write_text(json.dumps(result,indent=2),'utf-8');print(json.dumps({k:v for k,v in result.items() if k!='input_pins'}))
if __name__=='__main__':main()
