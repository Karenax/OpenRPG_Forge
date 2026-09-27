# Gate 1 — point 3 : comportement en mouvement

## Correction temporelle autorisée — intégrée le 11 septembre

Le propriétaire demande l'intégration de la règle commune, pas une dérogation :
fondu temporel environ 200 ms, hystérésis, ancien LOD conservé jusqu'à disponibilité
du nouveau, portée objets du kit et forêt individuelle/groupée ; terrain/herbe
conservent leurs chemins distincts. Aucune nouvelle acceptation de gate.

`crates/forge_engine/src/temporal_lod.rs` fournit le contrôleur commun sans
allocation : deux à quatre niveaux, au maximum deux représentations simultanées,
poids complémentaires, attente explicite de disponibilité GPU, arrêt caméra sans
arrêt du fondu, retour au niveau initial si la cible devient indisponible. Les
observations invalides ne modifient pas l'état. Un retournement de caméra finit
le fondu commencé avant de choisir le suivant ; pas de file de demandes cumulées.

Six tests ciblés initiaux exécutés PASS (compilation 1 min 49 s). Ce sont des tests
CPU du contrôleur. **Raccordement ultérieur implémenté** aux primitives du kit,
arbres et buffers de groupes, avec garde de readiness et état partagé des passes ;
contrôle borné natif/Studio PASS dans `R1_TEMPORAL_LOD.md` : fondu terminé caméra
arrêtée, captures inspectées, sans prétendre qualifier toute la fluidité du candidat.
Ne pas transmettre un simple handle CPU comme preuve de disponibilité GPU.

Contrainte constatée dans le Bevy 0.19.1 installé :
`bevy_render/src/view/visibility/range.rs`, `RenderVisibilityRanges::insert`,
conserve les slots de chaque plage distincte pendant la vie de l'application.
Animer continuellement des `VisibilityRange` en fonction de la distance et du
poids ferait croître cette table. Le transport temporel doit rester séparé et
borné ; aucun patch du registre Cargo ni fork de Bevy réalisé.

Les binaires antérieurs restent des preuves historiques épinglées. Ils ne
contiennent pas ce module ajouté et ne qualifient pas son futur raccordement.
Pas de build Studio, test graphique ou changement de carte/asset dans cette étape.

## Diagnostic du tramage persistant — 11 septembre

Le complément Medium/Ultra de `R1_043_CURRENT_NATIVE_STUDIO.md` montre des
troncs tramés alors que les deux plans ont un reçu GPU prêt. Inspection du
chemin réellement utilisé : `VegetationLodRanges::from_governed_profile` produit
une bande de fondu de huit mètres, Medium 20–28 m, Ultra 32–40 m dans ces exports.
`prototype_projection::prepare` donne la même origine de placement aux LOD.
Le vertex shader appelle `get_visibility_range_dither_level` de Bevy 0.19.1,
puis le fragment opaque appelle son `visibility_range_dither`. Ces calculs
utilisent la distance caméra/racine et un motif fixe 4×4, **aucune durée écoulée**.
Un placement immobile dans la bande conserve donc son fondu partiel indéfiniment.
Il ne faut pas attendre qu'une préparation GPU supplémentaire le rende opaque.

Contrôle arithmétique exécuté : 17 niveaux × 16 seuils, **272 cas PASS**, un
seul des deux masques complémentaires conserve chaque seuil. Cela écarte une
erreur de complémentarité du masque sur des silhouettes superposées ; cela ne
prouve pas que les silhouettes, normales ou déformations des deux LOD coïncident.
Le prépass de normales est déjà installé pour conserver le rejet profondeur/
couleur. Ne pas répéter son ancien correctif comme s'il était absent.

Conclusion bornée : comportement spatial persistant établi dans le code, pas
preuve de chaque pixel ni qualification temporelle. **Aucun correctif de rendu
livré par ce diagnostic.** Une correction durable doit traiter le mode de
transition (par exemple un fondu temporel borné avec hystérésis), partager la
couverture profondeur/couleur et conserver les budgets, le batching et les
placements. Cette solution n'est pas encore implémentée ni validée ; réduire
arbitrairement la bande ne ferait que déplacer le problème. Le HLOD par lots
possède son propre calcul par placement et doit rester cohérent avec le LOD
individuel. Pas de nouveau lancement, build, asset ou publication dans ce diagnostic.

