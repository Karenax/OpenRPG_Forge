# Poissons de l’étal et accessoires du village — 4 octobre 2026

Le contrôle des deux inventaires source distingue 78 FishProps du village et
5 poissons de FishStand. Le source Game.js construit ces derniers par
CatchDisplay avec FishStand.iceFish(). Le contrôle des 390 paires de positions
horizontales trouve zéro ancre commune à 1 cm ; la paire la plus proche est à
22,296262 m. Reçu local : fish-stand-prop-overlap-audit-20261004-v1/result.json.
Ce contrôle porte sur les ancres authorées, pas une intersection de géométrie.
Aucune suppression du composite n’est justifiée par un doublon de ces groupes.

L’adapter Studio fish_stand_scene.rs prépare encore cinq StandardMaterial gris
(base_color 0,56 / 0,58 / 0,55, metallic 0,15, roughness 0,32), sans le skin ou
le masque source. CatchDisplay source utilise pourtant FishProps et ses surfaces
procédurales, avec cloudy 0,5 / wet 0,7. Cette approximation demeure incomplète.

Une acquisition distincte GameFish est réalisée depuis le clone intact au commit
1438b1abfcaee3267092b75573014f4d9b4a983c, Node 22.22.3, GPU source headless.
Le véritable CatchDisplay est construit avec les frames calculées par la méthode
FishStand.iceFish source et TerrainData source. L’observation de FishProps.build
conserve ses appels originaux. La lecture des records GPU correspond exactement
aux records CPU capturés. Les graines Math.random réelles sont capturées sans
override : c’est une acquisition figée, pas une réexécution déterministe annoncée.

`game-fish-source-export-20261004-v1` contient 23 instances : 18 slots de prises
stationnés sous le monde et les 5 poissons de l’étal. Il conserve 18 prototypes,
3 LOD, positions, normales, données de nageoires, indices, records et provenance.
Validation indépendante : hashes, tailles, valeurs finies, indices bornés,
records float32 exacts et fichiers source identiques passent. Triangles cumulés
par LOD : 72 198 / 14 998 / 14 998. Reçu :
`game-fish-source-export-20261004-v1-validation.json`.

Le premier contrôle supposait à tort la position du corps exactement à -2000 m.
FishProps soustrait l’ancre des ouïes au point PARK ; le contrôle corrigé compare
chaque coordonnée au source capturé après conversion float32, puis vérifie les
slots sous le monde. Aucun changement du source n’a été nécessaire.

Prochaine étape : paquet gouverné et adapter commun pour réutiliser le matériau,
les LOD et les ombres masquées pour ces poissons, en conservant les transformations
et le cycle d’édition du composite. Acquisition et intégrité ne prouvent ni leur
rendu Rust ni les mécaniques de pêche ; aucune parité GameFish n’est déclarée.

## Paquet et lecteur Rust vérifiés

Le paquet `game-fish-package-20261004-v1` contient 248 fichiers, soit
3 940 603 octets. Son manifeste est épinglé à
`f26aceb2fdcee7f9e2261f90bd7e75fa7d66b672eb854dc66caa453c904c456d`.
Le matériau provient du paquet FishProps vérifié ; les graines capturées
appartiennent à cette acquisition immuable. Aucun runtime JavaScript requis.

`crates/forge_assets/src/tidewater_game_fish.rs` vérifie les tailles, chemins,
hashes et contrats avant publication. Son catalogue propose les cinq poissons
exposés et exclut les dix-huit slots de prises stationnés sous le monde.
Le harnais ciblé passe compilation, cinq tests et Clippy. Contrôle supplémentaire
dans la véritable crate, cache distinct de la compilation Gate 3 active :
`cargo test -p forge_assets tidewater_game_fish --locked --offline -- --include-ignored`
termine code 0, cinq tests réussis, aucun ignoré, 85 autres tests filtrés.
Le fixture est explicitement fourni par `FORGE_TIDEWATER_GAME_FISH_PACKAGE`.
Ce résultat ne constitue pas un test du workspace complet ou du rendu Studio.

`stand-game-geometry-comparison-20261004-v1.json` vérifie les cinq espèces :
positions, normales et aData sont identiques octet pour octet au LOD haut ;
les indices sont identiques après conversion u16 vers u32. Les entrées sont
hashées et inchangées après comparaison. Les trois paramètres de déformation
des cinq instances sont nuls. La géométrie existante peut être réutilisée pour
le raccord du matériau source ; cette preuve ne couvre pas les transformations
en scène, les LOD distants ou les ombres intégrées. Le matériau gris demeure
présent dans l’adapter Studio à cette étape.

