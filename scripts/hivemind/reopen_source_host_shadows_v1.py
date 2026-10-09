"""Independently rehash the host-shadow receipt and all admitted input pins."""
from pathlib import Path
from datetime import datetime,timezone
import json,hashlib
W=Path('F:/OpenRPG_ImportWork');CAP=W/'studio-source-host-shadows-v1';ROOT=W/'studio-source-host-shadows-reopen-v1'
def read(p):return json.loads(p.read_text('utf-8-sig'))
def sha(p):
 with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def main():
 assert not ROOT.exists();prep=read(CAP/'PREPARED.v1.json');run=read(CAP/'RUN-RESULT.v1.json');native=read(CAP/'NATIVE-RESULT.v1.json')
 assert run['actual_exit_code']==0 and sha(CAP/'NATIVE-RESULT.v1.json')==run['native_receipt_sha256']
 for p,h in prep['input_pins'].items():assert sha(p)==h,p
 keys=['same_frame_shadow_time_verified','camera_conflict_no_allocation','partial_install_rollback_verified','repeated_install_preserves_ids','refused_and_missing_routes_no_partial_frame']
 assert all(native[k] for k in keys) and native['host_shadow_routes']==4 and not native['root_revealed'] and not native['draw_executed']
 fixture=Path(prep['fixture']);host=Path('E:/OpenRPG_Forge/OpenRPG_Forge-Rust/apps/forge_studio/src/authoring_assets/source_graph_host.rs');preview=host.with_name('source_graph_preview.rs')
 assert 'host.install_shadow_routes(world, 31)?' in preview.read_text('utf-8')
 assert 'self.validate_shadow_routes(world)' in ''.join(host.read_text('utf-8').split())
 ROOT.mkdir()
 result=dict(status='production_host_shadow_routes_and_same_frame_refusals_independently_reopened',finished_utc=datetime.now(timezone.utc).isoformat(),sub_lot='L01.OMBRES.01',routes_verified=4,controls_verified=6,controls=['camera_layer_conflict_without_allocation','partial_install_rollback','idempotent_ids_and_materials','current_and_previous_shadow_time_same_frame','refused_route_no_partial_update','missing_route_no_partial_update'],source_pins_verified=len(prep['input_pins']),fixture_sha256=sha(fixture),input_pins={str(p):sha(p) for p in [Path(__file__).resolve(),CAP/'PREPARED.v1.json',CAP/'RUN-RESULT.v1.json',CAP/'NATIVE-RESULT.v1.json',host,preview]},production_shadow_route_api_installed=True,preview_callsite_present=True,full_studio_web_compile_verified=False,source_shadow_appearance_verified=False,source_stencil_and_queue_verified=False,lod_verified=False,editor_visual_verified=False,full_import=False)
 (ROOT/'RESULT.v1.json').write_text(json.dumps(result,indent=2),'utf-8');print(json.dumps({k:v for k,v in result.items() if k!='input_pins'}))
if __name__=='__main__':main()
