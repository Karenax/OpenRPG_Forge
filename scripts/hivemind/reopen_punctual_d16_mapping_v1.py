"""Current mapping revision: source-identity controls and strict depth refusal retained."""
import hashlib,json,struct
from datetime import datetime,timezone
from pathlib import Path
import numpy as np
W=Path('F:/OpenRPG_ImportWork');R=Path('E:/OpenRPG_Forge/OpenRPG_Forge-Rust');OUT=W/'source-punctual-atlas-mapped-reopen-v1'
def sha(p):
 with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def data(p):
 a=np.fromfile(p,dtype='<f4');assert a.size==4096**2 and np.isfinite(a).all();return a

def main():
 assert not OUT.exists()
 mapped=W/'source-punctual-atlas-shader-mapped-gpu-v1';mesh=W/'firepit-punctual-d16-caster-shader-mapped-v1';source=W/'source-punctual-atlas-source-coverage-v3'
 assert int((mapped/'engine-check.exit.txt').read_text())==0
 assert '5 passed; 0 failed' in (mapped/'tests.log').read_text(encoding='utf-8')
 assert int((mesh/'gpu.exit.txt').read_text())==0
 pins={};historical=[]
 for root,key in [(source,'PREPARED.v1.json'),(mesh,'CASE.v1.json')]:
  case=json.loads((root/key).read_text(encoding='utf-8'))
  for path,h in case['input_pins'].items():assert sha(path)==h,path;pins[path]=h
 for name in ['original-y-ccw','flipped-y-cw','flipped-y-ccw']:
  case_root=W/'firepit-punctual-d16-convention-controls-v1'/name
  assert int((case_root/'gpu.exit.txt').read_text())==0
  case=json.loads((case_root/'CASE.v1.json').read_text())
  for path,h in case['input_pins'].items():
   if Path(path).name=='source_graph_punctual_atlas.rs':
    saved=W/'source-punctual-atlas-mapped-code-before-v1/source_graph_punctual_atlas.rs';assert sha(saved)==h
    historical.append({'case':name,'path':path,'expected_sha256':h,'retained_revision':str(saved)});pins[str(saved)]=h
   else:assert sha(path)==h,path;pins[path]=h
 source_gpu=json.loads((source/'SOURCE-GPU.v1.json').read_text());assert source_gpu['backend']=='Direct3D11' and source_gpu['format']=='D16_UNorm'
 rows=[]
 for name in ['active-depth.float32','inactive-depth.float32']:
  a=data(source/name);b=data(mapped/name);assert np.array_equal(a,b) and sha(source/name)==sha(mapped/name)
  if name.startswith('inactive'):assert np.count_nonzero(a)==0
  rows.append({'name':name,'samples':int(a.size),'bit_mismatches':0,'sha256':sha(source/name)})
 src=data(W/'firepit-source-point-shadow-raster-v3/SOURCE-SHADOW-DEPTH-0.float32')
 comparisons=[]
 for name in ['original-y-ccw','flipped-y-cw','flipped-y-ccw']:
  root=W/'firepit-punctual-d16-convention-controls-v1'/name;n=data(root/'active-depth.float32');both=(src!=0)&(n!=0);union=(src!=0)|(n!=0);d=np.abs(src[both]-n[both])
  comparisons.append({'case':name,'support_iou':float(both.sum()/union.sum()),'depth_max':float(d.max()),'depth_mae':float(d.mean())})
 n=data(mesh/'active-depth.float32');both=(src!=0)&(n!=0);union=(src!=0)|(n!=0);difference=np.abs(src[both]-n[both]);threshold=json.loads((mesh/'CASE.v1.json').read_text())['predeclared_max_d16_depth_difference']
 assert threshold==2/65535
 assert np.count_nonzero(data(mesh/'inactive-depth.float32'))==0
 old_mesh=W/'firepit-punctual-d16-caster-replay-v1'
 for name in ['vertices.float32','indices.uint32']:assert sha(old_mesh/name)==sha(mesh/name)
 raster=json.loads((W/'firepit-source-point-shadow-raster-v3/SOURCE-SHADOW-RASTER-0.json').read_text());requests=json.loads(raster['requests_json'])
 for i,request in enumerate(requests):
  bits=(mesh/f'face-{i}.uniform').read_bytes();assert len(bits)==112 and bits[:80]==(old_mesh/f'face-{i}.uniform').read_bytes()
  values=[request['dynamicAtlasViewport'][k] for k in ['x','y','width','height']]+[4096.,4096.,0.,0.]
  assert bits[80:]==struct.pack('<8f',*values)
 for root in [source,mapped,mesh,W/'firepit-punctual-d16-convention-controls-v1',*(W/'firepit-punctual-d16-convention-controls-v1'/x for x in ['original-y-ccw','flipped-y-cw','flipped-y-ccw'])]:
  for pattern in ['*.json','*.float32','*.log','*.txt','src/main.rs','Cargo.toml','Cargo.lock']:
   for path in root.glob(pattern):pins[str(path)]=sha(path)
 for path in [R/'crates/forge_engine/src/source_graph_punctual_atlas.rs',R/'crates/forge_engine/src/source_graph.rs',W/'firepit-source-point-shadow-raster-v3/SOURCE-SHADOW-DEPTH-0.float32',Path(__file__)]:pins[str(path)]=sha(path)
 result={'status':'common_fractional_d16_mapping_source_numeric_bit_exact_native_compile_verified_caster_depth_refused','finished_utc':datetime.now(timezone.utc).isoformat(),'mapping_contract_tests':5,'engine_native_compile_verified':True,'numeric_source_gpu_comparisons':rows,'numeric_fractional_coverage_qualified':True,'historical_control_revision_pins':historical,'orientation_controls':comparisons,'raw_source_geometry_convention':{'y_flip':False,'front_face':'Ccw','scope':'original source-space positions/indices only; no target GLB orientation inference'},'mapped_caster':{'vertices':3381,'triangles':2819,'face_draws':6,'support_iou':float(both.sum()/union.sum()),'support_differences':int(np.count_nonzero((src!=0)!=(n!=0))),'max_depth_error_on_common_support':float(difference.max()),'mean_depth_error_on_common_support':float(difference.mean()),'predeclared_max_depth_error':threshold,'depth_pixels_outside_threshold':int(np.count_nonzero(difference>threshold)),'plain_geometry_depth_only':True,'full_source_shader_verified':False,'depth_fidelity_qualified':False},'full_import':False,'editor_verified':False,'complete_assets_delivered_increment':0,'web_current_revision_verified':False,'next_action':'Identifier le biais et la passe shader source responsables des profondeurs restantes, puis raccorder la passe et le récepteur ponctuels communs ; vérifier Web et Studio','input_pins':pins}
 OUT.mkdir();(OUT/'RESULT.v1.json').write_text(json.dumps(result,indent=2,ensure_ascii=False),encoding='utf-8')
 print(json.dumps({k:result[k] for k in ['status','mapped_caster']},ensure_ascii=True))
if __name__=='__main__':main()
