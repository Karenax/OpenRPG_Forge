# Tidewater — coût de la réfraction pendant le déplacement Studio

Suite du [port de la capture séparée](2026-10-01-studio-refraction-capture.md).
Le parcours 172 et son audit indépendant passent sur le candidat 169, dans une
nouvelle copie isolée. L'objectif complet reste actif. Cette tranche mesure le
rendu en mouvement ; elle ne change ni le moteur ni les réglages par défaut.

## Identités et protocole

Binaire : `f558a4dc09d0bcfc7c701861e4bbd05d74cc17090ca3d0b8b68d31e825b105c9`.
WASM : `957c3caf1fd143c5f135c7e5af7987ff19574e87e1b329590b08289e5ac2119e`.
Référence Tidewater : `1438b1abfcaee3267092b75573014f4d9b4a983c`.
Viewport physique 1920×1080, capture 960×540 ; adaptateur WebGPU AMD RDNA-3.
Le navigateur ne fournit pas ici l'identité précise du modèle de GPU.

Après le triplet figé vers la mer, six déplacements A/D de deux secondes sont
effectués par clavier CDP pour chacune des phases activée, désactivée, réactivée.
Le joueur reste en marche sur la plage, près de X40/Z−47. Chaque segment
parcourt 4,09 à 5,70 m ; chaque phase revient à moins de 0,60 m de son départ.
La simulation, le vent, les ombres et l'océan continuent. Aucun gel ou relevé
de textures n'est demandé pendant la mesure ; les observations couleur et
profondeur du gel précédent sont effacées avant les échantillons.

La seule préférence modifiée entre phases est « Fond visible à travers l'eau ».
Le contrôle et la préférence persistée concordent. La caméra dérivée suit sa
source ; un terrain et quatre parties de village restent présents. Ces comptes
ne constituent pas un audit de croissance mémoire ou d'éditions répétées.

## Mesures 172, premier trajet inclus

| Capture du fond | Cadence rAF | P95 intervalle | Plus long intervalle | 1 % bas | Passes capture encodées/s |
|---|---:|---:|---:|---:|---:|
| Activée | 72,25/s | 20,90 ms | **430,70 ms** | **7,76/s** | 73,14 |
| Désactivée | 82,72/s | 20,90 ms | 41,80 ms | 34,16/s | 0 |
| Réactivée | 76,34/s | 20,90 ms | 62,60 ms | 29,90/s | 75,35 |

La différence observée est de 6,38 à 10,47 callbacks/s, environ 7,7 à 12,7 %
de la cadence du passage désactivé. Les phases sont séquentielles, avec horloges
d'animation et trajectoires légèrement différentes : ce pourcentage n'est pas
un coût GPU isolé ni une comparaison à caméra et horloge source identiques.
Les compteurs de passes mesurent l'encodage des commandes, sans fence GPU.

Le premier segment activé contient des intervalles de 374,9, 166,5 et 430,7 ms.
Les moyennes seules masquent donc une saccade importante. La répétition améliore
le résultat, mais ne prouve pas encore la cause du problème ni sa correction.
La fluidité du premier déplacement reste ouverte.

Le collecteur 172 a initialisé le premier delta de chaque segment avec
`performance.now()`, puis utilisé les timestamps rAF : ce premier nombre peut
être négatif et n'est pas un intervalle entre deux images. Le premier audit
rejette cette donnée. Il est conservé dans `refraction-motion-audit-172-attempt-1.log`
et `refraction-motion-verification-attempt-1.json`. L'audit corrigé exclut
exactement le premier nombre de chacun des dix-huit segments, quel que soit son
signe. Tous les vrais intervalles, y compris les saccades, restent comptés.
La cadence moyenne compte tous les callbacks sur la durée murale du segment.

## Régression conservée

Le parcours complet passe édition/placement, sauvegarde et réouverture,
persistance graphique, FFT, géométrie, optique, ressac, rebond, AO, flare,
brume, flou de mouvement et ombres animées. Les huit étapes de nage et le
retour sur terre passent aussi. Les six triplets d'ombres et le triplet de
capture gardent tous les critères des audits 166/170, sans modifier leurs seuils.
L'archive source reste intacte et le processus du test est fermé.

La comparaison figée de capture trouve 83 610 pixels avec delta >2 sur l'image
entière, dont 24 429 dans l'eau près du ponton. Les témoins ciel et sable sec
restent à un maximum de 1/255. La répétition PNG et RGB est exacte. Les textures
GPU couvrent 358 075 pixels chacune, sans valeur non finie. Cela valide l'effet
local du réglage, pas tous les cas d'occlusion ou la parité globale.

