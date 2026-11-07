from pathlib import Path
from datetime import datetime,timezone
import hashlib,json
import numpy as np
W=Path('F:/OpenRPG_ImportWork');T=W/'SUIVI_LOTS_HIVEMIND'
def read(p):return json.loads(p.read_text(encoding='utf-8-sig'))
def sha(p):
 with p.open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
C=W/'townsmith-lighting-components-comparison-v47/RESULT.v1.json';j=read(C)
assert j['status']=='unfitted_lighting_components_compared_fidelity_open' and j['fixed_roi_pixels']==34445
B=W/'townsmith-lighting-components-batch-v47/BATCH.v1.json';assert read(B)['completed_channels']==[0,1,2,3] and 'finished_utc' in read(B)
a=(W/'townsmith-lighting-components-carrier-v42/source-directional-forward.wgsl').read_text(encoding='utf-8');b=(W/'townsmith-lighting-components-carrier-v47/source-directional-forward.wgsl').read_text(encoding='utf-8')
assert a.replace('ambient:vec3<f32>,reflection:vec3<f32>,mode:f32','private_ambient_layer:vec3<f32>,reflection:vec3<f32>,mode:f32').replace('return ambient;','return private_ambient_layer;')==b
old=W/'townsmith-native-source-uv1-lighting-v37/FORGE-LINEAR.rgba16f';new=W/'townsmith-native-lighting-layer-0-v47/FORGE-LINEAR.rgba16f'
d=np.abs(np.fromfile(old,dtype='<f2').astype(np.float32)-np.fromfile(new,dtype='<f2').astype(np.float32));assert int((d!=0).sum())==1 and float(d.max())==.0009765625
now=datetime.now(timezone.utc).isoformat();out=T/'TRANCHE_ECLAIRAGE_COMPOSANTES_V47.v1.json';assert not out.exists()
paths=[Path(__file__),C,B,old,new,W/'townsmith-lighting-diagnostic-v45/PENDING-PIPELINES-FINAL.v1.json',W/'townsmith-lighting-diagnostic-v46/PENDING-PIPELINES-FINAL.v1.json',W/'townsmith-lighting-components-carrier-v42/source-directional-forward.wgsl',W/'townsmith-lighting-components-carrier-v47/source-directional-forward.wgsl']
r=dict(status='lighting_components_technical_batch_delivered_fidelity_open',finished_utc=now,input_pins={str(p):sha(p) for p in paths},rows=j['rows'],rehashed_comparison_pins=j['rehashed_pins'],private_import_name_collision_control_resolved=True,source_material_changes=False,full_control_byte_equal=False,full_control_changed_channels=1,full_control_max_absolute_delta=float(d.max()),full_control_channels_over_001=int((d>.001).sum()),image_agent_review='Five-row contact sheet inspected; geometry and relative lighting match visibly at presentation scale. Unity full-frame background differs. No Studio or human visual approval.',full_import=False,forge_editor_verified=False,complete_assets_delivered_increment=0)
out.write_text(json.dumps(r,indent=2),encoding='utf-8')
msg=f"\n- {now} — **L03.TOWNSMITH.MATERIAUX.SCENE.ECLAIRAGE.COMPOSANTES livré techniquement** : conflit du paramètre diagnostique avec un import corrigé par renommage local uniquement. Quatre captures séparées, trois lectures GPU identiques chacune ; 1 387 épingles recontrôlées. Erreurs RGB moyennes : direct 0,004438 ; ambiance 0,000873 ; reflets 0,000307 ; rendu complet 0,005151. Un seul canal du contrôle complet diffère du V37, maximum 0,0009766 ; aucune différence au-delà de 0,001. La somme des diagnostics Unity ne reproduit pas exactement sa scène ; aucune fidélité complète déduite. Prochaine tranche : BRDF directe et formats Deferred, puis prefabs et Studio. [Preuve](TRANCHE_ECLAIRAGE_COMPOSANTES_V47.v1.json). Aucun nouvel asset 3D complet qualifié.\n"
for name in ['SUIVI_LISIBLE.md','PLAN_LOTS_A_SUIVRE.md','AVANCEMENT_ACTUEL.md','JOURNAL.md']:
 p=T/name;text=p.read_text(encoding='utf-8-sig')
 if name=='SUIVI_LISIBLE.md':
  text=text.replace('Diagnostic éclairage en cours ; V44 refusé après 90 s avec un pipeline encore en attente, sans erreur de validation GPU','Clos techniquement : conflit de nom local/import corrigé ; quatre captures stables comparées').replace('V45 identifie les états en attente ; conserver les refus et les seuils','Fidélité complète ouverte ; écart principal dans la lumière directe').replace('townsmith-lighting-diagnostic-v45/RUN-RESULT.v1.json','townsmith-lighting-components-comparison-v47/RESULT.v1.json').replace('**L03.TOWNSMITH.MATERIAUX.SCENE.ECLAIRAGE**','**L03.TOWNSMITH.MATERIAUX.SCENE.DIRECT.BRDF**')
  text=text.replace('Prochaines livraisons : contrôle UV Townsmith, correction/comparaison du matériau','Prochaines livraisons : comparaison BRDF directe Townsmith, fidélité du matériau')
 p.write_text(text+msg,encoding='utf-8')
for name in ['SUIVI_SIMPLE.v1.json','PLAN_LOTS_A_SUIVRE.v1.json']:
 p=T/name;k=read(p);k.update(updated_utc=now,active_sub_lot='L03.TOWNSMITH.MATERIAUX.SCENE.DIRECT.BRDF',current_work_sub_lot='L03.TOWNSMITH.MATERIAUX.SCENE.DIRECT.BRDF',state='comparaison des composantes livrée ; prochaine tranche BRDF directe',current_work_state='next_batch_pending',current_work_root=str(W/'townsmith-lighting-components-comparison-v47'),current_work_receipt=str(out),last_technical_batch='L03.TOWNSMITH.MATERIAUX.SCENE.ECLAIRAGE.COMPOSANTES',last_technical_proof=str(out),full_import=False,editor_visual_verified=False,next_action='Vérifier BRDF directe et formats Deferred au SDK Unity original ; puis fidélité du matériau, prefabs et Studio.')
 p.write_text(json.dumps(k,indent=2,ensure_ascii=False)+'\n',encoding='utf-8')
print(json.dumps({k:v for k,v in r.items() if k not in ['input_pins','rows']}))
