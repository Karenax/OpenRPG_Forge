"""Retain source material/pass contracts and highlight unresolved pass-dependent behavior."""
from pathlib import Path
from datetime import datetime,timezone
import hashlib,json
W=Path('F:/OpenRPG_ImportWork');S=Path(__file__).resolve().parent;O=W/'gladiator-render-state-delivery-v433'
def read(p):return json.loads(Path(p).read_text('utf-8-sig'))
def sha(p):
    with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
assert not O.exists()
p=W/'gladiator-render-contracts-v432/RESULT.v1.json';j=read(p)
assert len(j['rows'])==6 and j['pass_state_count']==17
for path,h in j['input_pins'].items():assert sha(path)==h
double=[r for r in j['rows'] if r['native_resolved_state']['_DoubleSidedEnable']==1]
alpha=[r for r in j['rows'] if r['native_resolved_state']['_AlphaCutoffEnable']==1]
assert len(double)==5 and len(alpha)==4
assert all(r['original_double_sided_constants_xyz']==[1,1,-1] and r['native_resolved_state']['_DoubleSidedNormalMode']==1 for r in double)
assert all(r['backface_equation_must_use_native_constants'] for r in j['rows'])
inputs=[p,Path(__file__),S/'read_gladiator_render_contracts_v431.py',S/'read_gladiator_render_contracts_v432.py']
O.mkdir();result=dict(status='six_material_render_states_and_seventeen_generated_pass_states_sealed',updated_utc=datetime.now(timezone.utc).isoformat(),input_pins={str(f):sha(f) for f in inputs},materials=6,double_sided_materials=5,alpha_test_materials=4,generated_pass_states=17,backface_constants_to_preserve=[1,1,-1],mode_number_is_not_sufficient_for_backface_equation=True,alpha_to_coverage_must_be_resolved_per_pass=True,source_inputs_unchanged=True,material_render_parity_qualified=False,complete_asset_conversion_increment=0,full_import=False,next_work='Compose native vertex/surface/forward/prepass programs using the pinned constants, clipping terminals and exact pass state; verify rendering and assemble GLBs.')
(O/'RESULT.v1.json').write_text(json.dumps(result,indent=2),'utf-8')
note='''## Gladiator — états de rendu conservés, 10 octobre 2026

6 matériaux : 5 double face, 4 avec test alpha. 17 états de passes du shader Unity généré sont conservés, avec culling, profondeur, blending et ColorMask séparés par cible.

Point à préserver dans le port : les 5 matériaux double face déclarent un mode numérique 1, mais leurs constantes originales et natives concordent sur [1,1,-1]. L'équation doit utiliser ces constantes. AlphaToMask est déclaré dans certaines passes de profondeur et doit être résolu par passe ; la présence d'un ancien mot-clé ne suffit pas à qualifier la couverture.

Preuve : F:\\OpenRPG_ImportWork\\gladiator-render-state-delivery-v433\\RESULT.v1.json
Restent composition des stages, rendu comparé, GLB et validation dans l'éditeur. Aucun nouvel asset complet qualifié.

'''
(O/'LOT.md').write_text(note,'utf-8')
for name in ['AVANCEMENT_ACTUEL.md','POINT_DE_REPRISE.md']:
    path=W/'SUIVI_LOTS_HIVEMIND'/name;before=path.read_bytes();(O/(name+'.before')).write_bytes(before);path.write_bytes(note.encode('utf-8')+before)
print(result['status'])