10 septembre 2026, branche Rust, local non publié. **Corrections et intégration
fonctionnelle terminées ; qualification temporelle complète PARTIAL.** Watchdog
et raccord de teinte corrigés, parcours final natif/Studio vérifiés ci-dessous.
Tramage résiduel et fluidité restent réservés, pas de clôture complète inventée. Point 2 reste
WAIVED/ACCEPTED ; ce lot ne rouvre pas ses seuils de performances.

## Changement et périmètre

Le collecteur existant `scripts/Capture-BridgeVideo.py` accepte maintenant
`--cloud-profile`, épingle ses octets et transmet le chemin comme un seul argument.
Il peut ainsi enregistrer le candidat combiné, sans retirer ses nuages.
Test associé ajouté dans `scripts/Test-BridgeVideo.py` : **5 tests PASS**, comprenant
les refus de preuves corrompues déjà présents. Aucun code moteur, shader, asset ou
document de carte modifié. Aucune recompilation Rust nécessaire pour ce changement
du collecteur ; les anciens tests Rust ne sont pas présentés comme rejoués.

## Exécuté et contrôlé

Huit exécutions natives séquentielles : Low, Medium, High, Ultra × clair/pluie.
Toutes terminent avec code 0, sans événement ERROR, traversée physique réussie,
20 instances du kit et 36 colliders prêts, bindings terrain/joueur/pont conservés.
Chaque exécution produit 271 PNG consécutifs 1920×1080 et un MP4 de 9,033333 s.
Le collecteur vérifie dimensions, séquence, diversité des images, readiness des
pipelines connus, arrêt propre, encodage et conservation des sources épinglées.

**Horloge hors temps réel, 30 images/s imposées** : les nombres FPS de ces vidéos
ne mesurent pas les performances. Ce sont des séquences disponibles pour inspection
des animations, pas une preuve d'absence de hitch. Le champ conservateur
`quality_and_forest_projection_verified=false` du manifeste ne doit pas être lu
comme un reçu GPU global ; les observations CPU détaillées sont dans
`render_projection_observations`. Les reçus GPU des cycles du point 1 restent une
preuve séparée, pas un reçu atomique pour chaque image de ces parcours.

Racine locale : `E:/OpenRPG_Forge/.gate1-evidence-runtime/`.
Préfixe des dossiers : `r1-motion-point3-20260910-` ; suffixes `clear`, `rain`
pour High, puis `low-clear`, `low-rain`, `medium-clear`, `medium-rain`,
`ultra-clear`, `ultra-rain`. Chaque dossier conserve `manifest.json`,
`runtime.jsonl`, `frames/`, `bridge.mp4` et `lod-audit.json`.
Synthèse rehashée : `r1-motion-point3-20260910-summary.json`.
Reproduction locale complémentaire : `Run-Point3-Remaining.ps1` et
`Audit-Point3.ps1`, dans cette même racine externe.

| Profil | SHA-256 vidéo claire | SHA-256 vidéo pluie |
| --- | --- | --- |
| Low | `8a979e405b779a8c8607123ca35278d2afea02751c81de3dd5870ce8d8cdf5c8` | `678e8aeb85c94e57f61aeefae7f678596cedcd566e4988d8a91aa41d3a9c35f0` |
| Medium | `a892ca3ddc08f5c7a506bbdb4f518152111cc0ad011848cb97b4d03c42ee13f4` | `6f0984438a6b8a4a6b2e4ec8f29dda709efaaea5ba831dd4616482eb471d1828` |
| High | `3037707d956cea56b88b4eebe6e656c8106d8346e331c47b9faf49298e8619b5` | `150029b6ae79827141a4d901b47fdee35ca3bba7da8346b55c2b3ad5151fe850` |
| Ultra | `fd9c2f069ff9fbaf70071382e1c6d638394693d360b1fe697be0f9c35d6361ee` | `c011483e2975c981ea6cfee298f8f21adbfe23fb7fadc65f75dd73a0eaee8b6e` |

