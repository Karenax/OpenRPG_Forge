"""Record source/material controls and the separate unresolved lighting refusal."""
from pathlib import Path
from datetime import datetime, timezone
import hashlib
import json

W=Path('F:/OpenRPG_ImportWork');T=W/'SUIVI_LOTS_HIVEMIND'
ao=W/'townsmith-source-ao-comparison-v40/RESULT.v1.json'
values=W/'townsmith-material-values-comparison-v41/RESULT.v1.json'
def read(p):return json.loads(p.read_text(encoding='utf-8-sig'))
def sha(p):
 with p.open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
a,b=read(ao),read(values)
assert a['source_depth_and_viewport_normals_unchanged'] and a['original_no_post_reference_byte_equal']
assert b['status']=='generated_source_and_native_graph_intermediates_compared'
assert [r['name'] for r in b['rows']]==['base_color','metal_smooth_ao','normal_ts']
for proof in [a,b]:
 for p,h in proof['input_pins'].items():assert sha(Path(p))==h,p
now=datetime.now(timezone.utc).isoformat()
refusal=W/'townsmith-lighting-components-batch-v42/BATCH.v1.json'
assert read(refusal)['status']=='native_uv_batch_refusal_retained'
out=T/'TRANCHE_MATERIAU_SCENE_V41.v1.json';assert not out.exists()
result=dict(status='source_scene_ao_and_raw_material_values_compared_lighting_open',finished_utc=now,input_pins={str(p):sha(p) for p in [Path(__file__),ao,values,refusal]},covered_pixels=34445,source_scene_ao_mean_rgb_effect=a['rows'][2]['mean_rgb_error'],source_no_ao_is_diagnostic_only=True,base_color_mean_rgb_errors=b['rows'][0]['rgba_mean_absolute_error'][:3],native_lighting_component_draw_refusal_retained=True,full_import=False,forge_editor_verified=False,complete_assets_delivered_increment=0)
out.write_text(json.dumps(result,indent=2),encoding='utf-8')
message=f'\n- {now} — **L03.TOWNSMITH.MATERIAUX.SCENE : contrôles AO et valeurs brutes vérifiés**. Occlusion source moyenne 0,000188 RGB ; sa désactivation diagnostique ne résout pas l’écart (0,005151 → 0,004978). Couleur de base du graphe : écarts moyens par canal inférieurs à 0,00005 sur 34 445 pixels. Paramètres métalliques/lissage/occlusion et normales tangentiels comparés ; références et sources intactes. Le contrôle de composantes éclairées V42 a échoué ; diagnostic GPU V44 en cours, avec attente des imports transitoires. [Livraison](TRANCHE_MATERIAU_SCENE_V41.v1.json). Fidélité du matériau, prefabs et Studio restent ouverts.\n'
for name in ['SUIVI_LISIBLE.md','PLAN_LOTS_A_SUIVRE.md','AVANCEMENT_ACTUEL.md','JOURNAL.md']:
 with (T/name).open('a',encoding='utf-8') as f:f.write(message)
for name in ['SUIVI_SIMPLE.v1.json','PLAN_LOTS_A_SUIVRE.v1.json']:
 p=T/name;j=read(p);j.update(updated_utc=now,active_sub_lot='L03.TOWNSMITH.MATERIAUX.SCENE.ECLAIRAGE',current_work_sub_lot='L03.TOWNSMITH.MATERIAUX.SCENE.ECLAIRAGE',state=result['status'],current_work_root='F:/OpenRPG_ImportWork/townsmith-lighting-diagnostic-v44',current_work_receipt=str(out),last_technical_proof=str(out),next_action='Resolve cached lighting pipeline refusal, compare direct/ambient/reflection terms; keep original AO-enabled source as target.',full_import=False);p.write_text(json.dumps(j,indent=2,ensure_ascii=False)+'\n',encoding='utf-8')
print(json.dumps({k:v for k,v in result.items() if k!='input_pins'}))
