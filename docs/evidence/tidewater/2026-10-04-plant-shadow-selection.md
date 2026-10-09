# Portée des casters végétaux : diagnostic borné

Le code courant `tidewater_plants/scene/shadow.rs` sélectionne cinq groupes,
exclut le sous-bois et utilise seulement leur géométrie proche. La passe conserve
les fenêtres LOD du paquet : palmiers 120 m, bananiers 100–120 m, monsteras et
broadleaf 85–105 m, canopée nominale 65 m avec fondu tramé 61,1–68,9 m. La sélection CPU et le
masque/déformation shader constituent donc deux limites distinctes. Les cascades
10/60/400 m ne prouvent pas une portée végétale de 400 m.

Le script `scripts/tidewater/audit-plant-shadow-selection.py` reproduit le
filtrage CPU, l'ordre stable des cellules puis des distances float32 et la limite
de capacité sur les streams acquis non retouchés. Il vérifie leurs SHA-256 et
cardinalités. Le paquet Studio contient les descriptions, mais reconstruit ses
instances depuis les documents canoniques : les streams acquis sont lus
séparément depuis `direct-lighting-canopy-001`. Le premier essai sans cette
séparation échoue sur fichier absent ; aucun résultat ne lui est attribué.

Reçu : `.placeables-smoke-game-fish-20261004-v1/visual-stand-v3/plant-shadow-selection.json`.
Caméra de référence historique `(53.60006, 4.44773, -76.99976)` :

| Groupe | Sélection CPU | Soumis après capacité | Plus grande distance planaire |
|---|---:|---:|---:|
| Palmiers | 56 | 56 | 129,96 m |
| Bananiers | 79 | 79 | 125,11 m |
| Monsteras | 1 | 1 | 77,36 m |
| Broadleaf | 47 | 47 | 114,10 m |
| Canopée | 4 | 4 | 52,96 m |

Aucune saturation de capacité n'apparaît sur cette population et cette caméra.
Les instances sélectionnées au-delà de la fenêtre shader ne garantissent pas
un dessin. Ce relevé ne mesure ni les retouches de la zone Studio actuelle, ni
les fragments de profondeur, ni les ombres reçues sur le sol. Il ne permet donc
pas encore d'attribuer le défaut visuel à une seule limite ou de choisir une
nouvelle portée sans comparaison appariée.

Suite nécessaire : relever les casters de la projection auteur actuelle avec
la caméra de jeu et inspecter leur atlas/réception à distances appariées. Garder
la coupure des végétaux très lointains demandée par le propriétaire ; qualifier
séparément toute extension de la fenêtre d'ombres proche.

## Diagnostic de la sélection auteur raccordé dans le code

`Source::select_observed` renvoie les streams et les observations issues du
même choix d'indices, avant upload. Studio conserve cette observation quand
il prépare les ombres animées et l'ajoute à `plant_caster_selection` dans ses
diagnostics publics. Elle comprend la caméra utilisée, la source/génération,
le groupe, les candidats, les instances soumises, les pertes de capacité,
la fenêtre LOD et la plus grande distance 3D des bases. Le nombre de bases
au-delà de la fin LOD reste une observation géométrique : il n'est pas présenté
comme un nombre de fragments rejetés. La retraite de la population efface
l'observation. Aucun rayon, masque ou rendu d'ombre n'est modifié.

Le harnais natif V3 compile le module actuel avec ses sous-modules et ses
dépendances reçues. Les deux tests de sélection passent : seuil strict du rayon,
cohérence des streams observés avec l'entrée de rendu, limitation de capacité,
distance du dernier arbre soumis et refus d'une caméra non finie. Les V1/V2
échoués sont conservés : résolution des chemins de modules puis mélanges
d'identités de crates ; V3 épingle les bibliothèques compatibles. Le reçu
`plant-shadow-observation-20261004-v3/result.json` confirme les entrées intactes
pendant le test. Un formatage Rust du bloc de distance suit ce reçu, sans
changement de comportement.