Runtime réutilisé du point 1 : `r1-effective-profiles-20260909/forge-runtime-v2.exe`,
SHA-256 `4df11013d9dab2365c475e371e1e1ffa3833916690fd0495c8196cf414002bcb`.
Kit `r1-atlas-cropped-runtime-kit-20260909` ; medium
`r1-water-medium-integration-20260909/clear-medium.json` ; nuages
`examples/visual_valley/cloud-layer-preview.json`. Hashes de toutes les entrées
dans chaque manifeste, revérifiés après la série. Aucun asset Cadle.

## Inspection et couverture LOD

PNG effectivement ouverts : image 135 des huit séquences ; High clair 72/96/120
et High pluie 0/270 en complément. Ce contrôle échantillonné **n'est pas une
lecture exhaustive des huit vidéos**. Terrain, pont, personnage, eau et forêt
restent présents dans ces images. La pluie et l'humidité apparaissent dans les
vues pluvieuses ; les budgets de pluie varient par profil (2048/4096/8192/16384).
Le brouillard reste le fallback distance authoré de l'ADR-0011, pas une
qualification de volumétrie expérimentale. Le ciel accepté n'est pas retouché.

`Analyze-BridgeLodTrace.ps1 -RequireScreenProjection` exécuté pour les huit
séquences : couverture des 271 demandes de capture et identités vérifiées.
High clair traverse intégralement 12 bandes near (24–32 m) et 181 bandes far
(172–180 m). Respectivement 12 et 153 racines passent dans le volume de projection.
Cela prouve les distances parcourues, **pas la visibilité des couronnes ni la
sélection LOD GPU**. Exemple inspecté : arbre `a717f216-a7e8-59d0-bd7f-8ec92e9d1161`,
images 72/96/120, distance 31,86/27,98/24,14 m.

Réserve visible : tramage sur les transitions et différence de teinte/densité
entre feuillages proche et moyen. Aucun verdict « transition imperceptible ».
Le vent lointain n'est pas isolé de la caméra, du tramage et de l'occlusion par
cette traversée ; l'ancien résultat non concluant n'est pas remplacé par PASS.

## Contrôle séparé en temps réel

`Run-Point3-Live.ps1` lance le même binaire, High/pluie, kit/forêt/eau/nuages,
contrôleur normal `--walk-proof 1800:1:0.22`, sans horloge manuelle.
Départ `(14.999655, 5.1502533, -224.99998)` ; arrivée
`(-132.49701, 2.5992887, -203.51773)`, au sol, vitesse horizontale nulle après
freinage. Environ 149 m, événement de fin après 32 secondes. Code 0, sans ERROR.
Fenêtre réelle observée avec Computer Use ; PNG final ouvert et inspecté.
La fenêtre observée et la capture finale sont en 2560×1369, **pas un benchmark
1080p**, ni une preuve vidéo exhaustive du déplacement. Les métriques glissantes
restent diagnostiques et ne modifient pas la dérogation du point 2.

Dossier `r1-motion-point3-20260910-live` :

- `live.png` : `3be6b591e484b8bd5e799029c00eab9411f110224df2655533b6aaede9e065ac` ;
- `runtime.jsonl` : `919a19344297e878be2ddc03f421f63fb92091f569a6cb518e7785de3b64d90a`.

PID possédé 6760 terminé ; absence du binaire de preuve vérifiée après les essais.
Aucun navigateur, serveur ou processus graphique de ce lot laissé ouvert.

## Complément exécuté — 10 septembre, correctif et preuves ciblées

Les paragraphes précédents décrivent la campagne initiale, avant le correctif.
Le shader partagé applique désormais le poids LOD en opacité au feuillage BLEND,
sans ajouter le quadrillage écran réservé aux surfaces opaques/masquées. Le bois
et les ombres gardent leur comportement ; textures, géométrie, distances LOD,
placements et collisions ne changent pas. Le poids Bevy reste quantifié sur
16 niveaux : ne pas annoncer un fondu continu ou imperceptible.

