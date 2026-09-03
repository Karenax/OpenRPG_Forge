# Gate 1 — protocole Browser R1-023/R1-025/R1-026

Statut : **ACQUISITION AUTOMATISÉE BROWSER PASS — REVUE HUMAINE ET PREUVES GPU PRÉCISES EN ATTENTE**.

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

Le harnais a ensuite été intégré à `48038854b828003dd514562e34d5930e614422b4`, puis son garde
d'entrée utilisateur et son reçu d'ABORT fail-closed ont été durcis à
`534ce4faef69972dc1a495981387ac24a3ca1cfd`, arbre
`8576d501a02fee5eabfb246d7b5269f0b8b19f6f`. C'est ce dernier candidat exact qui a été acquis.
`R1-023`, `R1-025` et `R1-026` restent ouvertes.

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

## Acquisition automatisée exacte

La campagne canonique a été exécutée une seule fois, Low puis High, depuis le worktree détaché propre
`E:\OpenRPG_Forge\.gate1-browser-weather-acq-534ce4f`. Le serveur Trunk Release a été construit dans
un cache Cargo externe neuf puis servi uniquement sur `127.0.0.1:1420`. Le driver a utilisé Edge
`152.0.4191.53` avec un profil isolé par tier :

```powershell
pwsh -NoProfile -File .\scripts\Invoke-StudioTrunk.ps1 -Action serve -RequireExactCommit
```

```powershell
pwsh -NoProfile -File .\scripts\Invoke-Gate1BrowserWeatherMatrix.ps1 `
  -BrowserExecutable 'C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe' `
  -OutputRoot 'E:\OpenRPG_Forge\.gate1-tools\evidence\gate1-browser-weather-matrix-534ce4f'
```

Le run externe exact est
`534ce4faef69-20260903T161148993Z`. Son verdict automatisé est
`pass_pending_human_and_precise_gpu_evidence` :

- 6 checkpoints, 6 captures page et 6 captures canvas, soit 12 PNG ;
- Low puis High atteignent chacun `clear → rain → clear`, wetness `0 → 0,78 → 0`, avec huit frames
  stables par endpoint, une pose caméra fixe et une seule instance Bevy/WASM par tier ;
- les instances Low `forge-weather-proof-1788451912494-1601.800` et High
  `forge-weather-proof-1788451952438-1619.000` sont distinctes ;
- Bloom est absent en Low et présent en High ; le composant de color grading est présent dans les
  deux tiers ;
- console : 0 erreur et 0 warning par tier ; réseau : 0 échec par tier ; bundles servis : 4/4 exacts ;
- aucune entrée utilisateur bloquée et aucun reçu d'ABORT ; Git est propre avant et après
  l'acquisition ; le contrôle hôte post-run a constaté l'absence du serveur, des navigateurs et des
  profils contrôlés, sans attestation de nettoyage sérialisée dans le ledger ;
- `gpu_presentation=not_observed`, `headed_observed=false`,
  `weather_shader_consumption_proven=false`, `water_draw_attribution_proven=false`,
  `exact_render_frame_identity_proven=false`, `human_review_performed=false` et
  `gate1_routes_qualified=false` ;
- ledger global SHA-256
  `aae5df2efe336e2521d0d9a19a9c77be4484fe82b5a5a6869f2008a4c33c021a` ;
- manifeste SHA-256
  `40952f814fde7b433a9b443a8157cb20f3bfb59bcfd9bfd044b72aa5dc1e1267` ;
- inventaire canonique des 29 fichiers SHA-256
  `c43c9ef797f47138d29cc437646d5fae311eeceea1cf99b5b5df3bd9dfece5d9`. Le manifeste décrit les
  28 autres fichiers et s'exclut lui-même.

Ces preuves restent hors du dépôt ; aucun PNG ni média externe n'est ajouté à Git. Le ledger lie les
octets du protocole présents au commit acquis, consultables par Git même après cette mise à jour
documentaire.

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

Le contrat exige, pour Low puis High : trois ledgers Rust, trois captures de
page et trois captures du canvas, soit six checkpoints et douze PNG au total. Chaque fichier est lié
par taille et SHA-256 dans un manifeste externe au dépôt. Low exige Bloom absent ; High exige Bloom
présent ; les deux tiers exigent le color grading et les endpoints `clear → rain → clear`.

## Frontière de preuve

Les checkpoints Rust décrivent uniquement des états main-world et assets CPU/ECS. Malgré les captures
CDP acquises :

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
