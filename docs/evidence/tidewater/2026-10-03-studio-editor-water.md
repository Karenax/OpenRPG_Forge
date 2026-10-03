# Océan dans la vue édition — 3 octobre 2026

## Défaut reproduit et cause

Le candidat Studio attempt.3, SHA-256
`d20adfdc81481b35a6091cea3291562f87b502dc7b5dcc2b2c791f7528737e1e`,
affiche les bâtiments, le ponton, les arbres et le ciel sur la fixture isolée
`studio-library-resume-20261003-v1/runtime-fixture-v1`. La zone océanique reste
un fond de terrain émergé dans la vue édition, avant et après un Playtest.
Le parcours et ses limites sont conservés dans le rapport library-resume du jour.

Dans `authoring_imported_terrain::launch`, trois gardes `playtest.is_some()`
réservaient l'optique, la géométrie et la préparation du spectre océan au jeu.
L'environnement et les paquets vérifiés étaient pourtant chargés dans les deux
modes. La surface n'était donc jamais construite dans l'éditeur.

## Correction source

Le même ensemble de paquets vérifiés active désormais l'océan, sa géométrie,
l'optique et le ressac en édition comme en jeu. Les paramètres explicites de
diagnostic restent pris en compte. Le contrôleur joueur et la nage restent
installés exclusivement dans le Playtest ; aucune donnée auteur n'est changée.

La caméra éditeur reçoit les usages de profondeur TEXTURE_BINDING et COPY_SRC
avant l'allocation du premier rendu. Avec l'optique active, elle reçoit HDR,
le tonemapping AcesFitted et le marqueur de caméra sous-marine. La projection
éditeur est ordonnée après la mise à jour sous-marine, comme en jeu.
Les mêmes mises à jour de géométrie lors des mouvements de caméra et la même
reconstruction depuis les retouches terrain sont conservées.

## Validation et limites

Le contrôle rustfmt du fichier et le diff ciblé passent. Une construction
release WASM réelle est lancée par `Build-EditorWater.py` dans
`F:/OpenRPG_Forge/.build/studio-editor-water-20261003-v1` (session 33920).
Le script vérifie le snapshot library-resume précédent, copie ses sources
avec le seul overlay `authoring_imported_terrain.rs`, puis vérifie les entrées
avant/après compilation et conserve l'artefact WASM exact. Les sources et
les candidats antérieurs ne sont pas remplacés.

À cette étape, aucun succès terminal de cette construction, nouvel assemblage
frontend, lancement natif du correctif, pixel d'eau dans l'éditeur ou parité
graphique n'est revendiqué. La validation doit vérifier la vue océan, les
mouvements de caméra, les retouches et le retour du jeu sur le nouveau candidat.

Le coordinateur Python PID 45468 (création 05:43:32.875233 UTC) est confirmé
vivant pendant la préparation ; son snapshot atteint ensuite 1 039 fichiers.
La session 33920 reste active, sans résultat Cargo terminal observé.
`Finalize-Frontend.py` prépare l'assemblage et l'ajout du worker de navigation,
avec contrôle syntaxique PASS. Il n'est pas encore exécuté.
Le script initial emploie un nom de sortie WASM incorrect (`forge_studio.wasm`
au lieu de la bibliothèque `forge_studio_frontend`). La continuation attend
son reçu terminal, conserve cet éventuel refus de copie et exige un succès
Cargo puis l'unique événement d'artefact optimisé avant toute récupération.
Elle revérifie le snapshot et le fichier source du correctif ; elle ne relance
pas Cargo pour réparer un nom de fichier et ne transforme pas l'assemblage
en preuve d'affichage dans Studio.

Le snapshot atteint ensuite 1 343 fichiers vérifiés. Le contrôle vivant suivant
confirme Cargo PID 40544 et rustc PID 44176, avec compilation de `forge_engine`
pour wasm32-unknown-unknown, opt-level=3, dans le cache boat-engine-build-338.
La session 33920 reste active ; aucun résultat terminal n'est encore disponible.
La continuation native attempt.4 est préparée, sans être exécutée pendant cette
compilation. Le parcours UI de retouche sur attempt.3 est consigné séparément
dans le rapport library-resume ; il ne valide pas ce correctif d'eau.

