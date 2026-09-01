# R1-040 — adapter runtime/Studio du profil qualité

Statut de cette preuve : lot technique autonome, sans qualification ni clôture de R1-040.

## Entrée et identité

- base Git : `f786a38aca2023f08d708687f99a47d14f34e6d4` ;
- document : `examples/visual_valley/quality-profile.v1.json` ;
- schéma : `forge.visual-valley-quality-profile` version 1 ;
- empreinte canonique attendue : `8dc219ad88ba1472b0ebbb13d8f4beac13f3fae1eeff688c928abb044d96d0e2` ;
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
cooldown, la dernière transition, le preset demandé complet et le statut de chaque champ. Le ledger
natif `forge.runtime-metrics` passe en version 7 par ajout optionnel du bloc `quality`. Le manifeste
Studio ajoute l'empreinte, la sélection et le tier ; les scènes Gate 0 conservent des champs nuls.

| Sous-système | Projection réelle | Limite déclarée, non simulée |
|---|---|---|
| Terrain | aucune modification du mesh source | les six champs LOD/cull/stride sont `not_applicable` : l'adapter terrain courant n'a qu'un LOD |
| Végétation | plages Bevy `VisibilityRange` près/moyen/HLOD/cull à chaque tier ; budget de brins appliqué lors de la construction du mesh | multiplicateur de densité non appliqué ; le budget de brins est `applied_at_startup` |
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

## Validation

Exécution locale Windows depuis le worktree détaché :

| Commande | Résultat |
|---|---|
| `cargo test -p forge_project quality --lib` | succès, 7 passés, 0 échec, 37 filtrés |
| `cargo test -p forge_engine --lib` | succès, 93 passés, 0 échec |
| `cargo test -p forge_runtime` | succès, 29 unitaires + 1 intégration, 0 échec |
| `cargo test -p forge_studio --lib` | succès, 20 passés, 0 échec |
| `cargo check -p forge_studio --lib --target wasm32-unknown-unknown --no-default-features --features web` | succès ; `forge_engine` est contrôlé comme dépendance WASM |
| `cargo clippy -p forge_engine -p forge_runtime --all-targets -- -D warnings` | succès |
| `cargo clippy -p forge_studio --all-targets -- -D warnings` | succès |
| `cargo clippy -p forge_studio --lib --target wasm32-unknown-unknown --no-default-features --features web -- -D warnings` | succès ; avertissement Cargo informatif de compatibilité future dans la dépendance amont `proc-macro-error2` |
| `cargo fmt --all -- --check` puis `git diff --check` | succès |

Ces validations prouvent les contrats de sélection, les transitions bornées, les composants Bevy
réellement modifiés, les ledgers et la compilation Web. Elles ne constituent ni un benchmark GPU,
ni une revue visuelle/humaine, ni une qualification de R1-040 ; la route reste donc ouverte.