Cette validation est isolée, pas un build Cargo complet ni un passage Studio.
L'adaptateur Studio doit encore être compilé et le nouveau diagnostic observé
dans un candidat avant d'en tirer un verdict sur la zone actuelle. Les fichiers
modifiés passent le contrôle ciblé `git diff --check` ; le contrôle global
précédent avait signalé des fins de ligne dans `gameplay_navigation.rs`, qui
restent hors de cette modification et sont conservées.

Le snapshot `studio-plant-shadow-observation-20261004-v1` conserve les
1 351 fichiers du candidat GameFish V1 vérifiés, avec trois remplacements
canoniques : sélecteur végétal, adaptateur Studio et publication des diagnostics.
Son manifeste épingle chaque fichier. Le contrôle Cargo WASM de Studio est
lancé sur ce snapshot, cache distinct des compilations natives observées.
À ce checkpoint la session 79017 est encore active ; aucun PASS Studio,
exécutable nouveau ni observation runtime n'est attribué à cette compilation.

## Contrôle Studio réussi et comparaison au code source

La session 79017 termine code 0. `result.json` confirme le contrôle Cargo WASM
de Studio et les 1 351 fichiers figés intacts. Cela valide le raccord compilé,
sans application liée ni exécution GPU. La construction du frontend lié est
lancée ensuite (session 89973) ; les cinq scripts de construction/assemblage
préparés restent séquentiels et vérifient les reçus et empreintes nécessaires.

Le clone source `.tidewater-integration/upstream` est contrôlé directement :
HEAD `1438b1abfcaee3267092b75573014f4d9b4a983c`, statut Git propre.
`src/world/Vegetation.js` SHA-256
`adeb042cabe46bd741cd85dc7c63a01f7eacceeea45a20d869a35f18581f439e`
définit lui-même `TREE_NEAR = 65`, `PALM_NEAR = 120`, les fades 100–120 m
des bananiers, la projection d'ombres des cinq groupes proches et l'exclusion
du sous-bois. La présence d'une fenêtre de 65 m côté Rust ne constitue donc
pas, seule, une divergence démontrée de la référence. Le défaut perçu doit
être confronté aux casters proches réellement présents et à la réception sur
le sol, dans un cadrage apparié. Les verdicts de parité restent ouverts.

## Préparation de l'observation en jeu

La fixture `native-plant-shadow-probe-v1` contient 26 fichiers vérifiés et un
profil WebView2 neuf. `PrepareRuntime.py` confirme les sources parent intactes
et exclut les locks. Le helper `MeasureRuntime.ps1` conserve les gardes du
parcours natif V3 déjà éprouvé : acquisition de la bibliothèque, clic public
Jouer, simulation réellement active après reconstruction, puis cadence des
trois cascades et contrôle de comparaison. Il attend aussi les cinq groupes
du nouveau diagnostic. Son nettoyage ne vise que son propre processus et
vérifie les empreintes de la copie et de la source originale. Parse PowerShell
PASS ; cette préparation n'est pas un lancement ni une preuve runtime.

`scripts/tidewater/evaluate-plant-shadow-selection.py` évalue les observations
avant/après : groupes exacts, source identifiée et constante, caméra concordant
avec la pose publique, comptages/capacités cohérents, distances et fenêtres
finies. Son auto-test synthétique passe et refuse les observations absentes,
caméras périmées, comptages invalides et changements de source. Il conserve
`gpu_depth_verified=false` et `visual_parity=false`, même si le contrôle CPU
réussit. L'évaluateur existant de cadence et readback receiver reste séparé.

L'inspection de `src/world/vegetation/InstanceLOD.js` confirme que les parties
source omettant `castShadow` ont une valeur par défaut `false` : les géométries
lointaines ne sont donc pas implicitement des casters. Le seuil CPU utilise
les rayons par instance et la grille, tandis que le vertex shader détermine
la fenêtre visible. Cette séparation est conservée dans le diagnostic Rust.

À ce checkpoint la session de frontend 89973 reste observée active ; la CPU
du compilateur moteur avance. Aucune seconde construction n'est lancée dans
ce cache. Le candidat natif et le relevé réel restent à produire après la
réussite du lien WASM.

