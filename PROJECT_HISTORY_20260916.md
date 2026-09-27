# Historique archivé du contexte — 16 septembre 2026

Source : bloc déplacé à l'identique depuis PROJECT_CONTEXT.md, entre le bilan
courant et la vision produit. Document de traçabilité uniquement ; pour l'état
actuel, lire [PROJECT_CONTEXT.md](PROJECT_CONTEXT.md). Les libellés « courant »
et les attentes du texte ci-dessous sont historiques, pas des instructions de
reprise. Les chemins relatifs restent résolus depuis la racine du dépôt.

## Historique du pilote Visual Valley déplacé depuis la section 8.9

Le pilote Visual Valley matérialise maintenant cinq masques sémantiques feathered et un cache de
carving route/rivière/ruisseau à 2 m, avec lits, berges, remblais/coupes bornés et exclusions de
végétation. Le relief sculpté est aussi promu explicitement en `TerrainDocument` v2. La révision 2
du WorldSketch (correction autorisée du 2026-09-05) remodèle le fond de vallée autour de la rivière
avec 42 022 deltas : altitudes explicites, raccord large borné, tracés et landmarks authorés conservés.
L'adapter physique existant en dérive 64 heightfields Rapier ; une preuve headless compare
un impact Rapier à la surface Forge sur un point réellement sculpté. Un audit moteur-neutre calcule
les pentes des triangles, l'eau bloquée, les composantes terrestres, les routes et les croisements.
Le compilateur dérive désormais `forge.world-bridge-placements` v1 pour `bridge_south`, lié aux
hashes exacts du terrain et de l'audit. L'adapter Bevy matérialise tablier, parapets, piles et colliders
Rapier ; un probe borné fait franchir le croisement route/rivière au vrai contrôleur joueur sans
changer les identités runtime. Le benchmark R1-068 couvre ensuite la vallée, le pont, un intérieur,
les portes, deux profils d'agent, les coûts, un lien hors maillage et l'évitement local. Tous les
probes locaux passent, mais la sélection de production est refusée pour les écarts détaillés dans
l'ADR-0009. Le fournisseur reste donc `not_selected` et aucune carte jouable complète n'est
revendiquée.

La correction de relief et le franchissement sont vérifiés nativement ; les profils d'environnement,
d'eau et de météo sont dérivés à la révision du WorldSketch sans modifier leurs modèles embarqués.
L'ancienne recette reste une fixture de régression. La promotion locale de la révision 2 n'est pas
une acceptation artistique : matériaux de berges, végétation, comparaison Cadle et revue humaine
de Gate 1 restent ouverts. Voir `docs/evidence/gate1/R1_RIVER_VALLEY_REGRADING.md`.

Le mode WorldSketch du Studio charge aussi son pont dérivé sans fichier externe. Le hash du pont
concorde maintenant entre le CLI natif et Chrome après normalisation de sa pente diagnostique ;
ses colliders sont créés dans le navigateur, mais la traversée Chrome reste explicitement non vérifiée.
Voir `docs/evidence/gate1/R1_STUDIO_BRIDGE.md` ; ce raccord ne démarre pas l'édition de Gate 2.

Le runtime WorldSketch charge également le pont de sa compilation, sans JSON intermédiaire.
Le probe natif avec les seuls flags `--world-sketch-visual-valley --bridge-crossing-probe`
confirme 518/518 ticks au sol et le même hash que Chrome. Une entrée de pont explicite conserve
sa priorité et ses erreurs, sans fallback silencieux. Voir `docs/evidence/gate1/R1_NATIVE_AUTO_BRIDGE.md`.

La source courante est désormais révision 3 : seule la caméra de validation `human_bridge` est
relevée de 3,4 m (position et cible) après constat qu'elle était enfouie dans le terrain sculpté.
Sa hauteur réelle devient 1,713 m au-dessus du sol. Géométrie du terrain, tracés, verrous et poses
du kit restent inchangés ; les hashes dérivés et pins d'aperçu sont actualisés pour la révision.
Les anciennes preuves Chrome et du pont restent des preuves de révision 2, pas une nouvelle
qualification de parité révision 3. Voir `docs/evidence/gate1/R1_065_HUMAN_CAMERA_HEIGHT.md`.

Une vérification Chrome ultérieure confirme désormais les hashes communs natif/Studio révision 3,
le pont et le cycle clair/pluie/clair sans erreur de shader observée. Les captures constituent une
preuve visuelle séparée du ledger CPU ; passe GPU TAA isolée et traversée Chrome non qualifiées.
La vue joueur initiale reste à inspecter. Voir `docs/evidence/gate1/R1_STUDIO_REV3_SHADER_SMOKE.md`.

Le départ joueur WorldSketch est ensuite raccordé au landmark authoré dans les chargeurs
natif et Studio, avec hauteur dérivée des triangles sculptés. Tests physiques et capture
native passent ; le cadrage regarde encore le bord sud, et le nouveau départ n'est pas
encore revérifié dans Chrome. Voir `docs/evidence/gate1/R1_AUTHORED_PLAYER_SPAWN.md`.

Complément suivant : caméra initiale dirigée vers le centre de la vallée, sans changement
des sources. Capture native et Chrome réel vérifiés, probe du pont conservé ; cadrage de
démarrage corrigé mais qualité artistique et performance de Gate 1 non qualifiées.
Voir `docs/evidence/gate1/R1_PLAYER_INITIAL_HEADING.md`.

Le kit Blender gouverné peut ensuite être exploré avec le contrôleur natif, sans caméra fixe
concurrente : capture joueur avec 20/20 instances et 36/36 colliders, traversée du pont avec
kit présent vérifiées. Ce raccord n'est pas un import du kit dans le Studio ni une preuve de
contact avec chaque asset. Voir le complément playtest dans
`docs/evidence/gate1/R1_064_GOVERNED_ASSET_RUNTIME_INTEGRATION.md`.
Les entrées du playtest natif avec kit attendent désormais les instances Bevy réellement créées
et les colliders enregistrés dans Rapier ; cette disponibilité est observée séparément du seul
chargement des fichiers, puis vérifiée sur les 20 instances et 36 colliders du candidat.

Complément du 2026-09-06 : aperçu du kit raccordé au Studio Web en mode opt-in
`forge_kit=governed`. Rapports et GLB locaux sont vérifiés avant démarrage, puis servis à Bevy
depuis une source mémoire. Chrome réel observe 20 instances et 36 colliders prêts ; kit altéré
refusé avant création du monde, démarrage sans kit conservé. Ce n'est pas encore un importeur
graphique, une preuve Tauri ou une validation artistique. Voir
`docs/evidence/gate1/R1_064_STUDIO_GOVERNED_KIT.md`. Gate 1 reste ouverte.

Le parcours de qualification partagé est ensuite raccordé au Studio avec `forge_probe=bridge` :
Chrome avec kit gouverné vérifie 518/518 ticks au sol, 434 contacts pont, 42,528 m parcourus
et l'arrêt, sans reconstruire le monde. Ce scénario automatique désactive les commandes manuelles ;
il ne remplace pas leur validation humaine. Voir `docs/evidence/gate1/R1_067_SHARED_CROSSING_PROBE.md`.

Indépendance locale vérifiée ensuite : une copie de 190 sources sans dossier `upstream` ni `.git`
compile le runtime hors ligne, passe 397 tests et exécute la vallée avec son kit et le probe pont.
Le clone canonique est préservé ailleurs ; caches Cargo réutilisés, pas d'isolation OS ou de
packaging final déduits. R1-054 dispose de cette preuve locale ; Gate 1 reste ouverte.
Voir `docs/evidence/gate1/R1_054_NO_UPSTREAM.md`, qui rectifie aussi les récents totaux narratifs
de tests erronés à partir des sorties Cargo disponibles.

Le raccord physique des arbres procéduraux est ajouté après cette preuve : 636 projections
statiques issues des profils de collision canoniques, indépendantes des LOD et activées à proximité
du joueur. Les changements de qualité conservent leurs identités et dimensions ; 399 tests passent,
avec Clippy et check WASM. L'ancienne protection logique des placements ne démontrait pas de
colliders Rapier matérialisés. Voir `docs/evidence/gate1/R1_TREE_TRUNK_COLLISIONS.md` pour les
preuves et limites, notamment l'enveloppe physique plus large que le tronc visuel simplifié.

La comparaison descriptive actuelle avec Cadle a ensuite été exécutée en Full HD sur la même
RX 7800 XT : six vues de référence et deux vues Forge inspectées par l'agent, sans erreur de
lancement. Elle confirme un écart visuel important, notamment couverture végétale, arbres et
matières ; la priorité suivante est la représentation végétale à l'échelle de la scène, pas un
nouvel audit CPU isolé. Aucun verdict de parité, benchmark équivalent ou accord humain n'en
est déduit. Voir `docs/evidence/gate1/R1_052_CADLE_VISUAL_GAP.md` ; Gate 1 reste ouverte.

Première correction de cet écart : brins courbés, touffes High à 64 brins et fondu proche resserré
sans portée supplémentaire. 400 tests, Clippy, check WASM et capture native passent ; les touffes
proches sont plus pleines, mais les espaces nus et la représentation des arbres restent à traiter.
Le complément du même rapport distingue cette amélioration locale de la couverture végétale globale.

La suite ajoute une densité spatiale reproductible au document de végétation : zones lissées de
32 m activées uniquement sur l'herbe de la recette. Le runtime passe de 4 531 à 9 606 touffes,
sans changer les autres placements, le WorldSketch, le terrain ni le kit manuel. 404 tests,
Clippy strict, check WASM, build et capture native passent. Le document végétation change
volontairement ; les plafonds de génération restent inchangés. La capture montre davantage
d'herbe mais une couverture encore insuffisante : ni naturalité ni Gate 1 validées. Rendu Chrome
du lot non exécuté. Preuves et empreintes dans le même rapport R1-052.

Les transitions végétales ont ensuite été resserrées à huit mètres maximum, avec racines
par placement dans les HLOD pour aligner la distance shader sur celle des instances individuelles.
Le culling CPU des lots reste conservateur ; la coupure visuelle du profil n'est pas augmentée.
407 tests, Clippy, check WASM et deux captures natives passent. Les arbres proches/moyens sont
plus pleins, mais les formes simplifiées et certains pointillés lointains restent visibles.
Sources, poses et kit inchangés ; mouvement continu, rendu WebGPU du lot et revue humaine non
qualifiés. Voir `docs/evidence/gate1/R1_VEGETATION_LOD_ANCHORS.md` ; Gate 1 demeure ouverte.

Rejeu WebGPU ultérieur : build Studio courant lancé dans Chrome, rendu végétal inspecté et kit
20/20 instances/36 colliders prêt. Après un échec watchdog conservé, le même parcours au premier
plan termine à 518/518 ticks au sol, 434 contacts, 42,528 m, sans changement de délai. Canvas
1408 × 797, pas un benchmark ni une acceptation humaine ; archivage de capture finale empêché
par une déconnexion de l'outil. Voir `docs/evidence/gate1/R1_VEGETATION_WEB_REPLAY.md`.

Le Studio expose ensuite deux demandes d'export de preuve en lecture seule : diagnostic JSON
borné et capture GPU PNG via Bevy. 412 tests, Clippy natif/Web et build Studio passent ;
callback GPU 1408 × 741 observé, mais réception locale des fichiers non vérifiée. Ce n'est
ni une sauvegarde projet ni une qualification complète de capture. Voir
`docs/evidence/gate1/R1_STUDIO_PROOF_EXPORT.md` ; Gate 1 reste ouverte.

Cycle des quatre qualités rejoué dans Chrome avec le kit courant, puis brume expérimentale
High → Low → High → arrêt : projections et rendu observés, sans qualification de fluidité
(lecture Ultra expirée à trois secondes avant stabilisation). La brume disparaît en Low ;
le budget de télémétrie conservait alors 64 au lieu de zéro, corrigé avec un test ECS.
Captures consultées dans l'outil, non archivées ; voir
`docs/evidence/gate1/R1_STUDIO_QUALITY_FOG_CYCLE.md`. Aucun preset complet acquitté.

La préparation CPU des brins utilise ensuite un échantillonnage de hauteur triangulée seul,
sans normales/matériaux inutilisés, cache ou réduction de scène. Égalité bit à bit testée ;
trois mesures natives High passent de 2,84–2,91 s à 0,56–0,68 s, Ultra de 3,02–3,47 s à
0,72–0,92 s. Mesure de préparation seulement, pas de FPS ni de latence Web complète.
Voir `docs/evidence/gate1/R1_GRASS_HEIGHT_ONLY_PREPARATION.md` ; R1-040 reste ouvert.
Rejeu du nouveau build dans Chrome : High → Ultra → High exécuté, kit conservé,
capture retour High inspectée. Clic → budgets observés : 2,13 s puis 1,23 s sur
une mesure par sens, toujours cinq domaines projetés et deux partiels. Pas de gain
Web chiffré sans baseline équivalente, ni de fluidité qualifiée ; voir le complément
de cette même preuve.

Le Studio Gate 1 ouvre désormais la vue sans le grand panneau de métriques ; un bouton
permet de le réafficher/masquer sans interrompre la collecte. Aller-retour inspecté dans
Chrome, 415 tests et Clippy natif/Web PASS, un monde et kit conservés. Défaut natif/Gate 0
visible conservé ; pas de nouveau parcours Tauri. Voir
`docs/evidence/gate1/R1_STUDIO_METRICS_VISIBILITY.md`.

Le terrain dispose maintenant d'un candidat de détail d'albédo/rugosité multi-échelle,
avec un cache procédural R8 partagé de 16 Kio, sans changer masques ou sources. Captures
natives clair/pluie et rendu Chrome inspectés ; coût Web et qualité artistique non qualifiés.
Le premier calcul par fragment a été remplacé après une alerte de cadence. Voir
`docs/evidence/gate1/R1_TERRAIN_SURFACE_DETAIL.md`.

Le contrat, les gates et le pilote sont décrits dans
`docs/ADR/ADR-0008-world-sketch-image-to-world.md`,
`docs/ADR/ADR-0009-navigation-provider-benchmark.md` et `docs/world-generation/README.md`.

Le document de qualité possède désormais un contrôleur de décision pur (hystérésis,
temporisation, bornes, benchmark High fixe). Une proposition n'est déclarée appliquée
qu'après confirmation de l'adapter. Aucun système Runtime/Studio ne le consomme encore :
les quatre profils graphiques dynamiques restent à implémenter et qualifier dans R1-040.
Premier adapter indépendant raccordé : budget de pluie modifiable sans perdre entité,
matériau, horloge ou source, et option native `--rain-quality`. Cycle couvert en ECS ;
quatre budgets lancés en natif et captures Low/Ultra inspectées. Ni pilotage global automatique,
ni sélection Studio, ni transition GPU à chaud ne sont qualifiés par ce lot.
Voir `docs/evidence/gate1/R1_040_QUALITY_DECISIONS.md`.
Les budgets d'ombres sont également consommés via `--shadow-quality` : cascades,
résolution, distance et contacts. 344 tests passent ; preuves natives et limites dans
`docs/evidence/gate1/R1_040_SHADOW_BUDGETS.md`. Une capture High trop précoce a été rejetée,
puis obtenue correctement au replay tardif : le préchauffage reste à fiabiliser (R1-039).
Ce raccord isolé ne clôt ni les profils globaux ni Gate 1.
Le calcul des LOD terrain existe maintenant dans `forge_world`, avec périmètres complets
partagés entre pas différents, sans collision dérivée simplifiée. 352 tests workspace passent.
Le raccord natif optionnel `--terrain-quality` projette maintenant trois LOD recentrés par chunk,
en conservant les 64 bindings complets pour les transactions et Rapier. Le test natif de retouche
reconstruit seulement trois caches concernés, sans changer les identités. High est inspecté.
Le défaut de terrain lointain disparu en Low est ensuite corrigé : cull à 896 m aux quatre niveaux,
fondu final borné, couverture géométrique des caméras/bords et capture Low native vérifiées.
Ce correctif ne qualifie ni le profil global, ni la qualité artistique, ni Studio ; voir
`docs/evidence/gate1/R1_040_TERRAIN_LOD_RENDERER.md` et la preuve de topologie associée.
Le post-traitement consomme également les presets via le même système que la météo : bloom,
étalonnage neutre ou authoré, tonemapping explicite. Cycle ECS et natif Low/High sous pluie
vérifiés ; la passe bloom est observée en High et absente des diagnostics Low. Environnement,
eau, végétation et contrôleur global restent à raccorder aux budgets complets ; voir
`docs/evidence/gate1/R1_040_POST_PROCESS_BUDGETS.md`.
Le relevé environnement est maintenant rafraîchi après les changements de qualité, sans rejouer
la projection authorée. Un cycle combinant météo et environnement vérifie notamment le tonemapping
désactivé explicitement ; ce relevé CPU ne qualifie pas les budgets environnement encore absents.
Le raccord environnement expose maintenant `--environment-quality` dans Runtime : une sonde
dérivée renouvelable porte le cache Bevy, sans remplacer la caméra. Les textures 64²/512² sont
générées et raccordées lors de deux démarrages natifs Low/Ultra inspectés. Le cycle natif
High/Low/Medium/Ultra/High est ensuite vérifié avec cinq captures : un défaut GPU de mip levels
a été corrigé en gardant le filtrage sur la sonde et en raccordant seulement ses textures filtrées
à la caméra. 373 tests workspace, Clippy et check moteur WASM PASS. Studio, transitions sans
variation transitoire et volumétrie restent non qualifiés ; voir aussi
`docs/evidence/gate1/R1_040_ENVIRONMENT_NATIVE_CYCLE.md` ;
aucun preset complet déclaré. Voir `docs/evidence/gate1/R1_040_ENVIRONMENT_QUALITY_IN_PROGRESS.md`.
La densité végétale dispose d'une sélection pure déterministe, distincte des sources et protégeant
les couches avec collision : densités 0,1..2 et conservation des placements vérifiées, 374 tests
workspace PASS. Elle est ensuite raccordée au renderer au démarrage optionnel via
`--vegetation-quality` : densité, distances et brins par patch. Les quatre profils sont testés,
Low/Ultra lancés et inspectés, 376 tests workspace PASS, Clippy et check moteur WASM PASS.
Les 636 arbres protégés et les hashes source restent inchangés. Changement à chaud, Studio et
acquittement global restent absents ; voir `docs/evidence/gate1/R1_040_VEGETATION_RENDER_DENSITY.md`
et `docs/evidence/gate1/R1_040_VEGETATION_INITIAL_QUALITY.md`. Gate 1 reste ouverte.
Suite végétation : API de changement à chaud implémentée et cycle ECS vérifié, conservation
des entités survivantes/handles/matériau, refus conservant la projection précédente. La CLI reste
un choix initial hors harness ; cycle natif High/Low/Medium/Ultra/High ensuite exécuté à code 0,
cinq PNG inspectés, zéro ERROR. Coût de transition et Studio restent non qualifiés. Voir
`docs/evidence/gate1/R1_040_VEGETATION_TRANSITION_IN_PROGRESS.md`.
Un choix Runtime global initial `--quality` raccorde maintenant les sept domaines existants,
sans autoriser de mélange ambigu avec les overrides individuels. Low/Ultra sous pluie natifs
inspectés, zéro ERROR, 379 tests workspace et Clippy PASS. Eau/environnement restent partiels,
aucun acquittement global ni adaptation automatique ; voir
`docs/evidence/gate1/R1_040_GLOBAL_INITIAL_QUALITY.md`.
Le choix global installe ensuite un coordinateur de demandes à chaud : présence des sept adapters
avant envoi, relevés CPU par domaine et politiques concordantes. Cycle natif sous pluie cinq niveaux
inspecté sans erreur GPU. Eau/environnement restent Partial et aucun preset complet n'est acquitté.
Adaptation et Studio restent ouverts ; voir `docs/evidence/gate1/R1_040_GLOBAL_QUALITY_CYCLE.md`.
Le raccord Studio est ensuite réalisé : quatre boutons de qualité, canal transitoire moteur,
sans sauvegarde ni redémarrage du viewport. Cycle réel Low/Medium/Ultra/High et météo pluie/clair
vérifiés dans le navigateur intégré, captures Low/Ultra inspectées, zéro warn/error relevé.
Statuts eau/environnement partiels conservés ; Full HD/Tauri et adaptation restent ouverts.
Voir `docs/evidence/gate1/R1_040_STUDIO_QUALITY.md`.
Le ledger Runtime v8 expose ensuite l'état global du même coordinateur après observation,
sans promouvoir les domaines partiels. Absence du bloc en Gate 0 et état High partiel vérifiés
dans deux exécutions natives ; voir l'addendum `R1_040_GLOBAL_QUALITY_CYCLE.md`.
La préparation pure de la volumétrie borne ensuite un volume dérivé et projette les budgets
32/64/96 étapes, sans modifier les sources. Raccordement ECS expérimental ensuite ajouté,
désactivé par défaut : suivi caméra, nettoyage des seuls composants possédés et métriques CPU.
Douze tests environnement PASS ; aucun résultat GPU annoncé. Activation CLI/Studio et
qualification visuelle restent ouvertes. Voir `docs/evidence/gate1/R1_040_VOLUMETRIC_PREPARATION.md`.
Activation CLI et soleil ensuite raccordés : lancement natif code 0, capture inspectée mais rejetée
(brume trop sombre et frontière du volume visible). Pas de promotion par défaut ; correction
spatiale et WebGPU restent requis. Voir `docs/evidence/gate1/R1_040_VOLUMETRIC_NATIVE.md`.
Correctif spatial ensuite vérifié en capture : texture de densité dérivée à bords doux et terme
ambiant Bevy désactivé dans la seule prévisualisation. Ligne du ciel disparue sur la vue testée,
assombrissement central toujours excessif ; rendu non accepté, effet toujours opt-in.
La portée est ensuite bornée par les cascades du soleil sans augmenter leur coût. Comparaison
native au sol : assombrissement atténué, volume actif, 64 étapes/128 m effectifs observés pour
320 m demandés. Sources conservées ; mouvement, autres presets et WebGPU restent non qualifiés.
Cycle natif High/Low/Medium/Ultra/High ensuite vérifié dans un processus : retrait en Low,
retour aux budgets 32/96 m, 96/192 m et 64/128 m, cinq captures inspectées, code 0.
Voir `docs/evidence/gate1/R1_040_VOLUMETRIC_CYCLE.md`. Mouvement/WebGPU et acceptation restent ouverts.
Studio ensuite raccordé par bouton expérimental sans sauvegarde : activation High, Low, retour
High et désactivation vérifiés dans le navigateur WebGPU réel, deux captures inspectées, aucun
warn/error relevé. Viewport 768×606 seulement ; mouvement, Full HD/Tauri et acceptation restent
ouverts. Voir `docs/evidence/gate1/R1_040_VOLUMETRIC_STUDIO.md`.
Coexistence native avec marche/saut/caméra ensuite vérifiée (232 ticks fixes), plus test CPU
de suivi après propagation et sortie/retour du volume. Pas de capture temporelle : qualité
visuelle en mouvement encore ouverte. Voir `docs/evidence/gate1/R1_040_VOLUMETRIC_MOTION.md`.
Tentative interactive Web suivante non validée : navigateur intégré refuse le pointer lock
avec WrongDocumentError. Les preuves statiques restent distinctes ; rendre l'état des entrées
observable puis rejouer le déplacement sur une surface compatible, sans contourner la sécurité.
Indicateur d'entrée ensuite implémenté : refus réel reproduit et affiché dans le viewport,
logs conservés, scène toujours rendue. Clippy WASM et build Studio PASS. Capture active et
déplacement Web restent non qualifiés. Voir `docs/evidence/gate1/R1_040_STUDIO_INPUT_CAPTURE.md`.
Essai Chrome suivant : après focalisation réelle de l'onglet, capture active, bascules de caméra
première/troisième personne et libération Échap vérifiées avec la brume High. Pas de nouvelle
erreur après focus. Marche continue, Full HD/Tauri et qualification artistique restent ouverts.
L'eau consomme désormais un sous-budget SSR : absent en Low, 24/48/64 étapes en Medium/High/Ultra.
Cycle ECS conservant sources et bindings, captures natives Low/Ultra et passe SSR Ultra vérifiés.
Le preset eau reste explicitement partiel : distance SSR non pilotable, réfraction écran indisponible,
réflexion environnement non qualifiée ; aucun acquittement global. 366 tests workspace PASS,
Clippy et check moteur WASM PASS. Voir `docs/evidence/gate1/R1_040_WATER_SSR_BUDGETS.md`.
Le harness PNG attend maintenant une disponibilité observée des pipelines et du kit, puis
vérifie le readback de sa propre demande : capture précoce différée en natif, délai trop court
refusé explicitement, smoke Gate 0 conservé. Ce garde ne préchauffe pas toutes les variantes
et ne remplace pas l'inspection ; voir `docs/evidence/gate1/R1_039_CAPTURE_READINESS.md`.

---

### Historique du 15 septembre — les états intermédiaires ci-dessous sont dépassés par le bilan

R2-020 poursuivi après 97DF58D4 : réconciliation étendue en code à la densité,
l'espacement, l'altitude et la pente depuis les réglages existants des couches.
Même préparateur de base et même commande atomique ; choix explicites, aucune
retouche effacée. Cache natif lié au hash de la recette complète, réponses liées
aux paramètres. Test CPU de conservation/archives/Undo/Redo et Clippy natif/WASM
PASS. Aucun nouveau binaire ni replay graphique de cette extension ; 97DF58D4
ne la contient pas. Gate 2 reste ouverte.

Transition de promotion corrigée après la garde ci-dessous : le nouveau placement
charge caché et n'est révélé qu'après acquittement végétal ; annulation le maintient
caché pendant le retrait. Placements acceptés inchangés. Deux tests CPU et Clippy
WASM PASS ; candidat regroupé 97DF58D4 construit (WASM 849C4CF7). Coque Studio
ouverte mais essai bloqué par un dialogue de redémarrage Windows à 05:53 : aucune
zone chargée, aucune qualification de transition. Copie 1F51F342 intacte,
processus possédés fermés et vérifiés. R2-020/Gate 2 restent ouverts.

Protection ajoutée après 11415DF8 : Apply des promotions/réconciliations dans
le Studio attend l'acquittement exact de l'aperçu végétal et la disponibilité
du placement. Bouton et raccourci passent par la même garde avant clear/envoi.
Annulation/changement de session invalident l'acquittement ; erreurs de projection
demandent la restauration acceptée. Test CPU de la garde et Clippy WASM PASS.
Intégrée ensuite au binaire 97DF58D4 ; pas de panne de chargement réelle rejouée. Cette garde
de présentation ne remplace pas les validations natives et ne clôture pas R2-020.

Dernier candidat avec parcours complet observé, 11415DF8 (sélecteur par proximité), rejoué sur copie
isolée : déplacement local de l'arbre promu, Apply/Save, nouvelle seed 43,
correspondance choisie à 3,9 m, aperçu/Apply, Undo/Redo et Save acquis dans le
Studio. Position retouchée conservée (-72.281502, 3.018228, 52.511951) m.
Playtest lancé puis retour édition synchronisé, archive inchangée par ce passage.
Archive v27 finale 1F51F342 : 19 commandes initiales intactes + quatre commandes.
Source DE035294 intacte ; candidat et descendants confirmés fermés. Réouverture
de cette dernière archive et qualification des chargements/chevauchements encore
dues ; R2-020/Gate 2 restent ouverts. Les paragraphes ci-dessous retracent les
étapes antérieures ; preuves actuelles dans R2_GENERATED_PROMOTION.md.

Candidat réconciliation AD6DDA38 construit et rejoué sur copie isolée : archive
promue rouverte, 1628 candidats natifs (20 affichés), correspondance choisie,
aperçu puis Apply seed 44→42 et Save acquis. Archive v27 DE035294, 18 commandes
antérieures intactes + une réconciliation ; source B171A07C intacte. Candidat et
ses descendants fermés. Réouverture post-réconciliation acquise : même placement
et pin visibles dans l'inspecteur. Saisie automatisée sans focus confirmé,
aucune retouche appliquée ; archive DE035294 intacte après fermeture.
Retouche manuelle et playtest encore dus ; R2-020 ouvert. Libellé de calcul terminé corrigé après
ce binaire, non rejoué. Preuves détaillées dans R2_GENERATED_PROMOTION.md.

Sélecteur de réconciliation amélioré après ce binaire : tri natif par distance
horizontale à la position authorée, départage stable par ID, pages de 20 et distance
affichée. Aucun choix automatique ; pour une exclusion d'objet supprimé, ordre
par ID sans distance inventée. Réponses liées aussi à l'objet choisi, doublons
interdits dans le brouillon et retrait d'un choix sans suppression d'objet.
Test CPU ciblé PASS (tri, retouche, liste périmée, tombstone, conservation),
Clippy natif/WASM strict PASS, y compris le retrait du choix.
Pas de nouveau candidat ni de qualification graphique de ce sélecteur.

R2-020 : chemin d'aperçu Studio étendu à ReconcilePromotions lorsque le terrain
et les placements restent inchangés. Nouvelle recette et exclusions projetées
ensemble ; révision d'aperçu maintenue sur le terrain accepté après vérification
du hash du reçu. Clippy Web/WASM strict PASS ; pas de nouveau binaire ni de
preuve graphique. Sélecteur de correspondances désormais codé dans Recette de
végétation : candidats paginés (20), coordonnées, choix explicites par objet,
invalidations seed/tête, PreviewZone sans application implicite. Test du modèle
de choix PASS (incomplet/introuvable/périmé refusés), Clippy WASM PASS.
Pas encore de candidat/replay de ce contrôle. Calcul et préparation désormais
natifs via tâche bloquante hors UI ; cache d'une population évincé aux commandes
authoring/workspace, réponse de 20 arbres maximum. Clippy natif/WASM PASS.
La sélection reste par liste, non par clic 3D ; réactivité, transport réel et
ergonomie restent à qualifier, R2-020 reste ouvert.

R2-020 : commande atomique ReconcilePromotions implémentée (nouvelle recette et
correspondances explicites, placements/verrous/autres retouches inchangés).
Préparation Monde refuse les origines inexistantes. Archives 25/26/27, tête v8
inchangée. Test intégré CPU PASS : nouvelle seed, remplacement d'origine,
placement verrouillé conservé, replay, Undo/Redo, trois codecs et downgrade refusé.
Clippy natif et test strict PASS. Interface de choix/aperçu non raccordée :
ne pas considérer la résolution interactive ou R2-020 comme terminés.

R2-020 : conflits d'origines désormais identifiés exactement par le générateur
(liste typée couche/instance, sans seconde génération). Le refus natif affiche
jusqu'à huit objets/origines et distingue les exclusions d'objets supprimés.
Deux tests ciblés PASS (conflit et conservation des voisins), Clippy Monde PASS.
Diagnostic seulement : réaffectation explicite dans le Studio encore à réaliser.

R2-020, protection native ajoutée après AD6D3D3F : le host Studio applique
validate_origins au candidat de chaque PreviewZone/ApplyZone, y compris Undo/Redo,
avant mutation, pour zones autonomes et gérées. Politique injectée sans dépendance
Projet → Monde ; autres consommateurs doivent l'injecter explicitement.
Test host autonome PASS (refus aperçu/application sans mutation, commandes
acceptées et replay conservés), Clippy binaire Studio PASS. Voie gérée raccordée
mais refus non rejoué ; nouveau binaire et parcours auteur restent dus.

R2-020, code post-AD6D3D3F : six préparations de recette végétale refusent avant
commande les origines promues introuvables après régénération. Test ciblé et
Clippy Monde PASS. Contrôle universel host et réconciliation encore dus ; pas
de nouveau binaire ni de qualification graphique pour cette protection.

R2-020, replay AD6D3D3F acquis : clic de promotion, aperçu, Apply, sauvegarde,
Undo/Redo dans le Studio ; origine et placement enregistrés ensemble (delta v24).
Copie finale B171A07C, source intacte. Candidat fermé. Réouverture, retouche,
régénération et playtest restent dus ; R2-020 et Gate 2 toujours ouverts.

Candidat promotion corrigé **AD6D3D3FC0845838F0247B001D76B0247EED01FC50BE8C4C44F024AC1E80A1ED**
construit dans `E:/OpenRPG_Forge/.gate2-evidence/promotion-ready-studio-20260915`.
Web et packaging réussis ; BUILD_ONLY, replay graphique encore dû. Contient
le correctif de garde far-batching inactif couvert par le test de régression.

Candidat promotion C2AEB393 construit et lancé : clic refusé par la garde de
projection (aucune retouche). Cause corrigée : batching lointain optionnel
exigé même inactif. Test de régression moteur PASS ; nouveau Web en construction.
Tous les processus de cet essai fermés, copie d'archive 5717781F inchangée.

R2-020 : retrait scatter de l'aperçu retardé jusqu'au placement prêt ; échec de
chargement déclenche restauration demandée. Clippy WASM PASS, sans preuve GUI.
Doublon transitoire et application pendant chargement restent à qualifier.

R2-020 : aperçu combiné retrait scatter + placement désormais raccordé, avec
validation du reçu et restauration à l'abandon. Clippy WASM PASS ; chargements
asynchrones et parcours graphique encore non qualifiés. Aucun nouveau candidat.

R2-020 : bouton Studio et sélection par raycast d'un arbre gouverné raccordés
à la préparation complète et PreviewZone. Clippy WASM PASS ; pas de nouveau
candidat lancé. Aperçu combiné et cycle auteur complet restent non qualifiés.

R2-020 : préparation complète `prepare_tree_promotion` codée, reconstruisant
les sources autoritaires et composant résolution puis commande atomique.
Clippy Studio Web/WASM PASS (29,80 s). Appel UI et preuve comportementale intégrée
encore absents : pas de promotion utilisable revendiquée.

R2-020 : transformation brute GLB calculée avec pivot et échelle du prototype,
raccordée au résultat `PromotedTreeAsset`. Deux tests mathématiques PASS et
Clippy moteur natif lib PASS. Commande intégrée, sélection Studio et parité
graphique toujours dues ; cette étape ne clôture aucun critère de gate.

R2-020 : résolveur de métadonnées d'asset implémenté côté moteur, réutilisant le
choix déterministe du rendu et AssetBrowser. Clippy natif lib PASS ; pas encore
de test comportemental ni UI. Transformation normalisée et commande complète
restent dues, aucune promotion de bout en bout annoncée.

R2-020 : résolution CPU d'une sélection canonique désormais disponible, avec
contrôle des hashes terrain/végétation et refus d'origine déjà promue. Test
ciblé et Clippy Monde PASS ; intégration sélection Studio et pin toujours dues.

R2-020, raccord source ultérieur : exclusions de promotions transmises par le
Studio et présentes dans le snapshot playtest ; préparations moteur et caches
adaptés. Clippy Studio Web/WASM strict PASS (33,38 s), aucun nouveau binaire ni
rendu qualifié. Sélection de l'origine, résolution du pin et conflits avant
commande restent dus : promotion pas encore utilisable de bout en bout.

Priorité fonctionnelle suivante : R2-020, promotion d'une instance générée en
placement retouchable sans doublon à la régénération. Inspection : lien d'origine
et suppression persistante du scatter absents. Les placements manuels existants
ne prouvent pas ce parcours. Contrat de travail R2_GENERATED_PROMOTION.md sous
ADR-0014 ; rien d'implémenté pour cette promotion par ce constat.
Depuis : origine persistante et préparation atomique origine+placement codées,
tête v8 et codecs 22/23/24. Quatre tests espèces/promotion et Clippy Projet PASS.
Résolution de l'instance, suppression effective dans le scatter et UI encore
absentes ; aucune promotion fonctionnelle ou graphique annoncée.
Filtrage exact prêt dans le générateur via with_promotions : suppression après
espacement, voisins conservés, origine introuvable refusée. Test ciblé et Clippy
Monde PASS. Appels adapters, résolution des pins et sélection Studio encore dus.
Voie de densité graphique corrigée aussi : les décorations supplémentaires ne
réintroduisent pas l'origine promue. Test CPU cinq multiplicateurs PASS, voisins
identiques ; coût de génération temporaire supplémentaire non mesuré. Raccord
Studio/runtime toujours dû, pas de preuve graphique nouvelle.

