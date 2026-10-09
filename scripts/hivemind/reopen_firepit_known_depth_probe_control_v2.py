from pathlib import Path
import datetime,hashlib,json,struct
W=Path('F:/OpenRPG_ImportWork');CAP=W/'firepit-source-shadow-probe-control-native-v2';OLD=W/'firepit-source-shadow-raster-zero-control-native-v1';ROOT=W/'firepit-source-shadow-probe-control-reopen-v2'
def read(p):return json.loads(Path(p).read_text('utf-8-sig'))
def sha(p):
 with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
if __name__=='__main__':
 assert not ROOT.exists()
 run=read(CAP/'RUN-RESULT.v1.json');assert 'actual_exit_code' in run,run
 prep=read(CAP/'PREPARED.v1.json')
 for p,h in prep['input_pins'].items():assert sha(p)==h,p
 meta=read(CAP/'KNOWN-DEPTH-PROBE-CONTROL.json');expected=[0.25,0.5,0.75,1.0]
 assert meta['expected']==expected and meta['samples']==1048576 and meta['same_gpu_device'] and meta['same_compute_pipeline']
 raw=CAP/'KNOWN-DEPTH-PROBE-CONTROL.float32';data=raw.read_bytes();assert len(data)==4194304
 mismatches=[0]*4
 for i,(value,) in enumerate(struct.iter_unpack('<f',data)):
  if value!=expected[i//262144]:mismatches[i//262144]+=1
 assert mismatches==meta['mismatches_per_layer']
 assert sha(CAP/'CASE.v1.json')==sha(OLD/'CASE.v1.json')
 source=Path(__file__).parent/'candidates/SourceShadowDepthKnownControlV1.rs';assembled=source.with_name('SourceShadowDepthReadbackV5.rs');assert source.read_text('utf-8') in assembled.read_text('utf-8')
 passed=not any(mismatches) and run['actual_exit_code']==0
 files=[Path(__file__).resolve(),source,assembled,CAP/'PREPARED.v1.json',CAP/'RUN-RESULT.v1.json',CAP/'KNOWN-DEPTH-PROBE-CONTROL.json',raw,CAP/'CASE.v1.json',OLD/'CASE.v1.json']
 if passed:
  for v in range(2):
   assert sha(CAP/f'gpu-{v}.rgba16f')==sha(OLD/f'gpu-{v}.rgba16f')
   files.extend([CAP/f'gpu-{v}.rgba16f',OLD/f'gpu-{v}.rgba16f'])
 result=dict(status='known_d32_probe_positive_control_gpu_verified' if passed else 'known_d32_probe_positive_control_refused_retained',created_utc=datetime.datetime.now(datetime.timezone.utc).isoformat(),expected_depths=expected,mismatches_per_layer=mismatches,known_gpu_depth_samples_reopened=1048576,scene_case_unchanged=True,rendered_images_unchanged=2 if passed else 0,full_import=False,full_shadow_fidelity_qualified=False,complete_assets_delivered_increment=0,next_action='Inspect effective caster GPU projection and clip coordinates' if passed else 'Correct depth probe before treating native zero reads as empty atlas',input_pins={str(p):sha(p) for p in files})
 ROOT.mkdir();(ROOT/'RESULT.v1.json').write_text(json.dumps(result,indent=2),'utf-8');print(json.dumps({k:v for k,v in result.items() if k!='input_pins'}))