## Enchaînement du candidat lancé sur processus identifié

Le processus Python du frontend est identifié par PID 7156, chemin d'exécutable
et ligne de commande exacte `BuildFrontend.py`. `ContinueCandidate.ps1` ouvre
ce processus vivant, conserve sa date de démarrage et attend sa terminaison.
Il exige ensuite le reçu frontend réussi et intact, ainsi que le hash inchangé
du script d'assemblage, avant les étapes séquentielles bindings, worker vérifié,
validation des entrées natives et construction desktop. Il ne relance pas le
frontend et ne lance aucune nouvelle compilation dans son cache pendant cette
attente. Parse PowerShell PASS ; session d'enchaînement 49994 observée active.

La session frontend 89973 passe ensuite du compilateur moteur au compilateur
Studio ; le reçu lié est encore absent à ce checkpoint. Le code shader acquis
confirme que `setupSourceLobes` initialise aussi les fenêtres arbres/arbustes
65/45 m : aucune absence d'initialisation n'est déduite du remplacement de
`setupSource` dans la passe de profondeur. Aucune qualification visuelle
nouvelle n'est revendiquée avant le test du candidat.

## Frontend lié terminé

La session 89973 termine code 0 ; `frontend-result.json` confirme les sources
figées intactes et le lien WASM réussi. L'artefact fait 125 820 321 octets,
SHA-256 `ba957becf9579857cc54a3fc8c76562bcd80d71eb877caa9d16d20d3c8b1c2b8`.
Le processus prédécesseur est terminé et l'enchaînement 49994 passe effectivement
à l'assemblage. Ce reçu ne constitue pas une exécution navigateur ou une
qualification visuelle ; la construction native et le relevé en jeu suivent.

## Assemblage réussi, reprise des entrées natives sur chemin court

L'assemblage wasm-bindgen et la réutilisation du worker vérifié terminent code 0.
`frontend-ready.json` inventorie 3 020 fichiers épinglés. Le WASM de bindings
porte le SHA-256 `5c6d358cb4580a940b6583198add0284b5f023ffe86d17b57e4513665c0b9a59` ;
il est distinct de l'artefact brut précédent. Toutes les sources du worker et
ses artefacts sont vérifiés avant copie.

L'enchaînement 49994 termine ensuite code 1 : la copie des entrées du
constructeur vers `validation-frontend` dépasse la longueur de chemin Windows
pour un fichier de végétation. Le traceback, la copie partielle et le reçu
d'échec restent conservés. `PrepareNativeValidationV2.py` utilise le dossier
court `v2`, conserve toutes les validations et termine code 0. Aucun frontend
n'est reconstruit pour cette reprise.

Le constructeur canonique retourne ensuite `INPUTS_VALID`, 3 020 fichiers,
sans revendiquer une construction par ce seul contrôle. La construction native
est réellement lancée en session 59662 ; son log atteint les crates Forge et
Studio. Son reçu final et l'observation du viewport restent attendus.

## Candidat natif construit, première transition de test trop précoce

La session 59662 termine code 0 après 3 min 23 s. Le reçu confirme les sources
figées et les entrées frontend intactes. SHA-256 de l'exécutable :
`5871617dfda5198afc60182b009ed164f21cd6ee5ed03bfd32e77dc1eb07499a`.

Le premier essai `native-plant-shadow-probe-v1` acquiert la bibliothèque et
synchronise les collisions, puis échoue sur expiration du transport CDP lors
du parcours vers Jouer. Son instantané `before-playtest.json` montre encore
`pending=true`, zéro passe encodée, trois compteurs nuls et aucune sélection
végétale publiée. Il ne prouve donc pas une entrée en jeu. Le helper avait
accepté la disponibilité CPU/collisions sans attendre la projection d'ombres.
Le traceback et tous les relevés restent conservés. `cleanup.json` confirme
les 26 fichiers originaux et copiés intacts, et fermeture du seul processus
lancé. Aucun message d'erreur n'est présent dans stdout/stderr du candidat ;
cela ne constitue pas une qualification de son rendu.