Candidat regroupé construit **C86B50EDFFC21EE7298EED6AC3CCD969625C99B76FCBE69B4CD4BA268E4C7CBE** :
`E:/OpenRPG_Forge/.gate2-evidence/draft-species-studio-20260915/forge-studio-candidate.exe`.
Contient mélange multi-couches partagé, reprise d'espèces et invalidation des
brouillons chemins/rivières/bâtiments. Build Web (Rust 3 min) et packaging PASS.
**BUILD_ONLY : pas encore lancé ni qualifié.** Réutiliser ce binaire pour le
prochain replay regroupé ; 42794BEB demeure la dernière preuve graphique.
Replay ciblé C86B50ED acquis depuis : retrait d'espèces sans catalogue chargé,
aperçu procédural puis Échap restaurant le mélange ; chemin examiné à 3 m,
largeur brouillon 3,1 m annulant la proposition et retirant l'aperçu cyan.
Copie draft-species-ui inchangée (5717781F), aucun Apply/Save. Essai fermé.
Mélange multi-couches, bâtiments/rivières et playtest restent non qualifiés par
ce replay. Détails R2_SPECIES_CATALOG.md et R2_SPLINE_EDIT.md.
Code ultérieur : déplacement X/Z du tracé entier raccordé au brouillon Studio,
via règle moteur-neutre path_stroke::translate. Six tests path_stroke PASS,
Clippy WASM PASS ; bouton pas encore dans C86B50ED et parcours GUI non acquis.

Qualification locale ajoutée : candidat espèces **42794BEB895F2221360B48E85FA60AC6FD5F045C22D50163908546331DF37EEA**,
`E:/OpenRPG_Forge/.gate2-evidence/species-palette-studio-20260915/forge-studio-candidate.exe`.
Sur copie isolée : aperçu chêne et abandon avec restauration procédurale observés ;
mélange chêne/pin appliqué, sauvegardé, fermé puis réouvert avec rendu restauré.
Limite 4 Mio rencontrée sans mutation ; conversion compacte explicite préalable.
Archive finale 5717781F, source C6343B05 intacte, candidat et descendants fermés.
Éclairage de travail surexposé : preuve fonctionnelle, pas qualification artistique.
Undo/Redo graphique des espèces, playtest et autres familles restent dus.
R2_SPECIES_CATALOG.md détaille ces preuves ; R2-016 et Gate 2 restent ouverts.
Les paragraphes ci-dessous décrivent les étapes antérieures à ce replay.

Code postérieur au candidat 42794BEB : une nouvelle demande de session/aperçu
peut réessayer le même hash après échec de téléchargement, sans boucle par frame.
Le retrait de tous les choix ne dépend plus d'un catalogue disponible : retour
procédural par aperçu/commande normale. Clippy Web/WASM strict et formatage PASS ;
ces deux chemins de reprise ne sont pas encore qualifiés graphiquement.
Palette multi-couches : bouton de reprise du mélange choisi sur toutes les
couches arborées ajouté au brouillon, avec validation des quotas avant remplacement.
Ni poses ni masques modifiés ; aperçu/application/sauvegarde restent explicites.
Clippy WASM PASS ; bouton absent du candidat 42794BEB, parcours réel encore dû.
Règle multi-couches désormais partagée dans forge_project, appelée par le Studio.
Trois tests espèces PASS : préparation sans mutation, quotas/orphelins, commande,
archive et Undo ; Clippy WASM PASS. Pas de qualification GUI supplémentaire.
Chemins : changements du nom, profil ou points du brouillon annulent désormais
l'aperçu précédent et désarment le geste. Montage initial et mises à jour de
session seules exclus de l'invalidation. Clippy WASM PASS ; parcours GUI dû.
Même protection partagée via authoring_draft_scope pour les rivières (profil et
points) et bâtiments (création, inspecteur, porte, duplication). Une seule règle
d'invalidation, aucun changement de format ni sauvegarde implicite. Clippy WASM
PASS (7,31 s) ; ces scénarios sont à rejouer ensemble, pas encore qualifiés.

R2-016 : sélections d'espèces maintenant persistantes dans la tête de zone v7,
commandes/Undo/Redo et codecs v19/20/21, régénération conservatrice. Deux tests
de codecs et entrées invalides PASS. Préparation du catalogue gouverné existante ;
résolution des références préparée côté adapter (hash complet du catalogue),
palette et projection raccordées dans le code, encore à qualifier.
Le chargeur moteur accepte désormais un remplacement de catalogue sans redémarrage
(Clippy WASM PASS), cache de vent renouvelable ; cycle réel encore non qualifié.
Le service de chargement est installé dans les zones auteur, même sans catalogue
initial. Livraison des GLB sélectionnés raccordée aux têtes acceptées : contrôles
de hash, quotas et époque, source mémoire adressée par contenu. Qualification
réelle encore due ; aucun nouveau candidat graphique construit.
Palette par couche et aperçu réversible maintenant raccordés dans le code, avec
retour explicite au rendu procédural lors du retrait. Replay groupé et qualification
Save/Open/rendu encore dus ; R2-016 demeure ouvert.
Aucun catalogue mutable parallèle. R2_SPECIES_CATALOG.md ; candidat 8C6A960A inchangé.

Code postérieur à 8C6A960A : changements de réglages relief/matériaux désarment
le geste et annulent l'aperçu, comme les réglages de densité. Clippy WASM PASS,
qualification graphique groupée encore due (R2_TERRAIN_BRUSHES.md).

R2-018 « seed stable » clôturé fonctionnellement en local : cycle 42→43→42
après relecture restitue exactement les instances/chunks et conserve les
retouches. Preuves Studio existantes recoupées, aucun replay graphique répété.
Voir R2_SEED_STABILITY.md. R2-C/Gate 2 restent ouverts ; code produit inchangé.

Candidat courant **8C6A960AB772C3F624523490212E33AB77910F3A53C1824219CCAE78540F37F6** :
`E:/OpenRPG_Forge/.gate2-evidence/habitat-density-studio-20260915/forge-studio-candidate.exe`.
Limites altitude/pente par couche et intensité du pinceau ajoutées. Web/packaging
et contrôles ciblés PASS. Parcours réel : altitude minimum 1000 m exclut les arbres
en aperçu/Apply, Undo restaure la forêt ; point Herbe à 0,25 appliqué/sauvegardé,
masque partiel 193–255. Décodeur : 14 reçus, recette et terrain de base inchangés.
Source intacte, candidat et six WebViews fermés. Pente et reste du parcours complet
non qualifiés ; R2_VEGETATION_AUTHORING.md. Gate 2 ouverte, aucun push.

### Candidat précédent — index et clavier

Candidat **DDCC9C5FDFC8BE693B2E4B09C1CE84950DB18E8B0209F43D07C79A2062CAF450** :
`E:/OpenRPG_Forge/.gate2-evidence/layer-index-keyboard-studio-20260915/forge-studio-candidate.exe`.
Index végétal et sélection commune recette/masque qualifiés sur Arbres verrouillé.
Échap corrigé et observé : annule l'aperçu de copie et restaure la végétation.
Web/packaging, test ciblé et Clippy PASS. Copie isolée et source FF89547B intactes,
aucun Apply/Save, candidat et six WebViews fermés. Reset multizone non qualifié.
Preuves : R2_SCENE_INDEX.md et R2_EDITOR_SHORTCUTS.md. Gate 2 ouverte, aucun push.

### Candidat précédent — aperçu végétal

Candidat de couches végétales **F5B45FD1171C841B3DE5A676B1DEAC663F8D07B8E819769B96B06B7567D617B4** :
`E:/OpenRPG_Forge/.gate2-evidence/vegetation-recipe-preview-studio-20260915/forge-studio-candidate.exe`.
Inclut copie/retrait/réordre/renommage, aperçu végétal temporaire avec retour
à la version acceptée et les correctifs de placements postérieurs à 1551BFCA.
Web et packaging PASS ; parcours réel sur copie isolée acquis : arbres 35 → 0 %
en aperçu, Abandonner restaure la forêt, puis Apply/Undo/Redo/Save observés.
Archive 56AEDC0D relue par Forge : corrections et terrain identiques, seule la
densité arbres change, 14 reçus. Source intacte, essai et six WebViews fermés.
Échap non qualifié (proposition restée active) ; copie/retrait/réordre/renommage,
réouverture GUI et playtest restent dus. La preuve BUILD_ONLY reste le rapport
de construction ; les observations graphiques sont consignées séparément.
Preuves : R2_LAYER_ORDER.md. Gate 2 reste ouverte, aucun push ni Gate 3.

### Candidat précédent — parcours placements

Nouveau candidat regroupé **1551BFCAF4A4AAC57A1690B1265C2B8D9E1C08433B5EF2A688396904A1C60DB2** :
`E:/OpenRPG_Forge/.gate2-evidence/incremental-placements-studio-20260915/forge-studio-candidate.exe`.
Inclut reconstruction partielle des placements, protection des propositions
périmées, sélection liée à l'époque et derniers libellés/attente natifs.
Web/WASM PASS (compilation Rust 59,46 s), packaging PASS, kit 40A55F75 inchangé.
Parcours réel acquis sur copie isolée : sélection/cadrage d'une arche,
Y=3 → 4 m en Preview/Apply, Undo puis Save. Relecture native : placement
exactement restauré, historique conservé (13 reçus, Redo disponible).
Essai et descendants fermés ; détails dans R2_PLACEMENT_INCREMENTAL.md.
Abandon/Redo graphique, plusieurs objets, identité ECS et gain restent dus.
Le code source a depuis évolué : indexation des placements, invalidation des
aperçus de l'inspecteur et commandes de couches de végétation sont absentes
de ce binaire ; leurs contrôles de code ne valent pas qualification graphique.
Réutiliser ce candidat pour ce lot sans reconstruire tant que ses entrées conviennent.
Gate 2 reste ouverte, aucune publication Git ni Gate 3 commencée.

### Candidat précédent conservé

284B775A61486FA858BA6C61691CBFAC1A8DABA1525CE0DF5B1D4C6132F03BF7 :
`E:/OpenRPG_Forge/.gate2-evidence/authoring-scope-studio-20260915/forge-studio-candidate.exe`.
Contient protections des brouillons/commandes multizone et reprise du départ
accepté dans le formulaire. Frontend authoring-scope-web-20260915, kit gouverné
40A55F75 inchangé. Build Web/WASM PASS (58,88 s), packaging PASS, processus
terminés. Départ persistant 40/0/90 acquis par Preview/Apply/Save/reprise puis
Jouer/Retour, archive inchangée après jeu ; essai fermé. Détails et limites
dans R2_PERSISTENT_START.md. Isolation de brouillon sur changement de zone
et coordonnées numériques de la capsule restent à qualifier. Gate 2 ouverte,
aucune publication Git ni lancement de Gate 3.

## Lot courant — 14 septembre 2026

Candidat corrigé C5C3C3B12384A4B71E69A9629D4D59679ABE014B9DA280A7AF4438DF3EA630CF
construit et lancé : libellé de pagination désormais correct, compteur 1–3/3
et navigation désactivée cohérents. Capture pagination-fixed.jpg ; aucun
Apply/Save. Réutiliser authoring-review-fixed-studio-20260914 pour la suite.
Navigation multipage et autres contrôles groupés encore dus ; Gate 2 ouverte.

Essai B4754D5D effectué : diff altitude 0 hérité → 6 retouché réellement
affiché sans Apply/Save, capture terrain-diff.jpg. Défaut découvert dans
« Objets suivants » : code affiché par parsing de >= hors accolades dans view!.
Correctif source appliqué, Clippy PASS ; candidat corrigé et contrôle bouton
encore dus. Essai fermé, aucun verdict global R2-006/035 ni Gate 2.

Candidat groupé des inspecteurs/diffs/pagination construit : B4754D5D440C19F032AFBF8884AFA7B35A6CD734BF358EDD15F28FEDFB336448,
`E:/OpenRPG_Forge/.gate2-evidence/authoring-review-studio-20260914/forge-studio-candidate.exe`.
Frontend `authoring-review-web-20260914`, kit gouverné conservé ; configuration
authoring écran secondaire. BUILD_ONLY, aucun lancement ni verdict graphique.
Rust/WASM PASS (2 min 55), desktop PASS (1 min 16), processus de build terminés.
Réutiliser ce candidat pour les contrôles groupés R2-006/035/047/049 ; ne pas
reprendre F1 pour qualifier les textes ajoutés après sa construction. Aucun push.

R2-035 : résumé terrain avant application lisible (valeurs avant/après,
héritage/retouche, couches vides explicites), lié au hash du reçu et sans
mutation. Test ciblé PASS, fmt et Clippy WASM PASS. Pas de nouveau lancement
graphique ; qualification visuelle groupée encore due, candidat F1 inchangé.
Preuve et limites : docs/evidence/gate2/R2_TERRAIN_DIFF.md.

R2-005 coché : parcours géométrique multizone F1C5DB66 acquis (B=6 m conservé
après B→A→B, A inchangée, Save explicite), ajouté au renommage/reprise D75 et aux
tests hôte indépendance/Undo/Redo/recréation/plafond16. Workspace final 674A0586,
captures dans workspace-geometry-ui-20260914. Huit processus fermés ; aucun
build/push. Limites et sources de preuve dans R2_WORKSPACE_FOUNDATION.md.

R2-005 : tests ciblés géométrie multizone et plafond d’onglets ajoutés, PASS.
A=+3 m/B=-4 m indépendants, Undo/Redo A sans toucher B, Save depuis B puis nouvel
hôte restitue les deux têtes exactes. 17e onglet refusé sans éviction/mutation,
réouverture existante possible, place libérée après fermeture. Clippy test PASS.
Qualification hôte synthétique, pas geste Studio de géométrie ; critère ouvert.

R2-047 : refus de régénération désormais explicatifs et partagés entre
prepare_regeneration et l’exécution brute : codes conservés, cible/verrou,
raison et piste corrective. Un masque orphelin n’est pas présenté comme résolu
par un simple déverrouillage. 4 tests zone_regeneration PASS, Clippy project
lib PASS. Pas de nouvelle résolution automatique ou de qualification GUI.

R2-049 : provenance lisible ajoutée aux inspecteurs porte/placements/chemins/
rivières. Porte : recette, override absolu et valeur effective séparés, verrou
du bâtiment entier explicite ; placements : AssetId/hash et pose absolue ;
tracés : recette auteur acceptée ou nouveau brouillon. Aucune mutation ajoutée.
Test ciblé origine porte PASS (zéro explicite distinct de l’héritage), Clippy
Web/WASM PASS. Texte non encore qualifié graphiquement, absent de F1C5DB66.

R2-045 coché sur preuves consolidées : porte déplacée/bâtiment verrouillé et
clairière peinte conservés à l’identique après régénération 42 → 43 et reprise
dans un nouveau processus Studio. L’ouverture F1C5DB66 du parcours index lève
l’ancienne absence d’observation ; archive 5269A9B0 intacte, comparaison des
corrections recontrôlée. Aucun nouvel essai graphique. Limites explicites dans
R2_ZONE_REGENERATION.md ; Gate 2 reste ouverte.

Index assets F1C5DB66 : parcours réel index → inspecteur → cadrage de l’arche
63a0e640 acquis sans recharger le catalogue. Archive FF89547B inchangée ; capture
asset-framed.jpg dans scene-index-assets-ui-20260914. Aucun Apply/Save ni build
supplémentaire. R2-006 reste ouvert pour qualification des retouches et cas limites.

Index/cadrage F1C5DB66 qualifié partiellement en Studio : sélection du chemin,
cadrage caméra réel, filtre BÂTIMENT et inspecteur correspondant acquis. Archive
5269A9B0 inchangée ; fenêtre et huit processus fermés. Fixture sans placement :
branche assets de l’index non qualifiée. Preuves dans R2_SCENE_INDEX.md.

Candidat index/cadrage construit F1C5DB66 dans
`E:/OpenRPG_Forge/.gate2-evidence/scene-index-studio-20260914` (BUILD_ONLY).
Frontend et packaging passent ; pas encore lancé. Réutiliser pour qualification
groupée, configuration écran secondaire. Pins dans R2_SCENE_INDEX.md.

R2-006 : assets placés intégrés à l’index commun. La sélection est partagée
entre index, liste d’assets et picking 3D, via l’unique liaison existante remontée
au propriétaire des contrôles. La bibliothèque s’ouvre sur sélection ; aucune
copie canonique ni commande durable. Clippy Web/WASM PASS (6,86 s).
Qualification graphique groupée de l’index et du cadrage chemin à effectuer.

R2-006 : index commun des chemins, rivières et bâtiments accepté dans le code
Studio, filtrable par nom/type/ID avec indication des verrous. Les sélections
sont partagées avec les inspecteurs existants (y compris le picking bâtiment),
dont l’ouverture est pilotée par la sélection. Aucun nouveau format ou writer.
Clippy Web/WASM passe (6,87 s) ; interaction graphique non encore qualifiée.
Assets placés et végétation restent dans leurs panneaux dédiés : R2-006 ouvert.

R2-006 : bouton « Cadrer le chemin accepté » implémenté dans l’inspecteur.
Navigation dérivée des points acceptés, de leurs altitudes sur le terrain et de
la largeur ; aucune commande documentaire ni application du brouillon. Clippy
Studio Web/WASM passe (7,22 s). Cadrage approximatif, sans garantie de visibilité
sur tous les reliefs/ratios d’écran ; geste graphique non encore qualifié.
Pas de nouveau candidat graphique lancé pour ce contrôle ciblé.

Consolidation des preuves : R2-001 (nouveau projet) et R2-002 (nouveau module)
qualifiés localement et cochés, sur gestes Studio et reprise des identités après
arrêt. Gate 2 reste ouverte ; pas de validation humaine inventée. Le journal
historique ci-dessous conserve ses anciens constats, supersédés par les preuves
plus récentes détaillées dans R2_WORKSPACE_FOUNDATION.md.

Reprise après arrêt du renommage multizone acquise sur D75CB60E : nom et hash
terrain identiques, archive AB913BF9 inchangée. Le cycle B→A→B→Save→redémarrage
est vérifié pour cette retouche. Capture conservée, tous les processus fermés.
Géométrie multizone/récupération et qualification globale Gate 2 restent ouvertes.

Onglets corrigés vérifiés sur D75CB60E : retouche du nom de B conservée après
B→A→B sans sauvegarde implicite, puis Save explicite acquis. Captures et hashes
dans R2_WORKSPACE_FOUNDATION.md ; candidat fermé. Reprise après arrêt de cette
retouche encore due ; cette preuve ne clôture pas R2-005 ni Gate 2.

Multizone Studio : seconde zone créée/sauvegardée, A→B puis fermeture B→A avec
terrain initial retrouvé sur 7A7812D0. Défaut de visibilité des onglets identifié
et CSS isolée corrigée (Clippy Web PASS), capture corrective encore due. Pas de
preuve graphique de conservation des edits multizones ajoutée ; R2-005 ouvert.

Fermeture multizone améliorée : activation de l'onglet voisin, conservation des
retouches non sauvegardées de celui-ci et aucune sauvegarde implicite. Deux tests
host multizones et Clippy strict PASS ; parcours graphique de fermeture encore dû.

Reprise Studio vérifiée sur candidat A74AEDAE : workspace enregistré rouvert,
trois opérations d'historique visibles et zone gérée affichée avec le même hash
terrain. Archives source/copie inchangées ; barre dans le flux et sections
repliables observées. Candidat et sept descendants fermés. Preuve détaillée dans
R2_WORKSPACE_FOUNDATION.md ; multizone/récupération et Gate 2 restent ouverts.

Historique projets/modules/zones du workspace raccordé : pages de 20 opérations
liées au hash de tête, sans recharger les objets ni modifier les sources. Trois
tests ciblés pagination/reprise/lecture depuis zone gérée PASS ; panneau Studio
codé, qualification visuelle encore ouverte (R2_WORKSPACE_FOUNDATION.md).

Parcours Studio projet→module→zone→Save→OpenManaged réellement effectué sur
candidat 4FB0B367, terrain plat affiché et archive sauvegardée contrôlée. Pas de
reprise après redémarrage ni de récupération/multizone dans ce replay. La barre
fixe masquait des champs : correction codée et Web compilée, encore non capturée.
Candidat fermé, preuve et limites dans R2_WORKSPACE_FOUNDATION.md.

Création de zone directement dans un module raccordée (Examiner/CreateNewZone,
recette terrain existante, objet immuable, Save workspace explicite). Test ciblé
création/reprise/Undo/Redo PASS, aucun fichier de zone historique intermédiaire.
Formulaire module/nom/seed/relief codé ; qualification graphique encore ouverte.

Récupération d'une sauvegarde workspace interrompue raccordée au service/host/Studio :
examen des versions, choix lié aux hashes, archives conservées, objets vérifiés et
verrous source lors d'une promotion. Test ciblé récupération/reprise PASS ; geste
Studio non qualifié. Voir R2_WORKSPACE_FOUNDATION.md ; Gate 2 reste ouverte.

Budget de vérification workspace corrigé : cumul des tailles réellement lues
sous plafond 256 Mio, limite individuelle 4 Mio et lecture bornée au budget
restant. Test ciblé budget exact/insuffisant PASS ; pas de qualification GUI.

Compaction des zones gérées raccordée au service et aux commandes auteur existantes :
revue liée aux références exactes, nouvel objet immuable, Save explicite, ancien
objet conservé. Test tête/historique inchangés, revue modifiée refusée, retry et
réouverture compacte PASS. Aucun essai graphique ; détail R2_WORKSPACE_FOUNDATION.md.

Synchronisation des panneaux corrigée : Save workspace relit l'état auteur ;
Save/ApplyZone/OpenManaged/Close relisent le workspace quand les requêtes sont
terminées. Pas de polling ni de mutation induite. Clippy Web PASS, geste réel non
qualifié. Le candidat 8A39ED7B construit précédemment ne contient pas ce correctif.
Son examen visuel avait été interrompu par Échap ; candidat et sept descendants
fermés/vérifiés, aucun pilotage graphique relancé depuis.