Nouveau parcours High/clair avec eau/nuages : 271 images FHD, zéro ERROR, code 0.
Images 72/96/120 effectivement inspectées : quadrillage du feuillage réduit,
mais différence de densité/teinte encore visible entre LOD0 et atlas LOD1.
Ce correctif seul ne qualifie pas cette transition. Vidéo `bridge/bridge.mp4`,
SHA-256 `c4b4982ee3255f5ab660d20108369e38ffd6c892eb5f7accd67489010799082d`.
Runtime intermédiaire conservé `runtime.exe`, SHA-256
`8d8b966eaf898831e867cba2e0e5c53e4420c09a2cb24f4211f0862c6ea5c7a8`.

Racine de tous les nouveaux fichiers ci-dessous :
`E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-motion-fix-20260910/`.

### Vent lointain : isolation temporelle bornée PASS

`--fixed-proof-steps` attend les assets/formes physiques avant d'avancer l'horloge
par pas de 0,25 s. `--fixed-proof-view-input` accepte uniquement un changement
de caméras de diagnostic : toute autre différence du WorldSketch est refusée.
Le document canonique chargé et le hash lié au kit restent intacts.
Entrées invalides, absence de mode borné et modifications du terrain testées.

Trois lancements indépendants aux phases 1 s / 3 s / 1 s, caméra immobile,
36 colliders initialisés dans chacun, codes 0 sans ERROR. Cadrage téléobjectif
6° visant un arbre existant à environ 197 m ; aucune édition de carte.
ROI feuillage `(1000,50)-(1600,500)` et témoin sol `(600,1020)-(1000,1070)` choisis
sur la première image avant inspection de la phase différente. Écarts moyens
RGB 8 bits : effet **13,03570**, retour **0,03001**, témoin effet **0,22120**,
témoin retour **0,04100**. Seuils prévus >3 / retour ≤2 / témoin ≤2 satisfaits.
Images 1 s et 3 s ouvertes ; déplacement des feuilles visible.

Protocole A/B/A déterministe, **pas** le protocole statistique à neuf images de
`Compare-OpticalSamples.ps1`. Pas de qualification photométrique ou artistique
de la scène combinée : eau avancée et nuages non activés dans cette isolation.
`wind-protocol.json`, `wind-result.json`, `tree-{1,3,return}.png` et logs conservés.
Échecs de préparation conservés : premier cadrage au sol inutilisable ; premier
essai de caméra refusé par le hash du kit, sans assouplir ce contrôle.
Runtime final natif `runtime-v2.exe`, SHA-256
`2f1e169433d245681ee7afb312c35992ad414475eb5df9d2f59c94d91e26d720`.

### Changements de qualité pendant la marche

**Natif, fonctionnel PASS** : High/Low/Medium/Ultra/High, 180 observations
compatibles consécutives et reçu GPU par étape, personnage à 5 m/s et positions
distinctes, sources stables, cinq PNG, code 0 sans ERROR. Deux trajets conservés :
`moving-cycle.stderr.jsonl`/`cycle/` finit avec une image masquée par un bâtiment,
donc pas une preuve visuelle finale. Le second, `forward.stderr.jsonl` et
`cycle-forward/`, reste dans la vallée dégagée ; images 0/2/4 inspectées, sol,
personnage, forêt et eau présents, sans traînée manifeste dans ces instantanés.
Positions z successives : −205,94 / −188,96 / −166,43 / −132,50 / −112,96 m.
Ce ne sont pas une vidéo des transitions ni une preuve d'absence de hitch.