Le contrôle complet `cargo clippy -p forge_assets --lib --locked --offline -- -D warnings`
termine code 101 avec 17 diagnostics dans `source_fragment.rs` et
`tidewater_pier_motion.rs`, hors du nouveau lecteur GameFish. Son journal complet
est conservé dans `game-fish-assets-validation-20261004-v1/cargo-clippy-full.log`.
Le verdict Clippy de la crate complète est donc ÉCHEC, distinct du PASS du
harnais ciblé. Aucun avertissement n’est désactivé pour obtenir un PASS.

## Raccord du matériau dans Studio — implémentation

Le loader de bibliothèque acquiert désormais le champ optionnel
`game_fish_sha256`, vérifie le paquet entier avant publication atomique et
efface sa référence à l’invalidation. La copie complète
`studio-game-fish-library-check-20261004-v1/library-stage` contient 3 009
fichiers vérifiés, incluant les 248 payloads GameFish et leur manifeste.
Tous les champs et ressources de la bibliothèque précédente sont conservés ;
la source reste inchangée. Cette copie n’est pas déployée.

`fish_stand_scene.rs` remplace le StandardMaterial gris par FishMaterial :
peau source, pattern/graines, flags des yeux et humidité, rest position et
aData. La pose utilise l’adapter de déformation existant, même lorsque les
paramètres source sont nuls, pour conserver les stabilisateurs epsilon source.
Les transformations capturées sont converties en poses locales par l’inverse
de la frame source du stand ; les retouches du root commun restent applicables.
Les caches de meshes, matériaux et skin buffer sont libérés à la purge ; les
cinq ressources CPU sont préparées avant publication des handles. Temps et
frame des matériaux sont actualisés.

La préparation refuse explicitement un paquet GameFish absent au lieu de
revenir silencieusement au matériau gris. Les anciennes bibliothèques doivent
donc être actualisées pour projeter le composite avec ce nouvel adapter.
Les cinq poissons restent enfants du placement FishStand ; aucun doublon
indépendant n’est ajouté à la carte. LOD et masques d’ombres de ces cinq enfants
restent à raccorder. Aucune capture Studio de ce raccord n’est encore validée.

La copie source figée `studio-game-fish-scene-check-20261004-v1` conserve
1 351 fichiers, hérités du candidat v6 avec les adapters et lecteur GameFish
mis à jour. La première commande Cargo directe échoue dans wasm-bindgen avant
vérification de l’application. Une validation de la copie figée utilise ensuite
l’environnement Rust explicitement épinglé des candidats précédents, dans un
cache distinct du cache Gate 3 actif. Le résultat de cette validation doit être
consigné séparément ; implémentation, compilation et preuve visuelle restent
trois états différents.

Le contrôle WASM de la copie figée termine code 0 en 2 min 36 s :
`studio-game-fish-scene-check-20261004-v1/result.json` confirme toutes les
sources inchangées. Il compile l’adapter Studio et le lecteur GameFish avec
Rust 1.98.0 explicite ; le résultat ne lie ni n’exécute l’application.
La construction du frontend lié est ensuite lancée sur cette même copie
(session 71422), avec conservation du produit précédent. Elle doit encore
terminer avant assemblage des bindings, worker et validation native/visuelle.

`worker-reuse-preflight.json` passe sur la copie GameFish : les 28 entrées
source, dont Cargo.lock, correspondent exactement au worker du candidat
précédent, et tous ses artefacts sont vérifiés. La réutilisation est donc
possible ; aucun worker n’a encore été copié dans le frontend GameFish.
Les scripts d’assemblage, validation des entrées du constructeur natif,
construction desktop et staging indépendant sont préparés. Ils sont séquentiels
et refusent un résultat de build non réussi ou des entrées modifiées.

La session frontend 71422 est recontrôlée vivante ; aucune nouvelle construction
concurrente n’est lancée dans son cache. La préparation native exclura forge-kit
de la copie utilisée pour valider les entrées du constructeur, mais le frontend
réel embarqué conservera le kit complet, comme pour le candidat v6. Ces scripts
préparés ne constituent ni une compilation native réussie ni une preuve runtime.

