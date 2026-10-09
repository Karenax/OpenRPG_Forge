from pathlib import Path
from datetime import datetime,timezone
import json,hashlib,subprocess,sys
S=Path(__file__).parent;W=Path('F:/OpenRPG_ImportWork');T=W/'SUIVI_LOTS_HIVEMIND';ROOT=W/'native-texture-capture-common-regressions-reopen-v1'
def read(p):return json.loads(p.read_text(encoding='utf-8-sig'))
def sha(p):
 with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def main():
 testroot=W/'native-texture-identity-contract-check-v3';test=read(testroot/'RESULT.v1.json');assert test['test_exit_code']==0
 for p,h in test['input_pins'].items():assert sha(p)==h,p
 assert '33 passed; 0 failed' in (testroot/'tests.stdout.log').read_text(encoding='utf-8')
 pins={str(p):sha(p) for p in [testroot/'RESULT.v1.json',testroot/'tests.stdout.log',testroot/'tests.stderr.log',Path(__file__)]};pins.update(test['input_pins'])
 result={'status':'permanent_native_protocol_regressions_reopened_33_candidate_tests_passed','finished_utc':datetime.now(timezone.utc).isoformat(),'tests_passed':33,'input_pins':pins,'common_engine_installation_verified':False,'actual_material_render_qualified':False,'full_import':False,'complete_assets_delivered_increment':0}
 ROOT.mkdir(exist_ok=False);proof=ROOT/'RESULT.v1.json';proof.write_text(json.dumps(result,indent=2),encoding='utf-8')
 p=S/'record_batch_progress_v1.py';s=p.read_text(encoding='utf-8');anchor=" active_lot=data.get('current_work_sub_lot',active_lot);active_state=data.get('current_work_state',active_state)";assert s.count(anchor)==1
 block=" common_install_queue=read(W/'native-texture-capture-common-install-v1/RUN-RESULT.v1.json')\n common_regressions=read(W/'native-texture-capture-common-regressions-reopen-v1/RESULT.v1.json')\n if common_regressions.get('tests_passed')==33:\n  data['current_work_state']+=' ; regressions permanentes preparees : 33 tests candidat passes ; installation sequentielle : '+common_install_queue.get('status','pending')\n  data['common_install_queue']=str(W/'native-texture-capture-common-install-v1/RUN-RESULT.v1.json')\n  key+=':'+common_regressions['status']+':'+common_install_queue.get('status','pending')\n"
 p.write_text(s.replace(anchor,block+anchor),encoding='utf-8')
 p=S/'render_lot_dashboard_v1.py';s=p.read_text(encoding='utf-8');anchor="    path = T / 'LOTS_ET_AVANCEMENT.md'";assert s.count(anchor)==1
 block="    common_regression_path=W/'native-texture-capture-common-regressions-reopen-v1/RESULT.v1.json'\n    common_install_prepared_path=W/'native-texture-capture-common-install-v1/PREPARED.v1.json'\n    output += '**Installation commune en chaine :** regressions permanentes preparees, 33 tests candidat passent ; installation attend le succes du test moteur deja actif, puis tests workspace assets et moteur sequentiels. Les sources historiques sont snapshottees avant remplacement ; aucune installation complete annoncee avant ses recus. [Regressions](../native-texture-capture-common-regressions-reopen-v1/RESULT.v1.json) / [Etat du travail](../native-texture-capture-common-install-v1/RUN-RESULT.v1.json).\\n\\n'\n"
 s=s.replace(anchor,block+anchor);anchor="    meta = T / 'LOTS_ET_AVANCEMENT.v1.json'";assert s.count(anchor)==1
 s=s.replace(anchor,"    for p in [common_regression_path,common_install_prepared_path]:\n        manifest['input_pins'][str(p)]=hashlib.sha256(p.read_bytes()).hexdigest()\n"+anchor);p.write_text(s,encoding='utf-8')
 planpath=T/'PLAN_EXECUTION_LOTS_20261009.v1.json';plan=read(planpath);plan['updated_utc']=result['finished_utc'];plan['common_install_job']=str(W/'native-texture-capture-common-install-v1/JOB.v1.json');plan['common_install_run']=str(W/'native-texture-capture-common-install-v1/RUN-RESULT.v1.json');plan['common_regressions_proof']=str(proof);planpath.write_text(json.dumps(plan,indent=2,ensure_ascii=False),encoding='utf-8')
 with (T/'JOURNAL.md').open('a',encoding='utf-8') as f:f.write('\n- '+result['finished_utc']+' — L03 installation commune : candidat 33 tests passes avec regressions permanentes ; controleur sequentiel actif apres test moteur 4964. Snapshots des deux sources avant modification ; tests workspace assets/moteur suivants. Aucune installation complete ni livraison annoncee. [Job](../native-texture-capture-common-install-v1/RUN-RESULT.v1.json).\n')
 subprocess.run([sys.executable,'-B',str(S/'record_batch_progress_v1.py')],check=True,stdout=subprocess.DEVNULL)
 for p,h in read(T/'LOTS_ET_AVANCEMENT.v1.json')['input_pins'].items():assert sha(p)==h,p
 print(json.dumps({'status':'common_install_sequence_tracking_updated','candidate_tests_passed':33,'common_install_state':read(W/'native-texture-capture-common-install-v1/RUN-RESULT.v1.json')['status'],'complete_assets_delivered_increment':0}))
if __name__=='__main__':main()
