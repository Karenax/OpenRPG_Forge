"""Publish a current, evidence-linked lot plan without changing historical receipts."""
from pathlib import Path
from datetime import datetime, timezone
import json

W=Path('F:/OpenRPG_ImportWork'); T=W/'SUIVI_LOTS_HIVEMIND'
def read(relative):
    p=W/relative
    return json.loads(p.read_text('utf-8-sig')) if p.exists() else {'status':'non_demarre'}
def write(p,text):
    tmp=p.with_suffix(p.suffix+'.tmp');tmp.write_text(text,'utf-8');tmp.replace(p)
def main():
    now=datetime.now(timezone.utc).isoformat()
    context=read('studio-source-context-native-v1/RUN-RESULT.v1.json')
    bootstrap=read('studio-source-bootstrap-native-v1/RUN-RESULT.v1.json')
    runtime=read('studio-source-runtime-native-v1/RUN-RESULT.v1.json')
    clock=read('studio-source-clock-native-v1/RUN-RESULT.v1.json')
    web_dir=next('studio-current-source-wasm-check-v'+str(v) for v in [15,14,13,12,11,10] if (W/('studio-current-source-wasm-check-v'+str(v)+'/JOB.v1.json')).exists())
    web=read(web_dir+'/RUN-RESULT.v1.json')
    entry=read('studio-source-preview-entry-native-v1/RUN-RESULT.v1.json')
    device_mount=read('studio-source-actual-device-mount-reopen-v1/RESULT.v1.json')
    pipeline_run=read('studio-source-pipeline-probe-v1/RUN-RESULT.v1.json')
    pipeline_result=read('studio-source-pipeline-probe-v1/PIPELINE-RESULT.v1.json')
    pipeline_reopen=read('studio-source-pipeline-probe-reopen-v1/RESULT.v1.json')
    shadow_reopen=read('studio-source-shadow-probe-reopen-v1/RESULT.v1.json')
    indexed=read('house-bench-native-thumbnail-index-v1/RESULT.v1.json')
    native_library=read('house-bench-current-native-library-probe-v2/RESULT.v1.json')
    house_converted=read('house-geometry-batches-independent-reopen-v1/RESULT.v1.json')
    installed_images=indexed.get('images_installed',0)
    missing_images=948-indexed.get('cards_updated',0)
    image_batch=read('house-source-thumbnail-batch-0001-0008-v1/RUN-RESULT.v1.json')
    image_batch_index=read('house-source-thumbnail-batch-0001-0008-index-v1/RESULT.v1.json')
    image_batch_reader=read('house-source-thumbnail-batch-0001-0008-native-reader-v1/RESULT.v1.json')
    missing_images-=image_batch_index.get('cards_updated',0)
    thumbnail_dir=next('unity-library-source-thumbnail-house-bench-v'+str(v) for v in [8,7,6,5,4,3,2] if (W/('unity-library-source-thumbnail-house-bench-v'+str(v)+'/JOB.v1.json')).exists())
    house_dir='house-native-prefab-hierarchy-v2' if (W/'house-native-prefab-hierarchy-v2/JOB.v1.json').exists() else 'house-native-prefab-hierarchy-v1'
    images=read(thumbnail_dir+'/RUN-RESULT.v1.json')
    image_review=read(thumbnail_dir+'/VISUAL-REVIEW.v1.json')
    image_reopen=read('house-source-thumbnail-reopen-v1/RESULT.v1.json')
    geometry=read('house-native-mesh-streams-v1/RUN-RESULT.v1.json')
    geometry_reopen=read('house-native-mesh-streams-reopen-v1/RESULT.v1.json')
    house=read(house_dir+'/RUN-RESULT.v1.json')
    house_reopen=read('house-native-prefab-hierarchy-reopen-v1/RESULT.v1.json')
    house_verified=house_reopen['status']=='fifty_house_native_hierarchies_independently_reopened'
    lots=[
      ('L00','Stockage, sources et licences','Clos pour le snapshot contrôlé','132 961 fichiers / 603 276 106 799 octets contrôlés ; conserver les preuves et contrôler chaque nouvelle livraison.'),
      ('L01','Chargement, aperçu et placement','Actif','Rocks/FirePit : contexte dynamique, passes et LOD, montage dans Studio, mémoire et rendu GPU.'),
      ('L02','Dungeon','Partiel','42 captures HDRP conservées ; finir prefabs complets, matériaux et rendu.'),
      ('L03','Conversions statiques','Partiel','Townsmith : résoudre 6 bindings ; Villa : 19 scènes restantes ; House : 50 hiérarchies et 48 dérivés géométriques vérifiés, assemblage GLB et matériaux restants.'),
      ('L04','Matériaux, textures et feuillage','Partiel','Contrôler shaders, transparence, normales, ombres, déformation et LOD pour chaque groupe.'),
      ('L05','Rigs et animations','Partiel','Contrôler squelette, poids, clips, poses et déformation dans le moteur.'),
      ('L06','Effets','Partiel','FirePit : simulation et rendu ; Blood et Dust : traiter les graphes par groupes d’IDs et vérifier dans le temps.'),
      ('L07','Sprites, terrains et autres formats','Ouvert','Conversion et contrôle adaptés à chaque type ; conserver les refus explicites.'),
      ('L08','Images et bibliothèque dans l’éditeur','Partiel',f'{missing_images} fiches sans image ; banc : deux images indexées et lecteur natif contrôlé ; recette visuelle éditeur, aperçu, placement et réouverture restants.'),
      ('L09','Réconciliation finale','Ouvert','Rapprocher chaque ID des 48 packs/variantes avec ses preuves de livraison et son état Studio/runtime.')]
    text=f'''# Plan de travail Hivemind — lots et avancement courant

Actualisé : {now}

Bibliothèque finale : **F:\\OpenRPG_AssetVault**. Conversions, caches et preuves : **F:\\OpenRPG_ImportWork**.

**Vue courte des lots et progression courante : [SUIVI_SIMPLE.md](SUIVI_SIMPLE.md).** Les reçus historiques ci-dessous conservent leur périmètre daté.

Périmètre : **48 packs/variantes**. Les **14 219 déclarations de modèles** et **12 516 fiches support** ne constituent pas un compteur de conversions entièrement validées.

| Lot | Travail | État | Reste à faire / preuve acquise |
|---|---|---|---|
'''
    text+=''.join('| '+' | '.join(r)+' |\n' for r in lots)
    if image_batch['status']!='non_demarre':
        text+=f"\n**L08.IMAGE.HOUSE.02 — huit fiches House, seize vues attendues.** {image_batch.get('cards_captured',0)}/8 captures terminales ; {image_batch_index.get('cards_updated',0)}/8 fiches indexées ; {image_batch_index.get('cards_refused',0)} fiches refusées ; {image_batch_reader.get('cards_verified',0)} fiches contrôlées dans le lecteur natif. [IDs et étapes](../house-source-thumbnail-batch-0001-0008-v1/LOT.md.json) · [Capture](../house-source-thumbnail-batch-0001-0008-v1/RUN-RESULT.v1.json). La capture seule ne vaut pas acceptation ni indexation.\n"
        if image_batch_index.get('cards_updated',0):
            text+='\n**Résultat partiel de ce sous-lot : cinq fiches, dix PNG indexés et contrôlés ; trois falaises refusées.** Le nuage, le cyprès et les trois clôtures sont recherchables, leurs détails/licences et images sont lus par le lecteur natif actuel de Studio. Les falaises restent trop sombres à EV4/EV8 ; les essais EV0/EV1 présentent des reflets saturés. Toutes les preuves sont conservées, sans abaisser les contrôles. Les sources restent non converties et non plaçables ; aucun rendu visuel de l’éditeur n’est déduit du contrôle natif. [Revue et refus](../house-source-thumbnail-batch-0001-0008-review-v3/RESULT.v1.json) · [Transaction et delta de stockage](../house-source-thumbnail-batch-0001-0008-index-v1/RESULT.v1.json) · [Lecteur natif](../house-source-thumbnail-batch-0001-0008-native-reader-v1/RESULT.v1.json).\n'
            if read('house-source-thumbnail-batch-0001-0008-delta-reopen-v1/RESULT.v1.json').get('cards_verified')==5:
                text+='\nRéconciliation indépendante de ce delta : **douze nouveaux fichiers contrôlés**, dont dix PNG, un catalogue immuable et une transaction ; un pointeur source modifié intentionnellement, registre des modèles inchangé et 12 511 autres fiches source identiques. **942 absences d’image principale** déduites de la baseline fermée et des deux deltas contrôlés. [Preuve indépendante](../house-source-thumbnail-batch-0001-0008-delta-reopen-v1/RESULT.v1.json). Ce contrôle ne constitue pas un nouveau scan global du Vault.\n'
    text+=f'''
## Sous-lots actuellement engagés

| Sous-lot | Attendu | Vérifié actuellement | État enregistré | Preuve |
|---|---|---|---|---|
| L01.CONTEXTE.01 — Rocks et FirePit | 7 tests du contexte dynamique, puis rendu | {context.get('actual_context_tests_passed',0)}/7 tests dédiés | `{context['status']}` | [Tests](../studio-source-context-native-v1/RUN-RESULT.v1.json) |
| L01.CONTEXTE.02 — initialisation du FirePit réel | 3 tests de propagation et refus | {bootstrap.get('bootstrap_tests_passed',0)}/3 | `{bootstrap['status']}` | [Tests](../studio-source-bootstrap-native-v1/RUN-RESULT.v1.json) |
| L01.HOST.01 — cycle de vie commun | 5 tests du host réel | {runtime.get('runtime_tests_passed',0)}/5 | `{runtime['status']}` | [Tests](../studio-source-runtime-native-v1/RUN-RESULT.v1.json) |
| L01.INTEGRATION.01 — installation et horloge | 2 tests d’ordonnancement et de pause | {clock.get('clock_tests_passed',0)}/2 | `{clock['status']}` | [Tests](../studio-source-clock-native-v1/RUN-RESULT.v1.json) |
| L01.INTEGRATION.02 — entrée de l’aperçu source | 3 contrôles d’admission ; puis montage GPU | {entry.get('preview_entry_tests_passed',0)}/3 | `{entry['status']}` | [Tests](../studio-source-preview-entry-native-v1/RUN-RESULT.v1.json) |
| L01.GPU.01 — FirePit sur le device Bevy réel | Montage et résidence des textures ; états/cycle de vie | {device_mount.get('nodes_verified',0)} nœuds, {device_mount.get('primitives_verified',0)} primitives, {device_mount.get('gpu_images_resident_verified',0)} textures GPU | `{device_mount['status']}` | [Réouverture](../studio-source-actual-device-mount-reopen-v1/RESULT.v1.json) |
| L01.GPU.02 — pipelines source du FirePit | Sollicitation dans une scène privée, compilation et erreurs | {pipeline_result.get('source_pipeline_count',0)} pipelines observés | `{pipeline_result['status'] if pipeline_result['status']!='non_demarre' else pipeline_run['status']}` | [Probe](../studio-source-pipeline-probe-v1/RUN-RESULT.v1.json) |
| L01.GPU.03 — ombre et états spécialisés | Shader d’ombre et sous-ensemble des états ; intégration séparée | {shadow_reopen.get('source_pipelines_verified',0)} pipelines, {shadow_reopen.get('shadow_routes_verified',0)} routes | `{shadow_reopen['status']}` | [Réouverture](../studio-source-shadow-probe-reopen-v1/RESULT.v1.json) |
| L08.IMAGE.HOUSE.01 — banc source | 2 vues originales, inspection puis indexation | {image_reopen.get('images_verified',0)}/2 images contrôlées ; {installed_images} fichiers installés | `{indexed['status'] if installed_images else image_review['status']}` | [Indexation](../house-bench-native-thumbnail-index-v1/RESULT.v1.json) |
| L03.HOUSE.HIERARCHIE.01 | 50 racines prefab, dont un effet | {50 if house_verified else house.get('captured_prefab_roots',0)}/50 {'vérifiées' if house_verified else 'captures'} | `{house_reopen['status'] if house_verified else house['status']}` | [Hiérarchies](../house-native-prefab-hierarchy-reopen-v1/RESULT.v1.json) |
| L03.HOUSE.GEOMETRIE.01 | 48 meshes de rendu exacts | {geometry_reopen.get('meshes_verified',0)}/48 réouverts | `{geometry_reopen['status'] if geometry_reopen['status']!='non_demarre' else geometry['status']}` | [Capture](../house-native-mesh-streams-v1/RUN-RESULT.v1.json) |
| L03.HOUSE.CONVERSION.01–03 | 3 lots de 16 dérivés de géométrie | {house_converted.get('meshes_verified',0)}/48 | `{house_converted['status']}` | [Réouverture indépendante](../house-geometry-batches-independent-reopen-v1/RESULT.v1.json) |

Étape précédente L01.ETATS.01 : **7/7 tests natifs réussis**, 95 nœuds, 63 renderers et 14 groupes LOD conservés. [Clôture datée](../studio-source-state-install-v1/RESULT.v2.json). Cela ne valide pas encore le rendu GPU ou l’usage dans l’éditeur.

Contexte dynamique installé : [empreintes des trois modules et des deux définitions SDK](../studio-source-context-install-v1/INSTALLED.v1.json). Les résultats natifs restent à obtenir ; le montage visible dans Studio et le pilotage LOD restent ouverts.

Étape suivante préparée : [contextes initiaux issus des hiérarchies admises](../studio-source-context-bootstrap-v1/PREPARED.v1.json). Candidat conservé sur F, non installé et non testé. Vérifier la composition initiale contre les transformations Bevy avant le raccordement aperçu/placement.

Trois tests natifs sont en file derrière le contrôleur précédent, observé vivant et identifié par son heure de création. Ils utilisent le GLB FirePit réel et les modules Bevy/moteur du produit. [Sources et paramètres épinglés](../studio-source-bootstrap-native-v1/PREPARED.v1.json). Ce contrôle ne dessine pas encore la scène et ne qualifie pas le GPU.

Gestion commune des hosts : [candidat de cycle de vie](../studio-source-scene-runtime-v1/PREPARED.v1.json), préparé sur F et non installé. Horloge explicite, politiques de coordonnées distinctes par racine, mise à jour après propagation et libération des handles pour les racines disparues. Tests et raccordement au viewport encore requis.

Cinq tests du cycle de vie sont en file après la réussite du bootstrap ; ils contrôlent les matériaux réels, les refus d’horloge, les repères et la libération du host, sans supprimer les assets encore empruntés. [Préparation et identité du contrôleur précédent](../studio-source-runtime-native-v1/PREPARED.v1.json).

Townsmith — six bindings toujours ouverts : [préflight des API dans Unity installé](../townsmith-frame-debugger-api-preflight-v1/RESULT.v1.json). Les données d’événement exposent mesh, sous-mesh et shader/passe, mais les méthodes de capture hors écran récentes sont absentes. L’observation d’une vraie frame reste requise ; aucune affectation de matériau supplémentaire n’est inventée.

Compilation Web v10 : sortie `{web.get('actual_exit_code','en cours')}`, état `{web['status']}`. {len(web.get('changed_source_files',[]))} fichiers ont changé pendant le contrôle : compilation réussie et qualification du snapshot courant restent distinctes. [Reçu](../studio-current-source-wasm-check-v10/RUN-RESULT.v1.json).

## Ordre d’exécution

1. Terminer **L01.CONTEXTE.01**, enregistrer le résultat et poursuivre rendu, LOD, aperçu et placement.
2. Achever les captures House déjà engagées, contrôler les images et les hiérarchies ; convertir ensuite par groupes explicites de prefabs.
3. Résoudre les **6 bindings Townsmith**, puis les **19 scènes Villa** ; ne pas reconvertir les géométries déjà vérifiées.
4. Traiter **Dungeon et matériaux/feuillage**, puis **rigs/animations**, **effets**, **autres formats**, par groupes d’IDs.
5. Pour chaque livraison : image, fiche, recherche/filtres, aperçu, placement, édition, sauvegarde/réouverture et runtime applicable.
6. Réconcilier tous les IDs et preuves dans L09.

## Critères et suivi

Chaque sous-lot enregistre : liste exacte d’IDs, total attendu, nombre vérifié, résultat, chemins des sorties sur F, reçu et prochaine action. Les étapes restent distinctes : **conversion → vérification du dérivé → matériaux/animation/effets → image et indexation → validation Studio/runtime**.

Un refus est conservé avec sa cause. Une source support, un test ou une compilation ne compte pas comme un asset livré. Après chaque résultat terminal, actualiser ce tableau et ajouter une entrée datée au journal. Aucune nouvelle autorisation nécessaire entre les lots déjà autorisés.

[Inventaire par pack](DECOUPAGE_OPERATIONNEL.md) · [Journal](JOURNAL.md) · [Stockage contrôlé](../vault-current-final-review-v4/RESULT.v1.json).

Recette visuelle de l’éditeur : le dernier essai du navigateur automatisé échoue avant accès à la page, dans le démarrage du sandbox Node. [Échec observé](../editor-browser-observation-unavailable-v3/RESULT.v1.json). Les conversions/tests continuent ; cet échec ne constitue aucune preuve d’accessibilité ou de défaut de la bibliothèque elle-même.
'''
    text=text.replace('../unity-library-source-thumbnail-house-bench-v2/RUN-RESULT.v1.json','../'+thumbnail_dir+'/RUN-RESULT.v1.json').replace('../house-native-prefab-hierarchy-v1/RUN-RESULT.v1.json','../'+house_dir+'/RUN-RESULT.v1.json')
    text=text.replace('Compilation Web v10 :',f'Compilation Web {web_dir.rsplit("-",1)[-1]} :').replace('../studio-current-source-wasm-check-v10/RUN-RESULT.v1.json','../'+web_dir+'/RUN-RESULT.v1.json')
    if thumbnail_dir.endswith('v3'):
        text+='\nReprise House : le premier lancement a refusé HDRP car son asset de pipeline référencé n’avait pas été copié. Deux fichiers existants (asset et meta) recopiés avec SHA ; même projet/cache réutilisé. [Préparation de réparation](../unity-library-source-thumbnail-house-bench-v3/PREPARED.v1.json) · [Refus conservé](../unity-library-source-thumbnail-house-bench-v2/RUN-RESULT.v1.json).\n'
    if house_reopen['status']=='fifty_house_native_hierarchies_independently_reopened':
        text+='\n**L03.HOUSE.HIERARCHIE.01 vérifié : 50/50 hiérarchies réouvertes indépendamment**, 97 nœuds, 50 renderers dont 49 statiques, 48 meshes uniques, 27 identités de matériaux et 36 colliders ; aucun script manquant déclaré. Identités, poses locales/composées, états actifs et sources SHA vérifiés. Géométrie GLB, matériaux/rendu, LOD et usage produit non qualifiés. [Réouverture](../house-native-prefab-hierarchy-reopen-v1/RESULT.v1.json).\n'
    if thumbnail_dir.endswith('v5'):
        text+='\nImages House : les deux captures des étapes v3 et v4 sont refusées visuellement pour saturation blanche ; rien installé dans le Vault. HDRP réinitialise l’intensité au montage de son composant. L’étape v5 applique l’intensité après cette initialisation et la consigne dans le rapport natif. [Refus v3](../unity-library-source-thumbnail-house-bench-v3/VISUAL-REVIEW.v1.json) · [Refus v4](../unity-library-source-thumbnail-house-bench-v4/VISUAL-REVIEW.v1.json) · [Reprise v5](../unity-library-source-thumbnail-house-bench-v5/PREPARED.v1.json).\n'
        visual=read(thumbnail_dir+'/VISUAL-REVIEW.v1.json')
        if visual['status']=='source_thumbnail_candidates_rejected_by_agent_visual_review':
            text+='\n**Images v5 également refusées** : intensité finale 1 vérifiée dans le rapport, mais surface blanche sans détails du bois. Poursuivre diagnostic matériau/dépendances ; aucune image installée. [Revue agent](../unity-library-source-thumbnail-house-bench-v5/VISUAL-REVIEW.v1.json).\n'
    if thumbnail_dir.endswith('v7'):
        text+='\nImages House : **v6 refusé visuellement**, malgré huit passes source compilées et sept textures chargées avec identité native. Le diagnostic v7 utilise une caméra perspective de 30 degrés et mesure maximum linéaire / pixels saturés avant PNG. Shader, matériau et textures source conservés ; aucune image installée. [Refus v6](../unity-library-source-thumbnail-house-bench-v6/VISUAL-REVIEW.v1.json) · [Diagnostic v7](../unity-library-source-thumbnail-house-bench-v7/PREPARED.v1.json).\n'
    if thumbnail_dir.endswith('v8'):
        text+='\nImages House : **v7 refusé pour saturation**. Les valeurs linéaires atteignent 200 et 168 avant PNG, malgré huit passes compilées et sept textures chargées. L’étape v8 applique une exposition fixe EV8 et le tonemapping Neutral du SDK HDRP dans une scène de capture privée. Il s’agit de paramètres de présentation ; shader, matériaux et textures source restent conservés. Aucun rendu produit ou image installée ne sont déduits de cette capture. [Refus v7](../unity-library-source-thumbnail-house-bench-v7/VISUAL-REVIEW.v1.json) · [Préparation v8](../unity-library-source-thumbnail-house-bench-v8/PREPARED.v1.json).\n'
    if image_reopen['status']=='two_house_native_source_thumbnails_independently_reopened_agent_reviewed':
        text+='\n**Images du banc : 2/2 contrôlées et acceptées par revue agent pour indexation**, bois peint bleu et usure visibles, aucun pixel RGB saturé. PNG, SHA, identités source et paramètres de présentation réouverts indépendamment. Revue humaine, indexation Vault et visibilité Studio encore ouvertes. [Preuve](../house-source-thumbnail-reopen-v1/RESULT.v1.json).\n'
    if geometry_reopen['status']=='48_house_native_render_mesh_streams_independently_reopened':
        text+=f"\n**Géométrie House : 48/48 meshes natifs réouverts**, {geometry_reopen['vertices']} sommets et {geometry_reopen['triangles']} triangles ; attributs présents/absents et indices contrôlés. Aucun attribut manquant généré. Conversion GLB, matériaux et livraison produit restantes. [Preuve](../house-native-mesh-streams-reopen-v1/RESULT.v1.json).\n"
    if context.get('actual_context_tests_passed',0)==7:
        text=text.replace('Les résultats natifs restent à obtenir ;','Les sept tests dédiés et les tests existants ont réussi ;')
        text=text.replace('1. Terminer **L01.CONTEXTE.01**, enregistrer le résultat et poursuivre rendu, LOD, aperçu et placement.','1. **L01.CONTEXTE.01 vérifié (7/7)** ; poursuivre initialisation, cycle de vie, rendu, LOD, aperçu et placement.')
    if bootstrap.get('bootstrap_tests_passed',0)==3:
        text=text.replace('Candidat conservé sur F, non installé et non testé. Vérifier la composition initiale contre les transformations Bevy avant le raccordement aperçu/placement.','Candidat conservé sur F, non installé ; trois tests réussis contre les transformations Bevy du FirePit réel. Le raccordement aperçu/placement reste requis.')
        text=text.replace('Trois tests natifs sont en file derrière le contrôleur précédent, observé vivant et identifié par son heure de création.','Les trois tests natifs ont réussi après le contrôleur précédent : 31 tests réussis au total, sources épinglées inchangées.')
    if runtime['status']=='testing_actual_shared_host_lifecycle':
        text=text.replace('Cinq tests du cycle de vie sont en file après la réussite du bootstrap ;','Cinq tests du cycle de vie sont en cours après la réussite du bootstrap ;')
    if runtime.get('runtime_tests_passed',0)==5:
        text=text.replace('Tests et raccordement au viewport encore requis.','Cinq tests du cycle de vie réussis ; installation et raccordement au viewport encore requis.')
        text=text.replace('Cinq tests du cycle de vie sont en file après la réussite du bootstrap ;','Les cinq tests du cycle de vie ont réussi après le bootstrap : 36 tests natifs réussis au total ;')
        text+='\n**Prochain sous-lot L01.INTEGRATION.01** : installer les candidats validés d’initialisation/cycle de vie puis raccorder aperçu et placement à leur host commun. Contrôler horloge et transformations ; garder le rendu, les passes, ombres et LOD non qualifiés jusqu’aux preuves applicables. Les tests natifs réussis ne prouvent pas une scène visible dans Studio.\n'
    if clock.get('clock_tests_passed',0)==2:
        text=text.replace('**Prochain sous-lot L01.INTEGRATION.01** : installer les candidats validés d’initialisation/cycle de vie puis raccorder aperçu et placement à leur host commun. Contrôler horloge et transformations ;','**L01.INTEGRATION.01 vérifié dans son périmètre** : candidats installés et horloge commune raccordée à l’enregistrement du viewport ; deux tests supplémentaires réussis, 38 tests natifs au total. [Installation et préimages](../studio-source-lifecycle-install-v2/INSTALLED.v1.json).\n\n**Prochain sous-lot L01.INTEGRATION.02** : raccorder les routes d’aperçu et de placement à leur host commun ;')
        text+='\nPortée du contrôle d’horloge : production installée compilée dans le harness natif avec les mêmes modules moteur/Bevy, temps courant/précédent, pause et matériau réel contrôlés. La coque Studio complète, WebGPU, montage visible et recette utilisateur restent à vérifier. [Preuve native](../studio-source-clock-native-v1/RUN-RESULT.v1.json).\n'
        text+='\nAprès ce contrôle, quatre fichiers Rust ont été normalisés en LF. Préimages conservées et contenu normalisé identique aux sources testées ; `git diff --check` passe sur les fichiers suivis concernés. [Équivalence et empreintes actuelles](../studio-source-lifecycle-line-ending-repair-v2/RESULT.v1.json). Les empreintes historiques des reçus de compilation restent conservées.\n'
    if house_converted.get('meshes_verified',0)==48:
        text+='\n**Trois nouveaux lots House de 16 géométries contrôlés (48/48)** : compteurs natifs d’os/morphs et formats rapprochés, puis buffers convertis et réouverts contre les SHA natifs antérieurs. Pas de GLB assemblé ni de matériaux qualifiés. [Clôture indépendante](../house-geometry-batches-independent-reopen-v1/RESULT.v1.json).\n'
    if entry.get('preview_entry_tests_passed',0)==3:
        text+='\n**Entrée de l’aperçu source branchée et contrôlée (3/3, 41 tests natifs au total)** : le FirePit réel exige le GPU, une racine singulière est refusée avant allocation, la route ordinaire est conservée. La coque Studio WASM compile sur 1 039 fichiers inchangés pendant le contrôle. La scène source reste masquée ; rendu, LOD, cadrage visible et route de placement sont encore à raccorder/vérifier. [Tests](../studio-source-preview-entry-native-v1/RUN-RESULT.v1.json).\n'
    if device_mount.get('gpu_images_resident_verified',0)==3:
        text+='\n**L01.GPU.01 vérifié dans son périmètre : montage du FirePit réel sur Radeon RX 7800 XT / DX12 avec les modules actuels du produit.** Neuf nœuds, quatre primitives, quatre instances de matériau ; trois textures réellement résidentes sur le GPU, 12 niveaux chacune. Les 36 payloads de mip intégrés au GLB sont réouverts par SHA ; formats et dimensions rapprochés des ressources GPU. 850 empreintes de sources/contrôles inchangées pendant le contrôle. Les flags de renderer, données LOD, horloge valide/invalide, refus de racine singulière et libération sans suppression des matériaux empruntés sont contrôlés. La racine reste masquée : aucun dessin, shader GPU, driver LOD ou rendu visuel éditeur n’est qualifié. [Capture native](../studio-source-actual-device-mount-v2/NATIVE-RESULT.v1.json) · [Réouverture indépendante](../studio-source-actual-device-mount-reopen-v1/RESULT.v1.json).\n\n**Prochain sous-lot L01.GPU.02 :** solliciter les pipelines source dans une scène privée, relever les erreurs de compilation et contrôler les passes avant tout affichage produit. Conserver les files de rendu d’origine et le refus de fallback PBR.\n'
    if pipeline_reopen.get('pipelines_verified',0)==2:
        text+='\n**L01.GPU.02 vérifié pour compilation/création : deux pipelines source GPU créés sans erreur**, rendu principal et prépass profondeur/normales, sur le device Bevy DX12. Leurs UUID sont rapprochés des SHA complets et des octets des deux shaders intégrés au GLB. Les 852 empreintes produit/contrôles sont inchangées. La scène de diagnostic sollicite les quatre layouts statiques ; elle ne qualifie ni sélection LOD, ni image finale, ni affichage produit. Les métadonnées de file 2225 et de passes désactivées sont conservées ; l’ordre de rendu effectif reste à qualifier. [Pipelines observés](../studio-source-pipeline-probe-v1/PIPELINE-RESULT.v1.json) · [Réouverture indépendante](../studio-source-pipeline-probe-reopen-v1/RESULT.v1.json).\n\n**Prochain sous-lot L01.GPU.03 :** solliciter le pipeline source d’ombre, puis rapprocher les états de rasterisation/profondeur/couleur effectivement spécialisés des états source. La compilation seule ne prouve pas la fidélité du rendu.\n'
    if shadow_reopen.get('source_pipelines_verified',0)==3:
        text+='\n**L01.GPU.03 : compilation d’ombre et sous-ensemble d’états vérifiés sur GPU.** Trois pipelines source prêts, quatre routes dédiées d’ombre ; les SHA/UUID intégrés au GLB sont rapprochés. Cull, face avant, test/écriture de profondeur, blends couleur/alpha, masque couleur et alpha-to-coverage du pipeline principal contrôlés contre le contrat. Les états de prépass/ombre sont relevés ; la passe d’ombre est sans cible couleur et utilise un échantillon. 853 empreintes inchangées. **Les routes sont installées uniquement dans le probe privé** ; stencils source, ordre effectif des files, apparence d’ombre, LOD et recette éditeur restent ouverts. [États réels](../studio-source-shadow-probe-v1/PIPELINE-RESULT.v1.json) · [Réouverture indépendante](../studio-source-shadow-probe-reopen-v1/RESULT.v1.json).\n\n**Prochain sous-lot L01.OMBRES.01 :** raccorder les routes au host commun avec politique explicite de couche privée et refus conservant la racine masquée ; synchroniser les paramètres de matériau/horloge de l’ombre dans la même frame que le matériau source. Préserver les ombres explicitement désactivées et les ressources empruntées. Aucun affichage produit n’est activé avant qualification des étapes restantes.\n'
    if installed_images:
        text=text.replace('aucune image installée','deux fichiers image indexés sur la seule fiche du banc').replace('Revue humaine, indexation Vault et visibilité Studio encore ouvertes.','Indexation Vault vérifiée ; revue humaine et visibilité effective dans Studio encore ouvertes.')
        text+='\n**Banc HDRP : fiche mise à jour transactionnellement**, deux PNG installés sur F, image principale lue par les modules natifs courants de Studio ; recherche nom/pack/disponibilité et fiche/licence contrôlées, statut non plaçable conservé. Les 12 515 autres fiches source sont identiques. Couverture : 947 absences restantes après cette seule modification. [Indexation](../house-bench-native-thumbnail-index-v1/RESULT.v1.json) · [Lecteur natif](../house-bench-current-native-library-probe-v2/RESULT.v1.json). Le snapshot global L00 précédent reste une baseline datée ; cette nouvelle révision est conservée avec sa transaction et ses empreintes.\n'
    text=text.replace('../editor-browser-observation-unavailable-v3/RESULT.v1.json','../editor-browser-observation-unavailable-v4/RESULT.v1.json')
    if installed_images:text+='\nDelta de stockage de cette indexation : quatre nouveaux fichiers, un changement intentionnel du registre source, registre des modèles inchangé. Le contrôle global L00 antérieur ne devient pas un nouveau scan complet. [Réconciliation du delta](../house-bench-vault-index-delta-v1/RESULT.v2.json).\n'
    host_shadow_dir='studio-source-host-shadows-reopen-v2' if (W/'studio-source-host-shadows-v2').exists() else 'studio-source-host-shadows-reopen-v1'
    host_shadow=read(host_shadow_dir+'/RESULT.v1.json')
    if host_shadow.get('controls_verified')==6:
        text+='\n**L01.OMBRES.01 — étape native vérifiée : 4 routes, 6 contrôles.** API commune et appel aperçu installés ; horloges source/ombre dans la même frame, collision de couche, rollback partiel, idempotence et refus sans avance partielle contrôlés sur le FirePit réel. Empreintes réouvertes indépendamment. Compilation Web : `'+web['status']+'`. Apparence, stencils, ordre des passes, LOD et recette éditeur restent ouverts. [Réouverture](../'+host_shadow_dir+'/RESULT.v1.json) · [Suivi court](SUIVI_SIMPLE.md).\n'
    source_case=read('firepit-shadow-comparison-case-v1/RESULT.v1.json')
    if source_case.get('source_shadow_response_verified'):
        text+='\n**L01.OMBRES.02.SOURCE vérifié : 2 références Unity sans saturation et 524 288 pixels réouverts**, 24 dépendances source, mesh LOD0/material/compteurs rapprochés du GLB. Huit passes uniques compilées ; 29 016 pixels hors objet plus sombres avec ombre. Le cas caméra/lumière/exposition est épinglé pour la capture Forge suivante. La référence utilise Unity installé et un LOD0 forcé pour diagnostic ; version originale du projet, comparaison Forge et usage éditeur non qualifiés. [Référence et identité](../firepit-shadow-comparison-case-v1/RESULT.v1.json) · [Cas de comparaison](../firepit-shadow-comparison-case-v1/CASE.v1.json).\n'
    write(T/'TABLEAU_DE_BORD_LOTS.md',text)
    active='L01.CONTEXTE.01' if context.get('actual_context_tests_passed',0)!=7 else ('L01.CONTEXTE.02' if bootstrap.get('bootstrap_tests_passed',0)!=3 else ('L01.HOST.01' if runtime.get('runtime_tests_passed',0)!=5 else 'L01.INTEGRATION.01'))
    if clock.get('clock_tests_passed',0)==2:active='L01.INTEGRATION.02'
    if pipeline_run['status']!='non_demarre':active='L01.GPU.02'
    if shadow_reopen.get('source_pipelines_verified',0)==3:active='L01.OMBRES.01'
    if source_case.get('source_shadow_response_verified'):active='L01.OMBRES.02.FORGE'
    if image_batch['status']!='non_demarre' and image_batch_index.get('cards_updated',0)==0:active='L08.IMAGE.HOUSE.02'
    write(T/'PLAN_PAR_LOTS.v4.json',json.dumps(dict(updated_utc=now,goal_complete=False,pack_variants=48,lots=[dict(id=r[0],work=r[1],status=r[2],remaining=r[3]) for r in lots],active_sub_batch=active,context=context,bootstrap=bootstrap,runtime=runtime,web_summary={k:v for k,v in web.items() if k!='input_pins'},images=images,image_review=image_review,image_reopen_summary={k:v for k,v in image_reopen.items() if k!='input_pins'},house=house,geometry=geometry,geometry_reopen_summary={k:v for k,v in geometry_reopen.items() if k not in ['input_pins','rows']},last_image_batch=dict(cards_updated=image_batch_index.get('cards_updated',0),cards_refused=image_batch_index.get('cards_refused',0),native_reader_verified=image_batch_reader.get('cards_verified',0),status=image_batch_index['status'],missing_primary_images=missing_images)),ensure_ascii=False,indent=2)+'\n')
    state=json.loads((T/'ETAT.v1.json').read_text('utf-8-sig'));state.update(updated_utc=now,goal_complete=False)
    active_result=context if active=='L01.CONTEXTE.01' else bootstrap if active=='L01.CONTEXTE.02' else runtime
    if active.startswith('L01.INTEGRATION'):active_result=dict(status=clock['status'] if active.endswith('.01') else 'raccordement_aperçu_placement_a_faire')
    if active=='L08.IMAGE.HOUSE.02':active_result=image_batch
    if active=='L01.GPU.02':active_result=pipeline_run
    if active=='L01.OMBRES.01':active_result=host_shadow if host_shadow.get('controls_verified')==6 else dict(status='raccordement_routes_ombre_et_parametres_meme_frame_a_faire')
    if active=='L01.OMBRES.02.FORGE':active_result=dict(status='paired_comparison_case_prepared_forge_capture_not_executed')
    state['current_execution']=dict(active_sub_batch=active,status=active_result['status'],dashboard=str(T/'TABLEAU_DE_BORD_LOTS.md'),gpu_qualified=False,editor_global_qualified=False,full_import=False)
    write(T/'ETAT.v1.json',json.dumps(state,ensure_ascii=False,indent=2)+'\n')
    journal=T/'JOURNAL.md';old=journal.read_text('utf-8-sig')
    marker='<!-- USER-LOT-PLAN-V4 -->'
    if marker not in old:write(journal,old+f'\n{marker}\n## {now} — Reprise documentée par lots\n\nPlan courant actualisé : L01.CONTEXTE.01 actif, captures House/images en cours. Les étapes conversion, vérification, comportement, bibliothèque et recette produit sont suivies séparément. [Tableau courant](TABLEAU_DE_BORD_LOTS.md).\n')
    statuses=dict(context=context['status'],bootstrap=bootstrap['status'],runtime=runtime['status'],clock=clock['status'],preview_entry=entry['status'],web=web['status'],image_index=indexed['status'],native_library=native_library['status'],thumbnail=dict(stage=thumbnail_dir,status=images['status'],visual_review=image_review['status'],independent_reopen=image_reopen['status']),house=dict(stage=house_dir,status=house['status'],independent_reopen=house_reopen['status']),geometry=dict(status=geometry['status'],independent_reopen=geometry_reopen['status'],conversion=house_converted['status']))
    statuses['house_image_batch']=dict(status=image_batch['status'],captured=image_batch.get('cards_captured',0),indexed=image_batch_index.get('cards_updated',0),refused=image_batch_index.get('cards_refused',0),index_status=image_batch_index['status'],reader_status=image_batch_reader['status'],reader_verified=image_batch_reader.get('cards_verified',0))
    statuses['actual_device_mount']=dict(status=device_mount['status'],nodes=device_mount.get('nodes_verified',0),primitives=device_mount.get('primitives_verified',0),gpu_images=device_mount.get('gpu_images_resident_verified',0),source_mips=device_mount.get('source_mip_payloads_verified',0),draw_executed=False)
    statuses['source_pipeline_probe']=dict(status=pipeline_run['status'],pipeline_status=pipeline_result['status'],source_pipeline_count=pipeline_result.get('source_pipeline_count',0),all_ready=pipeline_result.get('all_source_pipelines_ready',False),product_reveal_changed=False)
    statuses['source_pipeline_reopen']=dict(status=pipeline_reopen['status'],pipelines_verified=pipeline_reopen.get('pipelines_verified',0))
    statuses['source_shadow_reopen']=dict(status=shadow_reopen['status'],pipelines_verified=shadow_reopen.get('source_pipelines_verified',0),shadow_routes_verified=shadow_reopen.get('shadow_routes_verified',0),production_routes_installed=False)
    recorded=T/'DERNIERS_ETATS_JOURNALISES.v1.json'
    previous=json.loads(recorded.read_text('utf-8-sig')) if recorded.exists() else {}
    if previous.get('states')!=statuses:
        current_journal=journal.read_text('utf-8-sig')
        write(journal,current_journal+f'\n## {now} — États des sous-lots\n\n'+''.join(f'- {name} : `{json.dumps(value,ensure_ascii=False)}`\n' for name,value in statuses.items())+'\n[Compteurs et preuves courants](TABLEAU_DE_BORD_LOTS.md). Aucun passage automatique au statut livré.\n')
        write(recorded,json.dumps(dict(updated_utc=now,states=statuses),ensure_ascii=False,indent=2)+'\n')
    print(json.dumps(dict(dashboard=str(T/'TABLEAU_DE_BORD_LOTS.md'),active=active,context=context['status'])))
if __name__=='__main__':main()