## Préparation commune des trois LOD GameFish

Le préparateur canonique `PreparedFishProps` dispose désormais de
`from_game_package`. Les deux inventaires utilisent le même décodage de modèles,
la même déformation source et les mêmes attributs de shading. La branche GameFish
prépare uniquement les cinq instances exposées, avec leurs identités propres,
et conserve leurs trois LOD ; elle ne projette pas les dix-huit slots de prises.

`game-fish-engine-preparation-validation-20261004-v2` passe compilation,
20 tests (aucun ignoré, aucun filtré) et Clippy ciblé avec warnings en erreurs.
Les fixtures réelles comprennent le nouveau paquet GameFish, l’inventaire
FishProps existant et les oracles de pose, LOD et shaders précédents. Le nouveau
test vérifie les cinq IDs, les paramètres de surface exacts, les tailles de
géométrie des quinze meshes et les rest positions/aData identiques aux fichiers
source. Les régressions des 78 FishProps passent aussi. Toutes les entrées
hashées restent inchangées.

Le premier harnais v1 échoue à résoudre la dépendance transitive du bridge
d’assets. Le v2 ajoute son chemin de recherche et conserve le journal v1 ;
aucune relaxation de test ni modification de fixture n’est effectuée.
Cette preuve directe du module canonique utilise des bibliothèques natives
précédemment vérifiées : ce n’est pas un build Cargo complet ni une exécution
GPU. Le candidat frontend v1 en construction reste figé à son étape antérieure
de remplacement du matériau. Les trois LOD GameFish et leurs proxies d’ombres
ne sont pas encore raccordés au viewport Studio.

## Frontend lié et construction native du candidat matériau

La session frontend 71422 termine code 0 après 16 min 55 s. Le WASM lié
fait 125 834 245 octets, SHA256
`88aaae8fcd6d1d92b91e64a095aef673d4c2f42e63bd0e85f9c46a136ad0730a`.
`frontend-result.json` confirme les sources figées intactes. Les bindings
wasm-bindgen 0.2.127 et les fichiers de la bibliothèque complète sont assemblés.
Le worker est réutilisé après vérification de toutes ses entrées et artefacts.
`frontend-ready.json` inventorie 3 020 fichiers ; aucune exécution navigateur
ou parité visuelle n’est annoncée par ce reçu.

Les entrées du constructeur natif sont validées avec la copie séparée sans
forge-kit ; le frontend réel garde le kit complet. La construction native
est lancée (session 87041), cache distinct de la compilation Gate 3 existante.
Elle doit encore terminer, puis le candidat doit être lancé et capturé.
Cette construction conserve le préparateur FishProps de son snapshot v1 ;
le nouvel API de préparation GameFish et ses tests CPU précédents sont un
incrément séparé, non inclus dans ce candidat matériau.

## Résultat natif et acquisition réelle dans Studio

La construction native 87041 termine code 0. `native-result.json` confirme
les sources figées et les entrées frontend inchangées. L'exécutable candidat
porte le SHA256
`88dae4ba48c23cf398f6e5548f4dce277a1c67b23a7f5b44a9014730c6dc4597`.
Le staging indépendant conserve les 3 020 fichiers du frontend vérifiés.

Le candidat est lancé sur une copie isolée de la racine d'auteur, avec un
profil WebView2 neuf. Le parcours réel ouvre le workspace puis la zone
Tidewater island. Dans Objets → Fiche → Bibliothèque de bâtiments importés,
la commande « Vérifier et charger la bibliothèque » aboutit au message
« 23 bâtiments vérifiés et chargés en mémoire » et confirme les ressources
de ciel, nuages et végétation acquises. La navigation au clavier permet
de poursuivre après des observations de clics différées.

Le viewport sort de sa préparation et affiche terrain, océan, ciel, nuages,
végétation et bâtiments. Le journal indique les collisions synchronisées.
Captures et arbres UI sont conservés dans
`.placeables-smoke-game-fish-20261004-v1/visual-v1/` :
`library-acquired.jpg`, `library-acquired-ui.json`, `scene-rendered.jpg`
et `scene-rendered-ui.json`.

