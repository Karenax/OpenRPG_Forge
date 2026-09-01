# R1-040 — adapter runtime/Studio du profil qualité

Statut de cette preuve : lot technique autonome testé puis intégré au candidat combiné. Le format,
les tests CPU, le check Studio WASM exact et le Clippy strict exact du candidat passent ; les smokes
GPU natif/browser restent **NOT RUN**. R1-040 reste donc ouverte.

## Entrée et identité

- base Git de départ du lot autonome : `f786a38aca2023f08d708687f99a47d14f34e6d4` ;
- document : `examples/visual_valley/quality-profile.v1.json` ;
- schéma : `forge.visual-valley-quality-profile` version 1 ;
- empreinte canonique attendue : `8dc219ad88ba1472b0ebbb13d8f4beac13f3fae1eeff688c928abb044d96d0e2` ;
- empreinte native/Studio : la même valeur est exposée dans `quality.profile_hash` du ledger natif
  et `source_hashes.quality` du manifeste Studio ;
- sélection runtime stricte : `--quality low|medium|high|ultra`, `--adaptive-quality` ou
  `--benchmark-quality`, options mutuellement exclusives et réservées à `--visual-valley` ;
- sélection Studio stricte : `forge_quality=low|medium|high|ultra|adaptive|benchmark`, paramètre
  unique et réservé à `forge_scene=visual-valley` ;
- sans sélection explicite, les deux applications utilisent le preset par défaut du document
  (`High`) en mode fixe.

Le benchmark résout toujours `High`, avec adaptation désactivée. Cette propriété est validée à la
fois par le contrat `forge_project`, le constructeur de l'adapter et un test de non-transition sous
charge lente simulée.

## Projection observable

Le ledger `forge.visual-valley-quality-runtime` version 1 contient l'empreinte du profil, le mode, le
tier de démarrage, le tier courant, le tier effectivement projeté, le nombre de transitions, le
cooldown, la dernière transition, le preset demandé complet et le statut de chaque champ. Dans le
candidat combiné, le ledger natif `forge.runtime-metrics` est en **version 8** : il conserve le bloc
optionnel `quality` avec les blocs `scenery`, `gpu_memory` et `pipeline_warmup` issus des autres
tranches. Le ledger de scène Studio est en **version 7** et ajoute à son manifeste/observation le tier
courant, le tier réellement appliqué et les bornes LOD de remplacement attendues. Les scènes Gate 0
conservent ces champs à `null`.

Une métrique Gate 1 gouvernée exige la composition complète terrain, ombres, végétation, scenery,
environnement, eau, météo et qualité. Le bloc qualité doit exposer exactement 34 champs uniques et
documentés : 5 `applied_at_startup`, 15 `applied_on_tier_transition` et 14 `not_applicable`. La
fenêtre native doit contenir au minimum **120 échantillons post-warm-up** finis et positifs.

| Sous-système | Projection réelle | Limite déclarée, non simulée |
|---|---|---|
| Terrain | aucune modification du mesh source | les six champs LOD/cull/stride sont `not_applicable` : l'adapter terrain courant n'a qu'un LOD |
| Végétation | plages Bevy `VisibilityRange` près/moyen/HLOD/cull à chaque tier ; budget de brins appliqué lors de la construction du mesh | multiplicateur de densité non appliqué ; le budget de brins est `applied_at_startup` |
| Scenery gouvernée | la plage de visibilité des remplacements suit le preset appliqué : Low `48-128 m`, Medium `60-160 m`, High `72-180 m`, Ultra `96-240 m` | la concordance ECS/ledger ne constitue pas une preuve de présentation GPU ni une revue visuelle |
| Ombres | activation, nombre de cascades, distance, résolution `DirectionalLightShadowMap` et `ContactShadows` | exécution GPU non déduite des composants main-world |
| Environnement | activation atmosphère et résolution du milieu de diffusion au démarrage ; tonemapping projeté | volumétrie non qualifiée, donc trois champs `not_applicable` ; résolution atmosphère non reconstruite lors d'une transition |
| Eau | activation/désactivation SSR et nombre de pas `ScreenSpaceReflections` | distance maximale SSR absente de l'API Bevy 0.19 ; réflexion environnement non qualifiée ; réfraction écran indisponible, donc repli authored sans prétendre l'appliquer |
| Météo | budget exact de gouttes appliqué au mesh de démarrage ; visibilité pluie projetée | budget de gouttes non reconstruit lors d'une transition ; wetness appliqué seulement au démarrage |
| Post-traitement | bloom, color grading et tonemapping ajoutés/retirés via leurs composants réels | aucune présentation GPU n'est inférée |

## Adaptation bornée

