# R1-040 — Demande globale à chaud et cycle natif

2026-09-06, branche Rust, non publié. Gate 1/R1-040 ouverts ; CI distante NOT RUN.

## Implémentation

`GlobalQualityPlugin` est installé par Runtime avec `--quality`. `GlobalQualityRequest::request`
change le niveau demandé sans modifier les sources. PreUpdate vérifie la présence des sept ressources
avant toute transmission ; les adapters existants appliquent leurs changements ensuite.
Last rassemble les observations. Les six domaines portant un hash de politique doivent correspondre
à celui du terrain ; la pluie conserve son budget par niveau existant. Ordre des sept états :
terrain, végétation, ombres, environnement, eau, pluie, post-traitement.

Pas de transaction atomique de rendu : certains adapters peuvent rester en attente après l'envoi.
`requests_dispatched` n'est pas une preuve d'application. `available_budgets_projected` exige tous
les sous-budgets observés ; environnement/eau restent `Partial`. `full_preset_applied` reste false,
même si tous les relevés CPU concordent : aucune confirmation GPU globale ou adaptation automatique.
Le plugin suppose les politiques installées lors du chargement ; il ne remplace pas les sources
ou les plugins pendant une partie. UI Studio non raccordée.

Un test avec adapters absents vérifie trois demandes, aucun envoi partiel, sept Pending, aucune
confirmation. Le cycle CLI exige `--quality`, un dossier neuf et aucune automation concurrente ;
cas valides, doublons, absence du niveau et conflits testés.

## Reprise des adaptateurs — complément du 2026-09-06

La présence des sept ressources est maintenant vérifiée avant le raccourci « niveau déjà
envoyé ». Si une ressource manque, l'envoi antérieur est invalidé, le niveau demandé est
conservé et aucune distribution partielle n'est effectuée. Au retour de toutes les ressources,
le même niveau est redistribué. Avant correction, `sent == tier` empêchait cette reprise.
Pas de nouvel acquittement GPU, de fallback, de modification de carte ni d'adaptation automatique.

Test ECS sans renderer : envoi Low, retrait de la requête eau, observation de l'envoi invalidé,
modification du post-traitement survivant vers High, absence d'envoi tant que l'eau manque,
réinsertion de l'eau puis redistribution Low au post-traitement (bloom désactivé).
Le profil complet reste false. Les deux tests ciblés du coordinateur passent.
Ce test ne qualifie ni hot-reload de politique, ni remplacement sans absence observable,
ni destruction/reconstruction réelle du renderer dans le navigateur.

Validation finale : **464 tests workspace PASS / 1 timing préexistant ignoré** via
`cargo test --workspace --all-features --locked` ; Clippy workspace/all-targets/all-features
avec `-D warnings` PASS ; `cargo check -p forge_studio --target wasm32-unknown-unknown
--no-default-features --features web --locked` PASS. Formatage et diff check PASS.
Warning future-incompatibility préexistant `proc-macro-error2 2.0.1` conservé.
Pas de nouveau rendu natif/Web ni benchmark : le changement concerne la distribution des
requêtes, sans modification des shaders ou de la présentation normale.

Logs locaux : `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-quality-adapter-recovery-20260906`.
SHA-256 :

```text
tests.log  1df9286f0ca3076a3af919f5ed07054bfcbcbda34b9e5acbff5fa82f278f7df7
clippy.log 0fff5e5474f8fb8bb38d3c2086e9eda386db5db082fa89f7b871807f731a3c5e
wasm.log   cb79dfe9c9f3ccd198d3a6bd3d634b61b24cfe58743d486f7db76eec39dbdbd9
global_quality.rs 5f896bce69c0f750c5a3e316c5ba8ca4632cf8087fc257c16a69ad0beb8843fc
```

Les contrôles sont terminés, sans publication. Gate 1/R1-040 restent ouverts, CI NOT RUN.

## Cycle natif historique exécuté

`--quality high --quality-cycle-proof <dossier-neuf>` utilise le harness partagé, maintenant avec
le domaine Global. Chaque étape attend 180 observations des budgets disponibles, puis son PNG,
readback et fichier neuf. Ce n'est pas l'attente d'un preset complet. Source terrain/végétation/
environnement contrôlée constante par le harness ; les sources eau/météo ne sont pas comparées
par ce contrôle global spécifique. Sortie anticipée/refus/délai incomplet ne passent pas.

WorldSketch rev3, kit gouverné et aperçu human_bridge-clear du lot initial, météo rain,
RX 7800 XT/Vulkan, 1920×1080. Même processus High → Low → Medium → Ultra → High, aucune compilation
concurrente, binaire `fc84fc8698b238ffe00007377a2750c5eeb8ca352a9565ea3ed8b17321c20fe7`.
Sortie 0, runtime_errors_observed=false, zéro ERROR. Cinq PNG inspectés : terrain/pont/eau présents,
végétation et pluie changent ensemble et reviennent à High. Rendu stylisé, tramage visible,
eau granuleuse et grosses particules de pluie proches : pas d'acceptation artistique humaine.

Les cinq événements `global_quality_cycle_observed` rapportent chacun
`[CpuProjected, CpuProjected, CpuProjected, Partial, Partial, CpuProjected, CpuProjected]`
et full_preset_applied=false. Les états du log sont des chaînes Debug ; la ressource Rust est
typée et sérialisable, mais n'est pas encore ajoutée au ledger métrique global ni à Studio.
Les FPS instantanés des PNG ne constituent pas un benchmark. Coût et continuité de reconstruction,
adaptation et fallbacks complets restent à qualifier.

## Artefacts