**Studio, cycle fonctionnel PASS borné, parcours complet FAILED** : mode explicite
`forge_probe=bridge&forge_quality_motion=1`, avec eau forward, sans nuages dans
cet essai. La demande passe par la même file que les boutons de qualité.
Trois observations après huit images GPU compatibles en mouvement : High/Low/High,
ticks physiques 163/246/315, séquences GPU 1/2/3, générations 4/6/8, sept domaines
appliqués et cinq sources identiques. Positions et translations physiques non
nulles exportées dans `studio-motion-v2/diagnostic-isolated.json`.
SHA-256 du diagnostic :
`1ed7345bcb3de90036cfa4a0c6117d8414f82f8e6a7e784635d6e6f28fb8eb47`.
Lors du dernier reçu, la traversée était encore en cours, 265 ticks au sol,
224 contacts, 21,57 m, sans erreur physique signalée.

La traversée s'arrête **ensuite** à 24,15 m sur son watchdog de 30 s : elle n'est
pas réussie. Un premier essai avec compilation concurrente avait échoué avant le
dernier reçu ; `diagnostic-failed.json` conserve High/Low uniquement. L'essai isolé
ne change ni binaire ni délais. Aucune attribution certaine du ralentissement à
la compilation ; aucune extension de la dérogation performances.
Captures réelles Studio inspectées après arrêt : forêt/pont/personnage visibles,
réserve de teinte/densité confirmée. Pas de qualification exhaustive scintillement.

WASM SHA-256 `b48c91ad6ecf19d55b76b551998562c9638cfcbc5332b2a9d49c058467c6b338` ;
exécutable Studio SHA-256
`9378cd92f6dcccc51f6be4021b70bc6f0ca3be1b2f5f389d954ec62ab069b144`.
Manifestes `studio-motion-v2/build-proof.json` et frontend épinglé conservés.

### Contrôles et nettoyage

Workspace : **787 tests PASS, un ignoré** après modifications moteur/runtime.
Contrôle final affecté : runtime 99 + intégration 1, Studio 55 + shell 4 PASS.
Clippy workspace/all-targets/all-features strict et Clippy WASM/web strict PASS ;
fmt check PASS ; build natif, Trunk WASM et package Tauri PASS.
Tests d'entrées packaging PASS, ajout d'une route valide et d'un refus sans
BridgeProbe. Aucune nouvelle dépendance. Avertissement tiers futur connu
`proc-macro-error2 2.0.1`, pas présenté comme erreur projet résolue.
Les processus natifs possédés sont terminés ; les arbres de processus Studio
29492, 9696 et 27792, dont WebView2, fermés et absence vérifiée. Aucun serveur
ou onglet de navigateur ajouté ; aucun processus graphique remis ouvert.

## Restant avant clôture du point 3 (état avant correction finale)

1. Qualifier/corriger la transition visuelle proche/moyenne identifiée, en conservant
   des règles réutilisables et les placements, pas en retouchant cette carte à la main.
2. Résoudre/qualifier l'arrêt du parcours Studio sur son watchdog, sans transformer
   le cycle de qualité réussi en réussite de traversée ou en benchmark PASS.
3. Examen temporel des transitions/traînées/scintillements après correction LOD :
   les reçus GPU et instantanés ci-dessus ne prouvent pas leur imperceptibilité.

L'isolation du vent lointain et les changements fonctionnels de profil en mouvement
sont désormais prouvés dans les limites décrites ; ne pas les relancer inchangés.

Les preuves natives réutilisables sont désormais produites ; ne pas répéter cette
matrice sans changement pertinent. Point 3, R1-014, réserve R1-040 et Gate 1 restent
ouverts. Aucune extension de la dérogation du point 2, validation humaine inventée,
publication Git ou Gate 2.

## Correction finale du raccord et du watchdog — 10 septembre

Racine des preuves :
`E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-motion-final-20260910/`.
Les échecs et vidéos précédents sont conservés, pas réécrits en PASS.

### Préparation WebGPU et traversée Studio

Le pont attend huit observations fraîches de pipelines prêts avant de démarrer
sa simulation de qualification. Cette attente reste dans le délai de préparation
existant de 120 s. Le délai de mouvement reste 30 s ; une modification de qualité
après le départ ne suspend ni les contrôles ni ce délai. Test ajouté pour absence
de readiness, échantillons répétés, huit observations et compilation après départ.

