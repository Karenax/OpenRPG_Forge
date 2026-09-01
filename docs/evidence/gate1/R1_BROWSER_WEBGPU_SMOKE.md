# Gate 1 — smoke Browser WebGPU de Visual Valley

## Campagne courante d9e2670

La campagne courante vise `d9e2670111624aa836ab138afc35cddf441865ef`, arbre
`d6e5a40f73b47934444bda4514d5cdcb1dab093d`. Clear et rain passent chacun **24/24** contrôles :
9/9 modèles, 26/26 assets HTTP 200, zéro échec réseau, requête `.meta` ou entrée warning/erreur
console. Après warm-up complet, clear mesure 10,1175 ms de moyenne, p95 12,1 ms et 1 % low
77,519 FPS ; rain 9,5633 ms, p95 10,9 ms et 1 % low 85,106 FPS ; zéro hitch.

Les preuves courantes sont sous
[`browser/d9e2670111624aa836ab138afc35cddf441865ef/`](browser/d9e2670111624aa836ab138afc35cddf441865ef/)
et les captures [`clear`](captures/visual-valley-browser-webgpu-clear-high-d9e26701.png) et
[`rain`](captures/visual-valley-browser-webgpu-rain-high-d9e26701.png). Leur manifeste porte le
SHA-256 `c566fe7d3b8f36ea27e4f428301236e659488d3ed909c9d50ccf2ff26b2b50c7`.

Un premier passage 23/24, sous le seuil 1 % low, a été rejeté et conservé localement ; aucun seuil
n'a été abaissé. La comparaison native/Studio d9 passe ensuite 46/46. `gpu_presentation` reste
`not_observed`, Gate 1 reste ouverte. Voir
[`R1_GRAPHICS_FORWARD_IBL_CORRECTION.md`](R1_GRAPHICS_FORWARD_IBL_CORRECTION.md).

## Campagne historique a34596bd

Date de la campagne locale : **2026-09-01**

Commit Browser qualifié : **`a34596bd733131699c70028dab86cf2c612b896a`**

Verdict borné : **PASS AUTOMATISÉ LOCAL DU SMOKE BROWSER CLEAR/RAIN — R1-043 FERMÉE PAR LE REJEU
NATIF EXACT-COMMIT ; GATE 1 RESTE OUVERTE**

Le candidat charge Visual Valley dans le Studio Browser pour les variantes `clear` et `rain`, en
qualité High. Les deux exécutions passent chacune les **24/24** contrôles structurés de
[`validation.json`](browser/a34596bd733131699c70028dab86cf2c612b896a/validation.json). Ce résultat
prouve la cohérence du manifeste et de l'observation CPU/ECS du runtime WASM ainsi que le chargement
réseau du décor. Il ne transforme pas `gpu_presentation = not_observed` en preuve GPU et ne qualifie
ni la qualité artistique, ni la comparaison Cadle, ni la performance sur le GPU cible de Gate 1.

## Deux essais rejetés avant le résultat final

Les échecs ont été conservés comme diagnostic et n'ont pas été requalifiés en succès :

1. le premier essai restait à **0/9 modèles**. Bevy demandait des fichiers `.gltf.meta` et le serveur
   de développement répondait avec le fallback HTML ; la scène ne pouvait donc pas atteindre l'état
   de décor gouverné attendu ;
2. après correction du chargement, un second essai a été rejeté pour divergence du hash décor :
   `6b41ec73efde5b8a40ca0132e1d32462176d6b4810104f6bed69496569f81f5f` contre
   `6b14244116a4bc2e2e0612caa23077c70ef892fc06bb80759f1ee242067d30cf`.

Le résultat final n'émet aucune requête `.meta`. Le hash `scenery` sélectionné et observé dans les
deux ledgers Browser est désormais
`6b41ec73efde5b8a40ca0132e1d32462176d6b4810104f6bed69496569f81f5f`.

## Résultat final clear/rain

