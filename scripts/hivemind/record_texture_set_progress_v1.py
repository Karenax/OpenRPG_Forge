from pathlib import Path
from datetime import datetime,timezone
import json,hashlib,subprocess,sys
S=Path(__file__).parent;W=Path('F:/OpenRPG_ImportWork');T=W/'SUIVI_LOTS_HIVEMIND';ROOT=W/'townsmith-s-paint-texture-set-reopen-v1'
def read(p):return json.loads(p.read_text(encoding='utf-8-sig'))
def sha(p):
 with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def main():
 check=W/'townsmith-s-paint-texture-set-check-v1';run=read(check/'RESULT.v1.json');assert run['test_exit_code']==0
 assert '35 passed; 0 failed' in (check/'tests.stdout.log').read_text(encoding='utf-8')
 setpath=W/'townsmith-s-paint-texture-set-v1/TEXTURE-SET.v1.json';d=read(setpath)
 for entries in [run['input_pins'],d['input_pins']]:
  for p,h in entries.items():assert sha(p)==h,p
 assert len(d['textures'])==10 and len(d['buffer_views'])==32
 pins={**run['input_pins'],**d['input_pins']}
 for p in [setpath,check/'RESULT.v1.json',check/'tests.stdout.log',Path(__file__)]:pins[str(p)]=sha(p)
 result={'status':'ten_texture_shared_view_set_and_canonical_bindings_reopened_35_tests','finished_utc':datetime.now(timezone.utc).isoformat(),'texture_slots_verified':10,'shared_views_verified':32,'tests_passed':35,'arithmetic_operations_retained':134,'input_pins':pins,'complete_render_stages_created':False,'actual_material_render_qualified':False,'forge_editor_verified':False,'full_import':False,'complete_assets_delivered_increment':0}
 ROOT.mkdir(exist_ok=False);proof=ROOT/'RESULT.v1.json';proof.write_text(json.dumps(result,indent=2),encoding='utf-8')
 p=S/'record_batch_progress_v1.py';s=p.read_text(encoding='utf-8');anchor=" active_lot=data.get('current_work_sub_lot',active_lot);active_state=data.get('current_work_state',active_state)";assert s.count(anchor)==1
 block=" texture_set_close=read(W/'townsmith-s-paint-texture-set-reopen-v1/RESULT.v1.json')\n if texture_set_close.get('texture_slots_verified')==10:\n  data['current_work_state']='Jeu de dix textures assemble/reouvert : 32 vues partagees, bindings canoniques, 134 operations arithmetiques conservees ; 35 tests candidat passent ; lectures et filtrage explicite source/GPU verifies ; installation sequentielle : '+common_install_queue.get('status','pending')+' ; passes de rendu, sampling implicite et Studio restants'\n  data['texture_set_proof']=str(W/'townsmith-s-paint-texture-set-reopen-v1/RESULT.v1.json')\n  key+=':'+texture_set_close['status']\n"
 p.write_text(s.replace(anchor,block+anchor),encoding='utf-8')
 p=S/'render_lot_dashboard_v1.py';s=p.read_text(encoding='utf-8');anchor="    path = T / 'LOTS_ET_AVANCEMENT.md'";assert s.count(anchor)==1
 block="    texture_set_proof_path=W/'townsmith-s-paint-texture-set-reopen-v1/RESULT.v1.json'\n    output += '**Jeu de textures compose et reouvert :** dix slots originaux, 32 vues partagees et bindings canoniques ; 134 operations arithmetiques conservees. 35 tests passent avec les modules candidats. Passes de rendu completes, admission moteur et Studio restent ouverts. [Preuve](../townsmith-s-paint-texture-set-reopen-v1/RESULT.v1.json). Aucun nouvel asset complet.\\n\\n'\n"
 s=s.replace(anchor,block+anchor);anchor="    meta = T / 'LOTS_ET_AVANCEMENT.v1.json'";assert s.count(anchor)==1
 s=s.replace(anchor,"    manifest['input_pins'][str(texture_set_proof_path)]=hashlib.sha256(texture_set_proof_path.read_bytes()).hexdigest()\n"+anchor);p.write_text(s,encoding='utf-8')
 with (T/'JOURNAL.md').open('a',encoding='utf-8') as f:f.write('\n- '+result['finished_utc']+' — L03 textures du materiau : 10 slots / 32 vues partagees reouverts, bindings canoniques verifies, 134 operations conservees, 35 tests candidat passent. Installation commune et passes de rendu/Studio restantes. [Preuve](../townsmith-s-paint-texture-set-reopen-v1/RESULT.v1.json). Aucun asset complet ajoute.\n')
 subprocess.run([sys.executable,'-B',str(S/'record_batch_progress_v1.py')],check=True,stdout=subprocess.DEVNULL)
 for p,h in read(T/'LOTS_ET_AVANCEMENT.v1.json')['input_pins'].items():assert sha(p)==h,p
 print(json.dumps({k:v for k,v in result.items() if k!='input_pins'}))
if __name__=='__main__':main()