Le contrôleur ne consomme que des deltas finis et strictement positifs. Il respecte les fenêtres
d'upgrade/downgrade du profil, remet le compteur opposé à zéro, exige la durée soutenue complète,
applique le cooldown, borne les tiers à `minimum_tier..=maximum_tier` et ne change que d'un tier par
transition. Les valeurs de démarrage qui nécessitent une reconstruction de mesh ou de ressource
restent identifiées comme telles dans le ledger quand le tier courant change.

La transaction ECS complète s'exécute uniquement au démarrage et lors d'un changement de tier ; le
ledger continue toutefois de synchroniser ses compteurs à chaque frame. Bloom, color grading et la
visibilité pluie consultent le preset courant dans le système météo : une transition Low vers
Medium/High réinstalle le Bloom avec les paramètres authored du profil météo. Le test ECS démontre
qu'à tier stable la transaction qualité ne marque pas de nouveau ses composants comme modifiés.

Dans le candidat combiné, l'écriture des métriques gouvernées et la readiness Studio restent en
attente tant que `dynamically_applied_tier` ne correspond pas au tier courant. Elles rejettent aussi
une plage LOD des remplacements scenery différente des distances moyen/HLOD du preset effectivement
appliqué. Ce contrôle rend l'écart observable ; il ne le transforme pas en validation GPU.

Le natif empreinte le tier courant/appliqué, l'état et les comptes scenery et sa plage LOD ; cette
empreinte doit être stable avant et pendant la mesure. Studio v7 empreinte la scène, les tiers
courant/appliqué et les deux plages LOD qualité/scenery. Une transition de tier, une projection
incomplète ou toute modification de cette empreinte réinitialise la fenêtre Studio complète : 60
frames de warm-up, puis exactement 120 frames mesurées.

## Validation du lot autonome

Exécution locale Windows depuis le worktree détaché avant l'intégration combinée :

| Commande | Résultat |
|---|---|
| `cargo test -p forge_project quality --lib` | succès, 7 passés, 0 échec, 37 filtrés |
| `cargo test -p forge_engine quality::tests --lib` | succès, 5 passés, 0 échec, 88 filtrés ; retrait/réactivation et stabilité ECS couverts |
| `cargo test -p forge_engine --lib` | succès, 93 passés, 0 échec |
| `cargo test -p forge_runtime` | succès, 29 unitaires + 1 intégration, 0 échec |
| `cargo test -p forge_studio --lib` | succès, 20 passés, 0 échec |
| `cargo check -p forge_studio --lib --target wasm32-unknown-unknown --no-default-features --features web` | succès ; `forge_engine` est contrôlé comme dépendance WASM |
| `cargo clippy -p forge_engine -p forge_runtime --all-targets -- -D warnings` | succès |
| `cargo clippy -p forge_studio --all-targets -- -D warnings` | succès |
| `cargo clippy -p forge_studio --lib --target wasm32-unknown-unknown --no-default-features --features web -- -D warnings` | succès ; avertissement Cargo informatif de compatibilité future dans la dépendance amont `proc-macro-error2` |
| `cargo fmt --all -- --check` puis `git diff --check` | succès |

Ces validations prouvent les contrats de sélection, les transitions bornées, les composants Bevy
réellement modifiés, les ledgers et la compilation Web **du lot autonome**. Elles ne sont pas une
validation du candidat combiné actuel.

## Validation actuelle du candidat combiné

| Validation | Résultat |
|---|---|
| `cargo fmt --all -- --check` | **PASS** |
| `git diff --check` | **PASS** |
| tests bibliothèque `forge_engine` | **PASS — 118/118** |
| tests `forge_runtime` | **PASS — 32/32** (31 bin + 1 intégration) |
| tests `forge_studio` | **PASS — 31/31** (28 lib + 3 bin) |
| `cargo check -p forge_studio --lib --target wasm32-unknown-unknown --no-default-features --features web` exact courant | **PASS — 7.45 s** |
| `$env:CARGO_TARGET_DIR='E:\OpenRPG_Forge\.cargo-target-gate1-combined'; cargo clippy -p forge_engine -p forge_runtime -p forge_studio --all-targets --locked -j 1 -- -D warnings` | **PASS — 6.29 s** |
| Smoke runtime natif produisant `forge.runtime-metrics` v8 | **NOT RUN** |
| Smoke GPU réel avec changement de tier et LOD scenery observé | **NOT RUN** |
| Smoke Browser WebGPU produisant le ledger Studio v7 | **NOT RUN** |
| Revue visuelle/humaine et benchmark matériel cible | **PAS ENCORE EXÉCUTÉS** |

Les résultats PASS qualifient uniquement le format, les tests CPU, la compilation WASM et le Clippy
strict du candidat combiné. Sans smoke natif, smoke Browser WebGPU, benchmark matériel et revue
visuelle/humaine, ils ne qualifient pas R1-040 ; la route reste ouverte.
