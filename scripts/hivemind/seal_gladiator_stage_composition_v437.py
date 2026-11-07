"""Reopen every source/Bevy composition without declaring GPU material parity."""
from pathlib import Path
from datetime import datetime,timezone
import hashlib,json
W=Path('F:/OpenRPG_ImportWork');S=Path(__file__).resolve().parent;O=W/'gladiator-stage-composition-delivery-v437'
def read(p):return json.loads(Path(p).read_text('utf-8-sig'))
def sha(p):
    with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
assert not O.exists()
vertexpath=W/'gladiator-bevy-vertex-stages-v435/RESULT.v1.json';surfacepath=W/'gladiator-native-surface-interfaces-v436/RESULT.v1.json';stagepath=W/'gladiator-native-stages-v434/RESULT.v1.json'
vertex=read(vertexpath);surface=read(surfacepath);stages=read(stagepath)
assert len(vertex['rows'])==len(surface['rows'])==len(stages['rows'])==6
assert not vertex['failures'] and not surface['failures'] and not stages['failures']
pins={str(p):sha(p) for p in [Path(__file__),vertexpath,surfacepath,stagepath,S/'build_gladiator_native_stages_v434.py',S/'build_gladiator_bevy_vertex_stages_v435.py',S/'build_gladiator_surface_interfaces_v436.py',S/'build_delta_bevy_vertex_stages_v2.py',S/'build_delta_native_surface_interfaces_v1.py']}
for proof in [vertex,surface,stages]:
    for p,h in proof['input_pins'].items():assert sha(p)==h,p
    pins.update(proof['input_pins'])
ids={r['material_guid'] for r in vertex['rows']};assert ids=={r['material_guid'] for r in surface['rows']}=={r['material_guid'] for r in stages['rows']}
for r in stages['rows']:
    assert r['actual_naga_exit_code']==0 and r['vertex_dependency_nodes']==[]
    for key in ['program','probe']:assert sha(r[key])==r[key+'_sha256'];pins[r[key]]=r[key+'_sha256']
for r in vertex['rows']:
    assert len(r['actual_stage_compositions'])==4 and all(c['actual_exit_code']==0 for c in r['actual_stage_compositions'])
    assert sha(r['shader'])==r['sha256'];pins[r['shader']]=r['sha256']
for r in surface['rows']:
    assert len(r['actual_compositions'])==4 and all(c['actual_exit_code']==0 for c in r['actual_compositions'])
    for key in ['native','qualifier']:assert sha(r[key])==r[key+'_sha256'];pins[r[key]]=r[key+'_sha256']
    for c in r['actual_compositions']:
        for lane in ['stdout','stderr']:
            p=Path(r['native']).parent/(c['permutation']+'.'+lane+'.log');assert sha(p)==c[lane+'_sha256'];pins[str(p)]=sha(p)
O.mkdir();result=dict(status='six_native_vertex_and_surface_programs_composed_and_independently_reopened',updated_utc=datetime.now(timezone.utc).isoformat(),input_pins=pins,materials=6,standalone_stage_validations=6,bevy_vertex_compositions=24,bevy_surface_compositions=24,native_surface_interfaces_reopened=6,rows=surface['rows'],source_vertex_displacement_inactive=True,source_equations_preserved=True,material_gpu_render_qualified=False,actual_product_mesh_draw_verified=False,complete_asset_conversion_increment=0,full_import=False,next_work='Compose full native Lit forward/prepass fragments, resolve pass-dependent coverage and depth state, then assemble and draw GLBs.')
(O/'RESULT.v1.json').write_text(json.dumps(result,indent=2),'utf-8')
note='''## Gladiator — stages Bevy composés, 10 octobre 2026

6 programmes source validés par Naga, 24 compositions de stages vertex et 24 compositions de surfaces avec les modules Bevy épinglés. Les buffers de sortie et journaux de composition ont été réouverts et hachés. Les 17 meshes possèdent les deux canaux UV attendus ; aucun déplacement de sommets actif dans ces graphes.

Preuve : F:\\OpenRPG_ImportWork\\gladiator-stage-composition-delivery-v437\\RESULT.v1.json
Ces contrôles de composition ne sont pas des dessins GPU des matériaux. Restent fragments Lit complets, couverture/profondeur par passe, GLB, rendu comparé et validation dans l'éditeur. Aucun nouvel asset complet qualifié.

'''
(O/'LOT.md').write_text(note,'utf-8')
for name in ['AVANCEMENT_ACTUEL.md','POINT_DE_REPRISE.md']:
    p=W/'SUIVI_LOTS_HIVEMIND'/name;b=p.read_bytes();(O/(name+'.before')).write_bytes(b);p.write_bytes(note.encode('utf-8')+b)
print(json.dumps(dict(status=result['status'],materials=6,bevy_compositions=48)))
