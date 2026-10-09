"""Independent scalar/pixel reopening, without qualifying the refused depth fidelity."""
from pathlib import Path
from datetime import datetime,timezone
import hashlib,json
import numpy as np
W=Path('F:/OpenRPG_ImportWork');R=Path('E:/OpenRPG_Forge/OpenRPG_Forge-Rust');OUT=W/'firepit-punctual-projection-clipping-reopen-v1'
def sha(p):
 with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def read(p):return json.loads(p.read_text(encoding='utf-8'))
def depths(p):
 a=np.fromfile(p,dtype='<f4');assert a.size==4096**2 and np.isfinite(a).all();return a
def main():
 assert not OUT.exists()
 source=W/'firepit-punctual-source-caster-clip-coordinates-v1';native=W/'firepit-punctual-native-caster-clip-coordinates-v1';clipped=W/'firepit-punctual-d16-caster-face-clipped-v2'
 assert read(source/'RUN-RESULT.v1.json')['actual_exit_code']==0
 assert read(source/'RUN-RESULT.v1.json')['input_pins_unchanged']
 assert int((native/'gpu.exit.txt').read_text())==0 and int((clipped/'gpu.exit.txt').read_text())==0
 pins={}
 for root,name in [(source,'PREPARED.v1.json'),(native,'CASE.v1.json'),(clipped,'CASE.v1.json')]:
  for p,h in read(root/name)['input_pins'].items():assert sha(p)==h,p;pins[p]=h
 rows=[]
 for face in range(6):
  a=np.fromfile(source/f'clip-face-{face}.float32',dtype='<f4');b=np.fromfile(native/f'clip-face-{face}.float32',dtype='<f4')
  assert a.size==3381*4 and b.size==a.size and np.isfinite(a).all()
  assert np.array_equal(a.view('u4'),b.view('u4'))
  rows.append({'face':face,'scalars':int(a.size),'bit_mismatches':0,'source_sha256':sha(source/f'clip-face-{face}.float32')})
 original=W/'firepit-source-point-shadow-raster-v3/SOURCE-SHADOW-DEPTH-0.float32'
 assert sha(source/'active-depth.float32')==sha(original)
 case=read(clipped/'CASE.v1.json');assert case['input_clips_produced_by_forge_gpu'] and not case['source_unity_clips_used_as_render_input'] and not case['source_pixels_used_as_render_input']
 for stat in case['face_statistics']:
  data=np.fromfile(clipped/f"face-{stat['face']}.vertices.float32",dtype='<f4').reshape(-1,4)
  assert len(data)==stat['output_vertices'] and len(data)%3==0 and np.isfinite(data).all()
  if data.size:
   # Interpolation rounding at a clipped plane is recorded explicitly.
   distances=np.stack([data[:,3]+data[:,0],data[:,3]-data[:,0],data[:,3]+data[:,1],data[:,3]-data[:,1]])
   assert float(distances.min())>=-2e-6
  if stat['face']==2:assert len(data)==0
 gpu=read(clipped/'GPU-RESULT.v1.json')
 assert gpu['measurements'][0]['draws']==5 and gpu['measurements'][1]['draws']==0
 assert np.count_nonzero(depths(clipped/'inactive-depth.float32'))==0
 ref=depths(original);n=depths(clipped/'active-depth.float32');old=depths(W/'firepit-punctual-d16-caster-shader-mapped-v1/active-depth.float32')
 both=(ref!=0)&(n!=0);union=(ref!=0)|(n!=0);delta=np.abs(ref[both]-n[both]);threshold=case['predeclared_max_depth_error'];assert threshold==2/65535
 refusal=W/'firepit-punctual-d16-caster-face-clipped-v1';assert int((refusal/'gpu.exit.txt').read_text())==101 and 'buffer slices can not be empty' in (refusal/'gpu.log').read_text(encoding='utf-8')
 for root in [source,native,clipped,refusal]:
  for pattern in ['*.json','*.float32','*.log','*.txt','src/main.rs','Cargo.toml','Cargo.lock']:
   for p in root.glob(pattern):pins[str(p)]=sha(p)
 for p in [original,R/'crates/forge_engine/src/source_graph_punctual_atlas.rs',R/'crates/forge_engine/src/source_graph.rs',Path(__file__)]:pins[str(p)]=sha(p)
 result={'status':'gpu_projection_scalars_bit_exact_verified_face_clipping_replay_measured_depth_refused','finished_utc':datetime.now(timezone.utc).isoformat(),'projection_coordinates':{'vertices':3381,'faces':6,'scalars_bit_exact':81144,'readback_stage':'compute expressions corresponding to vertex projection; not direct vertex-stage feedback','faces_reopened':rows},'source_depth_reference_unchanged':True,'face_xy_clipping':{'input_generated_by_forge_gpu':True,'unity_reference_render_input':False,'source_depth_pixels_render_input':False,'face_statistics':case['face_statistics'],'nonempty_faces_drawn':5,'empty_face_drawn':False,'inactive_atlas_zero':True,'initial_empty_buffer_refusal_retained':True,'mapped_pixels_changed':int(np.count_nonzero(old!=n))},'depth_comparison':{'support_iou':float(both.sum()/union.sum()),'max_depth_error':float(delta.max()),'mean_depth_error':float(delta.mean()),'predeclared_max_depth_error':threshold,'pixels_outside_threshold':int(np.count_nonzero(delta>threshold)),'clipping_sufficient_to_resolve_depth_fidelity':False},'depth_fidelity_qualified':False,'full_import':False,'editor_verified':False,'complete_assets_delivered_increment':0,'next_action':'Mesurer la transformation effective des sommets dans le viewport et raccorder le récepteur ponctuel PCF5 pour comparer le rendu final dans les seuils existants ; conserver le refus technique de profondeur','input_pins':pins}
 OUT.mkdir();(OUT/'RESULT.v1.json').write_text(json.dumps(result,indent=2,ensure_ascii=False),encoding='utf-8')
 print(json.dumps({'status':result['status'],'projection_scalars_bit_exact':81144,'depth_comparison':result['depth_comparison']},ensure_ascii=True))
if __name__=='__main__':main()
