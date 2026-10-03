# Tidewater — suppression des mises à jour inutiles des matériaux Studio

Suite : [coût du déplacement continu en plein écran](2026-10-01-studio-walking-render-churn.md).
Les fenêtres de ce rapport sont mesurées avec une caméra immobile avant/après
la marche ; elles ne qualifient pas la cadence pendant le déplacement continu.

Suite de la [correction des récepteurs d'ombres](2026-09-30-studio-live-shadow-receivers.md)
et du [balayage d'exposition](2026-09-30-studio-exposure-sweep.md).
La référence demeure Tidewater figé à
`1438b1abfcaee3267092b75573014f4d9b4a983c`.

## Coût mesuré et correction

Le candidat 105 corrige les ombres animées, mais sa cadence rAF Studio
reste à 27,01 puis 26,63 images/s dans deux fenêtres de cinq secondes.
Un [profil CPU de cinq secondes, série 106](../../../../.tidewater-integration/studio-tidewater-shadow-profile-106/studio-cpu-profile.json)
relève 1 381,10 ms dans les sous-arbres `specialize_material_meshes`
(27,16 % du temps échantillonné) et 1 022,19 ms dans
`queue_material_meshes` (20,10 %). Le comptage WebGPU relève 27 962
appels `writeBuffer` sans étiquette pour 117 images encodées.
Le profil et le comptage ajoutent leur propre coût ; il ne s'agit pas de
timestamps GPU ni d'une mesure de présentation de l'écran.

Dans `authoring_imported_terrain::update_live_clouds`, les récepteurs du sol,
des LOD, des plantes et des bâtiments étaient parcourus par
`Assets::iter_mut()` à chaque image. Bevy 0.19.1 émet un événement
`AssetEvent::Modified` pour chaque matériau ainsi parcouru, même si les cinq
vecteurs des paramètres de nuages sont identiques. Cela provoque du travail
de préparation et de spécialisation sans changement de matériau.

Le candidat 107 compare ces paramètres en lecture seule et ne demande
`get_mut()` que pour les récepteurs dont les valeurs changent. Les textures
GPU des nuages continuent d'évoluer ; le ciel conserve les échanges de
textures nécessaires à son animation. La correction conserve les effets,
les distances d'ombres et l'exposition par défaut de +0,10 EV.

Deux contrôles CPU utilisant les vrais `Assets`, événements et plugins Bevy
[passent](../../../../.tidewater-integration/cloud-receivers-cpu-107.run.log) :
120 poses identiques ne produisent aucun `Modified` ; un changement de pose
modifie chacun des deux récepteurs une seule fois, puis la pose répétée
n'en produit plus. Le [harness](../../../../.tidewater-integration/cloud-receivers-cpu-107.rs)
contient la fonction extraite du code courant.

## Candidat 107 et parcours en jeu

La compilation WASM release/offline/locked termine en 7 min 01 s.
Le [reçu de construction](../../../../.tidewater-integration/studio-tidewater-cloud-receivers-candidate-107/build-proof.json)
épingle l'exécutable SHA-256
`81a1efdd49e1ae7cf23e8da341ce783cc8b4d7102d8f3802d547259a1844e17d`
et le WASM embarqué
`8bb85ed3ba928723a740ff11988b663ad28fd5c023077091e8560e78c146cade`.
Ce reçu indique `BUILD_ONLY` ; la validation runtime est séparée.

Le [smoke complet 107](../../../../.tidewater-integration/studio-tidewater-cloud-receivers-smoke-107/fish-stand-studio-smoke-ledger.json)
passe : ouverture, placement du stand, sauvegarde et réouverture, persistance
des réglages, entrée en Playtest, déplacement résolu, FFT et géométrie de
l'océan, optique, ressac, lumière de rebond, occlusion ambiante, flare,
brume et flou de mouvement. Le parcours travaille dans une copie isolée et
confirme `source_preserved=true`.

| Fenêtre de cinq secondes | rAF 105 | rAF 107 | p95 105 / 107 | 1 % low 105 / 107 |
|---|---:|---:|---:|---:|
| Avant déplacement | 27,01 | 107,31 | 48,70 / 41,60 ms | 19,12 / 20,52 |
| Après déplacement | 26,63 | 136,94 | 48,50 / 7,10 ms | 17,99 / 31,46 |

Les ombres encodent respectivement 106,12 et 134,94 images/s dans le 107.
La génération reste à 1, les trois cascades avancent, le vent avance et
aucune récupération n'est comptée. Ces compteurs attestent du travail
encodé ; ils ne sont pas des fences d'achèvement GPU. Ces fenêtres portent
sur le canvas du Playtest Studio, environ 1314 × 797, et ne qualifient pas
une cadence plein écran 1920 × 1080 ni une traversée de forêt prolongée.
La première fenêtre conserve des à-coups malgré la forte hausse moyenne.

## Confirmation du profil CPU

La [seconde exécution instrumentée, série 108](../../../../.tidewater-integration/studio-tidewater-cloud-receivers-profile-108/fish-stand-studio-smoke-ledger.json)
passe également tous les contrôles et retrouve 106,18 puis 139,61 images/s
rAF. Le [profil CPU](../../../../.tidewater-integration/studio-tidewater-cloud-receivers-profile-108/studio-cpu-profile.json)
et le [comptage WebGPU](../../../../.tidewater-integration/studio-tidewater-cloud-receivers-profile-108/studio-webgpu-writes.json)
reprennent le même harness, le même parcours, le même adapter AMD RDNA 3,
le même WebView2 et le même canvas 1314 × 797 que le 106.

| Sous-arbre CPU inclusif, cinq secondes | 105 / série 106 | 107 / série 108 |
|---|---:|---:|
| `specialize_material_meshes` | 1 381,10 ms (27,16 %) | 11,26 ms (0,22 %) |
| `queue_material_meshes` | 1 022,19 ms (20,10 %) | 15,33 ms (0,30 %) |
| `writeBuffer` | 257,62 ms (5,07 %) | 173,58 ms (3,42 %) |

Les profils couvrent respectivement 5 084,62 et 5 080,99 ms, avec 117 et
729 images encodées. Les écritures sans étiquette ne diminuent pas par
image : environ 239 contre 241 appels. Les compteurs du terrain d'ombre et
des plantes restent à une et dix écritures par image. L'amélioration porte
donc sur la préparation répétée des matériaux, sans suppression des passes
d'animation. Les pourcentages sont des parts de temps échantillonné avec
instrumentation ; ils ne donnent pas la durée GPU d'une image.

## Image conservée

La [série Village 107](../../../../.tidewater-integration/studio-tidewater-cloud-receivers-village-107/tidewater-studio-village-reference.json)
capture la caméra figée en 1920 × 1080 et confirme la préservation de la
source. La [capture à +0,10 EV](../../../../.tidewater-integration/studio-tidewater-cloud-receivers-village-107/village-exposure-010-00.png)
a été inspectée : les silhouettes des maisons et des palmiers demeurent et
la grande zone sombre du candidat 099 ne revient pas.

La [comparaison avec la source](../../../../.tidewater-integration/studio-tidewater-cloud-receivers-village-107/comparison-010-00.json),
avec `Compare-PairedCapture.ps1 -Preset Village -Step 8`, relève une luminance
moyenne de 108,50 contre 109,23 pour la source, et une erreur absolue de
9,54/255 contre 9,52 pour le 105. Les régions maisons, premier plan et
colline donnent respectivement 10,84, 10,92 et 6,29/255. Les états temporels
restent différents : ces mesures confirment la stabilité de cette capture,
sans démontrer la parité animée ni la complétude des accessoires.
La [répétition à +0,10 EV](../../../../.tidewater-integration/studio-tidewater-cloud-receivers-village-107/comparison-010-05.json)
retrouve 9,57/255, avec une luminance moyenne de 108,53.

Le formatage Rust ciblé, le contrôle des espaces du diff et le SelfTest du
smoke passent. Restent ouverts les à-coups, les performances plein écran et
en forêt, la parité temporelle et la qualification native/WebGPU complète.
L'objectif global reste actif ; aucun passage de gate ni publication Git
n'est revendiqué.
