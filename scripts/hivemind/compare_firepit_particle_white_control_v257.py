"""Compare all retained Unity/Forge raster pixels without fitted correction."""
from pathlib import Path
import json,hashlib,datetime
import numpy as np
W=Path('F:/OpenRPG_ImportWork');N=W/'firepit-particle-native-mesh-white-control-v255';G=W/'firepit-particle-mesh-white-control-v256';R=W/'firepit-particle-mesh-gpu-reopen-v254';O=W/'firepit-particle-white-control-comparison-v257'
def read(p):return json.loads(p.read_text('utf-8-sig'))
def sha(p):
 with p.open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def main():
 assert not O.exists();nr=read(N/'RUN-RESULT.v1.json');gr=read(G/'RUN-RESULT.v1.json');assert nr['unity_exit_code']==0 and nr['native_geometry_verified'] and gr['gpu_exit_code']==0
 assert sha(N/'INDEPENDENT-REOPEN.v1.json')==nr['independent_receipt_sha256'];assert sha(N/'GEOMETRY.v1.json')==read(N/'INDEPENDENT-REOPEN.v1.json')['capture_sha256']
 reopened=read(R/'RESULT.v1.json');assert reopened['frames']==336 and reopened['stable_repeat_pairs']==168
 old=read(W/'delta-firepit-ra-shader-gpu-v2/PREPARED.v1.json');tolerance=old['tolerance'];assert tolerance==1e-5
 pins={str(p):sha(p) for p in [Path(__file__),N/'RUN-RESULT.v1.json',N/'INDEPENDENT-REOPEN.v1.json',N/'GEOMETRY.v1.json',G/'RUN-RESULT.v1.json',G/'INPUT.v1.json',G/'gpu/RESULT.v1.json',R/'RESULT.v1.json',W/'delta-firepit-ra-shader-gpu-v2/PREPARED.v1.json']}
 native=[]
 for c in read(N/'GEOMETRY.v1.json')['cases']:
  for row in c['rows']:
   for geo in row['geometry']:native.append((c,row,geo))
 fixtures=read(G/'INPUT.v1.json')['fixtures'];renders=read(G/'gpu/RESULT.v1.json')['rows'];assert gr['white_texture_control'];assert len(native)==len(fixtures)==len(renders)==336
 pairs={};repeat_pairs=0;comparisons=[];maxima={'direct':0.,'vertical_flip':0.};differences={'direct':0,'vertical_flip':0}
 for (case,row,geo),fixture,gpu in zip(native,fixtures,renders):
  frame=read(Path(fixture['frame_path']));assert case['node_path']==frame['node_path'] and row['seed']==frame['seed'] and row['requested_time']==frame['requested_time'] and row['repetition']==frame['repetition'] and geo['fixture']==frame['camera_fixture']
  npth=Path(geo['native_pixels_path']);gpth=Path(gpu['gpu_rgba_f16']);assert sha(npth)==geo['native_pixels_sha256'] and sha(gpth)==gpu['gpu_rgba_f16_sha256']
  n=np.frombuffer(npth.read_bytes(),dtype='<f2').astype(np.float32).reshape(256,256,4);g=np.frombuffer(gpth.read_bytes(),dtype='<f2').astype(np.float32).reshape(256,256,4);assert np.isfinite(n).all() and np.isfinite(g).all() and np.all(n[:,:,3]==.25) and np.all(g[:,:,3]==.25)
  key=(frame['node_path'],frame['seed'],frame['requested_time'],frame['camera_fixture']);digests=(sha(npth),sha(gpth))
  if key in pairs:assert pairs[key]==digests,'Native/GPU white control repeat changed';repeat_pairs+=1
  else:pairs[key]=digests
  errors={};counts={}
  for key,image in [('direct',n),('vertical_flip',n[::-1])]:
   delta=np.abs(image-g);errors[key]=float(delta.max());counts[key]=int(np.any(delta>tolerance,axis=2).sum());maxima[key]=max(maxima[key],errors[key]);differences[key]+=counts[key]
  comparisons.append(dict(frame=fixture['frame_path'],maximum_rgba_errors=errors,pixels_over_original_fixture_tolerance=counts));pins[str(npth)]=sha(npth);pins[str(gpth)]=sha(gpth)
 matching=[k for k,v in maxima.items() if v<=tolerance];O.mkdir()
 result=dict(status='336_native_and_gpu_white_texture_control_rasters_compared',input_output_pins=pins,frames=336,rgba_pixels_compared=336*256*256,original_fixture_tolerance=tolerance,maximum_rgba_errors=maxima,pixels_over_tolerance=differences,matching_orientation_hypotheses=matching,white_control_mesh_raster_parity_qualified=len(matching)==1,actual_source_texture_sampling_qualified=False,stable_native_and_gpu_repeat_pairs=repeat_pairs,rows=comparisons,fitted_correction=False,product_renderer_integrated=False,autonomous_simulation=False,full_import=False,finished_utc=datetime.datetime.now(datetime.timezone.utc).isoformat())
 (O/'RESULT.v1.json').write_text(json.dumps(result,indent=2),'utf-8');print(json.dumps({k:v for k,v in result.items() if k not in ['input_output_pins','rows']}))
if __name__=='__main__':main()