| Contrôle | Clear High | Rain High |
|---|---:|---:|
| contrôles structurés | **24/24 PASS** | **24/24 PASS** |
| parité interne Browser | `source_manifest_matched` | `source_manifest_matched` |
| décor | 9 chargés / 0 échec | 9 chargés / 0 échec |
| instances | 125 demandées / 99 remplacements / 26 détails | mêmes comptes |
| procéduraux cachés | 147 | 147 |
| assets réseau du décor | 26 réponses HTTP 200 | 26 réponses HTTP 200 |
| requêtes `.meta` / échecs réseau | 0 / 0 | 0 / 0 |
| entrées console | 0 | 0 |
| warm-up / échantillons | 60 / 120 | 60 / 120 |
| moyenne loop WASM | 10,7283 ms | 9,1683 ms |
| p95 loop WASM | 13,4 ms | 10,1 ms |
| 1 % low calculé | 54,945 FPS | 84,034 FPS |
| hitches `> 16,7 ms` | 2 | 0 |
| hitches `> 33,3 ms` | 0 | 0 |

Les **26** assets sont les neuf `.gltf`, les neuf `.bin` et les huit textures PNG effectivement
référencées. Les deux journaux réseau ont `failures = []` et les deux journaux console sont des
tableaux vides. Les manifestes sélectionné/runtime portent les huit hashes attendus, une caméra 3D
active sur une attendue, l'eau `observed_main_world_components`, la météo
`observed_cpu_projection`, le tier courant/appliqué `high` et le décor `activated`.

La parité `source_manifest_matched` ci-dessus est **interne à chaque exécution Browser** : elle
compare le manifeste sélectionné au manifeste observé par le runtime WASM. La comparaison croisée
native/Studio a ensuite été exécutée au même commit `a34596bd…` : 46/46 assertions passent et les
sept hashes communs aux deux formats de ledger concordent, dont scenery `6b41ec73…`.
**`R1-043` est fermée** par la preuve bornée
[`R1_043_NATIVE_STUDIO_EXACT_COMMIT.md`](R1_043_NATIVE_STUDIO_EXACT_COMMIT.md).

## Captures inspectées

Les captures sont des screenshots du viewport Browser complet en **1920 × 1080**. Le canvas déclaré
par le ledger mesure 1393 × 1067 pixels dans cette surface Studio.

- [Visual Valley Browser — clear High](captures/visual-valley-browser-webgpu-clear-high-a34596bd.png),
  SHA-256 `c30871ba1ca932de2f18b8dbb22b9c7013d4105c10927e068e17b47cd01ce646` ;
- [Visual Valley Browser — rain High](captures/visual-valley-browser-webgpu-rain-high-a34596bd.png),
  SHA-256 `d3b3326a34c6226e0475d625d9bf53c628a676e7c448f5938ed913a1ad2b34b0`.

L'inspection confirme que les deux images sont non blanches, différentes et que la pluie est visible
dans la variante `rain`. Elle montre aussi des limites nettes : palette très saturée et criarde,
ombres et contours noirs dominants, personnage capsule de preuve, et overlay runtime volumineux qui
masque une partie importante du cadrage. Ces captures ne valent donc ni validation artistique
humaine, ni revue des matériaux/composition, ni comparaison A/B avec Cadle.

## Sémantique GPU et performance

Les deux scene ledgers conservent exactement :

```text
gpu_presentation = not_observed
evidence_source = bevy_main_world_cpu_ecs_projection_observed_not_gpu_presentation
```

Les gate ledgers conservent `webgpu.status = checking`, `webgpu_ready_ms = null` et la raison
`Gate 1 runtime CPU/ECS observed; GPU presentation remains not_observed`. Le temps de frame mesuré
est celui de la **boucle d'update Bevy WASM**, pas une requête de timestamp GPU. Les p95 et 1 % low
ci-dessus ne peuvent donc pas être présentés comme un temps GPU, un benchmark matériel cible ou une
preuve des budgets RTX 3060/RX 6600.

Restent notamment ouverts : sonde de présentation GPU, timestamp GPU, GPU milieu de gamme, profils
qualité complets, revue humaine corrective, comparaison A/B Cadle et verdict final Gate 1.

## Artefacts structurés versionnés et empreintes

Les JSON ci-dessous sont des copies octet pour octet du dossier local
`.gate1-browser-smoke-artifacts/a34596bd733131699c70028dab86cf2c612b896a/`. Sauf pour le manifeste
lui-même, les tailles et SHA-256 viennent de
[`artifact-manifest.json`](browser/a34596bd733131699c70028dab86cf2c612b896a/artifact-manifest.json).
L'empreinte du manifeste est recalculée localement.