[Parcours complet](../../../../.tidewater-integration/studio-tidewater-refraction-motion-runtime-172/fish-stand-studio-smoke-ledger.json),
[mesures brutes](../../../../.tidewater-integration/studio-tidewater-refraction-motion-runtime-172/refraction-motion.json),
[audit indépendant](../../../../.tidewater-integration/studio-tidewater-refraction-motion-runtime-172/refraction-motion-verification.json).

## Diagnostic 173 et correction de sélection

Le second parcours utilise encore le candidat 169. Le premier aller/retour A/D
est relevé séparément avec le profiler CPU CDP, désactivé avant les trois phases
de mesure. Le premier aller reproduit un intervalle de 444,5 ms ; le retour
reste sous 27,9 ms. Sur les trajets suivants, les cadences brutes sont
81,41 / 84,97 / 67,00 callbacks/s, avec maxima 27,9 / 41,6 / 41,8 ms.
La variation entre passages reste importante. Ces mesures ne démontrent aucun
gain de la correction décrite ci-dessous, absente du candidat 169.

Le profil contient 3 027 échantillons sur 4 681,041 ms. Parmi les temps propres
échantillonnés : `Resident::select` 435,814 ms, tri stable `far_ids` 147,007 ms,
écriture des vertices Bevy 155,454 ms et `writeBuffer` 388,134 ms. Les temps
propres sont des échantillons sur tout le relevé, pas la décomposition exacte
de la saccade. Ils motivent une correction bornée de la sélection CPU ; ils ne
prouvent pas que le tri explique seul les 444,5 ms.

**Le 173 échoue à l'audit visuel complet.** Le parcours UI/nage termine, mais le
témoin fixe « sable sec » contient ici une partie du bord de mer recouverte par
la vague figée : delta maximal 20/255 à X1673/Y875, 6 033 pixels avec delta >2.
L'image a été inspectée. Le seuil ancien de 1/255 reste inchangé ; le 173 garde
son échec. La preuve 172 n'est pas remplacée par ce diagnostic. Il faudra un
contrôle qui établit que les pixels témoins restent réellement hors de l'eau,
ou un état de préparation adapté, avant de qualifier ce nouveau cas.

[Diagnostic brut et profil CPU](../../../../.tidewater-integration/studio-tidewater-refraction-warm-motion-runtime-173/motion-cpu-diagnostic.json),
[échec de comparaison](../../../../.tidewater-integration/studio-tidewater-refraction-warm-motion-runtime-173/refraction-capture-verification.json).
Le processus 173 est fermé et la source reste préservée.

La correction 174 calcule chaque clé de distance éloignée une seule fois,
conserve le tri stable par buckets de 6 m et leur clamp 1023, et réutilise la
sélection d'un groupe entre ses niveaux de détail au même appel. Chaque niveau
applique toujours sa capacité propre. Les vecteurs ne sont pas conservés entre
caméras ou révisions. La projection résidente et le chemin de création initial
utilisent tous deux cette optimisation, sans changer les paramètres, les
ombres, les meshes, les identités ou la densité.

Deux tests supplémentaires couvrent les égalités, les frontières 6/12 m, le
clamp lointain, les préfixes de capacité, le déplacement de caméra, l'exclusion
près de la caméra avec sa composante verticale et l'ordre non trié. La
qualification visuelle et les gains du futur candidat restent à mesurer ;
les parcours 172/173 concernent le code antérieur.

Les 16 tests natifs `tidewater_plants::` passent sur le code 174, dont ces deux
nouveaux tests, en 7 min 59 s de construction. Le contrôle Engine
`wasm32-unknown-unknown`, features explicites `web,qualification`, passe en
1 min 21 s. Le formatage des deux fichiers modifiés et le contrôle de whitespace
passent. Ce contrôle WASM n'est pas une construction du frontend distribuable.
[Tests natifs](../../../../.tidewater-integration/plant-selection-tests-174.log),
[contrôle WebGPU](../../../../.tidewater-integration/plant-selection-webgpu-check-174.log),
[39 entrées source de la correction](../../../../.tidewater-integration/plant-selection-inputs-174.json).

Clippy strict 174 échoue encore sur les deux `needless_pass_by_value`
préexistants de `forge_project::zone_authoring::session`. L'audit limité aux
warnings termine avec les 65 warnings Engine antérieurs, aucun dans les deux
fichiers modifiés ; il n'est pas un Clippy strict PASS.
[Reçus des contrôles](../../../../.tidewater-integration/plant-selection-checks-174.json),
[manifestes des mesures et de la correction](../../../../.tidewater-integration/plant-selection-artifacts-174.json).

