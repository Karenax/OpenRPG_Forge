"""Seal native camera replay and actual WebGPU compilation without claiming rendering."""
from pathlib import Path
from datetime import datetime,timezone
import hashlib,json
W=Path('F:/OpenRPG_ImportWork');R=Path('E:/OpenRPG_Forge/OpenRPG_Forge-Rust');O=W/'gladiator-current-lod-delivery-v464'
assert not O.exists()
def read(p):return json.loads(Path(p).read_text('utf-8-sig'))
def sha(p):
 with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
I=W/'gladiator-current-lod-v462';C=W/'gladiator-lod-studio-web-check-v465'
run=read(I/'RUN-RESULT.v1.json');check=read(C/'RUN-RESULT.v1.json')
assert run['status']=='207_current_studio_lod_camera_decisions_and_visibility_checks_passed'
assert check['status']=='actual_studio_webgpu_lod_check_passed' and check['cargo_exit_code']==0
assert run['cargo_exit_code']==0 and len(run['rows'])==5 and sum(r['cases'] for r in run['rows'])==207
pins={}
for evidence in [run,check]:
 for p,h in evidence['input_pins'].items():assert sha(p)==h,p;pins[p]=h
for p in [Path(__file__),I/'RUN-RESULT.v1.json',I/'PREPARED.v1.json',I/'current-host-checker.exe',I/'cargo.log',C/'RUN-RESULT.v1.json',C/'cargo.log',R/'scripts/hivemind/run_gladiator_current_lod_v460.py']:
 pins[str(p)]=sha(p)
assert sha(I/'current-host-checker.exe')==run['checker_sha256']
for row in run['rows']:
 assert row['actual_exit_code']==0 and row['invalid_camera_atomic_refusal'] and row['root_remains_hidden']
 p=I/row['prefab_guid']/'stdout.json';assert sha(p)==row['stdout_sha256'];pins[str(p)]=sha(p)
 for key,value in read(p).items():assert row[key]==value
O.mkdir()
result=dict(status='current_lod_207_native_camera_replays_and_studio_webgpu_check_sealed',observed_utc=datetime.now(timezone.utc).isoformat(),input_pins=pins,rows=run['rows'],camera_decisions=207,scene_count=5,current_studio_lod_driver_replayed=True,current_bevy_camera_reading_replayed=True,viewport_runtime_lod_hook_compiles=True,viewport_runtime_behavior_qualified=False,orthographic_lod_qualified=False,collider_runtime_qualified=False,gpu_rendered=False,studio_gui_verified=False,full_import=False,complete_asset_conversion_increment=0)
(O/'RESULT.v1.json').write_text(json.dumps(result,indent=2),'utf-8')
note='''## Gladiator — driver LOD courant et caméra Bevy vérifiés, 10 octobre 2026

207 décisions du driver Studio courant identiques aux références Unity, sur 5 prefabs et trois champs de vue. Visibilité des primitives vérifiée à chaque déplacement. Les composants Camera/Camera3d/Projection/GlobalTransform Bevy réels sont utilisés ; caméras inactives et projections orthographiques non qualifiées refusées. Une caméra invalide ne modifie pas partiellement les visibilités. Le raccordement au viewport actif après propagation des transforms est écrit et la compilation des sources réelles Studio WebGPU passe.

Preuve : F:\\OpenRPG_ImportWork\\gladiator-current-lod-delivery-v464\\RESULT.v1.json
La compilation du raccordement n'est pas une preuve de fonctionnement dans le viewport. Les racines restent cachées jusqu'à admission du rendu. Restent validation comportementale du raccordement, colliders physiques, rendu comparé et parcours dans Studio. Aucun nouvel asset complet qualifié ; aucun changement du Vault.

'''
(O/'LOT.md').write_text(note,'utf-8')
for name in ['AVANCEMENT_ACTUEL.md','POINT_DE_REPRISE.md']:
 p=W/'SUIVI_LOTS_HIVEMIND'/name;before=p.read_bytes();(O/(name+'.before')).write_bytes(before);p.write_bytes(note.encode('utf-8')+before)
print(json.dumps(dict(status=result['status'],cases=207,scenes=5,complete_asset_conversion_increment=0)))
