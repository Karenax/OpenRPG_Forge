"""Reopen native image admission and preserve the two failed source epochs."""
from pathlib import Path
from datetime import datetime, timezone
import hashlib, json, subprocess, sys

S=Path(__file__).parent
W=Path('F:/OpenRPG_ImportWork')
T=W/'SUIVI_LOTS_HIVEMIND'
ROOT=W/'townsmith-paint-door-native-resource-admission-v3'
SEAL=W/'townsmith-paint-door-native-resource-admission-reopen-v3'
def read(p):return json.loads(p.read_text(encoding='utf-8-sig'))
def sha(p):
    with p.open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()

run=read(ROOT/'RUN-RESULT.v1.json')
assert run['actual_exit_code']==0 and run['textures_gpu_resident']==10 and run['mips_gpu_resident']==120
native=read(ROOT/'NATIVE-RESULT.v1.json')
assert run['native_receipt_sha256']==sha(ROOT/'NATIVE-RESULT.v1.json')
assert native['actual_resource_bind_groups_created'] and not native['authored_texture_substitution']
assert not native['gpu_validation_errors'] and not native['gpu_draw_executed']
assert len(native['textures'])==10 and sum(t['mips'] for t in native['textures'])==120
assert sum(t['native_bytes'] for t in native['textures'])==78293832
assert all(t['byte_equal'] and t['gpu_resident'] and t['actual_binding_identity_verified'] for t in native['textures'])
assert len(native['meshes'])==3 and sum(m['vertices'] for m in native['meshes'])==6981
assert (ROOT/'SOURCE-UNIFORMS.bin').stat().st_size==336
pins=dict(read(ROOT/'PREPARED.v1.json')['input_pins'])
for p,h in pins.items():assert sha(Path(p))==h,p
epochs=[]
example='E:\\OpenRPG_Forge\\OpenRPG_Forge-Rust\\crates\\forge_engine\\examples\\verify_source_glb_resources.rs'
for version in [1,2]:
    old=W/f'townsmith-paint-door-native-resource-admission-v{version}'
    snapshot=old/'before-repair-example.rs'
    prior=read(old/'PREPARED.v1.json')['input_pins'][example]
    assert sha(snapshot)==prior
    epochs.append(dict(version=version,original_path=example,historical_sha256=prior,snapshot=str(snapshot),
                       refusal=read(old/'RUN-RESULT.v1.json')['status']))
    pins[str(snapshot)]=prior
    pins[str(old/'RUN-RESULT.v1.json')]=sha(old/'RUN-RESULT.v1.json')
for p in [Path(__file__),ROOT/'PREPARED.v1.json',ROOT/'RUN-RESULT.v1.json',ROOT/'NATIVE-RESULT.v1.json',ROOT/'SOURCE-UNIFORMS.bin']:
    pins[str(p)]=sha(p)
assert not SEAL.exists();SEAL.mkdir()
result=dict(status='ten_original_glb_images_and_actual_bevy_material_allocation_reopened',
    finished_utc=datetime.now(timezone.utc).isoformat(),lot='L03.TOWNSMITH.RESSOURCES',
    textures_verified=10,mips_verified=120,native_bytes_verified=78293832,
    lod_meshes_imported=3,vertices_imported=6981,source_uniform_bytes=336,
    actual_resource_bind_group_created=True,authored_texture_substitution=False,
    source_epochs=epochs,input_pins=pins,source_scene_resources_qualified=False,
    gpu_draw_executed=False,source_sampling_parity_qualified=False,source_render_parity_qualified=False,
    forge_editor_verified=False,full_import=False,complete_assets_delivered_increment=0)
(SEAL/'RESULT.v1.json').write_text(json.dumps(result,indent=2),encoding='utf-8')
subprocess.run([sys.executable,'-B',str(S/'record_batch_progress_v1.py')],check=True,stdout=subprocess.DEVNULL)
subprocess.run([sys.executable,'-B',str(S/'seal_townsmith_material_packet_v1.py'),'refresh'],check=True)
current=T/'AVANCEMENT_ACTUEL.md'
with current.open('a',encoding='utf-8') as f:
    f.write('\n## Lot ressources Townsmith vérifié\n\nDix images natives du GLB, 120 mips et 78 293 832 octets source admis ; trois meshes LOD et 6 981 sommets importés par le moteur. Identité des vingt bindings texture/sampler vérifiée. Groupe matériau créé par le véritable allocateur Bevy ; uniformes source de 336 octets. Aucun dessin ni asset complet qualifié.\n\nProchain lot : **L03.TOWNSMITH.DESSIN** — états source, dessin réel, sampling et comparaison Unity ; Studio reste ouvert.\n\n[Preuve ressources](../townsmith-paint-door-native-resource-admission-reopen-v3/RESULT.v1.json).\n')
plan=T/'PLAN_LOTS_A_SUIVRE.md'
text=plan.read_text(encoding='utf-8').replace('**L03.TOWNSMITH.RESSOURCES** : charger','**L03.TOWNSMITH.RESSOURCES — terminé techniquement** : charger')
text += '\nL03.TOWNSMITH.RESSOURCES vérifié : dix textures natives / 120 mips / groupe matériau Bevy réel ; prochains contrôles dans L03.TOWNSMITH.DESSIN. [Preuve](../townsmith-paint-door-native-resource-admission-reopen-v3/RESULT.v1.json).\n'
plan.write_text(text,encoding='utf-8')
with (T/'JOURNAL.md').open('a',encoding='utf-8') as f:
    f.write('\n- L03.TOWNSMITH.RESSOURCES techniquement livré : dix images originales, 120 mips, 78 293 832 octets exacts, 20 bindings image/sampler vérifiés, allocateur matériau Bevy réel. V1 compilation et V2 OwnedData refusés, snapshots conservés. Dessin, fidélité et Studio ouverts ; zéro nouvel asset complet.\n')
print(json.dumps({k:v for k,v in result.items() if k not in ['input_pins','source_epochs']}))
