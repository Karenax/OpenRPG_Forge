"""Refresh lot tracking from reopened resource evidence; retain historic refusals."""
from pathlib import Path
from datetime import datetime,timezone
import json,hashlib,shutil,subprocess,sys
S=Path(__file__).parent;W=Path('F:/OpenRPG_ImportWork');T=W/'SUIVI_LOTS_HIVEMIND'
def read(p):return json.loads(p.read_text(encoding='utf-8-sig'))
def sha(p):
    with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def main():
    paths=[W/'townsmith-s-paint-compressed-storage-reopen-v2/RESULT.v1.json',W/'townsmith-s-paint-native-resources-reopen-v1/RESULT.v1.json']
    for p in paths:
        for name,h in read(p)['input_pins'].items():assert sha(name)==h,name
    backup=W/'native-resources-tracker-before-v1';backup.mkdir(exist_ok=False)
    for p in [S/'record_batch_progress_v1.py',S/'render_lot_dashboard_v1.py']+list(T.glob('*')):
        if p.is_file():shutil.copy2(p,backup/p.name)
    p=S/'record_batch_progress_v1.py';s=p.read_text(encoding='utf-8')
    anchor=" active_lot=data.get('current_work_sub_lot',active_lot);active_state=data.get('current_work_state',active_state)"
    assert s.count(anchor)==1
    block=''' compressed_v2_path=W/'townsmith-s-paint-compressed-storage-reopen-v2/RESULT.v1.json'
 resources_close_path=W/'townsmith-s-paint-native-resources-reopen-v1/RESULT.v1.json'
 resources_close=read(resources_close_path)
 if resources_close.get('status')=='ten_native_resources_independently_reopened_120_mips_exact':
  data['current_work_sub_lot']='L03.TOWNSMITH.SHADER.S_PAINT.TEXTURES.ADMISSION'
  data['current_work_state']='Dix ressources natives assemblees et reouvertes : 120 mips / 78 293 832 octets exacts ; huit textures compressees et 864 mesures avant/apres bit-exactes, reglages restaures ; sources et GUID conserves ; 25 tests de contrat passes ; tests moteur : '+ten_texture_engine.get('status','pending')+' ; admission, sampling Forge, normales, rendu et Studio restants'
  data['next_action']='Admettre explicitement les deux protocoles natifs dans le stockage commun sans reecrire les captures ; verifier les dix ressources puis comparer sampling et normales source/Forge sur GPU'
  data['townsmith_native_resources']={'proof':str(resources_close_path),'resources_verified':10,'mips_verified':120,'bytes_verified':78293832,'complete_assets_delivered_increment':0,'forge_editor_verified':False}
  text=text.replace('| L01.LOD.01 |','| L03.TOWNSMITH.SHADER.S_PAINT.TEXTURES.BC_STORAGE | Extraction compressee isolee | technique verifie | 8 textures / 96 mips ; 864 observations bit-exactes ; 8 reglages restaures ; V1 refuse conserve | [Preuve V2](../townsmith-s-paint-compressed-storage-reopen-v2/RESULT.v1.json) |\\n| L03.TOWNSMITH.SHADER.S_PAINT.TEXTURES.TRANSPORT | Ressources natives composees | reouverture verifiee | 10 textures / 120 mips / 78 293 832 octets ; admission moteur restante | [Preuve](../townsmith-s-paint-native-resources-reopen-v1/RESULT.v1.json) |\\n| L01.LOD.01 |')
  key+=':'+resources_close['status']
'''
    p.write_text(s.replace(anchor,block+anchor),encoding='utf-8')
    p=S/'render_lot_dashboard_v1.py';s=p.read_text(encoding='utf-8')
    anchor="    output += 'Prochaine action enregistrée : '+state['next_action']+'.\\n\\n'"
    assert s.count(anchor)==1
    block="    resource_progress_paths=[W/'townsmith-s-paint-compressed-storage-reopen-v2/RESULT.v1.json',W/'townsmith-s-paint-native-resources-reopen-v1/RESULT.v1.json']\n    output += '**Dernier lot technique termine :** dix ressources natives reouvertes, 120 mips et 78 293 832 octets exacts ; les 864 mesures des huit textures compressees sont bit-exactes et leurs reglages restaures. [Extraction V2](../townsmith-s-paint-compressed-storage-reopen-v2/RESULT.v1.json) / [Assemblage reouvert](../townsmith-s-paint-native-resources-reopen-v1/RESULT.v1.json). Les anciens refus sont conserves ; admission Forge, normales, rendu et Studio restent ouverts. Aucun nouvel asset complet livre.\\n\\n'\n"
    s=s.replace(anchor,block+anchor)
    anchor="    meta = T / 'LOTS_ET_AVANCEMENT.v1.json'";assert s.count(anchor)==1
    s=s.replace(anchor,"    for p in resource_progress_paths:\n        manifest['input_pins'][str(p)]=hashlib.sha256(p.read_bytes()).hexdigest()\n"+anchor)
    # The historical caption describes the older partial capture, not the latest state.
    s=s.replace('**Dernier controle textures :**','**Controle historique avant extraction BC :**')
    p.write_text(s,encoding='utf-8')
    planpath=T/'PLAN_EXECUTION_LOTS_20261009.v1.json';d=read(planpath)
    d['active_batch']='L03.TOWNSMITH.SHADER.S_PAINT.TEXTURES.ADMISSION'
    d['updated_utc']=datetime.now(timezone.utc).isoformat();d['current_receipt']=str(paths[1])
    d['next_action']='Admission commune des deux protocoles natifs, puis sampling et normales GPU, rendu et Studio'
    for lot,p in [('L03.TOWNSMITH.SHADER.S_PAINT.TEXTURES.BC_STORAGE',paths[0]),('L03.TOWNSMITH.SHADER.S_PAINT.TEXTURES.TRANSPORT',paths[1])]:
        assert not any(x['id']==lot for x in d['finished_batches'])
        d['finished_batches'].append({'id':lot,'proof':str(p),'technical_scope_only':True,'complete_assets_delivered_increment':0})
    planpath.write_text(json.dumps(d,indent=2,ensure_ascii=False),encoding='utf-8')
    p=T/'PLAN_EXECUTION_LOTS_20261009.md';s=p.read_text(encoding='utf-8')
    start=s.index('## Tranche actuelle');end=s.index('## Comment suivre',start)
    s=s[:start]+'''## Tranche actuelle

**L03.TOWNSMITH.SHADER.S_PAINT.TEXTURES.ADMISSION — transport commun vers Forge.**

- Termine techniquement : dix ressources natives reouvertes, 120 mips, 78 293 832 octets exacts. Sources, meta et captures originales conservees.
- Extraction compressee V2 : huit textures, 96 mips, 864 observations bit-exactes, huit reglages restaures. L'echec de restauration V1 reste conserve.
- Contrat dix slots : 25 tests passes ; tests moteur en compilation. Compatibilite GPU et navigateur a verifier.
- Suivant : admission des deux protocoles natifs, sampling et normales GPU, rendu du materiau, prefabs complets et usage editeur.

[Extraction](../townsmith-s-paint-compressed-storage-reopen-v2/RESULT.v1.json) / [Assemblage reouvert](../townsmith-s-paint-native-resources-reopen-v1/RESULT.v1.json). Aucun nouvel asset complet livre.

'''+s[end:]
    s+='\n- Extraction BC V2 et assemblage des dix ressources techniquement verifies ; admission et rendu Forge suivants. [Recu](../townsmith-s-paint-native-resources-reopen-v1/RESULT.v1.json).\n'
    p.write_text(s,encoding='utf-8')
    with (T/'JOURNAL.md').open('a',encoding='utf-8') as f:f.write('\n- '+d['updated_utc']+' — L03 textures : extraction BC V2 8/8, restauration 8/8, observations 864/864 bit-exactes ; assemblage reouvert 10/10 ressources, 120/120 mips, 78 293 832 octets. Aucun asset complet ajoute. Prochaine tranche : admission commune. [Preuve](../townsmith-s-paint-native-resources-reopen-v1/RESULT.v1.json).\n')
    subprocess.run([sys.executable,'-B',str(S/'record_batch_progress_v1.py')],check=True,stdout=subprocess.DEVNULL)
    state=read(T/'SUIVI_SIMPLE.v1.json');assert state['active_sub_lot']==d['active_batch']
    for p,h in read(T/'LOTS_ET_AVANCEMENT.v1.json')['input_pins'].items():assert sha(p)==h,p
    print(json.dumps({'active_lot':state['active_sub_lot'],'resources_verified':10,'native_mips_verified':120,'tracker_pins_verified':True,'complete_assets_delivered_increment':0}))
if __name__=='__main__':main()
