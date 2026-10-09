from pathlib import Path
from datetime import datetime,timezone
import json,hashlib,shutil
S=Path(__file__).parent;W=Path('F:/OpenRPG_ImportWork');T=W/'SUIVI_LOTS_HIVEMIND'
ROOT=W/'townsmith-s-paint-compressed-storage-v1';C=W/'townsmith-s-paint-compressed-storage-reopen-v1/RESULT.v1.json'
run=json.loads((ROOT/'RUN-RESULT.v1.json').read_text());closed=json.loads(C.read_text()) if C.exists() else None
if closed:
 assert closed['compressed_mips_verified']==96 and closed['source_to_readable_probe_samples_verified']==864
 for p,h in closed['input_pins'].items():
  with Path(p).open('rb') as f:assert hashlib.file_digest(f,'sha256').hexdigest()==h,p
backup=W/'townsmith-compressed-storage-tracker-before-v1'
if not backup.exists():
 backup.mkdir()
 for p in [S/'record_batch_progress_v1.py',S/'render_lot_dashboard_v1.py',T/'PLAN_EXECUTION_LOTS_20261009.md',T/'PLAN_EXECUTION_LOTS_20261009.v1.json']:shutil.copy2(p,backup/p.name)
p=S/'record_batch_progress_v1.py';text=p.read_text(encoding='utf-8');marker=" compressed_run=read(W/'townsmith-s-paint-compressed-storage-v1/RUN-RESULT.v1.json')"
if marker not in text:
 anchor=" active_lot=data.get('current_work_sub_lot',active_lot);active_state=data.get('current_work_state',active_state)";assert text.count(anchor)==1
 block=''' compressed_run=read(W/'townsmith-s-paint-compressed-storage-v1/RUN-RESULT.v1.json')
 compressed_close=read(W/'townsmith-s-paint-compressed-storage-reopen-v1/RESULT.v1.json')
 if compressed_run:
  data['current_work_sub_lot']='L03.TOWNSMITH.SHADER.S_PAINT.TEXTURES.BC_STORAGE'
  if compressed_close:
   data['current_work_sub_lot']='L03.TOWNSMITH.SHADER.S_PAINT.TEXTURES.TRANSPORT'
   data['current_work_state']='Huit textures compressees / 96 mips verifies ; 864 mesures de texels avant/apres readable bit-exactes ; huit reglages restaures dans la copie ; originaux inchangees ; dix ressources natives a raccorder dans Forge'
   data['next_action']='Composer les dix ressources natives sans changer formats, mips, sRGB ou samplers ; adapter le contrat commun et comparer source/Forge sur GPU'
  elif compressed_run.get('status')=='isolated_townsmith_compressed_texture_extraction_running':
   data['current_work_state']='Extraction Unity des huit textures compressees en cours dans un nouveau projet isole sur F ; import initial des packages, puis mips natifs et controle de texels avant/apres readable ; aucune nouvelle qualification acquise'
   data['next_action']='Attendre le processus Unity de ce projet isole, puis rouvrir les 96 mips et verifier formats, reglages et mesures GPU'
  else:
   data['current_work_state']='Execution d extraction compressee terminee ; verifier le recu natif et les erreurs avant toute qualification ; originaux et echecs preserves'
   data['next_action']='Lire le resultat natif et executer la reouverture des 96 mips, ou diagnostiquer le refus conserve'
  data['townsmith_compressed_texture_work']={'run_receipt':str(W/'townsmith-s-paint-compressed-storage-v1/RUN-RESULT.v1.json'),'closure_receipt':str(W/'townsmith-s-paint-compressed-storage-reopen-v1/RESULT.v1.json') if compressed_close else None,'complete_assets_delivered_increment':0}
  key+=':'+compressed_run['status']+(':'+compressed_close['status'] if compressed_close else '')
'''
 p.write_text(text.replace(anchor,block+anchor),encoding='utf-8')
p=S/'render_lot_dashboard_v1.py';text=p.read_text(encoding='utf-8');marker="    compressed_run_path=W/'townsmith-s-paint-compressed-storage-v1/RUN-RESULT.v1.json'"
if marker not in text:
 anchor="    output += 'Prochaine action enregistrée : '+state['next_action']+'.\\n\\n'";assert text.count(anchor)==1
 block="    compressed_run_path=W/'townsmith-s-paint-compressed-storage-v1/RUN-RESULT.v1.json'\n    compressed_close_path=W/'townsmith-s-paint-compressed-storage-reopen-v1/RESULT.v1.json'\n    output += '**Extraction BC isolee :** '+state.get('current_work_state',state['state'])+'. [Execution](../townsmith-s-paint-compressed-storage-v1/RUN-RESULT.v1.json). Aucun asset complet ajoute.\\n\\n'\n"
 text=text.replace(anchor,block+anchor);anchor="    meta = T / 'LOTS_ET_AVANCEMENT.v1.json'";assert text.count(anchor)==1
 text=text.replace(anchor,"    for p in [compressed_run_path,compressed_close_path]:\n        if p.exists():\n            manifest['input_pins'][str(p)] = hashlib.sha256(p.read_bytes()).hexdigest()\n"+anchor)
 p.write_text(text,encoding='utf-8')
p=T/'PLAN_EXECUTION_LOTS_20261009.v1.json';d=json.loads(p.read_text())
d.update(active_batch='L03.TOWNSMITH.SHADER.S_PAINT.TEXTURES.TRANSPORT' if closed else 'L03.TOWNSMITH.SHADER.S_PAINT.TEXTURES.BC_STORAGE',updated_utc=datetime.now(timezone.utc).isoformat(),current_receipt=str(C if closed else ROOT/'RUN-RESULT.v1.json'))
if closed and not any(r['id']==closed['sub_lot'] for r in d['finished_batches']):d['finished_batches'].append({'id':closed['sub_lot'],'proof':str(C),'compressed_mips_verified':96,'complete_assets_delivered_increment':0})
d['script']=str(Path(__file__));d['script_sha256']=hashlib.sha256(Path(__file__).read_bytes()).hexdigest()
p.write_text(json.dumps(d,indent=2,ensure_ascii=False),encoding='utf-8')
p=T/'PLAN_EXECUTION_LOTS_20261009.md';text=p.read_text(encoding='utf-8')
note='\n- Extraction BC lancee dans un nouveau projet isole sur F ; controles avant/apres readable et restoration des reglages. [Execution](../townsmith-s-paint-compressed-storage-v1/RUN-RESULT.v1.json).\n'
if note not in text:text+=note
if closed:
 note='\n- Extraction BC reouverte : huit textures, 96 mips et 864 mesures avant/apres bit-exactes. [Preuve](../townsmith-s-paint-compressed-storage-reopen-v1/RESULT.v1.json). Prochaine tranche : transport des dix ressources dans Forge. Aucun asset complet ajoute.\n'
 if note not in text:text+=note
p.write_text(text,encoding='utf-8')
from record_batch_progress_v1 import main
main()
