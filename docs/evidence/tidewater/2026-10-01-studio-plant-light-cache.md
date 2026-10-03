# Tidewater — cache d'éclairage des plantes pendant la marche

Suite du [profil de marche](2026-10-01-studio-walking-render-churn.md),
sous la référence figée `1438b1abfcaee3267092b75573014f4d9b4a983c`.

Suite : [hauteurs GPU de la surface dans Studio](2026-10-01-studio-surface-water-queries.md).

## Cause et changement

Dans `authoring_imported_plants::update`, toute translation de caméra
déclenchait `Resident::select`, puis remettait `state.lighting` à `None`.
La comparaison suivante échouait donc, même pour une clé d'éclairage
identique, et `Assets<PlantMaterial>::get_mut` marquait les matériaux de
chaque niveau végétal comme modifiés. Les ombres live disposent déjà d'un
buffer partagé ; cette invalidation ne sert pas leur animation.

La sélection de population conserve ses règles et ses handles. Elle ne
réinitialise plus le cache d'éclairage. `retire` continue de l'invalider
quand la projection est remplacée, et une véritable modification de la clé
de lumière provoque toujours le rafraîchissement. Les poses de nuages
restent mises à jour par leur système propre. Aucun shader, seuil de LOD,
nombre d'instances, profil graphique ou fréquence d'ombres n'est réduit.

Le compteur cumulatif `plant_lighting_refreshes`, publié dans le diagnostic
existant des cascades, compte les lots réellement rafraîchis. Il n'est pas
une clôture GPU ni une mesure de présentation.

## Construction 115

La construction WASM release passe en 3 min 53 s. Tauri passe en 19,56 s.
L'avertissement de compatibilité future préexistant de `proc-macro-error2`
reste visible. Le premier script d'empaquetage cherchait incorrectement
`forge_studio.wasm` ; la correction du chemin vers le fichier réellement
produit, `forge_studio_frontend.wasm`, permet l'empaquetage. Son journal
échoué reste conservé.

Le [reçu 115](../../../../.tidewater-integration/studio-tidewater-plant-cache-candidate-115/build-proof.json)
épingle l'exécutable
`07f8d9469d8e93abc8a832b1edb30a27c9b759c27e7479a6a5ae79e0db2d87d3`
et le WASM
`5637b9c41cbfb3517f0cb1418c8a32f97a54965d283aa0c291669df4b67f89d7`.
Le reçu de construction demeure `BUILD_ONLY`.

## Comparaison de marche 118/119

Le parcours 116 du candidat précédent passe mais chevauche une compilation.
Ses mesures restent disponibles sans servir de comparaison de performance.
La paire principale rejoue le même harness de forêt que la série 113,
départ X/Z −120/−340, vrai canvas 1920 × 1080, mêmes effets, douze segments
W/D/S/A de cinq secondes. Les mesures finales sont effectuées sans
compilation concurrente. Le premier segment instrumenté est exclu de la
cadence moyenne ; les p95 et 1 % low sont donnés par segment.

Les deux parcours passent, retour à l'édition compris, et préservent leurs
sources. Les mesures sont réalisées sans processus Cargo/Rust actif, sur
le même adaptateur AMD RDNA 3. Les douze segments se terminent au sol ; les
cascades progressent dans une génération, sans récupération.

| Mesure | 112, série 118 | 115, série 119 |
| --- | ---: | ---: |
| Cadence rAF à l'arrêt en forêt | 23,77 images/s | 126,13 images/s |
| Cadence rAF pondérée des onze segments non profilés | 21,35 images/s | 105,54 images/s |
| p95 rAF, plage des segments | 48,80–55,60 ms | 13,90–20,70 ms |
| 1 % low rAF, plage des segments | 15,14–18,02 images/s | 29,81–61,48 images/s |

Le gain de cadence moyenne est de 4,94 fois dans ce parcours. Il ne prouve
pas une cadence de présentation constante à 60 images/s. Les deux mesures
sont des replays de touches chronométrées, pas une trajectoire verrouillée
au bit : les départs diffèrent de moins de 10 cm et les arrivées d'environ
75 cm ; la durée mesurée totale est 60,94 puis 60,21 s. Les effets,
exposition +0,10 EV, direction de caméra et résolution restent les mêmes.

