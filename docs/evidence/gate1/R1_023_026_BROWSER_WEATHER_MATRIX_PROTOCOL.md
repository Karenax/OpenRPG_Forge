# Gate 1 — protocole Browser R1-023/R1-025/R1-026

Statut : **IMPLÉMENTATION ET VALIDATIONS STATIQUES/COMPILATEUR — ACQUISITION BROWSER NON LANCÉE**.

Ce lot a été préparé dans un worktree Git détaché fondé exactement sur
`83f02928da72774a49d3d62ee68be85335b0bf01`. Les validations locales suivantes ont été exécutées
avec `CARGO_TARGET_DIR=E:\OpenRPG_Forge\.gate1-qualification-055cd36a\target` :

- `cargo test -p forge_studio gate1_browser_weather_matrix --all-features` : **PASS, 4/4** ;
- `cargo check -p forge_studio --all-targets --all-features` : **PASS** ;
- `cargo clippy -p forge_studio --all-targets --all-features -- -D warnings` : **PASS** ;
- `cargo check --locked --release --target wasm32-unknown-unknown -p forge_studio --lib
  --no-default-features --features web` : **PASS** ;
- la même commande avec `cargo clippy` et `-- -D warnings` : **PASS** ;
- `cargo fmt --all -- --check`, `git diff --check` et
  `scripts/Test-Gate1BrowserWeatherMatrix.ps1` : **PASS**.

Le premier check WASM a refusé deux ambiguïtés `With` (`E0659`) et le premier Clippy WASM deux
lints ; les corrections minimales ont été appliquées et les commandes finales ci-dessus ont été
rejouées avec succès. Cargo conserve seulement l'avertissement de compatibilité future de la
dépendance tierce `proc-macro-error2 v2.0.1`.

Aucun `trunk build`, `trunk serve`, navigateur, campagne Browser ou test GPU n'a été lancé. Ces
compilations ne constituent pas une acquisition : il n'existe donc encore ni candidat d'acquisition,
ni capture, ni ledger d'exécution à qualifier. `R1-023`, `R1-025` et `R1-026` restent ouvertes.

## Surface séparée

La route de preuve est strictement opt-in :

```text
/?forge_scene=visual-valley&forge_weather=clear&forge_quality=<low|high>&forge_gate1_weather_proof=v1
```

`forge_gate1_weather_proof=v1` est rejeté hors de `visual-valley`, avec une météo initiale autre que
`clear`, avec un profil autre que Low/High fixe, avec une valeur inconnue ou en cas de paramètre
dupliqué. Le chemin produit existant et le protocole temporel `R1-014` ne sont pas modifiés par cette
sélection.

Le contrat versionné est `scripts/gate1-browser-weather-matrix.v1.json`. Le contrôleur Rust publie
son état dans `#forge-gate1-browser-weather-proof-ledger` et accepte uniquement l'événement typé
`forge-gate1-browser-weather-proof-advance`. Le driver CDP ne pilote pas les entrées du jeu.

## Séquence par tier

Low et High utilisent deux lancements Browser séparés. Dans chaque tier, les trois checkpoints sont
acquis dans **la même instance Bevy/WASM**, sans rechargement :

1. `initial_clear`, wetness exacte `0` ;
2. transition demandée par le driver, puis `rain`, wetness exacte `0,78` ;
3. transition demandée par le driver, puis `restored_clear`, wetness exacte `0`.

Un checkpoint n'est publié qu'après huit updates Bevy consécutives où tous les endpoints CPU/ECS
attendus sont simultanément vrais. Une observation invalide remet le compteur à zéro. Chaque
transition a une limite de 30 secondes et toute commande anticipée, dupliquée, désordonnée ou mal
formée place le contrôleur dans un état terminal d'échec.

La caméra est replacée à chaque update sur une pose déterministe calculée depuis le point de la
centerline de rivière le plus proche de l'ancre `(16 m, 160 m)`, à 10 m de la rivière. La position,
la rotation, le FOV, l'unique caméra active, l'unique démarrage de viewport et le tier fixe font partie
de l'endpoint.

## Acquisition prévue

Après intégration sur un commit propre, dans deux terminaux ouverts sur ce même commit :

```powershell
pwsh -NoProfile -File .\scripts\Invoke-StudioTrunk.ps1 -Action serve -RequireExactCommit
```

```powershell
pwsh -NoProfile -File .\scripts\Invoke-Gate1BrowserWeatherMatrix.ps1 `
  -BrowserExecutable 'C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe' `
  -OutputRoot 'E:\OpenRPG_Forge\.gate1-tools\evidence\gate1-browser-weather-matrix'
```

Le driver refuse un worktree sale, un bundle servi différent de `dist`, une origine autre que la
boucle locale, une configuration non canonique et une destination située dans le worktree. Il lance
un profil Browser isolé et headed par tier, bloque les entrées utilisateur avant navigation, attend la
fenêtre de télémétrie Studio complète, puis vérifie console, réseau, dimensions, DPR, commit embarqué
et hashes des quatre bundles servis.

Le lancement omet explicitement tout argument headless et le ledger note
`headed_requested=true`, mais CDP n'atteste pas à lui seul la visibilité physique de la fenêtre :
`headed_observed=false` reste enregistré jusqu'à la revue humaine. Toute entrée clavier, souris,
pointer ou touch reçue par la page est comptée, bloquée et fait échouer l'acquisition au lieu d'être
présentée comme une pose fixe.

Une acquisition complète doit produire, pour Low puis High : trois ledgers Rust, trois captures de
page et trois captures du canvas, soit six checkpoints et douze PNG au total. Chaque fichier est lié
par taille et SHA-256 dans un manifeste externe au dépôt. Low exige Bloom absent ; High exige Bloom
présent ; les deux tiers exigent le color grading et les endpoints `clear → rain → clear`.

## Frontière de preuve

Les checkpoints Rust décrivent uniquement des états main-world et assets CPU/ECS. Même après une
future capture CDP :

- `gpu_presentation` reste `not_observed` dans les métriques Studio ;
- les uniforms de wetness concordants ne prouvent pas leur consommation par un shader ;
- les diagnostics optiques de l'eau n'attribuent ni draw, ni invocation de matériau, ni frame de
  rendu exacte ;
- les PNG n'équivalent pas à une inspection humaine acceptée ;
- `weather_shader_consumption_proven`, `water_draw_attribution_proven`,
  `exact_render_frame_identity_proven`, `human_review_performed` et `gate1_routes_qualified`
  restent faux dans le ledger du contrôleur ;
- le driver laisse explicitement `R1-023`, `R1-025` et `R1-026` ouvertes.

La fermeture demandera donc encore la revue humaine versionnée et les preuves GPU précises définies
par la roadmap. Ce protocole n'utilise ni ne copie aucun asset Cadle ; la scène continue de résoudre
les assets Forge gouvernés à leur emplacement existant.