Cette observation prouve le démarrage et l'acquisition de la bibliothèque
dans le shell natif. Elle ne qualifie ni parité visuelle, ni performance,
ni affichage des cinq nouveaux matériaux : la fixture utilisée n'expose
pas de composite FishStand à inspecter. Le prochain parcours doit utiliser
une zone contenant ce placement commun ou le placer par les outils Studio,
puis focaliser ses poissons. Les trois LOD et ombres GameFish restent à
raccorder au viewport ; le candidat v1 conserve leur état antérieur.

`visual-v1/runtime-result.json` consigne acquisition et pixels observés,
avec les verdicts matériau et parité à false. Les hashes de la fixture
originale restent inchangés. Le candidat de cet essai est fermé par
« Enregistrer et quitter » sur sa copie isolée ; sa terminaison est vérifiée.
Les autres sessions Studio sont conservées.

## Composite réellement présent : parcours natif V3

Le candidat matériau inchangé est lancé sur une copie de la fixture
`studio-tidewater-fish-stand-edit-smoke-086/authoring-root`, qui contient
27 placements, dont l'échoppe `a5faa27f-2bc8-5171-957b-047baed51fb7`.
La tentative V2 sous contexte sandbox ne fournit pas de fenêtre accessible ;
seul son processus est fermé. Le lancement V3 sur le bureau interactif fournit
la fenêtre native attendue. Aucun autre Studio n'est arrêté.

Après acquisition de la bibliothèque, la recherche « Échoppe », la sélection
commune et le cadrage de l'objet fonctionnent. Le composite apparaît dans le
viewport. Les rotations relatives 180° puis 90° sont examinées uniquement en
aperçu. La cabine masque encore les poissons : ces captures ne qualifient pas
leurs cinq matériaux. L'aperçu 90° est abandonné par la commande publique ;
le journal confirme « carte appliquée inchangée » et la scène revient au pose
appliquée. Le champ de brouillon reste à 90°, sans application.

`visual-stand-v3/stand-common-preview.jpg`, les arbres UI associés et
`runtime-result.json` conservent les observations. La fermeture publique
« Enregistrer et quitter » concerne uniquement la copie isolée. Le PID 32320
est ensuite absent et les 30 empreintes de la fixture originale sont intactes.
La sélection/cadrage du composite est observée ; matériau et parité restent
non qualifiés. Les LOD et ombres GameFish restent ouverts.

## Raccordement LOD encore manquant : inspection du code courant

L'inspection de `authoring_assets/fish_stand_scene.rs` confirme que le cache
`State.fish` contient actuellement un seul `Handle<Mesh>` par poisson.
`prepare_fish_assets` construit ce maillage via `fish_mesh` et les payloads
FishStand, puis `spawn_fish_stand` publie un seul enfant visuel par poisson.
Ce chemin conserve les poses et le matériau GameFish, mais ne raccorde pas
les deux LOD visuels ni le maillage de profondeur.

Dans le moteur courant, `PreparedFishProps::from_game_package` conserve
les cinq templates source 18–22 et leurs trois `lod_meshes`. Cette préparation
n'est donc pas une preuve que Studio les affiche : l'appel n'est pas présent
dans ce chemin d'étal. La suite doit consommer ces meshes dans le composite
commun, avec uniforms LOD propres aux copies, et agréger leurs casters masqués
avec ceux des FishProps existants sans remplacer leur frame partagé. Toutes
les parties doivent garder l'identité du placement parent pour sélection,
transformation et suppression communes. Aucun raccordement n'est déclaré
terminé par cette inspection et aucun paquet de contenu n'est modifié.

## Raccordement des deux LOD visuels dans le code Studio

`fish_stand_scene` consomme désormais `PreparedFishProps::from_game_package`.
Le cache garde les trois meshes de chacun des cinq poissons, avec poses locales
calculées depuis la racine source de l'étal. Les deux LOD visuels sont publiés
comme enfants du même placement ; le troisième mesh de profondeur reste retenu
mais n'est pas encore envoyé au collecteur masqué. Il ne projette donc aucune
ombre supplémentaire par cette modification seule.

Le module `fish_stand_scene/lod.rs` clone un matériau par LOD et par copie,
utilise la politique source `tidewater_fish_props::lod::select`, la caméra
principale filtrée et son frustum. Distance et longueur viennent des transforms
mondiaux ; les uniformes de fondu, temps et frame sont indépendants pour chaque
copie. Chaque partie garde `ManualPlacementPart(parent_id)`. La suppression et
le purge retirent aussi ces matériaux indépendants et les meshes résidents.
Le transform/picking canonique demeure celui du placement composite.

