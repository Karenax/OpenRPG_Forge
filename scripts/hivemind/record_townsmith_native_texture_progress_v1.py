from pathlib import Path
from datetime import datetime,timezone
import json,hashlib,shutil
S=Path(__file__).parent;W=Path('F:/OpenRPG_ImportWork');T=W/'SUIVI_LOTS_HIVEMIND'
native_path=W/'townsmith-s-paint-native-textures-reopen-v1/RESULT.v1.json'
layout_path=W/'source-texture-layout-admission-reopen-v3/RESULT.v1.json'
native=json.loads(native_path.read_text());layout=json.loads(layout_path.read_text())
assert native['native_unorm_mips_verified']==24 and layout['native_pipeline_layouts_verified']==6
for proof in [native,layout]:
 for p,h in proof['input_pins'].items():
  with Path(p).open('rb') as f:assert hashlib.file_digest(f,'sha256').hexdigest()==h,p
backup=W/'townsmith-native-textures-tracker-before-v1'
assert not backup.exists();backup.mkdir()
for p in [S/'record_batch_progress_v1.py',S/'render_lot_dashboard_v1.py',T/'PLAN_EXECUTION_LOTS_20261009.md',T/'PLAN_EXECUTION_LOTS_20261009.v1.json']:shutil.copy2(p,backup/p.name)
p=S/'record_batch_progress_v1.py';text=p.read_text(encoding='utf-8')
anchor=" active_lot=data.get('current_work_sub_lot',active_lot);active_state=data.get('current_work_state',active_state)"
assert text.count(anchor)==1
block=''' native_textures=read(W/'townsmith-s-paint-native-textures-reopen-v1/RESULT.v1.json')
 if native_textures:
  data['current_work_sub_lot']='L03.TOWNSMITH.SHADER.S_PAINT.TEXTURES.BC_STORAGE'
  data['current_work_state']='Dix profils Unity verifies ; deux textures UNORM / 24 mips / 44 739 240 octets extraits et SHA verifies ; huit textures BC1/BC3 refusees en readback GPU direct ; sources inchangees ; extraction isolee restante'
  data['next_action']='Extraire les huit textures compressees avec la recette de copie readable isolee ; conserver les reglages originaux et verifier formats, mips et sampling apres reimport ; integration Forge et Studio restantes'
  data['townsmith_native_texture_profiles']={'proof':str(W/'townsmith-s-paint-native-textures-reopen-v1/RESULT.v1.json'),'profiles_verified':10,'native_mips_verified':24,'compressed_textures_refused':8,'complete_assets_delivered_increment':0}
  text=text.replace('| L01.LOD.01 |','| L03.TOWNSMITH.SHADER.S_PAINT.TEXTURES.NATIVE | Profils natifs et stockage | profils verifies ; stockage partiel | 10 profils ; 24 mips UNORM verifies ; 96 mips compresses refuses | [Preuve](../townsmith-s-paint-native-textures-reopen-v1/RESULT.v1.json) |\\n| L01.LOD.01 |')
  key+=':'+native_textures['status']
'''
p.write_text(text.replace(anchor,block+anchor),encoding='utf-8')
p=S/'render_lot_dashboard_v1.py';text=p.read_text(encoding='utf-8')
anchor="    output += 'Prochaine action enregistrée : '+state['next_action']+'.\\n\\n'"
assert text.count(anchor)==1
block="    native_texture_reopen_path=W/'townsmith-s-paint-native-textures-reopen-v1/RESULT.v1.json'\n    layout_reopen_path=W/'source-texture-layout-admission-reopen-v3/RESULT.v1.json'\n    output += '**Dernier controle textures :** dix profils Unity verifies ; deux textures UNORM completes (24 mips, 44 739 240 octets). Huit textures compressees BC1/BC3 ne sont pas lisibles par AsyncGPUReadback : refus retenus et recette isolee a executer. [Preuve textures](../townsmith-s-paint-native-textures-reopen-v1/RESULT.v1.json). Six layouts Bevy natifs reouverts ; limites Web de base depassees, aucun shader de rendu ni navigateur qualifie. [Preuve layouts](../source-texture-layout-admission-reopen-v3/RESULT.v1.json). Aucun asset complet ajoute.\\n\\n'\n"
text=text.replace(anchor,block+anchor)
anchor="    meta = T / 'LOTS_ET_AVANCEMENT.v1.json'";assert text.count(anchor)==1
text=text.replace(anchor,"    for p in [native_texture_reopen_path,layout_reopen_path]:\n        manifest['input_pins'][str(p)] = hashlib.sha256(p.read_bytes()).hexdigest()\n"+anchor)
p.write_text(text,encoding='utf-8')
p=T/'PLAN_EXECUTION_LOTS_20261009.v1.json';d=json.loads(p.read_text())
d.update(active_batch='L03.TOWNSMITH.SHADER.S_PAINT.TEXTURES.BC_STORAGE',updated_utc=datetime.now(timezone.utc).isoformat(),current_receipt=str(native_path),next_action=native['next_action'])
for proof,path in [(layout,layout_path),(native,native_path)]:
 if not any(r['id']==proof['sub_lot'] for r in d['finished_batches']):d['finished_batches'].append({'id':proof['sub_lot'],'proof':str(path),'technical_scope_only':True,'complete_assets_delivered_increment':0})
d['script']=str(Path(__file__));d['script_sha256']=hashlib.sha256(Path(__file__).read_bytes()).hexdigest()
p.write_text(json.dumps(d,indent=2,ensure_ascii=False),encoding='utf-8')
p=T/'PLAN_EXECUTION_LOTS_20261009.md';text=p.read_text(encoding='utf-8');start=text.index('## Tranche actuelle');end=text.index('## Comment suivre',start)
text=text[:start]+'''## Tranche actuelle

**L03.TOWNSMITH.SHADER.S_PAINT.TEXTURES.BC_STORAGE — extraction des huit textures compressees.**

- Verifie : programme WGSL de 134 operations, dix sources de textures et meta, dix profils Unity, six layouts de pipeline natifs.
- Stockage extrait : deux textures UNORM completes, 24 mips, 44 739 240 octets controles.
- Refus conserve : readback GPU direct des huit textures BC1/BC3 (96 mips). La recette readable isolee existante doit etre reprise et controlee.
- Restant : extraction compressee, sampling et normales, contrat commun compatible, rendu Forge, prefabs et usage editeur.

[Textures](../townsmith-s-paint-native-textures-reopen-v1/RESULT.v1.json) / [Layouts](../source-texture-layout-admission-reopen-v3/RESULT.v1.json). Aucun asset complet ajoute ; limites navigateur et rendu restent ouverts.

'''+text[end:]
text+='\n- L03 textures natifs : dix profils reouverts, deux chaines UNORM completes ; huit refus compresses explicites. Sources et reglages prives inchangees. Extraction BC isolee suivante.\n'
p.write_text(text,encoding='utf-8')
from record_batch_progress_v1 import main
main()
