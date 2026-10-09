"""Close source shadow compilation and the declared forward-state subset only."""
from pathlib import Path
from datetime import datetime,timezone
import hashlib,json,struct,uuid
W=Path('F:/OpenRPG_ImportWork');CAP=W/'studio-source-shadow-probe-v1';ROOT=W/'studio-source-shadow-probe-reopen-v1'
def read(p):return json.loads(Path(p).read_text('utf-8-sig'))
def sha(p):
 with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def main():
 assert not ROOT.exists();ROOT.mkdir();prep=read(CAP/'PREPARED.v1.json');run=read(CAP/'RUN-RESULT.v1.json');native=read(CAP/'NATIVE-RESULT.v1.json');probe=read(CAP/'PIPELINE-RESULT.v1.json');assert run['actual_exit_code']==0 and probe['source_shadow_pipeline_ready'] and probe['all_source_pipelines_ready'] and probe['explicit_shadow_routes']==4 and probe['source_pipeline_count']==3
 for p,d in prep['input_pins'].items():assert sha(p)==d,p
 assert sha(CAP/'NATIVE-RESULT.v1.json')==run['native_receipt_sha256'];fixture=Path(prep['fixture']);shaders={}
 with fixture.open('rb') as f:
  h=f.read(20);length=struct.unpack_from('<I',h,12)[0];doc=json.loads(f.read(length));bh=f.read(8);assert bh[4:]==b'BIN\0';start=f.tell();bin_length=struct.unpack_from('<I',bh)[0];contract=doc['materials'][0]['extensions']['OPENRPG_source_graph']
  for role in ['forward','prepass']:
   ref=contract[role];view=doc['bufferViews'][ref['buffer_view']];offset=view.get('byteOffset',0);size=view['byteLength'];assert offset+size<=bin_length;f.seek(start+offset);payload=f.read(size);assert len(payload)==size and hashlib.sha256(payload).hexdigest()==ref['sha256'];shaders[role]=str(uuid.UUID(hex=ref['sha256'][:32]))
 rows=probe['source_pipelines'];assert all(r['state']=='ok' and r['error'] is None for r in rows)
 shadow=[r for r in rows if 'SOURCE_GRAPH_SHADOW_PASS' in r['vertex_defs']];prepass=[r for r in rows if 'PREPASS_PIPELINE' in r['vertex_defs'] and 'SOURCE_GRAPH_SHADOW_PASS' not in r['vertex_defs']];forward=[r for r in rows if 'PREPASS_PIPELINE' not in r['vertex_defs']];assert len(shadow)==len(prepass)==len(forward)==1
 for r in shadow+prepass:assert shaders['prepass'] in r['vertex_shader'] and shaders['prepass'] in r['fragment_shader']
 for r in forward:assert shaders['forward'] in r['vertex_shader'] and shaders['forward'] in r['fragment_shader']
 state=contract['state'];assert state['forward_cull']==2 and state['prepass_cull']==2 and state['depth_compare']==3 and state['write_depth'] and state['color_write_mask']==15 and state['color_blend']==[1,0] and state['alpha_blend']==[1,0] and not state['alpha_to_coverage']
 r=forward[0];assert r['cull_mode']=='Some(Back)' and r['front_face']=='Ccw';assert 'depth_compare: Some(Equal)' in r['depth_stencil'] and 'depth_write_enabled: Some(true)' in r['depth_stencil'];assert 'src_factor: One, dst_factor: Zero, operation: Add' in r['color_targets'] and 'write_mask: ColorWrites(15)' in r['color_targets'];assert 'alpha_to_coverage_enabled: false' in r['multisample']
 for r in prepass+shadow:assert r['cull_mode']=='Some(Back)' and 'depth_write_enabled: Some(true)' in r['depth_stencil'] and 'depth_compare: Some(GreaterEqual)' in r['depth_stencil']
 assert shadow[0]['color_targets']=='[]' and 'count: 1,' in shadow[0]['multisample'];assert 'count: 4,' in forward[0]['multisample']
 result=dict(status='three_source_gpu_pipelines_and_forward_state_subset_independently_reopened',finished_utc=datetime.now(timezone.utc).isoformat(),sub_batch='L01.GPU.03',source_pipelines_verified=3,shadow_routes_verified=4,shadow_shader_uuid=shaders['prepass'],forward_state_subset_verified=['cull','front_face','depth_compare','depth_write','color_and_alpha_blend','color_write_mask','alpha_to_coverage'],adapter=native['adapter'],product_pins_unchanged=len(prep['input_pins']),input_pins={str(p):sha(p) for p in [Path(__file__).resolve(),fixture,CAP/'PREPARED.v1.json',CAP/'RUN-RESULT.v1.json',CAP/'PIPELINE-RESULT.v1.json',CAP/'NATIVE-RESULT.v1.json']},production_shadow_routes_installed=False,same_frame_shadow_parameters_qualified=False,source_stencil_states_qualified=False,original_queue_execution_qualified=False,shadow_appearance_qualified=False,source_lod_selection_qualified=False,editor_visual_verified=False,full_import=False)
 (ROOT/'RESULT.v1.json').write_text(json.dumps(result,indent=2),'utf-8');print(json.dumps({k:v for k,v in result.items() if k!='input_pins'}))
if __name__=='__main__':main()