Onglets gérés ajoutés : OpenManaged bascule entre zones sans perdre les edits
acceptés non sauvegardés du workspace. Maximum 16 onglets et une projection active ;
dirty par zone comparé au checkpoint de sauvegarde. Bandeau Studio avec nom/* et
zone active. Test A→B→A sans Save, puis Save/reprise hash exact PASS ; adoption
rejouée PASS. Qualification graphique et ergonomie du bandeau encore dues.

Adoption Studio raccordée : catalogue de zones sauvegardées, choix du module,
ReviewAdoption côté host (source fermée/hash exact/projet compatible), puis Adopt
via le bouton Apply partagé. Une saisie annule la proposition ; aucune adoption
automatique. Parcours service avec revue non mutatrice et hash périmé refusé PASS.
Rendu/geste réel de ce formulaire non qualifiés ; preuves R2_WORKSPACE_FOUNDATION.md.

Zones gérées raccordées au host auteur : OpenManaged, projection en lecture seule
de l'archive acceptée, Preview/Apply des commandes existantes, Save via workspace,
historique et snapshot playtest exact. Mutations globales du workspace refusées
tant que sa zone est ouverte ; Close sale refusé. Boutons d'ouverture des zones
ajoutés à la liste Studio et remplacement du viewport raccordé. Compilation Web
acquise ; aucune observation graphique de ce raccord, adoption UI encore absente.

Formulaires projet/module ajoutés au panneau auteur Studio : ouverture/création du
workspace, clés stables/noms, sélection du projet, Examiner/Apply/Undo/Redo/Save,
liste des projets/modules et indicateur non sauvegardé. Inspect au montage, saisie
annulant l'aperçu, requêtes bornées au host. Clippy Web PASS ; rendu/geste réel non
qualifiés. Adoption et édition gérée ne sont pas encore exposées dans le viewport.

Protocole workspace raccordé au host et à la commande Tauri Studio, avec un unique
host/verrou partagé par zone et workspace. Create/Open/Inspect/Preview/Apply/Adopt/
Save/Close accessibles au backend ; fermeture sale refusée et mutations interdites
pendant le playtest. Parcours host sauvegarde/reprise PASS, Clippy project/Studio
natif PASS. Formulaires visibles et édition gérée dans le viewport encore à raccorder.

Adoption explicite raccordée au service workspace : source sauvegardée/fermée,
hash exact, projet/zone/tête vérifiés ; verrou source jusqu'à sauvegarde de la tête.
Le host refuse ensuite Open/Create/Recover historiques pour les zones gérées,
même après Undo de leur appartenance (pas de migration inverse implicite).
Stockage immuable et relecture des références raccordés. Édition gérée ajoutée
au service : commandes de zone réutilisées, double aperçu zone/workspace,
UpdateZone lié aux hashes examinés, Save explicite et Undo par référence.
Pas encore d'assistant ni de raccord d'édition Studio ; qualification dans
R2_WORKSPACE_FOUNDATION.md.

Projet/module : ADR-0016 accepté explicitement (« j'autorise »). Premier socle
implémenté : documents bornés, commandes Preview/Apply/Undo/Redo, retry, audit,
sauvegarde atomique et réouverture par rejeu dans workspace-v1. Trois tests ciblés
PASS et Clippy core/project PASS. Pas encore de raccord Studio, d'objets immuables
à ce stade initial (adoption ajoutée dans le lot décrit ci-dessus).
R2-001/002/005 restent ouverts ; voir R2_WORKSPACE_FOUNDATION.md.

Contrôle workspace regroupé : 943 tests PASS, 3 opt-in ignorés ; Clippy global
a trouvé une enveloppe StudioCommand trop volumineuse, corrigée sans changement
JSON. Clippy workspace PASS ensuite, 67 tests Studio PASS après correction,
Clippy Web PASS après extraction d'un helper. Détail R2_WORKSPACE_CHECK_20260914.md
et ledger JSON ; pas de qualification propre ni visuelle, Gate 2 reste ouverte.

Historique corrigé : chemins, rivières et bâtiments étaient présents dans les
reçus mais absents du résumé visible. Comparaison par ID avec nom/verrou/hash,
ajout/retrait/modification et couches explicitement vides désormais raccordés.
Trois tests history PASS ; détail R2_OBJECT_HISTORY.md. Aucun format d'archive
modifié et qualification graphique encore due.

Départ : choix par clic terrain raccordé au brouillon, avec garde de tête/champs,
et reprise des valeurs acceptées. Pas d'application automatique. Compilation Web
acquise, geste réel non qualifié : préflight du candidat existant encore refusé
par le pilotage Windows ; processus fermés, aucun nouveau candidat construit.
Voir R2_PERSISTENT_START.md.

Départ de zone persistant implémenté : X/Z/cap dans la correction typée, commandes
Preview/Apply/Undo/Redo, historique et codecs versionnés (tête v6, archives
v16/17/18). Jouer utilise ce départ ; le départ temporaire ne l'écrase pas.
Raccord Studio compilé, contrôle graphique encore dû. Détails et qualification
dans R2_PERSISTENT_START.md ; R2-004 et Gate 2 restent ouverts.

Recette de végétation éditable par couche : probabilité de placement et espacement
minimum via la même régénération transactionnelle, sans perdre les masques peints
ni modifier les autres couches. Champs Studio et diff avant/après raccordés ;
une saisie annule l'aperçu précédent. Quatre tests world et Clippy ciblé PASS,
qualification graphique non acquise. Voir R2_ZONE_REGENERATION.md.

Revue de régénération Studio enrichie : inventaire des retouches conservées,
portée entière du verrou de bâtiment, hash source et comparaison sérialisée
avant d'annoncer une conservation exacte. Aucun format modifié. Clippy Web PASS ;
texte pas encore inspecté graphiquement. Détails R2_ZONE_REGENERATION.md.

Rétention Studio avancée sur le candidat existant A64D5ABA : porte décalée de
1,5 m et bâtiment verrouillé, clairière peinte/verrouillée, seed 42 → 43 puis
sauvegarde. Corrections strictement identiques avant/après ; archive 5269A9B0.
Fermeture complète effectuée, mais observation après relance non acquise :
outil Windows en erreur deux fois, essai arrêté et processus du candidat fermés.
R2-045 reste ouvert ; détails dans R2_ZONE_REGENERATION.md. Aucun nouveau build.

Correctif de l'écran noir confirmé sur A64D5ABA : copie exacte C58F62BC rendue
avec rivière/berges, forêt et maison. Chemin ajouté par geste → Examiner/Apply,
Save, entrée en playtest/retour puis Close/Open : ensemble visible et archive
825ADA4A identique après réouverture. Aucun déplacement/collision ou profil
optique qualifié par cette observation. Candidat et sept descendants fermés.
Parcours sans JSON manuel avancé, R2-043 et Gate 2 encore ouverts ; détails
`docs/evidence/gate2/R2_RIVER_CARVING.md`.

Rivière tracée réellement sur candidat 85FE08E6 : quatre points, Examiner,
Apply/Save/Close/Open et conservation forêt/maison acquis côté données (C58F62BC).
Rendu en échec : panic Bevy de phase différée absente, les berges de l'adapter
auteur restaient Deferred alors que l'eau était Forward. Correction des berges
et test de régression ajoutés (PASS), Clippy engine natif strict PASS ;
qualification du cas graphique obtenue ensuite sur A64D5ABA ci-dessus.
Candidat et console fermés ; détails R2_RIVER_CARVING.md. Gate 2 ouverte.

Tracé continu de rivière raccordé au brouillon Studio : geste X/Z simplifié,
4–128 points, niveau/largeur/profondeur du premier point repris explicitement,
retouches numériques ensuite possibles. Même commande Examiner/Appliquer,
aucune sauvegarde implicite. Trois tests CPU et Clippy natif/Web PASS ; geste
graphique obtenu sur 85FE08E6, rendu corrigé sur A64D5ABA (R2_RIVER_CARVING.md).

Création directe de zone agrégée raccordée au formulaire via CreateZone : plus
de conversion technique à demander pour une nouvelle zone. Ancienne commande
Create et migration explicite des fichiers existants conservées. Checkpoint
initial sans reçu construit en mémoire, premier fichier agrégé seulement à Save.
Quatre tests création/migration/persistance PASS, Clippy domaine/Web PASS. Nouveau
parcours Studio observé sur 0F315A98 : création sans conversion, forêt puis
maison via Preview/Apply, Save/Close/Open, archive F92C7F86 identique et rendu
restauré. Candidat et sept descendants fermés. R2-003 acquis localement dans
le périmètre 512 m/pas 2 m ; parcours auteur complet R2-043 encore partiel.
Détails et limites dans R2_ZONE_CREATION.md ; Gate 2 ouverte.

Création de zone extérieure paramétrée ajoutée : seed, altitude, amplitude et
échelle du relief, résumé avant Create, refus d'un examen périmé. Taille/pas
512 m/2 m conservés ; pas d'assistant projet/module déduit. Préparation domaine,
Save/Open et conservation des réglages à travers Undo/Redo de retouche testés ;
Clippy domaine et Studio Web PASS. Candidat 75DA9340 observé : examen invalidé
après changement d'amplitude, création d'un relief 24 m, Save/Close/Open,
même hash terrain et archive 5B80583F intacte. Processus fermés. Autres gestes
et couverture complète encore dus : `docs/evidence/gate2/R2_ZONE_CREATION.md`.
Gate 2 demeure ouverte.

Sélection directe des bâtiments ajoutée : clic ponctuel sur mesh visible,
occlusion, ID partagé avec l'inspecteur, garde de révision/projection et annulation
par les autres outils. Clippy Web strict PASS ; candidat 4C953E54 observé : clic
sur chacun des deux bâtiments, inspecteur correspondant et clic vide acquis.
Archive B4FB1904 inchangée, processus fermés. Occlusion/annulations non rejouées
graphiquement, aucune hiérarchie globale qualifiée. Voir R2_BUILDING_LAYOUT.md.

Sélection de bâtiment par nom/verrou/ID ajoutée au Studio : un inspecteur ouvert
à la fois, relu depuis la tête courante. ID absent : invite explicite, pas de
substitution silencieuse. Clippy Web PASS ; candidat 82A6A72D observé : archive
B4FB1904 rouverte, sélection et cadrage de chacun des deux bâtiments acquis.
Archive inchangée, candidat et sept descendants fermés. R2-006 reste partiel.

Candidat D29317A0 observé dans le Studio : renommage Auberge et duplication à
(30,3,20), Preview/Apply, Undo/Redo de copie et Save acquis sans JSON manuel.
Deux bâtiments rendus, ID de copie stable après Redo ; archive B4FB1904,
31 commandes, source BA9B8BE5 intacte, processus fermés. Réouverture UI acquise
sur 82A6A72D ; mesure de réutilisation encore due. Voir R2_BUILDING_LAYOUT.md.

Duplication des bâtiments ajoutée au domaine et à l'inspecteur : nouvelle identité,
nom/position explicites, réglages et porte conservés, original intact, copie
déverrouillée. Test preview/retry/Undo/Redo/réouverture et limites PASS. Renommage
et duplication attendent leur observation dans un candidat regroupé ; Gate 2 ouverte.

Renommage des bâtiments ajouté au domaine et à l'inspecteur : aperçu/application
par commande existante de zone, identité/géométrie/porte conservées, verrou respecté.
Test retry et trois archives avec Undo/Redo PASS. Nouveau champ pas encore qualifié
visuellement dans un candidat ; voir R2_BUILDING_LAYOUT.md.

Réutilisation de végétation terminée ajoutée pour les révisions Studio sans
changement géométrique : mêmes entités, nouveaux reçus de source, refus si un
calcul/upload est encore en cours ou si les bindings sont incomplets. Cinq tests
moteur ciblés PASS (bâtiments, chemins, rivières et qualité). Code postérieur au
candidat 21854D33 ; aucun gain de latence/FPS Studio mesuré. Détails et limites
dans R2_BUILDING_LAYOUT.md ; le terrain lui-même reste hors de cette optimisation.

Candidat Studio 21854D33 observé : sol intérieur visible dégagé avec végétation
extérieure conservée. Largeur de porte 1,4 → 1,8 m saisie, Preview/Apply/Save,
fermeture/réouverture et départ playtest devant la porte acquis sur copie isolée.
Décalage manuel X=1,5 m conservé ; archive BA9B8BE5, 27 commandes. Processus
fermés, original intact. Épaisseurs/hauteur et franchissement clavier encore dus.
Ce replay précise les limites des preuves de code ci-dessous ; Gate 2 ouverte.

Exclusion végétale des emprises de bâtiments implémentée dans forge_world :
rectangles tournés, source végétale intacte, hash dérivé des emprises, deux
tests et Clippy PASS. Raccord moteur natif/WASM/Studio implémenté ; Clippy Web
PASS et test moteur ajout/retrait/refus invalide PASS. Rendu encore à vérifier : ne
pas annoncer la végétation traversante corrigée dans le produit. Limite : points
d'implantation, pas découpe des branches extérieures. Voir R2_BUILDING_LAYOUT.md.

Inspecteur bâtiment complété au code : largeur/hauteur de porte, épaisseurs
murs/plancher/toit via SetDimensions existant, visibles dans le résumé d'aperçu.
Clippy WASM PASS ; conservation/régénération/trois archives/Undo-Redo testés avec
ces paramètres. Gestes des nouveaux champs encore dus dans un candidat regroupé.

Franchissement natif prouvé par un nouveau test d'intégration : projection
bâtiment + contrôleur/Rapier de production, quatre rotations, entrée/sortie
par l'ouverture et blocage par le mur voisin. Huit scénarios PASS (0,18 s).
Sol synthétique, commandes rejouées, hash source intact ; pas encore un passage
au clavier Studio. R2-033 reste ouvert. Détails R2_BUILDING_LAYOUT.md.

Départ devant une porte : orientation temporaire vers l'entrée ajoutée au
contrat PlaytestStart, indépendante des données sauvegardées. Ancien JSON X/Z
compatible, cap non fini refusé ; trois tests ciblés domaine/hôte/calcul PASS.
Raccord au rig moteur existant ; candidat regroupé 707BA511 construit et essayé :
ouverture lisible avec lumière ponctuelle, départ face à l'entrée et retour à
l'éclairage normal observés. Aucun franchissement déduit. Archive E9DFB69E
inchangée, processus fermés. Ne pas rejouer ces contrôles acquis sur candidat
inchangé. Suite : passage physique/parcours d'édition agrégé. Détails R2_BUILDING_LAYOUT.md.

Candidat regroupé 7DD26394 construit et essayé : réouverture de la pièce/porte
retouchées, cadrages côté façade et départ direct en playtest observés. Défaut
concret : façade ombragée/intérieur presque noirs ; lisibilité non qualifiée,
pas de franchissement joué. Archive E9DFB69E inchangée. Preuves détaillées dans
R2_BUILDING_LAYOUT.md ; ne pas relancer les anciens tests ni reconstruire ce
candidat inchangé. Éclairage de travail réversible implémenté dans le viewport
d'auteur (y compris playtest, explicitement signalé), sans changer les documents
ni l'éclairage final. Clippy WASM PASS ; candidat 4EFAFB4C construit et essayé :
bascule actif/inactif/réactivé capturée, archive inchangée, processus fermés.
La lumière uniforme distingue mal le fond de pièce de la façade dans l'ouverture :
lisibilité toujours non qualifiée. Lumière ponctuelle liée à la caméra active
ajoutée ; essai 9F38E462 à 2 000 lm insuffisant. Réglage à 100 000 lm compilé
(Clippy WASM PASS), désormais packagé/observé dans 707BA511 ci-dessus. Archive intacte et
processus d'essai fermés. Détails et limites R2_BUILDING_LAYOUT.md.

Accès Studio « Jouer devant cette porte » raccordé : départ terrain temporaire
à 2 m de l'ouverture effective, dans le repère tourné du bâtiment accepté.
Même StartPlaytestAt, validation des limites/hash et barrière physique ; aucune
écriture de point de départ. Test calcul cadrage/départ PASS, Clippy WASM PASS.
Packagé et essayé dans 7DD26394 ci-dessus ; franchissement joué encore à vérifier.

Conservation combinée porte/clairière prouvée sur fixture avec vraie reprise
fichier : nouvelle seed et ordre des couches, fermeture de session, Save/Open
gardent exactement les corrections verrouillées. Cinq tests bâtiments PASS,
Clippy projet tous targets PASS. Conflit de régénération désormais identifié
par bâtiment/verrou si la nouvelle emprise est incompatible. Pas de parcours
Studio ni de verrou fin de porte qualifié ; R2-045 reste ouvert.

Bâtiments : une révision sans changement de géométrie conserve désormais les
meshes/entités/colliders Rapier valides au lieu de reconstruire le lot. Hash de
source actualisé, état physique endommagé ou porte changée toujours reconstruit.
Trois tests moteur ciblés PASS (0,02 s). R2-050 reste partiel : comparaison des
parties encore calculée, changement géométrique reconstruit encore tout le lot.
Pas de gain FPS annoncé ni de nouveau candidat graphique pour ce complément.

Cadrage bâtiments corrigé au code : vue côté façade suivant la rotation, plus
« Cadrer la porte » visant son décalage effectif. Navigation seule, aucune
commande de carte. Test ciblé PASS (quatre orientations, override/recette,
source intacte) et Clippy Studio WASM PASS. Pas encore packagé ni observé :
regrouper avec le prochain candidat, sans rejouer l'essai 168F6C25 inchangé.

Essai ciblé du candidat 168F6C25 réalisé sur écran secondaire, sans rebuild :
cadrage proche, déplacement (20,3,20), correction de porte à X local 1,5 m,
Preview/Apply/Save acquis dans le Studio sur copie isolée. Archive v15, 26
commandes, SHA E9DFB69E ; démarrage du mode playtest observé, pas de franchissement
de porte ni de mouvement qualifié. Les états « non lancé » ci-dessous sont
historiques. Détail : R2_BUILDING_LAYOUT.md. Gate 2 reste ouverte.

Position et orientation de bâtiment ajoutées au domaine et à l'inspecteur :
SetPose, aperçu commun, verrous et empreinte tournée bornée au terrain.
Quatre tests de domaine PASS, dont Undo/Redo et conservation de la porte retouchée.
Geste Studio encore à qualifier ; aucune retouche hors produit de la carte preuve.
Clippy WASM PASS ; candidat regroupé 168F6C25 construit (Trunk/Tauri PASS),
non lancé. Inclut cadrage, pose et barrière Rapier. Copie d'essai prête dans
`.gate2-evidence/building-playtest-ui-20260914` (archive 2B718781).
Réutiliser ce candidat pour le prochain essai, sans reconstruction automatique.

Playtest bâtiments raccordé au code : snapshot immuable avec bâtiments, refus
général remplacé par attente du lot et des colliders réellement synchronisés.
Deux tests moteur PASS (dont attente/registration Rapier et rejet des parties
masquées/périmées), test de non-régression du helper partagé PASS ; trois tests
de domaine PASS avec snapshot gelé. Pas encore de candidat ni passage joué.
Les mentions de blocage systématique plus bas sont historiques.

Essai bâtiments candidat 974CAD82 acquis sur écran secondaire : pièce à (0,3,0),
aperçu cyan lointain, Apply/rendu accepté, Save, fermeture/réouverture avec pièce
retrouvée. Archive isolée v15, 24 commandes, SHA 2B718781 ; original 1E48B2F6
intact. Processus du candidat et descendants fermés et vérifiés absents.
Pas de retouche de porte, abandon/Undo UI ou passage joué qualifié. Cadrage trop
lointain : bouton « Cadrer ce bâtiment » ensuite ajouté au code, Clippy WASM
PASS (6,20 s), pas encore inclus dans un candidat. R2_BUILDING_LAYOUT.md.

Aperçu 3D bâtiments raccordé au code : reçu contrôlé par hash/révision/inverse,
changements bâtiments seuls, lot cyan sans collision et lot accepté conservé.
Le rendu accepté est masqué pendant l'aperçu pour éviter les surfaces coplanaires,
puis restauré à l'abandon ; ses collisions ne sont pas supprimées. Trois tests
de domaine PASS (dont reçu modifié/périmé). Observation graphique encore due.
Les mentions d'aperçu uniquement numérique ci-dessous sont les étapes antérieures.

Projection bâtiments initiale ajoutée au moteur et raccordée au Studio après
application : géométrie et colliders fixes dérivés des mêmes huit volumes,
cube/matériau partagés, ressources possédées libérées au remplacement/fermeture.
Pas de hash/calcul géométrique par image quand la source est inchangée.
L'aperçu Studio reste numérique, playtest toujours refusé en attendant le raccord
de disponibilité physique et le passage joué. Rendu réel non encore observé.
Test CPU de cycle de vie PASS (8 parties, colliders acceptés/aucun collider
aperçu, remplacement et nettoyage des ressources) ; Clippy natif bibliothèque
et Studio WASM PASS. Ne prouve pas la synchronisation Rapier ni le rendu GPU.

Panneau bâtiments raccordé au code Studio : création à coordonnées métriques,
dimensions, correction absolue de porte/retour recette, verrou et suppression.
Toutes les mutations demandent PreviewZone, puis Apply/Save explicites via la
barre existante. Résumé numérique des valeurs proposées, état accepté distinct.
Clippy Studio WASM strict PASS (5,70 s ; avertissement futur d'une dépendance).
Pas de candidat reconstruit ni observation UI ; rendu et collisions à raccorder.
Le panneau annonce cette limite et le blocage du playtest. Gate 2 ouverte.

Suite bâtiments : raccord canonique implémenté (corrections v5, archives
13/14/15), insertion/dimensions/retouche de porte/verrou/suppression via les
commandes de zone. Deux tests d'intégration PASS : aperçu sans mutation,
retouche conservée après agrandissement et régénération avec nouvelle seed,
relecture des trois archives et Undo/Redo, réduction incompatible et suppression
verrouillée refusées sans mutation. Clippy forge_project tous targets PASS.
Le playtest refuse explicitement ces bâtiments tant que la projection manque.
Preuve en mémoire seulement : UI, écriture disque atomique et compatibilité
historique complète restent à qualifier. Aucun essai graphique ni publication.
Les paragraphes ci-dessous décrivent les étapes antérieures de ce lot.

R2-032/033 commencé : calcul moteur-neutre d'une pièce paramétrique (sol,
murs, toit plat, passage de porte) et identités sémantiques stables au
redimensionnement. Trois tests ciblés PASS ; aucun raccord Studio, format
persistant, rendu ou collision jouée encore acquis. R2_BUILDING_LAYOUT.md.
Les critères restent ouverts ; le défaut Échap reste distinct et non résolu.
Paramètres ensuite déplacés dans forge_project et raccordés au générateur,
avec décalage local absolu et identité propre de porte. Agrandir la largeur ne
recentre pas la porte ; réduction incompatible refusée. 6 tests ciblés et
Clippy projet/world tous targets PASS (5,95 s). Payload sérialisable seulement :
pas encore de champ canonique, commande, archive ou UI bâtiment.
Raccord à la tête canonique et aux archives préparé dans R2_BUILDING_PERSISTENCE_PLAN.md,
non implémenté. Défaut de taille du test de transformations corrigé par extraction
de helpers sans perte d'assertions : 2 tests PASS, Clippy forge_world tous targets
PASS (0,36 s). Pas de campagne workspace complète ni lancement graphique.

Candidat D2A2866F : bouton Abandonner et reprise d'un nouvel aperçu de zone
vérifiés réellement, sans Apply/Save ; archive 1E48B2F6 intacte, processus fermés.
Échap n'a pas retiré la proposition dans cet essai : réception DOM/focus non
établie, ce raccourci n'est pas qualifié. Réponse native retardée non reproduite.
Routage clavier ensuite passé en capture : évite le `prevent_default` de winit
qui peut faire ignorer les raccourcis issus du canvas. Clippy WASM PASS (5,91 s),
sans nouveau candidat ; ne prouve pas la cause de l'échec Échap hors canvas.
Preuves et limites : R2_CATALOG_DROP.md. Gate 2 ouverte.

R2-027, annulation renforcée au code : le clic clavier (detail=0) n'est plus
consommé par un ancien drag. Bouton Abandonner et Échap partagent l'annulation
des gestes, contrôle GLB et proposition ; une réponse Preview native tardive
ne réactive plus l'Apply après abandon. Les réponses des mutations déjà envoyées
restent observées : abandon n'est pas un Undo. Format/Clippy WASM PASS (5,61 s).
Pas de candidat graphique reconstruit pour ce complément, cas négatifs toujours
à qualifier. Aucun processus graphique lancé, aucune carte modifiée.

R2-027 : dépôt catalogue → terrain acquis sur candidat 78005EAD. Rayon du
picker corrigé de 1 à 2 m pour respecter son contrat moteur. Aperçu 71→72,
Apply 72, Undo 71, Redo 72, Save puis fermeture/réouverture avec arche visible.
Archive isolée 1E48B2F6 ; 71 placements précédents intégralement conservés,
original F432409E intact. Format/Clippy et builds PASS ; processus fermés.
Cas négatifs du geste encore ouverts, pas de clôture générale de R2-027/Gate 2.
Voir R2_CATALOG_DROP.md. Aucun push ni ancienne campagne de tests rejouée.

Essai R2-027 67BFB82F : retour terminal prouve l'absence d'événement de dépôt,
avant picking/GLB. Sans Apply/Save, archive intacte, processus fermés. Transfert
HTML5 ensuite remplacé au code par un geste souris Studio (clic/drag distingués,
relâchement canvas, même commande gouvernée). Clippy WASM PASS ; geste nouveau
encore à reconstruire/qualifier. R2_CATALOG_DROP.md ; Gate 2 ouverte.

Candidat 67D52876 à taille 1600×900 vérifié entièrement sur écran secondaire
(WASM FC0F38F5 réutilisé). Catalogue chargé, début de drag reçu mais aucun aperçu
après deux gestes : R2-027 non validé. Essai fermé, archive intacte. Transmission
du point de dépôt et messages de fin renforcés ensuite au code, Clippy PASS ;
nouvelle observation due. R2_CATALOG_DROP.md distingue build, geste et limites.

Candidat catalogue/drop 4B9C1211 construit (Trunk/Tauri PASS), copie isolée
F432409E rouverte et rendue. Aucun dépôt ni sauvegarde réalisé : essai fermé
après placement inattendu sur écran principal malgré x=-1920/y=0. Geste R2-027
non qualifié ; réutiliser le candidat, corriger le placement avant reprise.
Voir R2_CATALOG_DROP.md pour hashes, chemins et limite du contrôle multi-écran.

Glisser-déposer catalogue → terrain raccordé au Studio (R2-027) : picking,
GLB contrôlé, garde de tête/ticket, même commande Insert que le clic ; aperçu
sans application implicite. Échelle 1/rotation 0/surface annoncées, fichiers
externes refusés. Tauri configuré pour HTML5 Windows. Clippy WASM PASS ; geste
réel et build candidat regroupé encore dus. Voir R2_CATALOG_DROP.md.

Inspecteur de remplacement enrichi : source/cible avec pins, nombre d'objets
concernés et verrous, avertissements cible identique/quota. Lecture seule avant
la commande existante ; Clippy Studio WASM PASS (5,94 s). Affichage réel de ce
complément non vérifié, pas de reconstruction graphique pour ce seul ajout.

Candidat 2300327E essayé sur écran secondaire : diagnostics visibles, entrée
active et simulation prête, mais aucun mouvement horizontal observé après la
brève touche ; aucune collision qualifiée. Archive F432409E intacte, candidat
et sept descendants fermés et vérifiés absents. Pas de campagne générale rejouée.
Suite R2-025 ouverte, détails dans R2_PATH_PLACEMENT.md.

Candidat Studio 2300327E construit (Trunk/Tauri PASS) : résumé des changements
et diagnostics demandé/résolu regroupés, sept références, écran secondaire.
Application non lancée : contact joué toujours non qualifié. R2_PATH_PLACEMENT.md.

Diagnostics de playtest enrichis au code : entrée active, simulation prête,
dernier déplacement horizontal demandé/résolu et contacts (sol inclus).
Ils ne déclarent pas automatiquement une collision réussie. Clippy Studio WASM
PASS ; observation réelle de ces nouveaux champs encore due. R2_PATH_PLACEMENT.md.

Archive F432409E rouverte graphiquement dans Studio 182C2A03 : rangée corrigée
présente, playtest proche à X=0/Z=5 puis Z=1, retours édition synchronisés.
Raccord droit observé sans ouverture sur le fond ; joint de mortier visible.
Contact contre collision non prouvé par la brève entrée clavier ; angles et
Undo/Redo UI encore dus. Archive inchangée, essai fermé et processus absents.
Voir R2_PATH_PLACEMENT.md ; R2-025 et Gate 2 restent ouverts.

Aperçu des placements enrichi au code : compte les pins remplacés et retouches
au lieu du seul total avant/après. Test ciblé et Clippy Studio WASM PASS ;
pas encore affiché dans un candidat reconstruit. Archive F432409E relue avec le
modèle Rust et réencodée en delta sans perte (20 reçus, 2 617 505 octets,
zéro écriture). L'encodage compact historique dépasse le quota et reste refusé.
R2_PATH_PLACEMENT.md ; parcours de réouverture Studio encore dû.

Studio 182C2A03 sur écran secondaire : catalogue à sept références chargé,
remplacement explicite des 69 murs Preview/Apply/Save acquis. Archive F432409E,
71 identifiants/noms/transforms/verrous conservés, deux arches inchangées.
Vue générale rendue ; réouverture de cette sauvegarde, raccords proches et
collisions jouées encore dus. Essai fermé, processus absents.
Position du candidat configurable via WindowX/WindowY ; tests de packaging PASS.
R2_PATH_PLACEMENT.md ; Gate 2 toujours ouverte.

Candidat Studio C53FDDEA construit (Trunk/Tauri PASS) : commande de remplacement
et catalogue additif à sept références réunis. Aucun lancement ni remplacement
réel encore acquis. Preuves et emplacement dans R2_PATH_PLACEMENT.md.

Catalogue additif assemblé en sortie séparée : six références existantes intactes,
nouveau `vv-architecture-wall-joint-a` (GLB 1BE108A0), même identité de kit,
révision 2. Génération/réimport du mur nommé et contrôle des anciens pins PASS.
Pas de remplacement de carte ; visibilité et geste Studio encore dus.
R2_PATH_PLACEMENT.md détaille les sources et limites.

Remplacement explicite d'asset codé (un objet ou lot partageant le pin attendu) :
transformations/noms/IDs conservés, verrous et révision respectés, Undo/Redo.
Inspecteur relié au catalogue avec contrôle du fichier avant aperçu. Deux tests
ciblés PASS dont réouverture ; Clippy project/tests PASS. Aucun remplacement UI
réel encore acquis, dérivé mur à exposer au catalogue. R2_PATH_PLACEMENT.md.

R2-025 : GLB dérivé D51C291D réimporté, deux panneaux assemblés à 4 m.
Contrôle ciblé du joint : zéro traversée sur 7 686 rayons aux trois LOD ;
témoin ancien détecté en échec aux LOD0/1. Rendu assemblé inspecté, joint
encore visible. Pas de qualification des angles/collisions jouées ni de
remplacement Studio acquis. Preuves : R2_PATH_PLACEMENT.md.

R2-025 : support de mortier porté aux limites largeur/hauteur du module dans
le générateur, sans modifier les placements ni l'ancien kit. Dérivé mur seul
construit (GLB D51C291D), rendu isolé inspecté ; prévisualisation de kits de
taille variable corrigée après échec explicite du premier build. Raccord de
deux panneaux, réimport et remplacement gouverné Studio restent non qualifiés.
Voir R2_PATH_PLACEMENT.md ; aucune campagne globale rejouée.

Rangée sauvegardée rouverte dans le candidat 7F31D8F4 (Trunk/Tauri PASS), entrée
en playtest à X=0/Z=5 et retour édition acquis, archive 4B564C2F inchangée.
Vue proche : fente entre panneaux, cohérente avec mortier réduit à 98 % dans
le générateur du kit. Continuité non validée ; corriger la règle et produire un
dérivé gouverné sans remplacer implicitement les références existantes.
Déplacement contre collisions et maintien des réglages encore dus. Essai fermé,
processus absents. R2_PATH_PLACEMENT.md ; Gate 2/R2-025 restent ouverts.

Rangée de murs observée dans Studio D264A3F0 : carte de tracés existante rouverte,
mur posé puis 68 copies (3→71 placements), Apply/Undo (3)/Redo (71)/Save acquis.
75 primitives Rapier synchronisées, parcours personnage non testé. Archive
4B564C2F conservée ; réouverture de cette rangée et raccords proches encore dus.
Essai fermé, processus absents. Défaut de champs de répétition réinitialisés
après aperçu corrigé au code (brouillon porté par panneau stable), Clippy WASM
PASS ; correctif UI non encore rejoué. Voir R2_PATH_PLACEMENT.md ; R2-025 ouvert.

Candidat d'édition avec kit préparé par `Build-StudioCandidate.ps1 -Authoring` :
source de kit obligatoire, pins vérifiés, options Gate 1 incompatibles refusées.
Tests de packaging et Trunk/Tauri PASS. EXE D264A3F0, WASM C19F882E ; Studio sur
écran secondaire charge six références et contrôle le vrai mur (1 928 420 octets,
SHA conforme, précontrôle GLB PASS). Pas de placement qualifié : dossier
FORGE_AUTHORING_ROOT absent de ce lancement, refus conservé. Session fermée,
processus vérifiés absents. Suite et chemins dans R2_PATH_PLACEMENT.md.

Répétition d'objet sur tracé codée : espacement intersegments, décalages,
orientation et surface effective ; un batch atomique avec copies indépendantes
et source épinglée intacte. Panneau dans l'inspecteur de placement. 5 nouveaux
tests + 7 régressions ciblées PASS, Clippy world et Studio WASM PASS. Qualification d'une vraie
clôture/mur et replay Studio encore dus ; R2-025 reste ouvert. R2_PATH_PLACEMENT.md.

Fusion dérivée des surfaces de chemins ajoutée : partition des recouvrements
par couverture maximale, priorité stable, hauteur du terrain effectif, sources
intactes et quotas explicites. 4 tests union + 14 régressions ciblées PASS,
Clippy world et Studio WASM PASS. Raccord automatique à l'aperçu/projection Studio existants ;
rendu et réactivité sur grands tracés non qualifiés, aucun nouveau replay GUI.
R2-024 ouvert ; voir R2_PATH_INTERSECTIONS.md.

Points de croisement chemins implémentés : X/T et auto-croisements, insertion
commune ordonnée sans déplacer les contrôles existants, verrous/quotas et commande
unique Preview/Apply/Undo. Bouton Studio raccordé ; 5 tests ciblés PASS (0,01 s),
Clippy world/test et Studio WASM PASS. Fusion des surfaces de carrefour et replay
graphique encore dus ; R2-024 ouvert. Détails dans R2_PATH_INTERSECTIONS.md.

Replay 15C48C0E : panneau rivière corrigé confirmé (quatre points), examen et
Apply/Save acquis. Archive 10/tête 4/révision 2 avec une rivière conservée.
Rendu refusé : eau recette révision 1 contre terrain effectif révision 2.
Projection corrigée pour aligner uniquement la copie dérivée, source intacte ;
test moteur ciblé PASS (0,03 s), Clippy moteur/tests PASS. Nouveau candidat
B2E07599 construit (Trunk/Tauri PASS), mais contrôle interrompu par Échap avant
réouverture. Rendu et playtest rivière encore non qualifiés. Processus fermés.

Replay C4C14623 : création/save/conversion/save de zone acquises, terrain rendu,
statut eau vide acquis. Défaut UI révélé à l'ouverture rivière : comparateurs
non délimités dans attributs view! produisaient du texte et 128 points de
brouillon sans clic d'ajout. Conditions délimitées corrigées dans rivière et
insertion médiane chemin, Clippy WASM PASS ; correctif non rejoué graphiquement.
Aucune rivière appliquée, archive v1/head v1 sans rivière conservée, essai fermé.

Playtest rivière raccordé au code : snapshot natif fige les recettes sans rendre
le terrain creusé canonique ; Studio dérive terrain/identité végétale et attend
les ressources eau concordantes (y compris retrait vers zéro rivière).
Tests project rivière/host PASS, Clippy project/tests et Studio WASM PASS.
Refus global levé au profit des gardes ; aucun playtest graphique rivière acquis.
Nage/physique de l'eau non implémentées, parcours réel et collider à qualifier.

Barrière CPU/ECS d'eau ajoutée pour le futur raccord playtest : tête/terrain/IDs,
entités, meshes, matériaux, image et liaisons vérifiés. Test ciblé PASS (0,05 s),
Clippy moteur/tests et Studio WASM PASS. Refus du playtest rivière toujours actif :
snapshot canonique/terrain creusé et collider restent à raccorder ; aucune preuve GPU.

Édition rivière directe raccordée au brouillon : clic terrain pour X/Z d'un
point en conservant Y/largeur/profondeur, garde hash zone + brouillon, annulation
et désabonnement ; insertion médiane interpolant les cinq valeurs sans déplacer
les voisins. Clippy Studio WASM PASS (2,91 s). Aucun geste réel observé pour ce
lot ; candidat conservé 133FE370 antérieur, preuve GUI toujours due.

État d'eau visible raccordé : attente terrain, échange moteur ou erreur explicite,
sans qualifier le GPU. Fin de session/échec retire l'eau possédée même sans terrain ;
réessai sur relecture, pas de boucle par frame. Trois tests moteur ciblés PASS
(1 nouveau), 0,12 s après 1 min 42 s de compilation ; Clippy moteur/tests et Studio
WASM PASS. Candidat 133FE370 antérieur, aucun replay graphique de ce lot.

Candidat rivière 133FE370 construit et ouvert : coque Studio capturée, aucune
zone ouverte. Essai fonctionnel interrompu par protection de contrôle Windows
(interactions utilisateur répétées, fenêtre Chrome recouvrante) ; aucune
création/édition revendiquée. Processus essai et descendants arrêtés/vérifiés,
racine d'essai vide. Binaire conservé pour reprendre sans rebuild ; détails
dans R2_AUTHORING_WATER_ADAPTER.md. Aucun verdict graphique rivière acquis.

Panneau rivière Studio codé : sélection/création, nom, points X/Y/Z, largeur et
profondeur par point, berges/budget, verrouillage/suppression. Toutes les actions
passent par PreviewZone puis Apply existants ; résumé avant/après et aperçu du
relief dérivé, pas d'aperçu d'eau proposé. Clippy Studio WASM strict PASS (2,07 s).
Panneau non encore observé dans un candidat graphique ; picking rivière,
profils optiques, état d'erreur utilisateur et playtest restent à terminer.

Brosse sur terrain dérivé raccordée : seuls les échantillons modifiés par le
geste deviennent des corrections canoniques, pas tout le lit procédural.
Le refus global de brosse avec rivière est remplacé par un précontrôle précis :
reprojection identique au résultat demandé ou conflit explicite sans mutation.
Trois nouveaux tests PASS : creusement manuel, extérieur, conflit, absence de
rivière, Preview/Apply/retry/Undo/Redo, régénération compatible et trois archives.
Changement de seed avec retouche toujours refusé par la protection existante.
Clippy world/test et Studio WASM PASS ; pas de preuve graphique de cette brosse.

Raccord Studio terrain/eau/végétation ajouté via cache dérivé borné à une tête.
Chemins, repères, aperçus et alignement des objets utilisent le relief creusé.
Échange d'eau différé jusqu'au hash terrain accepté ; aucune source réécrite.
Un nouveau test ciblé chemins/rivière PASS ; Clippy Studio WASM strict PASS.
Pas de candidat graphique lancé. Panneau rivière et playtest restent dus ;
brosse relief temporairement refusée avec rivière tant que la conversion vers
les corrections canoniques n'est pas implémentée. R2-023/Gate 2 restent ouverts.

Adaptateur d'eau remplaçable ajouté au moteur : préparation sur tête/terrain,
échange ECS gardé, ressources possédées libérées au retrait et animations par
rivière. Deux tests headless PASS ; pas d'affichage Studio ni de preuve GPU.
Raccord des consommateurs de terrain et panneau encore dus ; détail dans
`docs/evidence/gate2/R2_AUTHORING_WATER_ADAPTER.md`.

Rivières transmises au renderer végétal (synchrone, worker natif, incrémental
WASM, reprise et identité des gardes). 17 tests moteur PASS, 2 sondes ignorées ;
Clippy natif/tests et Studio WASM PASS. Studio ne transmet pas encore ces recettes,
terrain/eau et playtest restent à raccorder ; aucune qualification GPU déduite.

Dégagement rivière raccordé au générateur végétal : largeur variable, conservation
des masques/exclusions manuelles, identité composite sans changer le cas sans rivière.
8 tests intégration + 1 géométrique et Clippy world PASS. Transmission renderer/
Studio/playtest encore due ; pas de preuve GPU (R2_RIVER_CARVING.md).

Projection rivière depuis la tête ajoutée : terrain/eaux dérivés sans accumulation,
retouches de hauteur différant de la base protégées (conflit explicite), précontrôle
Insert/Replace partagé. 7 tests world et Clippy PASS. Raccord végétation/renderer/
Studio/playtest toujours dû ; aucune preuve GPU déduite (R2_RIVER_CARVING.md).

Recettes de rivière ajoutées à la tête v4 et aux archives v10/11/12 : commandes,
verrous, Undo/Redo, retry et conservation après régénération/relecture testés.
4 tests rivière/chemins + 10 régressions PASS (1 opt-in ignoré) ; Clippy project
et Studio WASM PASS. Rendu/creusement non raccordés à effective()/Studio et playtest
explicitement refusé avec rivière. R2-023 reste ouvert (R2_RIVER_CARVING.md).

R2-023 : préparation pure de creusement depuis TerrainDocument + WaterDocument
linéaire à niveaux explicites ajoutée, avec budget de coupe, transition de berge
et conservation des retouches extérieures. Trois tests ciblés et Clippy PASS.
Recette dans la tête/archives ajoutée ensuite ci-dessus ; projection transactionnelle,
outil Studio et qualification graphique restent dus (`docs/evidence/gate2/R2_RIVER_CARVING.md`).

Insertion au milieu d'un segment de chemin raccordée au brouillon Studio,
sans déplacement des voisins ni application implicite. 14 tests world ciblés
PASS dont Preview/retry/Undo/Redo/relecture, Clippy world/Studio WASM PASS.
Bouton encore à observer dans un prochain candidat groupé ; 5BE8BF4B le précède.

Repères facultatifs des points de chemins ajoutés et observés dans le candidat
5BE8BF4B : trois repères, sélection orange, désactivation sans modifier le tracé.
Ouverture, végétation dégagée, Jouer puis retour édition acquis sur la même copie.
Archives source/copie intactes, processus de l'essai fermés. Pas de preuve GPU
64 chemins ni de nouvelle qualification performance. R2-021/Gate 2 ouverts ;
hashes, parcours et limites dans `docs/evidence/gate2/R2_SPLINE_EDIT.md`.

Raccord des dégagements séparés codé dans Studio, préparation native/WASM,
qualité, commit d'upload et garde playtest. 16 tests renderer + 13 world PASS,
2 sondes ignorées préexistantes ; Clippy natif/WASM PASS. Replay ciblé acquis
ensuite sur 5BE8BF4B ; le candidat 7107B3C5 antérieur ne couvrait pas ce raccord.

Fondation CPU de dégagement végétal séparé ajoutée : le générateur accepte
64 chemins en plus des 32 exclusions manuelles, source/masques inchangés.
Cinq tests ciblés et Clippy PASS. Raccord renderer/Studio/playtest réalisé
ensuite (ci-dessus) ; l'adapter historique ne reste que dans les tests de référence.
Ce lot n'était pas inclus dans le candidat graphique 7107B3C5.

Replay corrigé 7107B3C5 : clic terrain et tracé continu renseignent le brouillon ;
aperçu cyan visible, abandon restaure le chemin accepté, Apply devient brun et
Undo restaure les trois points initiaux. Copie/source sauvegardées intactes,
processus fermés. Qualification bornée : pas de Save/Redo/playtest de ce geste.
Preuves dans R2_SPLINE_EDIT.md ; Gate 2 reste ouverte.

Replay outils 826A3551 : défaut réel de picking identifié (rayon 1 m refusé
par le contrat moteur 2..=128 m). Deux appels chemins corrigés à 2 m pour le
contour uniquement ; nouveau replay encore dû. Copie/source intactes, processus
fermés. Pas de validation des gestes ou du cyan déduite du build ; Gate 2 ouverte.

Aperçu de surface des chemins avant Apply raccordé : projection temporaire cyan
sur terrain accepté, restauration à l'abandon et exclusion du mode aperçu de la
barrière playtest. Huit tests surface PASS ; rendu réel encore dû. Végétation
acceptée inchangée pendant l'aperçu, aucune nouvelle source canonique.

Tracé continu des chemins raccordé au brouillon : geste terrain, simplification
déterministe à 1 m aux échantillons, 128 points maximum avec refus explicite.
Trois tests CPU et Clippy natif PASS ; interaction réelle encore à qualifier.
Aucune application ni sauvegarde implicite ; détails dans `R2_SPLINE_EDIT.md`.

Points de chemin plaçables par clic terrain dans le brouillon : picking existant,
garde hash/liste, aucune application implicite. Clippy WASM PASS ; parcours réel
de ces nouveaux boutons encore dû, candidat FD7CC2D7 antérieur à ce changement.

Replay multipoint FD7CC2D7 acquis : Ajouter un point fonctionne, saisie conservée
après Examiner, virage appliqué visible puis sauvegardé/relu (v7, 15 commandes).
Copie jetable uniquement ; original intact et processus fermés. Vue rapprochée,
refus UI, Undo/Redo graphique et gestes restent dus. Preuves : `R2_SPLINE_EDIT.md`.

Préparation des chemins Studio : contrôle CPU du ruban et de la végétation
dérivée avant PreviewZone pour Insert/Replace. Erreur sans mutation ; Unlock/Remove
restent possibles pour réparation. Six tests surface PASS et Clippy natif PASS.
Contrôle UI réel encore dû, calcul synchrone non qualifié en temps interactif.

Inspecteur chemins : brouillon isolé des simples notifications Preview/Inspect/Save
par comparaison de la cible ; remise à zéro explicite par Nouveau chemin.
Clippy WASM strict PASS, maintien réel des champs/focus encore à qualifier avec
le candidat multipoint. Aucune nouvelle archive ou publication.

Raccords multipoints codés : sections en onglet et indices partagés, contrôle des
plis/demi-tours, dégagement végétal conservateur du coin extérieur. Sept tests
géométrie/végétation et Clippy natif/WASM PASS. Rendu non rejoué ; le candidat
AF3EF695 précède ce changement. Quota 32 exclusions (corridors + coins) et
intersections complexes restent des limites ouvertes. Détails : `R2_SPLINE_EDIT.md`.

Replay regroupé `AF3EF695…` acquis : chemin numérique créé, diff examiné,
Apply/Save v7 (14 commandes), playtest actif au point X/Z=0 et personnage au sol,
retour édition puis réouverture par catalogue avec chemin visible. Archive
originale intacte et processus de l'essai fermés. Défaut d'attribut Leptos du
bouton Ajouter un point corrigé ensuite dans les sources ; correctif pas encore
rejoué graphiquement. Preuves et hashes : `docs/evidence/gate2/R2_SPLINE_EDIT.md`.
Gate 2 ouverte : édition multipoint/gestes, raccords et intersections encore dus.
Les notes antérieures ci-dessous décrivent l'avancement successif, pas le verdict actuel.

Exclusions végétales de chemins dérivées depuis les mêmes points/profils :
masques enregistrés inchangés, corridor testé vide d'instances générées.
PlaytestSnapshot transporte les chemins immuables ; Studio attend les surfaces
présentes et la végétation dérivée correspondante plutôt que refuser toute zone
avec chemins. Clippy WASM PASS ; barrière de projection codée, replay réel encore
dû. Quota actuel d'exclusions (32 au total) conservé avec erreur explicite.

Surfaces de chemins dérivées et raccordées au Studio : rubans subdivisés selon
largeur/accotement, sommets sur triangles terrain +2 cm, aucune écriture terrain
ou collision. Remplacement des meshes possédés après acquittement du hash terrain,
sans recalcul de tête à chaque frame d'attente. Deux tests géométriques PASS
(0,12 s), Clippy WASM strict PASS. Rendu réel non capturé ; raccords entre segments,
intersections, végétation et playtest restent à traiter. Les sources et le panneau
sont implémentés, pas encore qualifiés graphiquement. Gate 2 reste ouverte.

Panneau chemins Studio raccordé au bus PreviewZone : création, sélection d'un
seul tracé, points X/Z ajout/suppression/réordre, largeur/accotement, nom/verrou.
Récapitulatif textuel exact avant/après ; application et sauvegarde restent
les commandes communes. Clippy WASM strict PASS ; panneau non lancé/capturé,
gestes 3D et projection encore absents. Le panneau annonce cette limite et
le refus temporaire de playtest. Prochaine étape : projection terrain des chemins
puis un replay graphique regroupé, pas une qualification visuelle présumée.

Chemins manuels maintenant persistants dans `ZoneCorrections.paths` : IDs,
points, largeur/accotement, verrous et commandes du journal commun. Tête v3 ;
archives v7 delta/v8 intégrale/v9 compacte sans changement des anciens hashes
lorsque le domaine est absent. Deux tests chemins + six placements + un host
PASS ; Clippy project/all-targets PASS. Clippy project+world/all-targets reste
en échec sur `placement_transform_authoring.rs::exercise_transform_history`
(122 lignes), non corrigé dans ce lot. Raccord Studio/projection encore dus ;
playtest refusé si chemins présents non projetés. Gate 2 ouverte.

Préparation des retouches de tracés ajoutée : Insert/Replace/Remove typés sur
WorldSketch, hash exact, verrous et aperçu avant/après sans mutation source.
Deux tests ciblés et Clippy natif strict PASS. Pas encore raccordé à la tête
de zone ni au Studio : recette acceptée/persistance et commandes produit restent
à réaliser. R2-021 ouvert ; `docs/evidence/gate2/R2_SPLINE_EDIT.md`.

Départ temporaire du playtest implémenté de bout en bout : X/Z validés par le
host sur snapshot exact, conservés lors du remontage WebView, Y issu du terrain,
aucune mutation de zone. Jouer/Retour déplacés dans la barre fixe. Clippy WASM
et 9 tests project/host PASS ; affichage et apparition au point choisi encore
à vérifier. Preuve : `docs/evidence/gate2/R2_PLAYTEST_START.md`.

Raccord placements/playtest implémenté : snapshot immuable, racines/source
exactes, hiérarchie et pose/forme/échelle Rapier synchronisées avant Drive.
Ancien refus systématique remplacé par cette barrière. Tests project/host et
moteur ciblés et Clippy Studio WASM final PASS. Replay Studio `77049619…` :
zone avec deux arches chargée, Jouer → simulation active/personnage au sol →
retour édition acquis, archive v6 identique, processus fermés. Mouvement clavier
et contact/passage de l'arche non qualifiés ; point de départ fixe trop éloigné.
Prochaine étape : qualifier le départ choisi et le parcours de collision,
sans retoucher la map pour contourner cette limite. Gate 2 reste ouverte.
Détails dans `docs/evidence/gate2/R2_MANUAL_COLLISIONS.md`.

Les primitives UCX réutilisent le constructeur convex du kit gouverné, sans
enveloppe globale de l'asset. Absence, échec et attente restent explicites.
Historique et preuves : `docs/evidence/gate2/R2_MANUAL_COLLISIONS.md`.

Ctrl+Entrée raccordé à l'application de l'aperçu déjà examiné, hors champs de
saisie et sans sauvegarde implicite. Erreur de préparation locale traitée avant
effacement de l'aperçu précédent. Test de raccourcis et Clippy Studio WASM strict
PASS ; parcours clavier et maintien visuel de l'aperçu encore à qualifier.

Actions de zone regroupées dans une barre sticky de l'espace auteur : mêmes
commandes Apply/Undo/Redo/Save/abandon, diff et statut à proximité. Clippy WASM
strict PASS ; maintien à l'écran, focus et recouvrement non vérifiés visuellement.

Inspecteur de placements désormais unique : sélection par liste nom/ID/position
ou picking 3D, aucun inspecteur sans sélection ; ajout d'objet replié. Changer
de sélection annule les gestes transitoires sans mutation de zone. Clippy WASM
strict PASS ; parcours graphique et résolution du ciblage encore non vérifiés.

Candidat regroupé `09ea75b6…` construit (Trunk/Tauri PASS) puis ouvert : sauvegarde
v6 relue depuis le catalogue, terrain et deux arches affichés, panneaux présents.
Gestes non qualifiés : ciblage de commandes incohérent après défilement pendant
l'essai, cause non établie. Aucun Apply/Save, archive intacte, fenêtre Studio fermée.
Preuve détaillée dans `docs/evidence/gate2/R2_MANUAL_PLACEMENTS.md`.

Pivot de rotation désormais partagé par saisie et anneaux : même point capturé
pour dessin, picking, rayon et transformation finale, avec repère local/mondial
et pas angulaire. Clippy Studio WASM strict PASS. Replay graphique regroupé
encore dû ; ce raccord ne valide pas à lui seul R2-048 ou Gate 2.

Pivot de rotation numérique au choix : pivot propre ou point monde précis en
mètres. Position et orientation tournent ensemble, échelle conservée ; aucune
modification du GLB ni charnière gameplay. Deux tests géométriques et Clippy
Studio WASM strict PASS. Parcours UI encore dû ; gizmos sur pivot propre uniquement.

Déplacement local numérique raccordé aux mêmes règles et paramètres que les
poignées. Deux tests intégrés PASS, dont équivalence exacte saisie/calcul de
geste, Preview/retry/Undo/Redo/verrou/régénération/relecture v6. Clippy Studio
WASM strict PASS ; parcours visuel réel encore dû, Gate 2 ouverte.

Translation locale ajoutée aux poignées : orientation initiale commune au dessin,
picking et rayon, distances en mètres indépendantes de l'échelle ; grille relative
au départ sur l'axe choisi. Surface/local simultanés refusés explicitement.
Test ciblé et Clippy Studio WASM strict PASS ; gestes locaux et qualification
complète des gizmos encore dus. Aucun rendu ni build applicatif relancé.

Choix local/mondial des anneaux raccordé au Studio : même repère initial pour
dessin, sélection, rayon et composition finale, indépendant de l'échelle de
l'objet. Compatible avec le pas angulaire. Clippy Studio WASM strict PASS ;
geste sur objet incliné encore à qualifier. Translation locale encore due.

Alignement angulaire des anneaux ajouté : Libre / 1 / 5 / 15 / 30 / 45 / 90°,
pas capturé à l'activation, arrondi relatif à la transformation initiale et
règle commune à l'aperçu/final. Test ciblé et Clippy Studio WASM strict PASS ;
geste visuel non rejoué. Aucune nouvelle dépendance ni modification de format.
Les processus de l'ancien essai graphique sont désormais absents, archive
v6 intacte. Preuves dans `docs/evidence/gate2/R2_MANUAL_PLACEMENTS.md`.

Replay Studio regroupé `5fc11a29…` : conversion explicite du journal puis Save
v6 acquis (13 commandes, 2 560 237 octets, tête conservée). Une poignée Y a
produit un aperçu de déplacement, non appliqué avant interruption par Échap.
Aucune qualification complète des gizmos ni réouverture UI v6 déduite. Le
fichier sauvegardé a été relu et son hash confirmé après interruption ; détails
dans `docs/evidence/gate2/R2_MANUAL_PLACEMENTS.md`. Gate 2 reste ouverte.

Poignées d'échelle locale X/Y/Z ajoutées : rayon converti dans l'orientation de
départ, facteur relatif sur un seul axe depuis le geste initial, pivot/rotation
et autres échelles conservés. Géométrie de poignée cubique, échelles nulles,
négatives ou non finies refusées. Test ciblé et Clippy world/Studio WASM PASS.
Les trois modes gizmo sont implémentés mais aucun n'est qualifié graphiquement.
Leurs états historiques détaillés ci-dessous ne remplacent pas ce statut courant.

Anneaux de rotation mondiaux X/Y/Z implémentés : projection/picking des anneaux,
calcul rayon/plan moteur-neutre et composition autour du pivot, root temporaire
puis Preview au relâchement. Test ciblé angles/cas invalides PASS ; Clippy world/Studio WASM PASS.
Affichage/geste non qualifiés. Poignées d'échelle encore dues ; Gate 2 ouverte.

Alignement du gizmo de translation raccordé : paramètres grille/surface capturés
au départ, plan terrain construit une seule fois, règle partagée durant le geste
et au relâchement. Seul l'axe horizontal actif est quantifié ; Y/surface simultanés
refusés explicitement. Test ciblé PASS, Clippy world et Studio WASM PASS ; qualification UI due.

Premières poignées de translation X/Y/Z implémentées : gizmos Bevy dans la vue,
sélection de segment projeté, déplacement temporaire du root pendant le geste,
commande Preview au relâchement. Axes mondiaux et pivot de saisie conservé ;
source jamais mutée pendant le glissement. Annulation restaure uniquement un root
dont la transformation canonique correspond encore au départ. Test rayon/axe
et Clippy world/Studio WASM PASS. Pas de preuve graphique de ces poignées ;
grille/surface sur gizmo et poignées rotation/échelle encore dues. Gate 2 ouverte.

Déplacement par glissement terrain raccordé à l'inspecteur : offset de saisie
conservé, X/Z relatifs, altitude conservée sauf alignement surface explicite,
options grille/surface partagées. Un geste prépare une commande au relâchement,
sans cumul de mutations ; tête changée pendant le geste refusée. Test mathématique
et test intégré historique/verrou/régénération/relecture PASS ; Clippy world et
Studio WASM PASS. Geste UI non qualifié ; pas encore de poignées gizmo ni de
déplacement visuel continu de l'objet pendant le geste. Aucun rendu relancé.

Rotation locale/mondiale autour du pivot et échelles X/Y/Z précises implémentées,
règles partagées `forge_world::placement_transform` et inspecteur Preview/Apply
existant. Deux tests mathématiques et un test intégré Undo/Redo, verrou,
régénération/relecture v6 PASS ; Clippy world et Studio WASM PASS. Affichage Studio encore dû.
Gizmos non implémentés par ce panneau numérique ; R2-029/Gate 2 ouverts.
Le candidat `a8344c0d…` ci-dessous précède ces nouveaux contrôles.

Candidat journal v6 construit : Trunk/WASM et Tauri debug/no-bundle PASS,
SHA-256 `a8344c0d95c29fa8483d52b6688efe28aefba65242c663ebcc3ab4e5779d5b2a`.
Contrôles supplémentaires Regenerate/Undo/Redo et bornes d'expansion PASS.
Essai UI non réalisé : activation Windows refusée (`GetCursorPos`, accès refusé),
capture montrant le fond d'écran ; aucune conversion ni retouche effectuée.
Copie isolée `authoring-delta-ui-projects-20260913` intacte (`31fa6135…`).
Studio et sept descendants arrêtés, absence vérifiée. Reprendre le parcours court
conversion/alignement/Save/réouverture quand le bureau est contrôlable ; Gate 2 ouverte.

Archive v6 sans répétition des corrections inchangées implémentée via conversion
explicite du journal (versions historiques inchangées à l'ouverture). Tous les
reçus/Undo/Redo sont reconstruits et vérifiés, aucune commande supprimée.
Replay CPU de l'archive bloquée : 3 963 115 → 2 560 237 octets, 13 commandes
identiques ; retouche suivante acceptée et relue, archive à 2 561 438 octets.
Source disque intacte. Tests delta, host/save et placements PASS ; Clippy project
et Studio WASM PASS. Nouveau parcours de conversion/alignement Studio encore dû ; Gate 2 ouverte.

Sélection directe essayée sur candidat `34bca4ce…` : clic sur l'arche gauche,
identité b9ed8cc5… et inspecteur filtré acquis, sans mutation. Construction
Trunk/Tauri et Clippy PASS. L'alignement combiné a été refusé avant mutation :
archive v5 de 3 963 115 octets / 13 commandes, nouvelle commande dépassant 4 Mio.
Le format compact répète encore toutes les corrections dans chaque Edit ; la
dernière commande seule représente environ 387 ko. Priorité suivante : améliorer
l'encodage sans perdre commandes/Undo ni augmenter arbitrairement la limite.
Archive de test inchangée `31fa6135…`, Studio et sept descendants fermés.
R2-028/R2-031 ouverts ; alignement graphique non validé par cet essai.

Alignement grille/surface ajouté à l'inspecteur de placement (déplacement/copie).
Règle partagée `forge_world::placement_snap`, grille mondiale X/Z de 0,01 à 128 m,
puis pivot sur triangles du terrain effectif avec offset explicite. Transformation
absolue, rotation/échelle préservées, aucune liaison automatique après régénération.
Trois tests CPU ciblés et Clippy world/Studio WASM strict PASS ; formatage PASS.
Insertion au clic ensuite raccordée à cette même règle, grille facultative validée
avant chargement GLB. Test d'intégration supplémentaire PASS : Preview/Apply/retry,
Undo/Redo, changement d'altitude terrain, relecture compacte et refus de plan périmé.
Clippy world/tests et Studio WASM PASS. Nouveaux contrôles pas encore essayés
graphiquement ; R2-031 reste ouvert. Aucun Studio/processus GPU lancé pour ces lots.

Manipulation précise/duplication ajoutées aux placements : translation X/Y/Z
prévisualisée conservant rotation/échelle, copie avec nouvel ID et original intact.
Quatre tests placements et Clippy project/Studio WASM PASS. Candidat `35580363…`
compilé et essayé sur copie isolée : cadrage réel, déplacement X = -84 m,
duplication X = -90 m, application et sauvegarde de deux identités distinctes.
Archive `31fa6135…`, référence précédente intacte. Studio et sept descendants
fermés et vérifiés absents. Relecture UI de cette nouvelle copie, Undo/Redo UI,
gizmos et collisions restent dus ; aucun nouveau passage de gate.

Placement interactif raccordé : asset gouverné, clic terrain, proposition/application,
verrou et suppression ; projection persistante LOD0 dans le monde existant.
Clippy Studio WASM PASS. Rayon de picker corrigé après premier essai. Replay
`ff785805…` acquis : arche placée, appliquée, sauvegardée puis retrouvée après
fermeture/réouverture sur copie isolée. Archive v5 `ff89547b…`, source intacte,
Studio et sept descendants fermés. Pas de collision manuelle ni de nouveau passage
de gate ; gizmos et qualification complète encore dus. Voir R2_MANUAL_PLACEMENTS.md.
Les paragraphes suivants conservent les preuves des étapes antérieures.

Raccord de référence asset ajouté : identité stable kit/identifiant et contenu
épinglé, refus des références absentes/modifiées, chemin partagé par contrôle GLB
et aperçu temporaire Studio. Quatre tests catalogue et Clippy assets/Studio WASM
PASS. Placement interactif et projection persistante restent à raccorder.

Placements manuels persistants ajoutés à la tête de zone : IDs stables, asset
épinglé, transformation absolue et verrou, commandes/Undo/Save existants. Tête v2,
archives v4/v5 seulement après commande explicite, lecture historique préservée.
12 tests ciblés PASS dont Save/réouverture disque ; Clippy project/Studio WASM PASS.
Geste Studio et projection 3D encore dus ; playtest refusé si des placements ne
sont pas projetables. Voir `docs/evidence/gate2/R2_MANUAL_PLACEMENTS.md`.
Les étapes antérieures ci-dessous ne valent pas qualification de ces nouvelles données.

Replay playtest unifié acquis sur candidat `6c932781…` : ouvrir la zone avec sa
clairière, Jouer, capsule au sol et végétation, retour édition sans Save. Archive
SHA-256 `0b3bf389…` inchangée, Studio et 7 descendants fermés. Déplacement clavier
WebView non qualifié : outil Windows reçu sans code physique. Tests contrôleur
séparés ; pas de verdict performance ni passage de Gate 2. Voir R2_AUTHORING_PLAYTEST.md.
Les collisions végétales déclarées sont maintenant remplacées lors d'un changement
de source : 3 tests végétation PASS (1 probe CPU ignoré), Clippy moteur/tests et
Studio WASM PASS. Les changements de qualité conservent les identités physiques.

Raccord playtest agrégé implémenté côté host/Studio : snapshot effectif conservé,
réponse revalidée, bouton Jouer sur hash de tête, attente des projections avant
Drive, retour édition sans Save. Tests domaine/host et Clippy project/Studio WASM
PASS ; nouveau replay graphique en préparation, pas encore qualifié. Correction
complémentaire des collisions déclarées après régénération végétale en validation.
Les paragraphes ci-dessous conservent l'historique des étapes, pas l'état final.

Barrière moteur de préparation ajoutée : `PlayerSimulationReady` suspend le
contrôleur, purge les entrées et les vitesses pendant l'attente. 17 tests contrôleur
PASS, Clippy moteur strict PASS. Projection végétale Studio séparée de l'autorisation
de retouche ; raccord identité agrégée/verrou et parcours Jouer/retour encore dus.
Pas de nouveau verdict de gate ni de lancement graphique pour ce lot.

Préparation playtest unifiée codée : snapshot effectif immutable, identité de tête
et hashes terrain/végétation séparés, barrière de concordance des projections.
`PreparePlaytest` natif retourne l'identité sans simulation ni écriture. Tests
ciblés et Clippy project/Studio WASM PASS. Démarrage agrégé toujours refusé :
renderer et gel de simulation à raccorder (`R2_AUTHORING_PLAYTEST.md`).

Replay groupé historique + inspecteur acquis sur candidat `e4e75bc6…` : dix
commandes agrégées affichées, changements de seed/verrou et inverses lisibles ;
couche Arbres identifiée comme retouchée/verrouillée, recette/effective séparées
et boutons cohérents. Lecture seule, archive SHA-256 `0b3bf389…` inchangée.
Studio et descendants fermés. Analyse du playtest agrégé consignée dans
`R2_AUTHORING_PLAYTEST.md` : snapshot de tête, paire effective et barrière de
simulation à raccorder ; bouton toujours désactivé. Gate 2 reste ouverte.

Inspecteur de densité sélectionnée ajouté : recette/effective, présence d'override,
verrou, statistiques du masque et provenance sous détails. Actions UI bornées à
la couche valide et à son verrou ; validation des commandes inchangée. 1 test
d'inspection et Clippy project/Studio WASM PASS. Panneau pas encore observé dans
le candidat ; replay groupé avec l'historique encore dû. R2-049 reste partiel.

Historique agrégé raccordé au bus et au panneau Studio existants : pages de 32,
hash de tête, résumés des overrides/verrous/régénérations et inverses. Lecture sans
mutation, aucun nouveau journal persistant. Tests ciblés domaine/host et Clippy
project/Studio WASM PASS ; panneau compilé mais replay graphique encore dû,
à regrouper avec le prochain lot UI (`docs/evidence/gate2/R2_AUTHORING_HISTORY.md`).

Parcours Studio regroupé acquis sur copie isolée, candidat `975b9033…` :
conversion explicite du journal v2 → v3 (3 888 891 → 2 026 913 octets, 7 commandes),
trait de densité arbres avec aperçu au sol, Apply visible, verrou, seed 43 → 44,
Save/Close/Open avec clairière conservée. Archive finale v3 : 10 commandes,
2 802 538 octets ; 618 corrections de relief et 576 de matériaux conservées.
Source de test précédente intacte, Studio et ses 7 descendants fermés et vérifiés.
Build regroupé réussi ; 4 tests pinceau/overlay et Clippy ciblés PASS.
Gate 2 reste ouverte : qualification workspace ADR-0006, projection végétale
partielle et autres critères encore dus. Preuves : `R2_VEGETATION_AUTHORING.md`
et `R2_COMPACT_ARCHIVE.md` dans `docs/evidence/gate2/`. Aucune publication.

### Historique des étapes de ce lot (les réserves ci-dessous sont datées par étape)

Pinceau de densité Studio codé sur le picker commun : point/trait, cible 0/255,
verrou/déverrouillage/retrait du masque, Preview/Apply de la tête gardée par hash.
Clippy WASM PASS ; overlay 3D et replay clairière verrouillée/reseed encore dus.
Le binaire graphique précédent n'inclut pas ces commandes ni la conversion compacte.

Archive agrégée v3 compacte préparée en opt-in sans mutation : commandes intégrales,
digests des reçus reconstruits, checkpoint conservé, lecture v1/v2 inchangée.
8 tests ciblés et Clippy project strict PASS. Audit réel en mémoire :
3 888 891 → 2 026 913 octets, tête et 7 reçus identiques, aucun fichier écrit.
Conversion native/Studio ensuite raccordée : revue revalidée, conversion en mémoire
marquée dirty, Save atomique explicite. Test host disque v3 PASS, Clippy project
et Studio WASM PASS ; nouveau panneau pas encore lancé/capturé. Aucun fichier
utilisateur converti : `docs/evidence/gate2/R2_COMPACT_ARCHIVE.md`.

Replay végétation Studio acquis sur copie isolée, candidat 366762df... :
initialisation Preview/Apply, Undo/Redo, seed 42→43, Save/Close/Open et répartition
visible restaurée. Retouches terrain conservées. Processus d'essai fermés.
Archive 3 888 891 octets proche de la borne 4 MiB : capacité du journal à traiter.
Pas encore de geste de densité/clairière verrouillée qualifié dans le Studio,
ni de verdict performance. `docs/evidence/gate2/R2_VEGETATION_AUTHORING.md`.

Adapter moteur de changement des sources végétation ajouté : même renderer,
invalidations des préparations périmées et reprojection même à qualité constante.
1 test CPU/ECS ciblé PASS, Clippy natif/lib et WASM PASS. Canvas Studio ensuite
raccordé (paire effective la plus récente, attente du terrain, une soumission),
Clippy Studio WASM PASS. Build candidat en cours ; mise à jour par chunks et preuve
graphique encore dues. Aucun nouveau verdict visuel acquis.

Recette initiale de végétation générique ajoutée au domaine : identité et matériaux
de la zone courante, arbres/herbe en plaques, aucune exclusion de démonstration.
Commandes d'initialisation/reseed et panneau Studio ensuite codés : preview/Apply
communs, corrections verrouillées conservées. 3 tests ciblés et Clippy world/Studio
WASM strict PASS. Rendu agrégé et replay du panneau encore
à livrer ; `docs/evidence/gate2/R2_VEGETATION_AUTHORING.md`. Aucun nouveau replay
graphique, ni validation de Gate 2, ni publication.

ADR-0014/0015 acceptés explicitement pour implémentation. Premier lot matériaux
persistants codé : terrain v3 opt-in, migration/peinture réversibles, archive v2
avec conservation de la lecture v1 et refus de dépassement avant mutation ;
poids effectifs raccordés aux générations complète/partielle et surfaces.
224 tests project/world PASS ; Clippy strict domaine et Studio WASM PASS.
Pinceau domaine et contrôles/aperçu logique coloré Studio désormais codés ;
2 tests pinceau/aperçu et Clippy world/Studio WASM PASS. Test disque peinture PASS.
Parcours Studio migration, trait earth (576 cases), aperçu/application visible,
Undo/Redo et Save/Close/Open observé, hash archive/moteur concordant.
Qualification transversale et cas complémentaires encore dus :
`docs/evidence/gate2/R2_MATERIAL_PAINT.md`. Pas de verdict Gate 2 ni de push.
Moteur postérieur au candidat Studio : peinture limitée à l'attribut matériau
quand la géométrie est identique ; allocations positions/normales et colliders
conservés, rollback après corruption vérifié. 10 tests terrain_update PASS,
Clippy moteur PASS ; replay graphique de ce dernier changement encore dû.
Les mentions d'approbations en attente ci-dessous sont historiques.

Conservation des retouches : tête agrégée terrain/végétation, corrections typées,
migration avec checkpoint historique et transactions/archives désormais codées.
Hôte unique raccordé aux deux formats, migration explicite sans autosave,
preview/apply et Save/Open agrégés ; 9 tests ciblés host/catalogue/récupération
PASS, Clippy project et Studio WASM PASS. Outils terrain Studio ensuite raccordés
à la tête agrégée : migration explicite, altitude/Apply/Undo/Redo/Save/Close/Open
observés sur copie isolée (candidat 767bafe5...). Anciennes corrections conservées,
checkpoint exact et processus fermés. Trois tests tête PASS, dont préservation
inter-domaines. Le pinceau de densité domaine est codé (2 tests PASS), mais gestes
et projections de végétation agrégée restent à raccorder. Commandes densité,
verrou/déverrouillage et retour à l'héritage ensuite ajoutées ; pinceau relié à
la transaction agrégée. 3 tests pinceau/transaction PASS, dont verrou non
contournable et relecture d'archive ; Clippy project/world PASS. Aucun replay
graphique de végétation ni régénération acquis (`R2_VEGETATION_AUTHORING.md`).
Régénération de base terrain/végétation ensuite raccordée aux transactions :
corrections conservées, conflits de cibles/grille explicites, Undo/Redo et archive
v2 relisible (v1 inchangée sans régénération). 3 tests régénération et 4 de
non-régression PASS ; Clippy project PASS. Un rebasage de deltas de hauteur sur
un nouveau bruit reste refusé, pas silencieusement décalé. Panneau et projections
de régénération non livrés : `docs/evidence/gate2/R2_ZONE_REGENERATION.md`.
Playtest agrégé refusé en attendant le raccord ; pas de preuve produit générale
porte/clairière conservées. `docs/evidence/gate2/R2_ZONE_HEAD.md`.

## État courant — Gate 1 clôturée le 12 septembre 2026

**PASS automatisé local sous ADR-0006, avec les dérogations acceptées.**
Dérogations saccades pendant les changements de qualité et kit : ADR-0013 ;
comparaison Godot/Cadle validée explicitement. 738 tests PASS, 2 ignorés
préexistants ; builds et replays natif/Studio PASS. Révision propre qualifiée
`7386e00a1d9e8531470b57bcd8da190636a9e390`. [Rapport de clôture](docs/evidence/gate1/GATE1_CLOSURE_20260912.md).
CI distante NOT RUN ; aucune publication ni qualification de distribution.
Le travail Gate 2 est suivi séparément et ne fait pas partie de ce snapshot.
Gate 2 active sur demande explicite : transactions terrain, archive de session,
sauvegarde native avec conflits, reprise explicite du staging et hôte applicatif
Create/Open/Preview/Apply/Save/Close implémentés. **97 tests core/project PASS**.
Le raccord Tauri compile en natif. Premiers contrôles d'édition ajoutés au code,
constructeur de terrain neuf testé (98 tests core/project PASS), contrôle WASM
PASS avec Clippy strict. Bundle Trunk et Tauri construits ; parcours Studio
création, aperçu/renommage, sauvegarde, réouverture et Undo/Redo observé sur une
zone neuve, archive contrôlée. Projection initiale et renommage visibles ; reprise
3D après changement de session encore en correction, altitude et playtest non
qualifiés. Voir aussi `docs/evidence/gate2/R2_AUTHORING_LIVE_PROJECTION.md`.
Dernier contrôle domaine : **100 tests core/project PASS**, Clippy project strict
PASS ; conservation de la session non sauvegardée pendant Inspect testée.
Navigation WebView corrigée et rejouée : réouverture Studio à 8 m, nom conservé,
hash sauvegarde/moteur concordant et terrain rendu. Test moteur altitude/Rapier
PASS (1 ciblé). Le playtest et la qualification Gate 2 complète restent ouverts.
Playtest candidat : cycle natif lié au hash, refus des mutations pendant simulation,
Jouer/Retour raccordés dans les sources Studio. **101 tests core/project PASS** et
Clippy WASM strict PASS ; essai graphique encore requis, voir
`docs/evidence/gate2/R2_AUTHORING_PLAYTEST.md`.
Premier essai graphique : personnage chargé, retour à la vue d'édition et archive
inchangée vérifiés. Commandes du personnage non qualifiées (focus à diagnostiquer).
Panneau d'observation DOM/capture/contrôleur ajouté, Clippy PASS ; nouveau build
de diagnostic construit puis essai interrompu par les difficultés de focus ;
processus d'essai arrêté. Aucun passage de R2-041 ni de Gate 2.
Brosses locales lever/abaisser, aplatir et lisser implémentées avec contrôles
numériques Studio, preview et historique commun. 3 tests ciblés PASS, Clippy
world/project et Studio WASM PASS. Geste souris, cercle d'aperçu et replay intégré
restent ouverts : `docs/evidence/gate2/R2_TERRAIN_BRUSHES.md`.
Raccord souris et cercle de rayon maintenant codés, Clippy Studio WASM PASS :
sélection limitée au terrain, préparation de la même commande que la saisie
numérique. Replay graphique regroupé encore requis ; pas de verdict R2-B.
Bruit seedé et pente X/Z ajoutés aux brosses ; sélecteur d'outil Studio commun.
4 tests ciblés PASS, Clippy world/all-targets et Studio WASM PASS. Matériaux,
trait continu et aperçu géométrique avant application restent à réaliser.
Aperçu géométrique filaire maintenant implémenté, borné et lié au hash source,
avec abandon explicite. 5 tests brosses et Clippy PASS ; lisibilité graphique
encore à vérifier. Tracé continu maintenant implémenté : espacement métrique,
une commande et un Undo par geste, aperçu au relâchement, abandon explicite.
7 tests brosses PASS et Clippy world/Studio WASM PASS. Peinture de matériaux et
qualification graphique regroupée restent ouvertes.
Navigation de caméra auteur ajoutée : rotation, déplacement horizontal du pivot,
zoom et retour à la vue d'ensemble, sans mutation du terrain ni contrôle du joueur.
Clippy Studio WASM PASS ; gestes et présentation encore non qualifiés.
Voir `docs/evidence/gate2/R2_EDITOR_CAMERA.md`.
Premier candidat regroupé construit et ouvert : panneau latéral, zoom, aperçu
numérique puis Apply/Save/Undo/Save observés et archive contrôlée (109 samples
puis zéro). Le glisser rapide ne produit qu'une empreinte : collecte du chemin
à corriger ; tracé continu non qualifié. Application fermée et descendants
contrôlés absents. Voir `docs/evidence/gate2/R2_AUTHORING_BRUSH_WORKSPACE.md`.
Correctif du glisser rapide codé : file bornée des événements DOM et projection
ordonnée de tous les points, refus du geste sur miss/débordement. Clippy Studio
WASM avec moteur PASS ; le candidat graphique précédent ne contient pas ce
correctif, replay encore requis.
Replay du correctif effectué sur binaire 442d3065... : glisser droit rapide,
bande continue de 618 samples visible en aperçu puis appliquée/sauvegardée,
une commande edit révision 4 dans l'archive. Défaut initial du trait ponctuel
corrigé sur ce parcours. Diagnostics masqués par défaut, activables avec titre
Gate 2 vérifié. Autres gestes/outils et qualification générale encore ouverts.
Catalogue des zones sauvegardées ajouté : inventaire borné en lecture seule,
sélecteur Studio par nom/ID, ouverture via le bus existant. 1 test intégré et
Clippy project/Studio WASM PASS ; parcours graphique non vérifié.
Voir `docs/evidence/gate2/R2_SAVED_ZONE_CATALOG.md`. ADR-0015 reste proposé.
Catalogue enrichi de causes typées et d'avertissements de staging interrompu,
sans récupération automatique. Deux tests ciblés et Clippy project PASS ;
interface des causes codée mais non inspectée graphiquement.
Récupération Studio explicite ajoutée au catalogue : examen des versions,
confirmation, promotion valide ou archivage du candidat, refus des hashes périmés.
Quatre tests ciblés catalogue/récupération PASS ; Clippy natif/WASM PASS.
Replay graphique catalogue/récupération effectué sur candidat 64a59560... :
première sauvegarde récupérée après confirmation, archive de secours identique,
ouverture et terrain affiché au hash attendu. Original intact, processus fermés.
Conflits et archivage sans promotion restent testés uniquement côté natif.
Voir `docs/evidence/gate2/R2_STUDIO_RECOVERY.md` ; Gate 2 toujours ouverte.
Historique Studio raccordé au journal natif : lecture paginée (32), résumés
avant/après, Undo/Redo et commandes disponibles. Deux tests ciblés et Clippy
natif/WASM PASS ; panneau graphique encore à inspecter avec le prochain lot.
Pas de nouveau format persistant : `docs/evidence/gate2/R2_AUTHORING_HISTORY.md`.
Raccourcis auteur Ctrl+S/Z/Y/Maj+Z raccordés au bus, champs de saisie exclus,
playtest/refus de session respectés. Test de correspondance des touches et
Clippy WASM PASS ; focus/gestes à qualifier avec l'historique dans le Studio.
Voir `docs/evidence/gate2/R2_EDITOR_SHORTCUTS.md`.
Replay groupé effectué sur candidat c999bcfa... : historique visible, Ctrl+Z/Y
annule/rétablit effectivement le relief (révisions 5/6), Ctrl+S sauvegarde les
reçus ; Ctrl+Z dans le champ Nom reste local au texte. Fixture isolée, original
intact, processus fermés. Pages longues/IME/autres cas UI restent non rejoués.
Preuve : `docs/evidence/gate2/R2_AUTHORING_HISTORY.md`.
Navigateur du kit gouverné codé : recherche par ID/catégorie sans accents,
fiches provenance/hash et vérification GLB sélectionné sans GPU. Deux tests
d'index et Clippy assets/Studio WASM PASS ; panneau et fichier réel encore à
qualifier. Pas d'import ou de placement acquis : `docs/evidence/gate2/R2_ASSET_BROWSER.md`.
Essai bibliothèque effectué sur candidat 9ea00c8c... : six références visibles,
recherche « vegetation » → deux arbres, fiche du chêne et contrôle de son GLB
réussis (11 739 160 octets, hash conforme). Aucun chargement 3D ; kit source
préservé et processus fermés. Aperçu/placement restent à construire.
Aperçu individuel maintenant raccordé au même moteur/canvas auteur : un GLB
vérifié à la fois, LOD0 seulement, pas de collider ni placement persistant.
Clippy WASM et builds PASS ; candidat 5473916e... lancé sur écran secondaire :
chêne puis pin affichés, zoom et retrait/retour carte observés, archive inchangée.
Processus fermés. Cadrage fixe, cas d'erreur et libération GPU chiffrée non
qualifiés ; aucun placement/gizmo acquis. Voir R2_ASSET_BROWSER.md.
Cadrage automatique des aperçus ajouté ensuite dans les sources (bounds LOD0
transformés, aucun changement d'échelle), et erreur asynchrone annulée ignorée.
Clippy WASM PASS ; le dernier binaire essayé reste celui à cadrage fixe,
replay regroupé du nouveau cadrage encore requis. Formats ADR-0014/0015 en attente.
Replay du cadrage effectué ensuite : candidat 4e24703a..., rocher et arche
centrés automatiquement sans zoom manuel, retrait/retour carte observé,
archive inchangée, processus fermés. Builds PASS, limites de cas négatifs
maintenues dans R2_ASSET_BROWSER.md. Gate 2 et approbations structurelles ouvertes.
Preuve courante :
`docs/evidence/gate2/R2_AUTHORING_CONTROLS.md` et
`docs/evidence/gate2/R2_AUTHORING_HOST.md`, complétée par `R2_FILE_SESSION.md`
et `R2_INTERRUPTED_SAVE_RECOVERY.md` dans le même dossier. Aucun passage Gate 2.
Les états ouverts et attentes antérieurs ci-dessous sont historiques ;
ils ne doivent pas rouvrir les décisions soldées par ce rapport.


### Décisions de clôture — 12 septembre 2026

ADR-0013 consigne l'instruction explicite du propriétaire : saccades pendant les
changements de qualité dérogées ; point kit humain reporté sous dérogation ;
comparaison Godot/Cadle validée. Ces points ne sont plus en attente d'accord.
La qualification finale sur révision propre est en cours ; aucun PASS de test,
droit de distribution ou travail de Gate 2 déduit de ces décisions.
Voir `docs/ADR/ADR-0013-gate1-closure-owner-decisions.md` (depuis la racine).
Les paragraphes antérieurs ci-dessous restent l'historique des acquisitions.

Dernière mise à jour : **12 septembre 2026**
Branche : **`Rust`**
Statut : **Gate 1 clôturée localement sous dérogations ; produit complet non qualifié**

### Préparation de clôture — 12 septembre

Revue unique préparée dans `docs/evidence/gate1/R1_CLOSURE_REVIEW_20260912.md` :
340 fichiers liés aux preuves vérifiés, sources temporelles inchangées, références
Godot/Cadle inspectées sur place et six copies Blender accessibles. Le propriétaire
confirme n'avoir effectué aucune retouche humaine. Retouche/réimport, revues humaines,
jugement comparatif, réserve de stutter et révision propre restent non soldés.
Aucun nouveau benchmark, asset modifié, commit/push ou passage de Gate 2.

### Dernière décision — 11 septembre

**Correction commune du tramage autorisée par le propriétaire, non dérogée.**
Contrôleur temporel LOD ajouté dans `forge_engine` : fondu borné, hystérésis,
attente du LOD GPU prêt et poids complémentaires ; six tests CPU ciblés PASS.
Raccord aux shaders et groupes kit/forêt maintenant **implémenté**, y compris
lots lointains et ombres. Contrôle borné **PASS en natif et Studio** : le fondu
se termine caméra arrêtée ; captures inspectées. 20 tests ciblés et Clippy strict
PASS, builds natif/WASM PASS ; correction de l'ordre d'installation du kit Studio
vérifiée au lancement. Aucune clôture de Gate 1 ni absence globale de saccades
revendiquée. Voir `docs/evidence/gate1/R1_TEMPORAL_LOD.md`.
Les snapshots/binaires précédents sont conservés, pas relabellisés comme builds
de ces nouvelles sources. Essais actuels `attempt-5` natif et `attempt-7` Studio,
même code moteur ; processus possédés fermés. Ne pas répéter les acquisitions
inchangées pour remplacer les conditions humaines ou le verdict restant.

**Arrêt des acquisitions répétitives sur candidat inchangé**, après retour explicite
du propriétaire. Paire vidéo High clair/pluie acquise : 271 images FHD par vidéo,
mêmes entrées, sortie 0, six tests outil PASS ; pas une preuve de fluidité temps
réel ni une revue visuelle des MP4. Le dossier final distingue maintenant quatre
conditions résiduelles : temporalité, retouche/revue/provenance du kit, jugement
comparatif, puis verdict sur révision propre. Voir la section de décision de
`docs/evidence/gate1/R1_FINAL_DOSSIER_POINT7.md`. Aucun défaut ni critère supprimé,
aucune nouvelle dérogation inférée, Gate 1 ouverte. Ne plus remplacer ces décisions
par de nouveaux cycles/captures identiques.

**Douze vues natives du candidat actuel acquises et inspectées** : six caméras
clair/pluie, High 1080p, même runtime/kit que la parité courante, 284 sources
inchangées. Snapshot différent uniquement par les caméras de preuve. Arrêts propres
vérifiés, aucun runtime restant. Index final `index-twelve-views.json`, 447 fichiers
contrôlés, 15 tests du collecteur PASS. Voir `docs/evidence/gate1/R1_FINAL_DOSSIER_POINT7.md`.
Série fixe actuelle disponible ; vidéo appariée, réserves temporelles, comparaison
et revues humaines/révision propre restent ouvertes. Aucun rebuild ou changement de rendu.

**Candidat avec chevauchement consolidé** : collecteur final exécuté avec choix
explicite `preparation-overlap`, 408 fichiers contrôlés, 284 sources identiques
et 14 tests du collecteur PASS. Cycle mobile revérifié ; rapports et payloads de
parité quatre profils natif/Studio épinglés, sans rejouer leurs comparateurs.
Douze captures et deux vidéos historiques séparées des preuves actuelles.
Index `r1-final-dossier-overlap-20260911/index.json`, détails dans
`docs/evidence/gate1/R1_FINAL_DOSSIER_POINT7.md`. Gate 1 reste ouverte : fluidité,
dossier comparatif final, retouche/revues humaines et révision propre non acquis.
Pas de nouvelle acquisition graphique, compilation Rust ou publication dans ce lot.

**Publication progressive des meshes végétaux implémentée** : un remplacement
en attente, ancien rendu conservé
jusqu'au reçu GPU puis bascule des handles dérivés. Annulation et invalidation
des sources/politiques testées ; 714 tests PASS/deux ignorés, Clippy natif/Web et
build Studio PASS pour le candidat quatre meshes/8 Mio. Acquisition terminale :
pics Low/High 89,6/101,4 ms contre 143,5/264,5 ms auparavant, mais retour High
trop tard à 10,435 s et cycle mobile FAILED. Tous les processus des essais fermés.
Ajustement de débit seize meshes/12 Mio : quatre tests ciblés et Clippy/build PASS,
acquisition terminée, pics 97,6/103,9 ms et retour High à 9,255 s, encore trop tard
(cycle FAILED). Essais fermés, aucun processus restant ; aucun succès mobile ni
fluidité acquis. Suite prévue : réduire la latence séquentielle, sans allonger le
parcours. Ce chevauchement est maintenant implémenté dans la préparation Web :
chunks déplacés vers la file de transfert avant fin de préparation, ancienne vue
conservée jusqu'au reçu complet. 717 tests PASS/deux ignorés, Clippy natif/Web et
build PASS. Acquisition Studio : cycle High/Low/High en déplacement PASS, High
de retour à 8,418 s, pics Low/High 97,8/101,8 ms. Trois reçus et sources identiques
vérifiés, capture inspectée, huit processus fermés. Régression de délai résolue
pour ce protocole ; fluidité globale toujours distincte. Qualification native des
profils ensuite acquise sur les mêmes 284 sources : cycle High/Low/Medium/Ultra/High
PASS, captures Low/retour High inspectées ; compléments Studio Medium/Ultra acquis
et inspectés. Comparaison fonctionnelle des quatre profils PASS, zéro dérive des
sources, tous les processus fermés. Pas de parité pixel, benchmark ou revue humaine
déduits de ce résultat ; voir `docs/evidence/gate1/R1_043_CURRENT_NATIVE_STUDIO.md`.
Voir `docs/evidence/gate1/R1_040_MESH_STAGING.md`.
La nouvelle implémentation rend les anciens index historiques, pas courants.

**Dossier du candidat berges intégré sans nouvel essai graphique** : choix
explicite du Studio dans le collecteur, 377 fichiers contrôlés, douze captures
et deux vidéos historiques distinguées du nouveau diagnostic. Neuf tests PASS ;
282 sources identiques et unique dérive README épinglée. PNG/sortie normale
manquants restent signalés, aucun gain de fluidité ou passage de gate déduit.
Voir `docs/evidence/gate1/R1_FINAL_DOSSIER_POINT7.md`. Retouche humaine toujours
absente ; aucune publication ni nouvelle compilation Rust dans ce lot.

**Berges protégées : reconstructions de profil évitées dans le moteur**. La
géométrie pleine résolution sans morph est retenue si source/protection restent
identiques ; distances mises à jour, retouches et retrait de protection invalidants.
711 tests PASS/deux ignorés, Clippy natif/Web PASS. Aucun cache ni asset ajouté.
`docs/evidence/gate1/R1_040_BANK_REUSE.md`. Build Studio réussi, cycle PASS observé
dans l'interface puis JSON acquis après reprise explicite du propriétaire.
Comparaison des plans/sources PASS ; pics 147,2/271 → 143,5/264,5 ms, écart
modeste d'un essai, gain sensible non démontré. PNG manquant après déplacement
et réduction de la fenêtre ; quatorze processus fermés, sortie forcée -1.
Ne pas prolonger cette piste par une campagne identique ni qualifier le correctif
avec les anciens binaires. Voir le complément de reprise dans le même rapport.
Gate 1 ouverte ; réserve visuelle proposée mais non acceptée à ce stade.

**Comparaison Medium/Ultra natif–Studio complétée** : sélection réelle dans le
Studio épinglé, deux reçus GPU et deux PNG acquis, comparateur PASS sur sources,
sept domaines effectifs, substitutions et berges. Les quatre niveaux sont
désormais comparés fonctionnellement sur cette paire de candidats ; pas de
parité pixel ni de fluidité déduite. Huit processus fermés. Voir le complément
de `docs/evidence/gate1/R1_043_CURRENT_NATIVE_STUDIO.md`. Snapshots historiques
préservés ; README moteur et module de test ont changé depuis ces builds, pas
le rendu exécuté. Gate 1 ouverte, aucune publication ou modification d'asset.

**Piste cache des meshes végétaux identiques écartée par mesure** : sonde CPU
explicite enrichie, un test PASS et Clippy moteur strict PASS. Quatre payloads
identiques sur 132, moins de 0,14 % des sommets ; aucun cache de production ajouté.
`docs/evidence/gate1/R1_040_VERTEX_PAYLOAD_PROBE.md`. Les millions de sommets
reconstruits ne prouvent pas à eux seuls leur coût GPU. Pas de nouveau renderer,
asset ou publication ; ancien snapshot de sources conservé, module test seul
modifié. Gate 1 ouverte, ne pas répéter cette sonde sans changement pertinent.

**Natif courant rejoué et comparé au Studio** : mêmes 283 sources vérifiées,
cycle natif préchauffé High/Low/Medium/Ultra/High PASS et deux PNG inspectés.
Plans High/Low, sources, substitutions et berges concordent avec le cycle
Studio actuel ; comparaison des champs f32 bit à bit, pas tolérance arbitraire.
Deux processus fermés ; `docs/evidence/gate1/R1_043_CURRENT_NATIVE_STUDIO.md`.
Pas de nouvelle preuve optique, de fluidité, de comparaison Medium/Ultra détaillée
Studio ou de gate. Aucun changement moteur/asset ou publication dans ce lot.

**Provenance textuelle complétée dans Visual Valley** : 16 fichiers JSON/Markdown,
357 chaînes inventoriées et revue agent des labels/descriptions/prompts ; quatre
tests du collecteur PASS, sources intactes. Mentions Cadle documentaires, pas de
texte narratif upstream identifié dans ce périmètre. Voir
`docs/evidence/gate1/R1_053_PROVENANCE_CHAIN.md`. Ce n'est ni la revue des pixels,
de toutes les chaînes UI, des dérivations ou des droits ; R1-053 reste ouvert.
Aucun import, Blender, rendu ou publication effectué.

**Suivi de clôture réconcilié avec les décisions et preuves** : les entrées
R1-040/043/055/063/065 ne présentent plus les anciens échecs et transports comme
le travail courant. `docs/evidence/gate1/R1_055_GATE1_STATUS.md` donne les sept
blocs avec acquis/dérogations/restes. Point 2 et mesures GPU Studio ne sont pas
rouverts, vue intégrée/ciel acceptés conservés. Retouche humaine toujours absente,
fluidité, comparaison/provenance finales et révision propre restent distinctes.
Aucune gate fermée ni nouveau renderer, test Rust, suppression ou publication.

**Dossier consolidé sans nouvel essai graphique** : collecteur enrichi du candidat
Studio mobile épinglé et de ses 283 sources ; 378 fichiers, 12 captures et deux
vidéos revérifiés, huit tests du collecteur PASS. Index `index-reviewed.json`
dans `r1-final-dossier-motion-20260911`, détaillé dans
`docs/evidence/gate1/R1_FINAL_DOSSIER_POINT7.md`. Candidat actuel et acquisitions
historiques restent séparés ; statut partiel, aucune qualification de fluidité,
révision propre ou revue humaine déduite. Pas de renderer ou publication.

**Cycle mobile Studio enfin acquis après préparation bornée** : plusieurs petits
chunks par appel, budget souple 4 ms / huit étapes maximum ; géométrie, sources
et critères inchangés. 710 tests PASS / deux ignorés, Clippy natif/Web et builds
PASS. Même parcours : trois reçus High/Low/High vérifiés en déplacement, dernier
High GPU prêt à 8,4438 s contre 9,0811 s. Préparation 5/5 en 60,1782 s ; huit
processus fermés, PNG inspecté. Voir `docs/evidence/gate1/R1_040_PREPARATION_BUDGET.md`.
Cette preuve supersède l'échec du cycle préchauffé décrit ci-dessous, pas les
réserves de fluidité : pics 147,2/271 ms, aucune qualification FPS ou de gate.
Suite : intégrer le cycle acquis au dossier et examiner les réserves restantes,
pas rejouer son ancien échec ni modifier l'esthétique acceptée. Gate 1 ouverte.

**Génération végétale Web réutilisée entre profils** : un plan immuable indexé
par les hashes terrain/végétation, invalidation et rejet des résultats périmés
testés. 707 tests PASS / deux ignorés, Clippy natif/Web et builds PASS.
Même cycle préchauffé : préparation végétale 176–183 ms → 24–25 ms ; pics
globaux 145,5/263,7 ms, retour High GPU à 9,0811 s. Cycle mobile toujours FAILED,
aucune qualification FPS. JSON/PNG inspectés, huit processus fermés ;
`docs/evidence/gate1/R1_040_VEGETATION_PLAN_REUSE.md`. Suite ciblée : réduire la
latence par chunks avec un budget de préparation borné, sans retouche de carte
ni prolongation du parcours. Cette suite reste à implémenter. Gate 1 ouverte.

**Bornes forestières optimisées et gain mesuré** : cache par mesh limité à un
appel de préparation, retouches visibles au suivant. 17 tests ciblés, 706 tests
globaux et Clippy natif/Web PASS. Même essai Studio préchauffé : forêt inclusive
320–321 ms → 24–25 ms (~92 % de CPU en moins pour cette phase), pics restants
216,6/268,8 ms. Sources identiques, PNG inspecté, neuf processus fermés.
`docs/evidence/gate1/R1_040_FOREST_BOUNDS_REUSE.md`. Cycle mobile toujours FAILED,
High prêt à 9,151 s après préparation ; aucune qualification FPS ou de gate.
Prochaine cible : préparation végétale ~176–183 ms et latence par chunks,
pas nouvelle retouche de carte ni recalcul des bornes déjà corrigé.

**Cycle Studio préchauffé acquis, toujours refusé en mouvement** : préparation
Ready 5/5 en 67,6749 s avant départ ; traversée 518/518 ticks au sol. High/Low
acquittés en marche, High retour prêt à 10,0595 s après arrêt du parcours.
Plus de pic multiseconde dans cette fenêtre, mais 435,6/562,1 ms persistants,
dont forêt inclusive ~320 ms et préparation végétale ~190 ms. 704 tests PASS,
Clippy natif/Web et builds PASS, JSON/PNG inspectés, huit processus fermés.
`docs/evidence/gate1/R1_040_STUDIO_PREWARMED_MOTION.md`. Prochaine correction
ciblée : éviter les recalculs de bornes identiques dans un appel du projecteur
forestier, avec invalidation entre appels et preuve avant/après. Non implémentée
par cette acquisition. R1-040 et Gate 1 ouverts, aucun seuil assoupli.

**Trace CPU Studio acquise, cause partiellement isolée** : 704 tests PASS,
deux sondes ignorées, Clippy natif/Web et builds PASS. Instrumentation facultative
par phase : commit végétal 7,7–12,5 ms, forêt 333,7–341,1 ms lors des transitions ;
pauses Low de 5,9–7,8 s hors de ces phases. Forêt initiale 2,60 s dont lots
lointains 2,28 s. Cycle mobile encore FAILED, High prêt à l'arrêt seulement.
JSON/PNG inspectés, huit processus fermés ;
`docs/evidence/gate1/R1_040_STUDIO_PHASE_TRACE.md`. Ne pas optimiser encore le
commit végétal sur une attribution erronée. Séparer désormais premier cycle à
froid et cycle après le préchauffage existant ; aucune fluidité ni gate validée.

**Studio courant rejoué : traversée acquise, cycle en mouvement refusé**.
Nouveau bundle intégrant le contrôleur corrigé, 279 sources revérifiées.
High puis Low validés en déplacement ; retour High prêt après la traversée,
mais troisième étape mobile absente, watchdog FAILED. Relevé global avec pic
7,35 s non attribuable à une phase précise. JSON/PNG acquis et inspectés,
huit processus fermés. Voir `docs/evidence/gate1/R1_040_STUDIO_MOTION_20260911.md`.
R1-040 reste ouvert ; prochaine investigation : durées des phases Web, pas
répétition du même parcours ni retouche artistique. Les mentions plus anciennes
de Studio non rejoué sur le contrôleur sont supersédées pour cette traversée seule.

**Comparaison forestière complétée descriptivement** : nouvelle acquisition Cadle
avec FOV vertical contrôlé à 60° et capture intérieure inspectée face à la capture
Forge vivante. Deux fenêtres ~109 FPS moyens, p95 41,8/39 ms ; Forge ~17,81 FPS.
Budgets, géométrie et première/troisième personne différents : aucun ratio de
performance ni équivalence artistique qualifié. Huit processus fermés, upstream
intact ; `docs/evidence/gate1/R1_052_FOREST_OPTICAL_COMPARISON.md`.
R1-052 reste partiel pour l'équivalence des conditions et le jugement final.
Les bilans ci-dessous sont historiques, notamment les mentions de paire absente
et de dérive restant à corriger. Aucun nouveau chantier esthétique ni dérogation.

**Dérive du personnage au repos corrigée et revérifiée** : test de pente à 15°
rouge avant correction (4,8 cm/30 s), quatre pentes puis 17 tests contrôleur PASS,
701 tests globaux PASS / un ignoré. Rejeu natif forestier : 300/300 échantillons
vivants sans franchir 2 cm, capture séparée inspectée, processus fermés. Aucun
asset/carte modifié. Mesure ~17,81 FPS, p95 57,55 ms : lenteur toujours présente,
aucun gain FPS revendiqué. Voir `docs/evidence/gate1/R1_IDLE_SLOPE_STABILITY.md`.
R1-052 reste ouvert pour le pairage optique et le verdict, pas pour cette dérive
reproduite et corrigée. Studio graphique non rejoué sur ce nouveau contrôleur.

**R1-052 Forge : mode de mesure immobile vivant débloqué, essai refusé honnêtement**.
Garde optique ajusté sans autoriser temps figé/capture pendant mesure ; cinq tests
ciblés, Clippy et build natif PASS. Essai réel interrompu à 167/300 échantillons :
dérive physique >2 cm au repos sur pente. Seuil conservé. Capture séparée Full HD
acquise en intérieur forestier, sortie 0, processus fermés ; overlay ~17,6 FPS,
non benchmark. Voir `docs/evidence/gate1/R1_052_FORGE_LIVE_COST_20260911.md`.
Prochaine correction : reproduire la dérive dans le contrôleur générique, pas
retoucher la carte ni figer la physique. Comparaison et Gate 1 restent ouvertes.

**R1-052 : nouvelle mesure Cadle acquise**, deux fenêtres forêt intérieure
Full HD High, RX 7800 XT, sans capture pendant les fenêtres. Moyennes ~100 FPS
mais p95 43,5/50 ms : pas preuve de fluidité ni comparaison Forge réalisée.
Résultats bruts et capture restent dans le clone ignoré, état upstream vérifié
avant/après, sept processus possédés fermés. Voir
`docs/evidence/gate1/R1_052_CADLE_COST_20260911.md`. La paire Forge intérieure
reste à acquérir ; ne pas réutiliser une ancienne mesure figée comme équivalente.

**Dossier final : collecteur corrigé et exécuté**, six tests PASS, 89 fichiers,
12 vues/deux vidéos historiques vérifiés. Il ne confond plus le préchauffage
hors de son périmètre avec un préchauffage incomplet. Rapports Cadle : aucune
série de coûts enregistrée ; comparaison appariée toujours due. La vue forêt
Forge retenue est extérieure, celle de Cadle intérieure : cadrage à apparier
avant conclusion comparative, pas nouveau chantier esthétique implicite.
Voir `docs/evidence/gate1/R1_FINAL_DOSSIER_POINT7.md`. Gate 1 reste ouverte.

**R1-040, terrain Studio : préparation par chunks implémentée et smoke acquis**.
Après la végétation, le terrain Web prépare un chunk par frame, conserve les
meshes précédents jusqu'au résultat complet et abandonne les demandes périmées.
700 tests PASS / un timing ignoré, Clippy natif/Web et builds Studio PASS.
Cycle réel Ready 5/5 en 41,4071 s, High restauré, sept domaines et reçu GPU
cohérents, sources inchangées. JSON/PNG relus, huit processus de test fermés.
Voir `docs/evidence/gate1/R1_TERRAIN_STAGED_PREPARATION.md`. Application finale
des meshes non incrémentale : R1-040 et Gate 1 restent ouverts. Aucune retouche
artistique, validation humaine inventée ou publication. Les bilans suivants
sont historiques.

**R1-040, végétation Studio : préparation par chunks implémentée**, projection
précédente conservée jusqu'au résultat complet et abandon d'une demande remplacée.
697 tests PASS / un timing ignoré, dont parité exacte des meshes avec le calcul
synchrone. Frontend release de contrôle et bundle Studio construits ; smoke réel
finalement acquis à 21:49 : Ready 5/5 en 34,3811 s, High restauré, sept domaines
appliqués et acquittement GPU cohérent. JSON/PNG exportés et relus, sources
identiques à la baseline, huit processus de test fermés. Ce smoke remplace les
deux essais interrompus, pas la qualification temporelle. Voir
`docs/evidence/gate1/R1_QUALITY_TRANSITION_CPU.md` ;
le terrain Web et le commit final ne sont pas rendus incrémentaux par ce lot.
R1-040 et Gate 1 restent ouverts, sans nouveau chantier artistique ni publication.

**R1-039 préchauffage des profils soldé localement en natif et Studio** : service
commun Low/Medium/High/Ultra puis restauration du profil initial, sans modification
des sources auteurs. 696 tests PASS / un timing ignoré, Clippy natif/Web PASS,
build natif PASS. Trois acquisitions natives bornées réussies, dont deux cycles
clair/pluie après préparation. Bouton Studio utilisé : Ready 5/5 en clair/pluie,
exports JSON/PNG acquis, sources préservées, tous les processus de test terminés.
Voir `docs/evidence/gate1/R1_PROFILE_PREWARM.md`. Préchauffage des variantes connues
acquis ; aucune garantie de fluidité R1-040, nouvelle dérogation ou publication.
Les bilans suivants décrivent les étapes antérieures.

**Capacités de mesure R1-034/037/038 soldées pour le candidat local** : nouvel
overlay CPU/GPU natif capturé/inspecté et exporté, mémoire allocateur courante
mesurée (1403996632 octets alloués, 1838633268 réservés), capture/replay intégré
natif revérifié ; mesures GPU Studio dérogées sous ADR-0012. R1-039, réserves
temporelles, revues humaines et verdict final restent ouverts. Preuves et limites :
`docs/evidence/gate1/R1_MEASURES_POINT5.md`. Aucun rendu modifié, build ou push ;
processus natif de preuve terminé. Les bilans suivants sont historiques.

**Décision propriétaire : dérogation limitée aux mesures GPU du Studio acceptée**
par `docs/ADR/ADR-0012-gate1-studio-gpu-measurements-waiver.md` : sous-parties WebGPU
de R1-034/037/038 `WAIVED / ACCEPTED`, pas PASS technique. Mesures natives, CPU,
préchauffage, qualification temporelle et revues humaines non dérogés. Gate 1
reste ouverte ; aucun fork, changement du rendu ou push. Les bilans antérieurs
ci-dessous décrivent les preuves avant cette décision.

Dernier complément : overlay Studio effectivement affiché, CPU observé mais GPU
indisponible (chemin timestamps Bevy non compatible WebGPU identifié). Instance
terminée ; pas de nouvel export PNG obtenu. Dossier consolidé vérifié : 89 fichiers,
12 vues et 2 vidéos, incluant désormais capture/replay natif et copies Blender.
Trois tests du vérificateur PASS ; Gate 1 reste ouverte, aucun fork ni push.

Complément de clôture : capture GPU native corrigée pour conserver eau et nuages,
38 tests outillage PASS, acquisition/replay intégré PASS (1002 draws, 6424 instances,
9325611 triangles soumis sur une frame, pas FPS). Six GLB réouverts dans Blender 5.2.1,
copies `.blend` round-trip identiques prêtes pour retouche humaine. Référence Godot
historique retrouvée/inspectée ; comparaison complète toujours non acquise.
Ciel : améliorations préparées seulement, rendu accepté inchangé. Tous les processus
du lot terminés. `docs/evidence/gate1/R1_CLOSURE_PROGRESS_20260910.md` détaille résultats
et blocages ; **Gate 1 ouverte**, aucune dérogation nouvelle, aucun push.

Points 5 à 7 engagés : audit des six GLB (0 erreur, 38 avertissements),
14 captures bornées dont 12 retenues et inspectées, deux vidéos existantes
revérifiées, index de 62 fichiers hashés. Caméra de détail eau corrigée sans
changer la carte ; paire initiale masquée écartée, preuves conservées.
**Trois blocs PARTIAL, Gate 1 ouverte** : instrumentation exhaustive et
préchauffage complet, qualification humaine du kit, comparaison Godot et verdict
final sur révision propre restent dus. Comparaison Cadle descriptive, sans copie,
ne démontre pas l'équivalence. Voir `docs/evidence/gate1/R1_FINAL_DOSSIER_POINT7.md`,
`R1_MEASURES_POINT5.md`, `R1_KIT_POINT6.md`. Essais fermés ; aucun push.

Point 4 natif/Studio : parité fonctionnelle du candidat acquise. Packaging des
nuages existants raccordé à l'eau ; quatre profils et retour High comparés
exactement (sources, sept domaines, fallbacks), reçus GPU prêts dans les deux
environnements, même kit épinglé. Captures inspectées, processus fermés.
Studio acquis en deux sessions ; pas de comparaison pixel à pixel, de nouvelle
qualification temporelle ou de benchmark. Voir `docs/evidence/gate1/R1_NATIVE_STUDIO_POINT4.md`.
Gate 1 reste ouverte ; points 5 à 7 et réserves du point 3 conservés.

Point 3 mouvement : corrections et intégration fonctionnelle terminées ;
qualification temporelle complète PARTIAL. Watchdog Studio corrigé et parcours complet PASS (518 ticks
au sol, 434 contacts, 42,528 m), sans augmenter les délais existants. Préchauffage
WebGPU séparé du chronomètre de déplacement. Raccord d'ombres LOD0/LOD1 corrigé :
le fort blanchiment à moyenne distance disparaît des vues natives inspectées.
Normales des atlas corrigées dans des dérivés séparés ; sources et carte intactes.
Vent lointain isolé A/B/A PASS borné, R1-014 acquis ; cycles GPU en marche prouvés
en natif et Studio. Qualification temporelle complète encore réservée : tramage
résiduel des troncs, pas de preuve d'imperceptibilité ni d'absence de hitch.
Voir `docs/evidence/gate1/R1_MOTION_POINT3.md` pour les candidats exacts, contrôles
et limites. Candidat final Studio et quatre parcours natifs de profils affectés
exécutés/capturés/inspectés ; processus fermés. Ne pas répéter la matrice inchangée
ni déduire Gate 1 des reçus GPU.

Point 2 performances accepté par dérogation explicite du propriétaire sur sa
RX 7800 XT : clôture de suivi WAIVED/ACCEPTED, pas réussite mesurée des seuils.
Benchmark RTX 3060/RX 6600 toujours NOT RUN ; échecs historiques conservés,
candidat combiné non remesuré. Pas de nouvelle campagne requise pour clôturer
ce point. Gate 1 et les autres critères restent ouverts ; aucun push.
Portée et demande exacte : `docs/evidence/gate1/R1_PERFORMANCE_POINT2.md`.

### État vérifié antérieur — 9 septembre

Point 2 performances demandé ensuite : inventaire matériel vérifié, RX 7800 XT
et GPU intégré uniquement ; benchmark RTX 3060/RX 6600 NOT RUN. Rapport release
historique rehashé et recalculé : 72,47 FPS global mais 51,72 FPS forêt, p95 forêt
22,28 ms, 1 % low 39,90 FPS, seuils non satisfaits. Pas une mesure du dernier
candidat combiné. Point 2/R1-041 ouverts ; aucun nouveau lancement de test.
Voir `docs/evidence/gate1/R1_PERFORMANCE_POINT2.md`. Matériel cible nécessaire
pour qualifier ; aucune substitution de matériel ni dérogation supposée.

Bloc fonctionnel des profils terminé : forêt en batches et eau forward raccordées,
High/Low/Medium/Ultra/High acquittés par le GPU en natif et Studio, remontée
automatique native, descente Studio et choix manuel prioritaire vérifiés.
Sources et paramètres High préservés ; processus de test fermés. 684 tests
workspace PASS / un timing ignoré, puis 55 tests Studio PASS et Clippy strict final
PASS. Voir `R1_EFFECTIVE_PROFILES_INTEGRATION.md` pour preuves et limites.
R1-040 conserve la réserve de fluidité des transitions ; les blocs 3 à 7 restent
dus, le point 2 étant désormais dérogé. Aucun passage de gate. Ne pas reprendre le ciel accepté.

Le propriétaire accepte explicitement la vue intégrée en l'état, avec réserve
sur le ciel (« je valide en l'état »). L'attente de revue de ce rendu est levée ;
sa finition ne constitue plus un préalable implicite. Ce n'est pas un passage
de Gate 1 ni une dérogation aux performances/profils et autres preuves restantes.
Référence exacte et portée : `R1_055_GATE1_STATUS.md`, acceptation visuelle.

Historique précédent, complété par le raccord fonctionnel ci-dessus :
observation CPU des profils d'eau forward liée au profil puis au plan
installé, prérequis GPU de transmission distingués du SSR classique. Le fallback
sans plafond de distance reste refusé puisqu'il coupe le tracé forward ; aucune
capacité promue. 680 tests workspace PASS / un ignoré, Clippy strict et check Web
PASS. Pas de nouveau lancement graphique ni qualification optique. Rapport et
hashes : `R1_055_GATE1_STATUS.md`, installation forward et prérequis GPU.

Essai de bois LOD0 simplifié terminé et non retenu : conifère 14836→11127 triangles
de bois, feuillage/poses conservés, réimport et paire visuelle inspectés. ABBA même
release : ~51,8→52,2 FPS forêt, aucun gain substantiel ni seuil atteint. Kit courant
intact, code expérimental retiré et archivé sur E:, processus fermés. Ne pas répéter
cette réduction. Voir `R1_FOREST_FOUR_TIERS.md`, simplification du bois proche.

Release courant reconstruit (7 min 45 s), un trajet vivant Full HD mesuré puis
fermé : 72,47 FPS global / 51,72 FPS dans le dernier tiers forestier ; forêt
p95 22,28 ms et 1 % low 39,90 FPS. Seuils non satisfaits, pas de gain comparatif
revendiqué. Ce parcours utilise l'eau optique et les ombres candidates, sans
nuages ; il ne qualifie pas le candidat combiné ni le GPU cible. Rapport, hashes
et limites dans `R1_RELEASE_PREPARATION.md`, mesure après portée lointaine.

Candidat combiné accessible depuis le menu du Studio : kit gouverné, forêt,
eau à reflets et nuages expérimentaux, avec rechargement explicite sans sauvegarde.
55 tests Studio, Clippy natif/Web et builds Trunk/Tauri PASS. Clic réel et exports
JSON/PNG vérifiés dans `r1-integrated-menu-studio-20260909` : 1662 arbres,
36 colliders, un dôme, 101 pipelines prêts ; instance fermée et absence vérifiée.
Le ciel reste flou/artificiel et les profils incomplets ; ce raccord n'est pas
une qualification artistique, temporelle ou de performance. Détails dans
`R1_CLOUD_LAYER_PREVIEW.md`, candidat intégré. Gate 1 reste ouverte.

Candidat de portée lointaine : High 384→640 m, Ultra 512→768 m dans le profil,
sans densification ni modification des sources terrain/végétation ou des assets.
16 tests profil et build natif PASS ; capture globale native inspectée, forêt
de rive droite présente jusqu'au fond. Deux smokes courts : 66,87→61,57 FPS,
p95 16,51→17,94 ms, 1 % low 51,44→43,29 FPS. Coût explicite, pas qualification.
Intégration High ensuite vérifiée dans `r1-forest-distance-studio-20260909` :
Trunk/Tauri PASS, PNG/JSON réels inspectés, forêt lointaine présente, mêmes sources
de scène et kit, 87 pipelines prêts. PID 28748 fermé et absence vérifiée.
Ultra visuel et qualification finale restent dus.
Voir `R1_FOREST_REGION_SCALE.md`, portée lointaine. Gate 1 ouverte.
Contrôles du candidat terminés : 678 tests workspace PASS / un timing ignoré,
Clippy strict, check frontend WASM, format et diff-check PASS.

Candidat terrain sans grandes plaques chromatiques : mélange de litière réservé
au détail fin, variation large de luminosité bornée, aucune modification des sources
de carte ou collisions. 10 tests ciblés/build natif PASS ; deux captures natives
Full HD inspectées (globale et proche), motif camouflage retiré et grain conservé.
678 tests workspace PASS / un timing ignoré, Clippy strict et check frontend WASM PASS.
Intégré ensuite au paquet `r1-ground-chroma-studio-20260909` : Trunk/Tauri PASS,
PNG/JSON réels inspectés, mêmes six sources et kit, 87 pipelines prêts, 36 colliders.
Vue globale sans plaques confirmée dans le Studio ; PID 32836 fermé et absence vérifiée.
Forêt lointaine et limite du terrain non corrigées, Gate 1 ouverte. Détails dans
`R1_TERRAIN_SURFACE_DETAIL.md`, complément du 9 septembre.

Réapplication d'un profil global corrigée : une commande explicite du même niveau
n'est plus ignorée après un réglage individuel. Envoi unique, nouvelle application
sans ancien acquittement GPU ; sources et commutateurs optiques conservés.
Régression reproduite avant correction ; 39 tests coordinateur puis 678 tests
workspace PASS / un timing ignoré, Clippy strict et check frontend WASM PASS.
Intégré ensuite dans `r1-profile-reapply-studio-20260909` : Trunk/paquet PASS,
cycle Tauri High→Low→High et resélection High, deux JSON et un PNG inspectés.
Sources conservées, kit 20/20 et 36 colliders prêts, budgets eau restaurés,
instance fermée. Profils complets et Gate 1 toujours ouverts ; cas individuel
prouvé par test CPU, pas ce cycle UI. Détails `R1_055_GATE1_STATUS.md`, réapplication.
Essai de vent interrompu sans verdict nouveau, PID 5000 fermé et absence vérifiée.

Intégration du code courant vérifiée : 676 tests workspace PASS / un timing
ignoré, Clippy workspace strict tous targets/features PASS, format/diff PASS.
Helpers du recorder bornés à leur feature `qualification`, aucun comportement
graphique changé. Sessions terminées, pas de processus de build restant.
Journaux dans `r1-integration-check-20260909`, détail `R1_055_GATE1_STATUS.md`.
Pas de qualification visuelle, de performance cible ou de Gate 1 déduite.

Réfraction pilotable indépendamment dans le Studio : commande réversible sans
rayon/retour profil, testée sur les tiers et fallback effectif. 17 tests moteur,
10 tests Studio ciblés PASS ; Clippy WASM, Trunk et paquet PASS. Cycle produit
24→0→24 observé, reflets 48/96 constants, sources identiques ; instance fermée.
Profils complets et qualité visuelle restent non qualifiés. Preuve et empreintes
dans `R1_WATER_FORWARD_REFLECTION.md`, commande indépendante de réfraction.

Essai rapproché Tauri terminé : exports à temps fixe 0/0,25 s conservés dans
`r1-refracted-water-close-studio-20260909`, fond et reflets visibles dans le PNG
final inspecté ; sources et budgets identiques. Premier état avec zéro collider,
second avec 36 : paire non qualifiante pour une comparaison temporelle isolée.
PID 16980 fermé et absence vérifiée. Ne pas relancer cette paire inchangée.
Voir complément rapproché dans `R1_WATER_FORWARD_REFLECTION.md`.

Parcours pont avec eau optique désormais enregistrable : garde-fous CLI adaptés,
96 tests runtime et quatre tests Python PASS, 271 images capturées et vidéo
vérifiée. Eau trop latérale dans ce cadrage pour qualifier sa stabilité rapprochée ;
ne pas répéter ce parcours pour ce critère. Essai terminé, preuve dans
`R1_WATER_FORWARD_REFLECTION.md`, parcours intégré avec milieu optique.

Coût de la réfraction lissée mesuré en ABBA dev : ~0,85 % de FPS trajet,
~1,65 % forêt, p95 forêt +0,31 ms ; entrées et parcours comparables,
quatre processus fermés. Pas de qualification release/GPU cible ni 60 FPS
forêt acquis. Preuve `R1_WATER_FORWARD_REFLECTION.md`, coût du candidat corrigé.

Le candidat de réfraction corrigé est aussi construit/lancé dans Tauri :
99 pipelines prêts sans échec final, budget 24 pas/3,2 m exporté et concordant,
PNG/JSON conservés ; instance fermée. Vue de départ, pas qualification optique
rapprochée ou temporelle Web. `R1_WATER_FORWARD_REFLECTION.md`, intégration Studio.

Réfraction rapprochée : premier candidat refusé (frontières nettes hit/repli),
puis transition par confiance corrigée et capturée. Grandes frontières atténuées
sur la vue fixe ; trois essais terminés, sources conservées. Candidat courant
`r1-refracted-ray-smooth-build-20260909`, preuve dans
`docs/evidence/gate1/R1_WATER_FORWARD_REFLECTION.md`. Mouvement/coût/Studio dus.

Réfraction bornée raccordée au candidat forward d'eau : budgets High/Ultra,
retour à l'approximation sur miss, sources conservées. 41 tests eau et build
natif PASS ; capture réelle inspectée, shader chargé sans erreur runtime,
processus fermé. Vue éloignée : pas de qualification optique rapprochée ni de
performance ou Studio. Voir `docs/evidence/gate1/R1_WATER_FORWARD_REFLECTION.md`.

État de reprise : le saut d'ombre 161/162 a été corrigé dans le candidat B et
son coût mesuré ; ne plus reprendre l'identification RenderDoc comme préalable.
Le raccord de berge et les reflets ont aussi une preuve rapprochée Studio bornée.
La finition artistique, les profils complets et la qualification finale restent
ouverts, pas ces diagnostics déjà traités. Le suivi de sortie actualisé figure
dans `docs/evidence/gate1/R1_055_GATE1_STATUS.md` (9 septembre).
Les paragraphes datés suivants conservent la chronologie : une limite ancienne
levée par une preuve ultérieure n'est pas une instruction de relancer l'essai.

Correction de diagnostic LOD : règle gouvernée commune à la projection,
au fondu et au recorder ; trace 24–32 m au lieu de l'ancienne règle 64–72 m,
mode explicite dans le journal. Tests recorder 5 PASS et gouvernés 18 PASS
(un commun), aucune modification graphique ni relance. Voir règle commune
dans `R1_FOLIAGE_CLUSTER_ATLAS.md`. Anciens journaux conservés tels quels.

R1-014 actualisé sans relance : deux PNG du dernier Studio montrent une
variation proche localisée dans la végétation, cadrage stable. WebGPU lancé,
contrairement à l'ancienne mention roadmap ; vent lointain toujours non
qualifié, ne pas fermer l'item sur cette seule paire. Preuve dans
`R1_FOLIAGE_CLUSTER_ATLAS.md`, paire Studio réutilisée.

Coût du fondu mesuré en ABBA dev : forêt ~51,6 → 50,4 FPS (-2,3 %),
p95 ~22,0 → 22,7 ms, 1 % low dégradé ; parcours/entrées concordants,
quatre processus fermés. Pas un gain ni une qualification release/GPU cible.
Conserver cette preuve sans répéter la série inchangée. Voir coût natif dans
`R1_FOLIAGE_CLUSTER_ATLAS.md`. Gate 1 et son seuil forêt restent ouverts.

Correctif de fondu aussi intégré au paquet `r1-shadow-studio-candidate-20260909` :
WASM/Tauri reconstruits, scène réelle affichée, PNG/JSON exportés, 99 pipelines
prêts sans échec final, 1662 arbres ; processus fermé. Cette vue de départ
vérifie le chargement, pas la transition en mouvement ni la performance Studio.
Voir `R1_FOLIAGE_CLUSTER_ATLAS.md`, intégration Studio. Gate 1 ouverte.

Fondu d'ombre gouverné implémenté selon la distance caméra : replay natif
`r1-shadow-camera-fade-motion-20260909-b`, 271 images, arrêt propre. Saut
161/162 absent à l'inspection, ombre atténuée à 180. Candidat conservé ;
coût, profils, Studio et stabilité globale non qualifiés. Voir
`R1_FOLIAGE_CLUSTER_ATLAS.md`, fondu caméra. Gate 1 reste ouverte.

Diagnostic du saut 161/162 précisé : une seule racine de forêt franchit 32 m
dans la trace (32,065 → 31,901 m), à la coupure des casters LOD0. Bevy exclut
les casters hors plage avant dessin. Candidat spatial identifié, identité GPU
non prouvée ; traiter la transition des ombres de forêt à cette limite, pas
répéter le warmup. Voir corrélation dans `R1_FOLIAGE_CLUSTER_ATLAS.md`.

Les primitives GLTF du kit reçoivent désormais leur `VisibilityRange`, non
héritée des parents dans Bevy 0.19.1. Huit tests d'adaptateur PASS puis build
natif et traversée de 271 images terminés, processus fermés. Le saut 161/162
persiste : correction des plages conservée, pas une solution à ce défaut.
Voir `R1_FOLIAGE_CLUSTER_ATLAS.md`, projection des distances GLTF. Ne pas
rejouer ce candidat inchangé ; Gate 1 ouverte.

Deuxième essai de fondu (passes opaques incluses) exécuté puis retiré : saut
161/162 toujours visible, pas de correctif d'ombre retenu. Tests/build et replay
techniques PASS, processus fermés. Identifier désormais le draw/caster GPU
responsable avant une autre modification ; ne pas répéter ces deux variantes.
`R1_FOLIAGE_CLUSTER_ATLAS.md`, variante opaque. Gate 1 reste ouverte.

Essai de fondu d'ombre alpha-testé rejeté puis retiré : build/test et replay
PASS techniquement, mais saut 161/162 toujours visible. Aucun correctif d'ombre
retenu, processus fermés. Ne pas répéter ce candidat. Le journal établit la transition proche gouvernée à
24–32 m : l'ancien audit 64–72 m ne la qualifie pas. Voir diagnostic dans
`R1_FOLIAGE_CLUSTER_ATLAS.md`. Gate 1 reste ouverte.

Replay du garde vidéo terminé : scène visible dès frame 0 après 107 pipelines
prêts, traversée 271 images PASS, processus fermés. Départ noir corrigé sur cet
essai ; saut d'ombre encore observé entre frames 161/162. Code forestier : seuls
les LOD0 projettent des ombres, piste suivante à traiter sans allonger le warmup.
`R1_FOLIAGE_CLUSTER_ATLAS.md`, replay du correctif. Gate 1 reste ouverte.

Revue de la vidéo native : démarrage noir/ciel seul et saut d'ombre sur le pont
observés. Garde du recorder corrigé pour attendre des pipelines connus stables ;
quatre tests Rust et trois tests Python PASS. Correctif pas encore essayé dans
un nouvel exécutable graphique, cause du saut d'ombre non établie. Voir
`R1_FOLIAGE_CLUSTER_ATLAS.md`, revue temporelle. Gate 1 reste ouverte.

Séquence native du kit recadré enregistrée : 271 images Full HD, traversée du
pont réussie, sources conservées, audit des bandes LOD et trois frames inspectées.
Vidéo hors temps réel disponible, pas de FPS ni d'absence de popping déduits.
`R1_FOLIAGE_CLUSTER_ATLAS.md`, séquence native ; processus terminés. Exploiter
cette vidéo avant toute répétition du parcours. Gate 1 reste ouverte.

Eau rapprochée du candidat combiné observée dans le Studio : paire PNG avec
reflets écran puis environnement seul, arbres reflétés puis absents, raccord
de berge sans bande séparée dans ce cadrage. Sources/caméra constantes, essais
fermés. `R1_FOLIAGE_CLUSTER_ATLAS.md` : preuve visuelle bornée, pas de benchmark,
stabilité en mouvement ou acceptation artistique ; Gate 1 toujours ouverte.

Candidat combiné `r1-studio-atlas-reflections-20260909` construit et lancé :
atlas recadré et reflets transmissifs réunis, PNG/JSON exportés, 1662 arbres,
48 pas / 96 m observés, essais fermés. Remplace les candidats séparés comme base
de qualification, sans écraser leurs fichiers. Voir `R1_FOLIAGE_CLUSTER_ATLAS.md`.
Ni performance Studio, ni vue rapprochée de l'eau, ni transitions qualifiées.

Kit atlas recadré maintenant chargé dans un paquet Studio séparé : capture PNG
WebGPU exportée et inspectée (1408×966), 1662 arbres, 20/20 instances et 36
colliders. Essai fermé, preuves dans `R1_FOLIAGE_CLUSTER_ATLAS.md`. Affichage
constaté, pas performance Studio ni qualité artistique validées ; profil High
toujours `unresolved_capabilities`. Aucun critère de Gate 1 fermé par ce smoke.

Candidat LOD1 atlas **recadré** : ABBA natif ~53 FPS forêt contre ~44,8 original
(+18 % répété), ~74 FPS trajet ; 1662 arbres et textures conservés. Six tests CPU,
validation GLB et capture native terminés ; processus fermés. Preuves dans
`R1_FOLIAGE_CLUSTER_ATLAS.md`. Kit séparé, pas de promotion : 60 FPS forêt,
transitions/mips, Studio, GPU cible et acceptation humaine restent non qualifiés.
Les paragraphes suivants retracent les essais antérieurs, dont le non-recadré rejeté.

Premier LOD atlas intégré dans un kit séparé et essayé dans le moteur : 1662 arbres,
capture native inspectée, puis ABBA release. Forêt ~44,8 FPS ancien contre ~40,5
candidat : régression répétée, **candidat non retenu**, kit courant inchangé.
1285 ticks communs, écart max 7,53 mm, eau/ombres constantes, essais fermés.
Conversion PBR flottante corrigée et testée avant la mesure. Voir
`R1_FOLIAGE_CLUSTER_ATLAS.md` ; examiner surface des cartes/filtrage avant une
autre variante, ne pas rejouer celle-ci inchangée. Gate 1 reste ouverte.

Deux candidats GLB de feuillage LOD1 exportés et réouverts : 7462→1626 triangles
chêne, 5992→780 conifère ; autres LOD/bois/collisions préservés à l'identique.
Quatre tests CPU et validation glTF PASS (warnings source conservés), quatre
rendus DCC inspectés, essais fermés. `R1_FOLIAGE_CLUSTER_ATLAS.md` : cartes visibles,
fidélité PBR/mips/transitions non qualifiées ; aucun kit/runtime remplacé ni gain
FPS acquis. Le contrôle numérique du canal R ORM a échoué et reste documenté.

Atlas complets couleur/alpha et normales maintenant produits pour les deux arbres :
271/130 groupes, 813/390 vues par carte 2048², deux rendus CPU par arbre (~14/~12 s).
Débruitage/adaptation désactivés après défaut numérique des normales du premier
essai ; sorties v2 contrôlées, sources/scripts/PNG concordants par SHA-256.
Sept tests CPU de partition/layout PASS ; cinq fixtures de normales PASS. Quatre
PNG d'atlas inspectés, sources intactes et deux processus fermés. Voir
`R1_FOLIAGE_CLUSTER_ATLAS.md`. Pas encore de cartes 3D/GLB ou intégration runtime :
terminer marges/mips et PBR, puis exporter le LOD dérivé et comparer dans le jeu.
Aucun gain FPS, changement du kit actuel ou fermeture de gate déduit.

Première brique d'atlas feuillus implémentée : partition déterministe conservant
les feuilles entières, quatre tests CPU PASS. Deux études réelles Blender 5.2.1,
un groupe chêne/conifère, six vues albedo/alpha inspectées, sources intactes,
processus fermés. `R1_FOLIAGE_CLUSTER_ATLAS.md`. Pas encore d'atlas complet,
normales, export LOD ou intégration runtime ; aucun gain FPS. Borner les budgets
d'atlas avant génération complète, sans changement du kit actuel.

Inventaire GLB courant : LOD1 encore 7462 triangles de feuilles par chêne et 5992
par conifère ; motifs de feuilles agrandis, pas de groupes bakés. Piste suivante :
étude isolée d'atlas de groupes feuillus pour les LOD intermédiaires, sources,
LOD0/bois/collisions/placements conservés. **Prévu, non implémenté**, aucun gain
annoncé ; `R1_FOREST_FOUR_TIERS.md`, géométrie embarquée. Inspection sans build.

Rejet vertex des triangles HLOD déjà masqués essayé puis retiré : ABBA dev
56,28 / 56,94 / 56,67 / 56,83 FPS, forêt ~41,53 pour candidats et témoin retour,
gain non démontré. Deux captures inspectées, six essais fermés, sources moteur
restaurées aux hashes antérieurs ; `R1_FOREST_FOUR_TIERS.md`, section essai retiré.
Ne pas rejouer cette piste. Cache debug contenant encore le candidat rejeté :
rebuild requis avant livraison depuis ce cache. Examiner le coût des feuillages
réellement visibles ; aucune qualification ou optimisation produit acquise.

Éclairage High/Medium/Medium/High isolé en marche sur le release optique existant :
forêt High et eau/ombres constantes, quatre sorties 0, essais fermés. Tiers
forestier 43,24 / 45,35 / 44,99 / 44,76 FPS ; dérive du témoin, aucun gain majeur
démontré ni Medium promu. Requêtes observées, binding GPU non exporté par ce
release. `R1_040_ENVIRONMENT_NATIVE_CYCLE.md`, complément coût : ne pas répéter
cette paire ou réduire encore la carte d'éclairage ; examiner soumission et
remplissage forestiers. Aucun changement produit ni seuil de gate fermé.

Phases CPU existantes réutilisées pendant la marche : 1651 poses réelles,
intervalle rendu/présentation/attentes ~10 → 20 ms en forêt, phases ordinaires
du monde ~4–5 ms sans croissance comparable. Ce n'est pas un timer GPU pur.
95 tests Runtime/Clippy/build/fmt PASS, essai fermé. `R1_RENDERDOC_GPU_DURATIONS.md`,
séparation CPU ; cibler renderer/extraction/GPU/présentation plutôt que nouvelles
micro-optimisations des systèmes de jeu. Aucun gain ou passage de gate déduit.

Diagnostics Bevy existants raccordés au parcours natif : 29 snapshots réels,
sans nouveau query GPU ni changement de rendu. Passe transparente ~6,17 ms GPU
en moyenne sur les neuf relevés forestiers, opaque ~1,19, transmissive ~0,88.
Couverture partielle/asynchrone, binaire dev instrumenté : pas GPU total ni FPS
release. 95 tests Runtime/Clippy/build/fmt PASS, essai fermé, entrées conservées.
`R1_RENDERDOC_GPU_DURATIONS.md`, complément courant : identifier les soumissions
et coûts non couverts avant nouvelle optimisation. Aucune qualification promue.

Budgets végétation High/Medium comparés en marche release sur le candidat optique,
sans reconstruction : quatre passages High/Medium/Medium/High, mêmes 1662 arbres.
Dernier tiers forestier ~44,9 → 47,5–47,8 FPS ; gain répété mais insuffisant,
Medium non promu. 1373 ticks communs, écart maximal 7,6 mm ; ombres/eau/sources
constantes, quatre processus fermés. `R1_FOREST_FOUR_TIERS.md`, coût vivant.
Ne pas poursuivre par petits ajustements de distance inchangés : le coût du rendu
forestier doit être isolé, les seuils et l'acceptation visuelle restent ouverts.

Release optique courant construit et mesuré sans compilation/capture concurrente :
quatre parcours sans/avec/avec/sans reflets, même milieu transmissif et EXE,
sources constantes, 1344 ticks communs à poses/caméras identiques. ~64–65 FPS
sur le trajet mais ~44,5–45,6 dans le tiers forestier ; p95 ~24 ms et 1 % low
~37–40 FPS. Pas de coût majeur des reflets sur ce trajet : priorité de performance
à la forêt, pas à de nouvelles micro-retouches d'eau. RX 7800 XT, ombres candidates,
pas GPU cible/Studio/art qualifiés. Quatre essais fermés. `R1_RELEASE_PREPARATION.md`,
section coût optique courant ; ne pas répéter cette paire inchangée.

Commandes de reflets désormais exercées dans le Studio sur le bundle existant :
trois JSON confirment 48 pas/96 m → environnement 0/0 → retour exact 48/96,
sources constantes. PNG 1408 × 966 inspecté, eau lointaine : pas de qualification
artistique rapprochée. 94 pipelines prêts, zéro échec final, pic transitoire de
18 conservé. Huit processus d'essai fermés, 14 empreintes de livraison inchangées.
53 tests Studio/Clippy WASM du build conservés, aucune recompilation. Lot de
commandes terminé, ne pas rejouer inchangé ; mouvement, coût et art restent
ouverts. `R1_WATER_FORWARD_REFLECTION.md`, section commandes Studio.

Le prototype de reflet transmissif suit désormais les budgets de réflexion et
les désactivations de `WaterQualityRequest`, avec observation CPU distincte du
SSR caméra. Cycle ECS des quatre niveaux PASS ; 664 tests workspace avant
extraction du diagnostic, Clippy/build/check WASM après extraction PASS.
Captures natives Low/High inspectées, reflet local coupé/rétabli, sources
inchangées, essais fermés. `R1_WATER_FORWARD_REFLECTION.md`, section budgets.
Pas de profils globaux, coût, mouvement ou Studio qualifiés.

Reflets du décor ajoutés au candidat transmissif derrière une option native
explicite : parcours Bevy réutilisé, aucune passe ou texture supplémentaire.
Paire Full HD inspectée : reflet du pont visible, fond conservé. Deux essais
fermés, 663 tests workspace PASS / un ignoré, Clippy/check WASM PASS ; stabilité,
profils, coût et exécution Studio non qualifiés.
`R1_WATER_FORWARD_REFLECTION.md`. Gate 1 reste ouverte, rendu par défaut inchangé.

Normales du détail d'eau : conversion UV→monde corrigée par les dérivées réelles,
sans supposer un courant orthonormal. 662 tests workspace PASS / un ignoré,
Clippy/build/check WASM et onze jeux numériques GPU PASS ; paire native avec
kit courant inspectée et fermée. Différence visuelle faible, correction de repérage
seulement, pas eau réaliste ni gain FPS. `R1_WATER_DIRECTIONAL_COORDINATES.md`.
Studio non reconstruit ; ne pas répéter cette paire figée inchangée.

Profil d'eau RGB désormais compatible avec les parcours natifs bornés, et mode
d'eau inclus dans leur rapport. Parcours courant de 1800 ticks terminé, kit et
sources inchangés, milieu optique confirmé, processus fermé. 92 tests Runtime,
Clippy/build PASS. Preuve d'intégration en dev seulement, pas gain de rendu,
benchmark release ou acceptation artistique : `R1_WATER_MEDIUM_INTEGRATION.md`.
Les comparatifs release précédents utilisaient l'eau opaque et ne qualifient pas
ce candidat optique ; ne pas leur attribuer une portée supérieure.

Release courant reconstruit, sources épinglées constantes, capture native inspectée
et quatre parcours dev/release/release/dev terminés : ~54,7 → 62,6 FPS sur le trajet,
mais ~39,6 → 42,6 FPS dans son dernier tiers forestier. p95 release ~25,2 ms et
1 % low ~36 FPS : fluidité toujours insuffisante. Ombres candidates 2048/3 et GPU
RX 7800 XT, pas qualification High canonique/GPU cible/Studio. Tous les processus
d'essai fermés. `R1_RELEASE_PREPARATION.md`, actualisation du 9 septembre : ne pas
répéter cette paire inchangée ni optimiser sur les seuls résultats dev.

Essai alpha exactement nul terminé : quatre parcours A/B/B/A autour de 55 FPS,
aucun gain reproductible. Raccourci shader et test dédié retirés, shader précédent
restauré à empreinte identique ; preuves conservées dans `R1_FOLIAGE_ALPHA_COST.md`.
Ne pas répéter cette piste ou le passage BLEND/MASK déjà rejeté. Aucun progrès
artistique ni seuil de performance acquis par cet essai ; Gate 1 reste ouverte.

Durées GPU de la capture historique RenderDoc obtenues sans relancer Forge :
replay PASS, 2042 résultats feuilles, compteurs complets pour 1780 draws/dispatchs.
Passe transparente dominante dans ce replay ; ombres et génération des cartes
d'éclairage désormais chiffrées séparément. **Pas les FPS du candidat courant**.
34 tests outillage PASS, processus fermé ; `R1_RENDERDOC_GPU_DURATIONS.md`.
Piste de diagnostic précisée, aucun nouveau gain de rendu ni passage de gate.

Comparaison des ombres **en marche vivante** acquise : quatre passages ABBA natifs
sur RX 7800 XT, même EXE/kit, poses concordantes à moins de 8 mm aux ticks communs.
Dernier tiers forestier ~32 → 40 FPS avec 2048/trois cascades, trajet complet
~49 → 57 FPS. Gain répété, 60 FPS non atteints ; candidat non promu.
Harnais natif borné ajouté, 91 tests Runtime/Clippy/build PASS, quatre processus
fermés. `R1_LIVE_WALK_SHADOW_COMPARISON.md` ; ne pas refaire la paire inchangée.

Provenance du kit colorimétrique livré actualisée : neuf fichiers source/Studio
identiques, dérivation limitée aux huit images de couleur annoncées ; quatre
autres GLB inchangés. Audit borné PASS et quatre refus synthétiques PASS,
aucune identité binaire avec les 139 fichiers Cadle exclus. Voir
`R1_053_PROVENANCE_CHAIN.md`. R1-053 reste ouvert sur son périmètre complet ;
pas de nouvelle qualification artistique ou de droits. Aucun rendu relancé.

Candidat ombres équilibrées accessible et exercé depuis le menu Studio : High
2048 / trois cascades / 128 m / contact confirmés dans le plan, PNG inspecté.
56 tests Studio, Clippy WASM et build PASS ; exports archivés et essai fermé.
`R1_STUDIO_SHADOW_CANDIDATE.md` : lot d'accès terminé, profil canonique intact,
comparaison en jeu/acceptation encore dues, aucun gain ou passage de gate annoncé.

Comparaison vivante des ombres arrêtée sur refus du témoin stationnaire : dérive
physique > 2 cm après 255/300 échantillons, sortie 1 conservée, variante non lancée.
Aucun gain annoncé, seuil/physique inchangés, processus fermé. Complément
`R1_SHADOW_BALANCED_CANDIDATE.md` : le candidat 2048 / trois cascades reste
non promu ; ne pas relancer ce protocole inchangé ni requalifier les mesures figées.

Parcours natif dans la forêt exécuté avec le candidat courant : environ 149 m
de déplacement net par le contrôleur, arrivée au sol, PNG final 1080p inspecté,
sortie 0 et processus fermé. Cadence glissante finale ~36 ms : coût perceptible,
pas de benchmark. Observations ponctuelles, pas de preuve de continuité LOD.
Complément `R1_FOREST_FOUR_TIERS.md` ; aucun réglage d'ombres ou rendu promu.

Forêt gouvernée : cycle natif High → Low → Medium → Ultra → High exécuté,
cinq PNG Full HD inspectés, 1662 arbres conservés, 176 lots lointains réutilisés.
Sortie 0, essai fermé ; preuve de changement de budgets, pas de presets globaux,
de transitions en déplacement ou d'acceptation artistique. Voir
`docs/evidence/gate1/R1_FOREST_FOUR_TIERS.md`. Ne pas refaire le chargement/cycle
inchangé ; qualifier ensuite les transitions forestières en déplacement.

Qualification atmosphère/profils : omission du ledger volumétrique dans l'export
Studio corrigée sans changement de rendu. 54 tests Studio et Clippy WebGPU PASS ;
build et cycle Tauri terminés : High sans/avec brume → Low → High → désactivation,
cinq JSON/deux PNG archivés, restitution vérifiée. Essai fermé, qualification
artistique/performance et profils complets toujours ouverts. Voir
`docs/evidence/gate1/R1_STUDIO_ATMOSPHERE_CYCLE.md`. Les anciens cycles Web restent
des preuves partielles existantes, pas des essais à refaire à l'identique.

Studio : route « eau claire RGB » et profil synthétique partagé intégrés en
lecture seule avant publication du manifeste. 656 tests PASS / un ignoré et
Clippy WebGPU PASS ; bundle construit, lancé, PNG/JSON réels inspectés.
Manifestes et milieu RGB concordants, 74 pipelines prêts sans échec final.
Rendu global toujours insuffisant ; cadence relevée hors benchmark, aucune
qualification performance. Essai et descendants fermés, absence vérifiée.
`docs/evidence/gate1/R1_STUDIO_MEDIUM_CONTACT.md`. Ne pas confondre accès codé
avec qualification artistique ou validation humaine. Lot d'intégration terminé ;
suite : qualification indépendante des effets/profils atmosphériques du Studio,
pas de nouvelle série de réglages de cette vue d'eau.

Transition de contact ajoutée au candidat optique : lecture du fond existant,
sans nouveau maillage ni passe. 655 tests PASS / un ignoré, Clippy/fmt/build
natif/check WASM PASS, onze cas GPU PASS. Capture native inspectée : contact
moins tranché, gain limité, géométrie inchangée ; essai fermé.
`docs/evidence/gate1/R1_WATER_CONTACT.md`. Suite : candidat Studio intégré,
pas de nouvelle série de retouches de cette vue. Gate 1 toujours ouverte.

`WaterMediumProfile` raccordé au document optionnel et au shader candidat :
absorption/diffusion RGB en m^-1, anciens documents préservés. 655 tests workspace
PASS, un ignoré ; Clippy/fmt/build natif/check WASM PASS, neuf cas GPU PASS.
Trois captures natives inspectées : premier profil trop laiteux, variante claire
moins opaque, rives encore anguleuses. Essais fermés ; aucun nouveau Studio ni
qualification artistique livré. Paramètres réutilisables, cartes non modifiées.
Voir `docs/evidence/gate1/R1_WATER_MEDIUM_INTEGRATION.md` pour les limites.

Étirement transversal des rides retiré du candidat : advection métrique
conservée, capture native inspectée avec motifs moins alignés sous le pont.
649 tests PASS / un ignoré, build natif PASS, essais fermés. Gain local seulement,
fond brun/reflets encore insuffisants ; `R1_WATER_UNSTRETCHED_DETAIL.md`.
Dernier Studio inchangé : nouvelle variante non qualifiée en WebGPU/mouvement.

Éclairage eau et contraste intégrés au candidat Studio : Clippy WebGPU et
builds PASS, 649 tests précédents réutilisés sans changement source. Export
JSON/PNG réel inspecté, 74 pipelines préparés, aucune erreur finale. Détails
des ombres conservés ; eau encore trop uniforme/striée, pas de validation art.
`docs/evidence/gate1/R1_STUDIO_LIT_CONTRAST.md`. Essai fermé, Gate 1 ouverte.

Contraste météo corrigé dans l'adapter commun : courbe positive au-dessus de
1, paramètres authorés conservés. 649 tests PASS / un ignoré, Clippy moteur,
check WASM et build natif PASS. Deux captures inspectées (clair/soleil réduit) :
aplats noirs supprimés, détails du pont/lit retrouvés. Essais fermés.
`docs/evidence/gate1/R1_WEATHER_SHADOW_CONTRAST.md`. Bundle Studio non reconstruit,
eau globale toujours non qualifiée ; Gate 1 reste ouverte.

Écrasement des noirs isolé dans le contraste météo, pas uniquement dans l'eau :
à soleil réduit, même EXE/caméra et seul contraste pluie 1,08 → 1,00 rétablissent
les détails du pont et du lit. Profil de diagnostic local seulement, aucun
correctif produit livré. Adapter le contraste avant toute nouvelle retouche
de couleur d'eau ; complément `R1_WATER_LIT_SCATTERING.md`, essais fermés.
Gate 1 ouverte, météo et Studio non qualifiés.

Contribution colorée de l'eau raccordée à l'éclairage incident : ancien terme
quasi éteint par EV12 identifié et corrigé. Deux captures natives inspectées :
teinte profonde visible, fond conservé près des berges. 37 tests/Clippy/fmt/build
PASS, essais fermés ; `docs/evidence/gate1/R1_WATER_LIT_SCATTERING.md`.
Coût d'ombres supplémentaire et modèle diffus approximatif ; météo/temporalité/
Studio de cette correction non qualifiés. Aucun passage de gate.

Eau métrique intégrée dans un nouveau candidat Studio : 648 tests workspace
PASS / un ignoré, Clippy WASM et builds PASS. Exports JSON/PNG à 0/250/500 ms
inspectés : variations visibles, pas de qualification de fluidité ou d'art.
Rendu encore lisse/strié et fond brun dominant. Cadrage natif élargi acquis,
essais fermés ; `docs/evidence/gate1/R1_STUDIO_DIRECTIONAL_WATER.md`.
Ne pas répéter ces exports inchangés ; R1-022/023 et Gate 1 restent ouverts.

Coordonnées de rides désormais métriques le long de la rivière ; projection
mondiale directionnelle rejetée. 37 tests/Clippy/build natif PASS, invariance
par translation testée sur un coude. Capture acquise et inspectée sans gain
artistique net : règle technique conservée, mouvement/Studio non vérifiés.
`docs/evidence/gate1/R1_WATER_DIRECTIONAL_COORDINATES.md`. Essais fermés,
Gate 1 ouverte ; observer une portion courbe en mouvement avant autre réglage.

Normales de détail à gradients continus : 36 tests et build natif PASS,
comparaison rapprochée inspectée. Motifs anguleux atténués, gain limité ;
candidat conservé, pas promu au Studio. Courant directionnel et animation
restent à traiter ; `docs/evidence/gate1/R1_WATER_GRADIENT_DETAIL.md`.
Capture fermée, aucune qualification de Gate 1 ajoutée.

Isolation optique native disponible sans reconstruire entre variantes. Trois
captures du même EXE : normales coupées atténuent les motifs, réfraction coupée
ne les supprime pas. 86 tests runtime/Clippy/build PASS ; essais fermés.
Cibler désormais les normales de détail, sans masquer l'ombre sous le pont.
`docs/evidence/gate1/R1_WATER_OPTICS_ISOLATION.md`. Aucun nouveau rendu promu.

Essai de macro-relief non périodique exécuté puis retiré : pas d'amélioration
visuelle suffisante dans la paire native isolée. Shaders précédents restaurés
par hashes, lit minéral conservé ; aucun nouveau bundle Studio nécessaire.
`docs/evidence/gate1/R1_WATER_MACRO_RELIEF_REJECTED.md`. Ne pas recommencer
une série de variantes de grandes vagues ; composition/détail restent à isoler.

Lit minéral confirmé dans le Studio reconstruit : même vue rapprochée, disparition
du fond vert et des frontières brun/vert, exports JSON/PNG inspectés. 645 tests
workspace PASS / un ignoré, Clippy WASM et builds PASS. Rides et rupture sombre
sous le pont persistent ; eau globale/profils non qualifiés. Essai fermé.
`docs/evidence/gate1/R1_WATER_MINERAL_BED.md` ; Gate 1 reste ouverte.

Continuité minérale du lit implémentée dans le maillage de revêtement commun,
sans mutation de carte, relief ou collision. 36 tests ciblés eau et Clippy strict
PASS, build/capture natifs acquis. Le cadrage natif ne montre pas assez le lit :
correction visuelle rapprochée et bundle Studio encore non vérifiés.
`docs/evidence/gate1/R1_WATER_MINERAL_BED.md` ; essais fermés, Gate 1 ouverte.

Vue rapprochée du candidat optique exécutée ; JSON/PNG acquis à 0 et 250 ms,
nouveaux champs optiques présents dans les exports. Le fond vert et les bandes
nettes restent visuellement insuffisants. Prochaine correction à cibler sur
les règles génériques lit/berge après isolation de leur origine, pas sur une
retouche de carte. Processus d'essai fermé ; aucune qualification promue.
`docs/evidence/gate1/R1_STUDIO_WATER_CLOSE.md`.

### Dernier état vérifié — 8 septembre

Restitution optique Studio corrigée dans le code : libellé « candidat épinglé »,
ledger moteur/phase CPU échantillonné à 1 Hz et ajouté à l'export JSON. Tests,
fmt, Clippy natif/WASM et check WASM PASS. Pas encore de bundle/export réel avec
ces nouveaux champs ; à joindre au rejeu rapproché/temporel.
`docs/evidence/gate1/R1_STUDIO_WATER_OPTICS_DIAGNOSTICS.md` ; Gate 1 ouverte.

Studio optique corrigé construit et lancé : menu réellement exercé, scène
acceptée, JSON/PNG exportés et inspectés, processus d'essai fermé. 94 pipelines
préparés, aucun échec final (pic transitoire 18 consigné). Vue initiale éloignée,
mouvement non qualifié ; détail du mode optique absent de l'export et libellés
de profil encore à clarifier. `docs/evidence/gate1/R1_STUDIO_WATER_OPTICS.md`.
Gate 1 ouverte, aucune qualification artistique ou publication.

Bundle Studio optique construit et menu exercé, mais premier chargement refusé
par l'observateur moteur (`observed_incomplete_components`). Cause corrigée :
contrat distinct du candidat transmissif, exigences du défaut conservées.
643 tests PASS, fmt/Clippy/check WASM/build natif PASS ; capture native confirme
`ObservedMainWorldComponents` et conserve le rendu. Processus d'essai fermés.
Bundle Studio corrigé à reconstruire/rejouer ; aucune qualification visuelle.
`docs/evidence/gate1/R1_WATER_OPTICS_OBSERVATION.md` ; Gate 1 ouverte.

Accès Studio du candidat optique implémenté : lien explicite, sélection stricte,
installation de la ressource moteur avant plugins, statut sans qualification.
642 tests PASS / un ignoré, fmt/Clippy/check WASM PASS. Bundle non construit ni
lancé dans ce lot : clic/pixels/mouvement Studio restent à vérifier.
`docs/evidence/gate1/R1_STUDIO_WATER_OPTICS.md` ; Gate 1 ouverte.

Ombre du candidat transmissif corrigée : seule l'eau cesse de projeter dans
l'atlas opaque, tout en restant réceptrice. 641 tests PASS / un ignoré,
fmt/Clippy/check WASM/build PASS. Capture native inspectée : rupture sombre
supprimée à réglages d'ombres inchangés, ombre du pont encore visible.
Essai fermé. `docs/evidence/gate1/R1_WATER_SHADOW_PREVIEW.md` ; candidat non
promu dans le Studio, mouvement/profils/art et Gate 1 toujours non qualifiés.

Courant local du candidat : direction du tracé encodée dans les attributs existants,
détail normal advecté à deux échelles, extinction séparée de la palette de couleurs.
640 tests PASS / un ignoré, fmt/Clippy/check WASM/build et deux captures natives
PASS. Grosses stries atténuées, mais rupture de teinte visible à mi-rivière :
diagnostic supplémentaire : la coupure se déplace avec le preset d'ombres.
Projection opaque de l'eau sur son lit suspectée, pas encore confirmée/corrigée.
Candidat non promu. Pas de preuve de mouvement ni de Studio.
Essais fermés ; `docs/evidence/gate1/R1_WATER_FLOW_PREVIEW.md`.

Profondeur raccordée dans un candidat moteur natif explicite
`--water-depth-preview`, sans changer le rendu par défaut ni les sources.
Transmission forward et reflets d'environnement seulement (SSR désactivé,
qualités non promues). Tests workspace, Clippy/check WASM/build/captures PASS ;
comparaison corrigée pour conserver TAA. Raccord plus doux, mais eau trop sombre
et vagues répétitives : candidat non promu artistiquement, pas encore livré
dans le Studio. Sessions fermées. `docs/evidence/gate1/R1_WATER_DEPTH_PREVIEW.md`.

Calcul optique exécuté sur GPU : neuf cas synthétiques PASS par lecture storage
sur RX 7800 XT/Vulkan (distance, viewport décalé, premier plan, bornes et absorption).
Sonde headless terminée, JSON/hash conservés, fmt/Clippy ciblé PASS. Pas encore
de texture de fond réelle, de composition ou de nouveaux pixels dans le jeu.
Voir `docs/evidence/gate1/R1_WATER_OPTICS_REASSESSMENT.md` ; Gate 1 ouverte.

Profondeur d'eau : brique WGSL originale de reconstruction du rayon de vue et
d'absorption écrite, non raccordée au rendu. Parsing/validation Naga du shader
dans deux consommateurs PASS ; 638 tests workspace PASS / un ignoré, fmt et
Clippy strict PASS. Pas de lecture GPU numérique ni de nouveaux pixels.
Intégration de la composition et des reflets encore à réaliser ; voir
`docs/evidence/gate1/R1_WATER_OPTICS_REASSESSMENT.md`.

Eau toujours refusée par le propriétaire après les petites rides. Recherche
technique et inspection Bevy : la transmission standard change de phase de rendu
et ne calcule pas seule la profondeur optique de rivière ; son activation seule
ne garantit pas les reflets SSR actuels. Prochaine priorité : composition
fond/profondeur/berges et réflexion compatible, avant de nouveaux réglages de
vagues. Diagnostic seulement, aucun nouveau shader livré ni critère validé :
`docs/evidence/gate1/R1_WATER_OPTICS_REASSESSMENT.md`.

Petites rides d'eau filtrées implémentées dans les normales du shader partagé,
sans nouvelle géométrie ni retouche de carte. 637 tests PASS / un ignoré,
Clippy/check WASM/build natif PASS ; vues figée et rapprochée inspectées :
eau proche plus détaillée et éclat fragmenté, régularité résiduelle et réalisme
non acceptés. Candidat provisoire intégré au Studio, lancement et PNG/JSON vérifiés :
rides fines visibles, warmup prepared, commande de pas 0 → 0,250 s exercée,
caméra conservée et collisions ensuite prêtes. Pas de qualification artistique,
de mouvement ou de performance. Essais fermés, fenêtre propriétaire préservée ;
`docs/evidence/gate1/R1_WATER_RIPPLE_DETAIL.md`.

Eau : variante de crêtes modulées construite et comparée en natif à caméra/temps
figés ; effet toujours trop périodique, variante rejetée et patch WGSL retiré.
Tests, Clippy, check WASM et capture PASS ne valent pas amélioration artistique.
Ne pas reconstruire cette variante dans le Studio. Preuve et réorientation :
`docs/evidence/gate1/R1_WATER_WAVE_VARIATION.md`.

Préparation des vues fixes : cause du cover persistant identifiée (attente des
handles physiques alors que la simulation est figée). Correctif candidat séparant
préparation statique et readiness jouable, test ciblé et 637 workspace PASS / un
ignoré, fmt/Clippy strict/check WASM PASS. Nouveau Studio construit puis lancé :
cover disparu automatiquement, warmup prepared en 20,562 s et huit observations
stables. PNG/JSON inspectés, raccord de berge conservé, session fermée. Temps
figé à zéro et collisions encore en attente signalées ; pas un playtest qualifié.
Voir `docs/evidence/gate1/R1_FIXED_VIEW_WARMUP.md`. Aucun passage de gate.

Parcours de pont Studio explicitement assemblable via `-BridgeProbe`, sans
changer le lancement normal. Premier essai rejeté après 28 ticks : chargement
initial consommant le watchdog de marche. Séparation préparation 120 s /
marche-arrêt 30 s vérifiée : 637 tests PASS / un ignoré, Clippy/check WASM/builds
PASS ; traversées native et Web terminées, 518/518 ticks au sol et 42,528 m.
Packaging étendu aux vues fixes existantes : quatre cas valides / sept refus PASS.
Voir `docs/evidence/gate1/R1_BRIDGE_PREPARATION_DEADLINE.md`.

Chargement initial Studio corrigé : 635 tests PASS / un ignoré, Clippy et builds
PASS ; écran de préparation puis scène colorée observés. JSON final non exporté
par l'agent après prise en main propriétaire ; Studio PID 39624 laissé ouvert.
Voir `R1_STUDIO_INITIAL_RENDER_WARMUP.md`. Nouveau retour : bande entre rivière
et berge refusée. Extension de la surface sous la berge implémentée dans le moteur
partagé ; 14 tests ciblés et 636 workspace PASS / un ignoré, Clippy et check WASM
PASS. Capture native rapprochée inspectée : bande verte et ombre suspendue
supprimées, raccord au terrain ; crénelures des vagues et finition encore ouvertes.
Nouveau Studio construit ; après interruption initiale par Échap, reprise WebView
avec PNG/JSON exportés et vérifiés : scène colorée au spawn, kit/forêt et six
sources alignés, warmup prepared. Export rapproché Web ensuite inspecté : raccord
aux deux berges confirmé, crénelures/vagues régulières restantes. Limite distincte :
mode caméra fixe figé à zéro, collisions en attente et overlay de préparation
persistant ; pixels acquis par export du canvas, pas un démarrage fixe qualifié.
Session de test fermée, fenêtre propriétaire inchangée. Voir
`docs/evidence/gate1/R1_WATER_SHORE_OVERLAP.md`. Aucune validation de Gate 1.

Candidat LOD gouverné intégré au moteur partagé : transition proche High
24–32 m au lieu de 64–72 m, sans changer les 1 662 arbres, leurs tailles,
poses ou collisions. Tests workspace, Clippy strict, check WASM, build et capture
natifs PASS. Mesure statique locale 20,639 → 31,870 FPS ; compromis réel : ombres
des arbres moins lointaines et arrière-plan plus clair. Pas de qualité équivalente
ni de 60 FPS démontrés. Studio ensuite reconstruit avec ce LOD, kit corrigé
embarqué, lancé et PNG/JSON vérifiés : 1 662 arbres, sources alignées. Profil
d'ombres Studio inchangé, pas de transfert du gain natif ; noir transitoire au
démarrage encore observé, capture finale colorée depuis la prairie. Revue humaine
et Gate 1 ouvertes. Studio/WebViews fermés, pas de publication. Voir
`docs/evidence/gate1/R1_GOVERNED_NEAR_LOD_CANDIDATE.md`.

Kit aux couleurs corrigées raccordé au candidat Studio local : frontend frais
Trunk et coque Tauri compilés, lancement réel et exports JSON/PNG vérifiés.
Assemblage `-GovernedKit` avec pins et refus d'entrée testés (deux cas valides,
quatre refus). Kit 20/20, forêt 1 662 arbres, six hashes de scène alignés.
Capture stabilisée correcte depuis la prairie ; zones noires initiales disparues
sans réglage, problème de présentation au démarrage non qualifié. Pas de parité
visuelle exacte avec le parcours natif, viewport 1408×966 et performance non
qualifiée. Studio/WebViews fermés, anciennes sources/bundles préservés.
Voir `docs/evidence/gate1/R1_STUDIO_COLORSPACE_INTEGRATION.md`. Gate 1 reste ouverte.

Cause matérielle du sous-bois noir reproduite : couleurs procédurales linéaires
écrites sans encodage dans des PNG sRGB (0,18 devenait 0,027 après décodage).
Writer commun corrigé, test réel Blender rouge puis vert sur trois maps,
régression alpha PASS. Dérivé local des deux arbres, géométrie/maps de données
intactes, validateur glTF et réimport PASS ; capture native inspectée : troncs
bruns lisibles et feuillage nuancé sans toucher au soleil ni à l'exposition.
Originals préservés, bundle Studio non actualisé par ce lot, aucune promotion
humaine. Overlay ~22 FPS, performance toujours ouverte, processus fermés.
Voir `docs/evidence/gate1/R1_PROCEDURAL_TEXTURE_COLORSPACE.md` ; Gate 1 reste ouverte.

Liaison d'éclairage corrigée dans le moteur partagé : les changements de texture
diffuse, intensité, rotation et indicateur de diffuse sont publiés même si la
texture spéculaire garde son identité, sans réécriture à paramètres constants.
630 tests workspace PASS / un timing ignoré, test de régression rouge puis vert,
Clippy strict, check WASM, build natif et capture PASS. Sur le lancement réel,
sonde et caméra sont identiques (gain 1,0), mais les troncs restent presque noirs :
aucune correction artistique prétendue. Processus fermé, règles forestières
préservées, aucune publication. Voir `docs/evidence/gate1/R1_SKY_BINDING_INTEGRATION.md`.

Sonde de ciel brute vérifiée par lecture GPU réelle : six faces HDR 256²,
393 216 texels RGB finis et lumineux, aucune face noire. Ne pas augmenter le gain
du ciel pour masquer le sous-bois sombre : filtrage diffus, liaison finale et
matériaux restent à vérifier. Aucun réglage de rendu modifié ; capture toujours
sombre. Harnais natif uniquement, 84 tests + une intégration, Clippy/build PASS,
processus fermé. Voir `docs/evidence/gate1/R1_SKY_IBL_READBACK.md`.

Candidat combiné d'ombres 2048 / trois cascades / 128 m / contact conservé produit
hors dépôt et capturé après marche réelle dans le sous-bois. Projection effective
conforme, même forêt et capsule, ombres au sol présentes ; canopée/troncs encore
trop sombres. Overlay ~21,8 FPS (non benchmark), objectif 60 FPS non atteint.
Aucun preset canonique/Studio remplacé ni jugement humain acquis. Derniers contrôles
isolés : contact désactivé 13,285 FPS (faible effet), trois cascades seules 15,716 FPS ;
ne pas répéter ces essais inchangés. 82 tests Runtime + une intégration, Clippy,
build/capture PASS, trois processus fermés. Voir `R1_SHADOW_BALANCED_CANDIDATE.md`.

Paramètre d'ombres isolé : atlas 4096 → 2048, mêmes quatre cascades à 128 m et
contact shadows (16 pas), tous confirmés dans la projection exportée :
13,000 → 18,425 FPS sur 300 frames statiques. Portée seule 128 → 96 m : 12,819 FPS,
aucun gain ; profil 64 m refusé par monotonie High/Medium, échec conservé.
Résolution = levier significatif, mais preset non promu, aucune capture 2048 et
pas de qualification (poses proches, phase des animations au gel non normalisée).
Nombre de cascades/contact shadows restent à isoler. Entrée de profil externe
bornée au harnais natif, identité/révision validées ; 82 tests + une intégration,
Clippy et build PASS, quatre processus terminés. Voir `R1_UNDERSTORY_CPU_PHASES.md`.

Piste majeure enfin différenciée sous canopée : ombres demandées High → Low,
autres domaines explicitement High, même binaire/kit, 300 frames statiques :
13,057 → 29,891 FPS, temps externe aux phases principales 73,106 → 29,921 ms.
Paire exploratoire, pose différente d'environ 14 mm ; Low change plusieurs
paramètres et n'a pas été capturé. Aucun réglage promu : isoler résolution,
cascades/distance et contact shadows, exporter leur observation effective puis
valider visuellement. Le contrôle précédent des compteurs GPU (13,065 contre
12,901 FPS désactivés, timestamps conservés) n'apporte aucun gain et n'est pas
à répéter. Quatre processus terminés, preuves dans `R1_UNDERSTORY_CPU_PHASES.md`.
Gate 1 reste ouverte, Studio et réglages par défaut inchangés.

Localisation CPU du sous-bois exécutée : 300 frames statiques, 13,188 FPS,
72,260 ms hors First–PostUpdate contre 3,559 ms dans ces phases. Ne pas attribuer
les ~76 ms au compteur de végétation ou aux règles de génération : PostUpdate
complet représente ici moins de 3 ms en moyenne. Le segment externe inclut
Last, extraction/rendu, présentation et attentes ; cause précise non établie.
Prochaine mesure ciblée sur ce segment, pas de nouvelle comparaison alpha.
Harnais natif uniquement, 81 tests Runtime + un test d'intégration, Clippy et
build PASS ; processus fermé. Voir `docs/evidence/gate1/R1_UNDERSTORY_CPU_PHASES.md`.

Mesure sous canopée désormais reproductible après parcours : quatre runs natifs
BLEND/MASK/MASK/BLEND, 300 frames chacun en Full HD High, temps virtuel explicitement
figé après stabilisation. BLEND 13,026/13,039 FPS ; MASK 13,209/13,126 FPS : aucun
gain suffisant pour promouvoir MASK. Ce test isole un rendu statique, pas la
performance du jeu vivant. Le premier essai en simulation active a été refusé
par le contrôle de mouvement ; logs conservés, seuil non assoupli. 79 tests
Runtime + un test d'intégration PASS, Clippy strict et build natif PASS.
Tous les processus fermés. La simulation active seule n'explique donc pas les
13 FPS ; coût CPU/rendu/présentation restant à isoler, cause exacte non établie.
Ne pas répéter cette comparaison inchangée. Voir `R1_FOLIAGE_ALPHA_COST.md`
dans `docs/evidence/gate1/`. Gate 1 reste ouverte, aucun matériau promu.

Parcours natif commandé jusqu'au sous-bois désormais exécuté sans téléportation :
`--walk-proof`, contrôleur/collisions normaux, aucune modification de carte.
78 tests Runtime + un test d'intégration, Clippy strict et build PASS. Capture
finale en (-132,50 ; 2,60 ; -203,52) : capsule visible, grands troncs rapprochés ;
pas d'acceptation artistique ni de mesure exacte des cimes hors champ. Coût local
important révélé : environ 13 FPS affichés sous canopée, ombres très sombres,
contre les mesures historiques d'une autre caméra. Priorité au coût du rendu de
proximité, pas à de nouveaux essais inchangés depuis la prairie. Un essai arrêté
par deadline et une vue dans l'eau sont conservés séparément. Processus fermés.
Voir `docs/evidence/gate1/R1_FOREST_REGION_SCALE.md`. Gate 1 reste ouverte.

Clavier Studio : diagnostic DOM borné ajouté et lancé dans un candidat Tauri
séparé, 42 tests Studio et Clippy WASM PASS. La touche V envoyée par Computer Use
atteint le canvas avec un code physique vide/Unidentified, pas KeyV attendu par
le contrôleur. Ce résultat explique cette injection sans bascule ; il ne prouve
ni une panne moteur ni la qualification du clavier réel. Aucun changement aux
commandes, au spawn ou à la forêt. Candidat fermé, preuve détaillée dans
`docs/evidence/gate1/R1_STUDIO_EXPLORATION.md`. Ne pas répéter cet envoi inchangé.

**Revue artistique propriétaire : candidat forêt refusé.** Le propriétaire juge
les arbres trop petits et trop espacés, avec un résultat de plaine plutôt que
de forêt. Cible demandée : arbres adultes 5 à 10 fois plus hauts que le personnage
et peuplements nettement plus rapprochés. Le personnage canonique mesure 1,80 m
(capsule : deux fois 0,55 + 0,35 m), soit une cible arborée de 9 à 18 m.
Ne pas diminuer le personnage pour simuler ce rapport, ni promouvoir le candidat
refusé. Correction ensuite intégrée : recette forestière optionnelle et masque
de densité sérialisé, raccord Forest moins Buildable, hauteur effective 9,04 à
17,80 m sans changer le personnage. La vallée produit 1 662 arbres avec leurs
colliders, en conservant exclusions et placements authorés ; les anciennes
recettes sans ce champ restent compatibles. 619 tests PASS / un timing ignoré,
Clippy strict PASS, compilation et capture natives inspectées. Le peuplement
est maintenant concentré en forêt, pas étendu à toute la prairie. Mesure native
dev High : 39,77 FPS, donc objectif 60 FPS non satisfait sur cet essai ; aucune
qualification artistique ou performance déduite. Studio optimisé assemblé,
ouvert et capturé via Computer Use : 1 662 arbres, kit 20/20, hashes alignés,
PNG/JSON exportés et contrôlés. Le spawn reste dans la prairie ; vue comparative
près d'un arbre et acceptation artistique restent dues. Instance fermée, aucun
processus candidat restant ; WASM 41 137 942 octets.
Complément sans rebuild : même candidat natif capturé avec la caméra authorée
`human_bridge`, vue basse du pont et lisière dense inspectées, sortie 0 puis
fermeture vérifiée. Personnage au pied d'un arbre toujours absent de cette vue ;
ce cadrage ne vaut pas acceptation ni mesure indépendante de hauteur des yeux.
Voir `docs/evidence/gate1/R1_FOREST_REGION_SCALE.md`. Gate 1 reste ouverte.

Piste d'optimisation évaluée, non promue : six matériaux de feuillage gouvernés exportés
en BLEND, passe transparente native relevée à 6,11 ms GPU. Politique MASK opt-in
ajoutée au worker, trois exports de contrôle Blender 5.2 PASS. Dérivé externe
des deux arbres ensuite validé (géométrie/textures intactes, glTF, réimport),
5 tests du convertisseur PASS. A/B/B/A natif : BLEND 40,45/39,98 FPS, MASK
39,73/39,94 FPS, donc **aucun gain de cadence établi**. Capture basse inspectée,
pas d'avantage artistique décisif ; ne pas promouvoir ni rejouer ces essais
inchangés. Studio/manifeste/originaux intacts, tous les processus d'essai fermés.
Voir `docs/evidence/gate1/R1_FOLIAGE_ALPHA_COST.md`.

R1-025 clos comme capacité de mouillure réversible : preuves natives du
7 septembre recontrôlées (journal et trois PNG intègres), cycle Tauri du
8 septembre et tests partagés concordants. Pas de nouveau run natif, ni de
parité optique entre ces deux candidats distincts. Finition artistique des
reflets et qualification finale restent requises ; Gate 1 reste ouverte.
Voir l'actualisation de `R1_024_026_WEATHER_NATIVE.md`.

R1-035/036 terminés comme capacités de mesure : histogramme/moyenne/p95/1 % low
affichés et capturés dans le vrai Studio Tauri, export contrôlé ; ce ne sont pas
des seuils de performance satisfaits. Cycle météo du même candidat exécuté sans
rechargement : humidité 0 → 0,78 → 0 exacte, pluie puis retour sec visibles,
sources constantes et instance fermée. Soleil inchangé/ciel clair sous pluie
restent une limite du profil embarqué, pas une cohérence artistique acceptée.
Preuve `R1_TAURI_WEATHER_AND_METRICS.md`. Gate 1 reste ouverte.

Candidat Studio optimisé à jour assemblé et exécuté dans Tauri : WASM 41 126 864
octets, kit inchangé, sélection locale et cycle High → Low → High réellement
exercés, monde initialisé une seule fois. Captures et diagnostic conservés,
instance fermée. Preuve `R1_STUDIO_OPTIMIZED_CANDIDATE.md`. Profils partiels,
forêt toujours non qualifiée. Correction du remplacement d'éclairage ensuite
testée : 616 tests PASS / un timing ignoré, builds PASS et nouveau cycle Tauri
High → Low → High capturé sans coupure observée dans les images de transition.
Assombrissement initial encore présent ; capture discrète, pas garantie exhaustive.
Pointeur capturé/libéré, bascule clavier non démontrée. Nouveau candidat fermé,
zéro processus restant ; preuve détaillée dans le même rapport. Gate 1 ouverte.

Studio Tauri : candidat `r1-tauri-repro-20260908` ouvert et capturé directement
par l'agent via Computer Use. L'indisponibilité générale du pilotage natif annoncée
était erronée. PNG 1408 × 966 et diagnostic `tauri_studio` exportés et récupérés,
kit 20/20, 36 colliders, 650 arbres ; instance fermée, zéro processus restant.
Preuve `R1_STUDIO_REPRODUCIBLE_BUILD.md`. Lancement/rendu ponctuel acquis,
pas qualification des performances, parité exhaustive ou acceptation Gate 1.

R1-037 : deux acquisitions/relectures indépendantes Vulkan réussies sur les mêmes
onze entrées épinglées et arguments (hors sortie). Frames 305/304 : mêmes 1 641
draws et 3 065 instances ; 5 755 071/5 755 073 triangles soumis toutes passes.
Comparaison par passe : seul l'UI diffère de deux triangles, les dix-sept autres
passes ont les mêmes comptages. Répétition native acquise, pas identité des pixels.
Reçus hôte PASS, aucun runtime/QRenderDoc restant, aucun navigateur ouvert pour ce
lot. Ne pas refaire ces essais inchangés. Backend Studio, révision de qualification,
cible et validations humaines restent dus ; Gate 1 ouverte. Preuve
`R1_037_RECOVERED_TOOLING.md`. Aucun push ni passage Gate 2.

Clarification du protocole : le « reçu atomique » mentionné dans le suivi est une
solution technique envisagée, pas un critère supplémentaire de passage défini par
ADR-0004. L'obligation reste une capture externe reproductible pour chaque backend
qualifié, avec outil/version/backend/scène/frame et convention multi-draw explicites.
Ne pas transformer un choix d'outillage en chantier obligatoire supplémentaire.

Import d'assets — contexte complété le 8 septembre, preuves du 7 septembre :
le skill personnel Codex `$openrpg-import-assets` est documenté et testé avec
Blender **5.2.1 LTS** pour ce pack ; cela ne change pas la version du worker
gouverné R1-063. Entrée locale :
[SKILL.md](C:/Users/karen/.codex/skills/openrpg-import-assets/SKILL.md).
Dark Fantasy Weapons (Hivemind) : **17 GLB et fiches privées** avec descriptions
FR/EN, classement, hashes, origines/dimensions mesurées et **20 ancres proposées**.
[Pack normalisé](E:/OpenRPG_AssetVault/normalized/hivemind/dark-fantasy-weapons-v1/catalog.v1.json),
[index privé](E:/OpenRPG_AssetVault/metadata/asset-import-registry.v1.json) et
[rapport de test](E:/OpenRPG_AssetVault/metadata/hivemind/dark-fantasy-weapons.skill-test.md)
restent hors Git sur ce poste. Preuves : 17 tests synthétiques PASS, réimport sans
doublons/retouches conservées, validation glTF 17/17 sans erreur ni avertissement.
Licence Unity conservée, bibliothèque privée non redistribuable en l'état.
**Pas de raccordement au navigateur Studio Rust ni de qualification des prises,
orientations, collisions, LOD ou du runtime** ; `full-import` reste refusé.
Les helpers v1 automatisent les GLB statiques autonomes ; les autres profils
documentés nécessitent leurs adaptateurs. Aucune qualification de gate ajoutée.

R1-037 : relecture isolée de la capture 305 tentée sans runtime ; arrêt contrôlé
après 12,476 s par garde-fou, mémoire virtuelle libre système sous 2 Gio. Pas de
comptage GPU ni de processus encore actif. Capture conservée, ne pas la refaire.
28 tests Python, trois refus d'entrée et régression SDK PASS ; preuve
`R1_037_RECOVERED_TOOLING.md`. Relecture à reprendre avec davantage de mémoire
disponible, sans modification automatique des applications/pagination Windows.

R1-037 : première capture Vulkan réelle reçue, frame 305, 848 464 512 octets,
hash conservé dans `r1-renderdoc-capture-20260908-d/capture-pin.json` externe.
La relecture immédiate a crashé (sortie -1073740791), aucun comptage GPU obtenu.
28 tests Python synthétiques PASS ; acquisition/relecture désormais séparées,
séparation non rejouée. Aucun processus restant au contrôle final. Preuve
`R1_037_RECOVERED_TOOLING.md` ; Gate 1 reste ouverte, aucune qualification ajoutée.

R1-037 : smoke RenderDoc embarqué PASS après diagnostic de l'import unittest
incompatible (`_overlapped` absent). Analyseur/replay importés, fixture nominale
synthétique vérifiée, API présentes ; 25 tests séparés PASS sous Python normal.
Aucune capture/injection/relecture GPU effectuée, processus terminés ; preuve
`R1_037_RECOVERED_TOOLING.md`. Gate 1 reste ouverte.

Capture figée : disponibilité CPU/horloges désormais combinée à 30 observations
distinctes de pipelines prêts ; capture ordinaire conservée à huit. 615 tests
workspace PASS / un timing ignoré, Clippy/check WASM/build PASS. Rejeu natif :
capture frame 349, 113 pipelines prêts, seuil 30 effectivement atteint, sortie 0.
Pas de `.rdc`, de reçu atomique ni qualification artistique ; preuve
`R1_NATIVE_FIXED_PROOF_VIEW.md`, Gate 1 ouverte.

Stabilité de la vue figée observée en continu : 614 tests workspace PASS / un timing
ignoré, Clippy/check WASM/build PASS. Collecte post-run initialement en échec, déplacée
pendant l'exécution ; rejeu natif avec 30 observations CPU stables, horloges virtuelle
et fixe à zéro, aucune invalidation ultérieure. Pas de reçu GPU ni capture RenderDoc
déduit de ce signal ; preuve `R1_NATIVE_FIXED_PROOF_VIEW.md`, Gate 1 toujours ouverte.

Préparation capture native figée raccordée : option `--fixed-proof-view`, même
plugin de caméra/gel que Studio, refus des workloads incompatibles. 76 tests
runtime, Clippy strict et build PASS ; PNG réel 1080p inspecté, sortie 0, caméra
inconnue refusée avant lancement. Pas de reçu RenderDoc ni comptage GPU acquis ;
preuve `R1_NATIVE_FIXED_PROOF_VIEW.md`, Gate 1 ouverte.

R1-037 : frontière d'extraction replay intégrée à l'analyseur, API RenderDoc
injectée et 25 tests synthétiques PASS (18 modèle + sept replay/erreurs/libération).
Acquisition `.rdc` réelle et reçu de readiness/gel du candidat toujours à raccorder ;
aucun comptage GPU, validation Web ou passage de gate déduit de ces tests.
Voir `R1_037_RECOVERED_TOOLING.md`.

R1-037 : analyseur Python retrouvé maintenant intégré avec ses fixtures ; 18 tests
PASS après reproduction/correction d'un parent Drawcall ambigu susceptible d'être
omis du total. CLI nominal et refus exercés sur données synthétiques uniquement.
Ni capture GPU ni compteur global livré ; extraction/readiness encore à raccorder.
Preuve `R1_037_RECOVERED_TOOLING.md`, aucun changement de statut de gate.

R1-037 : outillage RenderDoc 1.46 retrouvé sur E: ; aide CLI exécutée et deux
hashes cœur vérifiés. Ancien analyseur/protocole retrouvés dans une copie de preuve,
mais captures historiquement NOT RUN et adapter incompatible avec les types actifs.
Ne pas refaire l'acquisition ni lancer ce harness inchangé ; reprise ciblée requise.
Voir `R1_037_RECOVERED_TOOLING.md`, aucun comptage GPU ou passage acquis.

Build Tauri local rendu rejouable par `scripts/Build-StudioCandidate.ps1` :
14 fichiers du bundle Web identifié copiés/hash-vérifiés, trois refus d'entrée
testés et compilation offline/locked PASS en 28 s. Nouveau candidat conservé
hors Git, non lancé : ni qualification WebView2 ni nouveau frontend revendiqués.
Voir `R1_STUDIO_REPRODUCIBLE_BUILD.md` ; Gate 1 reste ouverte.

Priorité précisée après le retour propriétaire sur le ciel : conserver son statut
provisoire et son chantier réutilisable, sans poursuivre maintenant les essais de
finition nuageuse. L'essai 128³ annoncé n'a pas été implémenté ; l'aperçu reste
64³. Ce report ne valide pas R1-019 et ne dispense pas de cohérence ciel/soleil
au verdict de Gate 1. Reprendre les manques du candidat complet, sans répéter
les preuves inchangées. La correction Tauri `ERR_FILE_NOT_FOUND` et la confirmation
utilisateur de l'affichage v2 existent déjà dans `R1_WEB_OPTICAL_SAMPLE_PROTOCOL.md` ;
ne pas confondre les qualifications WebView2 encore dues avec un démarrage non corrigé.

Répartition nuageuse sans grille implémentée dans l'aperçu opt-in : 64 centres et
rayons seedés calculés une fois, budgets GPU conservés. Capture native inspectée :
espacements plus variés et ciel plus dégagé, contours encore flous ; pas de réalisme
accepté. Quatre tests volume/Clippy/build, 613 tests workspace et check WASM PASS.
Pas de nouveau bundle Web, de mesure cible ou de passage de gate ; preuve
`R1_CLOUD_LAYER_PREVIEW.md`. Les paragraphes suivants retracent les essais antérieurs.

Travail du 8 septembre sur la morphologie nuageuse : volumes cellulaires dans
l'aperçu opt-in, quatre tests volume/Clippy/build natif et 613 tests workspace PASS.
Capture inspectée : masses séparées mais motif trop régulier, **pas un ciel naturel
accepté**. Code expérimental encore à corriger, aucun bundle Web remplacé ni profil
promu. Budget High 64³/24 pas conservé ; preuve `R1_CLOUD_LAYER_PREVIEW.md`.

Complément du 8 septembre : essai d'éclairage directionnel des nuages non retenu
après paire native inspectée ; changement lumineux sans gain convaincant de
réalisme. Shader antérieur restauré, aucune promotion Web. Build et 612 tests
workspace PASS pour l'essai ; forme nuageuse toujours à corriger, Gate 1 ouverte.
Voir `R1_CLOUD_LAYER_PREVIEW.md` ; ne pas rejouer ce réglage inchangé.

Cadrage groupé du kit corrigé : caméra calculée sur les bounds réels, arbres
entiers dans la planche inspectée. Trois formats contrôlés en plus des 19 cas
individuels ; sources inchangées, pas de modèle/runtime remplacé. Preuve
`R1_ASSET_KIT_FRAMING.md`. Qualification artistique et Gate 1 toujours ouvertes.

Reprise Blender complète après seconde retouche synthétique PASS : deux builds,
géométrie/UV/matrices conservées, six GLB et 33 textures identiques, 49 rendus dans
le contrat existant. Test corrigé sur la provenance d'une base déjà authorée,
cas positif puis six refus ; worker inchangé. Planche groupée encore tronquée en
haut, aucun candidat runtime remplacé ni revue humaine acquise. Preuve R1-063.

Blender 4.5.10 disponible en copie portable officielle isolée sur E:, sans toucher
Blender 5.2. Archive et fichiers extraits hash-vérifiés ; réimport des deux arbres
rev.4 et quatre refus de sources PASS. Blocage de disponibilité du worker levé,
pas de reconstruction complète, retouche humaine ou acceptation artistique acquise.
Script reproductible `scripts/Prepare-Blender45.ps1`, preuve `R1_063_GOVERNED_ASSET_KIT.md`.

Diagnostic du contrat de fondu clos sans modification : racines/marges cohérentes
dans le code courant, partition discrète vérifiée sur 272 cas. Cela ne qualifie
pas les silhouettes ni le GPU ; pas de nouvelle réécriture/capture identique
justifiée. Voir `R1_FOREST_FAR_PATH_DIAGNOSIS.md` pour la portée et les réserves.

Trace vidéo enrichie de projection caméra : 9/25 racines proches/lointaines entrent
dans l'image pendant leur bande LOD. Deux arbres localisés, six images inspectées ;
présence de silhouettes observée, pas de continuité fine ou de fondu GPU qualifiés.
Build/Clippy et collecte/analyse PASS ; `R1_048_BRIDGE_MOVEMENT_VIDEO.md`.

Recorder enrichi de traces caméra/racines LOD, uniquement hors temps réel. Build,
Clippy qualification et 3 tests recorder PASS ; nouvelle vidéo 271 images et
audit : 20 arbres couvrent la bande 64–72 m, 40 la bande 172–180 m. Distances
logiques uniquement, visibilité/transition GPU non qualifiées. Preuve
`R1_048_BRIDGE_MOVEMENT_VIDEO.md`, aucun statut de gate promu.

Cycle Web High → Low → High du candidat rev.4 vérifié à caméra fixe : 650 arbres,
36 colliders kit prêts et sources conservés ; comptes de géométrie/herbe High
restaurés, captures inspectées. Pas de continuité LOD en mouvement ni de performance
qualifiée, blocker forêt maintenu ; `R1_FOREST_QUALITY_BINDINGS.md`. Aucun code changé.

Studio Web actualisé et lancé sur loopback 1460 : Clippy/Trunk/assemblage PASS,
deux captures et diagnostics reçus/hash-vérifiés. Vue du pont inspectée : route
en terre continue, kit 20/20 et 36 colliders prêts, 650 arbres. Pas de cycle qualité,
de traversée Web ou Tauri qualifiés ; ciel toujours non accepté. Preuve et pins :
`docs/evidence/gate1/R1_ROAD_SURFACE.md`. Aucun passage de gate.

Essai d'isolation de la couture de berge : supprimer sa propre ombre portée ne
supprime pas le trait visible. Essai natif capturé puis changement retiré, source
restaurée/hash identique ; aucune qualification ajoutée. Voir `R1_ROAD_SURFACE.md`.

Routes/berges : le masque de route est désormais partagé avec la couche des berges,
sans toucher la géométrie ou les sources. Clippy/check frontend WASM/build natif
PASS, workspace **612 PASS / 0 FAIL / 1 timing ignoré**. Nouvelle traversée filmée,
271 images ; départ/arrivée comparés : bande verte supprimée, fine couture encore
visible à l'entrée du pont. Web/Tauri non rejoués, art non qualifié ; preuve
`docs/evidence/gate1/R1_ROAD_SURFACE.md`. Gate 1 reste ouverte.

Vidéo native du candidat forêt regroupée rev.4 acquise : collecteur étendu/testé
(3 tests PASS), runtime reconstruit, 271 images Full HD/9,03 s hors temps réel.
650 arbres/508 lots lointains, traversée 42,53 m et 518/518 ticks au sol. Trois images
inspectées, pas chaque seuil LOD ni fluidité/vent/art qualifiés. Pas de nouveau
total Rust ; `docs/evidence/gate1/R1_048_BRIDGE_MOVEMENT_VIDEO.md`.

Diagnostic forêt précisé sur les GLB déployés/hash-vérifiés : LOD0/1/2 déjà réduits
(chêne 34 158/8 152/662 triangles, pin 46 508/7 522/1 228). Le regroupement seul
n'ajoute pas de réduction ; priorité aux transitions/silhouettes/vent et au coût
du candidat, pas à une nouvelle simplification présumée nécessaire. Contrôle de
comptages uniquement, aucune qualification graphique acquise ; voir
`docs/evidence/gate1/R1_FOREST_FAR_PATH_DIAGNOSIS.md`.

Même invalidation corrigée pour la végétation : remplacement du profil à niveau
constant appliqué, puis reprise d'un profil corrigé après échec au même niveau.
Régression reproduite et scénario ECS étendu PASS ; sources et collisions des arbres
conservées. Clippy/check frontend WASM PASS, workspace rejoué **611 PASS / 0 FAIL /
1 timing ignoré**. Pas de nouvelle qualification visuelle ; voir
`docs/evidence/gate1/R1_QUALITY_TRANSITION_CPU.md`.

Invalidation terrain corrigée lors du remplacement du profil à niveau constant :
échec reproduit puis 15 tests ciblés PASS, sources/entités/handles conservés sur les
chemins synchrone et détaché. Clippy strict et check frontend WASM PASS ; suite
workspace terminée : **611 PASS, 0 FAIL, 1 timing ignoré**. Aucun nouveau bundle ou
verdict GPU/artistique. Preuve : `docs/evidence/gate1/R1_QUALITY_TRANSITION_CPU.md`.

Les observations suivantes précèdent ce correctif de cache.

Studio actualisé sur port local 1459 : Clippy Web/Trunk/assemblage PASS, PNG et JSON
reçus et vérifiés. Capture inspectée, forêt 650 arbres/kit 20/20/36 colliders prêts,
nuages corrigés présents mais toujours flous. Web synchrone, forêt et Tauri non
qualifiés ; voir `docs/evidence/gate1/R1_CLOUD_LAYER_PREVIEW.md`.

Validation globale après reprise des prototypes et correction des nuages :
`cargo test --workspace --locked --offline -q` terminé, **610 PASS / 0 FAIL /
1 timing ignoré** ; fmt et diff PASS. Ce total remplace les 609 tests précédents
pour l'état courant, sans promotion de capacité graphique ni de gate.

Nuages expérimentaux : échelle verticale du bruit alignée sur l'épaisseur physique,
sans hausse de texture/pas. Six tests, Clippy/build/check WASM PASS ; paire native
1080p inspectée : moins de strates, mais ciel toujours flou et non accepté. Aucun
gain FPS ou résultat visuel Web revendiqué. Voir `docs/evidence/gate1/R1_CLOUD_LAYER_PREVIEW.md`.

Forêt : un cache corrigé permet désormais une reprise après échec ; cache inchangé,
aucun nouvel essai automatique. 12 tests projecteur et Clippy strict PASS, reprise
synthétique sans doublon de collider. Réimport Blender et cycle GPU non qualifiés
par ce correctif ; voir `docs/evidence/gate1/R1_FOREST_QUALITY_BINDINGS.md`.

Réimport Blender : précontrôle installé 5.2.1 LTS, worker/précédentes preuves 4.5.x,
ancien exécutable documenté absent au chemin vérifié. Reprise de cette chaîne
en attente du choix restauration 4.5 ou qualification isolée 5.2 ; aucune source
reconstruite ou preuve ancienne promue. Voir `docs/evidence/gate1/R1_063_GOVERNED_ASSET_KIT.md`.

Projecteur forêt : remplacement des références de prototypes désormais pris en
compte à scatter/profil constants, avec maintien des entités et poses. 11 tests
ciblés, Clippy et check WASM PASS ; suite workspace ensuite terminée : 609 PASS,
0 FAIL, 1 ignoré. Pas de hot-reload Blender/GLB ou de transition
GPU qualifiés par ce test synthétique. Voir `docs/evidence/gate1/R1_FOREST_QUALITY_BINDINGS.md`.

Smoke natif du nouveau binaire avec forêt candidate regroupée : chargement conjoint
vérifié, 650 arbres, kit 20/20 et 36 colliders prêts, terrain High/20 chunks protégés.
Capture inspectée mais fenêtre redimensionnée : pas de comparaison de performance
1080p. Qualification forêt/HLOD toujours ouverte, garde-fou conservé.

Terrain natif : préparation détachée implémentée, 14 tests ciblés et Clippy strict
PASS. Une nouvelle berge au même niveau conserve l'ancienne géométrie visible sans
la qualifier ; vérifié en ECS jusqu'au résultat courant. Workspace 608 PASS / 1 ignoré,
check WASM/build PASS ; cycle natif cinq étapes GPU acquittées et deux captures
inspectées. Appel terrain final 4,60–5,98 ms, calcul worker 461–1017 ms en transition.
Saccades restantes, forêt expérimentale exclue, Web synchrone ; Gate 1 ouverte.
Voir `docs/evidence/gate1/R1_QUALITY_TRANSITION_CPU.md`.

Transitions végétales préparées en arrière-plan natif, ancienne projection conservée,
résultats périmés rejetés. 605 tests PASS / 1 ignoré, Clippy/check WASM/build PASS ;
cycle natif cinq étapes GPU acquittées, Low/retour High inspectés. Appel final principal
58–77 ms observé, terrain et Web encore synchrones : fluidité globale non qualifiée.
Essai hors forêt expérimentale. Voir `docs/evidence/gate1/R1_QUALITY_TRANSITION_CPU.md`.

Raccord GPU nuageux maintenant exercé en natif sans forêt expérimentale : cycle
High/Low/Medium/Ultra/High, cinq captures 1080p inspectées et acquittements GPU acquis.
Nuages masqués/restaurés, sources inchangées ; preset complet false, transitions
saccadées, aucun verdict artistique/Web/Tauri. Premier essai au journal filtré conservé.
Voir `docs/evidence/gate1/R1_CLOUD_LAYER_PREVIEW.md` ; Gate 1 reste ouverte.

L'inventaire GPU reconnaît maintenant le matériau nuageux et sa densité 3D, auparavant
comptés comme matériau inconnu. Remplacement/perte de ressources testés sans faux
acquittement : 16 tests ciblés, Clippy strict et workspace 602 PASS / 1 ignoré.
Pas de nouveau rendu ni bundle Web ; forêt toujours non qualifiée. Voir
`docs/evidence/gate1/R1_CLOUD_LAYER_PREVIEW.md`.

Cycle global natif incompatible avec la forêt expérimentale : refus CLI immédiat
ajouté après un essai expiré à 120 s. 74 tests runtime, Clippy et build PASS ;
refus réel en phase arguments vérifié, sans création de sorties. Aucun rendu,
profil ou statut de qualification modifié. Voir `docs/evidence/gate1/R1_CLOUD_LAYER_PREVIEW.md`.

Budget de nuages provisoire raccordé au niveau demandé : Low masqué, Medium 12,
High 24, Ultra 32 étapes ; réglages et texture conservés. Cycle ECS testé,
Clippy/build PASS, smoke High capturé. Suite globale 600 PASS, 0 FAIL, 1 ignoré ;
Clippy/build Web terminés. Capture Low inspectée sans nuages ; exports suivants
non acquis, donc cycle visuel des niveaux incomplet et aucun preset complet qualifié. Voir
`docs/evidence/gate1/R1_CLOUD_LAYER_PREVIEW.md`.

Raccordement IBL des nuages confirmé dans Chrome par cycle 1→0→1 à caméra/temps
fixes : trois PNG/JSON inspectés, assombrissement et retour visuel observés,
aucune erreur console. Preuve qualitative ciblée, pas photométrie/performance
ni acceptation artistique ; ne pas répéter ce contrôle inchangé. Voir
`docs/evidence/gate1/R1_CLOUD_LAYER_PREVIEW.md`.

Éclairage indirect des nuages raccordé à la cubemap de la vue, shader natif
capturé après correction du cas sans ENVIRONMENT_MAP. Ombres moins jaunes,
118 pipelines prêts, aucune erreur ; rendu toujours expérimental. Ce dernier
shader n'est pas encore vérifié dans Chrome et son smoke dev ne qualifie pas les
performances. Voir `docs/evidence/gate1/R1_CLOUD_LAYER_PREVIEW.md`.

Volume nuageux désormais chargé et capturé dans Chrome/WebGPU : bundle dev
reconstruit, Clippy Web PASS, kit rev.4 complet, diagnostic 3D aligné au natif,
PNG 2048×1157 inspecté, aucune erreur console relevée. Pas de performance Web,
Tauri ou acceptation artistique déduite ; aperçu explicite. Voir
`docs/evidence/gate1/R1_CLOUD_LAYER_PREVIEW.md`.

Morphologie nuageuse affinée par génération CPU, texture/shader GPU inchangés.
Six tests ciblés, Clippy et build PASS ; capture native inspectée : masses plus
fragmentées, couleur encore uniforme. Conservé dans l'aperçu uniquement ; aucun
nouveau PASS artistique, release ou Web/Tauri. Voir
`docs/evidence/gate1/R1_CLOUD_LAYER_PREVIEW.md`.

Nuages 3D maintenant raccordés à l'aperçu natif explicite : R8 64³, parcours borné
à 24 étapes, diagnostic et capture 1080p vérifiés. Pas de strates visibles sur la
vue, mais aspect toujours trop lisse/uniforme : conservé expérimental, pas accepté.
Deux tests moteur/Clippy/build PASS ; workspace 600 PASS, 0 FAIL, 1 timing ignoré. Aucun coût release,
profil complet ou Web/Tauri qualifié. Voir `docs/evidence/gate1/R1_CLOUD_LAYER_PREVIEW.md`.

Morphologie nuageuse 3D préparée dans forge_world, sans raccordement au rendu :
champ déterministe, périodique horizontalement, continu en hauteur, borné à 128³
octets. 118 tests forge_world PASS et Clippy strict PASS. Ancien aperçu inchangé ;
aucune amélioration visible revendiquée avant intégration/capture. Voir
`docs/evidence/gate1/R1_CLOUD_LAYER_PREVIEW.md`.

Essai de relief des nuages par normale de surface lancé et inspecté, mais rejeté :
formes toujours plates. Code restauré aux hashes du candidat release, aucune
lecture GPU supplémentaire conservée. Prochaine correction sur la morphologie,
pas une nouvelle série d'ajustements de lumière. Le cache debug contient encore
l'essai rejeté ; utiliser le release immuable ou reconstruire avant lancement.
Voir `docs/evidence/gate1/R1_CLOUD_LAYER_PREVIEW.md`.

Release native compilée, sources épinglées inchangées. Smoke complet inspecté puis
trois mesures de 5999 frames sans capture : 69,711–71,055 FPS moyens, p95
15,256–15,902 ms, 1 % low 57,076–58,607 FPS sur RX 7800 XT en 1080p.
Seuils locaux atteints ; GPU cibles, preset complet, Web/Tauri et art non qualifiés.
Ne plus prolonger les micro-optimisations sur la seule base des anciennes mesures dev.
Voir `docs/evidence/gate1/R1_RELEASE_PREPARATION.md` ; Gate 1 reste ouverte.

Conifère en rameaux chargé en natif, révision candidate 4 : dimensions issues du
réimport, vingt poses conservées et cinq autres GLB identiques. Kit complet,
650 arbres, capture 1080p inspectée : rameaux mieux distincts mais sombre/régulier.
Smoke non qualifiant, aucune promotion ; voir `docs/evidence/gate1/R1_CONIFER_SPRAYS.md`.

Conifère : profil dérivé de rameaux foliaires généré/réimporté, sites communs
aux LOD, bois/textures conservés et chêne identique. Deux vues Blender inspectées :
rameaux plus lisibles, grandes cartes lointaines encore visibles. Pas intégré,
pas de réalisme/transition qualifié. Voir `docs/evidence/gate1/R1_CONIFER_SPRAYS.md`.

Étude conifère : surface des cartes divisée par deux dans un dérivé Blender,
génération/réimport PASS, chêne inchangé. Image plus ajourée mais silhouette
toujours artificielle : modification seule non retenue pour promotion, aucun
benchmark/runtime répété. Reprendre structure de couronne/amas avant test intégré.
Voir `docs/evidence/gate1/R1_CONIFER_COVERAGE_STUDY.md` ; Gate 1 reste ouverte.

Dérivé bois chargé dans Chrome avec le bundle inchangé. Premier assemblage refusé
pour mauvaise liaison de caméras, préparation corrigée sans affaiblir le contrôle.
Kit prêt, deux PNG inspectés, cinq compteurs restaurés High/Low/High ; viewport
redimensionné, donc comparaison visuelle partielle. Sources conservées, pas de
qualification Tauri/performance/art. Voir `docs/evidence/gate1/R1_WOOD_LOD_DERIVATIVE.md`.

Dérivé du bois chargé en natif, révision candidate 3 : kit complet, 650 arbres,
capture inspectée. Comparaison ABBA partiellement invalide (A1 redimensionné) ;
seule paire 1080p restante favorable de 4,42 % en FPS moyens, insuffisante pour
qualifier la performance. Garde de résolution ajoutée au lanceur de preuve.
Sources et poses conservées, aucun passage de gate ; voir
`docs/evidence/gate1/R1_WOOD_LOD_DERIVATIVE.md`.

Dérivé du bois distant généré et réimporté sous Blender : total LOD2 réduit de
34,6 % (chêne) et 47,9 % (pin), sans changement vérifié du LOD0, du feuillage ou
des collisions. Sources intactes, quatre vues comparatives inspectées. Non promu,
pas encore chargé/mesuré dans Forge ; aucun gain FPS ou passage de gate déduit.
Voir `docs/evidence/gate1/R1_WOOD_LOD_DERIVATIVE.md`.

Coût des variantes mesuré sur deux paires natives : catalogue mixte 3,59–6,09 %
de FPS moyens en moins et ~84,2 Mio d'allocations backend supplémentaires. Même
binaire/scène/profil, sans capture ; poste non isolé, pas de qualification cible.
Support générique conservé, catalogue mixte non promu comme candidat final ;
géométrie des dérivés et conifère sombre restent à travailler. Voir
`docs/evidence/gate1/R1_FOREST_VARIANT_SELECTION.md`.

Variantes v2 vérifiées dans Chrome : Clippy Web/Trunk PASS, deux captures inspectées,
cycle High/Low/High avec six compteurs de meshes restaurés et sources/temps conservés.
Kit prêt ; aspect sombre du conifère, coût comparatif, fluidité et Tauri non qualifiés.
Voir `docs/evidence/gate1/R1_FOREST_VARIANT_SELECTION.md`. Aucune gate promue.

Variantes de forêt raccordées aux caches et trois LOD : test ECS de stabilité
High/Low/High, 597 tests workspace PASS / 1 ignored et Clippy workspace strict PASS.
Candidat natif chêne/conifère lancé,
capture inspectée : diversité visible, conifère proche encore sombre. 650 arbres,
7 800 primitives LOD, pas d'acceptation artistique ou de coût comparatif qualifié.
Ancien catalogue conservé ; candidat v2 séparé. Rejeu Web de la v2 encore dû.
Voir `docs/evidence/gate1/R1_FOREST_VARIANT_SELECTION.md`.

Forêt : sélection déterministe de variantes gouvernées préparée dans un catalogue
v2 explicite, v1 inchangée. Cinq tests ciblés PASS (ordre, réimport, ajout/retrait,
refus). À cette étape initiale, le renderer refusait v2 en attendant le raccord
caches/LOD décrit ci-dessus ; le contrat seul ne prouvait aucun rendu. Voir
`docs/evidence/gate1/R1_FOREST_VARIANT_SELECTION.md`.

Validation après cette préparation : 596 tests workspace PASS / 0 FAIL / 1 timing
déjà ignoré, Clippy workspace strict et contrôles format/diff PASS. Aucune preuve
de rendu multivariantes déduite de ces tests.

Essai nuageux à huit étapes rejeté après capture native : strates visibles. Variante
retirée du code actif ; aperçu précédent conservé, aucun alourdissement du renderer
retenu pour cet essai. Échec et binaire locaux conservés dans la preuve ciel. Réalisme
toujours ouvert ; ne pas confondre cette expérience avec une amélioration livrée.

Ciel expérimental raccordé au Studio par option explicite, désactivée par défaut.
41 tests Studio, Clippy Web strict et Trunk PASS. Premier rendu Chrome capturé et
inspecté, diagnostic exporté relu : hash de réglages identique au natif, kit prêt.
Pas de réalisme final, d'animation GPU ou de performance cible qualifiés ; profils
persistants inchangés. Voir `docs/evidence/gate1/R1_CLOUD_LAYER_PREVIEW.md`.

Diagnostic de l'aperçu nuageux raccordé aux métriques natives et relu après lancement
réel : paramètres/hash, temps et UV, visibilité CPU et limites explicites. Deux tests
ciblés enrichis et Clippy moteur strict PASS ; aucune preuve GPU déduite du JSON.
Ce lot natif seul n'établissait ni qualification artistique ni intégration Studio/profils.
Voir `docs/evidence/gate1/R1_CLOUD_LAYER_PREVIEW.md`.

Aperçu nuageux affiné : neuf mips R8 (87 381 octets), détail d'épaisseur et fondu
d'horizon. Deux tests ciblés et Clippy moteur strict PASS ; capture native inspectée,
horizon moins rayé et épaisseur moins uniforme, ciel encore stylisé/non accepté.
Pas de qualification animation, Web ou performance cible. Détails dans
`docs/evidence/gate1/R1_CLOUD_LAYER_PREVIEW.md`.

Aperçu nuageux natif désormais lançable par `--cloud-layer-preview settings.json`.
592 tests workspace PASS / 1 ignored avant correction du tri transparent ; test moteur
ciblé et Clippy strict ensuite PASS. Deux captures natives inspectées : recouvrement
du feuillage corrigé, nuages encore plats/répétitifs, pas de ciel final accepté.
Aperçu désactivé par défaut, profils v1 inchangés, shader Web non rejoué. Voir
`docs/evidence/gate1/R1_CLOUD_LAYER_PREVIEW.md`.

Ciel : contrat `CloudLayerSettings` (seed, couverture, altitude, échelle, opacité,
vent périodique borné), 63 tests projet PASS lors de sa préparation. Aucun changement
au profil environnement v1. Voir `docs/evidence/gate1/R1_CLOUD_LAYER_PREPARATION.md`
pour cette étape antérieure à l'aperçu natif décrit ci-dessus.

Champ de densité nuageuse désormais généré : périodique, seed déterministe,
résolution 32–512 et 256 Kio maximum. Trois tests monde ciblés et Clippy strict PASS.
Ce champ alimente désormais l'aperçu natif ; son intégration aux profils reste à faire.

Chemins : le masque de route compilé alimente maintenant les poids visuels terre,
sans modifier hauteurs, collisions ou sources de scatter. Raccordement natif/Studio,
584 tests workspace PASS / 1 ignored, Clippy strict natif PASS. Capture native
inspectée : chemin visible jusqu'au pont, palette encore orangée ; pas d'acceptation
artistique ou performance cible. Voir `docs/evidence/gate1/R1_ROAD_SURFACE.md`.

Complément routes : build Trunk PASS et capture Chrome inspectée, kit 20/20,
36 colliders prêts, aucune erreur console observée. Retour propriétaire du 7 septembre :
ciel pas assez réaliste. Ciel/horizon/nuages et cohérence avec l'éclairage restent
à corriger et à faire accepter ; aucune validation humaine de Gate 1 déduite.

Diagnostic régional du premier plan exécuté sur les documents candidats : 13 centres
d'herbe / 400 m², 32 sans exclusions, toujours 13 sans filtre matériau. Exemple
réutilisable en lecture seule testé et Clippy strict PASS. Pas de nouvelle correction
visuelle ; priorité aux corridors lisibles au sol et à la couverture adjacente,
sans supprimer les exclusions. Détails dans `R1_AUTHORED_CLEARINGS.md`.

Correction des dégagements WorldSketch : plus de corridor/clairière de fixture
hérités ; cercle dérivé du landmark PlayerSpawn et de son rayon authoré, routes
inchangées. Test de déplacement PASS. 581 tests workspace PASS / 1 ignored avant
extraction du helper, puis 109 tests monde PASS et Clippy strict natif/Web + Trunk PASS.
CLI et capture native vérifiés : 650 arbres, 19 395 touffes, kit complet ; exécutable
de preuve conservé séparément. Capture Web et diagnostic archivés : mêmes comptages,
kit 20/20 et 36 colliders prêts, aucune erreur console observée.
Pas d'acceptation artistique ou de performance cible.
Voir `docs/evidence/gate1/R1_AUTHORED_CLEARINGS.md`.

Prairie : recette à huit tentatives/cellule, High/Ultra à 32 brins/touffe, plafonds
inchangés. 19 138 touffes contre 9 606, anciens placements conservés ; en High,
612 416 brins contre 614 784. 580 tests workspace PASS / 1 timing ignoré, Clippy natif/Web
strict et build Web PASS. Capture Chrome et diagnostic cohérents, kit prêt, console error
vide. Capture native inspectée : plus de touffes, encore beaucoup de sol nu ;
smoke sous les seuils Gate 1, pas d'acceptation artistique. Catalogue candidat associé
au nouveau hash après refus explicite de l'ancien. Voir
`docs/evidence/gate1/R1_MEADOW_COVERAGE.md`.

Microrelief de sol ajouté au shader partagé, sans nouvelle lecture texture, mesh ou
modification des sources de carte. Premier essai remplacé après quadrillage visible ;
interpolation adoucie, deux captures natives sec/pluie inspectées, dix tests terrain
et Clippy strict moteur PASS. Build Web PASS et capture Chrome inspectée, kit 20/20,
36 colliders prêts, console error vide. Motif encore procédural et couverture herbeuse inchangée ;
pas d'acceptation artistique ou de performance cible. Voir
`docs/evidence/gate1/R1_TERRAIN_MICRORELIEF.md`.

Adaptation Web hors forêt expérimentale : Low puis Ultra acquittés et tracking
atteint après focus du viewport ; zéro proposition automatique, pas de baisse
qualifiée. Garde-fou fenêtre masquée ajouté après inspection de l'éligibilité,
579 tests workspace PASS / 1 timing ignoré, Clippy strict et build Web PASS ;
rejeu Chrome focus/pause/reprise vérifié. Masquage réel non exercé et deux refus
de capture du pointeur pendant l'automatisation : pas de qualification interactive
ni de baisse automatique. Voir
`docs/evidence/gate1/R1_ADAPTIVE_WINDOW_VISIBILITY.md`.

Réutilisation des lots lointains implémentée : géométrie conservée si entrées
exactes inchangées, ranges et marges recalculées ; tests de cycle et invalidation
PASS. Comparaison CPU synthétique 29,89–48,09 ms reconstruits / 4,75–7,95 ms réutilisés,
pas de gain FPS déduit. 578 tests workspace PASS / 1 timing ignoré, Clippy strict
natif/Web et build Web PASS ; cycle Chrome High/Low/High avec kit physique prêt,
sources constantes et six compteurs restaurés, deux PNG inspectés. Pas de fluidité
globale ou d'identité GPU qualifiée. Voir
`docs/evidence/gate1/R1_FAR_BATCH_REUSE.md`.

Contrôle borné du vent lointain terminé, **non concluant** : série 0/2/0 s en vue
serrée, changement RGB 2,46 pour bruit 1,07, filtre existant false. Initialisation
physique différente à temps gelé (0/36/0 colliders enregistrés) : pas de promotion
ni de rejeu identique. Détails dans `docs/evidence/gate1/R1_FAR_WIND_ISOLATION.md`.

Conflit caméra/horloge reproduit puis corrigé : le suivi joueur ne remplace plus
la vue fixe lors des pas de 250 ms. 17 tests ciblés PASS, dont régression avec
Rapier et reprise du suivi ordinaire. 575 tests workspace PASS / 1 timing ignoré,
Clippy strict natif/Web et build Web PASS ; rejeu Chrome 0 → 250 → 500 ms sans
refus, deux PNG inspectés sans saut de caméra. Vent lointain et gate non qualifiés.
Voir `docs/evidence/gate1/R1_FIXED_CAMERA_CLOCK_REGRESSION.md`.

Smoke Web animé du mode regroupé, sans gel du temps : trois PNG inspectés, herbe
mobile, progression des frames et sources constantes, 36 colliders du kit prêts.
Le vent des arbres lointains n'est pas isolé par ces images et reste non qualifié.
L'avertissement natif `@invariant` est retrouvé dans un log antérieur au regroupement ;
aucun fork Bevy entrepris sur ce seul signal. Détails dans les deux rapports lointains.

Comparaison native lointaine ajoutée ensuite, même binaire/caméra, deux ordres :
individuel 62,91/63,18 FPS, lots 67,04/65,91 FPS ; environ 129 Mio supplémentaires
dans l'allocateur GPU. Le p95 regroupé 16,40/17,07 ms ne tient pas toujours le seuil.
RX 7800 XT seulement, pas GPU cible. Capture native séparée inspectée, test CLI et
Clippy runtime strict PASS ; aucun nouveau total workspace déduit. Détails et réserves :
`docs/evidence/gate1/R1_NATIVE_FAR_BATCH_COST.md`. Mode regroupé toujours expérimental.

**Dernier lot lointain : 573 tests workspace PASS / 1 timing ignoré**, Clippy strict
natif/Web et build Web PASS. Regroupement expérimental par chunk/matériau raccordé,
opt-in `forge_forest=batches` avec kit gouverné. Deux chemins du même bundle exécutés
dans Chrome, captures inspectées, logs sans erreur, sources/triangles constants.
10 026 → 7 738 entités mesh, mais triangles uniques 1 907 294 → 2 549 914 : aucun
gain de performance déduit. Le regroupement ne simplifie pas la géométrie ; transitions,
vent animé, coût mémoire/GPU et qualification native restent dus. Blocker végétation
conservé. Voir `docs/evidence/gate1/R1_FOREST_FAR_PATH_DIAGNOSIS.md`.

Cycle Web suivant High/Low/High du même mode regroupé : retour exact aux six
compteurs live initiaux, sources constantes et aucune erreur navigateur observée.
Reconstruction fonctionnelle vérifiée, pas durée/ fluidité de transition, vent animé
ou absence de fuite mémoire. Aucun nouveau passage de tests ou build déduit de ce cycle.

Historique du **lot shader lointain en cours, non qualifié** : spécialisation des attributs racine/vent
et chemin WGSL conditionnel ajoutés après les 572 tests ci-dessous. `cargo check -p
forge_engine` et Clippy strict engine all-targets/all-features passent ; premier échec
de chemin de type Bevy corrigé. Pas encore de preuve GPU, de nouveau passage workspace
ou Web pour ce raccord. Le projecteur n'émet pas encore ces lots. Les preuves optiques
antérieures ne qualifient pas cette révision du shader commun.

Préparation CPU des futurs lots lointains implémentée, **572 tests workspace PASS /
1 timing ignoré**, Clippy strict natif/Web PASS. UV/couleurs conservées, données de
racine/vent séparées et prévalidation bornée. Ce helper n'est pas raccordé au rendu
et ne réduit pas les triangles ; forêt lointaine toujours non qualifiée. Voir
`docs/evidence/gate1/R1_FOREST_FAR_PATH_DIAGNOSIS.md`.

**570 tests workspace PASS / 1 timing préexistant ignoré** au comptage réel de végétation,
Clippy strict natif/Web et build PASS. Cycle Chrome High/Low/High : comptages restaurés,
sources constantes, aucun mesh non comptable ; blocker végétation conservé. Voir
`docs/evidence/gate1/R1_FOREST_LIVE_TELEMETRY.md`. Ce relevé CPU ne qualifie pas les
draw calls GPU, le HLOD lointain ou les performances.

Passage précédent : **567 tests workspace PASS / 1 timing préexistant ignoré**, Clippy strict et Studio
WASM PASS à la stabilisation des bindings forêt, après les raccords optiques Web. Les commandes Studio
d'activation/retour fixe de l'adaptation ont été exercées dans le navigateur sur le
bundle précédent : activation en attente du plan, retour manuel confirmé, **aucune
adaptation effective Web acquise**. Le nouveau garde-fou refuse l'adaptation dans une
vue fixe avant/après installation caméra ; preuve ECS et compilation, pas nouveau
replay Web de l'adaptation. Voir `docs/evidence/gate1/R1_STUDIO_ADAPTATION_CONTROLS.md`.

La variation de l'herbe est liée aux IDs stables ; les brins plus fins/touffes resserrées
sont vérifiés en natif sans changement des placements, mais couverture et naturel restent
non qualifiés. Le profil soleil/pluie opt-in a ses cycles natif et Studio documentés ;
aucun profil artistique canonique promu. Les tests ne remplacent pas les acceptations.

Les chemins IBL/SSR et teinte Web ont désormais une preuve d'exécution ; le contraste
de teinte reste faible et sous le filtre conservateur, sans acceptation artistique.
La réfraction écran reste indisponible : sa substitution par teinte est explicitement déclarée
sous ADR-0011 et retirée à la désactivation manuelle. Les deux PNG fournis sont retrouvés et inspectés ;
leurs dimensions différentes ne permettent pas une comparaison pixel A/B. Le timeout
d'observation des téléchargements ne démontre pas un défaut de l'export produit.
Voir `docs/evidence/gate1/R1_WEB_OPTICAL_SAMPLE_PROTOCOL.md`.

Le chemin SSR Web dispose désormais d'une série optique répétée avec retrait/restauration,
caméra rapprochée et identités contrôlées. Son support est distingué de celui de l'IBL ;
les garde-fous de matériau, budget et choix manuels sont conservés. Ce support de chemin
ne vaut pas readiness GPU ni validation artistique. Les caméras de preuve natives peuvent
être chargées opt-in dans le Studio à condition qu'aucune autre donnée WorldSketch ne diffère.
Voir `docs/evidence/gate1/R1_STUDIO_CANDIDATE_VIEWS.md`.
Raccord rejoué dans Chrome : blockers eau 3 → 4 → 3 quand le SSR est retiré puis restauré,
sources inchangées et readiness false. Candidat Tauri debug construit avec ce bundle et le kit,
lancé sous WebView2 ; fenêtre/processus observés seulement, affichage encore à confirmer par
capture utilisateur. Cela ne qualifie ni Tauri ni l'acceptation humaine.
La capture utilisateur suivante montre ERR_FILE_NOT_FOUND dans ce premier candidat Tauri.
Cause corrigée : chemin frontendDist Windows interprété comme URL ; overlay relatif et
contrôle de destination ajoutés, quatre tests shell PASS, candidat v2 construit et relancé
avec frontend embarqué. L'utilisateur a ensuite répondu « oui » à la question confirmant
l'affichage du Studio et de la vallée dans v2. Cette confirmation lève l'attente d'affichage,
pas la qualification des effets, des performances ou de Gate 1 ; échec initial conservé.

La sonde IBL des vues fixes Studio a ensuite été implémentée et exercée dans Chrome :
intensité 1 → 0 → 1, SSR coupé, réflectance et sources constantes, trois PNG reçus/inspectés.
Assombrissement puis restauration visibles ; premier smoke seulement, série répétée et
qualification de support IBL encore dues. 40 tests lib Studio PASS, Clippy WASM et build PASS ;
pas de nouvelle qualification workspace ou Tauri déduite de ce lot.
La série IBL répétée suivante (neuf PNG, sources/caméra/F0 constants, SSR coupé) établit
un effet réversible largement supérieur au bruit. Support de chemin raccordé avec refus
à intensité zéro ; 566 tests workspace, Clippy strict natif/Web et build PASS. Rejeu Chrome
confirmé : blockers 4 → 5 → 4 avec SSR coupé et forêt non qualifiée ; seule la réserve IBL
varie, sources constantes. Aucune readiness GPU, validation artistique ou Gate 1 déduite.

Raccord teinte suivant : 566 tests workspace et Clippy strict natif/Web PASS, build et
rejeu Chrome 1 → 3 → 1 blockers, forêt candidate toujours bloquante. Sans kit/forêt
expérimentaux, High/Low/Medium/Ultra/High reçoivent des acquittements GPU distincts,
sources constantes et full preset false. PNG final inspecté, pas une qualification visuelle
de chaque profil, ni adaptation, performance, WebView2 ou candidat complet. La première
demande Ultra interrompue avant acquittement est conservée comme pending dans les preuves.

La forêt conserve maintenant ses entités survivantes lors des changements de profil,
par placement/LOD/primitive, au lieu de les recréer toutes. Cinq tests ciblés et cycle Chrome
High/Low/High sur les 636 arbres/7 632 primitives vérifiés, sources constantes ; aucun gain
FPS revendiqué. Comptages de rendu propres et arbres lointains restent à qualifier ; blocker
végétation conservé. Voir `docs/evidence/gate1/R1_FOREST_QUALITY_BINDINGS.md`.

Le menu Studio donne maintenant accès explicite à la vallée avec kit et forêt candidats.
42 tests Studio, Clippy WASM et build PASS ; clic du menu et affichage du kit/forêt vérifiés
dans Chrome. Le dernier binaire natif dispose de deux nouvelles vues clair/pluie, pas d'une
nouvelle série finale complète : les smokes courts ne satisfont pas p95/1 % low. Aucun asset
promu ni modification de carte. Voir `docs/evidence/gate1/R1_INTEGRATED_CANDIDATE_ACCESS.md`.

Le rapport consolidé `docs/evidence/gate1/R1_055_GATE1_STATUS.md` distingue preuves,
limites et interventions humaines restantes. Gate 1 demeure ouverte, Gate 2 non commencée,
CI distante NOT RUN, worktree Rust préservé et aucune publication Git.

### Historique du raccord qualité — preuves liées à leurs versions

Le plan effectif dispose d'un acquittement natif de préparation/soumission GPU, d'une reprise
après resize avec nouvelle séquence et d'un cycle High/Low/Medium/Ultra/High vérifié avec cinq
captures inspectées. 519 tests workspace PASS / 1 timing ignoré, Clippy/Web-WASM/build natif PASS.
Sources inchangées, preset authoré complet toujours false. Qualification de l'adaptation, perte réelle
de device, qualification Web/Tauri, qualité artistique/performance cible et acceptations humaines
restent ouvertes. Preuves : `docs/evidence/gate1/R1_EFFECTIVE_QUALITY_PLAN.md`, dernières sections.

Adaptation optionnelle désormais raccordée au moteur : `--quality low --adaptive-quality`
choisit réellement Low → Medium → High → Ultra en natif, sur les durées de frame courantes,
avec acquittement GPU distinct à chaque niveau et cinq sources inchangées. Mode fixe inchangé,
choix global manuel prioritaire ; perte de preuve arrête l'adaptation sans rollback inventé.
533 tests workspace PASS / 1 timing ignoré, Clippy/Web-WASM/build natif PASS ; binaire final
exécuté sur 4000 frames adaptatives puis 360 frames fixes, sans erreur. Les transitions prennent
2,4 à 4,9 s entre proposition et acquittement : fonctionnement vérifié, fluidité non qualifiée.
Interface adaptative Studio, baisse automatique sous charge réelle, Web/device-loss et autres
validations humaines de Gate 1 restent ouverts. Adaptation désactivée par défaut, aucune gate passée.

Le coût dominant des transitions a ensuite été mesuré et réduit : préparation terrain High
3,82 → 0,74 s, Ultra 3,64 → 0,74 s via le pool Bevy, sans changement géométrique. Délais complets
vers Medium/High/Ultra : 1,11/1,99/1,85 s ; encore perceptibles, pas qualifiés fluides.
535 tests workspace PASS / 1 timing ignoré, Clippy/Web-WASM/build natif PASS. Comparaison High
fixe 1500 frames avant/après sans régression observée, sources/plan/géométrie identiques.
Voir `docs/evidence/gate1/R1_QUALITY_TRANSITION_CPU.md`. Suite : accès et qualification adaptative
Studio/Web, sans prolonger un micro-audit CPU général ; R1-040 et Gate 1 restent ouverts.

### Historique récent des raccords

Les paragraphes suivants conservent la progression des lots ; leurs limites intermédiaires
ne remplacent pas le dernier état vérifié ci-dessus.

ADR-0011 : profils effectifs explicitement dégradés acceptés, avec extension du 6 septembre
autorisant l'intensité des reflets conservée dans la source mais non appliquée, à condition
de déclarer et d'afficher cette limite. Cette quatrième limite est représentée dans le
résolveur, la déclaration de capacités et le texte du diagnostic Studio ; son affichage Web
est vérifié, mais l'application du plan reste à qualifier. Aucun preset complet ni passage de Gate 1 acquis ;
les validations humaines restent requises. Voir `docs/ADR/ADR-0011-explicit-effective-quality.md`.
Raccord natif IBL/SSR désormais conditionné aux observations courantes et aux choix manuels ;
Web non qualifié. Smoke High canonique : quatre limites explicites, seul blocage du plan
`terrain` (berges), mais application et readiness GPU toujours non acquises. Ce résultat
ne qualifie ni la forêt prototypes ni les critères artistiques/de performance de Gate 1.
Preuves : `docs/evidence/gate1/R1_EFFECTIVE_QUALITY_PLAN.md`.
Les contraintes de berges sont désormais décrites dans le plan par le hash terrain et les
IDs des chunks conservés à pleine résolution. Le cache s'invalide si cet inventaire change ;
489 tests workspace, Clippy et check Web/WASM passent. L'émission native de l'inventaire
et son affichage Web sont vérifiés, sans readiness GPU acquise.
Décision suivante : le propriétaire accepte explicitement l'écart localisé de budget lié
à la protection des berges (ADR-0011). Raccord implémenté : 491 tests workspace PASS,
Clippy/Web-WASM/build natif PASS ; smoke High canonique à cinq écarts déclarés et zéro
blocker de résolution. Le plan reste `resolved_plan_not_applied`, readiness GPU et preset
complet false. La forêt prototypes et les effets Web restent à qualifier ; l'adaptation
runtime reste désactivée. Gate 1 et les performances ne sont pas validées.
Prérequis suivant livré : les cinq documents source des domaines sont liés au hash du
plan ; le garde-fou refuse un plan non lié et une ancienne identité après retouche de
source. 493 tests workspace et contrôles Clippy/Web-WASM/build natif PASS ; smoke natif
confirmant les cinq hashes émis. Installation et acquittement GPU restent à réaliser.
Les points d'entrée d'installation eau/environnement existent maintenant et sont exercés
sur composants/matériaux ECS : overrides transitoires, sources préservées, priorité aux
contrôles manuels de l'eau, refus des identités/sources/délais périmés. Ils ne sont pas
encore appelés automatiquement par le coordinateur global ; aucun acquittement GPU acquis.
496 tests workspace PASS / 1 timing préexistant ignoré ; Clippy strict et check Studio
Web/WASM PASS. Preuves détaillées dans `R1_EFFECTIVE_QUALITY_PLAN.md`.
Raccord suivant : le coordinateur installe maintenant les paramètres eau/environnement,
observe les sept domaines CPU et retire les overrides périmés sans changer les niveaux
manuels. Smoke High natif : sept observations CPU, même identité optique, sources inchangées,
statut `plan_application_pending_gpu`. Le garde-fou a une deadline réelle de 30 secondes ;
le probe GPU reste absent, donc aucun succès GPU n'est possible dans ce raccord partiel.
500 tests workspace PASS / 1 timing ignoré, Clippy strict, Web/WASM et build natif PASS.
Essai natif prolongé : timeout effectivement observé, séquence inchangée (pas de retry),
sept observations CPU conservées mais aucun acquittement GPU. Gate 1 reste ouverte.
Sonde de soumission GPU désormais exercée en natif, y compris après redimensionnement :
callback réelle liée au plan, 82 pipelines sans attente/erreur, observation de frame terminée.
Les erreurs de pipeline fraîches alimentent le garde-fou. Cette preuve de file GPU ne
qualifie pas encore les ressources/effets de chaque domaine : `gpu_ready` reste false.
504 tests workspace PASS / 1 timing ignoré, puis 13 tests ciblés après renforcement des
invalidations ; Clippy/Web-WASM/build natif PASS. Preuves dans `R1_EFFECTIVE_QUALITY_PLAN.md`.
Inventaire d'assets GPU raccordé ensuite : meshes avec allocations, matériaux avec bind
groups prêts et images référencées contrôlés avant complétion de file. Smoke High natif :
501 meshes / 8 matériaux / 5 images préparés, sources inchangées, callback reçue. 506 tests
workspace PASS / 1 timing ignoré, Clippy/Web-WASM/build natif PASS. Ressources spécifiques
des vues/effets et acquittement complet restent ouverts ; Gate 1 n'est pas validée.
Contrôle caméra ajouté le 7 septembre : buffer uniforme, offset, bind groups et texture de
profondeur couvrant le viewport vérifiés en natif avant callback. Déplacement d'origine
invalidant testé ; conflit ECS initial corrigé et conservé dans les preuves. 19 tests ciblés,
Clippy strict, Web/WASM et build natif PASS ; smoke code 0, sources inchangées. Les ressources
d'ombres/atmosphère/post-traitement et l'acquittement complet restent ouverts, Gate 1 aussi.
Raccord suivant du 7 septembre : inventaire d'effets GPU et première preuve de préparation/
soumission du plan effectif consommée par le garde-fou. High 11/11 groupes et Low 6/6,
sept projections CPU, acquittement Ready natif ; High maintenu sur 4800 frames. Après resize,
ancien acquittement invalidé même si les ressources reviennent. Reprise automatique et cycles
des quatre profils à réaliser ; adaptation désactivée. 516 tests workspace PASS / 1 timing
ignoré, Clippy/Web-WASM/build natif PASS. Sources inchangées, preset complet false ; ni
pixels/qualité artistique, ni Web/Tauri, ni performance cible, ni Gate 1 validés par cette preuve.

Décision du 6 septembre : ADR-0010 approuvé pour implémenter la forêt à prototypes gouvernés
partagés. Le contrat et un aperçu natif opt-in existent : 636 arbres sont projetés avec leurs
trois LOD, mais le premier feuillage clairsemé a été rejeté visuellement.
Le candidat suivant améliore la couverture lointaine sans validation artistique acquise
(`docs/evidence/gate1/R1_FOLIAGE_COHERENT_LODS.md`). HLOD optimisé, qualification
et validations humaines restent à réaliser (`docs/evidence/gate1/R1_FOREST_PREVIEW.md`). Cet accord ne passe pas Gate 1.
Référence historique : `main@a7f4a3f5dd0eeff1dc060ea1237b1d7d650b7ca0`
Référence upstream exécutable : `Humpalumps/Cadle@ea0da62549980816a6a27e08b75808b1f56d70e7` (`v0.6.0`)

Ce document est la source de vérité narrative de la branche `Rust`. Il doit permettre à un agent de
développement de comprendre le produit, les objectifs, les frontières, l’architecture cible, la
stratégie de migration et les critères d’acceptation sans charger le dépôt C#/Godot complet.

La branche `Rust` est une branche orpheline créée avec un arbre Git vide. Elle n’a aucun ancêtre commun
avec `main`. Une fusion Git classique n’est donc ni prévue ni souhaitable. La migration est une
migration de connaissances, de contrats, de données et d’assets sélectionnés, pas une migration de
l’historique ni une traduction ligne par ligne du code existant.

La branche ne part toutefois plus d’une page blanche fonctionnelle. Le dépôt public **Cadle** est
adopté comme upstream exécutable de référence : l’agent obtient une copie locale épinglée, conserve
le snapshot source intact, lance la baseline sous l’overlay npm borné d’ADR-0003 et utilise ses systèmes MIT comme donneurs de
techniques et de contrats comportementaux. Cadle n’est ni une nouvelle dépendance de production ni le
runtime final de Forge : son JavaScript/Three.js/WebGL2 doit être progressivement remplacé par des
implémentations Rust/Bevy/wgpu/WebGPU, avec attribution et preuves comparatives.

Gate C et Gate 0 sont terminées. Le runtime natif, le viewport WASM/WebGPU et le Studio
Tauri 2 + Leptos partagent une scène Forge versionnée. ADR-0005 confirme ce Studio pour la suite.
Les revues humaines Gate 0 restent différées et la CI distante reste `NOT RUN` pour blocage de
facturation ; ADR-0006 autorise jusqu’à Gate 4 une décision sur preuves automatisées locales sans
jamais transformer ce statut en CI verte.

---

