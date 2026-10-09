"""Resolve source raster state without changing the pinned receiver-bias GLB."""
from pathlib import Path
import datetime,hashlib,json
W=Path('F:/OpenRPG_ImportWork');ROOT=W/'firepit-source-shadow-raster-case-v1';S=Path(__file__).parent
R=Path('E:/OpenRPG_Forge/OpenRPG_Forge-Rust')
def sha(p):
 with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def read(p):return json.loads(p.read_text('utf-8-sig'))
def main():
 assert not ROOT.exists()
 old=W/'firepit-source-tent7-receiver-bias-case-v2';case=read(old/'CASE.v1.json');proof=read(old/'RESULT.v1.json')
 source_path=W/'firepit-source-shadow-raster-reopen-v2/RESULT.v1.json';source=read(source_path)
 for p,h in {**proof['input_pins'],**source['input_pins'],**source['source_current_input_pins']}.items():assert sha(p)==h,p
 assert source['source_format']=='D32_SFloat' and source['reversed_z'] and source['actual_request_slope_bias']==[2.5]*4
 assert sha(proof['private_fixture'])==proof['private_fixture_sha256']
 case['source_shadow_raster_replay']={'schema_version':1,**source['candidate_native_reversed_depth_bias'],'depth_format':'Depth32Float','orthographic_shadow_only':True,'source_request_slope_bias':source['actual_request_slope_bias'],'source_active_depth':str(W/'firepit-source-shadow-raster-v4/SOURCE-SHADOW-DEPTH-0.float32'),'native_descriptor_and_depth_gpu_verified':False,'full_import':False}
 ROOT.mkdir();(ROOT/'CASE.v1.json').write_text(json.dumps(case,indent=2),'utf-8')
 files=[Path(__file__).resolve(),source_path,old/'CASE.v1.json',old/'RESULT.v1.json',S/'candidates/SourceShadowDepthProbe.wgsl']
 files.extend([R/'crates/forge_engine/src/source_graph_shadow_raster.rs',R/'crates/forge_engine/src/source_graph.rs'])
 proof.update(status='explicit_source_orthographic_shadow_raster_replay_prepared',source_shadow_raster_replay=case['source_shadow_raster_replay'],input_pins={**proof['input_pins'],**{str(p):sha(p) for p in files}},source_raster_bias_matched=False,full_import=False,finished_utc=datetime.datetime.now(datetime.timezone.utc).isoformat())
 (ROOT/'RESULT.v1.json').write_text(json.dumps(proof,indent=2),'utf-8');print(json.dumps({'root':str(ROOT),'native_state':case['source_shadow_raster_replay'],'full_import':False}))
if __name__=='__main__':main()
