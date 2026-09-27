# R2-006 — index commun des objets de zone

## Qualification locale — 20 septembre 2026

R2-006 est qualifié localement sur le candidat apparié EXE
`4DB02C6214FD2FDA1CA29198C923AAD5306081AE57E6401EC062D069C81CA32F`,
WASM `8FE207042BA4CD33B7A3572C1114691E0BF9DAF3ECFB548A9DA963D28650616`.
Luna max a observé l'ouverture de deux zones de 44 objets partageant les mêmes
IDs, les pages 1–40 et 41–44 sans manque, les sélections chemin/rivière/bâtiment/
placement, le reset A→B, la conservation sur nouvelle révision du même AreaId,
le filtre par ID suivi d'une retouche, le picking bâtiment, une cible occluse
négative, l'annulation du picker et suppression → rafraîchissement → Undo.

La frontière Tauri transporte maintenant les commandes et réponses comme une
chaîne JSON lossless : la seed `5929365240144680497`, qui reproduisait un
arrondi JavaScript bloquant, est conservée exactement. Test ciblé, 76 tests lib
Studio, 4 tests binaires, 89 tests projet et suites authoring/workspace/zone_start,
Clippy natif et WASM strict passent. La fixture synthétique v4 a été régénérée
dans une destination distincte ; les versions et preuves antérieures restent
conservées.

Rapport et captures :
`artifacts/forge-runtime/gate2-resume-20260919/r2-006-cu/r2-006-v4-validation-20260920.md`.
Le rendu GPU, les collisions/passage joué et la revue humaine finale ne sont pas
déduits de cette preuve et restent ouverts au niveau transversal de la Gate 2.

## Reprise du 19 septembre 2026

`SourceControls` efface désormais les sélections chemin/rivière/bâtiment et
placement au changement de l'AreaId accepté, y compris un ID réutilisé dans
l'autre zone. Le picking bâtiment est annulé ; le clear de placement efface
aussi son état thread-local et son clic armé. Une nouvelle révision de la même
zone conserve les sélections ; les catégories restent indépendantes.

Luna max : formatage et Clippy WASM strict PASS. Candidat C1BA0999 construit,
WASM A09DE1B4, statut BUILD_ONLY. Preuves et rapports sous
`artifacts/forge-runtime/gate2-resume-20260919/r2-006-check` et
`r2-006-candidate-20260919`. Qualification graphique en cours : reset multizone
avec IDs partagés, continuité de sélection dans la même zone, pagination >40,
annulation et occlusion. Aucun verdict complet R2-006 déduit du build.

Fixtures synthétiques produites par Luna via `ZoneEditSession` : deux archives
avec AreaId distincts, même projet et IDs d'objets partagés, 41 chemins, une
rivière, un bâtiment et un placement (44 lignes par zone). Référence du chêne
gouverné vérifiée contre son GLB. Relecture des archives, validation des têtes,
catalogue hôte de deux zones et ouverture/fermeture hôte acquis ; aucune
qualification graphique déduite de ces contrôles. Générateur autonome,
manifest et journaux conservés sous `r2-006-fixtures-prepared` dans le dossier
de reprise. Les contrôles UI utiliseront une copie sous `r2-006-authoring`.

## Couches végétales — raccord du 15 septembre 2026

Index dérivé extrait dans `authoring_scene_index` : cibles typées chemins,
rivières, bâtiments, placements et VegetationLayerId. Les couches respectent
l'ordre de recette, le filtre nom/type/ID et la pagination commune. Leur libellé
indique spécifiquement un masque verrouillé, pas un verrou de toute la recette.
Les instances générées ne deviennent pas des objets persistants individuels.

Un seul signal de sélection alimente index, filtre du panneau de recette et
sélecteur de densité. Choisir une couche ouvre ses réglages ; Afficher toutes
les couches retire le filtre. Changer de zone ou retirer la couche invalide
la sélection. Choisir une entrée d'index annule l'aperçu précédent et les outils
de picking/peinture ; le changement de couche dans le sélecteur de densité aussi.
Aucune écriture de document ni commande supplémentaire de mutation.

Test ciblé PASS (11,09 s compilation, moins de 0,01 s exécution) : deux couches
avec IDs stables, filtre nom insensible à la casse/espaces, filtre ID, résultat
vide, archive inchangée. Clippy WASM strict final PASS (6,98 s), après suppression
d'un wrapper local superflu pour respecter la limite de taille de composant.
La dépendance proc-macro-error2 conserve son avertissement de compatibilité future.