| Artefact versionné | Octets | SHA-256 |
|---|---:|---|
| [`artifact-manifest.json`](browser/a34596bd733131699c70028dab86cf2c612b896a/artifact-manifest.json) | 2 503 | `a636033fc0fbf55cde89efc2c34a7f7b2772a2c49dfcf4b34c8aa83dfce7a97c` |
| [`validation.json`](browser/a34596bd733131699c70028dab86cf2c612b896a/validation.json) | 3 507 | `913819da6299cfba198cf74df90b31a7cc2feb34f4f62fe4ab5aba28cb78b4f8` |
| [capture clear](captures/visual-valley-browser-webgpu-clear-high-a34596bd.png) | 1 296 307 | `c30871ba1ca932de2f18b8dbb22b9c7013d4105c10927e068e17b47cd01ce646` |
| [`clear.console.json`](browser/a34596bd733131699c70028dab86cf2c612b896a/visual-valley-clear-high.console.json) | 3 | `37517e5f3dc66819f61f5a7bb8ace1921282415f10551d2defa5c3eb0985b570` |
| [`clear.dom.json`](browser/a34596bd733131699c70028dab86cf2c612b896a/visual-valley-clear-high.dom.json) | 292 | `eeaf3824bd247dae74a6060a1369fdf8bbd9ebd86767e441e7c7a76597ed36b8` |
| [`clear.gate-ledger.json`](browser/a34596bd733131699c70028dab86cf2c612b896a/visual-valley-clear-high.gate-ledger.json) | 1 789 | `90afae69e4260be8d319e995bfea5afe53ea8e3fd8ca2f3b1454b3843ae3e9c0` |
| [`clear.network.json`](browser/a34596bd733131699c70028dab86cf2c612b896a/visual-valley-clear-high.network.json) | 6 620 | `fa08ee7a0e22d35abd1dfd89249fef4522a6db89f3261faab56b00155f11a5bb` |
| [`clear.scene-ledger.json`](browser/a34596bd733131699c70028dab86cf2c612b896a/visual-valley-clear-high.scene-ledger.json) | 4 042 | `36c99ceef5da02b5199ffbd7afe20eeaffba491ddcfcf738706275e6f0e524b3` |
| [capture rain](captures/visual-valley-browser-webgpu-rain-high-a34596bd.png) | 1 290 285 | `d3b3326a34c6226e0475d625d9bf53c628a676e7c448f5938ed913a1ad2b34b0` |
| [`rain.console.json`](browser/a34596bd733131699c70028dab86cf2c612b896a/visual-valley-rain-high.console.json) | 3 | `37517e5f3dc66819f61f5a7bb8ace1921282415f10551d2defa5c3eb0985b570` |
| [`rain.dom.json`](browser/a34596bd733131699c70028dab86cf2c612b896a/visual-valley-rain-high.dom.json) | 291 | `47e17cdcf3d38b268043fe8a04cfd7cf070fb4955b58f559e7f5319acf1daf00` |
| [`rain.gate-ledger.json`](browser/a34596bd733131699c70028dab86cf2c612b896a/visual-valley-rain-high.gate-ledger.json) | 1 786 | `95d8dd92ffff6ce65ac4d25a13caaa1a631cff420a2b491012bb3c646857353b` |
| [`rain.network.json`](browser/a34596bd733131699c70028dab86cf2c612b896a/visual-valley-rain-high.network.json) | 6 618 | `de2ecba5212baa93b04fe5b539746612701a799eeb5e6aab6e5d99afec195673` |
| [`rain.scene-ledger.json`](browser/a34596bd733131699c70028dab86cf2c612b896a/visual-valley-rain-high.scene-ledger.json) | 4 038 | `772f79c0db48c7c610d4da7e7d40c40c51ef3be3bb66e7e1ba421f7186cfb65f` |

## Verdict documentaire

Le smoke Browser clear/rain est reproductiblement archivé et ses contrôles automatisés passent au
commit `a34596bd…`. La présentation GPU reste non observée par le contrat, les métriques sont celles
de la boucle WASM et la qualité visuelle observée appelle une passe corrective. Le rejeu natif
exact-commit ultérieur ferme `R1-043`, sans fermer Gate 1 ni qualifier la présentation GPU Browser.
