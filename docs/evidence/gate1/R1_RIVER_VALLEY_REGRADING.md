# Gate 1 — Relief riverain corrigé et intégré, révision 2

2026-09-05, branche Rust, local non publié. Gate 1 reste ouverte.

## Résultat et périmètre

L'utilisateur a autorisé la correction du relief au-delà des berges actuelles et demandé de
poursuivre sans confirmations intermédiaires. La correction est intégrée dans
`examples/visual_valley/world-sketch.v1.json`, **révision 2** : ce n'est plus seulement un fichier
candidat extérieur. Aucun tracé XZ, largeur, landmark, verrou, seed ou référence artistique
n'est modifié. Seuls la révision et quatre paramètres de `river_main` changent :

```json
{
  "surface_heights_m": [28.0, 23.0, 8.0, 4.0, 3.0, 2.0, 0.5],
  "maximum_cut_m": 12.0,
  "maximum_fill_m": 40.0,
  "valley_blend_width_m": 120.0
}
```

Les candidats précédents — coupe seule à 40 m et profil encaissé à −12 m — restent rejetés.
L'ancienne source est conservée comme fixture dans
`crates/forge_world/tests/fixtures/world-sketch-pre-valley.v1.json` : ses tests historiques
continuent avec leurs assertions exactes, sans relaxation pour faire passer la nouvelle carte.
Un test supplémentaire couvre la source embarquée actuelle et la conservation des valeurs authorées.

## Implémentation

Le compilateur prépare un relief riverain à 1,5 m au-dessus du profil d'eau, puis le raccorde au
relief source par smoothstep sur 120 m au-delà du corridor de 42 m. Les coupes/remblais de ce
grading sont bornés et les emprises des villages/forteresses verrouillés sont protégées, avec
une transition de 16 m. Les routes et ruisseaux dérivés sont ensuite rééchantillonnés sur ce relief,
avant le carving fin et la recompilation du terrain, des collisions, de l'audit et du pont.
La protection d'emprise s'applique au grading large ; elle n'est pas une promesse de figer tous
les caches de route qui sont recalculés ensuite. Les données authorées des landmarks restent exactes.

Le raccord large ne change pas la largeur des exclusions de végétation ni celle du maillage de
berge. La végétation générée peut changer avec les pentes du nouveau terrain ; les placements
manuels du kit, leurs IDs, rotations et échelles sont conservés, avec ancrage au terrain recalculé.
Les deux pins WorldSketch/terrain de `runtime-preview-authored.v1.json` sont mis à jour ; le kit,
sa source Blender corrigée et son hash de provenance restent inchangés.

La révision 2 a exposé un défaut des chargeurs : les profils d'environnement, eau et météo restaient
révision 1. Le premier démarrage promu a correctement été refusé, code 2, et le test moteur a
signalé `RevisionMismatch`. Les deux chargeurs natif/Studio dérivent maintenant des copies liées
à la révision WorldSketch. Les modèles embarqués restent inchangés ; les contrôles d'identité
projet/zone/révision ne sont pas désactivés. Le test CLI de métadonnées attend désormais révision 2,
tout en conservant schema_version 1. Voir ADR-0008 pour la décision et les compatibilités.

## Tests et conservation vérifiés

- **312 tests workspace PASS** : Assets 14, CLI 7, Core 11, Engine 101, Project 45,
  Runtime 45, Studio 24, World 65.
- Trois compilations identiques pour le grading ; round-trip JSON exact.
- Budgets bornés, refus des largeurs invalides et des profils/budgets manquants.
- Grading identique bit à bit à la source hors rayon de raccord et dans les emprises protégées.
- Ancienne recette toujours testée ; comparaison de tous les autres champs authorés avec la
  révision 2 après retrait des seuls paramètres autorisés et normalisation de la révision.
- Les 346 centres d'eau sont au-dessus du terrain dans le test de la source embarquée actuelle.
- Clippy workspace toutes cibles/features et Clippy Studio WASM/web stricts : PASS.
- Builds CLI/runtime et Trunk final : PASS ; formatage et `git diff --check` : PASS.
- Avertissement amont restant : future incompatibilité Rust de `proc-macro-error2 2.0.1`.

## Preuve native et capture

Racine externe des preuves :
`E:\OpenRPG_Forge\.gate1-evidence-runtime\r1-river-valley-20260905`.

Le candidat avant incrément de révision a été compilé dans `compiled/`, lancé et capturé avec
le kit corrigé et la caméra héroïque Full HD : `hero.png`, frame 300, warm-up 180, sortie frame 420.
La capture a été ouverte et inspectée : le grand canyon et le canal surélevé du candidat antérieur
ne sont plus présents dans cette vue. Le raccord aux terrains environnants est large et le pont
repose près des berges. Les bandes de berge restent brunes/uniformes, la végétation et la palette
sont encore prototypales ; aucune acceptation artistique humaine n'est revendiquée.

La source révision 2 est recompilée dans `promoted/`. Les grilles sculptées et toutes les géométries
de splines de `compiled/` et `promoted/` ont été comparées et sont identiques ; la capture de ce
candidat est donc réutilisée pour cette géométrie, pas présentée comme une nouvelle capture de
révision 2. Un nouveau run sans `--world-sketch-input` vérifie bien la source **embarquée** :