Le compteur d'éclairage du 119 reste à 2 durant tous les segments, sans
rafraîchissement pendant la marche. L'animation des nuages, FFT, ressac,
vent et cascades continue. Le profil instrumenté du premier segment donne
1 369,82 ms de spécialisation des matériaux sur 5 464,59 ms échantillonnées
(25,07 %) avant, puis 11,00 ms sur 5 159,82 ms (0,21 %) après. La mise en
file passe de 983,67 à 6,24 ms. Il s'agit de sous-arbres CPU échantillonnés,
avec surcoût de profilage ; aucun timing GPU n'est déduit.

Les appels WebGPU sans étiquette restent à environ 238 par image encodée,
avec 111 puis 499 images dans les fenêtres profilées. Ce changement ne
prétend donc pas éliminer les écritures nécessaires aux effets animés.
Les images de début et de fin ont été inspectées : forêt, relief,
fougères, ombres et ciel restent présents. Elles ne qualifient pas la
fidélité avec un état temporel identique de la référence Three.js.

Preuves locales :

- [smoke avant 118](../../../../.tidewater-integration/studio-tidewater-plant-cache-before-118/fish-stand-studio-smoke-ledger.json)
  et [synthèse avant](../../../../.tidewater-integration/studio-tidewater-plant-cache-before-118/plant-cache-forest-summary.json) ;
- [smoke après 119](../../../../.tidewater-integration/studio-tidewater-plant-cache-after-119/fish-stand-studio-smoke-ledger.json),
  [synthèse après](../../../../.tidewater-integration/studio-tidewater-plant-cache-after-119/plant-cache-forest-summary.json)
  et [parcours détaillé](../../../../.tidewater-integration/studio-tidewater-plant-cache-after-119/tidewater-forest-walking.json) ;
- profils [avant](../../../../.tidewater-integration/studio-tidewater-plant-cache-before-118/profile-inclusive.json)
  et [après](../../../../.tidewater-integration/studio-tidewater-plant-cache-after-119/profile-inclusive.json) ;
- [capture finale après](../../../../.tidewater-integration/studio-tidewater-plant-cache-after-119/fullscreen-forest-end.png).

## Limites de validation

La [série Village 120](../../../../.tidewater-integration/studio-tidewater-plant-cache-village-120/tidewater-studio-village-reference.json)
passe, sources intactes. La capture à +0,10 EV a été inspectée : les ombres
demeurent et la zone sombre erronée du terrain ne réapparaît pas. La
[comparaison échantillonnée](../../../../.tidewater-integration/studio-tidewater-plant-cache-village-120/comparison-010-00.json)
donne 9,56/255 d'erreur absolue de luminance et une moyenne de 108,49,
contre 109,23 dans la source. La mesure précédente 114 donnait 9,52/255 ;
ces mesures proches ne prouvent pas la parité temporelle. Le contrôle
Clippy concurrent à cette seule capture n'est pas utilisé pour mesurer
la performance.

Le formatage ciblé et le contrôle des espaces passent. Clippy avec
`-D warnings` échoue dans les dépendances de la cible : seize diagnostics
dans `forge_assets`, deux dans `forge_project`, hors du changement de ce
rapport. Le [journal complet](../../../../.tidewater-integration/studio-plant-cache-clippy-115.log)
reste visible ; aucun lint n'est désactivé dans les sources. Le verdict
global de validation n'est donc pas acquis. Le contrôle `--no-deps`
limité au package Studio échoue aussi, avec 31 diagnostics dans le
[journal séparé](../../../../.tidewater-integration/studio-plant-cache-clippy-no-deps-115.log),
dont `too_many_lines` sur `authoring_imported_plants::update`, désormais
140 lignes pour un seuil de 100. La disparition du coût CPU n'est pas un
verdict de qualité globale du code ; les diagnostics restent à traiter.

Les essais graphiques et leurs processus sont terminés. L'archive de
référence reste à
`50bfd655a7fb4f5a61e4593240ea86dc8d13136d82d558d34737a41b4c6c8720`.
Restent ouverts les à-coups et timings de présentation/GPU, le benchmark
natif complet, la parité animée avec la référence et les autres fonctions
du plan. Aucun verdict de gate ni publication n'est prononcé.