`E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-global-cycle-20260906`, log adjacent. SHA-256 :

- module global : `d9775824e281c1843c8b49e28143125a8deb6926faf3ffdf04d7f4e6b3330a16` ;
- log : `cfb05775fa1d9ff97ef2bbb93bde21ff2a4952b0ba2908cd536d41679670d8d1` ;
- stage-0.png : `0b896ed1ae3a09af7d060bce09e18487f9342f931e314cf934f2b85cc2bb18d9` ;
- stage-1.png : `87b8063eadaad4d67c1d740d79ff85636dc267b9ba85d5d86572bded67a336c6` ;
- stage-2.png : `37666643a2dfbe2c5daef01812392c235fb43896c25354f778af1a051615fe50` ;
- stage-3.png : `656df516ae5c7c67737135c503ef57d6111d5cc0bbca48d7da79ca69ff78e43f` ;
- stage-4.png : `84cfb32fbaf510081ff69ffa14f0f582e9c300eee15a80a740a1141a98dfebe4`.

Clippy workspace tous targets/features, warnings en erreurs : PASS. **381 tests workspace PASS**.
Check moteur WASM avec `--no-default-features --features web,physics-rapier` : PASS, incluant
le coordinateur (il est conditionné par physics-rapier). Formatage et diff PASS.
Aucune dépendance ou asset ajouté, aucune publication Git.

## Addendum — ledger global v8, 2026-09-06

Voir aussi l'addendum de cohérence eau en fin de document : le budget caméra ne suffit
plus à projeter l'état global du domaine eau après ajout des commandes matérielles.

Le rapport `forge.runtime-metrics` passe à v8 et inclut `global_quality` seulement lorsque la
ressource est présente. L'observation globale est explicitement ordonnée avant l'écriture dans Last.
Le Studio utilise déjà cette même ressource ; pas de second calcul d'acceptation dans le CLI.
Le test de rapport sans scène vallée vérifie v8 et l'absence du nouveau bloc.

Deux exécutions natives séquentielles, même binaire
`488ac128f7b0aeb097a7fbaab8304f6445b19c1a2d932367d924b4e43a6fa6cb`, sans compilation concurrente :

- Gate 0 : `--metrics gate0.json --metrics-warmup-frames 60 --exit-after-frames 180` ; v8,
  bloc global absent, sortie 0 ;
- WorldSketch : `--world-sketch-visual-valley --quality high --metrics high.json
  --metrics-warmup-frames 600 --exit-after-frames 1020` ; sortie 0, niveau High, sept états
  exacts attendus (cinq cpu_projected/deux partial), available_budgets_projected=true,
  full_preset_applied=false. Assertions JSON exécutées avec échec bloquant sur divergence.

Pas de kit externe ni capture dans ce smoke de sérialisation ; aucun rendu changé et aucune
qualification visuelle/performance nouvelle revendiquée. Sorties locales dans
`E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-global-ledger-20260906`.

SHA-256 : gate0.json `838ac595444e9420afae1e8f03efd6e9a7358d1d68de87672c6868246b819acf`,
gate0.log `958dd5a18119c83bc23839a4b6a16e8eba70eb491476fe8225b43f75eecbb3c6`,
high.json `ffe6e785df0b5f20499d01143ffc50f49a6223f7f2a1b0470d9b4277c567a30d`,
high.log `d7573060cccf1fcfe725e9e08862c3e2a617bb73747fc0905858be0aba017edd`.
Clippy workspace tous targets/features, **381 tests workspace**, check moteur WASM, formatage
et diff PASS. CI toujours NOT RUN ; Gate 1 ouverte.

## Cohérence eau et prérequis adaptatif — 2026-09-06

Le bilan global ne vérifiait que le tier, le hash et le budget SSR du domaine eau. Depuis
les commandes de teinte/réflectance, cela pouvait déclarer les budgets disponibles projetés
sans preuve de l'application matérielle. Il exige maintenant aussi `surface_tint_projected`
et `material_reflectance_projected`. Un de ces champs false laisse le domaine Pending ;
les trois présents donnent Partial, jamais un preset optique complet.

Régression synthétique du bilan global : observation eau initiale issue du plugin ECS,
puis invalidation indépendante des deux preuves matérielles en conservant le SSR valide ;
Partial → Pending pour chaque absence → Partial après restauration. Les autres adapters
sont absents dans cette fixture : aucun succès global n'est revendiqué. **443 tests workspace
PASS, 1 timing ignoré**, Clippy strict workspace/all-targets/all-features et Studio WASM PASS,
fmt/diff-check PASS. Aucun shader/rendu changé ; pas de nouvelle capture ni benchmark.
Warning de future compatibilité proc-macro-error2 2.0.1 toujours présent.

Preuves : `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-global-water-evidence-20260906/`,
`tests.log`, `clippy.log`, `clippy-web.log`. SHA-256 des tests :
`fa49b63356d9e8a716ead8b4b8aaede9771863387578e27c5bab8396bb25d3ac`.

L'inspection du contrôleur pur confirme que `from_applied_tier` exige un niveau installé
et `acknowledge_applied` tous les presets concernés effectivement appliqués. L'adaptation
ne peut donc pas être honnêtement activée en branchant simplement le booléen des budgets
partiels : eau/environnement restent partiels et `full_preset_applied` reste false.
Ce lot corrige le bilan préalable, **n'implémente pas l'adaptation automatique**. Il faut
résoudre/qualifier les capacités manquantes, ou faire approuver explicitement un contrat
de presets à fallbacks avant d'en changer la sémantique. Les travaux visuels et les preuves
indépendantes de Gate 1 restent possibles. Aucun changement de gate, commit ou push.
