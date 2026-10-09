"""Close the technical sub-lot only after native reopen and actual Web compile."""
from pathlib import Path
from datetime import datetime,timezone
import json,hashlib,struct
W=Path('F:/OpenRPG_ImportWork');ROOT=W/'studio-source-host-shadow-install-v1'
A=Path('E:/OpenRPG_Forge/OpenRPG_Forge-Rust/apps/forge_studio/src/authoring_assets')
def read(p):return json.loads(p.read_text('utf-8-sig'))
def sha(p):
 with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def main():
 assert not (ROOT/'RESULT.v1.json').exists()
 native=W/'studio-source-host-shadows-reopen-v2/RESULT.v1.json';web=W/'studio-current-source-wasm-check-v15/RUN-RESULT.v1.json'
 n=read(native);w=read(web);assert n['controls_verified']==6 and n['routes_verified']==4 and w['actual_exit_code']==0 and not w['changed_source_files']
 for p,h in n['input_pins'].items():assert sha(p)==h,p
 for p,h in w['input_pins'].items():assert sha(p)==h,p
 old=(ROOT/'host-before-wasm-repair.rs').read_text('utf-8');now=(A/'source_graph_host.rs').read_text('utf-8')
 assert old.replace('use bevy::prelude::*;','use bevy::{ecs as bevy_ecs, prelude::*};')==now
 prep=read(W/'studio-source-host-shadows-v2/PREPARED.v1.json');fixture=Path(prep['fixture'])
 with fixture.open('rb') as f:header=f.read(20);doc=json.loads(f.read(struct.unpack_from('<I',header,12)[0]))
 ids=[dict(source_node=i,source_mesh=node['mesh'],name=node.get('name')) for i,node in enumerate(doc['nodes']) if 'mesh' in node]
 assert len(ids)==4
 result=dict(status='host_shadow_install_same_frame_native_and_web_technical_batch_verified',finished_utc=datetime.now(timezone.utc).isoformat(),sub_lot='L01.OMBRES.01',source_asset=fixture.stem,source_draw_ids=ids,source_fixture_sha256=sha(fixture),routes_verified=4,controls_verified=6,native_receipt=str(native),web_receipt=str(web),web_source_pins_verified=len(w['input_pins']),web_exit_code=0,retained_failure=str(W/'studio-current-source-wasm-check-v14/RUN-RESULT.v1.json'),wasm_repair_only_import_alias=True,input_pins={str(p):sha(p) for p in [Path(__file__).resolve(),native,web,*[A/f for f in ['source_graph_host.rs','source_graph_preview.rs','source_graph_frame.rs']],*list((ROOT/'preimages').glob('*.rs'))]},source_shadow_appearance_verified=False,stencils_and_queue_verified=False,lod_verified=False,editor_visual_verified=False,full_import=False,next_sub_lot='L01.OMBRES.02')
 (ROOT/'RESULT.v1.json').write_text(json.dumps(result,indent=2,ensure_ascii=False),'utf-8')
 print(json.dumps({k:v for k,v in result.items() if k not in ['input_pins','source_draw_ids']},ensure_ascii=False))
if __name__=='__main__':main()
