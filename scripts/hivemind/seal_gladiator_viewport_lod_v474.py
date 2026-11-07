"""Seal scheduled common host LOD behavior; preserve zero-material/GPU scope."""
from pathlib import Path
from datetime import datetime,timezone
import hashlib,json
W=Path('F:/OpenRPG_ImportWork');I=W/'gladiator-viewport-lod-cycle-v469';O=W/'gladiator-viewport-lod-delivery-v474'
assert not O.exists()
def read(p):return json.loads(Path(p).read_text('utf-8-sig'))
def sha(p):
 with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
run=read(I/'RUN-RESULT.v1.json');assert run['status']=='207_actual_viewport_lod_cycles_and_camera_refusal_recovery_checks_passed' and run['cargo_exit_code']==0
assert len(run['rows'])==5 and sum(r['cases'] for r in run['rows'])==207 and sum(r['primitives'] for r in run['rows'])==24
pins=dict(run['input_pins'])
for p,h in pins.items():assert sha(p)==h,p
assert sha(I/'viewport-cycle-checker.exe')==run['checker_sha256']
for p in [Path(__file__),I/'RUN-RESULT.v1.json',I/'PREPARED.v1.json',I/'cargo.log',I/'viewport-cycle-checker.exe']:pins[str(p)]=sha(p)
for row in run['rows']:
 assert row['actual_exit_code']==0 and row['material_binding_count']==0 and not row['gpu_rendered'] and not row['full_import']
 for key in ['inactive_camera_refused_without_lod_mutation','ambiguous_camera_refused','offscreen_camera_excluded','automatic_recovery_verified','bevy_transform_propagation_used','actual_product_host_runtime_modules_used','despawn_update_no_panic']:assert row[key]
 p=I/row['prefab_guid']/'stdout.json';assert sha(p)==row['stdout_sha256'];pins[str(p)]=sha(p)
 for key,value in read(p).items():assert row[key]==value
O.mkdir();result=dict(status='207_actual_scheduled_host_lod_cycles_sealed',observed_utc=datetime.now(timezone.utc).isoformat(),input_pins=pins,rows=run['rows'],scene_count=5,camera_cases=207,primitives=24,common_viewport_camera_lod_lifecycle_qualified_in_native_world=True,material_binding_count=0,source_material_frame_updates_qualified=False,source_pass_render_qualified=False,rapier_runtime_qualified=False,studio_gui_verified=False,full_import=False,complete_asset_conversion_increment=0)
(O/'RESULT.v1.json').write_text(json.dumps(result,indent=2),'utf-8')
note='''## Gladiator — cycle LOD du host courant vérifié, 10 octobre 2026

207 positions natives sur 5 prefabs passent par App.update, les modules courants du host/runtime et la propagation des transforms Bevy. Visibilité des 24 primitives conforme aux décisions Unity. Caméra inactive : refus sans changement partiel des visibilités ; deux caméras actives : refus ; capture hors écran : exclue ; situation corrigée : reprise automatique. Suppression de la racine suivie d'une update sans panique. Le banc conserve zéro binding matériau et des racines cachées : il qualifie le cycle LOD commun dans un World natif, pas les shaders, les passes GPU ou une fenêtre Studio.

Preuve : F:\\OpenRPG_ImportWork\\gladiator-viewport-lod-delivery-v474\\RESULT.v1.json
Le v466 refusé reste conservé ; le v469 corrigé est terminé et ne doit pas être relancé. Les références physiques natives sont scellées en v473 (63 points, 18 rayons). Prochaines étapes : raccordement et comparaison Rapier, puis admission du rendu et parcours dans Studio. Aucun nouvel asset complet qualifié ; aucun changement du Vault.

'''
(O/'LOT.md').write_text(note,'utf-8')
for name in ['AVANCEMENT_ACTUEL.md','POINT_DE_REPRISE.md']:
 p=W/'SUIVI_LOTS_HIVEMIND'/name;before=p.read_bytes();(O/(name+'.before')).write_bytes(before);p.write_bytes(note.encode('utf-8')+before)
print(json.dumps(dict(status=result['status'],scenes=5,cases=207,complete_asset_conversion_increment=0)))
