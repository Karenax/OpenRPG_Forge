from pathlib import Path
from datetime import datetime,timezone
import json,hashlib,re,shutil
S=Path(__file__).parent;W=Path('F:/OpenRPG_ImportWork');T=W/'SUIVI_LOTS_HIVEMIND';ROOT=W/'source-graph-ten-texture-contract-check-v1'
run=json.loads((ROOT/'RUN-RESULT.v1.json').read_text())
assert run['actual_exit_code']==0
assert hashlib.sha256((ROOT/'stdout.log').read_bytes()).hexdigest()==run['stdout_sha256']
assert re.search(r'test result: ok\. 25 passed; 0 failed;', (ROOT/'stdout.log').read_text())
for p,h in json.loads((ROOT/'PREPARED.v1.json').read_text())['input_pins'].items():
 with Path(p).open('rb') as f:assert hashlib.file_digest(f,'sha256').hexdigest()==h,p
backup=W/'ten-texture-contract-tracker-before-v1'
if not backup.exists():
 backup.mkdir()
 for p in [S/'record_batch_progress_v1.py',S/'render_lot_dashboard_v1.py',T/'PLAN_EXECUTION_LOTS_20261009.md',T/'PLAN_EXECUTION_LOTS_20261009.v1.json']:shutil.copy2(p,backup/p.name)
p=S/'record_batch_progress_v1.py';text=p.read_text(encoding='utf-8');marker=" ten_texture_wire=read(W/'source-graph-ten-texture-contract-check-v1/RUN-RESULT.v1.json')"
if marker not in text:
 anchor=" active_lot=data.get('current_work_sub_lot',active_lot);active_state=data.get('current_work_state',active_state)";assert text.count(anchor)==1
 block=''' ten_texture_wire=read(W/'source-graph-ten-texture-contract-check-v1/RUN-RESULT.v1.json')
 ten_texture_engine=read(W/'source-graph-ten-texture-engine-check-v1/RUN-RESULT.v1.json')
 if ten_texture_wire.get('status')=='ten_texture_wire_contract_tests_passed_inputs_unchanged':
  data['source_graph_ten_texture_contract']={'proof':str(W/'source-graph-ten-texture-contract-check-v1/RUN-RESULT.v1.json'),'wire_tests_passed':25,'schema_version_added':6,'historical_wire_capacity':7,'new_wire_capacity':10,'actual_modified_material_gpu_layout_verified':False,'complete_assets_delivered_increment':0}
  data['current_work_state'] += ' ; contrat commun schema 6 applique, 25 tests wire passes ; tests moteur : '+ten_texture_engine.get('status','non lances')+' ; budget GPU accru meme pour anciens programmes, compatibilite device non acquise'
  text=text.replace('| L01.LOD.01 |','| L03.TOWNSMITH.SHADER.S_PAINT.CONTRAT.10 | Contrat commun dix textures | 25 tests wire passes | anciens schemas limites a 7, scene bindings preserves ; moteur/GPU restants | [Tests](../source-graph-ten-texture-contract-check-v1/RUN-RESULT.v1.json) |\\n| L01.LOD.01 |')
  key+=':'+ten_texture_wire['status']+':'+ten_texture_engine.get('status','pending')
'''
 p.write_text(text.replace(anchor,block+anchor),encoding='utf-8')
p=S/'render_lot_dashboard_v1.py';text=p.read_text(encoding='utf-8');marker="    ten_texture_wire_path=W/'source-graph-ten-texture-contract-check-v1/RUN-RESULT.v1.json'"
if marker not in text:
 anchor="    output += 'Prochaine action enregistrée : '+state['next_action']+'.\\n\\n'";assert text.count(anchor)==1
 block="    ten_texture_wire_path=W/'source-graph-ten-texture-contract-check-v1/RUN-RESULT.v1.json'\n    ten_texture_engine_path=W/'source-graph-ten-texture-engine-check-v1/RUN-RESULT.v1.json'\n    output += '**Contrat commun dix textures applique :** schema 6, 25 tests wire passes ; anciens schemas limites a sept, bindings de scene et uniformes preserves. La reservation commune de slots augmente le budget GPU meme des anciens programmes : compatibilite device a verifier. [Tests wire](../source-graph-ten-texture-contract-check-v1/RUN-RESULT.v1.json) / [Tests moteur](../source-graph-ten-texture-engine-check-v1/RUN-RESULT.v1.json). Aucun rendu ni asset complet qualifie.\\n\\n'\n"
 text=text.replace(anchor,block+anchor);anchor="    meta = T / 'LOTS_ET_AVANCEMENT.v1.json'";assert text.count(anchor)==1
 text=text.replace(anchor,"    for p in [ten_texture_wire_path,ten_texture_engine_path]:\n        if p.exists():\n            manifest['input_pins'][str(p)] = hashlib.sha256(p.read_bytes()).hexdigest()\n"+anchor)
 p.write_text(text,encoding='utf-8')
p=T/'PLAN_EXECUTION_LOTS_20261009.v1.json';d=json.loads(p.read_text());batch='L03.TOWNSMITH.SHADER.S_PAINT.CONTRAT.10'
if not any(r['id']==batch for r in d['finished_batches']):d['finished_batches'].append({'id':batch,'proof':str(ROOT/'RUN-RESULT.v1.json'),'technical_scope_only':True,'wire_tests_passed':25,'engine_gpu_verified':False,'complete_assets_delivered_increment':0})
d['updated_utc']=datetime.now(timezone.utc).isoformat();d['parallel_engine_check']=str(W/'source-graph-ten-texture-engine-check-v1/RUN-RESULT.v1.json')
p.write_text(json.dumps(d,indent=2,ensure_ascii=False),encoding='utf-8')
p=T/'PLAN_EXECUTION_LOTS_20261009.md';text=p.read_text(encoding='utf-8');note='\n- Contrat commun schema 6 applique : dix slots, 25 tests wire passes ; anciens schemas et bindings numeriques preserves. Budget GPU accru a verifier, tests moteur en cours. [Tests](../source-graph-ten-texture-contract-check-v1/RUN-RESULT.v1.json). Aucun asset complet ajoute.\n'
if note not in text:p.write_text(text+note,encoding='utf-8')
from record_batch_progress_v1 import main
main()