Essai réel `studio/diagnostic.json` : **completed, 518/518 ticks au sol,
434 contacts, 42,528 m**. Cycle High/Low/High en mouvement avec trois reçus GPU,
positions distinctes et sources stables. Préparation froide : 31 855,7 ms,
95 pipelines, huit observations stables, aucun pipeline en échec/en attente à
la sortie. Le préchauffage seul dépassait donc les 30 s du délai de déplacement.
Ce résultat résout l'échec du watchdog ; il n'est pas un benchmark PASS.
Diagnostic SHA-256 :
`10047b0be8a17412d36f5f4968473b22d12f4e63d7bc4d3d63bc22e1267f8ca5`.
Capture réellement exportée et inspectée : `studio/viewport.png`.
Cet essai comprend l'eau avancée, sans nuages ; il précède le raccord des ombres
décrit ci-dessous et ne prouve pas à lui seul ce dernier dans Studio.

### Correction générique des dérivés et des ombres

L'atlas encode ses normales dans right/up/front, alors que les UV exportés et
le signe des tangentes reconstruisent right/minus-up/front. Le convertisseur
inverse le canal G du normal PNG avant padding et refuse un encodage inconnu.
Test de reconstruction TBN avec deux orientations, conservation R/B/A : huit
tests du convertisseur PASS. Dérivés oak/pine séparés ; sources intactes.
Khronos glTF Validator 2.0.0-dev.3.10 : zéro erreur pour chacun, neuf warnings
chacun sur des primitives source sans tangentes. Réimport Blender 5.2.1 LTS et
images source/candidat inspectés. Script exact ayant produit les dérivés archivé
dans cette racine ; le garde d'encodage ajouté ensuite ne change pas ces octets.

Cette correction seule ne supprimait pas le blanchiment. La comparaison avec
le kit source (`bridge-source/`) reproduit le défaut : la cause dominante était
la suppression des shadow casters dès LOD1. LOD0 et LOD1 projettent désormais
des ombres, avec extinction bornée à la fin du medium ; LOD2/batches lointains
restent exclus. Aucun changement de placement, de carte, de distances LOD,
de passe graphique ou de teinte artificielle. Davantage de casters : aucun gain
de performances revendiqué, aucune extension de la dérogation du point 2.

Parcours `bridge-shadow/` : 271 images FHD, code 0, zéro ERROR, eau/nuages et
nouveau kit. Runtime SHA-256 :
`50991d48b8b00716847f805aa821f72fdd5ded9e9bb52b5d2dfba3f4d69e5e8f`.
Images 72/96/120/161/162/200/270 ouvertes : raccord vert proche/moyen nettement
plus cohérent, disparition du fort blanchiment dans ces vues, forêt/sol/pont/eau
maintenus. Les différences de silhouette et le tramage de certains troncs restent
des limites du LOD ; ces images ne démontrent pas une transition imperceptible.
Les vidéos à horloge imposée ne démontrent toujours pas l'absence de hitch réel.

### Intégration Studio du raccord final : PASS fonctionnel

`studio-shadow/forge-studio-candidate.exe`, SHA-256
`4ffdf9ba3638eee6636a46213a85aa38a9fbfe0ae68b73b140e719291b7d893c`,
embarque le WASM final
`5cfc60f7cfbb389dd09a9c94056ef2660270b63a8ae44ca6fc390b9350da1007`
et le nouveau kit. Build Trunk et package Tauri réussis ; manifeste BUILD_ONLY
conservé sans le transformer en verdict de runtime.

Application lancée réellement, captures en début/milieu/fin observées : ombres
et teintes raccordées, eau et forêt présentes, **completed 518/518 ticks au sol,
434 contacts, 42,528 m**. High/Low/High reçus GPU aux ticks 186/278/381 avec
déplacements non nuls. JSON exporté via le bouton du Studio, relu sur disque :
`studio-shadow/diagnostic.json`, SHA-256
`ee34ff64ee817a3509163be01c21e35dcdbbfef10ba121768ae6d206c8b62ad7`.
Capture PNG 1408×966 exportée et inspectée : `studio-shadow/viewport.png`,
SHA-256 `744211429ef8b401f3a54e423dc57d219614bbde0a635cb1bba08fcbd2efe1a7`.
Le dernier état de traversée est dans `displayed_statuses.bridge_probe` ;
le snapshot interne du cycle correspond à sa fin pendant la traversée.