Rustfmt et diff ciblé passent. Contrôle WASM lancé sous session 12607 dans
`studio-stand-lod-check-20261004-v1` : copie du snapshot précédent, quatre
remplacements épinglés (projection, module LOD, préparateur moteur et lecteur
GameFish), cache WASM distinct contrôlé inactif avant lancement. Aucun verdict
Cargo ou rendu de ce changement n'est encore acquis ; le collecteur d'ombres,
le picking des deux LOD et l'inspection des cinq matériaux restent à qualifier.

## Collecteur masqué commun raccordé et contrôle WASM réussi

Le contrôle V1 était en échec sur l'alias de macro Bevy `bevy_ecs`. Le V2
était en échec sur une conversion directe de `forge_core::Transform` en
`GlobalTransform`. Ces erreurs sont corrigées sans modifier les données de
placement : alias ECS explicite et `domain_transform_to_bevy` à la frontière
moteur. Les reçus en échec restent conservés.

Le V3 termine code 0 : `studio-stand-lod-check-20261004-v3/result.json`, sources
figées intactes, Cargo WASM réussi. Ce reçu ne lie pas une application et
n'exécute aucun GPU. Il couvre les LOD et le collecteur décrits ci-dessous.

Le troisième mesh GameFish est désormais collecté avec ses attributs de masque
de nageoires. Les poses viennent du `GlobalTransform` du poisson enfant ; la
limite source 22 m est calculée indépendamment du frustum visuel. Chaque
copie de l'étal produit ses poses, conserve les mêmes meshes résidents et
retire sa contribution lorsqu'elle disparaît.

`fish_shadow_scene` conserve les contributions par type de producteur, sans
identité persistante nouvelle ni branche sur un slug de module. La fusion
s'exécute après les deux projections dans PostUpdate. Elle exige cible,
shader et horloge concordants, associe exactement meshes/poses et conserve
l'Arc de géométrie si les inventaires sont identiques. Les contributions
non rafraîchies dans la frame sont exclues. La suppression des poissons du
village ne retire plus directement le frame de l'étal, et réciproquement.
Un désaccord ferme le frame et expose un diagnostic, sans remplacement opaque.

Deux tests d'ownership/cache et de rejet sont ajoutés au module après le
snapshot V3 : fusion, réutilisation entre frames, retraite d'un producteur,
expiration, shader incompatible, poses absentes et horloge non finie.
L'exemple `tidewater_fish_shadow_composition_probe` peut exécuter ce vrai module
sans WebView avec des callbacks de projection neutres. Tests non encore
exécutés ; aucun PASS ne leur est attribué. Le candidat lié, la projection GPU,
le picking des deux LOD, les cinq matériaux et la comparaison source restent
à qualifier.

## Construction du candidat lié V3

Le frontend release est lancé sous session 88121 depuis les sources figées
du contrôle V3. Le manifeste SHA-256 reste
`bc7ffaf9ee8f95f963d778ba1da73ba599fb539efad78e0fd7225ae40d151613`.
Le cache WASM distinct est contrôlé libre avant lancement ; le test GPU
natif utilise un autre cache et poursuit sa compilation sans redémarrage.

Les scripts de bindgen, de réutilisation du worker avec vérification de ses
sources et artefacts, et de préparation du frontend natif sont prêts dans
le dossier V3. Ils ne sont pas encore exécutés et ne constituent aucun
verdict de rendu. Une première invocation du constructeur a refusé le
chemin parent déjà construit : les chemins du script sont corrigés, le
reçu parent reste intact. Ce candidat repose sur le snapshot figé du parent
et ne prétend pas inclure les corrections récentes de replay/historique
du domaine ; une consolidation reste nécessaire avant livraison complète.

## Préparation des tests du collecteur

`studio-fish-shadow-composition-test-20261004-v1` contient 1 354 fichiers
figés et hashés, dérivés du snapshot WASM V3 puis complétés avec le vrai
collecteur et son exemple de test. Les géométries des deux producteurs sont
désormais distinguées dans le test pour vérifier aussi leur association
exacte aux poses après fusion. Rustfmt et diff ciblé passent.