Le V2 ajoute une garde de projection initiale : génération engagée, passes
encodées positives et cinq groupes publiés avant clic Jouer. La lecture du
snapshot tolère 60 s et les erreurs conservent étape, pile et position.
Les critères post-transition, cadence et readback restent inchangés. Parse
PowerShell PASS et nouvelle fixture de 26 fichiers vérifiés. Ce replay est
lancé séparément ; son résultat runtime reste à observer.

## Replay V2 : observation runtime réussie

Le replay `native-plant-shadow-probe-v2` termine code 0. Les évaluations
`shadow-runtime-evaluation.json` et `plant-selection-evaluation.json` sont PASS.
La simulation post-transition est réellement active. Les passes encodées
avancent de 37 à 69, les trois couches de [37,21,11] à [69,37,19], et le vent
avance sur la même génération engagée. Le readback GPU des paramètres de
réception est courant et fini ; il ne lit pas la texture de profondeur.

La sélection CPU observée à la caméra (53.600044,4.447731,-76.999802)
contient 56 palmiers, 79 bananiers, 1 monstera, 47 broadleaf et 4 canopy,
sans rejet par capacité. Ces nombres concernent les streams soumis à
l'animation, pas la couverture des pixels de profondeur. Ils ne qualifient
pas la disparition des ombres aux limites de LOD.

`viewport-held.png` présente des ombres projetées visibles sur le sable et
près du village. Cette vue joueur entourée de la coque Studio ne partage
pas la caméra Village de la baseline : aucune métrique appariée n'en est
calculée. Parité visuelle et couverture GPU de profondeur restent non prouvées.
`cleanup.json` confirme les 26 fichiers source et fixture intacts et la
fermeture du seul processus lancé.

Une capture séparée `native-village-shadow-capture-v1` est préparée sur
26 nouveaux fichiers vérifiés, avec caméra Village et canvas 1920×1080.
Elle conserve la garde initiale de projection et le contrôle de nettoyage
V2. Son résultat est attendu séparément ; aucune comparaison n'est encore
revendiquée pour ce candidat.

## Capture Village du candidat observé

`native-village-shadow-capture-v1` termine code 0 (`VILLAGE_CAPTURE_COMPLETE`).
Le canvas et le PNG sont 1920×1080. Le diagnostic vivant confirme la caméra
(42,14,-90), rotation X=-0,2 rad, et 50 passes encodées. La sélection végétale
observe la même position. `cleanup.json` confirme les 26 fichiers originaux
et copiés intacts et le processus propre fermé.

Référence : `baseline-003/village.png`, SHA-256
`01e15da853e17fadaf4ce23ab90b45c66c2c59734abb254cdb6a7d609f82b142`.
Capture candidate : SHA-256
`7b8e4288ce9ee05faa1cca0e6ce8b2d1f42cd49a3f899430f12e27573c5578c6`.
La source est à 2 s, le candidat à 3,75 s : horloges non appariées.

`comparison.json`, calculé avec `Compare-PairedCapture.ps1 -Preset Village`,
donne une erreur absolue moyenne de luminance de 6,1759 pour la colline,
12,7615 pour les maisons, 8,3572 au premier plan et 9,2082 pour l'image entière.
Ces métriques descriptives ne constituent aucun seuil de parité. Le léger
écart avec le candidat historique ne prouve pas une amélioration, car les
animations ne partagent pas la même horloge.

Inspection des deux images : les silhouettes principales des ombres des
palmiers et bâtiments sont présentes sur le terrain aux mêmes emplacements
visuels. Matières, teinte des zones ombrées et accessoires du village diffèrent.
Les petits objets visibles dans la source près des maisons et de l'étal sont
encore absents de cette fixture/candidat figé. Les intégrations ultérieures
présentes dans le worktree ne sont pas qualifiées par cet ancien snapshot.
Aucune raison de modifier globalement les distances n'est établie par cette
vue seule. La suite doit tester le franchissement des fenêtres végétales avec
une trajectoire observée, et incorporer les nouveaux accessoires éditables
avant une nouvelle comparaison complète.

## Marche réelle : sélection dynamique vérifiée, parcours limité par collision

