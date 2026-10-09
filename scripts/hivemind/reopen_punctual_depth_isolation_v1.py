"""Depth isolation: source shader equivalence, bias controls, explicit device refusal."""
import hashlib,json
from datetime import datetime,timezone
from pathlib import Path
import numpy as np
W=Path('F:/OpenRPG_ImportWork');R=Path('E:/OpenRPG_Forge/OpenRPG_Forge-Rust');OUT=W/'firepit-punctual-depth-isolation-reopen-v1'
def sha(p):
 with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def data(p):
 a=np.fromfile(p,dtype='<f4');assert a.size==4096**2 and np.isfinite(a).all() and (a>=0).all() and (a<=1).all();return a

def stats(a,b):
 mask=(a!=0)&(b!=0);union=(a!=0)|(b!=0);d=np.abs(a[mask]-b[mask]);ticks=np.abs(np.rint(a[mask]*65535).astype('i4')-np.rint(b[mask]*65535).astype('i4'))
 return {'support_iou':float(mask.sum()/union.sum()),'common_pixels':int(mask.sum()),'max_depth_error':float(d.max()),'mean_depth_error':float(d.mean()),'outside_preexisting_float_threshold_2_lsb':int(np.count_nonzero(d>2/65535)),'integer_depth_steps_over_two':int(np.count_nonzero(ticks>2))}
def main():
 assert not OUT.exists();pins={}
 unity_roots=[W/'source-punctual-atlas-source-gradient-v4',W/'firepit-punctual-source-static-caster-depth-v1',W/'firepit-punctual-source-static-caster-depth-v2']
 for root in unity_roots:
  run=json.loads((root/'RUN-RESULT.v1.json').read_text());assert run['actual_exit_code']==0 and run['input_pins_unchanged']
  gpu=json.loads((root/'SOURCE-GPU.v1.json').read_text());assert gpu['backend']=='Direct3D11' and gpu['format']=='D16_UNorm'
  case=json.loads((root/'PREPARED.v1.json').read_text())
  for p,h in case['input_pins'].items():assert sha(p)==h,p;pins[p]=h
  assert np.count_nonzero(data(root/'inactive-depth.float32'))==0
 native_roots=[W/'source-punctual-atlas-shader-mapped-gradient-v1',W/'firepit-punctual-d16-caster-invariant-v1',W/'firepit-punctual-d16-caster-no-bias-v1']
 for root in native_roots:
  assert int((root/'gpu.exit.txt').read_text())==0
  assert np.count_nonzero(data(root/'inactive-depth.float32'))==0
  if (root/'CASE.v1.json').exists():
   case=json.loads((root/'CASE.v1.json').read_text())
   for p,h in case['input_pins'].items():assert sha(p)==h,p;pins[p]=h
 hdrp_path=W/'firepit-source-point-shadow-raster-v3/SOURCE-SHADOW-DEPTH-0.float32';hdrp=data(hdrp_path);simple_path=unity_roots[1]/'active-depth.float32';simple=data(simple_path);assert np.array_equal(hdrp,simple) and sha(hdrp_path)==sha(simple_path)
 mapped_path=W/'firepit-punctual-d16-caster-shader-mapped-v1/active-depth.float32';mapped=data(mapped_path);invariant=data(native_roots[1]/'active-depth.float32');assert np.array_equal(mapped,invariant)
 s0=data(unity_roots[2]/'active-depth.float32');n0=data(native_roots[2]/'active-depth.float32')
 bias_checks=[]
 for name,zero,biased in [('Unity',s0,simple),('Forge',n0,mapped)]:
  m=(zero!=0)&(biased!=0);delta=(zero[m]-biased[m])*65535
  assert (delta>=0).all()
  bias_checks.append({'backend':name,'common_pixels':int(m.sum()),'reversed_depth_bias_direction_verified':True,'bias_effect_step_quantiles':np.quantile(delta,[0,.01,.25,.5,.75,.99,1]).tolist()})
 gs=data(unity_roots[0]/'active-depth.float32');gn=data(native_roots[0]/'active-depth.float32');delta=np.abs(gs-gn);ticks=np.abs(np.rint(gs*65535).astype('i4')-np.rint(gn*65535).astype('i4'))
 assert np.count_nonzero((gs!=0)!=(gn!=0))==0
 clipping=W/'firepit-punctual-d16-caster-clip-distances-v1';assert int((clipping/'gpu.exit.txt').read_text())==101;assert 'Actual clip distances feature required' in (clipping/'gpu.log').read_text(encoding='utf-8');assert not (clipping/'active-depth.float32').exists()
 for root in unity_roots+native_roots+[clipping]:
  for pattern in ['*.json','*.float32','*.log','*.txt','Cargo.toml','Cargo.lock','src/main.rs']:
   for p in root.glob(pattern):pins[str(p)]=sha(p)
 for p in [hdrp_path,mapped_path,Path(__file__),R/'crates/forge_engine/src/source_graph_punctual_atlas.rs',R/'crates/forge_engine/src/source_graph.rs']:pins[str(p)]=sha(p)
 result={'status':'source_static_shadow_hdrp_depth_bit_exact_verified_bias_not_sole_cause_device_clip_refusal_retained','finished_utc':datetime.now(timezone.utc).isoformat(),'source_static_shadow_equivalence':{'hdrp_reference':str(hdrp_path),'simple_pass':str(simple_path),'samples_bit_exact':16777216,'vertices':3381,'triangles':2819,'submeshes':1,'faces':6,'scope':'captured original LOD0 reference state only'},'gradient_control':{'samples':int(gs.size),'support_differences':0,'max_float_depth_error':float(delta.max()),'mean_float_depth_error':float(delta.mean()),'float_difference_over_prior_2_lsb_threshold':int(np.count_nonzero(delta>2/65535)),'max_integer_depth_step_difference':int(ticks.max()),'threshold_not_relaxed':True},'invariant_position_control':{'samples':16777216,'changed_pixels':0},'bias_direction_controls':bias_checks,'without_bias_pair':stats(s0,n0),'with_bias_pair':stats(simple,mapped),'clip_distances_control':{'actual_exit_code':101,'actual_device_feature_refused':True,'silent_fallback':False,'native_geometry_clipping_verified':False},'full_import':False,'editor_verified':False,'depth_fidelity_qualified':False,'complete_assets_delivered_increment':0,'next_action':'Contrôler les positions clip GPU et traiter le clipping géométrique des faces sans dépendance à une feature optionnelle ; comparer ensuite le récepteur PCF5 et le rendu complet dans les seuils existants','input_pins':pins}
 OUT.mkdir();(OUT/'RESULT.v1.json').write_text(json.dumps(result,indent=2,ensure_ascii=False),encoding='utf-8');print(json.dumps({'status':result['status'],'source_depth_samples_bit_exact':16777216,'unbiased_pair':result['without_bias_pair'],'gradient':result['gradient_control']},ensure_ascii=True))
if __name__=='__main__':main()
