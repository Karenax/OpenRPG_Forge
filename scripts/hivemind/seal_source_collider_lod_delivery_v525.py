"""Seal simultaneous collider lifecycle and LOD replay on current product sources."""
from pathlib import Path
from datetime import datetime, timezone
import hashlib, json
W=Path('F:/OpenRPG_ImportWork');O=W/'source-collider-lod-delivery-v525'
def sha(p):
    with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
assert not O.exists()
np=W/'source-collider-lod-cycle-v504/RUN-RESULT.v1.json'
wp=W/'source-ray-and-projection-studio-web-check-v505/RUN-RESULT.v1.json'
n,w=[json.loads(p.read_text('utf-8-sig')) for p in [np,wp]]
assert n['status']=='207_lod_cycles_and_828_physics_phases_keep_source_colliders_live'
assert n['cargo_exit_code']==0 and len(n['rows'])==5
assert sum(r['cases'] for r in n['rows'])==207
assert sum(r['physical_phases'] for r in n['rows'])==828
assert sum(r['physical_rays'] for r in n['rows'])==3096
assert sum(r['source_boxes'] for r in n['rows'])==3
assert w['status']=='actual_studio_webgpu_lod_check_passed' and w['cargo_exit_code']==0 and w['inputs_unchanged']
pins={}
for evidence in [n,w]:
    for p,h in evidence['input_pins'].items():
        if p in pins:assert pins[p]==h,p
        pins[p]=h
for p,h in pins.items():assert sha(p)==h,p
exe=np.parent/'viewport-cycle-checker.exe';assert sha(exe)==n['checker_sha256']
for r in n['rows']:
    assert r['actual_exit_code']==0 and r['actual_host_collision_install_remove_verified'] and r['colliders_during_lod_verified']
    assert r['physical_phases']==r['cases']*4
    assert r['physical_rays']==r['physical_phases']*6*r['source_boxes']
    assert r['material_binding_count']==0 and not r['gpu_rendered'] and not r['studio_gui_verified'] and not r['full_import']
    p=np.parent/r['prefab_guid']/'stdout.json';assert sha(p)==r['stdout_sha256'];pins[str(p)]=r['stdout_sha256']
for p in [Path(__file__),np,wp,exe,np.parent/'cargo.log']:pins[str(p)]=sha(p)
O.mkdir()
result=dict(status='207_lod_cycles_828_simultaneous_physics_phases_and_3096_rays_verified',source_scenes=5,source_boxes=3,scheduled_lod_cases=207,physics_phases=828,physics_rays=3096,collider_entities_and_handles_stable=True,root_despawn_removes_owned_colliders=True,studio_webgpu_compile=True,input_pins=pins,rows=n['rows'],scope='Actual current host and scheduled LOD with explicit benchmark contact policy; query and lifecycle qualification only.',automatic_placement_contact_admission_qualified=False,material_contact_simulation_qualified=False,layer_interaction_mapping_qualified=False,gpu_render_qualified=False,studio_gui_verified=False,full_import=False,complete_asset_conversion_increment=0,vault_mutated=False,finished_utc=datetime.now(timezone.utc).isoformat())
(O/'RESULT.v1.json').write_text(json.dumps(result,indent=2),'utf-8')
note='## Colliders simultanes pendant LOD - v504/v505/v525\n\nCinq scenes, trois BoxCollider sources : 207 cycles LOD, 828 phases physiques et 3096 rayons verifies dans le host courant. Identites et handles des colliders stables pendant changement LOD, camera inactive, cameras ambigues et recuperation. Nettoyage a la suppression de racine verifie. Compilation Studio WebGPU reussie sur les memes sources. Banc terminal : ne pas relancer v504. Admission automatique au placement, equivalence des contacts/materiaux/couches, rendu source et GUI restent ouverts. Increment entierement converti : 0. Preuve : F:/OpenRPG_ImportWork/source-collider-lod-delivery-v525/RESULT.v1.json.\n\n'
for name in ['AVANCEMENT_ACTUEL.md','POINT_DE_REPRISE.md']:
    p=W/'SUIVI_LOTS_HIVEMIND'/name;raw=p.read_bytes();(O/(name+'.before')).write_bytes(raw);p.write_text(note+raw.decode('utf-8-sig'),'utf-8')
print(json.dumps({k:v for k,v in result.items() if k not in ['input_pins','rows']}))