`native-shadow-walk-v1` termine code 0. Les entrées CDP publiques capturent
réellement le pointeur puis maintiennent W par segments de 2 s. Treize
observations sont enregistrées, avec captures aux étapes 0,3,6,9,12.
`walk-evaluation.json` est PASS : déplacements observés, caméra de sélection
courante à chaque sample, cinq groupes cohérents, même source, génération
engagée, passes progressant, sources conservées et processus propre fermé.
Ce verdict concerne exclusivement les entrées et la sélection CPU dynamique.

La caméra passe de (53.600044,4.447731,-76.999802) à
(58.013336,4.936523,-97.980797), déplacement net 21,445704 m.
Les passes encodées avancent de 41 à 239. Palmiers : 56 au départ,
71 aux étapes 4–6 puis 70 à la fin ; bananiers 79→84,
monsteras 1→3, broadleaf 47→53, canopy 4→4. La sélection n'est donc pas
figée sur la caméra initiale. Les captures montrent toujours les ombres
projetées du village et, à la fin, la maison proche avec son ombrage.

La trajectoire rencontre une maison vers l'étape 4 : Z avance ensuite
très peu et le mouvement glisse latéralement de quelques mètres. Aucun
franchissement contrôlé des seuils 65/105/120 m n'est revendiqué ; le
PASS ne qualifie pas la disparition individuelle des ombres. La suite
nécessite une route contournant la maison, avec suivi d'arbres identifiés
et comparaison des mêmes récepteurs. Modifier les distances sans cette
attribution risquerait de masquer une autre différence de rendu.

## Fenêtres effectives du shader source vérifiées

`shader-shadow-range-audit-v2.json` vérifie le SHA-256 du shader acquis contre
`fixture.json` et compare les corps WGSL de `vegLodScale`, `vegLodDither`,
`vegPlantMask` et `vegCanopyMask` à la source figée. Les quatre corps sont
conservés. L'initialisation de canopée est 65/45 m, la bande LOD vaut 0,12.
Ce contrôle porte sur le code et les bornes ; il ne lit aucune texture GPU.

Le premier reçu est FAIL parce que l'extracteur attrapait les signatures
mentionnées dans les commentaires de documentation de VegNodes. Le V2
ancre la recherche au début d'une définition `fn`, sans changer les shaders.
Le reçu initial reste conservé ; aucune divergence shader n'en est déduite.

| Groupe | Début / fin du fondu tramé | Fin de réduction de géométrie |
|---|---|---|
| Palmiers | 112,8 / 127,2 m | 127,2 m |
| Canopée | 61,1 / 68,9 m | 68,9 m |
| Bananiers | Pas de fondu de sortie tramé pour cette plage | 120 m, réduction dès 100 m |
| Monsteras et broadleaf | Pas de fondu de sortie tramé pour cette plage | 105 m, réduction dès 85 m |

La condition nommée `hardEnd` dans le shader choisit une géométrie de taille
entière pendant la bande, puis nulle après ; elle n'enlève pas le fondu
fragmentaire. Le début du rapport est corrigé : parler d'une coupure dure
à 65 m confondait la fenêtre nominale et son masque. Les distances utilisent
la position principale de caméra et la base de l'instance en 3D, conformément
à la source ; la caméra de cascade n'est pas utilisée pour ces limites.
La prochaine observation doit suivre les mêmes arbres à travers les deux
bords effectifs, et pas tester seulement la distance nominale 65/120 m.

## Diagnostic GPU isolé en construction

Un nouvel exemple `tidewater_plant_shadow_lod_probe` charge les fichiers source
vérifiés, conserve une seule instance et son modèle exact, puis installe le
même graphe de profondeur et le même adaptateur shader que Studio. Terrain
et bâtiments sont exclus ; la caméra de lumière et ses matrices restent
fixes, seule la distance de la caméra principale varie. Le readback existant
lit les trois couches Depth32 de 2048×2048. Il ne qualifie pas la réception
sur le sol ni la sélection CPU vivante de Studio.

