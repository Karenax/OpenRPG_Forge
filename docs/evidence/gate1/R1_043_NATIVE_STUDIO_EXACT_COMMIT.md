# R1-043 — parité native / Studio au commit exact

Date de campagne locale : **2026-09-01**

Source qualifiée : **`a34596bd733131699c70028dab86cf2c612b896a`**

Arbre qualifié : **`8ccdf8cb2cb6a6aabd3a4569f22f1778e164ced6`**

Verdict borné : **R1-043 PASS AUTOMATISÉ LOCAL — GATE 1 RESTE OUVERTE**

Le runtime natif Release a été reconstruit puis exécuté depuis un worktree détaché propre au même
commit que les deux exécutions Browser déjà archivées. Les variantes `clear` et `rain` utilisent
Visual Valley, la qualité High fixe, 1920 × 1080 et VSync désactivée. Les deux processus terminent
avec le code 0 et produisent chacun une capture, un ledger `forge.runtime-metrics` v8 de 120 frames
post-warm-up et un journal structuré.

Cette preuve ferme la divergence factuelle qui maintenait `R1-043` ouverte : le natif et le Studio
portent désormais le même commit source et le même hash scenery
`6b41ec73efde5b8a40ca0132e1d32462176d6b4810104f6bed69496569f81f5f`.
Elle ne qualifie ni la présentation GPU Browser, ni un GPU milieu de gamme, ni la qualité visuelle.

## Construction et exécutions

Le worktree de preuve était propre et détaché sur le commit ci-dessus. Le binaire a été produit avec :

```powershell
$env:CARGO_TARGET_DIR='E:\OpenRPG_Forge\.cargo-target-gate1-combined'
cargo build -p forge_runtime --release
```

Résultat : **PASS en 3 min 23 s**. Le binaire
`forge_runtime.exe` mesure **81 728 000 octets** et porte le SHA-256
`70966031f73569eecc31035497bb853cb7cd0251525dc4b2909a70c47093ee9e`.

Les deux lancements utilisent le protocole suivant, avec `weather` et les noms de sortie adaptés :

```text
forge_runtime.exe --visual-valley --player-camera --weather clear|rain --quality high
  --capture <capture.png> --capture-frame 240
  --metrics <metrics.json> --metrics-warmup-frames 300
  --exit-after-frames 420 --no-vsync
```

`BEVY_ASSET_ROOT` pointait vers le worktree exact `a34596bd…`. Aucun asset Cadle n'a été copié ou
chargé par ces exécutions.

## Résultats natifs exact-commit

| Mesure | Clear High | Rain High |
|---|---:|---:|
| GPU / backend | RX 7800 XT / Vulkan | RX 7800 XT / Vulkan |
| résolution / VSync | 1920 × 1080 / off | 1920 × 1080 / off |
| samples qualifiés | 120 | 120 |
| moyenne | 7,4702 ms / 133,866 FPS | 7,6855 ms / 130,115 FPS |
| p95 | 7,7622 ms | 8,1653 ms |
| 1 % low | 125,519 FPS | 115,730 FPS |
| hitches `>16,7 ms` / `>33,3 ms` | 0 / 0 | 0 / 0 |
| pipelines prêts pendant la mesure | 68/68 | 69/69 |
| décor | 9 chargés / 0 échec | 9 chargés / 0 échec |
| mémoire wgpu allouée / réservée | 561 528 460 / 876 609 552 octets | 565 725 588 / 876 609 552 octets |

Ces chiffres qualifient uniquement la RX 7800 XT de développement et une fenêtre courte à caméra
fixe. Ils ne sont pas extrapolés à la RTX 3060 ou RX 6600 exigée par `R1-041`.

## Comparaison des données avec le Studio Browser

Le validateur local a passé **46/46 assertions** sur les deux météos : schémas, nombre d'échantillons,
résolution, qualité, pipelines, météo, décor, performances bornées, dimensions des captures, absence
d'erreur et correspondance des hashes exposés dans les deux formats de ledger.

| Identité de source | Natif v8 | Studio Browser v7 | Résultat |
|---|---|---|---|
| terrain | `9f16780a…` | `9f16780a…` | **MATCH** |
| végétation | `5c7619a6…` | `5c7619a6…` | **MATCH** |
| environnement | `4b252f03…` | `4b252f03…` | **MATCH** |
| eau | `c581e9ef…` | `c581e9ef…` | **MATCH** |
| météo | `b0d8e12b…` | `b0d8e12b…` | **MATCH** |
| scenery CC0 | `6b41ec73…` | `6b41ec73…` | **MATCH** |
| profil qualité | `8dc219ad…` | `8dc219ad…` | **MATCH** |

Le manifeste Studio contient en plus le hash `player_controller` `941eceef…`. Le ledger natif v8 ne
répète pas ce hash sous forme de champ autonome ; l'identité est bornée par le commit et l'arbre
exacts communs, mais elle n'est pas présentée comme un huitième match de ledger. Le Browser conserve
`gpu_presentation = not_observed` et ses temps restent ceux de la boucle Bevy WASM, pas du GPU.

## Limite graphique reproduite

Chaque journal natif contient exactement un warning `wgpu_core::validation` et zéro `ERROR`. Le
warning indique qu'un vertex shader écrit `@builtin(position)` sans `@invariant` dans un pipeline à
comparaison de profondeur `Equal`. Les deux captures reproduisent les motifs noirs et la palette très
contrastée déjà observés. La parité de données est donc acquise, mais la qualification graphique ne
l'est pas.

## Artefacts versionnés

| Artefact | Octets | SHA-256 |
|---|---:|---|
| [`clear.log`](native/a34596bd733131699c70028dab86cf2c612b896a/visual-valley-native-clear-high.log) | 913 | `406663a6afbe2ac458d995b426c79aae2397303b594490a24a7c517a20711f8a` |
| [`clear.metrics.json`](native/a34596bd733131699c70028dab86cf2c612b896a/visual-valley-native-clear-high.metrics.json) | 28 740 | `d268735afd5a444b1fd0f5d55e776fceb9c81bd4e8fa5c6841f93d7f05fb7219` |
| [`clear.png`](native/a34596bd733131699c70028dab86cf2c612b896a/visual-valley-native-clear-high.png) | 2 221 921 | `33f2e696ac730ea1619ab170659d4c41d412e26ff76f24960d433d88ecaabdb9` |
| [`rain.log`](native/a34596bd733131699c70028dab86cf2c612b896a/visual-valley-native-rain-high.log) | 913 | `1bd7eed1d520e292619776d7540bc894158d985429d97bca010c601cb67d0116` |
| [`rain.metrics.json`](native/a34596bd733131699c70028dab86cf2c612b896a/visual-valley-native-rain-high.metrics.json) | 29 506 | `ef25f1daf499d8517808fd08497757f19e92ae72ce8b15ca7b8f620b61ef5c30` |
| [`rain.png`](native/a34596bd733131699c70028dab86cf2c612b896a/visual-valley-native-rain-high.png) | 2 222 321 | `d15c609429286851c8a83c965fa637400bd255b3a78795f0ec28e4d38746c278` |

Les six copies versionnées ont été comparées aux artefacts externes par taille et SHA-256 : **6/6
identiques**.

## Conclusion bornée

`R1-043` est fermée sur la parité de données native/Studio au commit exact `a34596bd…`. Gate 1 reste
ouverte : warning/artefacts graphiques, présentation GPU Browser, profils Low/Medium/High/Ultra et
adaptation, parcours temporel, GPU cible et revue humaine comparative restent dus.