Replay ciblé acquis ensuite sur DDCC9C5F (hash complet et nettoyage dans
R2_EDITOR_SHORTCUTS.md) : choisir Arbres dans l'index filtre les réglages de
recette et sélectionne Arbres dans l'inspecteur de densité ; le masque y est
explicitement verrouillé. Capture `shared-layer-selection.jpg` dans
`E:/OpenRPG_Forge/.gate2-evidence/layer-index-keyboard-ui-20260915/`.
Archive inchangée après aperçu de copie et annulation par Échap, sans Apply/Save.
Limites : reset multizone, retrait de la sélection et pagination au-delà de
40 objets non qualifiés graphiquement. R2-006 et Gate 2 restent ouverts.

14 septembre 2026, Rust, travail local non publié.

## Pagination implémentée — qualification graphique en attente

Correctif observé sur C5C3C3B12384A4B71E69A9629D4D59679ABE014B9DA280A7AF4438DF3EA630CF
(`authoring-review-fixed-studio-20260914`) : « Objets suivants » s’affiche
normalement, compteur 1–3 sur 3, précédent/suivant désactivés sur cette seule
page. Capture `authoring-review-ui-20260914/pagination-fixed.jpg`. Aucun
Apply/Save. Ce contrôle corrige le verdict du libellé, pas la navigation sur
plus de 40 objets. Build WASM PASS (59,44 s), packaging PASS ; aucun push.

Essai B4754D5D : FAIL du libellé « Objets suivants ». Le parseur view! a
interprété le `>` de `>=` comme fin d’attribut, affichant du code et perdant
le handler. Expression désormais entourée d’accolades ; Clippy Web PASS
(7,38 s), mais ancien candidat conservé comme preuve d’échec. Rebuild et
contrôle réel du bouton corrigé encore dus. Compteur 1–3 sur 3 observé.

L’index affiche au plus 40 boutons d’objets à la fois, un compteur et des
commandes précédent/suivant. La recherche porte sur toute la liste et revient
à la première page ; une liste réduite borne automatiquement la page courante.
Les sélections existantes restent indépendantes de la page, sans mutation de
zone. Le calcul des entrées est mémoïsé ; la liste complète reste en mémoire,
donc ceci n’est ni une pagination serveur ni une preuve de budget mémoire.

Test ciblé des bornes : 1 PASS (vide, limite exacte, dernière page, réduction,
couverture sans doublons). La première commande avait exécuté zéro test car
le module UI est WASM-only : résultat non retenu ; helper isolé puis test réel.
Formatage effectué ; Clippy Web/WASM `-D warnings` PASS (7,06 s).
Affichage et boutons encore à qualifier avec le prochain candidat groupé ;
F1C5DB66 inchangé, aucun lancement graphique ni publication pour ce lot.

## Qualification assets — même candidat F1C5DB66

Parcours réel complémentaire sur la copie scene-index-assets-ui-20260914 de
authoring-placements-ui-projects-20260913, sans recompilation. Ouverture de la
zone 81de8d42-89b9-58b5-b090-406cf4c63d06 ; index avec l’asset placé
vv-architecture-arch-a, ID 63a0e640-ab20-53be-b3d7-7007135019e3. Clic sur
l’entrée : bibliothèque ouverte, même ID sélectionné et inspecteur affiché aux
coordonnées (-88,39 ; 3 ; 85,12) m. Cadrer : arche réellement visible en gros
plan. Catalogue non rechargé manuellement. Capture asset-framed.jpg sous cette
copie de qualification. Aucun Apply/Save ; archive avant/après identique :
FF89547BE4DFBF6C9345A2D4E9EFE340C51495158A69A08AB63C5D435ED868C0.

Ce parcours qualifie index → inspecteur → cadrage d’un placement existant.
Fermeture vérifiée : fenêtre absente, PID 34696 et descendants 11952, 25820,
18960, 25320, 34516, 33836, 12544 tous absents au dernier contrôle CIM. Deux
WebView terminaient encore au premier contrôle ; aucune relance ni arrêt global.
Il ne qualifie pas édition/Undo, suppression, multisélection ou toutes les
catégories d’assets. Les anciennes archives v5 rejouent leurs commandes : ne
pas déduire l’absence d’objets d’une propriété head inexistante dans leur JSON.

## Qualification graphique partielle — F1C5DB66

Parcours réellement réalisé via Computer Use, sur écran secondaire, avec une
copie de retention-ui-20260914-0900 dans scene-index-ui-20260914 : ouverture de
la zone 81de8d42-89b9-58b5-b090-406cf4c63d06, sélection du chemin depuis l’index,
inspecteur renseigné (« Chemin · 2 points »), bouton de cadrage puis déplacement
effectif de caméra observé. Filtre « BÂTIMENT » : une seule entrée, verrou visible,
clic puis inspecteur de la Pièce 76be5a7c-f146-5381-83ac-43e9881f09d1 présent
dans l’arbre d’accessibilité avec ses commandes de cadrage.