Les sources sont figées dans `isolated-lod-gpu-v1/source`, toutes les entrées
parentes relues contre leurs pins avant copie. Seuls l'exemple, son nouveau
module et la déclaration du module diffèrent du snapshot précédent.
Construction native lancée en session 23143 après contrôle du cache inactif.
La session est observée vivante ; le log progresse jusqu'à `bevy_ecs`.
Aucun verdict de compilation ou GPU n'est encore acquis.

La sélection source (42,14,-90) trouve quatre instances de canopée avec
`iDat.y<0` : ce sont des arbustes. Leur masque utilise 45 m et le fondu
42,3–47,7 m, pas le seuil arbre 65 m. `RunIsolatedLod.py` prépare trois
readbacks aux distances 40/45/50 m pour le premier arbuste et 110/120/130 m
pour le premier palmier. Le critère prévoit une empreinte proche non vide,
intermédiaire partielle et lointaine vide. Il reste à exécuter et observer.
La canopée des arbres de forêt sera contrôlée séparément ; ces six cas ne
pourront pas qualifier toutes les espèces ni une parité complète.

## Enchaînement GPU et préparation du cas arbre

Le constructeur est relu dans les processus Windows : PID 28388, commande
exacte `BuildIsolatedLod.py`, début 2026-10-04T22:24:36.480662+02:00.
`WaitAndRunIsolatedV1.ps1` vérifie identité et date, attend uniquement ce
processus, exige le reçu réussi et les sources intactes puis lance les six
readbacks. Session d'enchaînement 21968, état `waiting`. La compilation 23143
reste observée vivante ; le log atteint `bevy_asset`/`bevy_color`. Aucun build
n'est redémarré et aucun GPU PASS n'est encore revendiqué.

Le module canonique du diagnostic suivant accepte `canopy-tree` : il charge
la caméra forêt du manifeste source puis choisit explicitement un record
`iDat.y>=0`. Le record de plante et sa matrice demeurent acquis sans retouche.
Un sidecar neuf conserve hash du record/modèle, index de sélection, type signé,
base et caméras. Cette extension n'est pas dans le snapshot V1 en compilation ;
son build et ses mesures restent à effectuer séparément. Les sélections
Village et Forêt ne sont pas interchangeables et le résultat d'un arbuste ne
sera pas présenté comme une validation des arbres de forêt.

## Correction du profil de construction et enchaînement V2

L'audit de l'arbre de processus V1 montre rustc compilant les dépendances
Bevy avec `debuginfo=2`, alors que les constructeurs natifs Forge utilisent
`RUSTFLAGS=-C debuginfo=0` et `CARGO_INCREMENTAL=0`. Seuls Cargo PID 10008,
enfant du constructeur 28388, et son rustc vérifié sont arrêtés pour cette
correction. Le constructeur Python publie alors `exit_code=4294967295`,
sources intactes, GPU non exécuté. L'enchaînement 21968 refuse correctement
ce reçu et termine code 1. Aucun shader/GPU échec n'est déduit de cet arrêt.

Le V2 reprend dans un nouveau snapshot, avec options natives alignées et
extension `canopy-tree`/sidecar incluses. Point explicatif corrigé :
`forge_studio/desktop` n'inclut pas Bevy natif ; le viewport est le WASM.
Aligner les options ne garantit donc pas un cache complet du moteur natif.
La compilation des dépendances est nécessaire et n'est pas redémarrée V2.

Constructeur V2 observé : PID 23672, début
2026-10-04T22:36:24.150252+02:00, commande exacte `BuildIsolatedLodV2.py`.
Session 60801 vivante. L'enchaînement V2 attend cette identité puis exige le
reçu réussi et les sources intactes avant les neuf readbacks : arbuste
40/45/50 m, palmier 110/120/130 m, arbre de forêt 60/65/70 m. Les sidecars
permettent de vérifier que les trois distances gardent le même record et
modèle. Les résultats de compilation et GPU restent attendus.

## Critères du lecteur GPU précisés pendant la compilation