## Candidat 174 construit, parcours 175 validé

La construction WASM release termine en 9 min 58 s. Le nouveau frontend et son
shell isolé sont identifiés séparément : exécutable
`ea2c5c52d4b6113fb466327b243150b45ff6efc74b259f284e944707aadbb44a`, WASM
`8f08b2a57a8cf91d86795a23ec657f733cdc152a5942485f51f944b4474326a1`.
Le reçu de construction garde son verdict BUILD_ONLY ; les résultats runtime
sont dans les reçus du parcours 175.

Le 175 et son audit indépendant passent : édition, sauvegarde/réouverture,
réglages graphiques (dont flou 0/360 puis 180°), océan, effets, ombres,
capture séparée, huit étapes de nage et retour en édition. Les 39 entrées
produit étaient inchangées avant construction et lancement. La source est
préservée et le processus est fermé. Le launcher courant sur 8848 n'est pas
remplacé par ce candidat d'essai.

Le témoin de sable est préparé par une vraie marche arrière jusqu'à Z−54,
puis le joueur revient au point côtier. Aucun ROI ou seuil ancien n'est changé.
La capture modifie 64 878 pixels de plus de 2 niveaux, dont 12 036 dans l'eau
près du ponton. Ciel et sable sec restent sous 1/255 ; la répétition est exacte.
Les lectures couleur et profondeur couvrent chacune 90 291 pixels sans valeur
non finie. Les temps de simulation diffèrent des anciennes séries.

| Parcours | Candidat | Réfraction activée | Désactivée | Réactivée | Premier aller, intervalle maximal |
|---|---|---:|---:|---:|---:|
| 175 | 174, correction de sélection | 77,98 callbacks/s | 85,08 | 81,64 | 458,4 ms |
| 176 | 169, ancien code | 79,10 callbacks/s | 84,86 | 79,25 | 451,5 ms |
| 179, instrumenté | 174 | 66,14 callbacks/s | 67,52 | 63,89 | 576,3 ms |

Ces passages séquentiels ne démontrent pas de gain de cadence pour la
correction 174. Le 179 comporte en plus les hooks d'API et le profiler au premier
aller ; sa cadence ne constitue pas un benchmark comparable sans cette surcharge.
Les chiffres sont ceux des callbacks navigateur, pas un chronométrage GPU.

**Le contrôle complet 176 échoue à la plongée C**, avant les derniers triplets.
Les relevés déjà écrits restent un diagnostic partiel ; aucun timeout n'a été
élargi et aucun PASS global n'est attribué au 176. La cause de cette entrée
manquée n'est pas établie. Source préservée et processus fermé.

[Identités de construction](../../../../.tidewater-integration/plant-selection-binaries-174.json),
[régression et audit 175](../../../../.tidewater-integration/studio-tidewater-plant-selection-runtime-175/plant-selection-verification.json),
[échec conservé 176](../../../../.tidewater-integration/studio-tidewater-plant-selection-baseline-runtime-176/fish-stand-studio-smoke-ledger.json).

## Diagnostic WebGPU 177–179

Le hook 177 échoue au démarrage : son sérialiseur supposait que chaque cible
couleur d'un pipeline était non nulle. Ces entrées nulles sont valides ; le
TypeError provient du diagnostic, pas du shader produit. Le 178 accepte les
emplacements nuls et empêche les erreurs de journalisation de masquer le
résultat de l'API. Ses contrôles locaux passent. Le parcours 178 échoue ensuite
sur le contrôle UI flou 360°, sans erreur de hook ; cet échec reste conservé.
La cause du réglage resté à zéro n'est pas établie.

Le 179 mesure les appels GPU avec le flou habituel 180°. Le parcours et les
audits figés/mouvement passent, source intacte et processus fermé. Il n'exécute
pas la variation de flou 0/360 : il reste un diagnostic complémentaire, pas une
nouvelle qualification complète des réglages graphiques.

Sur 50,393 s de premier déplacement et phases chaudes, le diagnostic relève
13 635 appels : 3 252 grandes écritures, 10 371 soumissions, huit modules shader
et quatre pipelines de rendu. Les grandes écritures totalisent
10 430 899 436 octets, avec maximum 3 ms par appel. La création de pipeline
a un maximum arrondi de 0,1 ms et la soumission de 0,2 ms. Aucun appel synchrone relevé
n'explique individuellement la saccade de 576,3 ms. Ces durées de wrappers ne
mesurent pas l'exécution GPU ni une éventuelle compilation différée du driver.

