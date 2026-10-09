"""Bind the two ready GPU pipeline identities back to full shader payload SHA."""
from pathlib import Path
from datetime import datetime,timezone
import hashlib,json,struct,uuid
W=Path('F:/OpenRPG_ImportWork');CAP=W/'studio-source-pipeline-probe-v1';ROOT=W/'studio-source-pipeline-probe-reopen-v1'
def read(p):return json.loads(Path(p).read_text('utf-8-sig'))
def sha(p):
 with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def main():
 assert not ROOT.exists();ROOT.mkdir();prep=read(CAP/'PREPARED.v1.json');run=read(CAP/'RUN-RESULT.v1.json');native=read(CAP/'NATIVE-RESULT.v1.json');probe=read(CAP/'PIPELINE-RESULT.v1.json')
 assert run['actual_exit_code']==0 and sha(CAP/'NATIVE-RESULT.v1.json')==run['native_receipt_sha256'] and probe['all_source_pipelines_ready'] and probe['source_pipeline_count']==2
 for p,d in prep['input_pins'].items():assert sha(p)==d,p
 assert native['adapter']['backend']=='Dx12' and native['same_bevy_device'] and native['bc_feature'];assert not probe['production_reveal_changed'] and not probe['source_lod_selection_qualified'] and not probe['draw_readback_qualified']
 fixture=Path(prep['fixture']);shaders={}
 with fixture.open('rb') as f:
  header=f.read(20);assert header[:4]==b'glTF';length=struct.unpack_from('<I',header,12)[0];doc=json.loads(f.read(length));bin_header=f.read(8);assert bin_header[4:]==b'BIN\0';start=f.tell();bin_length=struct.unpack_from('<I',bin_header)[0]
  contracts=[m['extensions']['OPENRPG_source_graph'] for m in doc['materials'] if 'OPENRPG_source_graph' in m.get('extensions',{})];assert len(contracts)==1;contract=contracts[0]
  for role in ['forward','prepass']:
   ref=contract[role];view=doc['bufferViews'][ref['buffer_view']];offset=view.get('byteOffset',0);size=view['byteLength'];assert offset+size<=bin_length;f.seek(start+offset);payload=f.read(size);assert len(payload)==size and hashlib.sha256(payload).hexdigest()==ref['sha256'];payload.decode('utf-8');shaders[role]=dict(full_sha256=ref['sha256'],uuid=str(uuid.UUID(hex=ref['sha256'][:32])),bytes=size)
 states=contract['state'];assert native['original_passes']==[dict(material=0,render_queue=states['render_queue'],disabled_passes=states['disabled_passes'])]
 rows=[]
 for role,s in shaders.items():
  matches=[r for r in probe['source_pipelines'] if s['uuid'] in r['vertex_shader'] and s['uuid'] in r['fragment_shader']];assert len(matches)==1;r=matches[0];assert r['state']=='ok' and r['error'] is None
  defs=r['vertex_defs']+' '+r['fragment_defs'];assert ('PREPASS_PIPELINE' in defs)==(role=='prepass');assert 'MOTION_VECTOR_PREPASS' not in defs and 'SOURCE_GRAPH_SHADOW_PASS' not in defs
  rows.append(dict(role=role,shader=s,pipeline_label=r['label'],state='created_on_actual_device'))
 pins={str(p):sha(p) for p in [Path(__file__).resolve(),CAP/'PREPARED.v1.json',CAP/'RUN-RESULT.v1.json',CAP/'NATIVE-RESULT.v1.json',CAP/'PIPELINE-RESULT.v1.json',fixture]}
 result=dict(status='two_firepit_forward_and_prepass_gpu_pipelines_independently_reopened',finished_utc=datetime.now(timezone.utc).isoformat(),sub_batch='L01.GPU.02',pipelines_verified=2,rows=rows,adapter=native['adapter'],product_inputs_unchanged=len(prep['input_pins']),original_render_queue_preserved=states['render_queue'],motion_vectors_remain_disabled=True,input_pins=pins,diagnostic_all_static_layouts=True,production_reveal_changed=False,draw_readback_qualified=False,shadow_pipeline_qualified=False,source_lod_selection_qualified=False,editor_visual_verified=False,full_import=False)
 (ROOT/'RESULT.v1.json').write_text(json.dumps(result,indent=2),'utf-8');print(json.dumps({k:v for k,v in result.items() if k not in ['input_pins','rows']}))
if __name__=='__main__':main()
