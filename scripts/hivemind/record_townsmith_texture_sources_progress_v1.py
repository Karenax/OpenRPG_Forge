from pathlib import Path
from datetime import datetime, timezone
import hashlib
import json
import shutil

S = Path(__file__).parent
W = Path('F:/OpenRPG_ImportWork')
T = W / 'SUIVI_LOTS_HIVEMIND'
P = W / 'townsmith-s-paint-texture-inventory-v1/RESULT.v1.json'
L = W / 'source-texture-layout-admission-v2/RUN-RESULT.v1.json'

def read(p):
    return json.loads(p.read_text(encoding='utf-8-sig'))

def sha(p):
    with Path(p).open('rb') as f:
        return hashlib.file_digest(f, 'sha256').hexdigest()

proof = read(P)
assert proof['textures_reopened'] == len(proof['rows']) == 10
assert sorted(r['authored_texture_index'] for r in proof['rows']) == list(range(10))
assert sorted(r['normal_profile_index'] for r in proof['rows'] if r['normal_profile_index'] is not None) == [0, 1, 2]
for p, h in proof['input_pins'].items():
    assert sha(p) == h, p
layout = read(L)
assert layout['actual_exit_code'] == 0
assert sha(L.parent / 'NATIVE-RESULT.v1.json') == layout['native_receipt_sha256']
for p, h in read(L.parent / 'PREPARED.v1.json')['input_pins'].items():
    assert sha(p) == h, p
backup = W / 'townsmith-texture-sources-tracker-before-v1'
if not backup.exists():
    backup.mkdir()
    for p in [S/'record_batch_progress_v1.py', S/'render_lot_dashboard_v1.py',
              T/'PLAN_EXECUTION_LOTS_20261009.md', T/'PLAN_EXECUTION_LOTS_20261009.v1.json']:
        shutil.copy2(p, backup/p.name)
p = S/'record_batch_progress_v1.py'
text = p.read_text(encoding='utf-8')
anchor = " active_lot=data.get('current_work_sub_lot',active_lot);active_state=data.get('current_work_state',active_state)"
assert text.count(anchor) == 1
marker = " pipeline_probe=read(W/'source-texture-layout-admission-v3/RUN-RESULT.v1.json')"
if marker not in text:
    block = ''' pipeline_probe=read(W/'source-texture-layout-admission-v3/RUN-RESULT.v1.json')
 texture_sources=read(W/'townsmith-s-paint-texture-inventory-v1/RESULT.v1.json')
 if pipeline_probe and texture_sources:
  data['current_work_sub_lot']='L03.TOWNSMITH.SHADER.S_PAINT.PIPELINE.GPU'
  pipeline_status=pipeline_probe.get('status','unknown')
  if pipeline_status=='actual_bevy_four_group_layouts_created_requirements_measured_inputs_unchanged':
   data['current_work_state']='Layouts des quatre groupes Bevy mesures et crees sur GPU natif ; dix sources de textures et leurs meta SHA verifies ; compatibilite Web, pipeline de rendu, profils importes et Studio ouverts'
   data['next_action']='Lire les budgets complets des groupes natifs, preparer le contrat dix textures compatible puis capturer les profils importes Unity'
  elif pipeline_status=='common_source_texture_layout_native_check_running':
   data['current_work_state']='Admission du groupe materiau natif verifiee : 18 textures / 17 samplers avec extension ; budgets des quatre groupes Bevy en controle ; dix textures originales et meta SHA verifies ; moteur inchange'
   data['next_action']='Lire le controle GPU des quatre groupes ; resoudre les limites Web de base avant l extension commune ; capturer ensuite formats, mips et profils Unity'
  else:
   data['current_work_state']='Controle des quatre groupes Bevy refuse ; admission du groupe materiau V2 et dix sources originales verifies ; conserver et diagnostiquer le refus V3'
   data['next_action']='Diagnostiquer le refus V3 sans relancer les sources deja validees'
  data['townsmith_texture_source_inventory']={'proof':str(W/'townsmith-s-paint-texture-inventory-v1/RESULT.v1.json'),'textures_reopened':10,'independent_normal_profiles':3,'complete_assets_delivered_increment':0}
  text=text.replace('| L01.LOD.01 |','| L03.TOWNSMITH.SHADER.S_PAINT.TEXTURES.SOURCES | Dix textures originales | SHA et copies privees verifies | dix sources, dix meta, trois normal maps ; stockage GPU restant | [Preuve](../townsmith-s-paint-texture-inventory-v1/RESULT.v1.json) |\\n| L03.TOWNSMITH.SHADER.S_PAINT.PIPELINE.GPU | Quatre groupes Bevy | '+data['current_work_state']+' | pipeline rendu et Web non qualifies | [Controle](../source-texture-layout-admission-v3/RUN-RESULT.v1.json) |\\n| L01.LOD.01 |')
  key+=':'+pipeline_status+':'+texture_sources['status']
'''
    p.write_text(text.replace(anchor,block+anchor),encoding='utf-8')
