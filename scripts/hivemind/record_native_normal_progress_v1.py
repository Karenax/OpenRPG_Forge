"""Finish partial V2 tracker publication and record independently reopened normals."""
from pathlib import Path
from datetime import datetime,timezone
import json,hashlib,subprocess,sys
S=Path(__file__).parent;W=Path('F:/OpenRPG_ImportWork');T=W/'SUIVI_LOTS_HIVEMIND'
def read(p):return json.loads(p.read_text(encoding='utf-8-sig'))
def sha(p):
 with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def main():
 paths=[W/'native-texture-identity-contract-reopen-v2/RESULT.v1.json',W/'townsmith-s-paint-native-normal-decode-reopen-v1/RESULT.v1.json']
 for p in paths:
  for name,h in read(p)['input_pins'].items():assert sha(name)==h,name
 assert read(paths[0])['tests_passed']==30 and read(paths[1])['native_decode_observations_verified']==324
 p=S/'record_batch_progress_v1.py';s=p.read_text(encoding='utf-8');anchor=" active_lot=data.get('current_work_sub_lot',active_lot);active_state=data.get('current_work_state',active_state)";assert s.count(anchor)==1
 block=" normal_source_close=read(W/'townsmith-s-paint-native-normal-decode-reopen-v1/RESULT.v1.json')\n if normal_source_close.get('status')=='three_source_normal_profiles_reopened_324_native_decodes_original_texels_exact':\n  data['current_work_state']+=' ; reference normales Unity : 3 profils / 324 decodages reouverts, texels et alpha source identiques ; Forge GPU restant'\n  data['townsmith_native_normal_source_proof']=str(W/'townsmith-s-paint-native-normal-decode-reopen-v1/RESULT.v1.json')\n  key+=':'+normal_source_close['status']\n"
 p.write_text(s.replace(anchor,block+anchor),encoding='utf-8')
 p=S/'render_lot_dashboard_v1.py';s=p.read_text(encoding='utf-8');anchor="    path = T / 'LOTS_ET_AVANCEMENT.md'";assert s.count(anchor)==1
 block="    native_identity_v2_path=W/'native-texture-identity-contract-reopen-v2/RESULT.v1.json'\n    native_normal_source_path=W/'townsmith-s-paint-native-normal-decode-reopen-v1/RESULT.v1.json'\n    output += '**Contrat generique V2 :** 30 tests passent, admission par ressource independante du statut et de la taille du lot ; 80 mutations de ressources et deux protocoles inconnus refuses. [Preuve](../native-texture-identity-contract-reopen-v2/RESULT.v1.json). Installation commune restante.\\n\\n'\n    output += '**Reference normales source terminee :** trois profils, 324 decodages GPU Unity ; 324 texels encodes et alphas bit-exacts avec la capture anterieure. Mode RG/AG observe pour chaque profil ; comparaison Forge GPU et rendu restants. [Preuve](../townsmith-s-paint-native-normal-decode-reopen-v1/RESULT.v1.json). Aucun asset complet ajoute.\\n\\n'\n"
 s=s.replace(anchor,block+anchor);anchor="    meta = T / 'LOTS_ET_AVANCEMENT.v1.json'";assert s.count(anchor)==1
 s=s.replace(anchor,"    for p in [native_identity_v2_path,native_normal_source_path]:\n        manifest['input_pins'][str(p)]=hashlib.sha256(p.read_bytes()).hexdigest()\n"+anchor);p.write_text(s,encoding='utf-8')
 planpath=T/'PLAN_EXECUTION_LOTS_20261009.v1.json';plan=read(planpath);now=datetime.now(timezone.utc).isoformat();plan['updated_utc']=now;plan['current_receipt']=str(paths[1]);plan['native_admission_candidate_receipt']=str(paths[0])
 plan['next_action']='Finish active engine tests, install the common protocol and GUID contract, rerun workspace tests, then compare native normal sampling and decodes on Forge GPU'
 plan['finished_batches'].append({'id':'L03.TOWNSMITH.SHADER.S_PAINT.NORMALS.SOURCE','proof':str(paths[1]),'profiles_verified':3,'source_gpu_observations_verified':324,'technical_scope_only':True,'forge_gpu_qualified':False,'complete_assets_delivered_increment':0})
 planpath.write_text(json.dumps(plan,indent=2,ensure_ascii=False),encoding='utf-8')
 with (T/'PLAN_EXECUTION_LOTS_20261009.md').open('a',encoding='utf-8') as f:f.write('\n- Admission generique V2 : 30 tests, 80 mutations de ressources et deux protocoles inconnus refuses. [Preuve](../native-texture-identity-contract-reopen-v2/RESULT.v1.json).\n- Normales source : trois profils, 324 observations GPU, mode RG/AG observe ; originals et reglages inchangees. Forge GPU restant. [Preuve](../townsmith-s-paint-native-normal-decode-reopen-v1/RESULT.v1.json). Aucun asset complet ajoute.\n')
 with (T/'JOURNAL.md').open('a',encoding='utf-8') as f:f.write('\n- '+now+' — L03 admission V2 : 30 tests passes ; ressources independantes de la taille/statut du lot, 80 mutations refusees et deux protocoles inconnus refuses. Normales source : 3 profils / 324 observations, texels encodes et alpha identiques. Comparaison Forge GPU et installation communes restantes. [Normales](../townsmith-s-paint-native-normal-decode-reopen-v1/RESULT.v1.json). Aucun nouvel asset complet.\n')
 subprocess.run([sys.executable,'-B',str(S/'record_batch_progress_v1.py')],check=True,stdout=subprocess.DEVNULL)
 for name,h in read(T/'LOTS_ET_AVANCEMENT.v1.json')['input_pins'].items():assert sha(name)==h,name
 assert read(T/'SUIVI_SIMPLE.v1.json')['active_sub_lot']==plan['active_batch']
 print(json.dumps({'status':'generic_admission_and_native_normal_source_tracking_updated','tests_passed':30,'source_normal_observations':324,'complete_assets_delivered_increment':0,'tracker_pins_verified':True}))
if __name__=='__main__':main()