Cargo release WASM termine avec retour 0 à 06:19:02 UTC, après 29 min 31 s.
L'avertissement de future incompatibilité concerne proc-macro-error2 v2.0.1.
Le coordinateur conserve ensuite un refus de copie à 06:19:38 UTC : le nom
`forge_studio.wasm` n'existe pas. Il ne s'agit pas d'un échec Cargo et aucune
nouvelle compilation n'est lancée pour ce nom erroné. `Finalize-Frontend.py`
sélectionne l'unique artefact `forge_studio_frontend` annoncé par Cargo,
revérifie les sources puis lance l'assemblage (PID 46376, enfant 31844).
Le succès d'assemblage, le candidat natif et l'affichage de l'eau restent
non vérifiés à cette observation.

Une fixture UI séparée est en préparation depuis le workspace retouché dont
l'archive active est `5da543d0d0af548e6a338c142161d3a0eab04ac1f11c926fc31f725f2eb953f2`.
`Prepare-RuntimeFixture.py` copie les fichiers lisibles, revérifie leur stabilité
et exclut les verrous actifs. Le profil WebView est séparé ; aucun lancement
du nouveau candidat n'est encore effectué.

La préparation de fixture termine avec retour 0 : 26 fichiers copiés et
revérifiés, archive active retouchée identique, verrous exclus et profil
WebView séparé. `runtime-fixture-v1/fixture-preparation.json` conserve les pins.
La session d'assemblage 61379 reste en cours au contrôle suivant.

Au contrôle suivant, les handles des anciens coordinateurs sont absents et
aucun processus Finalize/Attach-Worker associé n'est vivant. L'assemblage a son
reçu terminal (1 754 fichiers), mais la sortie navigation est partielle, sans
reçu ni frontend-result.json. Elle est conservée. `Recover-Worker.py` revérifie
Cargo, les sources et l'artefact, puis ajoute le worker dans une sortie fraîche
`frontend-with-navigation-v2` ; aucune reconstruction WASM n'est effectuée.

La continuation termine avec retour 0 à 06:31:51 UTC : 1 758 fichiers finaux,
1 343 sources vérifiées et inchangées, reçu frontend SHA-256
`dada2f7cbe8009d81e1ddbaa55bb9cd8df2b7cf1b8cac6d052c9eeed8ececa06`.
Le WASM brut porte le SHA-256
`e340e47991eaa0d89abf20f2bc07deaa36df30ff9c29b9540bf1c74d26ac4873`.
`frontend-result-v2.json` conserve cette portée de construction seule et les
sorties interrompues. `Run-EditorWaterNativeV2.py` est préparé depuis le script
natif précédent avec le nouveau reçu, puis lancé (session 5039) après audit du
cache natif libre. Le candidat visé est attempt.4. Le script de lancement UI
isolé est préparé ; ni succès natif ni affichage de l'eau ne sont encore acquis.

La construction native attempt.4 termine avec retour 0 à 06:40:06 UTC.
Le reçu native-editor-water-v2/result.json vérifie 175 sources natives,
189 entrées source/métadonnées et 1 767 fichiers frontend du candidat.
Exécutable SHA-256 : 1ce9eb78f9ed8f7e0e55c0965ff70fdcacd4ec46de9fee06808f91cebc6bb9d0.
Le lancement isolé réussit à 06:44:13 UTC, PID 45656, avec les pins de
fixture et de frontend vérifiés avant lancement. La zone est ouverte via
le workspace propriétaire ; le parcours historique direct est refusé
comme prévu. Le chargement manuel des paquets est demandé une seule fois.
Le rendu eau et le cycle Playtest restent à observer.

Le parcours Computer Use observe ensuite le rendu réel de l'eau en édition :
vagues turquoise, reflets et rivage écumeux, avec changement visible des vagues
et de la ligne de rivage entre observations. Le Playtest affiche le village.
Après Retour à l'édition, la vue côtière, le ponton et l'eau sont restaurés
sans deuxième chargement manuel. Un rapprochement caméra demandé avant le
cycle est visible dans la vue restaurée. Aucune nouvelle retouche ni sauvegarde
n'est effectuée dans ce parcours. Le reçu editor-water-ui-observation-v1.json
consigne les observations et revérifie les pins des fixtures source et copie.
Cette observation ne qualifie ni la vitesse exacte des vagues, ni la fidélité
numérique des pixels, ni toute l'édition ou la parité complète de l'île.
