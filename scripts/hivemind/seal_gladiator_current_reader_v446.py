"""Retain current product admission evidence without claiming runtime visuals."""
from pathlib import Path
from datetime import datetime, timezone
import hashlib
import json

W=Path('F:/OpenRPG_ImportWork')
I=W/'gladiator-current-product-reader-v445'
O=W/'gladiator-current-reader-delivery-v446'
assert not O.exists()
def read(p): return json.loads(Path(p).read_text('utf-8-sig'))
def sha(p):
    with Path(p).open('rb') as f: return hashlib.file_digest(f,'sha256').hexdigest()
run=read(I/'RUN-RESULT.v1.json')
assert run['status']=='all_five_gladiator_glbs_admitted_by_current_product_readers'
assert run['cargo_exit_code']==0 and run['source_inputs_unchanged'] and len(run['rows'])==5
assert sum(r['primitives'] for r in run['rows'])==24
assert sum(r['triangles'] for r in run['rows'])==10000
pins=dict(run['input_pins'])
for p,h in pins.items(): assert sha(p)==h,p
for p in [Path(__file__),I/'RUN-RESULT.v1.json',I/'PREPARED.v1.json',I/'cargo.log',I/'current-product-reader.exe']:
    pins[str(p)]=sha(p)
assert sha(I/'current-product-reader.exe')==run['checker_sha256']
for row in run['rows']:
    assert row['actual_exit_code']==0 and row['original_uv1_admitted']
    assert row['shader_hash_negative_control'] and row['header_negative_control']
    assert sha(row['glb'])==row['sha256']
    case=I/row['prefab_guid']
    for lane,field in [('stdout.json','stdout_sha256'),('stderr.log','stderr_sha256')]:
        p=case/lane
        assert sha(p)==row[field]
        pins[str(p)]=row[field]
    observed=read(case/'stdout.json')
    for key,value in observed.items(): assert row[key]==value
O.mkdir()
result=dict(status='five_gladiator_glbs_current_rust_transport_and_geometry_admission_sealed',
    observed_utc=datetime.now(timezone.utc).isoformat(),input_pins=pins,rows=run['rows'],
    actual_current_product_reader_passed=True,source_inputs_unchanged=True,
    canonical_glbs=5,primitive_count=24,triangles=10000,
    runtime_renderer_lod_collider_admission_qualified=False,
    material_gpu_render_qualified=False,studio_verified=False,full_import=False,
    complete_asset_conversion_increment=0,
    next_work='Measure original renderer/LOD/collider state, bind it to the native scene contract, then execute actual product mesh draws and compare source rendering.')
(O/'RESULT.v1.json').write_text(json.dumps(result,indent=2),'utf-8')
note='''## Gladiator — admission Rust actuelle validée, 10 octobre 2026

Les 5 GLB autonomes passent les vrais lecteurs courants forge_assets de matériaux et de géométrie : 24 primitives, 10000 triangles, normales/tangentes et UV0/UV1 originaux acceptés. Compilation Cargo offline/locked et 5 exécutions réussies. En-têtes invalides et programmes source altérés refusés. Sources, GLB, exécutable et journaux rehashés ; preuves réouvertes.

Preuve : F:\\OpenRPG_ImportWork\\gladiator-current-reader-delivery-v446\\RESULT.v1.json
Restent admission runtime des états renderer/LOD/colliders, dessins GPU comparés, indexation de dérivés qualifiés et validation dans Studio. L'admission des données ne qualifie pas leur rendu. Aucun nouvel asset complet qualifié.

'''
(O/'LOT.md').write_text(note,'utf-8')
for name in ['AVANCEMENT_ACTUEL.md','POINT_DE_REPRISE.md']:
    p=W/'SUIVI_LOTS_HIVEMIND'/name
    before=p.read_bytes();(O/(name+'.before')).write_bytes(before)
    p.write_bytes(note.encode('utf-8')+before)
print(json.dumps(dict(status=result['status'],glbs=5,primitives=24,triangles=10000)))
