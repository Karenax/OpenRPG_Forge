"""Current short user-facing lot view, retaining all historical receipts."""
from pathlib import Path
from datetime import datetime,timezone
import json,hashlib
W=Path('F:/OpenRPG_ImportWork');T=W/'SUIVI_LOTS_HIVEMIND'
def read(p):return json.loads(p.read_text('utf-8-sig')) if p.exists() else {}
def write(p,value):
 t=p.with_suffix(p.suffix+'.tmp');t.write_text(value,'utf-8');t.replace(p)
def main():
 cap='studio-source-host-shadows-v2' if (W/'studio-source-host-shadows-v2').exists() else 'studio-source-host-shadows-v1'
 plan=read(T/'PLAN_PAR_LOTS.v4.json');run=read(W/cap/'RUN-RESULT.v1.json');native=read(W/cap/'NATIVE-RESULT.v1.json')
 passed=run.get('actual_exit_code')==0 and all(native.get(k) for k in ['same_frame_shadow_time_verified','camera_conflict_no_allocation','partial_install_rollback_verified','repeated_install_preserves_ids','refused_and_missing_routes_no_partial_frame']) and native.get('host_shadow_routes')==4
 failed=run.get('actual_exit_code') not in [None,0] or 'error' in run
 state='étape native vérifiée' if passed else 'refus à résoudre' if failed else 'contrôle natif en cours'
 close=read(W/'studio-source-host-shadow-install-v1/RESULT.v1.json')
 web_version=next((v for v in [25,24,23,22,21,20,19,18,17,16,15] if (W/f'studio-current-source-wasm-check-v{v}').exists()),15)
 web=read(W/f'studio-current-source-wasm-check-v{web_version}/RUN-RESULT.v1.json')
 if close.get('web_exit_code')==0 and passed:state='étape native et Web vérifiée'
 shadow_version=next((v for v in [8,7,6,5,4,3,2,1] if (W/f'firepit-shadow-source-reference-v{v}').exists()),8)
 shadow_source=read(W/f'firepit-shadow-source-reference-v{shadow_version}/RUN-RESULT.v1.json')
 shadow_measurement=read(W/f'firepit-shadow-source-measurement-v{shadow_version}/RESULT.v1.json')
 source_case=read(W/'firepit-shadow-comparison-case-v1/RESULT.v1.json')
 active_lot='L01.OMBRES.02.SOURCE' if shadow_source else 'L01.OMBRES.01'
 active_state=shadow_measurement.get('status',shadow_source.get('status',state))
 if source_case.get('source_shadow_response_verified'):
  active_lot='L01.OMBRES.02.FORGE'
  active_state='cas apparié préparé ; capture Forge à faire'
 forge_version=next((v for v in [4,3,2,1] if (W/f'firepit-forge-shadow-pair-v{v}').exists()),4)
 forge_run=read(W/f'firepit-forge-shadow-pair-v{forge_version}/RUN-RESULT.v1.json')
 forge_pair=read(W/f'firepit-forge-shadow-pair-v{forge_version}/GPU-PAIR.v1.json')
 comparison=read(W/'firepit-forge-shadow-pair-comparison-v2/RESULT.v1.json')
 if forge_run:active_state=comparison.get('status',forge_pair.get('status',forge_run['status']))
 now=datetime.now(timezone.utc).isoformat()
 rows=[('L01.OMBRES.01','Raccordement commun des ombres',state,'4 routes ; 6 contrôles attendus',cap+'/RUN-RESULT.v1.json'),
 ('L01.OMBRES.02.SOURCE','Référence Unity statique avec/sans ombre',shadow_measurement.get('status',shadow_source.get('status','à faire')),'2 images et 2 buffers de pixels attendus ; 24 dépendances source',f'firepit-shadow-source-reference-v{shadow_version}/RUN-RESULT.v1.json' if shadow_source else ''),
 ('L01.OMBRES.02.FORGE','Comparaison du rendu et de l’ombre Forge',active_state,'2 images HDR GPU, contrôle ombre désactivée et refus atomiques',f'firepit-forge-shadow-pair-v{forge_version}/RUN-RESULT.v1.json' if forge_run else ''),
 ('L01.LOD.01','Sélection réelle des niveaux de détail','à faire','résoudre les 27 écarts de seuils observés',''),
 ('L03.HOUSE.ASSEMBLAGE','Prefabs House complets','à faire','48 géométries réutilisables ; assemblage et matériaux',''),
 ('L03.TOWNSMITH.BINDINGS','Affectations de matériaux Townsmith','refus à résoudre','6 bindings source',''),
 ('L03.VILLA.SCENES','Scènes Villa restantes','à faire','19 scènes',''),
 ('L06.FIREPIT.SIMULATION','Effets FirePit dans le temps','à faire','2 effets ; référence de 112 observations',''),
 ('L06.BLOOD.GRAPHES','Effets Blood par groupes d’IDs','à faire','85 prefabs / 139 graphes à rapprocher',''),
 ('L06.DUST.SIMULATION','Poussière et trajectoires','à faire','comparaison aux références temporelles',''),
 ('L08.IMAGES','Images manquantes','partiel','942 fiches sans image ; 3 falaises refusées','house-source-thumbnail-batch-0001-0008-delta-reopen-v1/RESULT.v1.json'),
 ('L08.RECETTE','Recherche et usage dans l’éditeur','à faire','filtres, détails, aperçu, édition, sauvegarde, réouverture','')]
 text=f'# Hivemind — suivi simple par lots\n\nActualisé : {now}\n\n**Lot en cours : {active_lot} — {active_state}.**\n\nBibliothèque finale : `F:\\OpenRPG_AssetVault`. Travail, caches, conversions et preuves : `F:\\OpenRPG_ImportWork`.\n\n## Découpage principal\n\n| Lot | Travail | État | Reste |\n|---|---|---|---|\n'
 text+=''.join('| '+ ' | '.join([v['id'],v['work'],v['status'],v['remaining']])+' |\n' for v in plan['lots'])
 text+='\n## Ordre des prochaines tranches\n\n| Sous-lot | Travail | État | Périmètre | Preuve |\n|---|---|---|---|---|\n'
 text+=''.join('| '+' | '.join([*r[:4],f'[Reçu](../{r[4]})' if r[4] else 'à produire'])+' |\n' for r in rows)
 text+='\nLes sous-lots sont exécutés successivement. Une tranche de conversion contient au plus 16 prefabs lorsque leurs dépendances le permettent ; les effets sont groupés par graphe compatible. Avant chaque tranche, la liste exacte des IDs et les sorties sont épinglées dans son manifeste. Les autres packs restent dans le périmètre complet des 48 packs/variantes du [découpage par pack](DECOUPAGE_OPERATIONNEL.md).\n\n## Ce qui ferme une livraison\n\n1. Conversion avec sources, licences, métadonnées et SHA conservés.\n2. Réouverture et comparaison du dérivé : géométrie, transforms, pivots, dépendances.\n3. Matériaux, ombres, LOD et, selon le type, animation ou effets temporels.\n4. Image pertinente et indexation transactionnelle sur F.\n5. Recherche/filtres/détails, aperçu, placement/édition, sauvegarde/réouverture et runtime applicables dans le produit.\n\nChaque résultat est consigné avec attendu/vérifié, reçu, refus éventuels et prochaine action. Un test ou une fiche support ne compte pas comme un asset complètement livré. Les 14 219 déclarations de modèles et 12 516 fiches support sont des compteurs de registre.\n\n[Historique et preuves](TABLEAU_DE_BORD_LOTS.md) · [Journal](JOURNAL.md).\n'
 text+='\nCompilation Web du snapshot '+str(web_version)+' : `'+web.get('status','à lancer')+'`. [Reçu](../studio-current-source-wasm-check-v'+str(web_version)+'/RUN-RESULT.v1.json). Les reçus précédents conservent leur périmètre daté.\n'
 write(T/'SUIVI_SIMPLE.md',text)
 data=dict(updated_utc=now,active_sub_lot='L01.OMBRES.01',state=state,expected_shadow_routes=4,verified_shadow_routes=4 if passed else 0,expected_controls=6,verified_controls=6 if passed else 0,full_import=False,editor_visual_verified=False,receipt=str(W/cap/'RUN-RESULT.v1.json'),next_action='Compilation Web corrigée, puis L01.OMBRES.02 : comparaison visuelle et états manquants' if passed else 'Terminer le contrôle natif et réparer tout refus',sub_lots=[dict(zip(['id','work','state','scope','receipt'],r)) for r in rows])
 data['web_status']=web.get('status','à lancer')
 data['counts_scope']='L01.OMBRES.01'
 data['last_host_validation_state']=state
 data['active_sub_lot']=active_lot
 data['state']=active_state
 data['current_execution']=dict(sub_lot=active_lot,state=active_state,expected_source_images=2,verified_source_images=shadow_measurement.get('images_verified',0),forge_comparison_qualified=False)
 data['current_execution'].update(expected_forge_gpu_images=2,forge_gpu_images_reopened=comparison.get('gpu_images_reopened',0),forge_receipt=str(W/f'firepit-forge-shadow-pair-v{forge_version}/RUN-RESULT.v1.json'))
 if shadow_source:
  text+='\nRéférence d’ombre : le premier essai refusé portait aussi sur les particules sans mesh statique. La version actuelle sélectionne les quatre renderers statiques ; les deux effets restent dans L06. Aucun prefab complet ni fidélité visuelle Forge n’est déduit de cette capture.\n'
  if source_case.get('source_shadow_response_verified'):
   text+='\n**L01.OMBRES.02.SOURCE vérifié : 2 images sans saturation, 524 288 pixels réouverts, 24 dépendances source.** Mesh LOD0, matériau et nombre de sommets rapprochés du GLB Forge ; 8 passes uniques compilées (32 observations). Réponse d’ombre mesurée sur le récepteur hors de l’objet : 29 016 pixels plus sombres avec ombre. Source et copies privées inchangées. [Clôture source](../firepit-shadow-comparison-case-v1/RESULT.v1.json) · [Cas de comparaison Forge préparé](../firepit-shadow-comparison-case-v1/CASE.v1.json). Capture Forge, fidélité visuelle et recette éditeur restent ouvertes.\n'
  write(T/'SUIVI_SIMPLE.md',text)
 if source_case.get('source_shadow_response_verified'):data['next_action']='L01.OMBRES.02.FORGE : capturer les pixels du cas préparé et comparer'
 if comparison:
  data['next_action']=comparison['next_action']
  text+='\nComparaison Forge : **'+comparison['status']+'**, '+str(comparison.get('gpu_images_reopened',0))+' images GPU réouvertes ; IoU des réponses d’ombre '+str(comparison.get('shadow_mask_iou'))+'. Le transfert Neutral analytique CPU reste une interprétation ; les paramètres d’ombre et la fidélité finale ne sont pas qualifiés. [Mesures et refus](../firepit-forge-shadow-pair-comparison-v2/RESULT.v1.json).\n'
  write(T/'SUIVI_SIMPLE.md',text)
 correction=read(W/'source-shadow-disabled-state-fix-v1/RESULT.v1.json')
 environment=read(W/'firepit-shadow-source-reference-v11/RUN-RESULT.v1.json')
 environment_audit=read(W/'firepit-shadow-effective-environment-v2/RESULT.v1.json')
 surface_debug=read(W/'firepit-source-surface-debug-reopen-v1/RESULT.v1.json')
 probe_packet=read(W/'source-diffuse-probe-packet-check-v1/RUN-RESULT.v1.json')
 probe_gpu=read(W/'firepit-diffuse-probe-gpu-v1/RUN-RESULT.v1.json')
 probe_reopen=read(W/'firepit-diffuse-probe-gpu-reopen-v1/RESULT.v1.json')
 source_fgd=read(W/'firepit-source-fgd-reopen-v1/RESULT.v1.json')
 probe_close=read(W/'source-diffuse-probe-install-v1/RESULT.v1.json')
 brdf_plan=read(W/'source-brdf-lookup-case-v1/CASE.v1.json')
 brdf_storage=read(W/'source-brdf-storage-check-v2/RUN-RESULT.v1.json')
 brdf_layout=read(W/'source-brdf-layout-check-v1/RESULT.v1.json')
 brdf_gpu_prepared=read(W/'firepit-brdf-lookup-gpu-case-v1/RESULT.v1.json')
 brdf_gpu_version=next((v for v in [4,3,2,1] if (W/f'firepit-brdf-lookup-gpu-v{v}').exists()),1)
 brdf_gpu=read(W/f'firepit-brdf-lookup-gpu-v{brdf_gpu_version}/RUN-RESULT.v1.json')
 brdf_reopen=read(W/f'firepit-brdf-lookup-gpu-reopen-v{brdf_gpu_version}/RESULT.v1.json')
 brdf_close=read(W/'source-brdf-lookup-install-v1/RESULT.v1.json')
 source_exposure=read(W/'firepit-source-exposure-reopen-v1/RESULT.v1.json')
 diffuse_context_version=next((v for v in [2,1] if (W/f'firepit-source-diffuse-context-v{v}').exists()),1)
 diffuse_context=read(W/f'firepit-source-diffuse-context-v{diffuse_context_version}/RUN-RESULT.v1.json')
 diffuse_context_reopen=read(W/'firepit-source-diffuse-context-reopen-v2/RESULT.v1.json')
 if correction:
  text+='\n**Correction de l’état « ombre désactivée » vérifiée sur GPU natif et compilation Web.** 856 épingles source natives et 1 040 Web inchangées ; les contrôles d’autorisation et de refus atomique passent. Cette clôture technique ne ferme pas le refus de rendu. [Reçu de correction](../source-shadow-disabled-state-fix-v1/RESULT.v1.json).\n'
  data['disabled_state_correction']=correction['status']
 if environment:
  data['source_environment_recheck']=environment['status']
  data['next_action']='Relever les réglages Unity effectifs de caméra, lumière, ciel, brouillard, éclairage indirect et ombres, puis résoudre les écarts mesurés'
  text+='\nRelevé complémentaire des paramètres effectifs Unity : `'+environment['status']+'`. [Exécution](../firepit-shadow-source-reference-v11/RUN-RESULT.v1.json). Le relevé v10 et l’audit v1 lisaient le mauvais canal HDCamera : leurs conclusions sur le ciel et la sonde sont remplacées par le relevé CustomUserHistory0. Les essais précédents sont conservés.\n'
 if environment_audit:
  data['next_action']=environment_audit['next_action']
  text+='\n**Canal de rendu réel vérifié : 2 états identiques, 24 dépendances source inchangées.** Ciel HDRI (type 1) et buffer GPU diffus SH de 7 vecteurs non nuls. Les deux images source sont identiques, octet pour octet, à la référence initiale. Ce contexte lumineux manque dans le test Forge ; son raccordement précède la nouvelle comparaison. Résolution d’ombre source 512, 4 cascades ; filtre/cascades/biais restent à qualifier. [Audit du canal et du buffer GPU](../firepit-shadow-effective-environment-v2/RESULT.v1.json).\n'
 if surface_debug:
  text+='\n**8 diagnostics de surface/éclairage HDRP réouverts, 2 097 152 pixels.** Albedo, normale, smoothness, occlusion, métal, specular, lumière diffuse directe et indirecte. Le debug indirect confirme une contribution visible. Leurs encodages SDK restent à interpréter avant comparaison matérielle. [Diagnostics](../firepit-source-surface-debug-reopen-v1/RESULT.v1.json).\n'
  data['source_surface_debug_images_verified']=8
 if probe_packet:
  data['diffuse_probe_packet_check']=probe_packet['status']
  text+='\nRaccordement commun du contexte diffus : nouveau binding explicite 121, désactivé par défaut ; repère rigide déclaré et coefficients SH mesurés, séparés du BRDF et de l’exposition. Compilation Web du snapshot 17 réussie sur 1 042 fichiers source inchangés. Tests du packet : `'+probe_packet['status']+'`. [Contrôle natif du packet](../source-diffuse-probe-packet-check-v1/RUN-RESULT.v1.json). Le rendu GPU avec cette sonde et la parité restent à produire.\n'
 if probe_gpu:
  data['diffuse_probe_gpu_witness']=probe_reopen.get('status',probe_gpu['status'])
  data['diffuse_probe_packet_tests_verified']=probe_packet.get('tests_verified',0)
  data['next_action']='Terminer la sonde GPU de six directions du binding 121, puis raccorder les facteurs BRDF/occlusion/exposition source et refaire le cas visuel initial'
  text+='\nSonde GPU du contexte diffus : `'+data['diffuse_probe_gpu_witness']+'`. Six directions connues et repère réfléchi ; coefficients SHA conservés. Le dérivé GLB est exclusivement diagnostique et reste hors de la bibliothèque. Seuils et minimum de 100 pixels par direction fixés avant lecture. [Protocole](../firepit-diffuse-probe-gpu-case-v1/RESULT.v1.json) · [Exécution](../firepit-diffuse-probe-gpu-v1/RUN-RESULT.v1.json). Ce contrôle ne qualifie ni BRDF, ni exposition, ni fidélité du rendu.\n'
 if source_fgd:
  data['source_fgd_texels_verified']=source_fgd['texels_verified']
  text+='\n**LUT BRDF Unity natif réouvert : 4 096 texels et 16 384 canaux, 24 dépendances source inchangées.** Lecture GPU Load du format A2B10G10R10, coefficients conservés par SHA, facteur diffus « bleu + 0,5 » et coordonnées sqrt(NdotV)/roughness épinglés dans le SDK ; 35 cas de lecture CPU. Échantillonnage cible, BRDF appliqué et rendu complet restent ouverts. [Preuve LUT et contrat SDK](../firepit-source-fgd-reopen-v1/RESULT.v1.json).\n'
 if probe_close:
  data['diffuse_probe_technical_closure']=probe_close['status']
  data['diffuse_probe_gpu_direction_cases_verified']=probe_close['gpu_direction_cases_verified']
  data['diffuse_probe_gpu_pixels_verified']=probe_close['gpu_witness_pixels_verified']
  data['next_action']='L01.ECLAIRAGE.BRDF : raccorder la table partagée et vérifier ses lectures GPU ; puis L01.ECLAIRAGE.APPLICATION et L01.OMBRES.02.RECOMPARAISON'
  text=text.replace('Le rendu GPU avec cette sonde et la parité restent à produire.','La sonde GPU est vérifiée ci-dessous ; la parité du rendu reste ouverte.')
  text+='\n**L01.ECLAIRAGE.SONDE — sous-lot technique clos : 2 tests natifs, 12 cas GPU, 34 526 pixels et compilation Web sur 1 042 fichiers source.** Aucun coefficient ajusté ; binding désactivé par défaut. BRDF, exposition, fidélité finale et recette éditeur restent ouverts. [Clôture](../source-diffuse-probe-install-v1/RESULT.v1.json).\n'
  new_rows=[dict(id='L01.ECLAIRAGE.SONDE',work='Sonde diffuse mesurée et repère source',state='technique vérifié',scope='2 tests ; 12 cas GPU ; 34 526 pixels',receipt='source-diffuse-probe-install-v1/RESULT.v1.json'),
   dict(id='L01.ECLAIRAGE.BRDF',work='Table BRDF partagée et échantillonnage GPU',state='protocole préparé' if brdf_plan else 'à faire',scope='35 lectures GPU attendues ; refus des données invalides',receipt='source-brdf-lookup-case-v1/CASE.v1.json' if brdf_plan else ''),
   dict(id='L01.ECLAIRAGE.APPLICATION',work='Sonde + BRDF + occlusion + exposition',state='à faire',scope='scène statique source ; contexte explicite ; réflexions HDRI à traiter',receipt=''),
   dict(id='L01.OMBRES.02.RECOMPARAISON',work='Nouvelle comparaison du cas original Unity/Forge',state='à faire',scope='2 images HDR ; ombres, biais, filtre et cascades',receipt='')]
  data['sub_lots'][3:3]=new_rows
  table=''.join('| '+' | '.join([r['id'],r['work'],r['state'],r['scope'],f"[Reçu](../{r['receipt']})" if r['receipt'] else 'à produire'])+' |\n' for r in new_rows)
  text=text.replace('| L01.LOD.01 |',table+'| L01.LOD.01 |',1)
  if brdf_plan:
   text+='\nProchain contrôle GPU BRDF : [manifeste des 35 cas et seuils fixés avant mesure](../source-brdf-lookup-case-v1/CASE.v1.json). Les résultats attendus sont des références CPU, pas des lectures GPU déjà validées.\n'
 if brdf_storage or brdf_gpu_prepared:
  brdf_state=brdf_reopen.get('status',brdf_gpu.get('status',brdf_storage.get('status','témoin GPU préparé')))
  data['brdf_storage_state']=brdf_storage.get('status','à lancer')
  data['brdf_storage_tests_verified']=brdf_storage.get('tests_verified',0)
  data['brdf_gpu_state']=brdf_reopen.get('status',brdf_gpu.get('status','35 cas préparés ; rendu à lancer'))
  data['current_work_sub_lot']='L01.ECLAIRAGE.BRDF'
  data['current_work_state']=brdf_state
  for r in data['sub_lots']:
   if r['id']=='L01.ECLAIRAGE.BRDF':
    text=text.replace('| '+r['id']+' | '+r['work']+' | '+r['state']+' |','| '+r['id']+' | '+r['work']+' | '+brdf_state+' |')
    r['state']=brdf_state
  text+='\n**L01.ECLAIRAGE.BRDF — raccordement réalisé, vérification en cours.** Table linéaire partagée au binding 122, refus de rendu si la dépendance requise manque. Test du stockage : `'+data['brdf_storage_state']+'` ('+str(data['brdf_storage_tests_verified'])+'/4 tests validés). Témoin GPU : `'+data['brdf_gpu_state']+'`. [Tests natifs](../source-brdf-storage-check-v2/RUN-RESULT.v1.json) · [35 cas GPU préparés](../firepit-brdf-lookup-gpu-case-v1/RESULT.v1.json). Ce raccordement n’est pas encore une qualification GPU, Web ou de rendu complet.\n'
 if brdf_layout:
  data['brdf_source_contract_tests_verified']=brdf_layout['tests_verified']
  text+='\n**Contrat source BRDF : 20/20 tests vérifiés, dont 3 nouveaux contrôles de déclaration.** La table de scène ne remplace aucun slot de texture d’asset ; doublons, bindings inconnus et sampler supplémentaire sont refusés. Les schemas source restent inchangés. [Preuve du contrat](../source-brdf-layout-check-v1/RESULT.v1.json).\n'
 if brdf_reopen.get('status')=='actual_gpu_brdf_35_case_sampling_verified':
  text=text.replace('Ce raccordement n’est pas encore une qualification GPU, Web ou de rendu complet.','La lecture GPU est vérifiée ; l’application au rendu complet reste ouverte.')
 if brdf_close:
  data['brdf_technical_closure']=brdf_close['status']
  data['next_action']=brdf_close['next_action']
  data['current_work_sub_lot']='L01.ECLAIRAGE.APPLICATION'
  data['current_work_state']='raccordement complet à préparer'
  for r in data['sub_lots']:
   if r['id']=='L01.ECLAIRAGE.BRDF':
    text=text.replace('| '+r['id']+' | '+r['work']+' | '+r['state']+' |','| '+r['id']+' | '+r['work']+' | technique clos : stockage, contrat, GPU et Web |')
    r['state']='technique clos : stockage, contrat, GPU et Web'
  text+='\n**L01.ECLAIRAGE.BRDF — sous-lot technique clos.** 4 tests de stockage, 20 tests de contrat, 35 cas GPU dans chacune des deux captures (70 observations), 34 526 pixels témoins, 860 épingles natives et 1 044 épingles Web vérifiées. Le refus d’une table requise manquante est atomique. Le rendu Web sur GPU, l’éclairage conjoint, les réflexions HDRI, la fidélité complète et la recette éditeur restent ouverts. [Clôture technique](../source-brdf-lookup-install-v1/RESULT.v1.json).\n'
 if source_exposure:
  data['source_exposure_texels_verified']=source_exposure['source_exposure_texels_verified']
  data['source_exposure_multiplier']=source_exposure['rows'][0]['effective_source_multiplier']
  data['current_work_sub_lot']='L01.ECLAIRAGE.APPLICATION'
  data['current_work_state']='exposition source vérifiée ; relevé du mode de rendu et de la SSAO'
  data['next_action']='Appliquer le contexte diffus après vérification du mode Forward/Deferred et de la SSAO ; traiter aussi le matériau du récepteur, puis comparer le cas original'
  text+='\n**L01.ECLAIRAGE.APPLICATION — exposition source mesurée.** Deux lectures GPU identiques : multiplicateur 0,0008138020639307797, EV100 10, facteur de probe 1. Le facteur source égale exactement la formule CPU Bevy en f32 ; aucune correction supplémentaire ne se justifie pour ce cas. Les images source restent identiques à la référence. Mode de rendu et SSAO : `'+diffuse_context.get('status','capture à vérifier')+'`. [Exposition](../firepit-source-exposure-reopen-v1/RESULT.v1.json) · [Contexte diffus](../firepit-source-diffuse-context-v1/RUN-RESULT.v1.json). L’exposition GPU cible et l’éclairage conjoint restent à vérifier.\n'
 if correction or environment:write(T/'SUIVI_SIMPLE.md',text)
 if close.get('web_exit_code')==0 and not source_case.get('source_shadow_response_verified'):data['next_action']='L01.OMBRES.02 : comparaison visuelle et états manquants'
 if source_case.get('source_shadow_response_verified'):data['current_execution']['source_reference_receipt']=str(W/'firepit-shadow-comparison-case-v1/RESULT.v1.json')
 visible_states={
  'forge_source_shadow_comparison_refused':'écarts du rendu Unity/Forge à résoudre',
  'source_shadow_response_outside_caster_bounds_verified':'référence source vérifiée',
  'actual_source_device_mount_check_running':'compilation et contrôle GPU en cours',
  'actual_brdf_storage_admission_and_refusals_verified':'4 tests natifs de stockage vérifiés',
  'actual_gpu_brdf_35_case_sampling_verified':'35 cas de lecture GPU vérifiés',
 }
 for technical,label in visible_states.items():text=text.replace(technical,label)
 if brdf_storage:
  gpu_count=35 if brdf_reopen.get('status')=='actual_gpu_brdf_35_case_sampling_verified' else 0
  data['brdf_gpu_unique_cases_verified']=gpu_count
  header=('**Travail actuel : L01.ECLAIRAGE.APPLICATION.** Exposition source mesurée ; mode de rendu et SSAO en vérification. BRDF technique clos. Le rendu complet reste à qualifier.\n\n' if source_exposure else '**Prochain sous-lot : L01.ECLAIRAGE.APPLICATION.** BRDF technique clos : 4/4 tests de stockage, 20/20 tests de contrat, 35/35 cas GPU et compilation Web. Le rendu complet du lot principal reste à qualifier.\n\n' if brdf_close else '**Travail actuel : L01.ECLAIRAGE.BRDF.** '+str(data['brdf_storage_tests_verified'])+'/4 tests natifs validés ; '+str(gpu_count)+'/35 cas GPU validés. Le rendu complet du lot principal reste à qualifier.\n\n')
  text=text.replace('## Découpage principal',header+'## Découpage principal',1)
 write(T/'SUIVI_SIMPLE.md',text)
 digest=hashlib.sha256(json.dumps(data,sort_keys=True).encode()).hexdigest();last=T/'SUIVI_SIMPLE_LAST_STATE.txt'
 if diffuse_context_reopen:
  data['current_work_state']='contexte Deferred et exposition vérifiés ; capture GPU SSAO puis application diffuse'
  data['source_diffuse_context_verified']=diffuse_context_reopen['status']
  data['next_action']='L01.ECLAIRAGE.APPLICATION : capturer les pixels SSAO réels, puis appliquer le transport diffus Deferred et recomparer Unity/Forge'
  text=text.replace('../firepit-source-diffuse-context-v1/RUN-RESULT.v1.json',f'../firepit-source-diffuse-context-v{diffuse_context_version}/RUN-RESULT.v1.json')
  text=text.replace('Exposition source mesurée ; mode de rendu et SSAO en vérification.','Exposition et contexte Deferred vérifiés ; pixels SSAO à capturer.')
  text+='\n**Contexte diffus vérifié : 2/2 états identiques, sources inchangées.** Mode Deferred ; SSAO autorisée et volume actif, intensité 0,5, force sur l’éclairage direct 0. Les pixels SSAO restent à capturer ; leur facteur ne peut pas être supposé neutre. [Preuve du contexte](../firepit-source-diffuse-context-reopen-v2/RESULT.v1.json).\n'
  write(T/'SUIVI_SIMPLE.md',text)
 ssao_reopen=read(W/'firepit-source-ssao-debug-reopen-v1/RESULT.v1.json')
 if ssao_reopen:
  data['source_ssao_diagnostic_pixels_verified']=ssao_reopen['pixels_verified']
  data['current_work_state']='contexte Deferred, exposition et diagnostic SSAO vérifiés ; application diffuse à qualifier'
  data['next_action']='L01.ECLAIRAGE.APPLICATION : appliquer la sonde et la BRDF avec AO matériau Deferred ; qualifier le transport SSAO puis recomparer le rendu'
  text=text.replace('pixels SSAO à capturer.','diagnostic SSAO réouvert ; application diffuse à qualifier.')
  text=text.replace('Les pixels SSAO restent à capturer ; leur facteur ne peut pas être supposé neutre.','Le diagnostic SSAO est maintenant réouvert ci-dessous ; son transport Forge reste à qualifier.')
  text+='\n**Diagnostic SSAO GPU réouvert : 262 144 pixels finis en niveaux de gris, valeurs de 0,482421875 à 1.** 223 729 pixels sous 0,999 : l’occlusion ne peut pas être supposée neutre. 24 dépendances source inchangées. Le transport SSAO Forge et le rendu complet restent à qualifier. [Preuve SSAO](../firepit-source-ssao-debug-reopen-v1/RESULT.v1.json).\n'
  write(T/'SUIVI_SIMPLE.md',text)
 key=f'{state}:{data["verified_controls"]}:{active_lot}:{active_state}:{probe_close.get("status", "")}:{bool(brdf_plan)}:{brdf_storage.get("status", "")}:{brdf_gpu_version}:{brdf_gpu.get("status", "")}:{brdf_reopen.get("status", "")}:{brdf_layout.get("status", "")}:{brdf_close.get("status", "")}:{source_exposure.get("status", "")}:{diffuse_context.get("status", "")}:{diffuse_context_reopen.get("status", "")}'
 key+=':'+ssao_reopen.get('status','')
 combined_run=read(W/'firepit-combined-diffuse-gpu-v1/RUN-RESULT.v1.json')
 combined_reopen=read(W/'firepit-combined-diffuse-gpu-reopen-v1/RESULT.v1.json')
 if combined_run:
  data['combined_diffuse_gpu_status']=combined_reopen.get('status',combined_run['status'])
  data['current_work_state']='48 cas GPU du transport diffus Deferred : '+data['combined_diffuse_gpu_status']
  text+='\n**Application diffuse — 48 cas GPU combinés.** Sonde mesurée, table BRDF partagée, couleurs, AO matériau, visibilité SSAO et exposition appliquée une fois. État : `'+data['combined_diffuse_gpu_status']+'`. Ce contrôle du calcul ne ferme pas l’application à la scène originale. [Exécution](../firepit-combined-diffuse-gpu-v1/RUN-RESULT.v1.json).\n'
  write(T/'SUIVI_SIMPLE.md',text)
 key+=':'+combined_reopen.get('status',combined_run.get('status',''))
 diffuse_scene_version=next((v for v in [5,4,3,2,1] if (W/f'firepit-diffuse-scene-v{v}').exists()),1)
 diffuse_scene_run=read(W/f'firepit-diffuse-scene-v{diffuse_scene_version}/RUN-RESULT.v1.json')
 diffuse_scene_comparison=read(W/f'firepit-diffuse-scene-comparison-v{diffuse_scene_version}/RESULT.v1.json')
 if diffuse_scene_run:
  data['current_work_state']='Application diffuse sur objet et sol : '+diffuse_scene_comparison.get('status',diffuse_scene_run['status'])
  data['diffuse_scene_version']=diffuse_scene_version
  data['next_action']='Terminer la capture de scène, réouvrir les pixels et comparer avec les seuils conservés ; résoudre les écarts SSAO, réflexions et ombres'
  text+='\n**Scène originale avec diffuse mesurée sur objet et sol — essai '+str(diffuse_scene_version)+'.** Géométrie, textures, caméra et 36 688 152 octets d’origine conservés. Les essais refusés restent archivés. État : `'+diffuse_scene_comparison.get('status',diffuse_scene_run['status'])+'`. La SSAO est calculée par Bevy ; les pixels SSAO Unity ne sont pas réinjectés. Algorithme SSAO, réflexions HDRI et ombres restent à qualifier. [Structure](../firepit-diffuse-scene-structure-v1/RESULT.v1.json) · [Exécution](../firepit-diffuse-scene-v'+str(diffuse_scene_version)+'/RUN-RESULT.v1.json).\n'
  write(T/'SUIVI_SIMPLE.md',text)
 key+=':'+diffuse_scene_comparison.get('status',diffuse_scene_run.get('status',''))+':'+str(diffuse_scene_version)
 diffuse_residual=read(W/'firepit-diffuse-scene-residual-v1/RESULT.v1.json')
 if diffuse_residual:
  data['current_work_sub_lot']='L01.ECLAIRAGE.HDRI'
  data['current_work_state']='Diffuse appliquée sur objet et sol ; capture des réflexions source à préparer'
  data['next_action']=diffuse_residual['next_action']
  data['diffuse_scene_full_render_qualified']=False
  text=text.replace('**Travail actuel : L01.ECLAIRAGE.APPLICATION.**','**Prochain sous-lot : L01.ECLAIRAGE.HDRI.**')
  text=text.replace('| L01.ECLAIRAGE.APPLICATION | Sonde + BRDF + occlusion + exposition | à faire |','| L01.ECLAIRAGE.APPLICATION | Sonde + BRDF + occlusion + exposition | appliqué sur GPU ; fidélité restante |')
  text+='\n**Application de la diffuse mesurée sur la scène : 2 images GPU, 5 pipelines prêts et 861 épingles source inchangées.** Erreur couleur moyenne globale réduite de 41,3 % avec ombres et 38,6 % sans ombres. La région rectangulaire du modèle reste à 0,083 / 0,081 d’erreur moyenne ; l’IoU des ombres reste 0,296 (seuil 0,95). Le rendu complet reste refusé. Prochain sous-lot : capturer les réflexions HDRI source et traiter les écarts d’occlusion et de filtre d’ombre. [Mesures séparées](../firepit-diffuse-scene-residual-v1/RESULT.v1.json) · [Comparaison](../firepit-diffuse-scene-comparison-v5/RESULT.v1.json).\n'
  write(T/'SUIVI_SIMPLE.md',text)
 key+=':'+diffuse_residual.get('status','')
 sky_source=read(W/'firepit-source-sky-reopen-v1/RESULT.v1.json')
 sky_samples=read(W/'firepit-source-sky-sampling-reopen-v3/RESULT.v1.json')
 sky_packet=read(W/'source-sky-cube-packet-v1/PACKET.v1.json')
 if sky_source and sky_samples and sky_packet:
  data['current_work_sub_lot']='L01.ECLAIRAGE.HDRI.ADMISSION'
  data['current_work_state']='Réflexion source vérifiée ; stockage cube commun et lectures GPU Forge à intégrer'
  data['next_action']=sky_packet['next_action']
  data['source_sky_texels_verified']=sky_source['texels_verified']
  data['source_sky_sampling_cases_verified']=sky_samples['sampling_cases_verified']
  data['source_sky_packet_bytes']=sky_packet['bytes']
  text=text.replace('**Prochain sous-lot : L01.ECLAIRAGE.HDRI.**','**Travail actuel : L01.ECLAIRAGE.HDRI.ADMISSION.**')
  anchor='| L01.OMBRES.02.RECOMPARAISON |'
  rows_hdr='| L01.ECLAIRAGE.HDRI.SOURCE | Réflexion HDRI native et échantillonnage | source vérifiée | 6 faces, 9 niveaux, 182 cas / 364 observations | [Preuve](../firepit-source-sky-sampling-reopen-v3/RESULT.v1.json) |\n| L01.ECLAIRAGE.HDRI.ADMISSION | Stockage partagé et dépendance de scène | à intégrer | paquet natif de 4 194 288 octets sans perte | [Paquet](../source-sky-cube-packet-v1/PACKET.v1.json) |\n| L01.ECLAIRAGE.HDRI.GPU | Lectures Forge aux faces, bords et LOD | à faire | comparer aux 182 lectures source | à produire |\n'
  text=text.replace(anchor,rows_hdr+anchor)
  text+='\n**Réflexion HDRI source vérifiée : 1 048 572 texels, 6 faces et 9 niveaux sur deux vues identiques.** 182 lectures aux axes, bords et coins avec LOD fractionnaires vérifiées sur deux vues (364 observations). Images de référence et 24 dépendances source inchangées. Paquet CubeArray Rgba16Float de 4 194 288 octets, sans perte de valeurs natives. Stockage commun, lectures GPU Forge et application spéculaire restent à réaliser ; aucun asset complet supplémentaire n’est déclaré livré. [Faces et niveaux](../firepit-source-sky-reopen-v1/RESULT.v1.json) · [Lectures source](../firepit-source-sky-sampling-reopen-v3/RESULT.v1.json) · [Paquet](../source-sky-cube-packet-v1/PACKET.v1.json).\n'
  write(T/'SUIVI_SIMPLE.md',text)
 key+=':'+sky_samples.get('status','')+':'+sky_packet.get('status','')
 sky_storage=read(W/'source-sky-cube-storage-check-v3/RUN-RESULT.v1.json')
 if sky_storage and sky_storage.get('status')=='actual_sky_cube_storage_and_residency_refusals_verified':
  data['current_work_state']='Cube natif et refus de résidence vérifiés ; raccordement aux matériaux puis GPU restant'
  data['source_sky_storage_tests_verified']=sky_storage['tests_verified']
  data['next_action']='Raccorder le cube partagé au contrat des matériaux et au précontrôle atomique de scène ; comparer ensuite les 182 lectures GPU sans modifier les seuils.'
  old='| L01.ECLAIRAGE.HDRI.ADMISSION | Stockage partagé et dépendance de scène | à intégrer | paquet natif de 4 194 288 octets sans perte | [Paquet](../source-sky-cube-packet-v1/PACKET.v1.json) |'
  new='| L01.ECLAIRAGE.HDRI.STOCKAGE | Cube partagé et refus de résidence | technique vérifié | 5/5 tests ; paquet natif inchangé ; 13 mutations et éviction refusées | [Tests](../source-sky-cube-storage-check-v3/RUN-RESULT.v1.json) |\n| L01.ECLAIRAGE.HDRI.ADMISSION | Dépendance de scène et contrat des matériaux | à raccorder | cube explicite requis ; refus avant mutation des horloges | à produire |'
  text=text.replace(old,new)
  text+='\n**HDRI — stockage et résidence : 5/5 tests natifs vérifiés.** Paquet réel de 4 194 288 octets conservé sans réencodage ; une image partagée, neuf niveaux. Treize mutations de stockage/vue/sampler et éviction refusées. Essais v1 et refus de compilation v2 conservés. Contrat matériau, précontrôle de scène, lectures GPU Forge et application spéculaire restent ouverts ; aucun asset complet supplémentaire n’est déclaré livré. [Reçu](../source-sky-cube-storage-check-v3/RUN-RESULT.v1.json).\n'
  write(T/'SUIVI_SIMPLE.md',text)
  key+=':'+sky_storage['status']
 sky_binding=read(W/'source-sky-cube-binding-check-v3/RUN-RESULT.v1.json')
 sky_gpu_case=read(W/'firepit-sky-cube-gpu-case-v1/RESULT.v1.json')
 if sky_gpu_case:
  text=text.replace('| L01.ECLAIRAGE.HDRI.GPU | Lectures Forge aux faces, bords et LOD | à faire | comparer aux 182 lectures source | à produire |',
   '| L01.ECLAIRAGE.HDRI.GPU | Lectures Forge aux faces, bords et LOD | protocole prêt ; GPU restant | 182 cas, seuils fixés avant mesure ; témoin privé hors Vault | [Protocole](../firepit-sky-cube-gpu-case-v1/RESULT.v1.json) |')
  data['source_sky_gpu_cases_prepared']=sky_gpu_case['expected_cases']
 if sky_binding:
  data['source_sky_binding_state']=sky_binding['status']
  binding_passed=sky_binding['status']=='canonical_sky_binding_and_atomic_scene_preflight_verified'
  data['source_sky_binding_qualified']=binding_passed
  if binding_passed:
   data['source_sky_contract_tests_verified']=sky_binding['contract_tests_verified']
   data['source_sky_frame_tests_verified']=sky_binding['frame_tests_verified']
   data['current_work_sub_lot']='L01.ECLAIRAGE.HDRI.GPU'
   data['current_work_state']='Raccordement HDRI et refus atomiques vérifiés ; 182 lectures GPU Forge restantes'
   data['next_action']='Capturer puis réouvrir les 182 lectures GPU Forge selon le protocole épinglé ; conserver tout refus sans ajuster les données source ni les seuils.'
   text=text.replace('**Travail actuel : L01.ECLAIRAGE.HDRI.ADMISSION.**','**Travail actuel : L01.ECLAIRAGE.HDRI.GPU.**')
   text=text.replace('| L01.ECLAIRAGE.HDRI.ADMISSION | Dépendance de scène et contrat des matériaux | à raccorder | cube explicite requis ; refus avant mutation des horloges | à produire |',
    f'| L01.ECLAIRAGE.HDRI.ADMISSION | Dépendance de scène et contrat des matériaux | technique vérifié | {sky_binding["contract_tests_verified"]} tests de contrat ; {sky_binding["frame_tests_verified"]} tests de frame ; slots conservés | [Reçu](../source-sky-cube-binding-check-v3/RUN-RESULT.v1.json) |')
   text+=f'\n**HDRI — raccordement et précontrôle atomique vérifiés : {sky_binding["contract_tests_verified"]} tests de contrat et {sky_binding["frame_tests_verified"]} tests de frame.** Cube partagé au binding 123, sampler 124, sans remplacer les sept textures de l’asset. Déclaration inconnue, doublon, paire incomplète, ressource manquante, retirée ou altérée refusés. Caméra et ombre conservent leurs horloges lors du refus. Lectures GPU, application spéculaire et recette éditeur restent ouvertes. [Reçu](../source-sky-cube-binding-check-v3/RUN-RESULT.v1.json).\n'
  else:
   data['current_work_state']='Raccordement HDRI implémenté ; tests natifs en cours ou refus conservé'
   text=text.replace('| L01.ECLAIRAGE.HDRI.ADMISSION | Dépendance de scène et contrat des matériaux | à raccorder | cube explicite requis ; refus avant mutation des horloges | à produire |',
    '| L01.ECLAIRAGE.HDRI.ADMISSION | Dépendance de scène et contrat des matériaux | implémenté ; tests à terminer | cube explicite requis ; contrôle atomique avant les horloges | [Exécution](../source-sky-cube-binding-check-v3/RUN-RESULT.v1.json) |')
  key+=':'+sky_binding['status']
 sky_gpu=read(W/'firepit-sky-cube-gpu-reopen-v1/RESULT.v1.json')
 if sky_gpu:
  data['source_sky_gpu_state']=sky_gpu['status']
  sky_gpu_passed=sky_gpu['status']=='actual_gpu_source_sky_182_case_sampling_verified'
  data['source_sky_gpu_sampling_qualified']=sky_gpu_passed
  if sky_gpu_passed:
   data['source_sky_gpu_cases_verified']=sky_gpu['sky_cube_cases_verified']
   data['source_sky_gpu_observations_verified']=sky_gpu['sky_cube_cases_verified']*sky_gpu['images_reopened']
   data['source_sky_gpu_witness_pixels']=sum(g['samples'] for r in sky_gpu['rows'] for g in r['cases'])
   data['current_work_sub_lot']='L01.ECLAIRAGE.HDRI.APPLICATION'
   data['current_work_state']='Lecture HDRI GPU vérifiée ; application spéculaire source et rendu complet restants'
   data['next_action']=sky_gpu['next_action']
   text=text.replace('**Travail actuel : L01.ECLAIRAGE.HDRI.GPU.**','**Travail actuel : L01.ECLAIRAGE.HDRI.APPLICATION.**')
   text=text.replace('| L01.ECLAIRAGE.HDRI.GPU | Lectures Forge aux faces, bords et LOD | protocole prêt ; GPU restant | 182 cas, seuils fixés avant mesure ; témoin privé hors Vault | [Protocole](../firepit-sky-cube-gpu-case-v1/RESULT.v1.json) |',
    '| L01.ECLAIRAGE.HDRI.GPU | Lectures Forge aux faces, bords et LOD | technique vérifié sur GPU natif | 182 cas / 364 observations ; 34 526 pixels ; zéro canal hors seuil | [Preuve](../firepit-sky-cube-gpu-reopen-v1/RESULT.v1.json) |\n| L01.ECLAIRAGE.HDRI.APPLICATION | Reflet spéculaire sur le modèle original | à faire | direction et roughness source, FGD, occlusion, exposition ; aucune diffuse doublée | à produire |')
   text+='\n**HDRI — 182 cas GPU vérifiés sur deux captures, soit 364 observations et 34 526 pixels témoins.** Au moins 89 pixels par cas/capture ; aucun canal RGB hors des seuils préenregistrés. 862 épingles source inchangées ; quatre matériaux utilisent le même cube et le refus d’une dépendance requise manquante est atomique. Le repère de direction source est explicite dans ce témoin ; le reflet appliqué au modèle, la fidélité du rendu complet, le rendu WebGPU et la recette éditeur restent ouverts. [Mesures GPU](../firepit-sky-cube-gpu-reopen-v1/RESULT.v1.json) · [Structure inchangée](../firepit-sky-cube-gpu-case-v1/STRUCTURE.v1.json).\n'
  key+=':'+sky_gpu['status']
 sky_close=read(W/'source-sky-cube-install-v1/RESULT.v1.json')
 if sky_close and sky_close.get('status')=='explicit_source_sky_storage_binding_native_gpu_and_web_compile_technical_batch_verified':
  data['source_sky_technical_closure']=sky_close['status']
  data['source_sky_technical_closure_receipt']=str(W/'source-sky-cube-install-v1/RESULT.v1.json')
  data['source_sky_web_compile_verified']=sky_close['web_frontend_compile_verified']
  data['source_sky_web_gpu_render_verified']=False
  text+='\n**HDRI — sous-lot technique clos : 5 tests de stockage, 23 tests de contrat, 7 tests de frame, 182 cas GPU sur deux captures et compilation Web du snapshot 19.** 862 épingles natives et 1 046 Web inchangées ; original de géométrie et ABI de repère/temps conservés. Le reflet appliqué à la scène, la fidélité complète et la recette éditeur restent à qualifier. Aucun asset complet supplémentaire n’est déclaré livré. [Clôture technique](../source-sky-cube-install-v1/RESULT.v1.json).\n'
  key+=':'+sky_close['status']
 specular_source=read(W/'firepit-source-specular-sdk-reopen-v2/RESULT.v1.json')
 if specular_source and specular_source.get('status')=='native_source_deferred_specular_sdk_756_cases_live_context_verified':
  data['source_specular_sdk_status']=specular_source['status']
  data['current_work_sub_lot']='L01.ECLAIRAGE.HDRI.APPLICATION'
  data['current_work_state']='756 cas spéculaires Unity vérifiés ; comparaison GPU Forge en cours'
  text=text.replace('| L01.ECLAIRAGE.HDRI.APPLICATION | Reflet spéculaire sur le modèle original | à faire | direction et roughness source, FGD, occlusion, exposition ; aucune diffuse doublée | à produire |',
   '| L01.ECLAIRAGE.HDRI.SPECULAIRE.SOURCE | Calcul spéculaire Deferred Unity | source vérifiée | 756 cas / 1 512 observations ; encodage normal, F0, occlusion, BRDF et ciel | [Preuve](../firepit-source-specular-sdk-reopen-v2/RESULT.v1.json) |\n| L01.ECLAIRAGE.HDRI.SPECULAIRE.GPU | Même calcul dans Forge | en cours | 756 cas ; seuils fixés avant mesure ; témoin privé | [Protocole](../firepit-specular-gpu-case-v1/RESULT.v1.json) |\n| L01.ECLAIRAGE.HDRI.APPLICATION | Reflet sur le modèle original | à faire | appliquer le calcul vérifié puis comparer les deux images | à produire |')
  text+='\n**Spéculaire — référence Unity vérifiée : 756 cas sur deux vues, soit 1 512 observations.** Encodage Deferred matériel et normale réellement exécuté sur GPU, BRDF, occlusion et cube source conservés. Le CBuffer effectif indique un mélange d’occlusion de 1 ; le premier relevé global erroné reste archivé sans qualification. Le calcul Forge puis son application au modèle restent à vérifier. [Preuve source](../firepit-source-specular-sdk-reopen-v2/RESULT.v1.json).\n'
  key+=':'+specular_source['status']
 specular_gpu_path=W/'firepit-specular-fixed8-gpu-reopen-v1/RESULT.v1.json'
 if not specular_gpu_path.exists():specular_gpu_path=W/'firepit-specular-gpu-reopen-v1/RESULT.v1.json'
 specular_gpu=read(specular_gpu_path)
 if specular_gpu:
  data['source_specular_gpu_status']=specular_gpu['status']
  data['source_specular_gpu_receipt']=str(specular_gpu_path)
  specular_passed=specular_gpu['status']=='actual_gpu_deferred_specular_756_case_composition_verified'
  data['source_specular_gpu_qualified']=specular_passed
  data['source_specular_gpu_cases_verified']=specular_gpu['source_specular_cases_verified']
  data['source_specular_gpu_observations_verified']=specular_gpu['source_specular_cases_verified']*specular_gpu['images_reopened']
  data['current_work_state']='Calcul spéculaire GPU vérifié ; application au modèle restante' if specular_passed else 'Comparaison spéculaire GPU refusée ; diagnostic des écarts en cours'
  text=text.replace('| Même calcul dans Forge | en cours |','| Même calcul dans Forge | '+('vérifié' if specular_passed else 'refus à résoudre')+' |')
  if specular_passed:text=text.replace('firepit-specular-gpu-case-v1/RESULT.v1.json','firepit-specular-fixed8-gpu-case-v1/RESULT.v1.json')
  text+='\n**Spéculaire — résultat GPU Forge : `'+specular_gpu['status']+'`, '+str(specular_gpu['source_specular_cases_verified'])+'/756 cas vérifiés sur deux captures.** Les seuils restent inchangés. L’interpolation source quantifiée corrige les deux écarts du premier essai, conservé. Ce résultat concerne le calcul, pas un asset complètement livré. [Mesures](../'+specular_gpu_path.parent.name+'/RESULT.v1.json).\n'
  key+=':'+specular_gpu['status']
 specular_scene_version=next((v for v in [2,1] if (W/f'firepit-specular-scene-v{v}/RUN-RESULT.v1.json').exists()),1)
 specular_scene_run=read(W/f'firepit-specular-scene-v{specular_scene_version}/RUN-RESULT.v1.json')
 specular_scene_comparison=read(W/f'firepit-specular-scene-comparison-v{specular_scene_version}/RESULT.v1.json')
 if specular_scene_run:
  data['specular_scene_execution']=specular_scene_run['status']
  data['specular_scene_version']=specular_scene_version
  data['current_work_state']='Spéculaire appliqué au modèle et au sol ; capture native en cours'
  text=text.replace('| Reflet sur le modèle original | à faire |','| Reflet sur le modèle original | capture en cours |')
  text+='\n**Application diffuse et spéculaire au modèle original et au sol :** `'+specular_scene_run['status']+'`. Géométrie et textures conservées ; protocole de comparaison inchangé et défini avant les pixels. [Exécution](../firepit-specular-scene-v1/RUN-RESULT.v1.json) · [Structure](../firepit-specular-scene-case-v1/STRUCTURE.v1.json).\n'
  key+=':'+specular_scene_run['status']
 if specular_scene_comparison:
  data['specular_scene_comparison']=specular_scene_comparison['status']
  data['current_work_state']='Diffuse et spéculaire appliqués ; rendu complet : '+specular_scene_comparison['status']
  data['next_action']='L01.ECLAIRAGE.SSAO.CIBLE : mesurer la SSAO native sur la scène originale, puis rapprocher éclairage direct et filtre d’ombre source'
  text=text.replace('| Reflet sur le modèle original | capture en cours |','| Reflet sur le modèle original | mesuré ; fidélité restante |')
  text+='\n**Rendu complet après spéculaire :** `'+specular_scene_comparison['status']+'` ; IoU des ombres '+str(specular_scene_comparison['shadow_mask_iou'])+'. [Comparaison](../firepit-specular-scene-comparison-v1/RESULT.v1.json). La présentation CPU analytique ne qualifie pas le transfert GPU ni l’éditeur.\n'
  key+=':'+specular_scene_comparison['status']
  text=text.replace('| L01.OMBRES.02.RECOMPARAISON |',
   '| L01.ECLAIRAGE.SSAO.CIBLE | Diagnostic de l’occlusion Forge | prochain sous-lot | capturer les valeurs natives ; source déjà réouverte ; aucune réinjection de pixels Unity | à produire |\n| L01.ECLAIRAGE.DIRECT.SOURCE | Éclairage direct effectif source | à rapprocher | paramètres réellement utilisés et BRDF ; distinguer le transfert de présentation | à produire |\n| L01.OMBRES.02.RECOMPARAISON |')
 for family in ['firepit-specular-scene-v','firepit-specular-scene-case-v','firepit-specular-scene-comparison-v']:
  text=text.replace(family+'1/',family+str(specular_scene_version)+'/')
 ssao_native=read(W/'firepit-ssao-native-reopen-v1/RESULT.v1.json')
 if ssao_native:
  data['native_ssao_measurement']=ssao_native['status']
  data['current_work_sub_lot']='L01.ECLAIRAGE.SSAO.CIBLE'
  data['current_work_state']='Occlusion native mesurée ; projection de l’intensité source à vérifier'
  text=text.replace('| Diagnostic de l’occlusion Forge | prochain sous-lot | capturer les valeurs natives ; source déjà réouverte ; aucune réinjection de pixels Unity | à produire |',
   '| Diagnostic de l’occlusion Forge | mesuré sur deux vues | 225 699 fragments par vue ; visibilité 0,872 / source 0,976 ; algorithmes différents | [Preuve](../firepit-ssao-native-reopen-v1/RESULT.v1.json) |')
  text+='\n**SSAO cible mesurée sur deux vues : 225 699 fragments par vue.** Visibilité moyenne 0,872 contre 0,976 source aux mêmes coordonnées ; dans le rectangle du modèle, 0,799 contre 0,938. Ce rectangle ne constitue pas une segmentation d’objet. Paramètres natifs inchangés, aucune réinjection de pixels source. [Mesures](../firepit-ssao-native-reopen-v1/RESULT.v1.json).\n'
  key+=':'+ssao_native['status']
 source_ao_run=read(W/'firepit-source-ao-scene-v1/RUN-RESULT.v1.json')
 source_ao_comparison=read(W/'firepit-source-ao-scene-comparison-v1/RESULT.v1.json')
 if source_ao_run:
  data['source_ao_intensity_projection_run']=source_ao_run['status']
  data['current_work_state']='Intensité AO source 0,5 projetée ; capture/comparaison à vérifier'
  text=text.replace('| L01.ECLAIRAGE.DIRECT.SOURCE |',
   '| L01.ECLAIRAGE.SSAO.INTENSITE | Intensité d’occlusion source | essai en cours | visibilité puissance 0,5 suivant SDK ; paramètres natifs conservés | [Exécution](../firepit-source-ao-scene-v1/RUN-RESULT.v1.json) |\n| L01.ECLAIRAGE.DIRECT.SOURCE |')
  text+='\nProjection de l’intensité AO source : valeur 0,5 déclarée par les deux volumes source identiques ; formule SDK `visibilité^intensité`. Le calcul natif de voisinage reste distinct. [Protocole de scène](../firepit-source-ao-scene-case-v1/RESULT.v1.json).\n'
  key+=':'+source_ao_run['status']
 if source_ao_comparison:
  data['source_ao_intensity_scene_comparison']=source_ao_comparison['status']
  data['current_work_state']='Intensité AO source appliquée ; rendu complet : '+source_ao_comparison['status']
  data['next_action']='L01.ECLAIRAGE.DIRECT.SOURCE : mesurer les données directionnelles effectives ; poursuivre filtre d’ombre et qualification SSAO'
  text=text.replace('| Intensité d’occlusion source | essai en cours |','| Intensité d’occlusion source | mesuré ; fidélité restante |')
  text+='\n**Scène après intensité AO source :** `'+source_ao_comparison['status']+'` ; erreur couleur '+str([r['mae'] for r in source_ao_comparison['linear_color_errors']])+'. [Comparaison](../firepit-source-ao-scene-comparison-v1/RESULT.v1.json). Algorithme SSAO, éclairage direct, ombres, présentation GPU et éditeur restent à qualifier.\n'
  key+=':'+source_ao_comparison['status']
 directional_source=read(W/'firepit-source-directional-gpu-reopen-v1/RESULT.v1.json')
 if directional_source:
  data['source_directional_gpu_verification']=directional_source['status']
  data['current_work_sub_lot']='L01.MATERIAUX.DIAGNOSTICS'
  data['current_work_state']='Lumière directionnelle source vérifiée ; surfaces et BRDF direct à rapprocher'
  data['next_action']=directional_source['next_action']
  text=text.replace('| Éclairage direct effectif source | à rapprocher | paramètres réellement utilisés et BRDF ; distinguer le transfert de présentation | à produire |',
   '| Éclairage direct effectif source | données GPU vérifiées | une lumière / deux vues ; 26 champs ; couleur et direction cohérentes avec les paramètres déclarés Forge | [Preuve](../firepit-source-directional-gpu-reopen-v1/RESULT.v1.json) |')
  text=text.replace('| L01.OMBRES.02.RECOMPARAISON |',
   '| L01.MATERIAUX.DIAGNOSTICS | Surfaces source et Forge | prochain sous-lot | base couleur, normales, roughness, métal et AO ; interpréter les encodages SDK avant comparaison | à produire |\n| L01.OMBRES.02.RECOMPARAISON |')
  text+='\n**Directionnel source : une lumière, 26 champs GPU sur deux vues vérifiés contre les octets et le layout SDK de 176 octets.** Couleur 10 000 par canal, direction cohérente avec le repère déclaré Forge ; atténuations à 1, cookie et contact shadow absents. Seul l’index d’ombre change. Les deux images source et 24 dépendances sont inchangées. Ceci ne qualifie pas le BRDF direct cible ni le filtre d’ombre. [Preuve](../firepit-source-directional-gpu-reopen-v1/RESULT.v1.json).\n'
  key+=':'+directional_source['status']
 specular_stage_version=next((v for v in [3,2,1] if (W/f'firepit-specular-stage-gpu-v{v}/RUN-RESULT.v1.json').exists()),None)
 if specular_stage_version:
  specular_stage_run=read(W/f'firepit-specular-stage-gpu-v{specular_stage_version}/RUN-RESULT.v1.json')
  data['specular_stage_diagnostic_status']=specular_stage_run['status']
  text+='\nDiagnostic des deux cas refusés : normale, BRDF, ciel et résultat séparés ; essai '+str(specular_stage_version)+', état `'+specular_stage_run['status']+'`. [Exécution](../firepit-specular-stage-gpu-v'+str(specular_stage_version)+'/RUN-RESULT.v1.json). Le refus shader du premier diagnostic est conservé.\n'
  key+=':stage'+str(specular_stage_version)+':'+specular_stage_run['status']
 surface_debug=read(W/'firepit-surface-native-reopen-v1/RESULT.v1.json')
 if surface_debug:
  data['surface_debug_comparison']=surface_debug['status']
  data['current_work_sub_lot']='L01.MATERIAUX.DIAGNOSTICS'
  data['current_work_state']='Cinq propriétés mesurées sur deux vues ; écarts conservés, repère et correspondance des surfaces à vérifier'
  data['next_action']='Mesurer les matrices caméra source et rapprocher positions, UV et normales avant de modifier le BRDF direct.'
  data['surface_debug_model_role_measurements']=surface_debug['rows'][0]['groups']['model']
  text=text.replace('| Surfaces source et Forge | prochain sous-lot | base couleur, normales, roughness, métal et AO ; interpréter les encodages SDK avant comparaison | à produire |',
   '| Surfaces source et Forge | mesuré ; refus à résoudre | 5 propriétés sur 2 vues ; 883 épingles source ; métal passe, autres propriétés refusées | [Preuve](../firepit-surface-native-reopen-v1/RESULT.v1.json) |')
  text+='\n**Surfaces — cinq propriétés réouvertes sur deux vues, 883 épingles source inchangées.** Métal dans le seuil fixé de 2/255 ; couleur, normales, smoothness et AO du modèle hors seuil. Écart moyen des normales : 54,73°. Le repère objet et le repère monde déclarent tous deux la réflexion X ; une rotation arbitraire n’est pas justifiée. Vérification des matrices caméra et de la correspondance des surfaces en cours. Les essais restent refusés ; aucune livraison complète supplémentaire. [Mesures](../firepit-surface-native-reopen-v1/RESULT.v1.json) · [Planche de diagnostic](../firepit-surface-layout-review-v1/surface-layout.png).\n'
  key+=':'+surface_debug['status']
  data['last_shadow_execution']=data['current_execution']
  measurements=[g for r in surface_debug['rows'] for group in r['groups'].values() for g in group]
  data['current_execution']={'sub_lot':'L01.MATERIAUX.DIAGNOSTICS','state':surface_debug['status'],'measurement_scope':'5 propriétés × 2 masques × 2 vues, aucun compteur d’assets','expected_role_measurements':20,'measured_roles':len(measurements),'role_measurements_within_threshold':sum(g['within_predeclared_mae'] for g in measurements),'receipt':str(W/'firepit-surface-native-reopen-v1/RESULT.v1.json'),'full_import':False}
  active_lot=data['current_work_sub_lot'];active_state=data['current_work_state']
  data['active_sub_lot']=active_lot;data['state']=surface_debug['status']
 camera_source=read(W/'firepit-source-camera-matrices-reopen-v1/RESULT.v1.json')
 if camera_source:
  data['surface_source_camera_verification']=camera_source['status']
  data['next_action']=camera_source['next_action']
  text+='\n**Caméra source — deux vues, projection à 30° sans jitter et 24 dépendances vérifiées.** Images et buffers identiques à la référence ; canal CustomUserHistory0, repère caméra relatif et inversion Y de projection du render target explicités. Relevé des transforms et matrices extraites Forge en cours, sans modifier le rendu. [Preuve caméra source](../firepit-source-camera-matrices-reopen-v1/RESULT.v1.json).\n'
  key+=':'+camera_source['status']
 camera_native=read(W/'firepit-surface-camera-native-reopen-v1/RESULT.v1.json')
 if camera_native:
  data['surface_native_camera_verification']=camera_native['status']
  data['current_work_state']='Caméras et transforms observés ; images inchangées ; culling et attributs vertex à contrôler'
  data['next_action']=camera_native['next_action']
  active_state=data['current_work_state']
  distance=max(r['maximum_cpu_projection_distance_pixels'] for r in camera_native['rows'])
  text+='\n**Relevé natif réouvert : deux images GPU identiques au témoin précédent.** Caméra, transforms des draws, repères matériau et cinq vues extraites observés. Les projections CPU des 3 381 sommets source/cible diffèrent au plus de '+format(distance,'.5f')+' pixel. Ce contrôle ne lit pas les uniforms GPU et l’enveloppe des sommets ne constitue pas une silhouette visible. Prochaine action : contrôler orientation des triangles, culling et attributs vertex. [Preuve native](../firepit-surface-camera-native-reopen-v1/RESULT.v1.json).\n'
  key+=':'+camera_native['status']
 cull_control=read(W/'firepit-surface-cull-control-reopen-v1/RESULT.v1.json')
 if cull_control:
  data['surface_cull_control_state']=cull_control['status']
  data['current_work_state']='Contrôle privé sans culling réouvert ; écarts conservés ; matrices GPU à lire'
  data['next_action']='Lire six matrices réellement consommées par le shader via 96 scalaires encodés sans perte ; conserver culling, géométrie et shaders source.'
  text+='\n**Contrôle privé sans culling : écart des normales 54,71°, contre 54,73° auparavant ; comparaison toujours refusée.** Buffers de 36 911 132 octets entièrement inchangés ; seule la copie de l’état de culling est modifiée, sans livraison au Vault. Le culling source est conservé. [Mesures](../firepit-surface-cull-control-reopen-v1/RESULT.v1.json). Prochaine tranche : six matrices GPU, 96 scalaires encodés par leurs octets.\n'
  key+=':cull_control:'+cull_control['status']
 frame_gpu=read(W/'firepit-gpu-frame-matrix-reopen-v1/RESULT.v1.json')
 if frame_gpu:
  data['actual_gpu_frame_matrices']=frame_gpu['status']
  data['current_work_state']='96 scalaires GPU réouverts ; le modèle consomme la translation du sol ; transport d’index d’instance à corriger'
  data['next_action']='Tester le transport explicite de l’index d’instance suivant le contournement DX12 de Bevy ; puis recomparer les cinq propriétés aux mêmes seuils.'
  text+='\n**Matrices GPU — 96 scalaires / deux vues réouverts par leurs octets, 887 épingles source vérifiées.** Les repères matériau et caméra égalent les relevés CPU, mais le world_from_local consommé par le modèle porte la translation du sol (écart Y de 0,04089). Le shader Bevy épinglé documente le transport séparé de l’index pour DX12 ; un candidat privé est en cours. [Preuve GPU](../firepit-gpu-frame-matrix-reopen-v1/RESULT.v1.json). Aucun asset complet livré.\n'
  key+=':'+frame_gpu['status']
 instance_close=read(W/'source-instance-transport-technical-v1/RESULT.v1.json')
 if instance_close:
  data['instance_transport_technical_correction']=instance_close['status']
  data['current_work_sub_lot']='L01.INSTANCE.CONVERTISSEURS'
  data['current_work_state']='Correctif d’index d’instance vérifié sur GPU privé ; intégration aux convertisseurs et fidélité restante'
  data['next_action']=instance_close['next_action']
  data['complete_assets_delivered_increment']=0
  data['surface_roles_after_instance_correction']=instance_close['model_comparisons']
  data['current_execution']={'sub_lot':'L01.INSTANCE.CONVERTISSEURS','state':instance_close['status'],'measurement_scope':'correctif privé ; matrices et surfaces, aucun compteur d’assets','matrix_scalars_reopened_per_view':96,'views_verified':2,'surface_measurements':20,'surface_measurements_within_threshold':instance_close['surface_measurements_within_threshold'],'receipt':str(W/'source-instance-transport-technical-v1/RESULT.v1.json'),'full_import':False}
  text=text.replace('| L01.OMBRES.02.RECOMPARAISON |','| L01.INSTANCE.GPU | Transport de l’index d’instance | correctif privé vérifié | 96 scalaires × 2 vues ; 160 valeurs CPU/GPU égales ; données source conservées | [Preuve](../source-instance-transport-technical-v1/RESULT.v1.json) |\n| L01.INSTANCE.CONVERTISSEURS | Appliquer la recette commune aux nouveaux programmes | à faire | préserver snapshots historiques ; recomparer surfaces et scène avant livraison | à produire |\n| L01.OMBRES.02.RECOMPARAISON |')
  text+='\n**Correctif privé d’index d’instance — tranche technique vérifiée.** Après passage explicite de l’index depuis l’entrée vertex, 160 valeurs de cinq matrices égalent exactement leurs valeurs CPU sur deux vues. Couleur : erreur 0,1793 → 0,01049 ; normales : 54,73° → 3,81° ; AO : 0,2540 → 0,02104. Rugosité et métal du modèle passent ; les cinq propriétés du sol passent. Total : 14/20 comparaisons dans le seuil inchangé de 2/255. Couleur, normales et AO du modèle restent refusées. Aucun asset livré supplémentaire ni installation du correctif dans le Vault. Prochaine tranche : convertisseurs communs puis nouvelle scène. [Clôture technique](../source-instance-transport-technical-v1/RESULT.v1.json).\n'
  key+=':'+instance_close['status']
 converter_batches=read(W/'source-instance-converter-batches-reopen-v1/RESULT.v1.json')
 sampling=read(W/'firepit-source-sampling-context-reopen-v1/RESULT.v1.json')
 corrected_scene=read(W/'firepit-source-scene-instance-fix-comparison-v1/RESULT.v1.json')
 origin_control=read(W/'firepit-surface-origin-control-reopen-v1/RESULT.v1.json')
 if converter_batches:
  data['instance_converter_batches']=converter_batches['status']
  data['current_work_sub_lot']='L01.MATERIAUX.ECHANTILLONNAGE'
  data['current_work_state']='56 variantes de programmes réouvertes ; fidélité de couleur, normales, AO et ombres encore refusée'
  data['next_action']='Comparer le format HDR et les gradients de textures effectivement mesurés côté Unity ; conserver les seuils et les matériaux source.'
  data['complete_assets_delivered_increment']=0
  data['current_execution']={'sub_lot':data['current_work_sub_lot'],'state':converter_batches['status'],'measurement_scope':'programmes générés et contrôles techniques, aucun compteur d’assets','generated_stage_variants_verified':56,'positive_compositions_verified':224,'unsupported_feature_refusals_preserved':5,'source_output_pins_verified':converter_batches['pin_count'],'receipt':str(W/'source-instance-converter-batches-reopen-v1/RESULT.v1.json'),'renderer_installed':False,'full_import':False}
  text=text.replace('| Appliquer la recette commune aux nouveaux programmes | à faire | préserver snapshots historiques ; recomparer surfaces et scène avant livraison | à produire |','| Appliquer la recette commune aux nouveaux programmes | génération et réouverture vérifiées | 56 variantes / 224 compositions ; 5 refus préservés ; installation runtime restante | [Preuve](../source-instance-converter-batches-reopen-v1/RESULT.v1.json) |')
  text=text.replace('| L01.OMBRES.02.RECOMPARAISON | Nouvelle comparaison du cas original Unity/Forge | à faire | 2 images HDR ; ombres, biais, filtre et cascades | à produire |','| L01.OMBRES.02.RECOMPARAISON | Nouvelle comparaison du cas original Unity/Forge | mesuré ; refus conservé | 2 images HDR ; erreur couleur réduite ; IoU ombre 0,1925 | [Preuve](../firepit-source-scene-instance-fix-comparison-v1/RESULT.v1.json) |')
  text=text.replace('| L01.LOD.01 |','| L01.MATERIAUX.ECHANTILLONNAGE | Format HDR et gradients de textures | en cours | contexte Unity réel réouvert ; parité GPU restante | [Preuve](../firepit-source-sampling-context-reopen-v1/RESULT.v1.json) |\n| L01.LOD.01 |')
  text+='\n**Convertisseurs communs — 56 variantes réouvertes, 224 compositions réussies et 158 épingles vérifiées.** Les 48 variantes historiques ont été traitées en trois tranches de 16 ; six programmes à deux UV et deux programmes UV0 complètent ce sous-lot. Les cinq refus de fonctionnalités non qualifiées restent actifs. Génération vérifiée, installation runtime et fidélité GPU non qualifiées ; aucun asset ajouté au Vault. [Clôture de génération](../source-instance-converter-batches-reopen-v1/RESULT.v1.json).\n'
  key+=':'+converter_batches['status']
 if corrected_scene:
  data['corrected_scene_comparison']=corrected_scene['status']
  text+='\n**Scène corrigée — comparaison encore refusée.** Erreur couleur moyenne 0,02697 / 0,02363, contre 0,03643 / 0,03640 avant correction. IoU de la réponse d’ombre 0,19249, contre 0,29569 auparavant : les ombres ne passent pas. Les seuils préexistants sont conservés ; les chemins du cas corrigé ont été liés après capture, sans prétendre à un nouveau protocole défini avant capture. [Comparaison](../firepit-source-scene-instance-fix-comparison-v1/RESULT.v1.json).\n'
  key+=':corrected_scene:'+corrected_scene['status']
 if origin_control:
  data['origin_control_status']=origin_control['status']
  text+='\n**Contrôle de l’origine du monde — écarts toujours refusés.** Retirer le décalage commun de 1 024 ne réduit que légèrement les erreurs : couleur 0,01027, normales 3,63°, AO 0,02047. Ce décalage ne justifie pas à lui seul une réécriture du repère moteur. [Mesure](../firepit-surface-origin-control-reopen-v1/RESULT.v1.json).\n'
  key+=':'+origin_control['status']
 if sampling:
  data['source_sampling_context_verification']=sampling['status']
  data['state']='source_sampling_verified_render_fidelity_refused'
  data['source_sampling_context']=sampling['sampling_context']
  text+='\n**Échantillonnage Unity — deux vues et 24 dépendances vérifiées ; images de référence identiques.** Biais global et biais des textures à zéro, textures bilinéaires sans streaming, dix affectations contrôlées. Format couleur interne mesuré : B10G11R11_UFloatPack32 ; la sortie de capture RGBAHalf ne décrit donc pas ce format interne. La parité des gradients et de l’encodage couleur reste à vérifier. [Relevé réouvert](../firepit-source-sampling-context-reopen-v1/RESULT.v1.json).\n'
  key+=':'+sampling['status']
 packed_hdr=read(W/'firepit-surface-packed-hdr-control-reopen-v1/RESULT.v1.json')
 varying_case=read(W/'firepit-varying-native-case-v1/RESULT.v1.json')
 varying_native=read(W/'firepit-varying-native-reopen-v1/RESULT.v1.json')
 if packed_hdr:
  data['packed_hdr_output_control']=packed_hdr['status']
  data['current_work_sub_lot']='L01.MATERIAUX.UV_TBN'
  data['current_work_state']='Format HDR mesuré sur GPU ; écarts du modèle conservés ; diagnostic UV et base tangente en cours'
  data['next_action']='Réouvrir la paire UV/TBN Unity/Forge ; distinguer erreur des attributs interpolés et erreur d’échantillonnage des textures.'
  data['current_execution']={'sub_lot':data['current_work_sub_lot'],'state':packed_hdr['status'],'measurement_scope':'témoin HDR GPU sur cinq rôles de surfaces ; aucune livraison d’asset','views_verified':2,'source_pins_verified':packed_hdr['source_pins_verified'],'receipt':str(W/'firepit-surface-packed-hdr-control-reopen-v1/RESULT.v1.json'),'full_import':False}
  text=text.replace('| Format HDR et gradients de textures | en cours | contexte Unity réel réouvert ; parité GPU restante | [Preuve](../firepit-source-sampling-context-reopen-v1/RESULT.v1.json) |','| Format HDR et gradients de textures | format HDR mesuré ; gradients restants | sortie GPU Rg11b10Ufloat réouverte ; erreurs modèle restantes | [Preuve](../firepit-surface-packed-hdr-control-reopen-v1/RESULT.v1.json) |')
  text=text.replace('| L01.LOD.01 |','| L01.MATERIAUX.UV_TBN | UV et base tangente interpolés | en cours | 4 diagnostics source ; paire GPU native | [Cas préparé](../firepit-varying-native-case-v1/RESULT.v1.json) |\n| L01.LOD.01 |')
  text+='\n**Format HDR réel — deux captures GPU réouvertes, 891 épingles source inchangées.** Conversion matérielle de sortie Rg11b10Ufloat, sans changer shaders ni géométrie ; masques alpha conservés séparément avec caméra/transforms identiques. Normale du sol : erreur moyenne 0,001384 → 0,000000704. Le modèle reste refusé : couleur 0,01044, normale 3,805°, AO 0,02069. Le format explique l’écart constant du sol ; il ne résout pas les écarts du modèle et ne qualifie pas les buffers internes d’éclairage. [Preuve](../firepit-surface-packed-hdr-control-reopen-v1/RESULT.v1.json).\n'
  key+=':packed_hdr:'+packed_hdr['status']
 if varying_case:
  data['source_varying_case_prepared']=varying_case['status']
  text+='\n**UV/TBN source — quatre diagnostics HDRP natifs réouverts, 1 048 576 valeurs RGBA et 24 dépendances.** Images source principales identiques à la référence ; 37 020 096 octets de buffers du témoin natif conservés. La comparaison native utilise les encodages SDK et le format HDR mesuré. Les UV du sol de test ne constituent pas une géométrie source équivalente et ne seront pas promus en preuve de parité. [Cas et épingles](../firepit-varying-native-case-v1/RESULT.v1.json).\n'
  key+=':'+varying_case['status']
 if varying_native:
  data['varying_native_comparison']=varying_native['status']
  data['current_work_state']='Paire UV/TBN réouverte ; poursuivre selon les écarts mesurés'
  data['varying_model_comparisons']=varying_native['rows'][0]['groups']['model']
  text+='\n**Paire UV/TBN native réouverte.** Quatre rôles sur deux vues, état `'+varying_native['status']+'`. Aucun asset complet ni fidélité d’échantillonnage implicitement qualifiés. [Mesures](../firepit-varying-native-reopen-v1/RESULT.v1.json).\n'
  key+=':varying_native:'+varying_native['status']
 varying_close=read(W/'firepit-varying-transport-technical-v1/RESULT.v1.json')
 if varying_close:
  data['varying_model_technical_checks']=varying_close['status']
  data['current_work_sub_lot']='L01.MATERIAUX.GRADIENTS'
  data['current_work_state']='UV/TBN du modèle : 8/8 contrôles moyens vérifiés ; gradients et choix implicite des mip à mesurer'
  data['next_action']=varying_close['next_action']
  data['state']='model_varying_average_checks_verified_sampling_fidelity_open'
  data['current_execution']={'sub_lot':data['current_work_sub_lot'],'state':varying_close['status'],'measurement_scope':'8 contrôles moyens UV/TBN du modèle, aucune fidélité locale ni livraison complète','views_verified':2,'model_role_comparisons_verified':8,'maximum_local_channel_error':varying_close['maximum_local_channel_error'],'receipt':str(W/'firepit-varying-transport-technical-v1/RESULT.v1.json'),'full_import':False}
  text=text.replace('| UV et base tangente interpolés | en cours | 4 diagnostics source ; paire GPU native | [Cas préparé](../firepit-varying-native-case-v1/RESULT.v1.json) |','| UV et base tangente interpolés | contrôle moyen modèle vérifié | 8/8 comparaisons ; écarts locaux conservés ; UV sol non équivalents | [Clôture limitée](../firepit-varying-transport-technical-v1/RESULT.v1.json) |')
  text=text.replace('| L01.LOD.01 |','| L01.MATERIAUX.GRADIENTS | Gradients UV et choix implicite des mip | prochain sous-lot | modèle original ; aucun ajustement de texture ni seuil | à produire |\n| L01.LOD.01 |')
  text+='\n**UV/TBN modèle — 8/8 contrôles moyens dans le seuil initial de 2/255 sur deux vues.** Erreurs moyennes : UV0 0,000720, tangente 0,001082, bitangente 0,000683, normale géométrique 0,001486. Les erreurs ponctuelles atteignent 0,98096 ; la fidélité locale ne découle pas de ces moyennes. Les UV du sol non équivalent restent refusés. Prochain sous-lot : gradients UV et choix implicite des mip ; les refus couleur, normale de surface, AO et ombres restent ouverts. [Clôture technique limitée](../firepit-varying-transport-technical-v1/RESULT.v1.json). Aucun asset complet supplémentaire.\n'
  key+=':'+varying_close['status']
 mip_refusal=read(W/'firepit-mip-palette-refusal-v1/RESULT.v1.json')
 sampling_quality=read(W/'firepit-sampling-quality-diagnostic-v1/RESULT.v1.json')
 sampling_policy=read(W/'firepit-source-sampling-policy-technical-v1/RESULT.v1.json')
 if mip_refusal:
  data['mip_palette_protocol_refusal']=mip_refusal['status']
  text+='\n**Palette de mip — protocole d’identifiants entiers refusé et conservé.** 17 656 pixels modèle examinés ; 2 578 pixels source gris dépassent la distance d’arrondi déclarée. La palette source renvoie des valeurs intermédiaires ; les gradients bruts n’ont pas été lus. Aucun seuil élargi après les pixels, aucune parité déduite de ce contrôle. [Refus exploitable](../firepit-mip-palette-refusal-v1/RESULT.v1.json).\n'
  key+=':'+mip_refusal['status']
 if sampling_quality:
  data['sampling_quality_causal_control']=sampling_quality['status']
  text+='\n**Cause mesurée — qualité du projet de capture Unity : Ultra / ForceEnable.** Les métadonnées des textures déclarent une anisotropie à 1, mais le réglage global du contexte de capture modifie le sampling effectif. En désactivant uniquement ce réglage dans un contrôle privé, 20/20 comparaisons moyennes passent ; normales modèle 3,805° → 0,416°. Les références principales et les sources restent inchangées. Ce contrôle ne remplace pas la référence originale. [Diagnostic](../firepit-sampling-quality-diagnostic-v1/RESULT.v1.json).\n'
  key+=':'+sampling_quality['status']
 if sampling_policy:
  data['source_sampling_policy_private_gpu']=sampling_policy['status']
  data['current_work_sub_lot']='L01.MATERIAUX.SAMPLER.COMMUN'
  data['current_work_state']='Sampling source reproduit sur GPU privé : 20/20 contrôles moyens ; intégration au chargement commun restante'
  data['next_action']='Admettre un contexte de sampling de scène explicite dans le chargement commun ; préserver métadonnées des textures et identités de cache ; puis recomparer la scène complète.'
  data['state']=sampling_policy['status']
  data['complete_assets_delivered_increment']=0
  data['current_execution']={'sub_lot':data['current_work_sub_lot'],'state':sampling_policy['status'],'measurement_scope':'5 propriétés × 2 masques × 2 vues ; correctif GPU privé, aucun compteur d’assets','surface_mean_comparisons_verified':20,'surface_mean_comparisons_expected':20,'views_verified':2,'original_texture_images_verified':3,'maximum_local_channel_error':sampling_policy['maximum_local_channel_error'],'common_renderer_installation_verified':False,'receipt':str(W/'firepit-source-sampling-policy-technical-v1/RESULT.v1.json'),'full_import':False}
  text=text.replace('| Gradients UV et choix implicite des mip | prochain sous-lot | modèle original ; aucun ajustement de texture ni seuil | à produire |','| Gradients UV et choix implicite des mip | diagnostic effectué ; cause qualité mesurée | protocole palette refusé ; gradients bruts non lus ; contexte global à reproduire | [Diagnostic](../firepit-sampling-quality-diagnostic-v1/RESULT.v1.json) |')
  text=text.replace('| L01.LOD.01 |','| L01.MATERIAUX.ANISOTROPIE.GPU | Reproduire le sampling effectif de la référence | correctif GPU privé vérifié | 20/20 contrôles moyens ; références originales conservées | [Clôture privée](../firepit-source-sampling-policy-technical-v1/RESULT.v1.json) |\n| L01.MATERIAUX.SAMPLER.COMMUN | Contexte de sampling et cache commun | à faire | contexte explicite ; métadonnées préservées ; scène complète à recomparer | à produire |\n| L01.LOD.01 |')
  text+='\n**Sampling natif corrigé — 20/20 contrôles moyens passent contre les références Unity originales.** Anisotropie 9 et interpolation entre mip conformément au réglage ForceEnable D3D11 ; trois images BC d’origine, douze mip chacune, texels et formats inchangés. Couleur modèle : 0,01044 → 0,000879 ; normales : 3,805° → 0,465° ; AO : 0,02069 → 0,001372. Les écarts locaux jusqu’à 0,73682 restent conservés ; la fidélité locale et la scène complète ne sont pas qualifiées. Aucun shader ni géométrie ajusté pour ce contrôle. Le réglage est un contexte de scène mesuré, pas une réécriture des métadonnées des assets. Installation commune, éclairage, ombres et recette éditeur restent ouverts. [Preuve GPU privée](../firepit-source-sampling-policy-technical-v1/RESULT.v1.json) · [Règle Unity du filtre effectif](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Texture-anisoLevel.html). Aucun asset complet supplémentaire.\n'
  key+=':'+sampling_policy['status']
 common_cap='firepit-surface-common-sampling-v2' if (W/'firepit-surface-common-sampling-v2/RUN-RESULT.v1.json').exists() else 'firepit-surface-common-sampling-v1'
 common_sampling=read(W/common_cap/'RUN-RESULT.v1.json')
 if common_sampling:
  data['current_work_sub_lot']='L01.MATERIAUX.SAMPLER.COMMUN'
  data['current_work_state']='Contexte intégré au chargeur commun ; compilation et contrôle GPU en cours'
  if common_sampling.get('actual_exit_code') not in [None,0] or common_sampling.get('error'):
   data['current_work_state']='Contrôle du sampling commun refusé ; diagnostic et réparation en cours'
  elif common_sampling.get('actual_exit_code')==0:
   data['current_work_state']='Exécution native commune terminée ; réouverture et comparaison à vérifier'
  data['state']=common_sampling['status']
  data['current_execution']={'sub_lot':data['current_work_sub_lot'],'state':common_sampling['status'],'receipt':str(W/common_cap/'RUN-RESULT.v1.json'),'full_import':False,'complete_assets_delivered_increment':0}
  data['next_action']='Réouvrir les pixels du chemin commun contre les références originales, puis recomparer la scène complète et ses ombres.'
  text=text.replace('| Contexte de sampling et cache commun | à faire | contexte explicite ; métadonnées préservées ; scène complète à recomparer | à produire |','| Contexte de sampling et cache commun | '+data['current_work_state']+' | contexte explicite ; images par scène ; refus GPU et changement de politique | [Exécution](../'+common_cap+'/RUN-RESULT.v1.json) |')
  text+='\n**Sampling commun : contexte explicite raccordé au montage de scène Studio.** Le réglage reste distinct des métadonnées source. Un adaptateur absent ou incompatible est refusé ; un changement de contexte après montage masque la scène et impose son remontage. La compilation et le contrôle GPU sont suivis dans le reçu ; aucun verdict de fidélité ni asset livré avant leur réouverture. [Exécution](../firepit-surface-common-sampling-v1/RUN-RESULT.v1.json).\n'
  key+=':'+common_sampling['status']
  if common_cap.endswith('v2'):
   text=text.replace('[Exécution](../firepit-surface-common-sampling-v1/RUN-RESULT.v1.json)','[Exécution](../'+common_cap+'/RUN-RESULT.v1.json)')
   text+='\nLe premier programme de contrôle a refusé la compilation pour une sérialisation JSON mal échappée ; son reçu v1 est conservé. Le contrôle corrigé v2 réutilise le moteur compilé. [Refus conservé](../firepit-surface-common-sampling-v1/RUN-RESULT.v1.json).\n'
 common_close=read(W/'source-scene-common-sampling-technical-v2/RESULT.v1.json')
 if common_close:
  data['common_native_sampling_verified']=common_close['common_native_sampling_verified']
  data['current_work_state']='Sampling commun natif vérifié : 20/20 contrôles moyens ; scène complète restante'
  data['state']=common_close['status']
  data['current_execution']={'sub_lot':'L01.MATERIAUX.SAMPLER.COMMUN','state':common_close['status'],'surface_mean_comparisons_verified':20,'surface_mean_comparisons_expected':20,'receipt':str(W/'source-scene-common-sampling-technical-v2/RESULT.v1.json'),'full_import':False,'complete_assets_delivered_increment':0}
  text+='\n**L01.MATERIAUX.SAMPLER.COMMUN — contrôle natif vérifié : 20/20 comparaisons moyennes.** Montage commun Studio effectivement exécuté ; trois textures originales, douze mip chacune, réglages source conservés. L’absence d’adaptateur mesuré est refusée ; un changement de contexte masque le host. Images propres à chaque scène, shaders partagés. Les écarts locaux jusqu’à 0,73682, la compilation Web, le rendu complet et la recette éditeur restent ouverts. [Clôture technique native](../source-scene-common-sampling-technical-v2/RESULT.v1.json). Aucun asset complet supplémentaire.\n'
  key+=':'+common_close['status']
 scene_sampling=read(W/'firepit-scene-common-sampling-v1/RUN-RESULT.v1.json')
 if scene_sampling:
  data['current_work_sub_lot']='L01.OMBRES.02.SCENE.SAMPLING'
  data['current_work_state']='Sampling commun vérifié ; capture du rendu complet avec/sans ombre en cours'
  if scene_sampling.get('actual_exit_code') not in [None,0] or scene_sampling.get('error'):
   data['current_work_state']='Capture de scène refusée ; diagnostic à résoudre'
  elif scene_sampling.get('actual_exit_code')==0:
   data['current_work_state']='Scène capturée ; comparaison aux références originales restante'
  data['state']=scene_sampling['status']
  data['current_execution']={'sub_lot':data['current_work_sub_lot'],'state':scene_sampling['status'],'receipt':str(W/'firepit-scene-common-sampling-v1/RUN-RESULT.v1.json'),'full_import':False,'complete_assets_delivered_increment':0}
  text=text.replace('| L01.LOD.01 |','| L01.OMBRES.02.SCENE.SAMPLING | Rendu complet avec le sampling commun | '+data['current_work_state']+' | deux vues ; seuils originaux fixés avant pixels | [Exécution](../firepit-scene-common-sampling-v1/RUN-RESULT.v1.json) |\n| L01.LOD.01 |')
  key+=':'+scene_sampling['status']
 scene_sampling_comparison=read(W/'firepit-scene-common-sampling-comparison-v1/RESULT.v1.json')
 if scene_sampling_comparison:
  data['scene_common_sampling_comparison']=scene_sampling_comparison['status']
  data['current_work_state']='Sampling corrigé ; fidélité de scène refusée, filtre et cascades d’ombres à résoudre'
  data['state']=scene_sampling_comparison['status']
  text+='\n**Rendu complet après sampling commun — refus conservé.** Deux images GPU réouvertes ; erreurs moyennes 0,026906 / 0,023489 ; IoU du masque 0,192536. Le réglage de texture réduit fortement les erreurs de surface, mais ne ferme pas les écarts de scène et d’ombres. Les seuils initiaux restent inchangés. [Comparaison](../firepit-scene-common-sampling-comparison-v1/RESULT.v1.json).\n'
  key+=':'+scene_sampling_comparison['status']
 ssao_shadow=read(W/'firepit-source-shadow-ssao-control-reopen-v1/RESULT.v1.json')
 if ssao_shadow:
  data['shadow_ssao_causal_control']=ssao_shadow['status']
  data['current_work_sub_lot']='L01.OMBRES.FILTRE.CASCADES.SOURCE'
  data['current_work_state']='SSAO écartée comme cause principale ; données GPU du filtre et des cascades source en capture'
  data['next_action']='Lire les cascades effectives et le filtre directionnel HDRP, puis appliquer une politique commune explicite et recomparer sans changer les seuils.'
  text=text.replace('| L01.LOD.01 |','| L01.OMBRES.SSAO.CONTROLE | Isoler la variation d’occlusion source | diagnostic vérifié | 61 602 pixels avec SSAO / 60 181 sans ; références originales identiques | [Diagnostic](../firepit-source-shadow-ssao-control-reopen-v1/RESULT.v1.json) |\n| L01.OMBRES.FILTRE.CASCADES.SOURCE | Données GPU effectives des ombres | capture en cours | matrices, tailles, biais, sphères et filtre ; pas de réglage ajusté aux images | [Exécution](../firepit-source-shadow-gpu-v1/RUN-RESULT.v1.json) |\n| L01.LOD.01 |')
  text+='\n**Contrôle causal SSAO — les deux références source originales sont identiques, 24 dépendances conservées.** Une répétition avec SSAO couvre 61 602 pixels du masque, sa désactivation 60 181 ; 2 683 pixels disparaissent et 1 262 apparaissent. Cela ne suffit pas à expliquer l’écart à Forge (11 881 pixels). Le filtre source directionnel Medium correspond au PCF tent 7×7 dans le SDK ; les cascades GPU sont à relever avant intégration. [Contrôle causal](../firepit-source-shadow-ssao-control-reopen-v1/RESULT.v1.json).\n'
  key+=':'+ssao_shadow['status']
 all_lights=read(W/'firepit-source-all-lights-gpu-reopen-v2/RESULT.v1.json')
 if all_lights:
  data['all_source_lights_gpu_verified']=all_lights['status']
  data['current_work_sub_lot']='L01.LUMIERES.PONCTUELLES.INTEGRATION'
  data['current_work_state']='Lumière ponctuelle source manquante identifiée ; raccordement de scène à vérifier'
  data['state']=all_lights['status']
  data['next_action']=all_lights['next_action']
  data['current_execution']={'sub_lot':data['current_work_sub_lot'],'state':all_lights['status'],'source_punctual_lights_verified':1,'source_directional_cascades_verified':4,'source_punctual_shadow_faces_verified':6,'native_punctual_gpu_count_verified':False,'receipt':str(W/'firepit-source-all-lights-gpu-reopen-v2/RESULT.v1.json'),'full_import':False,'complete_assets_delivered_increment':0}
  text=text.replace('| Données GPU effectives des ombres | capture en cours | matrices, tailles, biais, sphères et filtre ; pas de réglage ajusté aux images | [Exécution](../firepit-source-shadow-gpu-v1/RUN-RESULT.v1.json) |','| Données GPU effectives des ombres | source vérifiée | 4 cascades directionnelles + 6 faces ponctuelles ; 10 records GPU | [Preuve](../firepit-source-all-lights-gpu-reopen-v2/RESULT.v1.json) |')
  text=text.replace('| L01.LOD.01 |','| L01.LUMIERES.PONCTUELLES.INTEGRATION | Conserver et rendre les lumières du prefab | prochain sous-lot | lumière source, contrat de scène, lumière directe et routes d’ombres | [Manque identifié](../firepit-source-all-lights-gpu-reopen-v2/RESULT.v1.json) |\n| L01.LOD.01 |')
  text+='\n**Manque identifié dans le cas de scène — lumière ponctuelle du prefab.** Un record source de 224 octets / 56 slots vérifié sur deux vues, position monde (0 ; 0,33 ; 0), portée 1 m, couleur linéaire mesurée (19,8944 ; 6,40353 ; 0). Les 10 records d’ombre couvrent 4 cascades directionnelles et 6 faces ponctuelles. Le centre de l’objet utilise la quatrième cascade source, en 512×512. Le cas Forge construit seulement une lumière directionnelle ; le shader du récepteur possède déjà les boucles ponctuelles ; le diagnostic v1 sur leur absence était incorrect et est remplacé par v2. C’est une preuve de manque dans la définition du cas, pas une lecture du nombre de lumières GPU Forge. Prochain sous-lot : raccordement commun des lumières du prefab, lumière directe et ombres, puis nouvelle comparaison. [Preuve et limites](../firepit-source-all-lights-gpu-reopen-v2/RESULT.v1.json). Aucun asset complet livré.\n'
  key+=':'+all_lights['status']
 punctual_version=next((v for v in [2,1] if (W/f'firepit-punctual-scene-v{v}').exists()),None)
 if punctual_version:
  receipt=W/f'firepit-punctual-scene-v{punctual_version}/RUN-RESULT.v1.json'
  punctual=read(receipt)
  data['current_work_sub_lot']='L01.LUMIERES.PONCTUELLES.INTEGRATION'
  data['current_work_state']='Montage de la lumière source : contrôle natif en cours'
  if punctual.get('actual_exit_code')==0:data['current_work_state']='Montage exécuté ; comparaison et qualification restantes'
  elif punctual.get('actual_exit_code') is not None:data['current_work_state']='Montage refusé ; admission de hiérarchie à corriger'
  data['current_execution']={'sub_lot':data['current_work_sub_lot'],'state':punctual.get('status','running'),'receipt':str(receipt),'full_import':False,'complete_assets_delivered_increment':0}
  text+='\n**Montage ponctuel — '+data['current_work_state']+'.** Le premier essai est conservé : le lecteur de hiérarchie refusait KHR_lights_punctual avant le montage. Le second ajoute une admission limitée aux références existantes, en conservant le refus des extensions inconnues. Géométrie et shaders du dérivé inchangés. [Exécution](../firepit-punctual-scene-v'+str(punctual_version)+'/RUN-RESULT.v1.json). Aucun asset complet supplémentaire.\n'
  key+=':punctual-v'+str(punctual_version)+':'+punctual.get('status','running')
 punctual_comparison=read(W/'firepit-punctual-scene-comparison-v2/RESULT.v1.json')
 if punctual_comparison:
  data['punctual_scene_comparison']=punctual_comparison['status']
  data['current_work_state']='Lumière montée et scène capturée ; fidélité des ombres et couleur refusée'
  data['next_action']='Relever les cascades et texels d’ombre réellement consommés par Forge ; rapprocher les matrices et le filtre source avant une nouvelle comparaison.'
  data['current_execution'].update(shadow_mask_iou=punctual_comparison['shadow_mask_iou'],comparison_receipt=str(W/'firepit-punctual-scene-comparison-v2/RESULT.v1.json'))
  text+='\n**Lumière source montée — 9 nodes, 4 primitives et 4 instances matériau ; deux images GPU réouvertes.** Position, photométrie, métadonnées et libération avec le host contrôlées. IoU ombre : 0,192536 → 0,230625, sous le seuil de 0,95 ; erreurs couleur 0,026877 / 0,023511, sous-lot de fidélité refusé. Six pipelines prêts. Le nombre ponctuel CPU est vérifié, pas un readback du compteur GPU. [Comparaison et refus](../firepit-punctual-scene-comparison-v2/RESULT.v1.json). Aucun asset complet supplémentaire.\n'
  key+=':'+punctual_comparison['status']+':punctual-scene-v2'
 punctual_tests=read(W/'source-punctual-light-contract-check-v1/RUN-RESULT.v1.json')
 if punctual_tests:
  data['punctual_light_contract_tests_verified']=punctual_tests.get('tests_verified',0)
  text+='\nTests du contrat lumière et hiérarchie : '+str(punctual_tests.get('tests_verified',0))+'/7 vérifiés ; état `'+punctual_tests['status']+'`. [Reçu](../source-punctual-light-contract-check-v1/RUN-RESULT.v1.json).\n'
  key+=':'+punctual_tests['status']
 punctual_close=read(W/'source-punctual-light-technical-v1/RESULT.v1.json')
 if punctual_close:
  data['punctual_light_technical_closure']=punctual_close['status']
  data['current_work_sub_lot']='L01.OMBRES.CASCADES.CIBLE'
  data['current_work_state']='Montage lumière clos techniquement ; relevé des cascades Forge à réaliser'
  text=text.replace('| Conserver et rendre les lumières du prefab | prochain sous-lot | lumière source, contrat de scène, lumière directe et routes d’ombres | [Manque identifié](../firepit-source-all-lights-gpu-reopen-v2/RESULT.v1.json) |','| Conserver et rendre les lumières du prefab | montage technique vérifié ; fidélité ouverte | 1 lumière ; 7 tests ; 2 captures ; Web vérifié | [Clôture limitée](../source-punctual-light-technical-v1/RESULT.v1.json) |')
  text=text.replace('| L01.LOD.01 |','| L01.OMBRES.CASCADES.CIBLE | Relever les cascades Forge effectives | à réaliser | bounds, matrices, texels et filtre ; rapprochement du packet source | à produire |\n| L01.LOD.01 |')
  text+='\n**L01.LUMIERES.PONCTUELLES.INTEGRATION — montage technique clos.** Capture native et cycle de vie, 7/7 tests de contrat et compilation Web sur 1 052 sources inchangées. Fidélité couleur/ombre et recette éditeur restent ouvertes. [Clôture et limites](../source-punctual-light-technical-v1/RESULT.v1.json). Prochaine tranche : L01.OMBRES.CASCADES.CIBLE.\n'
  key+=':'+punctual_close['status']
 cascade_cpu=read(W/'firepit-shadow-cascades-native-reopen-v1/RESULT.v1.json')
 if cascade_cpu:
  data['native_shadow_cascade_cpu_diagnostic']=cascade_cpu['status']
  data['current_work_sub_lot']='L01.OMBRES.CASCADES.CIBLE'
  data['current_work_state']='Cascades CPU relevées ; confirmation du buffer GPU natif en cours'
  data['next_action']=cascade_cpu['next_action']
  text=text.replace('| Relever les cascades Forge effectives | à réaliser | bounds, matrices, texels et filtre ; rapprochement du packet source | à produire |','| Relever les cascades Forge effectives | CPU vérifié ; readback GPU en cours | 2 vues ; pixels originaux identiques ; contexte source différent | [Diagnostic](../firepit-shadow-cascades-native-reopen-v1/RESULT.v1.json) |')
  text+='\n**Cascades Forge relevées sans changer les pixels — deux captures identiques octet pour octet.** Le modèle utilise la première cascade native, résolution 2 048 et texel 0,005371 m ; Unity utilise la quatrième, résolution 512 et texel 0,023319 m. Ce diagnostic établit un contexte différent, sans prouver une correction causale de la fidélité. [Preuve CPU et limites](../firepit-shadow-cascades-native-reopen-v1/RESULT.v1.json).\n'
  cascade_version=next((v for v in [4,3,2] if (W/f'firepit-shadow-cascades-native-v{v}').exists()),None)
  if cascade_version:
   receipt=W/f'firepit-shadow-cascades-native-v{cascade_version}/RUN-RESULT.v1.json';cascade_run=read(receipt)
   data['current_execution']={'sub_lot':data['current_work_sub_lot'],'state':cascade_run.get('status','running'),'receipt':str(receipt),'full_import':False,'complete_assets_delivered_increment':0}
   text+='\nConfirmation par copie du buffer GPU réel : essai '+str(cascade_version)+', état `'+cascade_run.get('status','running')+'`. Les refus de compilation et d’initialisation précédents sont conservés. [Exécution](../firepit-shadow-cascades-native-v'+str(cascade_version)+'/RUN-RESULT.v1.json).\n'
   key+=':native-cascade-v'+str(cascade_version)+':'+cascade_run.get('status','running')
  key+=':'+cascade_cpu['status']
 cascade_gpu=read(W/'firepit-shadow-cascades-gpu-reopen-v1/RESULT.v1.json')
 if cascade_gpu:
  data['native_shadow_cascade_gpu_verified']=cascade_gpu['status']
  data['current_work_sub_lot']='L01.OMBRES.CONTEXTE.SOURCE'
  data['current_work_state']='Relevé des cascades clos : 144 scalaires GPU vérifiés ; contexte d’ombres source à raccorder'
  data['next_action']=cascade_gpu['next_action']
  data['current_execution']={'sub_lot':'L01.OMBRES.CASCADES.CIBLE','state':cascade_gpu['status'],'receipt':str(W/'firepit-shadow-cascades-gpu-reopen-v1/RESULT.v1.json'),'gpu_scalars_verified':144,'full_import':False,'complete_assets_delivered_increment':0}
  text=text.replace('| Relever les cascades Forge effectives | CPU vérifié ; readback GPU en cours | 2 vues ; pixels originaux identiques ; contexte source différent | [Diagnostic](../firepit-shadow-cascades-native-reopen-v1/RESULT.v1.json) |','| Relever les cascades Forge effectives | CPU et buffer GPU vérifiés | 144 scalaires / 2 vues ; pixels originaux identiques | [Preuve GPU](../firepit-shadow-cascades-gpu-reopen-v1/RESULT.v1.json) |')
  text=text.replace('| L01.LOD.01 |','| L01.OMBRES.CONTEXTE.SOURCE | Raccorder le contexte d’ombres mesuré | à faire | matrices, cascades, résolution, biais et filtre source ; seuils originaux | à produire |\n| L01.LOD.01 |')
  text+='\n**L01.OMBRES.CASCADES.CIBLE — relevé technique clos : 144 scalaires du buffer GPU réouverts bit à bit.** Quatre matrices, texels et bornes sur deux vues ; 877 épingles source inchangées. Les deux images restent identiques octet pour octet. La sélection est calculée à partir des bornes GPU et de la profondeur CPU ; l’index choisi par le shader n’a pas été lu. Le contexte diffère de Unity ; raccordement et fidélité restent ouverts. [Preuve et limites](../firepit-shadow-cascades-gpu-reopen-v1/RESULT.v1.json). Aucun asset complet supplémentaire.\n'
  key+=':'+cascade_gpu['status']
 source_shadow_context=read(W/'firepit-source-shadow-context-case-v1/RESULT.v1.json')
 if source_shadow_context:
  data['current_work_sub_lot']='L01.OMBRES.CONTEXTE.SOURCE'
  data['current_work_state']='Bornes et résolution source épinglées ; replay natif en cours'
  context=source_shadow_context['source_shadow_context_replay']
  data['next_action']='Vérifier les nouvelles bornes et la résolution dans le buffer GPU puis comparer les références originales ; traiter matrices, filtre, biais et bordures encore non équivalents.'
  context_run=read(W/'firepit-source-shadow-context-native-v1/RUN-RESULT.v1.json')
  data['current_execution']={'sub_lot':data['current_work_sub_lot'],'state':context_run.get('status','prepared'),'receipt':str(W/'firepit-source-shadow-context-native-v1/RUN-RESULT.v1.json'),'full_import':False,'complete_assets_delivered_increment':0}
  text=text.replace('| Raccorder le contexte d’ombres mesuré | à faire | matrices, cascades, résolution, biais et filtre source ; seuils originaux | à produire |','| Raccorder le contexte d’ombres mesuré | replay privé en cours | bornes 0,5 / 1,5 / 3 / 10 m ; résolution 512 ; filtre et biais non équivalents | [Cas épinglé](../firepit-source-shadow-context-case-v1/RESULT.v1.json) |')
  text+='\n**Distance source relevée : 10 m ; splits effectifs 0,05 / 0,15 / 0,30.** Capture Unity terminée, 24 dépendances et copies privées conservées ; les deux images de référence restent identiques. Replay privé préparé avec les bornes 0,5 / 1,5 / 3 / 10 m et la résolution directionnelle 512. Matrices, filtre Gaussian, biais et bordures ne sont pas déclarés équivalents à HDRP. [Capture de culling](../firepit-source-shadow-culling-v1/RUN-RESULT.v1.json) · [Exécution du replay](../firepit-source-shadow-context-native-v1/RUN-RESULT.v1.json).\n'
  key+=':source-shadow-context:'+context_run.get('status','prepared')
 source_context_close=read(W/'firepit-source-shadow-context-reopen-v1/RESULT.v1.json')
 if source_context_close:
  data['source_shadow_bounds_resolution_gpu_verified']=source_context_close['status']
  data['current_work_sub_lot']='L01.OMBRES.PROJECTION.FILTRE'
  data['current_work_state']='Bornes et résolution source appliquées sur GPU ; projection, filtre et biais non équivalents'
  data['next_action']=source_context_close['next_action']
  data['current_execution']={'sub_lot':'L01.OMBRES.CONTEXTE.SOURCE','state':source_context_close['status'],'receipt':str(W/'firepit-source-shadow-context-reopen-v1/RESULT.v1.json'),'gpu_scalars_verified':144,'shadow_mask_iou':source_context_close['shadow_mask_iou'],'full_import':False,'complete_assets_delivered_increment':0}
  text=text.replace('| Raccorder le contexte d’ombres mesuré | replay privé en cours | bornes 0,5 / 1,5 / 3 / 10 m ; résolution 512 ; filtre et biais non équivalents | [Cas épinglé](../firepit-source-shadow-context-case-v1/RESULT.v1.json) |','| Raccorder le contexte d’ombres mesuré | bornes/résolution vérifiées en replay privé ; fidélité refusée | 144 scalaires ; IoU 0,275561 ; autres paramètres non équivalents | [Preuve GPU](../firepit-source-shadow-context-reopen-v1/RESULT.v1.json) |')
  text=text.replace('| L01.LOD.01 |','| L01.OMBRES.PROJECTION.FILTRE | Rapprocher projection, filtre et biais | à réaliser | texel natif 0,017578 m / source 0,023319 m ; Gaussian / PCF tent 7×7 | à produire |\n| L01.LOD.01 |')
  text+='\n**Replay des bornes/résolution source — 144 scalaires GPU vérifiés, deux vues et 891 épingles source conservées.** Le modèle utilise désormais la quatrième cascade, résolution 512 ; texel natif 0,017578 m, source 0,023319 m. IoU ombre 0,230625 → 0,275561 ; erreur couleur avec ombre 0,026877 → 0,025908. Les seuils initiaux sont inchangés : fidélité refusée. Projection, filtre, biais et bordures source restent non équivalents ; raccordement commun non qualifié. [Preuve et limites](../firepit-source-shadow-context-reopen-v1/RESULT.v1.json) · [Comparaison](../firepit-source-shadow-context-comparison-v1/RESULT.v1.json). Aucun asset complet supplémentaire.\n'
  key+=':'+source_context_close['status']
 source_projection_case=read(W/'firepit-source-shadow-projection-case-v1/RESULT.v1.json')
 if source_projection_case:
  data['source_shadow_projection_reconstruction']=source_projection_case['status']
  data['current_work_sub_lot']='L01.OMBRES.PROJECTION.FILTRE'
  data['current_work_state']='Quatre projections source reconstruites ; transport natif en cours'
  projection_version=next((v for v in [2,1] if (W/f'firepit-source-shadow-projection-native-v{v}').exists()),1)
  projection_receipt=W/f'firepit-source-shadow-projection-native-v{projection_version}/RUN-RESULT.v1.json';projection_run=read(projection_receipt)
  data['current_execution']={'sub_lot':data['current_work_sub_lot'],'state':projection_run.get('status','prepared'),'receipt':str(projection_receipt),'full_import':False,'complete_assets_delivered_increment':0}
  data['next_action']='Vérifier le transport des projections source dans les uniforms du récepteur et les vues de shadow map, puis comparer les références et traiter le filtre PCF/biais.'
  text+='\n**Projection source reconstruite — quatre cascades, erreur inverse maximale 6,71×10⁻⁷.** Le SDK stocke transpose(inverse(viewProjection)) ; reconstruction depuis rotation/position/projection GPU et transport vers le repère Forge avec correction Y explicite. Correction d’interprétation : worldTexelSize HDRP mesure la diagonale, Bevy le côté ; le texel source comparable vaut 0,016489 m, le natif de la tranche précédente 0,017578 m. Les reçus précédents conservent les valeurs brutes et leur périmètre daté. Le premier replay a échoué sur le chemin du type Cascade ; le nouvel essai utilise son module public et conserve les mêmes données/seuils. [Reconstruction et conventions](../firepit-source-shadow-projection-case-v1/RESULT.v1.json) · [Replay](../firepit-source-shadow-projection-native-v'+str(projection_version)+'/RUN-RESULT.v1.json). Filtre et biais encore non équivalents ; aucun asset complet supplémentaire.\n'
  key+=':source-projection-v'+str(projection_version)+':'+projection_run.get('status','prepared')
 source_projection_close=read(W/'firepit-source-shadow-projection-reopen-v1/RESULT.v1.json')
 if source_projection_close:
  data['source_projection_transport_verified']=source_projection_close['status']
  data['current_work_sub_lot']='L01.OMBRES.PCF.BIAIS'
  data['current_work_state']='Transport des projections source vérifié ; filtre PCF 7×7 et biais source à traiter'
  data['next_action']=source_projection_close['next_action']
  data['current_execution']={'sub_lot':'L01.OMBRES.PROJECTION.FILTRE','state':source_projection_close['status'],'receipt':str(W/'firepit-source-shadow-projection-reopen-v1/RESULT.v1.json'),'receiver_gpu_scalars_verified':144,'caster_extracted_field_bits_verified':256,'shadow_mask_iou':source_projection_close['shadow_mask_iou'],'full_import':False,'complete_assets_delivered_increment':0}
  text=text.replace('| Rapprocher projection, filtre et biais | à réaliser | texel natif 0,017578 m / source 0,023319 m ; Gaussian / PCF tent 7×7 | à produire |','| Rapprocher projection, filtre et biais | projection transportée ; fidélité refusée | 144 scalaires GPU ; 256 champs de vues ; filtre/biais ouverts | [Preuve](../firepit-source-shadow-projection-reopen-v1/RESULT.v1.json) |')
  text=text.replace('| L01.LOD.01 |','| L01.OMBRES.PCF.BIAIS | Filtre PCF tent 7×7 et biais source | à réaliser | poids/UV des 16 taps ; biais normal/raster ; comparaison originale | à produire |\n| L01.LOD.01 |')
  text+='\n**Transport de projection source vérifié — 144 scalaires GPU du récepteur et 256 champs des vues extraites.** Deux vues et 899 épingles source inchangées. Les uniforms GPU des caméras de shadow map n’ont pas été copiés ; leurs champs extraits ont été comparés bit à bit. IoU ombre 0,275561 → 0,251648 : projection source seule moins proche avec le filtre/biais natifs conservés. Les deux résultats et seuils initiaux sont conservés ; aucun résultat sélectionné pour masquer ce recul. Filtre PCF tent 7×7, biais et bordures source restent ouverts. [Preuve et limites](../firepit-source-shadow-projection-reopen-v1/RESULT.v1.json) · [Comparaison](../firepit-source-shadow-projection-comparison-v2/RESULT.v1.json). Aucun asset complet supplémentaire.\n'
  key+=':'+source_projection_close['status']
 tent7=read(W/'source-shadow-tent7-reopen-v1/RESULT.v1.json')
 if tent7:
  data['source_shadow_tent7_math']=tent7['status']
  data['current_work_sub_lot']='L01.OMBRES.PCF.APPLICATION'
  data['current_work_state']='Calcul des poids/UV du filtre source vérifié sur GPU ; application au shadow sampler et biais restants'
  data['next_action']=tent7.get('next_action','Résoudre le refus du contrôle tent7 avant application.')
  data['current_execution']={'sub_lot':'L01.OMBRES.PCF.MATH','state':tent7['status'],'receipt':str(W/'source-shadow-tent7-reopen-v1/RESULT.v1.json'),'cases_verified':tent7.get('cases_verified',0),'full_import':False,'complete_assets_delivered_increment':0}
  text=text.replace('| L01.OMBRES.PCF.BIAIS | Filtre PCF tent 7×7 et biais source | à réaliser | poids/UV des 16 taps ; biais normal/raster ; comparaison originale | à produire |','| L01.OMBRES.PCF.MATH | Calcul du filtre PCF tent 7×7 | GPU source et Forge vérifiés | 25 phases × 16 taps × 2 vues ; 70 épingles ; références conservées | [Preuve](../source-shadow-tent7-reopen-v1/RESULT.v1.json) |\n| L01.OMBRES.PCF.APPLICATION | Application du filtre et biais source | à faire | comparaison depth, biais normal/raster et bordures ; scène originale | à produire |')
  text+='\n**L01.OMBRES.PCF.MATH — calcul GPU vérifié.** 25 phases × 16 taps × 2 vues ; écart maximal poids 3,7253e-9, UV exactement identiques, seuils 2e-7 fixés avant mesure. Coefficient Unity 0,081632 conservé sans renormalisation ; somme source environ 0,999984. 70 épingles et 24 dépendances source inchangées ; les deux images HDR originales sont identiques octet pour octet. Les premiers refus de compilation sont conservés. Application aux lectures depth, biais et rendu de scène restent ouverts ; aucun asset complet supplémentaire. [Preuve](../source-shadow-tent7-reopen-v1/RESULT.v1.json).\n'
  key+=':'+tent7['status']
 tent7_scene_version=next((v for v in [6,5,4,3,2,1] if (W/f'firepit-source-tent7-scene-native-v{v}/RUN-RESULT.v1.json').exists()),1)
 tent7_scene=read(W/f'firepit-source-tent7-scene-native-v{tent7_scene_version}/RUN-RESULT.v1.json')
 if tent7_scene:
  data['current_execution']={'sub_lot':'L01.OMBRES.PCF.APPLICATION','state':tent7_scene.get('status','running'),'receipt':str(W/f'firepit-source-tent7-scene-native-v{tent7_scene_version}/RUN-RESULT.v1.json'),'full_import':False,'complete_assets_delivered_increment':0}
  text+='\n**L01.OMBRES.PCF.APPLICATION — dérivé privé préparé ; essais refusés conservés ; imports corrigés, boucle de lectures et écritures de poids/UV développées à indices constants pour FXC.** Filtre source appliqué au modèle et au récepteur ; préfixe binaire original, nodes, meshes et accessors conservés. Biais natifs maintenus pour isoler le filtre ; biais source, frontières d’atlas et fidélité restent non qualifiés. État réel : `'+tent7_scene.get('status','running')+'`. [Exécution](../firepit-source-tent7-scene-native-v'+str(tent7_scene_version)+'/RUN-RESULT.v1.json).\n'
  key+=':tent7-scene-v'+str(tent7_scene_version)+':'+tent7_scene.get('status','running')
 bias_recipe=read(W/'firepit-source-shadow-bias-protocol-v1/PROTOCOL.v1.json')
 if bias_recipe:
  text+='\nTranche suivante : biais source épinglés — normal 0,75 × diagonale du texel, comparaison depth +1/65536, raster unités 1 et slope à transporter selon le format et le sens de profondeur. Application et qualification GPU non réalisées. [Protocole](../firepit-source-shadow-bias-protocol-v1/PROTOCOL.v1.json).\n'
 if (W/'source-scene-capture-binary-v1/BINARY.v1.json').exists():
  text+='\nProgramme de capture Rust conservé et réutilisé sur les dérivés de shader/données : même source Rust et dépendances vérifiées par SHA, exécutable privé épinglé ; compilation Rust non répétée. Les shaders restent compilés par le pilote à chaque essai. [Provenance du binaire](../source-scene-capture-binary-v1/BINARY.v1.json).\n'
 tent7_application=read(W/'firepit-source-tent7-scene-reopen-v1/RESULT.v1.json')
 if tent7_application:
  data['source_tent7_filter_application']=tent7_application['status']
  text=text.replace('| L01.OMBRES.PCF.APPLICATION | Application du filtre et biais source | à faire | comparaison depth, biais normal/raster et bordures ; scène originale | à produire |','| L01.OMBRES.PCF.APPLICATION | Application du filtre source | captures et géométrie vérifiées ; fidélité refusée | 6 pipelines ; 2 vues ; IoU 0,295544 ; biais natifs conservés | [Preuve](../firepit-source-tent7-scene-reopen-v1/RESULT.v1.json) |')
  text+='\n**Filtre tent 7×7 appliqué : six pipelines GPU prêts et deux captures réouvertes.** Poids et lectures développés à indices constants pour éviter le refus FXC ; expressions source conservées. Géométrie et préfixe binaire original inchangés ; 144 scalaires GPU de projection et 256 champs de vues extraites vérifiés. IoU ombre 0,251648 → 0,295544 ; fidélité toujours refusée selon les seuils originaux. [Preuve](../firepit-source-tent7-scene-reopen-v1/RESULT.v1.json).\n'
  key+=':'+tent7_application['status']
 receiver_bias=read(W/'firepit-source-tent7-receiver-bias-reopen-v1/RESULT.v1.json')
 if receiver_bias:
  data['current_work_sub_lot']='L01.OMBRES.RASTER.SOURCE'
  data['current_work_state']='Filtre et biais du récepteur appliqués en replay privé ; biais raster et fidélité restants'
  data['next_action']=receiver_bias['next_action']
  data['current_execution']={'sub_lot':'L01.OMBRES.BIAIS.RECEPTEUR','state':receiver_bias['status'],'receipt':str(W/'firepit-source-tent7-receiver-bias-reopen-v1/RESULT.v1.json'),'shadow_mask_iou':receiver_bias['shadow_mask_iou'],'full_import':False,'complete_assets_delivered_increment':0}
  text=text.replace('| L01.LOD.01 |','| L01.OMBRES.BIAIS.RECEPTEUR | Biais normal et comparaison depth source | captures vérifiées ; fidélité refusée | normal 0,75 × diagonale ; depth +1/65536 ; IoU 0,366584 | [Preuve](../firepit-source-tent7-receiver-bias-reopen-v1/RESULT.v1.json) |\n| L01.OMBRES.RASTER.SOURCE | Format et biais de rasterisation source | à faire | format réel, unités et slope source ; contrôle GPU et scène | à produire |\n| L01.LOD.01 |')
  text+='\n**Biais du récepteur source appliqués — six pipelines et deux vues GPU.** IoU ombre 0,295544 → 0,366584 ; erreur couleur avec ombre 0,026015 → 0,023771. 925 épingles conservées ; géométrie originale et projections récepteur GPU réouvertes. Les constantes/expressions proviennent des données source ; les offsets numériques calculés dans le shader n’ont pas été relus directement. Biais raster, bordures/sélection de cascades, rendu Neutral GPU et SSAO restent non équivalents ; aucune fidélité complète ni livraison d’asset supplémentaire. [Preuve](../firepit-source-tent7-receiver-bias-reopen-v1/RESULT.v1.json) · [Comparaison](../firepit-source-tent7-receiver-bias-comparison-v1/RESULT.v1.json).\n'
  key+=':'+receiver_bias['status']
 raster_source=read(W/'firepit-source-shadow-raster-reopen-v2/RESULT.v1.json')
 if raster_source:
  data['current_work_sub_lot']='L01.OMBRES.RASTER.APPLICATION'
  data['current_work_state']='Atlas source D32 et quatre requêtes de pente 2,5 vérifiés ; 1 048 576 profondeurs actives GPU conservées ; transport natif restant'
  data['next_action']=raster_source['next_action']
  data['current_execution']={'sub_lot':'L01.OMBRES.RASTER.SOURCE','state':raster_source['status'],'receipt':str(W/'firepit-source-shadow-raster-reopen-v2/RESULT.v1.json'),'active_gpu_depth_samples_verified':1048576,'source_requests_verified':4,'full_import':False,'complete_assets_delivered_increment':0}
  text=text.replace('| L01.OMBRES.RASTER.SOURCE | Format et biais de rasterisation source | à faire | format réel, unités et slope source ; contrôle GPU et scène | à produire |','| L01.OMBRES.RASTER.SOURCE | Format et biais de rasterisation source | allocation et pixels actifs GPU vérifiés | D32_SFloat ; 4 requêtes pente 2,5 ; 1 048 576 valeurs ; vue off inactive | [Preuve](../firepit-source-shadow-raster-reopen-v2/RESULT.v1.json) |')
  text=text.replace('| L01.LOD.01 |','| L01.OMBRES.RASTER.APPLICATION | Transport raster et comparaison des profondeurs | à faire | politique orthographique explicite, clé de cache et format ; GPU natif puis images | à produire |\n| L01.LOD.01 |')
  text+='\n**L01.OMBRES.RASTER.SOURCE — référence source réouverte.** Une allocation créée correspond à l’atlas 1024 × 1024, D32_SFloat ; quatre requêtes actives donnent une pente de 2,5, résultat SDK de 0,5 × baseBias. 1 048 576 profondeurs actives vérifiées ; 4 118 texels non nuls sur les deux dernières cascades. Vue sans ombres : zéro requête, copie identique de l’allocation du frame précédent ; ce second buffer ne compte pas comme nouvelle shadow map. Les quatre fichiers PNG/HDR de référence sont identiques octet pour octet ; 59 épingles et 24 dépendances conservées. Le binding global de texture n’a pas été observé ; identité fondée sur l’allocation unique correspondant au descripteur. Transport raster natif et fidélité restent ouverts. [Preuve](../firepit-source-shadow-raster-reopen-v2/RESULT.v1.json). Aucun asset complet supplémentaire.\n'
  text+='\nLa conversion de signe du biais cible doit respecter la profondeur inversée : Unity la gère automatiquement, un backend natif doit traduire ce signe. Candidat à vérifier : constant −1, pente −2,5, clamp 0 ; il n’est pas déclaré appliqué ni qualifié. [Convention Unity](https://docs.unity3d.com/Manual/SL-PlatformDifferences.html).\n'
  key+=':'+raster_source['status']
 raster_contract=read(W/'source-shadow-raster-contract-check-v2/RUN-RESULT.v1.json')
 if raster_contract:
  verified=raster_contract.get('tests_verified',0)
  state=raster_contract['status']
  data['current_work_sub_lot']='L01.OMBRES.RASTER.APPLICATION'
  data['current_work_state']=f'Politique raster commune : {verified}/7 tests vérifiés ; atlas lu vide et fidélité refusée, copie synchronisée à vérifier'
  data['current_execution']={'sub_lot':'L01.OMBRES.RASTER.APPLICATION','state':state,'tests_expected':7,'tests_verified':verified,'receipt':str(W/'source-shadow-raster-contract-check-v2/RUN-RESULT.v1.json'),'full_import':False,'complete_assets_delivered_increment':0}
  data['next_action']='Terminer les 7 tests, puis capturer les profondeurs GPU natives et comparer aux quatre cascades Unity actives'
  text=text.replace('| L01.OMBRES.RASTER.APPLICATION | Transport raster et comparaison des profondeurs | à faire | politique orthographique explicite, clé de cache et format ; GPU natif puis images | à produire |',f'| L01.OMBRES.RASTER.APPLICATION | Transport raster et comparaison des profondeurs | {verified}/7 tests ; {state} | politique commune et refus atomiques ; profondeur GPU et images restantes | [Tests](../source-shadow-raster-contract-check-v2/RUN-RESULT.v1.json) |')
  text+='\nPolitique raster ajoutée à la clé de pipeline, limitée aux passes orthographiques d’ombre avec profondeur compatible. Les anciens reçus et le binaire conservé précèdent ce changement : une nouvelle compilation native et Web est requise. Aucun asset complet supplémentaire.\n'
  key+=':'+state+':'+str(verified)
  raster_native=read(W/'firepit-source-shadow-raster-native-v6/RUN-RESULT.v1.json')
  if raster_native:
   data['current_execution']['native_capture_status']=raster_native['status']
   data['current_execution']['native_capture_receipt']=str(W/'firepit-source-shadow-raster-native-v6/RUN-RESULT.v1.json')
   data['current_work_state']+= ' ; exécution native : '+raster_native['status']
   text+='\nCapture native du lot raster lancée : `'+raster_native['status']+'`. [Exécution](../firepit-source-shadow-raster-native-v6/RUN-RESULT.v1.json). La première version est conservée avec son refus de compilation (conflit de nom dans le témoin) ; La version 2 compile mais le contrôle GPU refuse un biais nul : la synchronisation recrée le proxy depuis le matériau source. La version 3 porte la politique sur les matériaux source et contrôle les pipelines actifs des phases ; la version 4 répète sans recompilation, avec protocole image préalable. La version 5 refuse des imports privés Bevy, corrigés en version 6 : copie dans le même encodeur GPU après la passe d’ombre. Le démarrage ne vaut pas validation GPU. [Critères de profondeur fixés avant mesure](../firepit-source-shadow-depth-protocol-v1/PROTOCOL.v1.json).\n'
   key+=':'+raster_native['status']
 raster_depth=read(W/'firepit-source-shadow-depth-comparison-v3/RESULT.v1.json')
 raster_image=read(W/'firepit-source-shadow-raster-comparison-v4/RESULT.v1.json')
 if raster_depth and raster_image:
  data['raster_depth_comparison']=raster_depth['status']
  data['raster_shadow_mask_iou']=raster_image['shadow_mask_iou']
  data['next_action']='Copier les profondeurs dans le même encodeur GPU après la passe d’ombre ; expliquer atlas lu vide et recul IoU 0,366584 vers 0,328282'
  text+='\n**Raster appliqué : descripteur actif vérifié, deux captures natives et répétition avec binaire conservé.** 1 048 576 profondeurs lues, toutes nulles côté Forge ; Unity comporte 4 118 texels non nuls. Comparaison refusée. IoU des images 0,366584 → 0,328282 ; fidélité refusée. Copie dans la passe GPU à vérifier ; aucune livraison complète supplémentaire. [Profondeurs](../firepit-source-shadow-depth-comparison-v3/RESULT.v1.json) · [Images](../firepit-source-shadow-raster-comparison-v4/RESULT.v1.json).\n'
  key+=':'+raster_depth['status']+':'+str(raster_image['shadow_mask_iou'])
 raster_reopen=read(W/'firepit-source-shadow-raster-replay-reopen-v1/RESULT.v1.json')
 if raster_reopen:
  data['raster_in_pass_gpu_depth_reopened']=raster_reopen['status']
  data['next_action']='Contrôle causal sans biais raster, puis vérifier géométrie/projection effective des casters si atlas toujours vide'
  data['current_work_sub_lot']='L01.OMBRES.RASTER.CONTROLE_ZERO'
  zero=read(W/'firepit-source-shadow-raster-zero-control-native-v1/RUN-RESULT.v1.json')
  data['current_work_state']='Copie GPU dans la passe vérifiée ; atlas toujours vide ; contrôle sans biais : '+zero.get('status','préparé')
  data['current_execution']={'sub_lot':'L01.OMBRES.RASTER.CONTROLE_ZERO','state':zero.get('status','préparé'),'receipt':str(W/'firepit-source-shadow-raster-zero-control-native-v1/RUN-RESULT.v1.json'),'tests_verified':7,'source_geometry_unchanged':True,'full_import':False,'complete_assets_delivered_increment':0}
  text=text.replace('| L01.LOD.01 |','| L01.OMBRES.RASTER.COPIE_GPU | Copie des profondeurs après la passe | technique vérifiée ; fidélité refusée | 1 048 576 profondeurs ; 917 épingles ; deux images identiques | [Preuve](../firepit-source-shadow-raster-replay-reopen-v1/RESULT.v1.json) |\n| L01.OMBRES.RASTER.CONTROLE_ZERO | Isoler le biais raster | '+zero.get('status','préparé')+' | même scène et shaders ; seule politique raster neutralisée pour diagnostic | [Exécution](../firepit-source-shadow-raster-zero-control-native-v1/RUN-RESULT.v1.json) |\n| L01.LOD.01 |')
  text+='\n**Copie synchronisée réouverte :** 917 épingles natives et 1 053 Web conservées ; deux images identiques à la copie externe ; atlas également vide. Le timing de cette lecture n’explique pas l’écart. Un contrôle privé à biais nul est lancé pour isoler la cause ; il n’est pas présenté comme un réglage source fidèle. [Preuve](../firepit-source-shadow-raster-replay-reopen-v1/RESULT.v1.json). Aucun asset complet supplémentaire.\n'
  key+=':'+raster_reopen['status']+':'+zero.get('status','préparé')
 zero_analysis=read(W/'firepit-source-shadow-raster-zero-control-reopen-v1/RESULT.v1.json')
 if zero_analysis:
  probe_control=read(W/'firepit-source-shadow-probe-control-native-v1/RUN-RESULT.v1.json')
  data['current_work_sub_lot']='L01.OMBRES.SONDE.CONTROLE'
  data['current_work_state']='Biais nul : quatre couches lues nulles, image IoU 0,366584 ; qualifier la sonde sur quatre profondeurs connues : '+probe_control.get('status','préparé')
  data['current_execution']={'sub_lot':'L01.OMBRES.SONDE.CONTROLE','state':probe_control.get('status','préparé'),'receipt':str(W/'firepit-source-shadow-probe-control-native-v1/RUN-RESULT.v1.json'),'expected_known_depth_layers':4,'expected_known_depth_samples':1048576,'full_import':False,'complete_assets_delivered_increment':0}
  data['next_action']='Vérifier la sonde D32 avec quatre couches à 0,25/0,5/0,75/1 sur le même device et compute pipeline ; ne pas qualifier les valeurs nulles comme atlas vide avant ce contrôle'
  text=text.replace('| L01.LOD.01 |','| L01.OMBRES.SONDE.CONTROLE | Qualifier la lecture GPU de profondeur | '+probe_control.get('status','préparé')+' | 4 couches connues ; même device et pipeline ; 1 048 576 valeurs attendues | [Exécution](../firepit-source-shadow-probe-control-native-v1/RUN-RESULT.v1.json) |\n| L01.LOD.01 |')
  text+='\n**Contrôle sans biais mesuré :** 2 097 152 profondeurs des deux essais, quatre couches lues nulles dans les deux ; la concordance image retrouve 0,366584. Cette différence impose un contrôle positif de la sonde avant d’interpréter les valeurs nulles comme un atlas réellement vide. [Contrôle causal](../firepit-source-shadow-raster-zero-control-reopen-v1/RESULT.v1.json) · [Images](../firepit-source-shadow-raster-zero-control-comparison-v1/RESULT.v1.json). Aucun asset complet livré.\n'
  key+=':'+zero_analysis['status']+':'+probe_control.get('status','préparé')
 depth_correction=read(W/'firepit-source-shadow-depth-readback-correction-v1/RESULT.v1.json')
 corrected_probe=read(W/'firepit-source-shadow-probe-control-reopen-v2/RESULT.v1.json')
 raster_current=read(W/'firepit-source-shadow-raster-replay-reopen-v2/RESULT.v1.json')
 if depth_correction and corrected_probe and raster_current:
  caster=read(W/'firepit-source-shadow-caster-view-gpu-native-v1/RUN-RESULT.v1.json')
  data['current_work_sub_lot']='L01.OMBRES.PROJECTION.CASTER_GPU'
  data['current_work_state']='Sonde quatre couches qualifiée ; atlas non vide, comparaison refusée ; lecture des matrices des casters : '+caster.get('status','préparé')
  data['current_execution']={'sub_lot':'L01.OMBRES.PROJECTION.CASTER_GPU','state':caster.get('status','préparé'),'receipt':str(W/'firepit-source-shadow-caster-view-gpu-native-v1/RUN-RESULT.v1.json'),'expected_gpu_scalars':896,'expected_cpu_gpu_scalars_compared':384,'full_import':False,'complete_assets_delivered_increment':0}
  data['native_atlas_empty_inference_valid']=False
  data['corrected_probe_samples_verified']=1048576
  data['corrected_depth_source_native_comparison']='shadow_depth_fixed_comparison_refused'
  data['next_action']='Lire les matrices du véritable buffer GPU des casters via les quatre offsets par vue, puis confronter aux projections source sans ajustement des pixels'
  warning='**Correction des mesures de profondeur :** les anciennes conclusions « atlas vide » sont invalidées. La requête de couches du shader ne lisait que la première couche sur ce chemin HLSL. Sonde corrigée et vérifiée sur 1 048 576 valeurs connues ; atlas Forge non vide. Les mesures des images restent valides. [Correction et reçus remplacés](../firepit-source-shadow-depth-readback-correction-v1/RESULT.v1.json).\n\n'
  text=text.replace('## Découpage principal',warning+'## Découpage principal',1)
  lines=text.splitlines()
  lines=[('| L01.OMBRES.RASTER.APPLICATION | Transport raster et comparaison des profondeurs | appliqué et réouvert ; fidélité refusée | 7 tests ; 932 épingles ; 4 couches GPU ; empreintes 0,948534 / 0,878423 | [Preuve](../firepit-source-shadow-raster-replay-reopen-v2/RESULT.v1.json) |') if line.startswith('| L01.OMBRES.RASTER.APPLICATION |') else ('| L01.OMBRES.SONDE.CONTROLE | Qualifier la lecture GPU de profondeur | technique clos | 4 couches connues ; 1 048 576 valeurs exactes ; images inchangées | [Preuve](../firepit-source-shadow-probe-control-reopen-v2/RESULT.v1.json) |') if line.startswith('| L01.OMBRES.SONDE.CONTROLE |') else line for line in lines]
  text='\n'.join(lines)+'\n'
  text=text.replace('| L01.LOD.01 |','| L01.OMBRES.PROJECTION.CASTER_GPU | Uniformes GPU effectifs des casters | '+caster.get('status','préparé')+' | 7 matrices × 4 cascades × 2 vues ; 384 scalaires CPU/GPU comparés | [Exécution](../firepit-source-shadow-caster-view-gpu-native-v1/RUN-RESULT.v1.json) |\n| L01.LOD.01 |')
  text+='\n**Sonde de profondeur corrigée et qualifiée.** Contrôle GPU 0,25/0,5/0,75/1 : 1 048 576 valeurs, zéro erreur ; deux images restent identiques. La traduction HLSL NumLayers lisait le nombre de mipmaps plutôt que les couches. Contrat exact de quatre couches vérifié côté hôte. Atlas Forge : 3 041 et 803 texels actifs ; source : 3 206 et 912. Empreintes 0,948534 et 0,878423, erreurs de profondeur encore hors seuil. Image IoU 0,328282 inchangée. Lecture réelle des matrices GPU des casters lancée. [Sonde](../firepit-source-shadow-probe-control-reopen-v2/RESULT.v1.json) · [Profondeurs corrigées](../firepit-source-shadow-depth-comparison-v6/RESULT.v1.json) · [Réouverture](../firepit-source-shadow-raster-replay-reopen-v2/RESULT.v1.json). Aucun asset complet supplémentaire.\n'
  key+=':'+corrected_probe['status']+':'+raster_current['status']+':'+caster.get('status','préparé')
 caster_reopen=read(W/'firepit-source-shadow-caster-view-gpu-reopen-v1/RESULT.v1.json')
 clip_source=read(W/'firepit-source-shadow-depth-clipping-source-v1/RESULT.v1.json')
 if caster_reopen and clip_source:
  raster_state=read(W/'firepit-source-shadow-raster-state-gpu-native-v1/RUN-RESULT.v1.json')
  data['current_work_sub_lot']='L01.OMBRES.RASTER.CLIPPING'
  data['current_work_state']='Matrices GPU des casters vérifiées ; clipping source désactivé sur quatre requêtes ; état et capacité GPU Forge : '+raster_state.get('status','préparé')
  data['current_execution']={'sub_lot':'L01.OMBRES.RASTER.CLIPPING','state':raster_state.get('status','préparé'),'receipt':str(W/'firepit-source-shadow-raster-state-gpu-native-v1/RUN-RESULT.v1.json'),'caster_gpu_scalars_read':896,'source_projection_gpu_scalars_bit_verified':384,'source_requests_depth_clip_disabled_verified':4,'full_import':False,'complete_assets_delivered_increment':0}
  data['caster_view_uniform_gpu_verified']=True
  data['next_action']='Vérifier unclipped_depth du pipeline actif et DEPTH_CLIP_CONTROL device/adapter, puis reproduire le clipping source avec une politique commune explicite et refus si capacité absente'
  lines=text.splitlines()
  lines=[('| L01.OMBRES.PROJECTION.CASTER_GPU | Uniformes GPU effectifs des casters | technique clos | 896 scalaires lus ; 384 comparés bit à bit ; deux images inchangées | [Preuve](../firepit-source-shadow-caster-view-gpu-reopen-v1/RESULT.v1.json) |') if line.startswith('| L01.OMBRES.PROJECTION.CASTER_GPU |') else line for line in lines]
  text='\n'.join(lines)+'\n'
  text=text.replace('| L01.LOD.01 |','| L01.OMBRES.RASTER.CLIPPING | Clipping de profondeur des ombres | source vérifiée ; contrôle Forge '+raster_state.get('status','préparé')+' | 4 requêtes source ZClip=false ; état natif et capacité GPU avant application | [Source](../firepit-source-shadow-depth-clipping-source-v1/RESULT.v1.json) |\n| L01.LOD.01 |')
  text+='\n**Matrices des casters lues sur le véritable buffer GPU :** 896 scalaires sur deux vues ; 384 scalaires de projection, pose et clip égaux bit à bit au plan source, deux images identiques et contrôle positif de profondeur réussi. 922 épingles natives conservées. Ce transport est clos techniquement ; géométrie rasterisée et fidélité restent ouvertes. Les flags effectifs des quatre requêtes Unity donnent ZClip=false ; contrôle du pipeline natif et de la capacité GPU lancé avant tout changement du clipping. [Matrices](../firepit-source-shadow-caster-view-gpu-reopen-v1/RESULT.v1.json) · [Clipping source](../firepit-source-shadow-depth-clipping-source-v1/RESULT.v1.json). Aucun asset complet supplémentaire.\n'
  key+=':'+caster_reopen['status']+':'+clip_source['status']+':'+raster_state.get('status','préparé')
 render_matrices=read(W/'firepit-source-shadow-render-matrices-reopen-v1/RESULT.v1.json')
 clip_match=read(W/'firepit-source-shadow-raster-state-gpu-reopen-v1/RESULT.v1.json')
 if render_matrices and clip_match:
  data['current_work_sub_lot']='L01.OMBRES.PROJECTION.RENDU_SOURCE'
  data['current_work_state']='Clipping natif/source identique ; matrices effectives de rendu Unity réouvertes sur quatre cascades ; comparaison dans le repère Forge restante'
  data['current_execution']={'sub_lot':data['current_work_sub_lot'],'state':render_matrices['status'],'receipt':str(W/'firepit-source-shadow-render-matrices-reopen-v1/RESULT.v1.json'),'source_dependencies_verified':24,'matrix_scalars_verified':384,'original_image_hdr_pairs_unchanged':8,'full_import':False,'complete_assets_delivered_increment':0}
  data['next_action']='Comparer les matrices de rendu effectives Unity aux matrices GPU Forge après transformation du repère déclaré, sans ajustement aux pixels'
  lines=text.splitlines()
  lines=[('| L01.OMBRES.RASTER.CLIPPING | Clipping de profondeur des ombres | technique clos | quatre requêtes source ZClip=false ; pipeline natif unclipped_depth=true ; capacité device/adapter vérifiée | [Preuve](../firepit-source-shadow-raster-state-gpu-reopen-v1/RESULT.v1.json) |') if line.startswith('| L01.OMBRES.RASTER.CLIPPING |') else line for line in lines]
  text='\n'.join(lines)+'\n'
  text=text.replace('| L01.LOD.01 |','| L01.OMBRES.PROJECTION.RENDU_SOURCE | Matrices utilisées pour rasteriser les ombres Unity | acquisition source vérifiée ; comparaison Forge restante | quatre cascades, 384 scalaires finis, 24 dépendances inchangées, références originales identiques | [Preuve](../firepit-source-shadow-render-matrices-reopen-v1/RESULT.v1.json) |\n| L01.LOD.01 |')
  text+='\n**Nouvelle tranche vérifiée :** clipping déjà équivalent ; matrices effectives de rendu Unity capturées et réouvertes sur quatre cascades. 107 empreintes source et privées inchangées ; huit comparaisons PNG/HDR aux références originales identiques. Les matrices GPU Forge précédemment vérifiées correspondaient au plan reconstruit depuis les données d’échantillonnage ; leur équivalence aux matrices effectives de rasterisation source reste à vérifier. Aucun asset complet supplémentaire.\n'
  key+=':'+clip_match['status']+':'+render_matrices['status']
 actual_matrix_match=read(W/'firepit-source-render-matrix-comparison-v1/RESULT.v1.json')
 if actual_matrix_match:
  data['current_work_sub_lot']='L01.OMBRES.CASTERS.GEOMETRIE'
  data['current_work_state']='Clipping et matrices effectives de rendu source vérifiés ; prochaine tranche : géométrie rasterisée, winding/cull et biais de profondeur'
  data['current_execution']={'sub_lot':data['current_work_sub_lot'],'state':'next_batch_manifest_to_prepare','last_verified_receipt':str(W/'firepit-source-render-matrix-comparison-v1/RESULT.v1.json'),'source_cascades_verified':4,'full_import':False,'complete_assets_delivered_increment':0}
  data['next_action']='Préparer le manifeste de comparaison des casters source/Forge : géométrie, winding/cull et biais de profondeur, puis vérifier les profondeurs sans ajustement aux pixels'
  lines=text.splitlines()
  lines=[('| L01.OMBRES.PROJECTION.RENDU_SOURCE | Matrices utilisées pour rasteriser les ombres Unity | technique clos dans le repère déclaré | quatre cascades ; 192 scalaires source/plan/GPU comparés bit à bit ; produit direct mesuré | [Preuve](../firepit-source-render-matrix-comparison-v1/RESULT.v1.json) |') if line.startswith('| L01.OMBRES.PROJECTION.RENDU_SOURCE |') else line for line in lines]
  text='\n'.join(lines)+'\n'
  text=text.replace('| L01.LOD.01 |','| L01.OMBRES.CASTERS.GEOMETRIE | Géométrie rasterisée, winding/cull et biais | prochaine tranche à préparer | mêmes quatre casters et références ; comparaison de profondeur sans ajustement aux pixels | à produire |\n| L01.LOD.01 |')
  text+='\n**Comparaison des matrices de rendu terminée :** quatre cascades, zéro différence des valeurs float32 de vue source/plan, projection source/native et clip GPU/plan déclaré. Le produit direct en double présente un écart maximal de 0,000001612 par rapport au buffer float32. Le repère relatif à la caméra existant est conservé. Clipping et projections ne sont pas des écarts identifiés ; prochaine tranche : géométrie rasterisée, winding/cull et biais. Fidélité globale toujours refusée ; aucun asset complet supplémentaire. [Preuve](../firepit-source-render-matrix-comparison-v1/RESULT.v1.json).\n'
  key+=':'+actual_matrix_match['status']
 caster_geometry=read(W/'firepit-source-caster-geometry-reopen-v1/RESULT.v1.json')
 if caster_geometry:
  data['current_work_sub_lot']='L01.OMBRES.CASTERS.VERTEX_GPU'
  data['current_work_state']='Géométrie statique vérifiée : quatre LOD, 7 462 sommets et 5 285 triangles identiques après conversion ; sorties vertex GPU et profondeur restantes'
  data['current_execution']={'sub_lot':data['current_work_sub_lot'],'state':'gpu_vertex_capture_to_prepare','last_verified_receipt':str(W/'firepit-source-caster-geometry-reopen-v1/RESULT.v1.json'),'mesh_levels_verified':4,'distinct_vertices_verified':7462,'distinct_triangles_verified':5285,'full_import':False,'complete_assets_delivered_increment':0}
  data['next_action']='Lire les transformations et sorties vertex GPU effectives des casters ; vérifier la précision du repère et la profondeur écrite sans ajustement aux pixels'
  lines=text.splitlines()
  lines=[('| L01.OMBRES.CASTERS.GEOMETRIE | Géométrie statique et états de face source | technique clos pour les buffers statiques | quatre LOD, 7 462 sommets et 5 285 triangles ; zéro différence de positions/normales/winding ; sources et images inchangées | [Preuve](../firepit-source-caster-geometry-reopen-v1/RESULT.v1.json) |') if line.startswith('| L01.OMBRES.CASTERS.GEOMETRIE |') else line for line in lines]
  text='\n'.join(lines)+'\n'
  text=text.replace('| L01.LOD.01 |','| L01.OMBRES.CASTERS.VERTEX_GPU | Sorties GPU des casters et profondeur | prochaine tranche à préparer | transformation effective, précision du repère, rasterisation et biais ; aucun fitting | à produire |\n| L01.LOD.01 |')
  text+='\n**Tranche géométrie statique réouverte :** quatre niveaux de détail, 7 462 sommets et 5 285 triangles, positions/normales float32 et ordre inversé des faces exactement conformes au repère déclaré. Unity montre LOD0 visible et les autres niveaux invisibles dans les deux vues du diagnostic. Matériau : cull back, pas de depth offset ni alpha clip ; témoins avec/sans ombre conservés. 24 dépendances source inchangées, quatre paires PNG/HDR identiques. Le graphe vertex converti conserve la position objet ; ses sorties GPU effectives restent à mesurer. Aucun asset complet supplémentaire. [Preuve](../firepit-source-caster-geometry-reopen-v1/RESULT.v1.json).\n'
  key+=':'+caster_geometry['status']
 mesh_gpu=read(W/'firepit-source-shadow-mesh-matrix-reopen-v1/RESULT.v1.json')
 fixed_depth=read(W/'firepit-source-fixed-shadow-depth-causal-reopen-v1/RESULT.v1.json')
 if mesh_gpu and fixed_depth:
  data['current_work_sub_lot']='L01.OMBRES.CASTERS.POSITION_CLIP'
  data['current_work_state']='Matrice mesh réellement indexée par les casters GPU vérifiée ; témoin frag_depth sans effet ; positions clip et précision restantes'
  data['current_execution']={'sub_lot':data['current_work_sub_lot'],'state':'clip_vertex_probe_to_prepare','last_verified_receipt':str(W/'firepit-source-shadow-mesh-matrix-reopen-v1/RESULT.v1.json'),'gpu_matrix_components_verified':32,'gpu_pixel_observations_verified':3844,'depth_positive_control_samples':1048576,'full_import':False,'complete_assets_delivered_increment':0}
  data['next_action']='Comparer les positions clip effectives des sommets, puis la couverture des triangles ; conserver le protocole sans recalage aux pixels'
  lines=text.splitlines()
  lines=[('| L01.OMBRES.CASTERS.VERTEX_GPU | Matrice mesh effective dans la passe GPU | matrice vérifiée ; sorties vertex clip restantes | 32 composantes sur deux cascades ; 3 844 observations exactes ; contrôle positif 1 048 576 valeurs | [Preuve](../firepit-source-shadow-mesh-matrix-reopen-v1/RESULT.v1.json) |') if line.startswith('| L01.OMBRES.CASTERS.VERTEX_GPU |') else line for line in lines]
  text='\n'.join(lines)+'\n'
  text=text.replace('| L01.LOD.01 |','| L01.OMBRES.CASTERS.PROFONDEUR_FIXE | Isoler frag_depth explicite | témoin sans effet | deux buffers profondeur et deux images identiques au cas précédent ; fidélité toujours refusée | [Preuve](../firepit-source-fixed-shadow-depth-causal-reopen-v1/RESULT.v1.json) |\n| L01.OMBRES.CASTERS.POSITION_CLIP | Positions clip et couverture rasterisée | prochaine tranche à préparer | mêmes vertices et matrices ; précision du repère à vérifier | à produire |\n| L01.LOD.01 |')
  text+='\n**Matrice mesh des casters GPU vérifiée :** la sonde du shader lit le buffer effectivement indexé par la passe d’ombres. 32 composantes, 3 844 pixels, zéro écart numérique avec le transform attendu ; signe des zéros non conservé par cet encodage. Le contrôle positif de lecture vérifie 1 048 576 valeurs. Sonde privée avec biais neutralisés, pas une qualification du rendu final. Le témoin sans frag_depth explicite donne deux buffers profondeur et deux images strictement identiques au cas antérieur ; cette écriture n’explique pas l’écart. Géométrie et sources préservées. Positions clip effectives, précision et couverture restantes ; aucun asset complet supplémentaire.\n'
  key+=':'+mesh_gpu['status']+':'+fixed_depth['status']
 origin_gpu=read(W/'firepit-source-shadow-origin-reopen-v1/RESULT.v1.json')
 origin_depth=read(W/'firepit-source-shadow-origin-depth-comparison-v1/RESULT.v1.json')
 positive_depth=read(W/'firepit-source-shadow-raster-positive-depth-comparison-v1/RESULT.v1.json')
 if origin_gpu and origin_depth and positive_depth:
  data['current_work_sub_lot']='L01.OMBRES.CASTERS.POSITION_CLIP'
  data['current_work_state']='Contrôle origine réouvert : 896 scalaires GPU lus ; origine et signe positif du biais ne résolvent pas la fidélité ; positions clip/topologie restantes'
  data['current_execution']={'sub_lot':data['current_work_sub_lot'],'state':'origin_and_bias_controls_measured_fidelity_refused','last_verified_receipt':str(W/'firepit-source-shadow-origin-reopen-v1/RESULT.v1.json'),'gpu_scalars_read':896,'gpu_cpu_scalars_bit_compared':384,'origin_depth_comparison':origin_depth['status'],'positive_bias_comparison':positive_depth['status'],'full_import':False,'complete_assets_delivered_increment':0}
  data['next_action']='Vérifier les positions clip effectives et la topologie réellement rasterisée ; ne pas adopter un signe de biais ni un recentrage comme qualification sans preuve'
  text=text.replace('| L01.LOD.01 |','| L01.OMBRES.CASTERS.ORIGINE | Isoler la précision du repère Y=1024 | contrôle réouvert ; fidélité refusée | origine ramenée à zéro par transformation analytique ; 896 scalaires GPU, 926 épingles ; couverture 0,948534 / 0,879386 | [Preuve GPU](../firepit-source-shadow-origin-reopen-v1/RESULT.v1.json) · [Profondeur](../firepit-source-shadow-origin-depth-comparison-v1/RESULT.v1.json) |\n| L01.OMBRES.RASTER.SIGNE | Contrôle du signe numérique du biais | mesuré ; fidélité refusée | constantes +1/+2,5 contre replay -1/-2,5 ; mêmes géométrie et shaders ; couverture inchangée | [Preuve](../firepit-source-shadow-raster-positive-depth-comparison-v1/RESULT.v1.json) |\n| L01.LOD.01 |')
  text+='\n**Contrôles de précision et de biais terminés.** Scène recentrée à Y=0 avec transformation analytique des matrices source, sans recalage : 896 scalaires GPU lus, 384 CPU/GPU et 384 plan/GPU comparés bit à bit, 926 épingles inchangées. Profondeurs encore refusées : couverture 0,948534 / 0,879386, contre 0,948534 / 0,878423 à Y=1024. Le recentrage ne résout pas l’écart. Le signe positif déclaré numériquement par la commande source conserve la couverture précédente et reste hors seuil de profondeur ; aucune qualification du signe ni correction produit déduite. Contrôles positifs de lecture GPU réussis sur 1 048 576 valeurs pour les deux essais. Positions clip effectives et topologie rasterisée restantes ; aucun asset complet supplémentaire.\n'
  key+=':'+origin_gpu['status']+':'+origin_depth['status']+':'+positive_depth['status']
 triangle_coverage=read(W/'firepit-source-shadow-triangle-coverage-v1/RESULT.v1.json')
 face_depth=read(W/'firepit-source-shadow-front-face-control-depth-comparison-v1/RESULT.v1.json')
 face_image=read(W/'firepit-source-shadow-front-face-control-image-comparison-v1/RESULT.v1.json')
 if triangle_coverage and face_depth and face_image:
  data['current_work_sub_lot']='L01.OMBRES.RASTER.ORIENTATION_COMMUNE'
  data['current_work_state']='Sélection des faces identifiée : couverture GPU 3 206/912 identique source ; profondeur maximale 3,55e-6 reste hors seuil ; correction limitée aux ombres à intégrer'
  data['current_execution']={'sub_lot':data['current_work_sub_lot'],'state':'source_face_selection_causal_control_measured_product_fix_pending','receipt':str(W/'firepit-source-shadow-front-face-control-depth-comparison-v1/RESULT.v1.json'),'active_cascade_support_iou':[1.0,1.0],'active_cascade_depth_max_errors':[3.550201654434204e-6,1.125037670135498e-6],'visual_comparison':face_image['status'],'main_prepass_also_changed_in_private_control':True,'full_import':False,'complete_assets_delivered_increment':0}
  data['next_action']='Transporter explicitement l’orientation frontale dans la politique raster commune des seules ombres orthographiques ; préserver cull et prépasses principales ; vérifier cache, refus atomiques et GPU'
  text=text.replace('| L01.LOD.01 |','| L01.OMBRES.CASTERS.COUVERTURE | Couverture analytique des triangles source | diagnostic mesuré sans recalage | 2 819 triangles LOD0 ; orientations opposées correspondent respectivement aux masques source et Forge | [Mesure](../firepit-source-shadow-triangle-coverage-v1/RESULT.v1.json) |\n| L01.OMBRES.RASTER.ORIENTATION_COMMUNE | Orientation frontale des seules ombres | cause confirmée par témoin ; correction produit restante | couverture source 3 206/912 retrouvée ; depth 3,55e-6 / 1,13e-6 ; prépasse principale à préserver | [Profondeur](../firepit-source-shadow-front-face-control-depth-comparison-v1/RESULT.v1.json) · [Refus visuel](../firepit-source-shadow-front-face-control-image-comparison-v1/RESULT.v1.json) |\n| L01.LOD.01 |')
  text+='\n**Cause de couverture identifiée et confirmée sur GPU.** Les 2 819 triangles LOD0 projetés sans recalage donnent exactement le masque source pour une orientation, le masque Forge pour l’autre (un pixel de précision sur la dernière cascade). Le témoin de sélection opposée des faces conserve tous les buffers géométriques et shaders ; culling GPU Front vérifié. Couverture source/Forge désormais identique : 3 206 et 912 pixels, IoU 1/1. Erreurs maximales de profondeur 0,0000035502 et 0,0000011250 : première cascade active encore au-delà du seuil historique 0,000002, non relevé. Ce témoin modifie aussi la prépasse principale et refuse la comparaison visuelle ; il ne constitue pas une réparation produit. Correction à limiter à l’orientation frontale des ombres dans le contrat commun, en conservant le culling original et la prépasse principale. Aucun asset complet supplémentaire.\n'
  key+=':'+triangle_coverage['status']+':'+face_depth['status']+':'+face_image['status']
 orientation_close=read(W/'source-shadow-orientation-technical-v1/RESULT.v1.json')
 if orientation_close:
  data['current_work_sub_lot']='L01.OMBRES.RECEPTEUR.APRES_ORIENTATION'
  data['current_work_state']='Orientation commune vérifiée : 10 tests, transport GPU et Web ; image sans ombre identique ; profondeur et réponse du récepteur hors seuil'
  data['current_execution']={'sub_lot':data['current_work_sub_lot'],'state':'common_orientation_technical_verified_fidelity_open','receipt':str(W/'source-shadow-orientation-technical-v1/RESULT.v1.json'),'contract_tests_verified':10,'native_gpu_scalars_read':896,'main_unshadowed_image_byte_identical':1,'web_snapshot':24,'shadow_mask_iou':0.39287200110941617,'full_import':False,'complete_assets_delivered_increment':0}
  data['next_action']='Isoler les résidus de profondeur aux bords et l’échantillonnage du récepteur ; conserver les seuils et l’invariance des passes principales'
  lines=text.splitlines()
  lines=[('| L01.OMBRES.RASTER.ORIENTATION_COMMUNE | Orientation frontale des seules ombres | contrat commun vérifié ; fidélité ouverte | 10 tests ; 896 scalaires GPU ; Cw + cull Back ; image sans ombre identique ; Web snapshot 24 | [Clôture technique](../source-shadow-orientation-technical-v1/RESULT.v1.json) |') if line.startswith('| L01.OMBRES.RASTER.ORIENTATION_COMMUNE |') else line for line in lines]
  text='\n'.join(lines)+'\n'
  text=text.replace('| L01.LOD.01 |','| L01.OMBRES.RECEPTEUR.APRES_ORIENTATION | Résidus de profondeur et réponse du récepteur | à résoudre | couverture 1 / 0,998905 ; masque visuel IoU 0,392872 ; MAE 0,022964 / 0,023508 | [Profondeur](../firepit-source-shadow-oriented-depth-comparison-v1/RESULT.v1.json) · [Images](../firepit-source-shadow-oriented-image-comparison-v1/RESULT.v1.json) |\n| L01.LOD.01 |')
  text+='\n**Contrat commun d’orientation des ombres vérifié techniquement.** Orientation frontale facultative et explicite, incluse dans le cache, appliquée seulement aux ombres orthographiques sous contrat D32 inversé. Culling original Back conservé. 10 tests passent ; 896 scalaires de vues GPU lus, 384 CPU/GPU et 384 plan/GPU comparés bit à bit ; 927 épingles natives inchangées. Image sans ombre strictement identique au cas précédent. Web snapshot 24 compilé, sans recette GPU navigateur. Couverture 1 / 0,998905 et IoU du masque visuel 0,392872 contre 0,328282 auparavant. Profondeurs et couleur restent hors seuil ; qualification complète refusée et montage dans le host produit selon contexte source encore requis. Chaque ancienne preuve conserve sa révision ; le changement d’une empreinte moteur est tracé dans la révision dédiée. Aucun asset complet supplémentaire. [Clôture technique](../source-shadow-orientation-technical-v1/RESULT.v1.json).\n'
  key+=':'+orientation_close['status']
 if common_close:
  text=text.replace('Exécution native commune terminée ; réouverture et comparaison à vérifier','20/20 contrôles moyens natifs vérifiés')
 if scene_sampling_comparison:
  text=text.replace('Scène capturée ; comparaison aux références originales restante','Comparaison effectuée ; fidélité refusée')
 receiver_selection_audit=read(W/'firepit-receiver-cascade-selection-audit-v1/RESULT.v1.json')
 if receiver_selection_audit:
  data['current_work_sub_lot']='L01.OMBRES.RECEPTEUR.SELECTION.REPLAY'
  data['current_work_state']='Audit des sélecteurs terminé : sphères Unity contre profondeur de vue Forge ; effet sur les pixels à mesurer dans un replay privé'
  data['current_execution']={'sub_lot':data['current_work_sub_lot'],'state':receiver_selection_audit['status'],'receipt':str(W/'firepit-receiver-cascade-selection-audit-v1/RESULT.v1.json'),'cause_of_pixel_mismatch_proven':False,'full_import':False,'complete_assets_delivered_increment':0}
  data['next_action']=receiver_selection_audit['next_action']
  text=text.replace('| L01.LOD.01 |','| L01.OMBRES.RECEPTEUR.SELECTION.AUDIT | Sélection des cascades source/cible | audit terminé ; effet GPU restant | source : sphères et transition radiale ; cible : profondeur de vue et overlap | [Audit](../firepit-receiver-cascade-selection-audit-v1/RESULT.v1.json) |\n| L01.LOD.01 |')
  key+=':'+receiver_selection_audit['status']
 receiver_controls=read(W/'firepit-receiver-shadow-controls-reopen-v1/RESULT.v1.json')
 if receiver_controls:
  data['current_work_sub_lot']='L01.OMBRES.PONCTUELLES.GPU'
  data['current_work_state']='Deux contrôles récepteur GPU terminés et réouverts ; IoU 0,389967 reste refusée ; suppression du rejet de profondeur sans effet sur les deux images ; ombres ponctuelles à mesurer'
  data['current_execution']={'sub_lot':data['current_work_sub_lot'],'state':receiver_controls['status'],'receipt':str(W/'firepit-receiver-shadow-controls-reopen-v1/RESULT.v1.json'),'gpu_controls_reopened':2,'gpu_views_per_control':2,'shadow_mask_iou':0.38996698855397527,'coordinate_control_two_images_byte_identical':True,'full_import':False,'complete_assets_delivered_increment':0}
  data['next_action']='Lire les projections, le sampling et la participation réelle des casters aux six faces ponctuelles GPU ; comparer aux six faces Unity mesurées, sans modifier les seuils'
  text=text.replace('| L01.LOD.01 |','| L01.OMBRES.RECEPTEUR.CONTROLES | Sphères, bordures et coordonnées source | deux contrôles GPU réouverts ; fidélité refusée | 956 épingles par capture ; 4 profondeurs inchangées ; suppression du rejet z sans effet ; IoU 0,389967 | [Réouverture](../firepit-receiver-shadow-controls-reopen-v1/RESULT.v1.json) |\n| L01.LOD.01 |')
  key+=':'+receiver_controls['status']
 point_gpu=read(W/'firepit-point-shadow-diagnostics-reopen-v1/RESULT.v1.json')
 if point_gpu:
  data['current_work_sub_lot']='L01.OMBRES.PONCTUELLES.BIAIS.CONTROLE'
  data['current_work_state']='Records ponctuels GPU réouverts : couleur source exacte, 12 vues et 576 scalaires comparés bit à bit ; images inchangées ; projection, résolution et déplacement natifs différents'
  data['current_execution']={'sub_lot':data['current_work_sub_lot'],'state':point_gpu['status'],'receipt':str(W/'firepit-point-shadow-diagnostics-reopen-v1/RESULT.v1.json'),'point_gpu_records':2,'point_views':12,'matrix_scalars_bit_exact':576,'original_images_byte_identical':2,'full_import':False,'complete_assets_delivered_increment':0}
  data['next_action']='Comparer le contrôle privé supprimant le déplacement ponctuel natif de 8 cm ; conserver projection, filtre et biais normal'
  text=text.replace('| L01.LOD.01 |','| L01.OMBRES.PONCTUELLES.GPU | Records et vues ponctuels effectifs | technique réouvert ; fidélité ouverte | 2 records ; 12 vues ; 576 scalaires bit exacts ; 2 images identiques ; couleur source exacte | [Réouverture](../firepit-point-shadow-diagnostics-reopen-v1/RESULT.v1.json) |\n| L01.LOD.01 |')
  key+=':'+point_gpu['status']
 point_world_bias=read(W/'firepit-point-world-bias-control-reopen-v1/RESULT.v1.json')
 if point_world_bias:
  data['current_work_sub_lot']='L01.OMBRES.PONCTUELLES.ATLAS_SOURCE'
  data['current_work_state']='Déplacement ponctuel natif de 8 cm isolé : IoU 0,389967 → 0,423939 ; 1 016 épingles et matrices/profondeurs inchangées ; fidélité encore refusée'
  data['current_execution']={'sub_lot':data['current_work_sub_lot'],'state':point_world_bias['status'],'receipt':str(W/'firepit-point-world-bias-control-reopen-v1/RESULT.v1.json'),'shadow_mask_iou':point_world_bias['control_shadow_iou'],'prior_shadow_mask_iou':point_world_bias['prior_shadow_iou'],'full_import':False,'complete_assets_delivered_increment':0}
  data['next_action']='Capturer les profondeurs et paramètres raster des six faces ponctuelles Unity ; comparer aux faces Forge pour qualifier la politique source complète avant intégration commune'
  text=text.replace('| L01.LOD.01 |','| L01.OMBRES.PONCTUELLES.BIAIS.CONTROLE | Déplacement de profondeur ponctuel natif | effet GPU mesuré ; fidélité refusée | shader privé sans déplacement ; IoU 0,423939 ; records, 12 matrices de face et profondeurs inchangés | [Contrôle](../firepit-point-world-bias-control-reopen-v1/RESULT.v1.json) |\n| L01.LOD.01 |')
  key+=':'+point_world_bias['status']
 point_raster_source=read(W/'firepit-source-point-raster-reopen-v1/RESULT.v1.json')
 if point_raster_source:
  data['current_work_sub_lot']='L01.OMBRES.PONCTUELLES.ATLAS_D16.COMMUN'
  data['current_work_state']='Source ponctuelle vérifiée : atlas D16 4 096², 16 777 216 profondeurs actives, 6 requêtes, pente 2,5 et clipping ; 24 dépendances et références inchangées ; atlas natif commun restant'
  data['current_execution']={'sub_lot':data['current_work_sub_lot'],'state':point_raster_source['status'],'receipt':str(W/'firepit-source-point-raster-reopen-v1/RESULT.v1.json'),'active_gpu_depth_samples':16777216,'source_requests':6,'caster_matrix_scalars':576,'source_actual_depth_format':'D16_UNorm','current_native_point_depth_format':'Depth32Float','inactive_allocation_stale':True,'full_import':False,'complete_assets_delivered_increment':0}
  data['next_action']='Préparer le rendu ponctuel commun avec atlas D16, viewports fractionnaires, projections perspectives et raster source ; comparer les profondeurs natives puis le filtre PCF5 avant qualification produit'
  text=text.replace('| L01.LOD.01 |','| L01.OMBRES.PONCTUELLES.ATLAS_SOURCE | Atlas et raster ponctuels Unity effectifs | source réouverte ; cible restante | D16 4 096² ; 16 777 216 valeurs actives ; 6 requêtes pente 2,5 ; clipping ; références inchangées | [Réouverture](../firepit-source-point-raster-reopen-v1/RESULT.v1.json) |\n| L01.LOD.01 |')
  key+=':'+point_raster_source['status']
 atlas_contract=read(W/'source-punctual-atlas-contract-v1/RESULT.v1.json')
 if atlas_contract.get('status')=='common_d16_atlas_contract_native_compile_gpu_interior_and_inactive_clear_reopened_edge_coverage_refused':
  data['current_work_sub_lot']='L01.OMBRES.PONCTUELLES.ATLAS_D16.COUVERTURE'
  data['current_work_state']='Atlas D16 commun ajouté ; 5 tests et compilation native vérifiés ; 33 554 432 profondeurs GPU réouvertes ; effacement inactif conforme ; 4 096 pixels de bord refusés'
  data['punctual_atlas_contract']={'proof':str(W/'source-punctual-atlas-contract-v1/RESULT.v1.json'),'contract_tests_verified':5,'gpu_samples':33554432,'ideal_coverage_mismatches':4096,'fractional_coverage_qualified':False,'full_import':False,'complete_assets_delivered_increment':0}
  data['next_action']=atlas_contract['next_action']
  text=text.replace('| L01.LOD.01 |','| L01.OMBRES.PONCTUELLES.ATLAS_D16.CONTRAT | Atlas commun D16 | infrastructure vérifiée ; couverture refusée | 5 tests ; compilation native ; 33 554 432 profondeurs ; 4 096 pixels de bord ouverts | [Preuve](../source-punctual-atlas-contract-v1/RESULT.v1.json) |\n| L01.LOD.01 |')
  key+=':'+atlas_contract['status']
 caster_d16=read(W/'firepit-punctual-d16-caster-reopen-v1/RESULT.v1.json')
 if caster_d16.get('status')=='source_d3d11_fractional_edge_difference_confirmed_original_lod0_d16_depth_replay_measured_fidelity_refused':
  data['current_work_sub_lot']='L01.OMBRES.PONCTUELLES.CASTERS_D16.CONVENTIONS'
  data['current_work_state']='Écart de bord D3D11/D3D12 confirmé ; fragment sans effet ; géométrie FirePit originale rendue en D16 sur 6 faces ; IoU des supports de profondeur 0,713568 ; fidélité refusée'
  data['punctual_caster_d16']={'proof':str(W/'firepit-punctual-d16-caster-reopen-v1/RESULT.v1.json'),**caster_d16['caster_replay'],'fractional_coverage_qualified':False,'full_import':False,'complete_assets_delivered_increment':0}
  data['next_action']=caster_d16['next_action']
  text=text.replace('| L01.LOD.01 |','| L01.OMBRES.PONCTUELLES.CASTERS_D16 | Géométrie source dans atlas commun | capture mesurée ; fidélité refusée | 3 381 sommets ; 2 819 triangles ; 6 faces ; IoU supports 0,713568 ; bord non conforme confirmé | [Réouverture](../firepit-punctual-d16-caster-reopen-v1/RESULT.v1.json) |\n| L01.LOD.01 |')
  key+=':'+caster_d16['status']
 mapped_d16=read(W/'source-punctual-atlas-mapped-reopen-v1/RESULT.v1.json')
 if mapped_d16.get('status')=='common_fractional_d16_mapping_source_numeric_bit_exact_native_compile_verified_caster_depth_refused':
  data['current_work_sub_lot']='L01.OMBRES.PONCTUELLES.CASTERS_D16.PROFONDEURS'
  data['current_work_state']='Couverture fractionnaire commune corrigée : 33 554 432 profondeurs numériques identiques à Unity ; 5 tests et compilation native ; FirePit IoU supports 0,999321 ; profondeurs hors seuil et passe source restantes'
  data['punctual_mapped_d16']={'proof':str(W/'source-punctual-atlas-mapped-reopen-v1/RESULT.v1.json'),'numeric_samples_bit_exact':33554432,'source_numeric_fractional_coverage_qualified':True,**mapped_d16['mapped_caster'],'full_import':False,'complete_assets_delivered_increment':0}
  data['next_action']=mapped_d16['next_action']
  text=text.replace('| L01.LOD.01 |','| L01.OMBRES.PONCTUELLES.MAPPING_D16 | Couverture fractionnaire commune | numérique qualifié ; profondeur mesh refusée | 33 554 432 valeurs source bit-exactes ; 5 tests ; natif ; FirePit supports 0,999321 | [Réouverture](../source-punctual-atlas-mapped-reopen-v1/RESULT.v1.json) |\n| L01.LOD.01 |')
  key+=':'+mapped_d16['status']
 mapped_close=read(W/'source-punctual-atlas-mapped-install-v1/RESULT.v1.json')
 if mapped_close.get('status')=='common_fractional_d16_mapping_numeric_source_bit_exact_native_and_web_compile_technical_verified_asset_depth_refused':
  data['current_work_sub_lot']='L01.OMBRES.PONCTUELLES.CASTERS_D16.PROFONDEURS'
  data['current_work_state']='Sous-lot couverture fractionnaire techniquement clos : 33 554 432 profondeurs numériques bit-exactes, 5 tests, natif et Web compilés, 1 054 épingles Web ; profondeur des assets, récepteur et usage Studio restants'
  data['punctual_mapped_d16_compile_close']={'proof':str(W/'source-punctual-atlas-mapped-install-v1/RESULT.v1.json'),'native_compile':True,'web_compile':True,'web_gpu_render':False,'web_source_pins_verified':1054,'full_import':False,'complete_assets_delivered_increment':0}
  data['next_action']='Identifier le biais et la passe shader source responsables des profondeurs restantes, puis raccorder passe et récepteur ponctuels communs ; vérifier rendu WebGPU et usage Studio'
  text=text.replace('| L01.LOD.01 |','| L01.OMBRES.PONCTUELLES.MAPPING_D16.TECHNIQUE | Couverture fractionnaire commune | clos technique | 5 tests ; 33 554 432 profondeurs ; natif/Web compilés ; aucune fidélité asset ni usage Studio acquis | [Clôture](../source-punctual-atlas-mapped-install-v1/RESULT.v1.json) |\n| L01.LOD.01 |')
  key+=':'+mapped_close['status']
 depth_isolation=read(W/'firepit-punctual-depth-isolation-reopen-v1/RESULT.v1.json')
 if depth_isolation.get('status')=='source_static_shadow_hdrp_depth_bit_exact_verified_bias_not_sole_cause_device_clip_refusal_retained':
  data['current_work_sub_lot']='L01.OMBRES.PONCTUELLES.CASTERS_D16.CLIPPING'
  data['current_work_state']='Passe Unity simple et HDRP identiques sur 16 777 216 profondeurs LOD0 ; écart Forge persistant sans biais ; invariance sans effet ; feature clip distances refusée explicitement ; clipping à traiter sans cette dépendance'
  data['punctual_depth_isolation']={'proof':str(W/'firepit-punctual-depth-isolation-reopen-v1/RESULT.v1.json'),'source_hdrp_simple_bit_exact_samples':16777216,'invariant_changed_pixels':0,'bias_not_sole_cause':True,'optional_clip_feature_refused':True,'full_import':False,'complete_assets_delivered_increment':0}
  data['next_action']=depth_isolation['next_action']
  text=text.replace('| L01.LOD.01 |','| L01.OMBRES.PONCTUELLES.PROFONDEUR.ISOLATION | Isoler shader et raster | source réouverte ; profondeur cible refusée | 16 777 216 profondeurs Unity/HDRP identiques ; invariance sans effet ; sans biais : écart persistant ; clip distances indisponible | [Preuve](../firepit-punctual-depth-isolation-reopen-v1/RESULT.v1.json) |\n| L01.LOD.01 |')
  key+=':'+depth_isolation['status']
 projection_clip=read(W/'firepit-punctual-projection-clipping-reopen-v1/RESULT.v1.json')
 if projection_clip.get('status')=='gpu_projection_scalars_bit_exact_verified_face_clipping_replay_measured_depth_refused':
  data['current_work_sub_lot']='L01.OMBRES.PONCTUELLES.CASTERS_D16.RASTER'
  data['current_work_state']='81 144 coordonnées de projection GPU Unity/Forge bit-exactes ; découpe XY exécutée sans feature optionnelle, 5 faces dessinées et une vide ; profondeur encore refusée, découpe non installée comme correction'
  data['punctual_projection_clipping']={'proof':str(W/'firepit-punctual-projection-clipping-reopen-v1/RESULT.v1.json'),'projection_scalars_bit_exact':81144,'faces':6,'nonempty_faces_drawn':5,'clipping_sufficient':False,'full_import':False,'complete_assets_delivered_increment':0}
  data['next_action']=projection_clip['next_action']
  text=text.replace('| L01.LOD.01 |','| L01.OMBRES.PONCTUELLES.PROJECTION.CLIPPING | Projection GPU et découpe des triangles | coordonnées vérifiées ; profondeur refusée | 81 144 scalaires identiques ; 5 faces + une vide ; découpe insuffisante ; échec initial conservé | [Réouverture](../firepit-punctual-projection-clipping-reopen-v1/RESULT.v1.json) |\n| L01.LOD.01 |')
  key+=':'+projection_clip['status']
 tent5_source=read(W/'source-shadow-tent5-source-reopen-v1/RESULT.v1.json')
 if tent5_source.get('status')=='source_pcf5_gpu_capture_reopened':
  data['current_work_sub_lot']='L01.PCF5.2'
  data['current_work_state']='L01.PCF5.1 termine : 450 observations GPU source poids/UV reouvertes ; deux captures identiques ; calcul Forge, raccordement atlas et rendu final restants'
  data['punctual_pcf5_source']={'proof':str(W/'source-shadow-tent5-source-reopen-v1/RESULT.v1.json'),'gpu_observations':450,'full_import':False,'complete_assets_delivered_increment':0}
  data['next_action']=tent5_source['next_action']
  text=text.replace('| L01.LOD.01 |','| L01.PCF5.1 | Reference GPU du filtre ponctuel | capture source reouverte | 25 phases, 9 taps, 2 captures : 450 observations ; aucune integration Forge acquise | [Preuve](../source-shadow-tent5-source-reopen-v1/RESULT.v1.json) |\n| L01.LOD.01 |')
  key+=':'+tent5_source['status']
 tent5_math=read(W/'source-shadow-tent5-gpu-reopen-v1/RESULT.v1.json')
 tent5_atlas=read(W/'source-shadow-tent5-atlas-reopen-v1/RESULT.v1.json')
 tent5_audit=read(W/'source-shadow-tent5-atlas-difference-audit-v1/RESULT.v1.json')
 if tent5_math.get('status')=='actual_source_native_tent5_gpu_weights_uv_verified' and tent5_audit.get('status')=='single_pcf5_receiver_difference_localized':
  data['current_work_sub_lot']='L01.PCF5.3.LIMITES'
  data['current_work_state']='PCF5 : poids et UV GPU Unity/Forge identiques ; recepteurs executes, 1 cas actif sur 1 458 refuse ; atlas inactif exact sur 1 458 cas ; ecart localise au texel (410,2864), source 0,222278 / Forge 0 ; profondeur et rendu final non qualifies'
  data['punctual_pcf5_math']={'proof':str(W/'source-shadow-tent5-gpu-reopen-v1/RESULT.v1.json'),'observations_verified':450,'max_weight_error':0,'max_uv_error':0,'full_import':False}
  data['punctual_pcf5_atlas']={'proof':str(W/'source-shadow-tent5-atlas-reopen-v1/RESULT.v1.json'),'audit':str(W/'source-shadow-tent5-atlas-difference-audit-v1/RESULT.v1.json'),'measurements':tent5_atlas['measurements'],'sampling_qualified':False,'complete_assets_delivered_increment':0}
  data['next_action']=tent5_audit['next_action']
  text=text.replace('| L01.LOD.01 |','| L01.PCF5.2 | Poids et UV Forge sur GPU | technique verifie | 25 phases / 9 taps ; zero ecart ; images source inchangees | [Preuve](../source-shadow-tent5-gpu-reopen-v1/RESULT.v1.json) |\n| L01.PCF5.3 | Lecture des atlas autonomes | execute ; 1 cas refuse | 1 458 cas actifs + 1 458 inactifs ; un texel de comparaison different ; deux echecs de compilation archives | [Comparaison](../source-shadow-tent5-atlas-reopen-v1/RESULT.v1.json) · [Audit](../source-shadow-tent5-atlas-difference-audit-v1/RESULT.v1.json) |\n| L01.LOD.01 |')
  key+=':'+tent5_math['status']+':'+tent5_atlas['status']+':'+tent5_audit['status']
 tent5_taps=read(W/'source-shadow-tent5-taps-reopen-v1/RESULT.v1.json')
 tent5_bridge=read(W/'source-shadow-tent5-single-texel-bridge-v1/RESULT.v1.json')
 town_scope=read(W/'townsmith-current-draw-binding-scope-v1/RESULT.v1.json')
 if tent5_bridge.get('status')=='single_native_missing_depth_texel_explains_captured_pcf5_difference':
  data['current_work_sub_lot']='L01.PCF5.3.CASTERS'
  data['current_work_state']='26 244 taps par backend verifies : poids et UV identiques ; un seul tap actif differe ; texel absent (410,2864) explique l ecart PCF a 0,00000003 pres ; raster, scene et Studio non qualifies'
  data['punctual_pcf5_taps']={'proof':str(W/'source-shadow-tent5-taps-reopen-v1/RESULT.v1.json'),'taps_per_backend':26244,'weight_uv_error':0,'active_taps_differing':1,'full_import':False}
  data['punctual_pcf5_single_texel_bridge']={'proof':str(W/'source-shadow-tent5-single-texel-bridge-v1/RESULT.v1.json'),'reconstruction_error':tent5_bridge['reconstruction_error'],'scope':'captured isolated tap','complete_assets_delivered_increment':0}
  data['next_action']=tent5_bridge['next_action']
  text=text.replace('| L01.LOD.01 |','| L01.PCF5.3.TAPS | Neuf taps par point GPU | poids et UV verifies ; profondeur refusee | 26 244 taps par backend ; un tap actif different | [Preuve](../source-shadow-tent5-taps-reopen-v1/RESULT.v1.json) |\n| L01.PCF5.3.CAUSE | Contribution du texel absent | cause verifiee pour ce point | quatre texels du tap ; erreur reconstruite a moins de 0,00000003 ; aucun seuil change | [Preuve](../source-shadow-tent5-single-texel-bridge-v1/RESULT.v1.json) |\n| L01.LOD.01 |')
  key+=':'+tent5_taps['status']+':'+tent5_bridge['status']
 if town_scope.get('status')=='eight_prefabs_draw_binding_scope_refreshed':
  data['townsmith_current_draw_scope']={'proof':str(W/'townsmith-current-draw-binding-scope-v1/RESULT.v1.json'),'prefabs_reviewed':8,'bindings_requiring_native_qualification':37,'unique_meshes_affected':8,'draw_mapping_qualified':False}
  text=text.replace('Townsmith : résoudre 6 bindings','Townsmith : qualifier 37 bindings / 8 meshes dans les 8 prefabs')
  text=text.replace('| L03.TOWNSMITH.BINDINGS | Affectations de matériaux Townsmith | refus à résoudre | 6 bindings source | à produire |','| L03.TOWNSMITH.BINDINGS | Affectations de matériaux Townsmith | refus à résoudre | 37 bindings / 8 meshes / 8 prefabs ; ancien compteur 6 limite au premier groupe | [Inventaire actuel](../townsmith-current-draw-binding-scope-v1/RESULT.v1.json) |')
  key+=':'+town_scope['status']
 town_context=read(W/'townsmith-hdrp-slot-contexts-reopen-v1/RESULT.v1.json')
 if town_context.get('status')=='eight_source_hdrp_slot_contexts_reopened_private_serialization_scoped_draw_mapping_unobserved':
  data['current_work_sub_lot']='L03.TOWNSMITH.DESSINS.CONTEXTE_HDRP'
  data['current_work_state']='8 configurations HDRP source reouvertes ; 144 passes compilees ; 8 images identiques entre deux executions ; 7 copies privees reserialisees avec proprietes de rendu preservees ; 37 bindings a qualifier ; usage Forge non verifie'
  data['townsmith_hdrp_slot_contexts']={'proof':str(W/'townsmith-hdrp-slot-contexts-reopen-v1/RESULT.v1.json'),'cases_reopened':8,'affected_bindings':37,'native_draw_mapping_qualified':False,'complete_assets_delivered_increment':0}
  data['next_action']='Comparer les huit rendus source aux controles explicites de sous-maillages et slots, puis convertir et verifier dans Forge ; conserver les refus de rendu ponctuel ouverts'
  text=text.replace('| L01.LOD.01 |','| L03.TOWNSMITH.DESSINS.CONTEXTE_HDRP | Huit configurations source | controle technique verifie ; dessins ouverts | 8 vues / 144 passes ; sources preservees ; aucun usage Forge acquis | [Preuve](../townsmith-hdrp-slot-contexts-reopen-v1/RESULT.v1.json) |\n| L01.LOD.01 |')
  key+=':'+town_context['status']
 town_explicit=read(W/'townsmith-explicit-slot-controls-reopen-v1/RESULT.v1.json')
 if town_explicit.get('status')=='sixteen_explicit_triangle_slot_gpu_controls_reopened':
  data['current_work_sub_lot']='L03.TOWNSMITH.DESSINS.CONTROLES_EXPLICITES'
  data['current_work_state']='16 comparaisons GPU reouvertes ; lit et etagere : 10 vues bit-exactes ; portes : 2 a 4 pixels differents en dessin par slots, plus de 4500 en affectation a tous les sous-maillages ; portes, multiplicite des passes et Forge non qualifies'
  data['townsmith_explicit_controls']={'proof':str(W/'townsmith-explicit-slot-controls-reopen-v1/RESULT.v1.json'),'cases_reopened':16,'byte_identical_controls':10,'native_draw_event_mapping_qualified':False,'complete_assets_delivered_increment':0}
  data['next_action']='Isoler les 2 a 4 pixels differents des portes et qualifier les affectations converties dans Forge ; conserver les passes repetees et le rendu ponctuel ouverts'
  text=text.replace('| L01.LOD.01 |','| L03.TOWNSMITH.DESSINS.CONTROLES_EXPLICITES | Comparaisons par sous-maillage et slot | 16 controles executes ; portes ouvertes | 10 vues identiques ; portes : 2 a 4 pixels differents ; aucun usage Forge acquis | [Preuve](../townsmith-explicit-slot-controls-reopen-v1/RESULT.v1.json) |\n| L01.LOD.01 |')
  key+=':'+town_explicit['status']
 town_descriptors=read(W/'townsmith-descriptor-slot-controls-reopen-v2/DESCRIPTOR-RESULT.v1.json')
 if town_descriptors.get('status')=='original_index_descriptors_and_vertex_attributes_verified_gpu_differences_measured':
  data['current_work_sub_lot']='L03.TOWNSMITH.DESSINS.DESCRIPTEURS'
  data['current_work_state']='16 captures supplementaires ; 44 descripteurs et attributs vertex verifies ; ecarts des portes inchanges (3/2/4 pixels) ; reconstruction des indices non explicative ; echec initial de troncature conserve'
  data['townsmith_descriptor_controls']={'proof':str(W/'townsmith-descriptor-slot-controls-reopen-v2/DESCRIPTOR-RESULT.v1.json'),'descriptors_verified':44,'captures':16,'door_differences_open':True,'complete_assets_delivered_increment':0}
  data['next_action']='Comparer clone complet et triangles visibles avec ombres du mesh source pour isoler les passes ; convertir ensuite les affectations qualifiees dans Forge'
  text=text.replace('| L01.LOD.01 |','| L03.TOWNSMITH.DESSINS.DESCRIPTEURS | Indices et attributs originaux | technique verifie ; portes ouvertes | 44 descripteurs / 16 captures ; neuf pixels residuels inchanges ; echec initial conserve | [Preuve](../townsmith-descriptor-slot-controls-reopen-v2/DESCRIPTOR-RESULT.v1.json) |\n| L01.LOD.01 |')
  key+=':'+town_descriptors['status']
 town_passes=read(W/'townsmith-door-pass-controls-reopen-v1/RESULT.v1.json')
 town_shadows=read(W/'townsmith-explicit-shadow-controls-reopen-v1/RESULT.v1.json')
 if town_passes.get('status')=='nine_original_door_renderer_and_shadow_controls_reopened' and town_shadows.get('status')=='six_explicit_door_shadow_geometry_controls_reopened':
  data['current_work_sub_lot']='L03.TOWNSMITH.DESSINS.PASSES'
  data['current_work_state']='15 captures supplementaires bit-exactes ; clone et contexte enfant ecartes ; separer visible et shadow caster donne zero ecart ; ombres completes et visibles indiscernables : triangles caches non prouves comme cause ; conversion Forge encore ouverte'
  data['townsmith_pass_controls']={'renderer_proof':str(W/'townsmith-door-pass-controls-reopen-v1/RESULT.v1.json'),'explicit_shadow_proof':str(W/'townsmith-explicit-shadow-controls-reopen-v1/RESULT.v1.json'),'captures':15,'byte_identical':15,'hidden_shadow_triangles_causal_qualification':False,'complete_assets_delivered_increment':0}
  data['next_action']='Isoler le mode shadow casting du renderer et relever l etat reel des ombres de la lumiere ; conserver le plan de conversion couleur et ombres comme non qualifie pour Forge'
  text=text.replace('| L01.LOD.01 |','| L03.TOWNSMITH.DESSINS.PASSES | Contextes renderer et ombres explicites | 15 vues bit-exactes ; causalite limitee | clone/enfant exacts ; deux geometries d ombres equivalentes ; aucune fidelite Forge acquise | [Renderer](../townsmith-door-pass-controls-reopen-v1/RESULT.v1.json) / [Ombres explicites](../townsmith-explicit-shadow-controls-reopen-v1/RESULT.v1.json) |\n| L01.LOD.01 |')
  key+=':'+town_passes['status']+':'+town_shadows['status']
 town_modes=read(W/'townsmith-shadow-mode-controls-reopen-v1/RESULT.v1.json')
 town_recipes=read(W/'townsmith-verified-door-draw-recipes-v1/DRAW_BINDINGS.v1.json')
 if town_recipes.get('status')=='fifteen_source_verified_door_colour_bindings_converted_complete_geometry_retained':
  data['current_work_sub_lot']='L03.TOWNSMITH.CONVERSION.AFFECTATIONS_PORTES'
  data['current_work_state']='15 affectations couleur converties / 3 meshes ; geometrie complete conservee ; correspondance GPU source avec et sans ombres ; lumieres anciennes sans ombres confirmees ; shader Forge, politique des ombres et prefabs complets non qualifies'
  data['townsmith_door_colour_recipes']={'proof':str(W/'townsmith-verified-door-draw-recipes-v1/DRAW_BINDINGS.v1.json'),'bindings_converted':15,'unique_meshes':3,'all_submesh_geometry_retained':True,'forge_material_program_qualified':False,'complete_assets_delivered_increment':0}
  data['next_action']=town_recipes['next_action']
  text=text.replace('| L01.LOD.01 |','| L03.TOWNSMITH.CONVERSION.AFFECTATIONS_PORTES | Affectations couleur des portes | converties en recettes ; rendu Forge ouvert | 15 bindings / 3 meshes ; sous-maillages et slots conserves ; shader et usage Studio a qualifier | [Recettes](../townsmith-verified-door-draw-recipes-v1/DRAW_BINDINGS.v1.json) |\n| L01.LOD.01 |')
  key+=':'+town_modes['status']+':'+town_recipes['status']
 town_paint=read(W/'townsmith-s-paint-source-program-reopen-v1/RESULT.v1.json')
 if town_paint.get('status')=='source_paint_ir_objects_and_sdk_vertex_bindings_independently_reopened':
  data['current_work_sub_lot']='L03.TOWNSMITH.SHADER.S_PAINT.SOURCE'
  data['current_work_state']='S_Paint compile en IR : 134 operations / 255 ports / 7 branches / 2 instances de sous-graphe ; code Unity genere et reouvert ; entrees vertex implicites originales preservees ; WGSL et fidelite GPU Forge restantes'
  data['townsmith_paint_source']={'proof':str(W/'townsmith-s-paint-source-program-reopen-v1/RESULT.v1.json'),'active_nodes_reopened':134,'input_slots_reopened':255,'selected_branches_reopened':7,'subgraph_instances_reopened':2,'wgsl_generated':False,'forge_material_render_qualified':False,'complete_assets_delivered_increment':0}
  data['next_action']=town_paint['next_action']
  text=text.replace('| L01.LOD.01 |','| L03.TOWNSMITH.SHADER.S_PAINT.SOURCE | Programme shader source | IR et code SDK reouverts | 134 operations / 255 ports ; position, normale et tangente implicites ; WGSL et rendu Forge ouverts | [Preuve](../townsmith-s-paint-source-program-reopen-v1/RESULT.v1.json) |\n| L01.LOD.01 |')
  key+=':'+town_paint['status']
 town_wgsl=read(W/'townsmith-s-paint-wgsl-reopen-v1/RESULT.v1.json')
 if town_wgsl.get('status')=='all_134_native_typed_paint_wgsl_operations_reopened_runtime_texture_capacity_gap_verified':
  data['current_work_sub_lot']='L03.TOWNSMITH.SHADER.S_PAINT.WGSL'
  data['current_work_state']='134 operations WGSL generees, types fragment valides et reouverts ; 10 textures / 3 normal maps ; entrees implicites conservees ; contrat commun actuel limite a 7 textures, extension requise ; import textures, GPU Forge et Studio non qualifies'
  data['townsmith_paint_wgsl']={'proof':str(W/'townsmith-s-paint-wgsl-reopen-v1/RESULT.v1.json'),'operations_reopened':134,'texture_bindings_required':10,'current_common_runtime_texture_capacity_snapshot':7,'source_numeric_gpu_equivalence_qualified':False,'complete_assets_delivered_increment':0}
  data['next_action']=town_wgsl['next_action']
  text=text.replace('| L01.LOD.01 |','| L03.TOWNSMITH.SHADER.S_PAINT.WGSL | Programme arithmetique WGSL | genere et types verifies ; runtime ouvert | 134 operations / 10 textures / 3 normal maps ; contrat commun a etendre de 7 a 10 | [Preuve](../townsmith-s-paint-wgsl-reopen-v1/RESULT.v1.json) |\n| L01.LOD.01 |')
  key+=':'+town_wgsl['status']
 layout_probe=read(W/'source-texture-layout-admission-v2/RUN-RESULT.v1.json')
 if layout_probe:
  data['current_work_sub_lot']='L03.TOWNSMITH.SHADER.S_PAINT.TEXTURES.GPU'
  layout_status=layout_probe.get('status','unknown')
  if layout_status=='actual_common_and_proposed_ten_texture_native_layouts_created_inputs_unchanged':
   data['current_work_state']='Layouts existant et dix textures crees sur GPU natif ; sources communes inchangees ; pipeline complet, profils textures, fidelite Forge et Studio restent a verifier'
   data['next_action']='Verifier les limites du pipeline complet puis etendre le contrat commun sans modifier les anciens bindings et modes de normales'
  elif layout_status=='common_source_texture_layout_native_check_running':
   data['current_work_state']='Controle GPU prive V2 en compilation/execution ; echec de compilation V1 conserve ; aucune extension du moteur appliquee'
   data['next_action']='Lire le resultat GPU V2, puis verifier les limites du pipeline complet avant l extension commune'
  else:
   data['current_work_state']='Controle GPU V2 refuse ; consulter stderr et le recu ; aucun asset supplementaire livre et moteur commun inchange'
   data['next_action']='Diagnostiquer le refus GPU V2 et preparer une correction versionnee sans masquer l echec'
  data['source_texture_layout_probe']={'proof':str(W/'source-texture-layout-admission-v2/RUN-RESULT.v1.json'),'status':layout_status,'complete_assets_delivered_increment':0,'actual_render_pipeline_created':False}
  text=text.replace('| L01.LOD.01 |','| L03.TOWNSMITH.SHADER.S_PAINT.TEXTURES.GPU | Admission des dix textures | '+data['current_work_state']+' | test natif ; pipeline et asset non qualifies | [Recu](../source-texture-layout-admission-v2/RUN-RESULT.v1.json) |\n| L01.LOD.01 |')
  key+=':'+layout_status
 pipeline_probe=read(W/'source-texture-layout-admission-v3/RUN-RESULT.v1.json')
 texture_sources=read(W/'townsmith-s-paint-texture-inventory-v1/RESULT.v1.json')
 if pipeline_probe and texture_sources:
  data['current_work_sub_lot']='L03.TOWNSMITH.SHADER.S_PAINT.PIPELINE.GPU'
  pipeline_status=pipeline_probe.get('status','unknown')
  if pipeline_status=='actual_bevy_four_group_layouts_created_requirements_measured_inputs_unchanged':
   data['current_work_state']='Layouts des quatre groupes Bevy mesures et crees sur GPU natif ; dix sources de textures et leurs meta SHA verifies ; compatibilite Web, pipeline de rendu, profils importes et Studio ouverts'
   data['next_action']='Lire les budgets complets des groupes natifs, preparer le contrat dix textures compatible puis capturer les profils importes Unity'
  elif pipeline_status=='common_source_texture_layout_native_check_running':
   data['current_work_state']='Admission du groupe materiau natif verifiee : 18 textures / 17 samplers avec extension ; budgets des quatre groupes Bevy en controle ; dix textures originales et meta SHA verifies ; moteur inchange'
   data['next_action']='Lire le controle GPU des quatre groupes ; resoudre les limites Web de base avant l extension commune ; capturer ensuite formats, mips et profils Unity'
  else:
   data['current_work_state']='Controle des quatre groupes Bevy refuse ; admission du groupe materiau V2 et dix sources originales verifies ; conserver et diagnostiquer le refus V3'
   data['next_action']='Diagnostiquer le refus V3 sans relancer les sources deja validees'
  data['townsmith_texture_source_inventory']={'proof':str(W/'townsmith-s-paint-texture-inventory-v1/RESULT.v1.json'),'textures_reopened':10,'independent_normal_profiles':3,'complete_assets_delivered_increment':0}
  text=text.replace('| L01.LOD.01 |','| L03.TOWNSMITH.SHADER.S_PAINT.TEXTURES.SOURCES | Dix textures originales | SHA et copies privees verifies | dix sources, dix meta, trois normal maps ; stockage GPU restant | [Preuve](../townsmith-s-paint-texture-inventory-v1/RESULT.v1.json) |\n| L03.TOWNSMITH.SHADER.S_PAINT.PIPELINE.GPU | Quatre groupes Bevy | '+data['current_work_state']+' | pipeline rendu et Web non qualifies | [Controle](../source-texture-layout-admission-v3/RUN-RESULT.v1.json) |\n| L01.LOD.01 |')
  key+=':'+pipeline_status+':'+texture_sources['status']
 native_textures=read(W/'townsmith-s-paint-native-textures-reopen-v1/RESULT.v1.json')
 if native_textures:
  data['current_work_sub_lot']='L03.TOWNSMITH.SHADER.S_PAINT.TEXTURES.BC_STORAGE'
  data['current_work_state']='Dix profils Unity verifies ; deux textures UNORM / 24 mips / 44 739 240 octets extraits et SHA verifies ; huit textures BC1/BC3 refusees en readback GPU direct ; sources inchangees ; extraction isolee restante'
  data['next_action']='Extraire les huit textures compressees avec la recette de copie readable isolee ; conserver les reglages originaux et verifier formats, mips et sampling apres reimport ; integration Forge et Studio restantes'
  data['townsmith_native_texture_profiles']={'proof':str(W/'townsmith-s-paint-native-textures-reopen-v1/RESULT.v1.json'),'profiles_verified':10,'native_mips_verified':24,'compressed_textures_refused':8,'complete_assets_delivered_increment':0}
  text=text.replace('| L01.LOD.01 |','| L03.TOWNSMITH.SHADER.S_PAINT.TEXTURES.NATIVE | Profils natifs et stockage | profils verifies ; stockage partiel | 10 profils ; 24 mips UNORM verifies ; 96 mips compresses refuses | [Preuve](../townsmith-s-paint-native-textures-reopen-v1/RESULT.v1.json) |\n| L01.LOD.01 |')
  key+=':'+native_textures['status']
 compressed_run=read(W/'townsmith-s-paint-compressed-storage-v1/RUN-RESULT.v1.json')
 compressed_close=read(W/'townsmith-s-paint-compressed-storage-reopen-v1/RESULT.v1.json')
 if compressed_run:
  data['current_work_sub_lot']='L03.TOWNSMITH.SHADER.S_PAINT.TEXTURES.BC_STORAGE'
  if compressed_close:
   data['current_work_sub_lot']='L03.TOWNSMITH.SHADER.S_PAINT.TEXTURES.TRANSPORT'
   data['current_work_state']='Huit textures compressees / 96 mips verifies ; 864 mesures de texels avant/apres readable bit-exactes ; huit reglages restaures dans la copie ; originaux inchangees ; dix ressources natives a raccorder dans Forge'
   data['next_action']='Composer les dix ressources natives sans changer formats, mips, sRGB ou samplers ; adapter le contrat commun et comparer source/Forge sur GPU'
  elif compressed_run.get('status')=='isolated_townsmith_compressed_texture_extraction_running':
   data['current_work_state']='Extraction Unity des huit textures compressees en cours dans un nouveau projet isole sur F ; import initial des packages, puis mips natifs et controle de texels avant/apres readable ; aucune nouvelle qualification acquise'
   data['next_action']='Attendre le processus Unity de ce projet isole, puis rouvrir les 96 mips et verifier formats, reglages et mesures GPU'
  else:
   data['current_work_state']='Execution d extraction compressee terminee ; verifier le recu natif et les erreurs avant toute qualification ; originaux et echecs preserves'
   data['next_action']='Lire le resultat natif et executer la reouverture des 96 mips, ou diagnostiquer le refus conserve'
  data['townsmith_compressed_texture_work']={'run_receipt':str(W/'townsmith-s-paint-compressed-storage-v1/RUN-RESULT.v1.json'),'closure_receipt':str(W/'townsmith-s-paint-compressed-storage-reopen-v1/RESULT.v1.json') if compressed_close else None,'complete_assets_delivered_increment':0}
  key+=':'+compressed_run['status']+(':'+compressed_close['status'] if compressed_close else '')
 ten_texture_wire=read(W/'source-graph-ten-texture-contract-check-v1/RUN-RESULT.v1.json')
 ten_texture_engine=read(W/'source-graph-ten-texture-engine-check-v1/RUN-RESULT.v1.json')
 if ten_texture_wire.get('status')=='ten_texture_wire_contract_tests_passed_inputs_unchanged':
  data['source_graph_ten_texture_contract']={'proof':str(W/'source-graph-ten-texture-contract-check-v1/RUN-RESULT.v1.json'),'wire_tests_passed':25,'schema_version_added':6,'historical_wire_capacity':7,'new_wire_capacity':10,'actual_modified_material_gpu_layout_verified':False,'complete_assets_delivered_increment':0}
  data['current_work_state'] += ' ; contrat commun schema 6 applique, 25 tests wire passes ; tests moteur : '+ten_texture_engine.get('status','non lances')+' ; budget GPU accru meme pour anciens programmes, compatibilite device non acquise'
  text=text.replace('| L01.LOD.01 |','| L03.TOWNSMITH.SHADER.S_PAINT.CONTRAT.10 | Contrat commun dix textures | 25 tests wire passes | anciens schemas limites a 7, scene bindings preserves ; moteur/GPU restants | [Tests](../source-graph-ten-texture-contract-check-v1/RUN-RESULT.v1.json) |\n| L01.LOD.01 |')
  key+=':'+ten_texture_wire['status']+':'+ten_texture_engine.get('status','pending')
 compressed_v2_path=W/'townsmith-s-paint-compressed-storage-reopen-v2/RESULT.v1.json'
 resources_close_path=W/'townsmith-s-paint-native-resources-reopen-v1/RESULT.v1.json'
 resources_close=read(resources_close_path)
 if resources_close.get('status')=='ten_native_resources_independently_reopened_120_mips_exact':
  data['current_work_sub_lot']='L03.TOWNSMITH.SHADER.S_PAINT.TEXTURES.ADMISSION'
  data['current_work_state']='Dix ressources natives assemblees et reouvertes : 120 mips / 78 293 832 octets exacts ; huit textures compressees et 864 mesures avant/apres bit-exactes, reglages restaures ; sources et GUID conserves ; 25 tests de contrat passes ; tests moteur : '+ten_texture_engine.get('status','pending')+' ; admission, sampling Forge, normales, rendu et Studio restants'
  data['next_action']='Admettre explicitement les deux protocoles natifs dans le stockage commun sans reecrire les captures ; verifier les dix ressources puis comparer sampling et normales source/Forge sur GPU'
  data['townsmith_native_resources']={'proof':str(resources_close_path),'resources_verified':10,'mips_verified':120,'bytes_verified':78293832,'complete_assets_delivered_increment':0,'forge_editor_verified':False}
  text=text.replace('| L01.LOD.01 |','| L03.TOWNSMITH.SHADER.S_PAINT.TEXTURES.BC_STORAGE | Extraction compressee isolee | technique verifie | 8 textures / 96 mips ; 864 observations bit-exactes ; 8 reglages restaures ; V1 refuse conserve | [Preuve V2](../townsmith-s-paint-compressed-storage-reopen-v2/RESULT.v1.json) |\n| L03.TOWNSMITH.SHADER.S_PAINT.TEXTURES.TRANSPORT | Ressources natives composees | reouverture verifiee | 10 textures / 120 mips / 78 293 832 octets ; admission moteur restante | [Preuve](../townsmith-s-paint-native-resources-reopen-v1/RESULT.v1.json) |\n| L01.LOD.01 |')
  key+=':'+resources_close['status']
 candidate_close=read(W/'native-texture-capture-contract-reopen-v1/RESULT.v1.json')
 if candidate_close.get('status')=='isolated_native_capture_validator_reopened_eight_tests_ten_actual_resources':
  data['current_work_state']+=' ; candidat admission isole : 8 tests passes, 10 ressources acceptees et 40 mutations refusees ; raccordement aux meta et installation commune restants'
  data['native_capture_candidate_proof']=str(W/'native-texture-capture-contract-reopen-v1/RESULT.v1.json')
  key+=':'+candidate_close['status']
 identity_close=read(W/'native-texture-identity-contract-reopen-v1/RESULT.v1.json')
 if identity_close.get('status')=='real_common_parent_candidate_reopened_29_tests_ten_original_resources_guid_binding_verified':
  data['current_work_state']+=' ; contrat parent reel en isolation : 29 tests passes, GUID/meta verifies sur les 10 ressources originales, 70 mutations refusees ; installation commune et GPU restants'
  data['native_identity_candidate_proof']=str(W/'native-texture-identity-contract-reopen-v1/RESULT.v1.json')
  data['next_action']='Terminer les tests moteur actifs, verifier la generalite des statuts de capture, installer le contrat commun puis rejouer les tests du workspace et comparer sampling/normales sur GPU'
  key+=':'+identity_close['status']
 identity_close=read(W/'native-texture-identity-contract-reopen-v2/RESULT.v1.json')
 if identity_close.get('status')=='real_common_parent_candidate_reopened_30_tests_ten_original_resources_guid_binding_verified':
  data['current_work_state']+=' ; contrat parent reel en isolation : 30 tests passes, GUID/meta verifies sur les 10 ressources originales, 80 mutations de ressources et deux protocoles inconnus refuses ; installation commune et GPU restants'
  data['native_identity_candidate_proof']=str(W/'native-texture-identity-contract-reopen-v2/RESULT.v1.json')
  data['next_action']='Terminer les tests moteur actifs, installer le contrat commun puis rejouer les tests du workspace et comparer sampling/normales sur GPU'
  key+=':'+identity_close['status']
 normal_source_close=read(W/'townsmith-s-paint-native-normal-decode-reopen-v1/RESULT.v1.json')
 if normal_source_close.get('status')=='three_source_normal_profiles_reopened_324_native_decodes_original_texels_exact':
  data['current_work_state']+=' ; reference normales Unity : 3 profils / 324 decodages reouverts, texels et alpha source identiques ; Forge GPU restant'
  data['townsmith_native_normal_source_proof']=str(W/'townsmith-s-paint-native-normal-decode-reopen-v1/RESULT.v1.json')
  key+=':'+normal_source_close['status']
 native_texture_gpu=read(W/'townsmith-s-paint-native-texture-gpu-control-reopen-v1/RESULT.v1.json')
 if native_texture_gpu.get('status')=='native_ten_texture_gpu_load_and_three_normal_decode_controls_verified':
  data['current_work_state']='Dix ressources natives / 120 mips uploades sur GPU D3D12 ; 1 080 lectures et 324 decodages normaux sans ecart (864 references GPU Unity, 216 references bytes UNORM) ; contrat candidat generique 30 tests passes ; tests moteur : '+ten_texture_engine.get('status','pending')+' ; installation commune, ABI normales, sampling filtre, rendu complet et Studio restants'
  data['native_texture_gpu_control_proof']=str(W/'townsmith-s-paint-native-texture-gpu-control-reopen-v1/RESULT.v1.json')
  data['next_action']='Installer le contrat commun apres les tests moteur actifs, mapper explicitement les profils de normales dans ABI commun, puis verifier sampling filtre, rendu du materiau et usage Studio'
  key+=':'+native_texture_gpu['status']
 native_texture_gpu=read(W/'townsmith-s-paint-native-texture-gpu-control-reopen-v2/RESULT.v1.json')
 if native_texture_gpu.get('status')=='native_ten_texture_gpu_load_and_three_normal_decode_controls_verified':
  data['current_work_state']='Dix ressources natives / 120 mips uploades sur GPU D3D12 ; 1 080 lectures et 324 decodages normaux sans ecart avec modes communs (source RG/AG 1 vers ABI 2) (864 references GPU Unity, 216 references bytes UNORM) ; contrat candidat generique 30 tests passes ; tests moteur : '+ten_texture_engine.get('status','pending')+' ; installation commune, binding uniforms reel, sampling filtre, rendu complet et Studio restants'
  data['native_texture_gpu_control_proof']=str(W/'townsmith-s-paint-native-texture-gpu-control-reopen-v2/RESULT.v1.json')
  data['next_action']='Installer le contrat commun apres les tests moteur actifs, verifier le binding effectif des uniforms de normales, puis verifier sampling filtre, rendu du materiau et usage Studio'
  key+=':'+native_texture_gpu['status']
 filtered_texture_close=read(W/'townsmith-s-paint-native-filtered-gpu-reopen-v2/RESULT.v1.json')
 if filtered_texture_close.get('status')=='ten_native_filtered_texture_controls_verified':
  data['current_work_sub_lot']='L03.TOWNSMITH.SHADER.S_PAINT.CONTRAT.INSTALLATION'
  data['current_work_state']='Dix textures : lectures directes 1 080, controles filtres a LOD explicite 1 080 et normales filtrees 324 sans ecart ; modes communs RG/AG verifies ; metadata Bilinear preservees, interpolation mip native observee ; candidat contrat commun 30 tests passes ; tests moteur : '+ten_texture_engine.get('status','pending')+' ; installation, binding reel, sampling implicite, rendu complet et Studio restants'
  data['filtered_texture_gpu_proof']=str(W/'townsmith-s-paint-native-filtered-gpu-reopen-v2/RESULT.v1.json')
  data['next_action']='Terminer les tests moteur actifs, installer le contrat commun et verifier le binding effectif ; confirmer sampling implicite et politique source dans le rendu du materiau, puis Studio'
  key+=':'+filtered_texture_close['status']
 common_install_queue=read(W/'native-texture-capture-common-install-v1/RUN-RESULT.v1.json')
 common_regressions=read(W/'native-texture-capture-common-regressions-reopen-v1/RESULT.v1.json')
 if common_regressions.get('tests_passed')==33:
  data['current_work_state']+=' ; regressions permanentes preparees : 33 tests candidat passes ; installation sequentielle : '+common_install_queue.get('status','pending')
  data['common_install_queue']=str(W/'native-texture-capture-common-install-v1/RUN-RESULT.v1.json')
  key+=':'+common_regressions['status']+':'+common_install_queue.get('status','pending')
 texture_packets=read(W/'townsmith-native-texture-contract-packets-seal-v1/RESULT.v1.json')
 if texture_packets.get('texture_contracts_reopened')==10:
  data['current_work_state']+=' ; dix contrats textures serialises et reouverts, 40 references buffers verifiees ; 34 tests avec les modules candidats communs passent ; admission moteur restante'
  data['texture_contract_packets_proof']=str(W/'townsmith-native-texture-contract-packets-seal-v1/RESULT.v1.json')
  key+=':'+texture_packets['status']
 texture_set_close=read(W/'townsmith-s-paint-texture-set-reopen-v1/RESULT.v1.json')
 if texture_set_close.get('texture_slots_verified')==10:
  data['current_work_state']='Jeu de dix textures assemble/reouvert : 32 vues partagees, bindings canoniques, 134 operations arithmetiques conservees ; 35 tests candidat passent ; lectures et filtrage explicite source/GPU verifies ; installation sequentielle : '+common_install_queue.get('status','pending')+' ; passes de rendu, sampling implicite et Studio restants'
  data['texture_set_proof']=str(W/'townsmith-s-paint-texture-set-reopen-v1/RESULT.v1.json')
  key+=':'+texture_set_close['status']
 canonical_wgsl_close=read(W/'townsmith-s-paint-canonical-wgsl-check-v1/RESULT.v1.json')
 if canonical_wgsl_close.get('status')=='canonical_arithmetic_wgsl_composition_passed':
  data['current_work_state']+=' ; composition arithmetique WGSL validee pour deux groupes de bindings (sonde non executee)'
  data['canonical_wgsl_proof']=str(W/'townsmith-s-paint-canonical-wgsl-check-v1/RESULT.v1.json')
  key+=':'+canonical_wgsl_close['status']
 host_adapter_close=read(W/'townsmith-s-paint-material-host-adapter-v1/RESULT.v1.json')
 if host_adapter_close.get('status')=='material_common_uniform_host_adapter_composed':
  recovery=read(W/'townsmith-ten-texture-engine-recovery-v1/RUN-RESULT.v1.json')
  engine_retry=read(W/'source-graph-ten-texture-engine-check-v2/RUN-RESULT.v1.json')
  install_retry=read(W/'native-texture-capture-common-install-v2/RUN-RESULT.v1.json')
  retry_state=install_retry.get('status',engine_retry.get('status',recovery.get('status','pending')))
  data['current_work_state']='Dix textures / 32 vues partagees / 134 operations conservees, 35 tests candidats passes ; shader arithmetique et adaptateur uniforms communs composes (2 controles chacun). Test moteur v1 refuse : constructeur non eclaire incomplet, corrige sans ajouter de textures. Reprise : '+retry_state+' ; transport UV0 complet, passes de rendu, sampling implicite et Studio restants'
  data['material_host_adapter_proof']=str(W/'townsmith-s-paint-material-host-adapter-v1/RESULT.v1.json')
  data['engine_recovery_proof']=str(W/'townsmith-ten-texture-engine-recovery-v1/RUN-RESULT.v1.json')
  key+=':'+host_adapter_close['status']+':'+retry_state
 active_lot=data.get('current_work_sub_lot',active_lot);active_state=data.get('current_work_state',active_state)
 data['active_sub_lot']=active_lot
 # Keep the prominent work summary consistent with the latest measured sub-lot.
 if specular_source:
  lines=text.splitlines()
  lines=[('**Travail actuel : '+data['current_work_sub_lot']+'.** '+data['current_work_state']+'.') if line.startswith('**Travail actuel :') else ('**Lot en cours : '+active_lot.split('.')[0]+' — '+('conversions statiques Townsmith.' if active_lot.startswith('L03.') else 'éclairage et rendu des assets source.')+'**') if line.startswith('**Lot en cours :') else line for line in lines]
  text='\n'.join(lines)+'\n'
 write(T/'SUIVI_SIMPLE.md',text)
 # Persist after all sub-lot receipts have updated the state, not before HDRI.
 write(T/'SUIVI_SIMPLE.v1.json',json.dumps(data,indent=2,ensure_ascii=False))
 if not last.exists() or last.read_text()!=key:
  previous_key=last.read_text() if last.exists() else ''
  with (T/'JOURNAL.md').open('a',encoding='utf-8') as f:f.write(f'\n- {now} — {active_lot} : {active_state}. Dernier lot host : {state}, {data["verified_controls"]}/6 contrôles et {data["verified_shadow_routes"]}/4 routes. [Suivi court](SUIVI_SIMPLE.md). Pas de qualification complète ni visuelle éditeur.\n')
  if probe_close and probe_close['status'] not in previous_key:
   with (T/'JOURNAL.md').open('a',encoding='utf-8') as f:f.write(f'\n- {now} — L01.ECLAIRAGE.SONDE : technique clos, 2 tests natifs, 12 cas GPU / 34 526 pixels, Web vérifié. Prochain sous-lot : L01.ECLAIRAGE.BRDF. [Preuve](../source-diffuse-probe-install-v1/RESULT.v1.json).\n')
  if (brdf_storage or brdf_gpu_prepared) and data.get('current_work_sub_lot')=='L01.ECLAIRAGE.BRDF':
   with (T/'JOURNAL.md').open('a',encoding='utf-8') as f:f.write(f'\n- {now} — L01.ECLAIRAGE.BRDF : {data["current_work_state"]}, {data["brdf_storage_tests_verified"]}/4 tests natifs validés. [Tests](../source-brdf-storage-check-v2/RUN-RESULT.v1.json). Pas de qualification complète du rendu.\n')
  if brdf_close and brdf_close['status'] not in previous_key:
   with (T/'JOURNAL.md').open('a',encoding='utf-8') as f:f.write(f'\n- {now} — L01.ECLAIRAGE.BRDF : technique clos, 4 tests de stockage, 20 de contrat, 35 cas GPU / 70 observations, Web vérifié. Prochain sous-lot : L01.ECLAIRAGE.APPLICATION. [Clôture](../source-brdf-lookup-install-v1/RESULT.v1.json).\n')
  if source_exposure and source_exposure['status'] not in previous_key:
   with (T/'JOURNAL.md').open('a',encoding='utf-8') as f:f.write(f'\n- {now} — L01.ECLAIRAGE.APPLICATION : exposition Unity GPU réouverte, 2 états identiques ; facteur 0,0008138020639307797 à EV100 10, convention CPU Bevy identique. [Preuve](../firepit-source-exposure-reopen-v1/RESULT.v1.json). Éclairage complet et exposition cible GPU non qualifiés.\n')
  if sky_storage and sky_storage.get('status')=='actual_sky_cube_storage_and_residency_refusals_verified' and sky_storage['status'] not in previous_key:
   with (T/'JOURNAL.md').open('a',encoding='utf-8') as f:f.write(f'\n- {now} — L01.ECLAIRAGE.HDRI.STOCKAGE : 5/5 tests natifs vérifiés ; 13 mutations et éviction refusées. Contrat matériau et 182 lectures GPU Forge restants. [Preuve](../source-sky-cube-storage-check-v3/RUN-RESULT.v1.json). Pas de livraison complète supplémentaire.\n')
  if sky_binding and sky_binding['status']=='canonical_sky_binding_and_atomic_scene_preflight_verified' and sky_binding['status'] not in previous_key:
   with (T/'JOURNAL.md').open('a',encoding='utf-8') as f:f.write(f'\n- {now} — L01.ECLAIRAGE.HDRI.ADMISSION : {sky_binding["contract_tests_verified"]} tests de contrat et {sky_binding["frame_tests_verified"]} tests de frame vérifiés. Prochain sous-lot : 182 lectures GPU. [Preuve](../source-sky-cube-binding-check-v3/RUN-RESULT.v1.json). Pas de qualification du rendu complet.\n')
  if sky_gpu and sky_gpu['status']=='actual_gpu_source_sky_182_case_sampling_verified' and sky_gpu['status'] not in previous_key:
   with (T/'JOURNAL.md').open('a',encoding='utf-8') as f:f.write(f'\n- {now} — L01.ECLAIRAGE.HDRI.GPU : 182 cas / 364 observations, 34 526 pixels témoins, 862 épingles source inchangées, zéro canal RGB hors seuil. Prochain sous-lot : application spéculaire à la scène originale. [Preuve](../firepit-sky-cube-gpu-reopen-v1/RESULT.v1.json). Rendu complet et recette éditeur non qualifiés.\n')
  if sky_close and sky_close['status']=='explicit_source_sky_storage_binding_native_gpu_and_web_compile_technical_batch_verified' and sky_close['status'] not in previous_key:
   with (T/'JOURNAL.md').open('a',encoding='utf-8') as f:f.write(f'\n- {now} — L01.ECLAIRAGE.HDRI.TECHNIQUE : stockage, contrat, frame, 182 cas GPU et compilation Web vérifiés. Application spéculaire au modèle et recette éditeur restantes. [Clôture](../source-sky-cube-install-v1/RESULT.v1.json). Aucune livraison complète supplémentaire.\n')
  write(last,key)
 from render_lot_dashboard_v1 import render
 render()
 # Windows redirected consoles can use cp1252; files above remain UTF-8.
 print(json.dumps({k:v for k,v in data.items() if k!='sub_lots'},ensure_ascii=True))
if __name__=='__main__':main()