Limites : eau avancée, pas de nuages dans cet essai Studio. Mesure glissante
affichée de 120 images : moyenne 51,08 ms, p95 57,40 ms, build debug ; **pas une
preuve de fluidité ni une comparaison contrôlée de performances**. Le coût des
casters supplémentaires n'est pas dissimulé. La dérogation du point 2 demeure
distincte ; aucun seuil de performances n'est déclaré réussi.

Clippy workspace/all-targets/all-features et WASM/web stricts PASS, fmt check
PASS, suite workspace PASS (un test de timing ignoré), neuf tests du pont PASS
après séparation de sa readiness en ressource dédiée. Une première invocation
ciblée sans feature qualification a sélectionné zéro test : elle ne compte pas
comme preuve ; les neuf tests avec features actives ont ensuite été exécutés.
Huit tests atlas et cinq tests collecteur vidéo PASS. Aucune dépendance ajoutée.
L'avertissement futur de la dépendance `proc-macro-error2 2.0.1` reste connu.

PID Studio 15764 et descendants 33464/16700/8708/16044/27788/25008/12324 arrêtés,
absence vérifiée ; précédent Studio 30732 et descendants également fermés.

### Rejeu borné des profils affectés

Après High/clair, trois parcours séquentiels supplémentaires du même
`runtime-shadow.exe` et du même kit, avec eau/nuages, sans compilation concurrente :
Low/pluie, Medium/clair, Ultra/pluie. Chacun termine code 0 sans ERROR, 271 PNG
FHD et MP4 9,033333 s vérifiés, entrées épinglées inchangées. Images 96 et 270
de chacun ouvertes : scène et ombres présentes, densité/pluie modulées selon
le profil. Aspect humide plus clair dans le feuillage pluvieux, à distinguer du
défaut de shadow caster corrigé. Tramage encore visible sur certains troncs.
Ce n'est ni un rejeu des huit combinaisons ni une inspection exhaustive des vidéos.

| Dossier | SHA-256 vidéo |
| --- | --- |
| `bridge-shadow` (High/clair) | `a48a2cc29fdf618f0e757fd0354b62fc2da6c6bfd66d643f8b4fba0faf145899` |
| `bridge-shadow-low-rain` | `ee45e224987c9b2eecc9d85d4f474b277df49cef6d1971ae8d94e75fd0c497ee` |
| `bridge-shadow-medium-clear` | `d45e624e9000d0c798c98a96d7e8028d9ce0b8cb5e6651f5ea574ee71b84ac03` |
| `bridge-shadow-ultra-rain` | `e0fd42e43a3241b19dbeebef1e7d594c47f992e93e0c99a2b5100541e4ad504b` |

Collecteurs terminés ; absence du runtime de preuve vérifiée. Aucun processus
graphique, navigateur ou compilation de ce lot laissé actif. Aucun commit/push.

### Verdict de remise

- **Implémenté et vérifié** : correction générique du fondu BLEND, normales atlas,
  continuité des ombres LOD0/LOD1, préparation froide du pont ; traversées et
  changements de qualité en mouvement natif/Studio ; vent lointain isolé.
- **Encore réservé** : aspect temporel des transitions (tramage/silhouettes) et
  fluidité des changements de profil. Les captures, reçus GPU et vidéos à horloge
  imposée ne constituent pas une qualification exhaustive de ces critères.
- **Non autorisé par ce verdict** : clôture globale de Gate 1, passage Gate 2,
  promotion automatique du kit dérivé, performance PASS ou acceptation humaine.

Le prochain examen doit utiliser ces séquences/candidat et porter sur les réserves
précises, pas relancer les contrôles déjà réussis. La validation de la vue intégrée
précédemment donnée par le propriétaire reste acquise dans sa portée ; elle n'est
ni redemandée ni étendue aux nouveaux critères temporels.