Le runner V2 vérifie aussi les sidecars : même hash de record et de modèle
aux trois distances, type arbre/arbuste exact selon `iDat.y`, et distance
réellement demandée. Il conserve hash de l'exécutable et du manifeste acquis,
puis recontrôle manifeste et toutes les sources figées après les readbacks.
Parse Python PASS. Il n'a pas encore exécuté de mesure ; la session 56601
reste dans l'attente du constructeur 60801, observé vivant. Le log atteint
`bevy_input`/`bevy_window`. Ces attentes ne qualifient aucun texel ni rendu.

## Échec de compilation V2 et correction de la sonde V3

60801 termine en échec Cargo 101, sources figées intactes ; 56601 refuse
correctement d'exécuter le GPU. Le compilateur relève deux erreurs propres
à l'exemple : `Settings.matrices` est un tableau de quatre matrices alors
que les trois passes de profondeur utilisent les trois premières ;
`Input::validate` est privée. Aucun texel n'a été lu et aucun défaut de
shader n'est déduit de cet échec.

La sonde conserve ses trois passes/layers, initialise le tableau uniforme
avec quatre matrices et appelle `graph::install`, API publique retournant
`Result` qui exécute elle-même la validation. Aucune validation du graphe
n'est désactivée et aucun shader ni seuil de fondu n'est modifié. Rustfmt
et diff ciblé passent. V3 construit dans `isolated-lod-gpu-v3`, session
83698 ; les reçus/logs V2 restent conservés. Le résultat compilé puis les
neuf readbacks restent attendus.

## Cause de l'écart temporel de la capture Village

Inspection actuelle : `authoring_imported_terrain::launch` utilise
`FixedProofViewPlugin::camera_only` pour le paramètre de village. Le
plugin valide/fixe la caméra, mais n'installe pas le gel ni les pas
d'horloge de son mode `from_camera`. Le statut publié distingue bien
le temps virtuel réellement observé. Attendre ce statut puis cinq secondes
de plus dans le harness ne peut donc pas produire exactement la référence
à 2 s ; le reçu précédent signale correctement l'absence d'appariement.

La prochaine comparaison doit avoir un mode diagnostique explicite de
chargement et d'avancement contrôlé de l'horloge, sans rebaptiser une vue
seulement cadrée en vue temporellement appariée ni réécrire les sources
sauvegardées. Les simulations dépendantes de leur historique devront
aussi être vérifiées avant toute affirmation de parité. Aucun réglage
d'horloge, shader ou nouveau mode n'est modifié par cette inspection.

## Neuf lectures GPU V3 réussies

83698 termine code 0, sources figées intactes ; EXE SHA-256
`6b34da369d62c8ddf9191d453e73ae2bdb7c6889baa606ea7fb16c06cba49728`.
98110 termine code 0 ; `isolated-lod-gpu-v3/gpu-result.json` PASS,
18 critères réussis, neuf sorties de 50 331 648 bytes finies dans [0,1].
Les neuf hashes binaires sont revérifiés après ce résultat.

| Instance source isolée | Près : texels couverts | Transition : texels couverts | Loin : texels couverts |
|---|---:|---:|---:|
| Arbuste canopy | 40 m : 3 886 | 45 m : 1 941 | 50 m : 0 |
| Palmier | 110 m : 18 644 | 120 m : 9 328 | 130 m : 0 |
| Arbre canopy de forêt | 60 m : 43 446 | 65 m : 21 726 | 70 m : 0 |

Les trois layers présentent ces mêmes valeurs, car les matrices de lumière
sont volontairement identiques et fixes pour isoler le masque de distance.
Chaque triplet garde exactement le même record, modèle et transform, avec
type arbre/arbuste contrôlé. Les positions réelles et hashes sont dans les
sidecars ; l'instance forêt retenue est positive selon `iDat.y`.

Cette preuve exerce le vrai graphe de profondeur et les masques source sur
le GPU. Le collecteur CPU est tenu fixe, aucun receiver visible n'est
comparé, et aucune parité de carte ou portée perceptuelle Studio n'est
qualifiée par elle. Les fades source et seuils n'ont pas été assouplis.
Le shader confirme les transitions observées par l'audit statique ;
la prochaine validation concerne le candidat Studio consolidé.