Les transferts répétés concernent les slabs Bevy des meshes : buffers de
vertices 7/8 et indices 1. Le code terrain/océan compare actuellement des
vecteurs de tuiles triés par distance ; un nouvel ordre suffit donc à recréer
le mesh alors que ses tuiles restent identiques. Cela motive la correction
180 ci-dessous. Le traceur ne prouve pas que ces transferts expliquent seuls
la saccade initiale.

[Trace brut](../../../../.tidewater-integration/studio-tidewater-plant-selection-gpu-runtime-179/gpu-cold-calls.json),
[audit des durées](../../../../.tidewater-integration/studio-tidewater-plant-selection-gpu-runtime-179/gpu-cold-api-verification.json),
[audit visuel et mouvement](../../../../.tidewater-integration/studio-tidewater-plant-selection-gpu-runtime-179/plant-selection-verification.json).

## Réutilisation de géométrie 180 — parcours Studio 181 validé

La sélection CDLOD conserve son ordre source. Un contrôle de géométrie distinct
compare les tuiles comme un multiensemble de leurs champs exacts ; il ne modifie
pas les sélections. Le packing déjà rendu d'un mesh opaque peut rester lorsque
seul le tri par distance change. Une autre origine, taille, LOD ou multiplicité
invalide toujours ce cache, ainsi que des valeurs non finies. Les bits de zéro
signé sont conservés. Ce contrôle est utilisé pour terrain/océan Studio et
leur adapter de marche native. Le natif masque aussi une sélection vide.

Les cinq tests CDLOD natifs passent, dont deux nouveaux couvrant permutations,
doublons et invalidations. Le formatage ciblé et le whitespace passent.
Le contrôle natif du probe passe en 5 min 42 s ; Clippy World ciblé avec
`--no-deps -D warnings` passe en 42,79 s. Ce dernier résultat ne clôt pas
les anciens échecs Clippy Engine/projet. La construction WebGPU release et le package passent, en 52 min 15 s pour
le frontend, pendant une compilation parallèle sur la même machine. L'exécutable
180 vaut `866bda362d83830eb2d2019af43d1942fead482f1a6a5fdaf22cbeae9ad77dff`,
le WASM `fc4ecb82929f8d8e2c39502fb9906d33aebd5fc17cc58463f408ceaa60119961`.
Le parcours Studio 181 et ses audits indépendants passent sur ces identités ;
les mesures et leurs limites figurent ci-dessous.
Une modification simultanée de `forge_engine/src/lib.rs` ajoutant le module
`source_fragment` a été constatée et préservée ; elle ne fait pas partie de
cette correction. Les entrées de compilation suivantes l'identifient.

Une seconde modification externe de `lib.rs` ajoute le module
`source_fragment_loader` à 13:29:36 pendant la compilation. Les cinq fichiers
de la correction restent conformes à leurs pins, mais le pin de `lib.rs` diffère
après construction. Ce changement est conservé et tracé ; la reproductibilité
intégrale des sources de ce build partagé n'est pas affirmée. Les identités
de l'exécutable et du WASM qualifié restent exactes.

[Construction](../../../../.tidewater-integration/mesh-reuse-build-checks-180.json),
[identités et dérive](../../../../.tidewater-integration/mesh-reuse-binaries-180.json),
[limite de provenance](../../../../.tidewater-integration/mesh-reuse-post-build-source-180.json).

[Tests](../../../../.tidewater-integration/mesh-reuse-world-tests-180.log),
[entrées épinglées](../../../../.tidewater-integration/mesh-reuse-inputs-180.json),
[limite des changements](../../../../.tidewater-integration/mesh-reuse-source-boundary-180.json).

## Résultat Studio 181 et comparaison instrumentée

Le parcours complet 181 passe sur le candidat 180 : édition, placement,
sauvegarde/réouverture, persistance des réglages, flou 0/360 puis 180°,
océan et effets, ombres, capture de réfraction, huit étapes de nage et retour
en édition. Les audits indépendants de mouvement, contrôles figés et appels
WebGPU passent également. La source de la carte est préservée et le processus
candidat est fermé. Le contrôle préalable au mouvement n'observe plus la
compilation externe ; il n'introduit aucune attente prolongée de chauffe.

