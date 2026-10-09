from pathlib import Path
from datetime import datetime,timezone
import json,hashlib,subprocess,sys
S=Path(__file__).parent;W=Path('F:/OpenRPG_ImportWork');T=W/'SUIVI_LOTS_HIVEMIND'
def read(p):return json.loads(p.read_text(encoding='utf-8-sig'))
def sha(p):
 with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def main():
 paths=[W/'townsmith-s-paint-native-filtered-gpu-reopen-v1/RESULT.v1.json',W/'townsmith-s-paint-filtered-mip-blend-diagnostic-v1/RESULT.v1.json',W/'townsmith-s-paint-native-filtered-gpu-reopen-v2/RESULT.v1.json']
 for p in paths:
  for name,h in read(p)['input_pins'].items():assert sha(name)==h,name
 proof=paths[2];d=read(proof);assert d['explicit_lod_filtered_controls_qualified'] and d['refused_observations']==0
 p=S/'record_batch_progress_v1.py';s=p.read_text(encoding='utf-8');anchor=" active_lot=data.get('current_work_sub_lot',active_lot);active_state=data.get('current_work_state',active_state)";assert s.count(anchor)==1
 block=" filtered_texture_close=read(W/'townsmith-s-paint-native-filtered-gpu-reopen-v2/RESULT.v1.json')\n if filtered_texture_close.get('status')=='ten_native_filtered_texture_controls_verified':\n  data['current_work_sub_lot']='L03.TOWNSMITH.SHADER.S_PAINT.CONTRAT.INSTALLATION'\n  data['current_work_state']='Dix textures : lectures directes 1 080, controles filtres a LOD explicite 1 080 et normales filtrees 324 sans ecart ; modes communs RG/AG verifies ; metadata Bilinear preservees, interpolation mip native observee ; candidat contrat commun 30 tests passes ; tests moteur : '+ten_texture_engine.get('status','pending')+' ; installation, binding reel, sampling implicite, rendu complet et Studio restants'\n  data['filtered_texture_gpu_proof']=str(W/'townsmith-s-paint-native-filtered-gpu-reopen-v2/RESULT.v1.json')\n  data['next_action']='Terminer les tests moteur actifs, installer le contrat commun et verifier le binding effectif ; confirmer sampling implicite et politique source dans le rendu du materiau, puis Studio'\n  key+=':'+filtered_texture_close['status']\n"
 p.write_text(s.replace(anchor,block+anchor),encoding='utf-8')
 p=S/'render_lot_dashboard_v1.py';s=p.read_text(encoding='utf-8');anchor="    path = T / 'LOTS_ET_AVANCEMENT.md'";assert s.count(anchor)==1
 block="    filtered_texture_progress_paths=[W/'townsmith-s-paint-native-filtered-gpu-reopen-v1/RESULT.v1.json',W/'townsmith-s-paint-filtered-mip-blend-diagnostic-v1/RESULT.v1.json',W/'townsmith-s-paint-native-filtered-gpu-reopen-v2/RESULT.v1.json']\n    output += '**Filtrage a LOD explicite verifie :** dix textures, 1 080 valeurs RGBA et 324 normales filtrees sans ecart. La sonde Unity melange les deux mips adjacents ; ce comportement est reproduit dans le controle GPU, metadata Bilinear conservees. [Refus V1](../townsmith-s-paint-native-filtered-gpu-reopen-v1/RESULT.v1.json) / [Diagnostic](../townsmith-s-paint-filtered-mip-blend-diagnostic-v1/RESULT.v1.json) / [Controle V2](../townsmith-s-paint-native-filtered-gpu-reopen-v2/RESULT.v1.json). Politique complete du sampler, gradients implicites, moteur et Studio restent ouverts. Aucun asset complet ajoute.\\n\\n'\n"
 s=s.replace(anchor,block+anchor);anchor="    meta = T / 'LOTS_ET_AVANCEMENT.v1.json'";assert s.count(anchor)==1
 s=s.replace(anchor,"    for p in filtered_texture_progress_paths:\n        manifest['input_pins'][str(p)]=hashlib.sha256(p.read_bytes()).hexdigest()\n"+anchor);p.write_text(s,encoding='utf-8')
 path=T/'PLAN_EXECUTION_LOTS_20261009.v1.json';plan=read(path);now=datetime.now(timezone.utc).isoformat();plan.update(updated_utc=now,current_receipt=str(proof),active_batch='L03.TOWNSMITH.SHADER.S_PAINT.CONTRAT.INSTALLATION')
 plan['next_action']='Finish active engine tests, install the common transport, qualify actual binding and implicit sampling in the material, then Studio'
 plan['finished_batches'].append({'id':'L03.TOWNSMITH.SHADER.S_PAINT.TEXTURES.FILTER.EXPLICIT_LOD','proof':str(proof),'technical_scope_only':True,'filtered_rgba_cases':1080,'filtered_normal_cases':324,'full_sampler_policy_qualified':False,'actual_material_render_qualified':False,'complete_assets_delivered_increment':0})
 path.write_text(json.dumps(plan,indent=2,ensure_ascii=False),encoding='utf-8')
 path=T/'PLAN_EXECUTION_LOTS_20261009.md';s=path.read_text(encoding='utf-8');s=s.replace('**L03.TOWNSMITH.SHADER.S_PAINT.TEXTURES.ADMISSION — transport commun vers Forge.**','**L03.TOWNSMITH.SHADER.S_PAINT.CONTRAT.INSTALLATION — installation et tests du contrat commun.**')
 s+='\n- Filtrage a LOD explicite : 1 080 RGBA et 324 normales sans ecart, interpolation des mips observee dans la sonde Unity et reproduite ; metadata inchangees, refus V1 conserve. Politique complete et sampling implicite du materiau restent ouverts. [Preuve V2](../townsmith-s-paint-native-filtered-gpu-reopen-v2/RESULT.v1.json). Aucun asset complet ajoute.\n';path.write_text(s,encoding='utf-8')
 with (T/'JOURNAL.md').open('a',encoding='utf-8') as f:f.write('\n- '+now+' — L03 filtre explicite : 1 080/1 080 RGBA et 324/324 normales sans ecart avec interpolation mip observee. Refus V1 : 1 261 observations ; diagnostic preserve. Metadata et seuil inchanges. Installation moteur, binding, gradients implicites et Studio restants. [Preuve](../townsmith-s-paint-native-filtered-gpu-reopen-v2/RESULT.v1.json). Aucun nouvel asset complet.\n')
 subprocess.run([sys.executable,'-B',str(S/'record_batch_progress_v1.py')],check=True,stdout=subprocess.DEVNULL)
 for name,h in read(T/'LOTS_ET_AVANCEMENT.v1.json')['input_pins'].items():assert sha(name)==h,name
 assert read(T/'SUIVI_SIMPLE.v1.json')['active_sub_lot']==plan['active_batch']
 print(json.dumps({'status':'explicit_lod_filter_control_tracking_updated','filtered_rgba_cases':1080,'filtered_normal_cases':324,'complete_assets_delivered_increment':0}))
if __name__=='__main__':main()
