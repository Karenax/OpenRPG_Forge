"""Seal actual current host World checks while retaining LOD driver and visuals as pending."""
from pathlib import Path
from datetime import datetime,timezone
import hashlib,json
W=Path('F:/OpenRPG_ImportWork');I=W/'gladiator-current-host-state-v454';O=W/'gladiator-host-state-delivery-v455'
assert not O.exists()
def read(p):return json.loads(Path(p).read_text('utf-8-sig'))
def sha(p):
 with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
run=read(I/'RUN-RESULT.v1.json');assert run['status']=='five_current_studio_host_state_and_hierarchy_world_checks_passed'
assert run['cargo_exit_code']==0 and len(run['rows'])==5
assert sum(r['renderers'] for r in run['rows'])==17 and sum(r['primitives'] for r in run['rows'])==24
pins=dict(run['input_pins'])
for p,h in pins.items():assert sha(p)==h,p
for p in [Path(__file__),I/'RUN-RESULT.v1.json',I/'PREPARED.v1.json',I/'cargo.log',I/'current-host-checker.exe']:pins[str(p)]=sha(p)
assert sha(I/'current-host-checker.exe')==run['checker_sha256']
for row in run['rows']:
 assert row['actual_exit_code']==0 and row['root_hidden_pending_lod_driver'] and row['invalid_active_chain_refused']
 p=I/row['prefab_guid']/'stdout.json';assert sha(p)==row['stdout_sha256'];pins[str(p)]=sha(p)
 for key,value in read(p).items():assert row[key]==value
O.mkdir();result=dict(status='five_actual_current_studio_host_state_world_checks_sealed',observed_utc=datetime.now(timezone.utc).isoformat(),rows=run['rows'],input_pins=pins,renderers=17,primitives=24,lod_groups=5,actual_current_host_state_applied=True,automatic_lod_driver_qualified=False,collider_runtime_qualified=False,gpu_rendered=False,studio_gui_verified=False,full_import=False,complete_asset_conversion_increment=0)
(O/'RESULT.v1.json').write_text(json.dumps(result,indent=2),'utf-8')
note='''## Gladiator — états appliqués par le host Studio actuel, 10 octobre 2026

5 scènes acceptées et montées dans un World isolé par les modules courants inchangés de hiérarchie et d'états Studio. 17 renderers et 24 primitives : visibilité et marqueurs d'ombres appliqués, 5 groupes LOD conservés. Les racines restent cachées avec le marqueur explicite de driver LOD attendu. Chaînes d'activation altérées refusées. Les chunks binaires des GLB sont inchangés.

Preuve : F:\\OpenRPG_ImportWork\\gladiator-host-state-delivery-v455\\RESULT.v1.json
Restent driver LOD automatique, colliders runtime, dessins GPU comparés et parcours GUI Studio. Ce test World n'est pas une preuve d'affichage dans l'éditeur. Aucun nouvel asset complet qualifié.

''';(O/'LOT.md').write_text(note,'utf-8')
for name in ['AVANCEMENT_ACTUEL.md','POINT_DE_REPRISE.md']:
 p=W/'SUIVI_LOTS_HIVEMIND'/name;before=p.read_bytes();(O/(name+'.before')).write_bytes(before);p.write_bytes(note.encode('utf-8')+before)
print(json.dumps(dict(status=result['status'],scenes=5,renderers=17,primitives=24)))
