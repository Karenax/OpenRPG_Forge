"""Record the ten-texture admission sub-batch without counting delivered assets."""
from pathlib import Path
from datetime import datetime, timezone
import hashlib
import json
import shutil

S = Path(__file__).parent
W = Path('F:/OpenRPG_ImportWork')
T = W / 'SUIVI_LOTS_HIVEMIND'
P = W / 'source-texture-layout-admission-v2/RUN-RESULT.v1.json'
BATCH = 'L03.TOWNSMITH.SHADER.S_PAINT.TEXTURES.GPU'
backup = W / 'source-texture-layout-tracker-before-v2'
if not backup.exists():
    backup.mkdir()
    for p in [S / 'record_batch_progress_v1.py', S / 'render_lot_dashboard_v1.py',
              T / 'PLAN_EXECUTION_LOTS_20261009.md', T / 'PLAN_EXECUTION_LOTS_20261009.v1.json']:
        shutil.copy2(p, backup / p.name)

p = S / 'record_batch_progress_v1.py'
text = p.read_text(encoding='utf-8')
marker = " layout_probe=read(W/'source-texture-layout-admission-v2/RUN-RESULT.v1.json')"
if marker not in text:
    anchor = " active_lot=data.get('current_work_sub_lot',active_lot);active_state=data.get('current_work_state',active_state)"
    assert text.count(anchor) == 1
    block = ''' layout_probe=read(W/'source-texture-layout-admission-v2/RUN-RESULT.v1.json')
 if layout_probe:
  data['current_work_sub_lot']='L03.TOWNSMITH.SHADER.S_PAINT.TEXTURES.GPU'
  layout_status=layout_probe.get('status','unknown')
  if layout_status=='actual_common_and_proposed_ten_texture_native_layouts_created_inputs_unchanged':
   data['current_work_state']='Layouts existant et dix textures crees sur GPU natif ; sources communes inchangees ; pipeline complet, profils textures, fidelite Forge et Studio restent a verifier'
   data['next_action']='Verifier les limites du pipeline complet puis etendre le contrat commun sans modifier les anciens bindings et modes de normales'
  elif layout_status=='common_source_texture_layout_native_check_running':
   data['current_work_state']='Controle GPU prive V2 en compilation/execution ; echec de compilation V1 conserve ; aucune extension du moteur appliquee'
   data['next_action']='Lire le resultat GPU V2, puis verifier les limites du pipeline complet avant l extension commune'
  else:
   data['current_work_state']='Controle GPU V2 refuse ; consulter stderr et le recu ; aucun asset supplementaire livre et moteur commun inchange'
   data['next_action']='Diagnostiquer le refus GPU V2 et preparer une correction versionnee sans masquer l echec'
  data['source_texture_layout_probe']={'proof':str(W/'source-texture-layout-admission-v2/RUN-RESULT.v1.json'),'status':layout_status,'complete_assets_delivered_increment':0,'actual_render_pipeline_created':False}
  text=text.replace('| L01.LOD.01 |','| L03.TOWNSMITH.SHADER.S_PAINT.TEXTURES.GPU | Admission des dix textures | '+data['current_work_state']+' | test natif ; pipeline et asset non qualifies | [Recu](../source-texture-layout-admission-v2/RUN-RESULT.v1.json) |\\n| L01.LOD.01 |')
  key+=':'+layout_status
'''
    p.write_text(text.replace(anchor, block + anchor), encoding='utf-8')

p = S / 'render_lot_dashboard_v1.py'
text = p.read_text(encoding='utf-8')
marker = "    layout_probe_path=W/'source-texture-layout-admission-v2/RUN-RESULT.v1.json'"
if marker not in text:
    anchor = "    output += 'Prochaine action enregistrée : '+state['next_action']+'.\\n\\n'"
    assert text.count(anchor) == 1
    block = '''    layout_probe_path=W/'source-texture-layout-admission-v2/RUN-RESULT.v1.json'
    if layout_probe_path.exists():
        output += '**Sous-lot dix textures :** '+state.get('current_work_state',state['state'])+'. [Controle courant](../source-texture-layout-admission-v2/RUN-RESULT.v1.json). Le premier echec de compilation est conserve. Aucun asset complet ajoute.\\n\\n'
'''
    text = text.replace(anchor, block + anchor)
    anchor = "    meta = T / 'LOTS_ET_AVANCEMENT.v1.json'"
    assert text.count(anchor) == 1
    text = text.replace(anchor, "    if layout_probe_path.exists():\n        manifest['input_pins'][str(layout_probe_path)] = hashlib.sha256(layout_probe_path.read_bytes()).hexdigest()\n" + anchor)
    p.write_text(text, encoding='utf-8')

proof = json.loads(P.read_text(encoding='utf-8'))
now = datetime.now(timezone.utc).isoformat()
p = T / 'PLAN_EXECUTION_LOTS_20261009.v1.json'
data = json.loads(p.read_text(encoding='utf-8'))
data.update(active_batch=BATCH, updated_utc=now, current_receipt=str(P), full_import=False)
data['next_action'] = 'Admission GPU des dix textures, puis contrat commun, profils importes, comparaison Forge et usage editeur'
data['script'] = str(Path(__file__))
data['script_sha256'] = hashlib.sha256(Path(__file__).read_bytes()).hexdigest()
p.write_text(json.dumps(data, indent=2, ensure_ascii=False), encoding='utf-8')
p = T / 'PLAN_EXECUTION_LOTS_20261009.md'
text = p.read_text(encoding='utf-8')
start = text.index('## Tranche actuelle')
end = text.index('## Comment suivre', start)
current = '''## Tranche actuelle

**L03.TOWNSMITH.SHADER.S_PAINT.TEXTURES.GPU — admission des dix textures.**

1. Programme source et WGSL : 134 operations et types fragment verifies ; dix textures et trois normal maps requises.
2. Controle natif des layouts existant et propose : ''' + proof['status'] + '''. L echec initial V1 est conserve ; aucune extension du moteur n est encore appliquee.
3. Restant : limites du pipeline complet, contrat commun compatible, profils et stockage des textures, comparaison GPU source/Forge.
4. Livraison : integration des prefabs, recherche, image, apercu, placement, sauvegarde/reouverture et runtime.

[Recu du sous-lot](../source-texture-layout-admission-v2/RUN-RESULT.v1.json). Aucun asset complet ajoute par ces controles techniques. L01 et ses refus restent ouverts.

'''
p.write_text(text[:start] + current + text[end:], encoding='utf-8')

from record_batch_progress_v1 import main
main()