`Run.py` vérifie le cache natif libre avant Cargo, impose les mêmes options
de construction et recontrôle toutes les sources après les tests. Il n'est
pas encore exécuté : le constructeur de la mesure GPU végétale 60801 reste
vivant et utilise ce cache. Le constructeur du shell natif V3 est préparé
avec validation des entrées via `Build-StudioCandidate.ps1`, vérification
du frontend et protection de l'ancien binaire ; aucun EXE V3 n'est annoncé.

## Sources natives consolidées préparées

`studio-stand-lod-native-consolidated-20261004-v1` fige 1 353 fichiers :
le snapshot visuel V3 et les quatre fichiers domaine du candidat natif
`studio-fish-native-check-20261004-v3` dont le reçu est réussi et intact.
Les hashes de chaque remplacement sont vérifiés contre sa dérivation.
Ces fichiers corrigent les caches de workspace et le replay/taille
incrémentale des historiques ; la provenance est conservée dans
`derivation.json`.

La préparation du frontend et le constructeur restent non exécutés.
`Prepare.py` exige le frontend V3 lié et son worker vérifié avant d'accepter
les artefacts, puis prépare l'entrée séparée de kit gouverné. Le frontend
WASM en cours de construction garde ses sources V3 d'origine ; les quatre
remplacements domaine sont appliqués au shell natif seulement. Aucun
contrôle combiné, parcours d'édition, rendu ou distribution n'est validé
par cette préparation.

## Frontend lié V3 et worker vérifiés

88121 termine code 0, sources intactes : raw WASM 125 900 557 bytes,
SHA-256 `6f5303f367bbd5d79855ee92881ecb6ef3ce0be8786cad431c54733defc5ca7d`.
37763 termine code 0 après bindgen 0.2.127, assemblage de 3 020 fichiers
et réutilisation du worker dont les sources, Cargo.lock et artefacts sont
concordants. L'inventaire frontend complet est revérifié après assemblage.

Les entrées du candidat natif consolidé sont préparées avec ce frontend
et le kit gouverné distinct. Il reste à construire et exécuter : ces
reçus ne prouvent aucun draw, picking, ombre GPU, édition persistante ou
parité visuelle. La sonde de distance végétale corrigée V3 occupe encore
le cache natif ; aucun second build n'est lancé dans ce cache.

## Enchaînement natif contrôlé lancé

48179 exécute `ContinueNativeCandidate.ps1` et attend le constructeur
V3 précisément identifié (PID 40872, création
2026-10-04T23:39:17.483082+02:00). Après reçu de construction réussi
et intact, il lance les tests réels du collecteur dans leur copie figée,
puis seulement après succès le shell natif consolidé et le parcours
`Invoke-FishStandStudioSmoke.ps1` sur une copie indépendante.

Le staging épingle EXE et les 3 020 fichiers frontend avant exécution.
Le parcours conserve placement/Save/réouverture/playtest et le délai
existant de 120 s ; aucune injection de contenu dans les documents
utilisateur n'est prévue. Le résultat des neuf mesures végétales est
indépendant : cet enchaînement n'en revendique pas la réussite.
Chaque échec arrête les étapes suivantes et reste consigné. Les tests
du collecteur, le build natif et le parcours Studio ne sont pas encore
exécutés au lancement ; aucune parité ou qualification finale n'est acquise.

## Tests du collecteur réussis et défaut de retrait RenderWorld découvert

Le reçu `studio-fish-shadow-composition-test-20261004-v1/result.json`
termine code 0, sources intactes : deux tests exécutés, zéro échec,
ignoré ou filtré. Ils couvrent propriétaires indépendants, association
mesh/pose, réutilisation de géométrie, expiration et contributions invalides.
48179 passe ensuite à la construction native consolidée. Ces tests ne
couvrent pas l'extraction entre les deux mondes Bevy ni le GPU.

Inspection du code épinglé Bevy 0.19.1 : `extract_resource` ne retire pas
une ressource du monde de rendu quand sa source devient absente. Le graphe
utilisait ce plugin pour `fish_shadow::Frame`; retirer le dernier producteur
côté Studio pouvait donc conserver l'ancien frame côté rendu et sa batch.
Le cas est distinct de la fusion/ownership validée ci-dessus.

Le code courant remplace uniquement cette extraction par
`tidewater_object_shadow/fish_extract.rs` : clone du frame présent, retrait
explicite du frame absent. Les autres plugins, poses, shaders et policies
de distance sont préservés. Un test de la vraie fonction d'extraction
couvre publication, mise à jour sans réupload de géométrie, retrait, retrait
répété et réintroduction. Rustfmt et diff ciblé passent après normalisation
des seules fins de ligne des fichiers possédés. Test natif non exécuté.

