# Gate 1 — correction forward, IBL atmosphérique et requalification d9

Date : **2026-09-01**

Commit : **`d9e2670111624aa836ab138afc35cddf441865ef`**
Arbre : **`d6e5a40f73b47934444bda4514d5cdcb1dab093d`**

Verdict borné : **CORRECTION ET PAIRE EXACT-COMMIT PASS — GATE 1 RESTE OUVERTE**

## Cause et correction

La campagne historique `a34596bd…` reproduisait un warning wgpu lié au chemin différé/SSR, une
projection colorimétrique trop forte et de grandes zones noires. Le correctif courant :

- projette les valeurs Forge normalisées `temperature`/`tint` vers un faible offset CIE xy ;
- ajoute `AtmosphereEnvironmentMapLight` à la caméra, intensité `1.0`, résolution `256 × 256` ;
- rend l'eau et les berges par le chemin forward ;
- déclare le SSR écran indisponible dans la matrice Bevy 0.19.1 et résout honnêtement la réflexion
  demandée vers `disabled`, avec `reflection_fallback_used=true` ;
- résout la réfraction vers `surface_tint` ;
- empêche les changements de tier qualité de réinsérer SSR ou `DeferredPrepass` ;
- observe zéro caméra SSR et zéro caméra différée dans le chemin résolu ;
- classe `water.reflection_steps` `not_applicable`, soit 5 champs startup, 14 transition et 15 N/A.

Le profil authored conserve l'intention de réflexion. Le fallback est séparé, observable et ne doit
pas être présenté comme une implémentation SSR qualifiée. `R1-023` reste donc ouverte.

## Validation source

| Contrôle | Résultat |
|---|---|
| tests ciblés `forge_engine` | **120/120 PASS** |
| workspace/all-targets | **296/296 PASS** |
| `cargo fmt --all -- --check` | **PASS** |
| `cargo clippy --workspace --all-targets -- -D warnings` | **PASS**, zéro warning |
| check Studio WASM `web` | **PASS** |
| build `forge_runtime --release --locked` | **PASS**, 3 min 24 s |

Le seul avis Cargo est la future incompatibilité transitive connue de `proc-macro-error2 v2.0.1`.

Le binaire Release mesure **81 735 680 octets** et porte le SHA-256
`e44bc941b70eabab8b7e6155a262ab93491d9c2d4763454cb09bfd480e24cc75`.

## Preuve native exacte

Protocole pour `clear` puis `rain` :

```text
forge_runtime.exe --visual-valley --player-camera --weather clear|rain --quality high
  --capture <capture.png> --capture-frame 240
  --metrics <metrics.json> --metrics-warmup-frames 300
  --exit-after-frames 420 --no-vsync
```

`BEVY_ASSET_ROOT` pointait vers le worktree exact d9. Les deux processus terminent avec le code 0 sur
AMD Radeon RX 7800 XT, Vulkan, pilote AMD 26.8.1, en 1920 × 1080 High fixe, VSync off.

| Mesure native | Clear | Rain |
|---|---:|---:|
| échantillons | 120 | 120 |
| moyenne | 8,3478 ms / 119,792 FPS | 8,2920 ms / 120,598 FPS |
| p95 | 8,7132 ms | 9,0950 ms |
| 1 % low | 113,775 FPS | 96,354 FPS |
| hitches `>16,7` / `>33,3` | 0 / 0 | 0 / 0 |
| pipelines prêts | 69/69 | 71/71 |
| mémoire allouée / réservée | 530 273 124 / 876 609 552 | 534 407 108 / 876 609 552 |

Les deux logs sont vides. Cela signifie qu'aucun warning ni `ERROR` n'est enregistré dans ces deux
exécutions ; ce constat n'est pas extrapolé à tous les backends.

| Artefact natif | Octets | SHA-256 |
|---|---:|---|
| [`clear.log`](native/d9e2670111624aa836ab138afc35cddf441865ef/visual-valley-native-clear-high.log) | 0 | `e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855` |
| [`clear.metrics.json`](native/d9e2670111624aa836ab138afc35cddf441865ef/visual-valley-native-clear-high.metrics.json) | 26 768 | `384089ac53c74e47d9f7fadc5f51c3fc51ff27b614db46d80e4bfc9ec0b919c8` |
| [`clear.png`](native/d9e2670111624aa836ab138afc35cddf441865ef/visual-valley-native-clear-high.png) | 2 236 851 | `12fd7e86171b35b3d83bcf230d1e977a5625c978beb6c65033d1884a69318187` |
| [`rain.log`](native/d9e2670111624aa836ab138afc35cddf441865ef/visual-valley-native-rain-high.log) | 0 | `e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855` |
| [`rain.metrics.json`](native/d9e2670111624aa836ab138afc35cddf441865ef/visual-valley-native-rain-high.metrics.json) | 27 494 | `fa5fc0aae0f4c0478666217866fa1794875131654b8ef2edbc4299e226c5085b` |
| [`rain.png`](native/d9e2670111624aa836ab138afc35cddf441865ef/visual-valley-native-rain-high.png) | 2 255 196 | `6c0c2f13c6645aea7d58ee326900d6b5b7e3e77821d7c5ac2c721529a29704a6` |