```text
forge_runtime --world-sketch-visual-valley
  --asset-kit-build <preuves>/r1-063-authored-preservation-20260905-b/rebuild-1/asset-kit-build.v1.json
  --asset-kit-preview examples/visual_valley/asset-kits/r1-063/runtime-preview-authored.v1.json
  --bridge-placement <racine>/promoted/bridge-placements.v1.json
  --metrics <racine>/promoted-metrics-final.json --metrics-warmup-frames 60 --exit-after-frames 240
```

RX 7800 XT, Vulkan, pilote 26.8.1, 1920 × 1080 :

- code 0 ; arrêt **18:07:52.448089 UTC**, `runtime_errors_observed=false`, aucune ligne ERROR ;
- 42 022 hauteurs modifiées ; coupe maximale **8,797834 m**, remblai maximal **32,522274 m**.
  Le remblai est important et réparti sur le fond de vallée, pas masqué comme une petite retouche ;
- eau : marge observée sur les trois voies du mesh entre **0,8589535 et 2,8000002 m** ;
- 180 frames : moyenne 103,44 FPS, p95 11,0638 ms, 1 % low 77,43 FPS, aucun hitch > 16,7 ms.
  Smoke court, non qualification de performance ni mesure d'un gain d'optimisation.

## Pont et accès immédiats

Probe natif révision 2 : `--terrain <promoted>/terrain-carved.v2.json --bridge-placement
<promoted>/bridge-placements.v1.json --bridge-crossing-probe`.
Événement `bridge_crossing_probe_completed` du **18:03:51.312516 UTC**, code 0 :

- progression 42,5282 m pour une cible 42,49999 m ;
- **518/518 ticks au sol**, aucune perte de contact, 435 ticks sur le tablier ;
- pont horizontal de 36 m, pente 0,21012°, hauteur maximale au-dessus du terrain **3,90455 m** ;
- centre du croisement franchi ; bindings terrain, joueur et pont conservés ; état final au sol.

Ce probe qualifie le pont et ses accès immédiats, pas l'ensemble du réseau routier ou une navigation
RPG. Le fournisseur reste `not_selected`. Son succès est vérifié par son événement propre ; le
flag du harness général `automated_run=false` n'est pas utilisé comme preuve humaine.

## Studio WebGPU

Trunk final dans `final-dist/`. Chrome a réellement chargé
`http://127.0.0.1:14620/?forge_scene=world-sketch-visual-valley`, puis le viewport a été capturé
dans la conversation. Un monde initialisé, une caméra active ; `source_manifest_matched`,
aucune erreur retournée par l'outil de logs. Le serveur note uniquement un favicon absent (404).
Le Studio conserve sa vue joueur de diagnostic, sans kit Blender ni pont : ce n'est pas la vue
héroïque native ni une qualification artistique ou de déplacement prolongé du navigateur.

Les **cinq hashes communs observés** concordent entre métriques natives et ledger Chrome :

| Source | SHA-256 canonique observé |
|---|---|
| Terrain | `c9000f6ab51b7d716511dd718924f5cd6403c749456620bef86bb2120f364930` |
| Végétation | `2f0defd7659c62566270f86ad53a3575c8d815b9e36137877d69b9dc91be538a` |
| Environnement | `cfe82fa9e9c95b99c224cc537b8fd1f6bfc7a2432f5eccdf4feb7d0f6d8ab5e6` |
| Eau | `0deba0ba6829eed82f40003fbb1819770e14436c83bc8cab09dbde8d994657cc` |
| Météo | `1c4ed5e6346883157d0d483ccbbdc6ec816510c68507e8e1265e66cff9033181` |

Premier état ECS observé à 3994 ms ; préparation à 3994,1 ms. Le ledger mesure le CPU/ECS et
continue d'indiquer `gpu_presentation=not_observed`. L'image du navigateur est la preuve visuelle
distincte, conservée seulement dans la conversation. Onglet et serveur de preuve fermés ensuite.

## Hashes et limites de livraison

WorldSketch canonique : `9841898bfa77367b04d080e52e1b8ead6f0cfdb8edee3e17e5b57a3d6360f8d3`.
Pont canonique : `f0e44ba91e65dba028e80a27f56d5cd30b86c23e3f3b3909df2098dd81e5c8db`.

| Artefact | SHA-256 octets |
|---|---|
| source WorldSketch révision 2 | `85a568c25bd8329f9845de2c1e8d899d29df672db2ff4ab659136c3ab0ce1576` |
| `hero.png` | `93d3261de5228d555ad74ca2eeacfd35b9d6d1cf28ae7f3ca59c2e828ebc6a14` |
| `promoted-metrics-final.json` | `1c57de6ea28e266c46b858ceaea46e54d8bc3a0b2cd75166cd2cdb11a846704e` |
| `promoted-native-final.jsonl` | `edcf9d65501fc2eb5725c07df286e6a88840ee5b38d738ac9a1a1c12280fa4b9` |
| `promoted-bridge.jsonl` | `aa8063a787f1192df8f0c9522b82811907d4fdbf269e9c84c133b582651b3620` |
| `forge_runtime.exe` final | `e4c2ce1f89ba474dad531ea5c7fbe3ea7294afd92db9c452de7e1268c2d80d33` |
| `final-dist/forge_studio_bg.wasm` | `4043bb25c08fe165fca550449959d41bb11d8f0f2f54a557278d8141f7198223` |

La correction fonctionnelle du relief est intégrée. La finition des matériaux de berge, la qualité
artistique de la végétation, la comparaison Cadle et les validations humaines explicites restent
ouvertes. Aucun Gate 2+, commit, push, changement de branche, suppression du travail existant ou
asset Cadle. Les anciennes preuves restent historiques, sans les présenter comme actuelles.
