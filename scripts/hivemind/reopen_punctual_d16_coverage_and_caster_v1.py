"""Reopen actual source coverage and source-mesh depth replay without qualifying fidelity."""
import array,hashlib,json
from pathlib import Path
from datetime import datetime,timezone
W=Path('F:/OpenRPG_ImportWork');OUT=W/'firepit-punctual-d16-caster-reopen-v1'
def sha(p):
 with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def floats(p):
 a=array.array('f');a.frombytes(p.read_bytes());assert len(a)==4096**2;return a

def main():
 assert not OUT.exists();OUT.mkdir()
 pins={}
 source=W/'source-punctual-atlas-source-coverage-v2'
 run=json.loads((source/'RUN-RESULT.v1.json').read_text());assert run['actual_exit_code']==0 and run['input_pins_unchanged']
 for root in [source,W/'firepit-punctual-d16-caster-replay-v1']:
  prepared=json.loads((root/'PREPARED.v1.json').read_text())
  for p,h in prepared['input_pins'].items():assert sha(p)==h,p;pins[p]=h
 src=floats(source/'active-depth.float32');inactive=floats(source/'inactive-depth.float32')
 assert inactive.count(0)==len(inactive);assert src.count(0.2500038146972656)==2731*4096
 for y in range(4096):
  row=src[y*4096:(y+1)*4096];assert row[:2731].count(0.2500038146972656)==2731 and row[2731:].count(0)==1365
 comparisons=[]
 for name in ['source-punctual-atlas-contract-v1','source-punctual-atlas-fragment-control-v1']:
  root=W/name;target=floats(root/'active-depth.float32');assert floats(root/'inactive-depth.float32').count(0)==len(src)
  mismatch=[i for i,(a,b) in enumerate(zip(src,target,strict=True)) if a!=b]
  assert mismatch==[y*4096+2730 for y in range(4096)]
  comparisons.append({'root':str(root),'samples':len(src),'source_differences':len(mismatch),'mismatch_x':2730,'inactive_zero':True})
 native_path=W/'firepit-punctual-d16-caster-replay-v1/active-depth.float32'
 actual_source_path=W/'firepit-source-point-shadow-raster-v3/SOURCE-SHADOW-DEPTH-0.float32'
 native=floats(native_path);reference=floats(actual_source_path)
 source_nonzero=len(reference)-reference.count(0);native_nonzero=len(native)-native.count(0)
 both=sum(a!=0 and b!=0 for a,b in zip(reference,native,strict=True));union=source_nonzero+native_nonzero-both
 errors=[abs(a-b) for a,b in zip(reference,native,strict=True) if a!=0 and b!=0]
 for root in [source,W/'source-punctual-atlas-source-coverage-v1',W/'source-punctual-atlas-contract-v1',W/'source-punctual-atlas-fragment-control-v1',W/'firepit-punctual-d16-caster-replay-v1']:
  for pattern in ['*.json','*.float32','*.log','src/main.rs','Cargo.toml','Cargo.lock']:
   for p in root.glob(pattern):pins[str(p)]=sha(p)
 pins[str(actual_source_path)]=sha(actual_source_path);pins[str(Path(__file__))]=sha(Path(__file__))
 result={'status':'source_d3d11_fractional_edge_difference_confirmed_original_lod0_d16_depth_replay_measured_fidelity_refused','finished_utc':datetime.now(timezone.utc).isoformat(),'numeric_source_samples':2*len(src),'source_numeric_active_pixels':2731*4096,'hardware_viewport_comparisons':comparisons,'dummy_fragment_resolves_edge':False,'initial_unity_clear_convention_control_retained':True,'caster_replay':{'source_vertices':3381,'source_triangles':2819,'actual_face_draws':6,'source_depth_nonzero':source_nonzero,'native_depth_nonzero':native_nonzero,'support_intersection':both,'support_union':union,'support_iou':both/union if union else 0,'max_depth_difference_on_intersection':max(errors) if errors else None,'depth_mae_on_intersection':sum(errors)/len(errors) if errors else None,'plain_geometry_depth_only':True,'full_source_shadow_shader_qualified':False,'gpu_field_ideal_coverage_mismatches_counts_invalid_depth_only':True},'fractional_coverage_qualified':False,'receiver_filter_verified':False,'editor_verified':False,'full_import':False,'complete_assets_delivered_increment':0,'next_action':'Corriger la couverture fractionnaire sans tronquer les viewports ; qualifier conventions Y et winding sur les profondeurs des géométries source avant raccordement du récepteur PCF5','input_pins':pins}
 (OUT/'RESULT.v1.json').write_text(json.dumps(result,indent=2,ensure_ascii=False),encoding='utf-8')
 print(json.dumps({k:result[k] for k in ['status','caster_replay']},ensure_ascii=True))
if __name__=='__main__':main()