## Preuve Browser exacte

Le Studio a été reconstruit et servi par `scripts/Invoke-StudioTrunk.ps1 -Action serve`. Les parcours
clear/rain utilisent un viewport 1920 × 1080, 60 frames de warm-up et 120 frames de mesure.

Un premier passage a été rejeté parce que `frame_thresholds` échouait : 1 % low 37,383 FPS en clear
et 32 FPS en rain. Il est conservé localement sous `.gate1-browser-smoke-rejected-artifacts/` et n'est
pas versionné comme succès. Aucun seuil n'a été abaissé. Après warm-up complet, la paire retenue passe :

| Mesure Browser | Clear | Rain |
|---|---:|---:|
| contrôles | **24/24 PASS** | **24/24 PASS** |
| moyenne | 10,1175 ms / 98,839 FPS | 9,5633 ms / 104,566 FPS |
| p95 | 12,1 ms | 10,9 ms |
| 1 % low | 77,519 FPS | 85,106 FPS |
| hitches `>16,7` / `>33,3` | 0 / 0 | 0 / 0 |
| réseau / console | 26 assets HTTP 200, 0 échec, 0 warning/erreur | identique |

Le ledger conserve `gpu_presentation=not_observed` et `webgpu.status=checking`. Les chiffres sont ceux
de la boucle Bevy WASM, pas des timestamps GPU.

| Artefact Browser | Octets | SHA-256 |
|---|---:|---|
| [`artifact-manifest.json`](browser/d9e2670111624aa836ab138afc35cddf441865ef/artifact-manifest.json) | 2 503 | `c566fe7d3b8f36ea27e4f428301236e659488d3ed909c9d50ccf2ff26b2b50c7` |
| [`validation.json`](browser/d9e2670111624aa836ab138afc35cddf441865ef/validation.json) | 3 507 | `cd6b196e06ca1e02a970c94d47ed64a6aa613f017284cd92616d8040ee6a792f` |
| [capture clear](captures/visual-valley-browser-webgpu-clear-high-d9e26701.png) | 1 293 570 | `819bd9994fafc8f4694d2e65300528bbf2d2b05f6e8946ae515305b58beb33d4` |
| [capture rain](captures/visual-valley-browser-webgpu-rain-high-d9e26701.png) | 1 323 858 | `7703f0c4207052010343585d12b4e6bc94ca076114f6e39a4acf4cd10be76e5a` |

Les douze JSON versionnés et les deux captures ont été comparés byte-for-byte aux artefacts externes.

## Parité native / Studio

Le validateur exact-commit passe **46/46 contrôles** : schémas, fenêtre 120 frames, budgets locaux,
résolution, qualité, pipelines, météo, décor, absence d'erreur, dimensions des captures et sept hashes
communs. Terrain, végétation, environnement, eau, météo, scenery et profil qualité concordent.
Le résultat structuré
[`cross-target/…/validation.json`](cross-target/d9e2670111624aa836ab138afc35cddf441865ef/validation.json)
mesure 2 490 octets et porte le SHA-256
`3fcc23549e726c603e299d775223b51e6d4e88b66cd3a3c7f3feec362627bf8a`. `R1-043` reste fermée sur
la paire d9.

## Verdict visuel et limites

Les captures d9 corrigent nettement le ciel rose, le terrain moutarde et les grandes ombres noires.
Le ciel est gris-bleu, le terrain vert et la pluie est lisible. Des contours de végétation très
sombres, du stipple/aliasing et quelques facettes sombres restent visibles. L'eau est hors cadrage.
Les images fixes ne prouvent ni vent, déplacement, réversibilité météo, stutter représentatif ni
qualité artistique comparative.

Gate 1 reste ouverte pour `R1-023`, les profils complets/adaptation, les parcours et vues
qualifiantes, le GPU cible RTX 3060/RX 6600 et les revues humaines `R1-049` à `R1-052`. Aucun asset
Cadle n'a été copié ou chargé par cette campagne.
