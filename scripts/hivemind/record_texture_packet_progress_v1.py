from pathlib import Path
from datetime import datetime,timezone
import json,hashlib,subprocess,sys
S=Path(__file__).parent;W=Path('F:/OpenRPG_ImportWork');T=W/'SUIVI_LOTS_HIVEMIND';ROOT=W/'townsmith-native-texture-contract-packets-seal-v1'
def read(p):return json.loads(p.read_text(encoding='utf-8-sig'))
def sha(p):
 with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def main():
 checkroot=W/'townsmith-native-texture-contract-packets-reopen-v1';run=read(checkroot/'RESULT.v1.json');assert run['test_exit_code']==0
 for p,h in run['input_pins'].items():assert sha(p)==h,p
 assert '34 passed; 0 failed' in (checkroot/'tests.stdout.log').read_text(encoding='utf-8')
 packet=W/'townsmith-native-texture-contract-packets-v1/TEXTURE-CONTRACTS.v1.json';d=read(packet);assert len(d['rows'])==10
 pins=dict(run['input_pins']);views=0
 for row in d['rows']:
  assert row['texture']['imported_storage']['native_capture']['source_guid']==row['guid']
  for v in row['local_buffer_views']:assert sha(v['file'])==v['sha256'];assert Path(v['file']).stat().st_size==v['bytes'];pins[v['file']]=v['sha256'];views+=1
 for p in [packet,checkroot/'RESULT.v1.json',checkroot/'tests.stdout.log',Path(__file__)]:pins[str(p)]=sha(p)
 result={'status':'ten_serialized_native_texture_contracts_independently_reopened_34_tests','finished_utc':datetime.now(timezone.utc).isoformat(),'texture_contracts_reopened':10,'local_buffer_references_verified':views,'tests_passed':34,'input_pins':pins,'candidate_common_type_validation_verified':True,'actual_engine_material_admission_verified':False,'full_import':False,'complete_assets_delivered_increment':0}
 ROOT.mkdir(exist_ok=False);proof=ROOT/'RESULT.v1.json';proof.write_text(json.dumps(result,indent=2),encoding='utf-8')
 p=S/'record_batch_progress_v1.py';s=p.read_text(encoding='utf-8');anchor=" active_lot=data.get('current_work_sub_lot',active_lot);active_state=data.get('current_work_state',active_state)";assert s.count(anchor)==1
 block=" texture_packets=read(W/'townsmith-native-texture-contract-packets-seal-v1/RESULT.v1.json')\n if texture_packets.get('texture_contracts_reopened')==10:\n  data['current_work_state']+=' ; dix contrats textures serialises et reouverts, 40 references buffers verifiees ; 34 tests avec les modules candidats communs passent ; admission moteur restante'\n  data['texture_contract_packets_proof']=str(W/'townsmith-native-texture-contract-packets-seal-v1/RESULT.v1.json')\n  key+=':'+texture_packets['status']\n"
 p.write_text(s.replace(anchor,block+anchor),encoding='utf-8')
 p=S/'render_lot_dashboard_v1.py';s=p.read_text(encoding='utf-8');anchor="    path = T / 'LOTS_ET_AVANCEMENT.md'";assert s.count(anchor)==1
 block="    texture_packet_proof_path=W/'townsmith-native-texture-contract-packets-seal-v1/RESULT.v1.json'\n    output += '**Contrats de ressources exportes et reouverts :** dix textures avec leurs captures, PNG et meta ; 40 references de buffers verifiees, 34 tests des modules candidats passent. Ces ressources attendent assemblage du materiau et admission moteur ; aucun objet placable ni livraison complete ajoute. [Preuve](../townsmith-native-texture-contract-packets-seal-v1/RESULT.v1.json).\\n\\n'\n"
 s=s.replace(anchor,block+anchor);anchor="    meta = T / 'LOTS_ET_AVANCEMENT.v1.json'";assert s.count(anchor)==1
 s=s.replace(anchor,"    manifest['input_pins'][str(texture_packet_proof_path)]=hashlib.sha256(texture_packet_proof_path.read_bytes()).hexdigest()\n"+anchor);p.write_text(s,encoding='utf-8')
 with (T/'JOURNAL.md').open('a',encoding='utf-8') as f:f.write('\n- '+result['finished_utc']+' — L03 ressources : dix contrats SourceGraphTexture exportes/reouverts, 40 references buffers SHA/tailles verifiees ; 34 tests candidats passes. Assemblage/admission moteur et Studio restants. [Preuve](../townsmith-native-texture-contract-packets-seal-v1/RESULT.v1.json). Aucun asset complet ajoute.\n')
 subprocess.run([sys.executable,'-B',str(S/'record_batch_progress_v1.py')],check=True,stdout=subprocess.DEVNULL)
 for p,h in read(T/'LOTS_ET_AVANCEMENT.v1.json')['input_pins'].items():assert sha(p)==h,p
 print(json.dumps({k:v for k,v in result.items() if k!='input_pins'}))
if __name__=='__main__':main()
