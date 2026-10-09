"""Independent reopening of D16 infrastructure; asset fidelity remains refused."""
import array, hashlib, json, struct
from datetime import datetime, timezone
from pathlib import Path
W=Path('F:/OpenRPG_ImportWork/source-punctual-atlas-contract-v1')
R=Path('E:/OpenRPG_Forge/OpenRPG_Forge-Rust')
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def main():
 gpu=json.loads((W/'GPU-RESULT.v1.json').read_text(encoding='utf-8'))
 assert gpu['status']=='actual_d16_atlas_render_and_clear_verified_fractional_edge_discrepancy_open'
 assert '5 passed; 0 failed' in (W/'tests.log').read_text(encoding='utf-8')
 assert int((W/'engine-check.exit.txt').read_text())==0
 source_path=Path('F:/OpenRPG_ImportWork/firepit-source-point-shadow-raster-v3/SOURCE-SHADOW-RASTER-0.json')
 source=json.loads(source_path.read_text(encoding='utf-8'))
 requests=json.loads(source['requests_json'])
 for request,values in zip(requests,gpu['viewport_values'],strict=True):
  expected=[request['dynamicAtlasViewport'][k] for k in ('x','y','width','height')]
  assert [struct.pack('<f',v) for v in values]==[struct.pack('<f',v) for v in expected]
 assert len(requests)==6
 depth=struct.unpack('<f',struct.pack('<f',16384/65535))[0]
 checks=[]
 for name,active in [('active-depth.float32',True),('inactive-depth.float32',False)]:
  path=W/name;data=array.array('f');data.frombytes(path.read_bytes());assert len(data)==4096**2
  if active:
   for row in range(4096):
    line=data[row*4096:(row+1)*4096]
    assert line[:2730].count(depth)==2730 and line[2730:].count(0.0)==1366
   assert gpu['measurements'][0]['ideal_coverage_mismatches']==4096
   count=2730*4096
  else:
   assert data.count(0.0)==len(data);count=0
  checks.append({'path':str(path),'sha256':sha(path),'samples':len(data),'nonzero':count})
 export=R/'crates/forge_engine/src/source_graph.rs'
 insertion='#[path="source_graph_punctual_atlas.rs"]\nmod source_punctual_atlas;\npub use source_punctual_atlas::{SourceGraphPunctualAtlas,SourceGraphPunctualAtlasLayout,SourceGraphPunctualViewport};\n'
 assert export.read_text(encoding='utf-8').replace(insertion,'')==(W/'source_graph.before.rs').read_text(encoding='utf-8')
 paths=[Path(__file__),export,R/'crates/forge_engine/src/source_graph_punctual_atlas.rs',W/'Cargo.toml',W/'Cargo.lock',W/'src/main.rs',W/'tests.log',W/'gpu.log',W/'GPU-RESULT.v1.json',W/'engine-check.log',W/'engine-check.exit.txt',source_path,W/'gpu.edge-refusal.log',W/'gpu.initial-refusal.log']
 result={'status':'common_d16_atlas_contract_native_compile_gpu_interior_and_inactive_clear_reopened_edge_coverage_refused','finished_utc':datetime.now(timezone.utc).isoformat(),'contract_tests_verified':5,'engine_native_compile_verified':True,'source_viewports_bit_exact':6,'numeric_gpu_checks':checks,'ideal_active_coverage_mismatches':4096,'fractional_coverage_qualified':False,'source_asset_geometry_render_verified':False,'receiver_filter_verified':False,'full_import':False,'editor_verified':False,'complete_assets_delivered_increment':0,'next_action':'Mesurer la couverture raster des viewports fractionnaires sur les géométries source, puis raccorder le récepteur ponctuel PCF5 ; compilation Web restante','input_pins':{str(p):sha(p) for p in paths}}
 (W/'RESULT.v1.json').write_text(json.dumps(result,indent=2,ensure_ascii=False),encoding='utf-8')
 print(json.dumps({'status':result['status'],'tests':5,'gpu_samples':2*4096**2,'edge_refused_pixels':4096},ensure_ascii=True))
if __name__=='__main__':main()