p = S/'render_lot_dashboard_v1.py'
text = p.read_text(encoding='utf-8')
marker = "    texture_sources_path=W/'townsmith-s-paint-texture-inventory-v1/RESULT.v1.json'"
if marker not in text:
    anchor = "    output += 'Prochaine action enregistrée : '+state['next_action']+'.\\n\\n'"
    assert text.count(anchor) == 1
    block = '''    texture_sources_path=W/'townsmith-s-paint-texture-inventory-v1/RESULT.v1.json'
    pipeline_probe_path=W/'source-texture-layout-admission-v3/RUN-RESULT.v1.json'
    if texture_sources_path.exists():
        output += '**Dix textures S_Paint reouvertes :** dix fichiers et dix meta originaux verifies par SHA, copies privees identiques ; trois profils de normales distincts aux indices texture 6/7/8. Formats GPU, mips et sampler effectifs restent a capturer. [Inventaire](../townsmith-s-paint-texture-inventory-v1/RESULT.v1.json). Le controle V2 a cree les layouts natifs (18 textures / 17 samplers proposes), pas un rendu ni une preuve Web. [Quatre groupes en controle](../source-texture-layout-admission-v3/RUN-RESULT.v1.json). Aucun asset complet ajoute.\\n\\n'
'''
    text = text.replace(anchor,block+anchor)
    anchor = "    meta = T / 'LOTS_ET_AVANCEMENT.v1.json'"
    assert text.count(anchor) == 1
    text = text.replace(anchor,"    for p in [texture_sources_path,pipeline_probe_path]:\n        if p.exists():\n            manifest['input_pins'][str(p)] = hashlib.sha256(p.read_bytes()).hexdigest()\n"+anchor)
    p.write_text(text,encoding='utf-8')
p = T/'PLAN_EXECUTION_LOTS_20261009.v1.json'
data = read(p)
data.update(active_batch='L03.TOWNSMITH.SHADER.S_PAINT.PIPELINE.GPU',updated_utc=datetime.now(timezone.utc).isoformat(),current_receipt=str(W/'source-texture-layout-admission-v3/RUN-RESULT.v1.json'))
for batch, receipt, fields in [
    ('L03.TOWNSMITH.SHADER.S_PAINT.TEXTURES.GPU',L,{'native_material_layout_created':True,'web_gpu_verified':False}),
    (proof['sub_lot'],P,{'textures_reopened':10,'independent_normal_profiles':3})]:
    if not any(row['id']==batch for row in data['finished_batches']):
        data['finished_batches'].append({'id':batch,'proof':str(receipt),'complete_assets_delivered_increment':0,**fields})
data['script']=str(Path(__file__))
data['script_sha256']=sha(Path(__file__))
p.write_text(json.dumps(data,indent=2,ensure_ascii=False),encoding='utf-8')
p=T/'PLAN_EXECUTION_LOTS_20261009.md'
text=p.read_text(encoding='utf-8').replace('**L03.TOWNSMITH.SHADER.S_PAINT.TEXTURES.GPU — admission des dix textures.**','**L03.TOWNSMITH.SHADER.S_PAINT.PIPELINE.GPU — budgets des quatre groupes Bevy.**')
text=text.replace('controle GPU prive V2 en compilation/execution','admission native V2 verifiee')
text=text.replace('common_source_texture_layout_native_check_running. L echec initial V1','actual_common_and_proposed_ten_texture_native_layouts_created_inputs_unchanged. L echec initial V1')
if 'dix textures originales et dix meta' not in text:
    text+='\n- L03.TOWNSMITH.SHADER.S_PAINT.TEXTURES.SOURCES : dix textures originales et dix meta SHA verifies ; copies privees identiques, trois normal maps distinctes. Import GPU restant. [Inventaire](../townsmith-s-paint-texture-inventory-v1/RESULT.v1.json).\n- Controle suivant : budgets des quatre groupes Bevy ; le groupe materiau seul demande 18 textures et 17 samplers. [Execution V3](../source-texture-layout-admission-v3/RUN-RESULT.v1.json). Aucun asset complet ajoute.\n'
p.write_text(text,encoding='utf-8')
from record_batch_progress_v1 import main
main()