Captures sous E:/OpenRPG_Forge/.gate2-evidence/scene-index-ui-20260914 :
path-framed.jpg et filter-building.jpg. Archive avant/après identique :
5269A9B0F430785FE9A307928B615E716EF0CD89C33F204A61142CD1A0BE7D20.
Aucun Apply/Save pendant ce parcours. Le premier clic de cadrage hors fenêtre
a été refusé par l’outil ; après défilement, le bouton visible a fonctionné.

La fixture contient un chemin, une rivière, un bâtiment et **zéro placement**.
La première inspection PowerShell avec @($null).Count était trompeuse ; un
comptage corrigé et l’index réel confirment zéro. Ni sélection d’asset ni édition,
suppression/Undo, ni picking 3D synchronisé ne sont qualifiés par ce parcours.

Fermeture acquise : fenêtre absente, PID 19092 et descendants 32012, 28384,
30424, 17708, 29864, 34308, 28944 tous absents au contrôle CIM final. Pas de
nouvelle compilation ni publication. R2-006 reste partiel.

## Extension aux placements — même journée

Candidat groupé construit :
`E:/OpenRPG_Forge/.gate2-evidence/scene-index-studio-20260914/forge-studio-candidate.exe`,
SHA-256 `F1C5DB66E3DAEF16005BFEA6E43E5769B38EFA57BF2739B9F7BCC6DE1819FD9F`.
Frontend `scene-index-web-20260914`, WASM
`90620A7DFC2F3ECEFD534F6D425683A7B5470D391155216B7383A54D5CB9CE76`.
Trunk PASS (compilation 59,63 s), packaging local BUILD_ONLY PASS, sans lancement.
Configuration écran secondaire (-1840, 90), 1600 × 900. Kit gouverné repris du
candidat D75CB60E, contrôlé par Build-StudioCandidate.ps1. Comprend aussi le
cadrage chemin de R2_PATH_FRAMING.md. Réutiliser ce candidat pour le prochain
parcours graphique, ne pas reconstruire sans changement de source.

Les assets placés sont désormais inclus (nom, identifiant et verrou). Une seule
liaison de sélection est créée dans AuthoringControls et transmise à l’index,
la bibliothèque et l’inspecteur. Le picking 3D continue d’alimenter cette même
liaison. Choisir un placement appelle le mécanisme existant choose(), et ouvre
la bibliothèque ; choisir un autre type annule aussi le picking de placements.
Les lignes portent EntityId directement, sans parser un identifiant libre.

Clippy Web/WASM avec les mêmes options : PASS, 6,86 s. Interaction réelle non
encore qualifiée. Les paragraphes ci-dessous décrivent le lot initial ; leur
exclusion des assets est supersédée par cette extension. La végétation générée
reste volontairement dans le panneau de règles, sans identité canonique par arbre.

## Implémenté

- Liste des chemins, rivières et bâtiments de la tête acceptée, dérivée à
  l’affichage ; filtre insensible à la casse par nom/type/ID, verrous indiqués.
- Sélections remontées dans SourceControls puis partagées avec les inspecteurs
  existants. La sélection bâtiment demeure celle du picking 3D existant.
- Les panneaux sélectionnés s’ouvrent ; les champs restent gérés par leurs
  inspecteurs. Plusieurs panneaux peuvent garder leur sélection, ce n’est pas
  une sélection multiple destinée à une commande de groupe.
- Choisir une entrée annule les outils de picking concernés ; aucune commande
  durable, application de brouillon ou sauvegarde. Busy/playtest désactivent
  les boutons. Liste vide explicitée.
- Assets placés et végétation restent dans leurs panneaux dédiés, annoncé dans
  l’interface. Aucun objet Bevy pris comme source canonique.

## Vérifié

`cargo clippy -p forge_studio --lib --no-default-features --features web
--target wasm32-unknown-unknown --offline --locked -j 2 -- -D warnings` : PASS,
6,87 s. Avertissement futur tiers proc-macro-error2 2.0.1 inchangé.

## Reste à qualifier

Interaction réelle filtre → sélection → ouverture → retouche, cohérence avec
le picking bâtiment et rafraîchissement après suppression/Undo. Pas de
qualification graphique ni de test de comportement revendiqué par Clippy.
Pas de nouveau navigateur/processus graphique lancé. R2-006 et Gate 2 ouverts.
