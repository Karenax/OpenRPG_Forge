from pathlib import Path
from datetime import datetime,timezone
import json,hashlib,subprocess,sys
S=Path(__file__).parent;W=Path('F:/OpenRPG_ImportWork');T=W/'SUIVI_LOTS_HIVEMIND'
def read(p):return json.loads(p.read_text(encoding='utf-8-sig'))
def sha(p):
 with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def main():
 proof=W/'townsmith-s-paint-native-texture-gpu-control-reopen-v2/RESULT.v1.json';d=read(proof)
 assert d['status']=='native_ten_texture_gpu_load_and_three_normal_decode_controls_verified' and d['refused_observations']==0
 for name,h in d['input_pins'].items():assert sha(name)==h,name
 p=S/'record_batch_progress_v1.py';s=p.read_text(encoding='utf-8');anchor=" active_lot=data.get('current_work_sub_lot',active_lot);active_state=data.get('current_work_state',active_state)";assert s.count(anchor)==1
 block=" native_texture_gpu=read(W/'townsmith-s-paint-native-texture-gpu-control-reopen-v2/RESULT.v1.json')\n if native_texture_gpu.get('status')=='native_ten_texture_gpu_load_and_three_normal_decode_controls_verified':\n  data['current_work_state']='Dix ressources natives / 120 mips uploades sur GPU D3D12 ; 1 080 lectures et 324 decodages normaux sans ecart avec modes communs (source RG/AG 1 vers ABI 2) (864 references GPU Unity, 216 references bytes UNORM) ; contrat candidat generique 30 tests passes ; tests moteur : '+ten_texture_engine.get('status','pending')+' ; installation commune, binding uniforms reel, sampling filtre, rendu complet et Studio restants'\n  data['native_texture_gpu_control_proof']=str(W/'townsmith-s-paint-native-texture-gpu-control-reopen-v2/RESULT.v1.json')\n  data['next_action']='Installer le contrat commun apres les tests moteur actifs, verifier le binding effectif des uniforms de normales, puis verifier sampling filtre, rendu du materiau et usage Studio'\n  key+=':'+native_texture_gpu['status']\n"
 p.write_text(s.replace(anchor,block+anchor),encoding='utf-8')
 p=S/'render_lot_dashboard_v1.py';s=p.read_text(encoding='utf-8');anchor="    path = T / 'LOTS_ET_AVANCEMENT.md'";assert s.count(anchor)==1
 block="    native_texture_gpu_proof_path=W/'townsmith-s-paint-native-texture-gpu-control-reopen-v2/RESULT.v1.json'\n    output += '**Lectures GPU natives du prototype verifiees :** dix textures / 120 mips, 1 080 lectures et 324 decodages normaux sans ecart avec modes communs (source RG/AG 1 vers ABI 2) ; 864 references directes GPU Unity et 216 references des bytes natifs UNORM. Sampling filtre, binding effectif des uniforms, rendu materiau, Web et Studio restent ouverts. [Preuve](../townsmith-s-paint-native-texture-gpu-control-reopen-v2/RESULT.v1.json). Aucun asset complet ajoute.\\n\\n'\n"
 s=s.replace(anchor,block+anchor);anchor="    meta = T / 'LOTS_ET_AVANCEMENT.v1.json'";assert s.count(anchor)==1
 s=s.replace(anchor,"    manifest['input_pins'][str(native_texture_gpu_proof_path)]=hashlib.sha256(native_texture_gpu_proof_path.read_bytes()).hexdigest()\n"+anchor);p.write_text(s,encoding='utf-8')
 planpath=T/'PLAN_EXECUTION_LOTS_20261009.v1.json';plan=read(planpath);now=datetime.now(timezone.utc).isoformat();plan['updated_utc']=now;plan['current_receipt']=str(proof)
 plan['next_action']=d['next_action'];plan['finished_batches'].append({'id':'L03.TOWNSMITH.SHADER.S_PAINT.TEXTURES.NORMALS.ABI_CONTROL','proof':str(proof),'technical_scope_only':True,'mips_uploaded':120,'load_cases_verified':1080,'normal_decode_cases_verified':324,'actual_material_render_qualified':False,'complete_assets_delivered_increment':0})
 planpath.write_text(json.dumps(plan,indent=2,ensure_ascii=False),encoding='utf-8')
 with (T/'PLAN_EXECUTION_LOTS_20261009.md').open('a',encoding='utf-8') as f:f.write('\n- GPU natif avec modes communs : dix ressources et 120 mips uploades, 1 080 lectures et 324 normales sans ecart. 864 references GPU source / 216 references bytes UNORM. ABI commun, filtrage, rendu complet et Studio restants. [Preuve](../townsmith-s-paint-native-texture-gpu-control-reopen-v2/RESULT.v1.json). Aucun asset complet ajoute.\n')
 with (T/'JOURNAL.md').open('a',encoding='utf-8') as f:f.write('\n- '+now+' — L03 textures GPU avec modes communs : 10 ressources / 120 mips, lectures 1 080/1 080 et decodages 324/324 sans ecart. Contrat moteur, ABI, filtre, rendu et Studio restants. [Preuve](../townsmith-s-paint-native-texture-gpu-control-reopen-v2/RESULT.v1.json). Aucun nouvel asset complet.\n')
 subprocess.run([sys.executable,'-B',str(S/'record_batch_progress_v1.py')],check=True,stdout=subprocess.DEVNULL)
 for name,h in read(T/'LOTS_ET_AVANCEMENT.v1.json')['input_pins'].items():assert sha(name)==h,name
 print(json.dumps({'status':'native_texture_gpu_control_tracking_updated','load_cases_verified':1080,'normal_decode_cases_verified':324,'complete_assets_delivered_increment':0}))
if __name__=='__main__':main()