Sur 49,5952 s, les grandes écritures passent à 708 appels et 2 308 332 808
octets, contre 3 252 appels et 10 430 899 436 octets sur 50,393 s au 179.
Le débit mesuré passe de 206,99 à 46,54 Mo/s décimaux : **réduction de 77,51 %**.
Le temps cumulé des appels d'écriture passe de 3 638,6 à 349,7 ms ; ce sont
les durées des wrappers synchrones, sans chronométrage d'exécution GPU.

| Phase | Callbacks/s au 179 | Callbacks/s au 181 | P95 des intervalles au 181 | Intervalle maximal au 181 |
|---|---:|---:|---:|---:|
| Capture activée | 66,14 | 108,00 | 16,00 ms | 30,1 ms |
| Capture désactivée | 67,52 | 115,08 | 15,60 ms | 84,3 ms |
| Capture réactivée | 63,89 | 111,18 | 15,71 ms | 23,5 ms |

Les deux mesures utilisent les mêmes hooks GPU et le même protocole de
premier mouvement puis de marche chaude. Les parcours physiques et horloges
de simulation restent différents. Le 179 omet la variation UI préalable
0/360°, mais les deux fenêtres de mouvement emploient 180°. Ces résultats
locaux ne garantissent pas cette cadence sur d'autres machines et ne sont
pas une comparaison visuelle synchronisée avec Three.js.

**La saccade initiale reste ouverte : 476,3 ms au premier aller du 181.**
Le profil CPU attribue encore 437,7 ms cumulés à la sélection végétale sur
4,743 s ; il ne localise pas à lui seul la cause de cet intervalle. Aucun
appel GPU synchrone enregistré ne dépasse 100 ms. Le pic de 84,3 ms pendant
la phase chaude désactivée reste conservé dans les résultats.

Les contrôles figés observent 59 122 pixels modifiés de plus de 2 niveaux,
dont 9 653 dans l'eau proche du ponton. Le ciel reste sous 1/255 et le sable
sec est identique ; la répétition est exacte. Les captures côtière et sous
le ponton ont été inspectées, sans trou de tuiles manifeste. Cela valide les
contrôles locaux existants, sans établir une parité complète de la carte ni
une qualification GPU de la capture native. Le retour au point côtier après
la préparation du témoin sec présente un écart de 0,512 m, sous le seuil inchangé.

[Parcours et audit visuel/mouvement](../../../../.tidewater-integration/studio-tidewater-mesh-reuse-gpu-runtime-181/plant-selection-verification.json),
[audit des appels GPU](../../../../.tidewater-integration/studio-tidewater-mesh-reuse-gpu-runtime-181/gpu-cold-api-verification.json),
[comparaison chiffrée et limites](../../../../.tidewater-integration/studio-tidewater-mesh-reuse-gpu-runtime-181/mesh-upload-comparison.json),
[manifestes de cette tranche](../../../../.tidewater-integration/mesh-reuse-artifacts-181.json).

## Suite native et limites

Suite exécutée : le [raccordement natif](2026-10-01-native-refraction-capture.md)
du candidat 215 passe ses 24 tests CPU, le triplet de surface 216 et le parcours
de nage/ombres 217 avec leurs audits indépendants. Ces nouveaux résultats
restent séparés des mesures Studio 181 et ne qualifient pas leur performance.
Les observations de code ci-dessous précèdent ce raccordement ; les exclusions
de caméra et bindings indiqués sont désormais corrigés dans cette suite native.
Les [entrées temporelles auxiliaires](2026-10-01-temporal-auxiliary-design.md)
restent au stade de conception et leurs cinq tests CPU purs ne constituent
pas une intégration GPU.

L'inspection du probe natif confirme que `terrain_lod::LodMaterial`,
`terrain_surface::TerrainMaterial` et `village::VillageMaterial` réexportent
déjà les types partagés Engine. Il reste son matériau d'eau local, le choix de
caméra et le raccordement des textures. La copie de profondeur native doit
exclure la caméra de capture : son assertion actuelle exige 1920×1080 pour
chaque vue, incompatible avec la nouvelle vue 960×540. La sélection de caméra
du matériau d'eau doit également exclure cette seconde caméra active.
Cette inspection n'est pas une preuve GPU native et aucun raccordement natif
n'est déclaré terminé ici.

Restent notamment l'analyse de la saccade, les cas d'occlusion distincts,
le redimensionnement/jitter réel, la dispersion des caustiques isolée,
les éditions répétées et la validation native. TAAU, poissons transparents,
rochers/récifs, torche, autres comportements, package final et comparaison
avec horloge source identique conservent leurs propres critères ouverts.
