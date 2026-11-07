from pathlib import Path
from datetime import datetime,timezone
import hashlib,json
import numpy as np
W=Path('F:/OpenRPG_ImportWork');D=W/'townsmith-native-lighting-layer-0-v51';O=W/'townsmith-gpu-srgb-storage-comparison-v51';assert not O.exists()
def read(p):return json.loads(p.read_text(encoding='utf-8-sig'))
def sha(p):
 with p.open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
run=read(D/'RUN-RESULT.v1.json');assert run['actual_exit_code']==0 and run['gpu_draw_executed']
native=read(D/'NATIVE-RESULT.v1.json');assert native['target_format']=='Rgba8UnormSrgb' and native['consecutive_equal_readbacks']>=3
assert sha(D/'NATIVE-RESULT.v1.json')==run['native_receipt_sha256'];assert sha(D/'FORGE-LINEAR.rgba16f')==run['forge_image_sha256']
source_receipt=W/'townsmith-source-deferred-inputs-reopen-v49/RESULT.v1.json';source=read(source_receipt);assert source['status']=='actual_inpass_deferred_inputs_reopened'
pins=dict(read(D/'PREPARED.v1.json')['input_pins']);pins.update(source['input_pins'])
for p,h in pins.items():assert sha(Path(p))==h,p
src=W/'townsmith-door-source-deferred-inputs-v49/door-nopost-linear.actual-gbuffer0-3.rgba32f'
depth=W/'townsmith-door-source-deferred-inputs-v49/door-nopost-linear.depth-3.r32f';decoded=D/'GPU-SRGB-DECODED.rgba32f'
a=np.fromfile(src,dtype='<f4').reshape(512,512,4)[::-1];b=np.fromfile(decoded,dtype='<f4').reshape(512,512,4);mask=np.fromfile(depth,dtype='<f4').reshape(512,512)[::-1]>0
assert np.isfinite(a).all() and np.isfinite(b).all();assert int(mask.sum())==34445;assert np.array_equal(b[:,:,0]>0,mask)
encoded=np.fromfile(D/'FORGE-LINEAR.rgba16f',dtype=np.uint8).reshape(512,512,4);assert np.array_equal(encoded[:,:,3]==255,mask)
error=np.abs(a[:,:,:3]-b[:,:,:3])[mask]
for p in [Path(__file__),source_receipt,src,depth,decoded,D/'FORGE-LINEAR.rgba16f',D/'RUN-RESULT.v1.json',D/'NATIVE-RESULT.v1.json',D/'PREPARED.v1.json',W/'townsmith-lighting-components-batch-v51/BATCH.v1.json',W/'townsmith-lighting-components-batch-v50/BATCH.v1.json']:
 pins[str(p)]=sha(p)
now=datetime.now(timezone.utc).isoformat();result=dict(status='actual_gpu_srgb_source_diffuse_storage_compared',finished_utc=now,input_pins=pins,rehashed_pins=len(pins),covered_pixels=34445,exact_rgb_pixels=int((error.max(axis=1)==0).sum()),mean_rgb_error=float(error.mean()),max_rgb_error=float(error.max()),pixels_over_001=int((error.max(axis=1)>.001).sum()),native_srgb_encoded_draw_equal_readbacks=3,native_srgb_texture_load_equal_readbacks=3,source_beforepostprocess_gbuffer=True,source_original_reference_unchanged=True,source_pixels_used_as_native_input=False,native_input_is_actual_source_graph_material=True,native_target_format='Rgba8UnormSrgb',native_gpu_decoding_format='rgba32float_storage',raw_template_receipt_lighting_flags_are_not_used_as_lighting_proof=True,measured_sky_reflection_applied=False,measured_gpu_ambient_applied=False,fixture_scope='Actual common GLB imported material, diffuse color rendered to sRGB RT without Hdr component; actual resident textureLoad GPU decode. Lighting is bypassed in this private diagnostic.',full_material_fidelity_qualified=False,forge_editor_verified=False,full_import=False,complete_assets_delivered_increment=0,next_action='Apply source Deferred storage semantics to common lighting path and compare complete lighting; preserve original Unity scene as target.')
assert result['exact_rgb_pixels']==33619
O.mkdir();(O/'RESULT.v1.json').write_text(json.dumps(result,indent=2),encoding='utf-8')
print(json.dumps({k:v for k,v in result.items() if k!='input_pins'}))
T=W/'SUIVI_LOTS_HIVEMIND'
msg=f"\n- {now} — **L03.TOWNSMITH.MATERIAUX.DEFERRED.STOCKAGE : contrôle GPU clos techniquement**. Diffuse/spéculaire séparés (V48), 36 captures source soleil/GBuffer réouvertes (V49). Soleil : 15 000 lux, coefficients à 1. Stockage sRGB réel du matériau natif et décodage GPU (V51) : 33 619 / 34 445 pixels RGB identiques ; erreur moyenne 0,000014. Trois lectures stables ; refus V50 conservé. Référence source inchangée ; aucune couleur source utilisée comme entrée native. Prochaine tranche : application des règles Deferred au chemin commun, puis recomparaison du matériau éclairé. [Preuve stockage](../townsmith-gpu-srgb-storage-comparison-v51/RESULT.v1.json) · [Source](../townsmith-source-deferred-inputs-reopen-v49/RESULT.v1.json) · [Composantes directes](../townsmith-direct-components-comparison-v48/RESULT.v1.json). Aucun asset 3D complet supplémentaire qualifié ; prefabs, Studio et runtime restent ouverts.\n"
for name in ['SUIVI_LISIBLE.md','PLAN_LOTS_A_SUIVRE.md','AVANCEMENT_ACTUEL.md','JOURNAL.md']:
 p=T/name;text=p.read_text(encoding='utf-8-sig')
 if name=='SUIVI_LISIBLE.md':
  text=text.replace('**L03.TOWNSMITH.MATERIAUX.SCENE.DIRECT.BRDF**','**L03.TOWNSMITH.MATERIAUX.DEFERRED.APPLICATION**')
  marker='| L03 · Diagnostic éclairage |';pos=text.index(marker);end=text.index('\n',pos)
  text=text[:end]+"\n| L03 · Stockage Deferred | Contrôle sRGB GPU clos ; 33 619 / 34 445 pixels RGB identiques à Unity | Appliquer les règles au rendu commun et comparer le matériau éclairé |"+text[end:]
 p.write_text(text+msg,encoding='utf-8')
for name in ['SUIVI_SIMPLE.v1.json','PLAN_LOTS_A_SUIVRE.v1.json']:
 p=T/name;j=read(p);j.update(updated_utc=now,state=result['status'],active_sub_lot='L03.TOWNSMITH.MATERIAUX.DEFERRED.APPLICATION',current_work_sub_lot='L03.TOWNSMITH.MATERIAUX.DEFERRED.APPLICATION',current_work_state='next_batch_pending',current_work_root=str(O),current_work_receipt=str(O/'RESULT.v1.json'),last_technical_batch='L03.TOWNSMITH.MATERIAUX.DEFERRED.STOCKAGE',last_technical_proof=str(O/'RESULT.v1.json'),next_action=result['next_action'],full_import=False,editor_visual_verified=False)
 p.write_text(json.dumps(j,indent=2,ensure_ascii=False)+'\n',encoding='utf-8')