Nouveau snapshot de 1 355 fichiers :
`studio-fish-shadow-retirement-check-20261005-v1`, contrôle WASM 50918 actif.
Le candidat natif consolidé déjà en construction reste figé sur V3 et
ne contient pas ce correctif ; ses résultats garderont cette limite.
Aucun retrait GPU ni parité n'est encore attribué au nouveau correctif.

## 5 octobre : extraction validée et parcours catalogue à corriger

Le reçu `studio-fish-shadow-retirement-check-20261005-v1/native-test-result.json`
est code 0, sources intactes : la vraie fonction d'extraction Bevy est testée
pour mise à jour, retrait, retrait répété et réintroduction (1 test réussi).
Le contrôle WASM puis la liaison frontend réussissent également ; raw WASM
SHA-256 `7a1293650f6ec24f959cf554412ed2d0e9cf76f9d5470e57dfe6459286592c7b`,
125 783 071 octets ; assemblage et worker vérifiés, 3 020 fichiers.
Ces contrôles ne prouvent pas le retrait d'une ombre visible dans Studio.

Le candidat natif consolidé précédent est construit : EXE SHA-256
`61d6854e3f2f252439ca1aea54cfb5a4dc1c08a16b5f8c2b908b1445368eacc7`.
Son parcours `stand-edit-runtime-v1` échoue avant placement après 120 s :
le harness attend le bouton dédié supprimé. Inspection du frontend :
le catalogue commun ne comporte que le kit GLB gouverné et n'expose pas
l'étal composite malgré le message de disponibilité. Ce résultat reste
`failed_closed` ; sources et documents originaux restent inchangés.

Correction locale en cours : façade de catalogue transitoire alimentée par
les fournisseurs vérifiés, références importées inchangées ; placement,
dépôt et remplacement passent par les commandes communes existantes.
Aucun faux GLB ni identité persistante supplémentaire. L'adapter expose
bâtiments, étal, passerelles, jetée, place et accessoires à partir des
paquets en mémoire. Le harness choisit l'étal du catalogue et le bouton
commun, avec les mêmes assertions de pins, Preview/Apply/Save/réouverture.
Contrôle de compilation isolé lancé sur un snapshot de 1 357 fichiers ;
aucun nouveau parcours Studio réussi ni parité visuelle à ce stade.

### Catalogue : vérification V1 échouée et snapshot V2 cohérent

Le contrôle V1 termine Cargo 101, sources intactes : `authoring_assets.rs`
courant déclare `vault`, absent de l'ancien snapshot. Le reçu d'échec est
conservé ; ce n'est pas une preuve de réussite du catalogue. V2 recopie
les 859 fichiers Rust/manifests actuels des applications et crates, plus
les manifests racine, dans une nouvelle copie indépendante de 1 362
fichiers. Manifest SHA-256
`98a5a6d5d9c033c9487f7b0d322679c17974606d34beec0ff902071f76204d13`.
Le contrôle WASM V2 est lancé, résultat encore attendu.

Le harness courant se parse avec zéro erreur via `Parser.ParseInput`
sur le texte explicitement décodé UTF-8. Une apostrophe typographique
dans le nouveau libellé de bibliothèque exigeait une chaîne PowerShell
entre guillemets doubles ; le snapshot V1 conserve cette version erronée,
V2 inclut sa correction. Diff ciblé sans erreur d'espacement.
La recherche conserve la normalisation accents/séparateurs du catalogue
précédent. Retrait d'un paquet : notification de la liste et refus du
placement/dépôt/remplacement en attente si sa référence est indisponible.
Cette dernière propriété reste à vérifier dans l'application exécutée.

### Catalogue V2 : contrôle WASM réussi

Le reçu V2 `result.json` termine code 0, sources figées intactes ; contrôle
release en 1 min 24 s. Il couvre la façade commune, l'adapter importé et
les voies insertion/dépôt/remplacement, avec le module Vault existant.
Construction du frontend lié, assemblage et préparation du candidat sont
lancés séquentiellement après ce reçu. Aucun placement Studio exécuté
ni nouvelle qualification visuelle n'est encore acquis.
