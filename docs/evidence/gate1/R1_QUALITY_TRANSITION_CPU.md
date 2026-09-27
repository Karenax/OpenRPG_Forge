# R1-040 — Coût de projection des changements de qualité

## Diagnostic du 11 septembre — publication et extraction des meshes

Après le correctif berges, inspection du JSON déjà acquis
`r1-terrain-bank-reuse-20260911/resume-2/studio-motion.json`, sans nouveau rendu.
Le pic Low de 143,5 ms suit les appels préparation végétale 24,9 ms,
commit végétal 6,6 ms et forêt inclusive 23,1 ms (dont far-batches 5,1 ms).
Le pic retour High de 264,5 ms suit 27,4/8,5/24,6 ms respectivement
(far-batches 5,4 ms inclus). Aucun appel terrain au second pic.
Ces phases ne couvrent pas le reste de la frame ; ne pas attribuer le résidu
au GPU par soustraction, ni additionner deux fois les far-batches.

Lecture du code exact installé Bevy **0.19.1**, registre Cargo local
`C:/Users/karen/.cargo/registry/src/index.crates.io-1949cf8c6b5b557f/` :

- Forge `vegetation/quality/transition.rs::reconcile_inner` publie tous les
  meshes préparés dans le même appel. Les meshes procéduraux utilisent
  `MAIN_WORLD | RENDER_WORLD` : copie d'extraction dans `bevy_render-0.19.1/`
  `src/render_asset.rs` autour de la ligne 351.
- `src/mesh/allocator.rs::allocate_and_free_meshes` (243) puis
  `allocate_meshes` (362) parcourent tous les meshes extraits ; ils n'utilisent
  pas le limiteur `RenderAssetBytesPerFrameLimiter` dans ce chemin.
- `src/mesh/mod.rs::RenderMesh::byte_len` (152) existe, mais limiter
  `prepare_assets` ne borne donc pas cette allocation/publication globale.
- `src/render_asset.rs::prepare_assets` enlève en outre l'ancien RenderAsset
  avant le contrôle du budget pour les nouveaux éléments extraits (autour de
  435). Activer simplement `RenderAssetBytesPerFrame` ne garantit pas la
  conservation de l'ancien rendu pendant l'attente.

**Décision : ne pas activer ce limiteur comme correctif des transitions.**
Pas de fork Bevy, nouveau cache d'égalité ou changement des profils. La piste
suivante est une publication de meshes dérivés répartie avant extraction,
avec ancien rendu conservé jusqu'au remplacement prêt, invalidation sur
nouvelle requête/source, libération des ressources annulées et acquittement
du plan seulement après installation complète. Cette piste reste **à implémenter**,
avec coût mémoire transitoire borné ; les IDs authorés/collisions/retouches
ne doivent pas changer. Elle n'est pas qualifiée par la présente lecture.

Ce diagnostic exclut un réglage inadéquat mais n'identifie pas encore la part
mesurée de l'extraction/allocation dans les 264,5 ms. Aucun test Rust, build,
processus graphique ou nouveau gain annoncé dans ce lot.

## Actualisation du 11 septembre — terrain Studio

Le terrain Web est désormais préparé un chunk par frame, après la végétation.
700 tests PASS, Clippy natif/Web PASS et smoke réel Studio Ready 5/5 acquis ;
voir [preuve terrain](R1_TERRAIN_STAGED_PREPARATION.md). Les mentions ci-dessous
du terrain Web synchrone décrivent l'ancien candidat. Application finale et
qualification de fluidité restent ouvertes ; aucun gain FPS revendiqué.

## Actualisation du 10 septembre — préparation végétation Studio

**Essai fonctionnel acquis à 21:49** : même bundle de contrôle, cycle
Low/Medium/High/Ultra/High terminé en 34,3811 s. JSON et PNG 1408 × 966 exportés,
relus et capture inspectée. Application des sept domaines, High restauré,
identité GPU identique à l'application, queue terminée, zéro pipeline en attente
ou en erreur. Sources de scène identiques à la baseline Studio claire précédente.
Un seul monde initialisé ; terrain, végétation, forêt lointaine et personnage
affichés. Aucun benchmark ni qualification du mouvement déduit de cette image.

Preuves sous `r1-profile-prewarm-20260910/incremental-resume/` :
`receipt.json`, `studio-clear.json` (SHA-256
`8b454faae7b00f75e45d8fd6bee22468ebd5718349aab46bf24cbb717c9ee109`),
`studio-clear.png` (SHA-256
`a10e546b25f6d7d0728fc039f12e82ab7157aee8716fc132a08cbf4dbbac63d8`).
Les huit processus possédés sont fermés. Les interruptions décrites plus bas
restent historiques ; ne pas relancer ce smoke comme entièrement NOT RUN.

Le préchauffage des quatre profils est désormais acquis séparément dans
`R1_PROFILE_PREWARM.md`. Il ne supprime pas le travail CPU de changement de profil.
Le chemin natif actuel prépare déjà les projections en arrière-plan : le texte
du 7 septembre ci-dessous décrit une étape antérieure, pas son architecture actuelle.

Le chemin Web végétation est maintenant réparti en une étape de préparation par
chunk et par appel de la frame principale. La future est sondée explicitement une
fois par frame, sans boucle de microtâches ni worker Web supposé. L'ancienne
projection reste en place jusqu'au résultat complet ; une demande remplacée est
abandonnée. Un seul travail intermédiaire est conservé, pas quatre copies des
projections. Le calcul géométrique commun reste inchangé et le chemin natif garde
son worker existant.

Limites : génération initiale, copie/hash des sources et commit ECS final ne sont
pas répartis par ce changement ; le terrain Web reste un travail distinct. Cette
modification ne vaut ni absence de saccades ni fermeture de R1-040.

Validation : 697 tests PASS / un timing ignoré, dont parité des statistiques,
inventaires, attributs et indices de meshes, absence de mutation des bindings
pendant la préparation et abandon d'une demande remplacée. Compilation Clippy
stricte native et Web réussie. L'essai Studio de ce lot doit utiliser un
nouveau binaire ; les captures et hashes du préchauffage précédent ne qualifient
pas ce nouveau code. Les journaux `incremental-*` sont séparés sous la racine
`r1-profile-prewarm-20260910`. Le build fonctionnel Web de contrôle conserve Rust
release mais désactive seulement la passe Binaryen finale (`data-wasm-opt="0"`)
dans un HTML temporaire hors dépôt. Aucun changement du build de distribution.

Build de contrôle Web PASS (Rust release 2 min 47 s), bundle Studio PASS.
Les deux premiers essais de configuration temporaire ont échoué (chemin absolu
HTML Windows puis sélection de toolchain hors dépôt) ; corrigés par chemins
relatifs et `RUSTUP_TOOLCHAIN=1.98.0`, sans modifier la configuration du produit.
Journaux d'échec conservés, succès dans `incremental-web-build-c.log`.
Lancement du bundle SHA-256
`5D3B94B1F188A75D4BD0D41564196F904A8E0D279AEE54BE7223304B007B47D4`,
PID 4076, le 10 septembre à 21:20:36 +02:00. Computer Use interrompu par
Échap utilisateur au repérage initial : aucune capture ni interaction du nouveau
candidat qualifiée. Arrêt du candidat de test et de ses enfants, travail conservé.
L'essai graphique de cette préparation incrémentale reste NOT RUN.

Reprise ultérieure le 10 septembre, même bundle SHA-256, PID 33812 lancé à
21:28:08 +02:00 : scène effectivement affichée, bouton de préparation actionné,
passage `Idle` → `Warming` → `Ready 5/5` observé dans le Studio. Vue 1408 × 895,
monde initialisé une seule fois. Ce constat remplace NOT RUN pour ce seul smoke
fonctionnel, pas pour la qualification temporelle complète. L'export JSON/PNG
n'a pas été confirmé : interactions de fenêtre instables puis interruption
Computer Use par Échap utilisateur. Aucun benchmark ni nouvelle preuve de
fluidité revendiqué. Candidat et enfants arrêtés au point sûr ; Gate 1 ouverte.

## Mesures historiques du 7 septembre

7 septembre 2026, branche Rust. **Gain CPU et délais natifs vérifiés ; R1-040/Gate 1 ouverts.**

## Cause mesurée et changement

Le probe existant `quality_preparation_cpu_probe`, exécuté explicitement avec `--ignored
--nocapture`, mesure sur trois tours 159–165 ms de préparation végétation Low, 542–578 ms
High et 685–698 ms Ultra. Il ne suffisait pas à expliquer les transitions de plusieurs secondes.
Deux événements natifs `quality_projection_cpu` isolent ensuite les projections : le terrain
High prend 3816 ms contre 556 ms pour la végétation. Ce coût justifie le travail effectué.

La préparation indépendante des chunks terrain utilise maintenant le pool Compute Bevy existant.
Seules des références à la géométrie et aux contraintes entrent dans les tâches scopées. Leur
ordre de sortie est celui des entrées ; toutes doivent terminer et réussir avant le commit
séquentiel existant des meshes. Pas de travail détaché, pas d'accès ECS/assets dans les workers,
pas de résultat périmé appliqué après une retouche. Aucun algorithme géométrique, profil, source,
collider, budget, shader, format, dépendance ou asset ajouté/modifié par cette optimisation.
Le chemin mono-thread reste compatible ; aucun gain Web n'est déduit du résultat natif.

Deux nouveaux tests comparent les payloads au calcul séquentiel sur quatre chunks sculptés,
les quatre profils et une contrainte de berge, avec deux replays ; géométrie et attributs sont
comparés sans tolérance, bits positions/normales/UV inclus. Les chunks inchangés restent ignorés,
une longueur d'inventaire incohérente ou un chunk invalide échoue sans commit partiel.

## Protocole natif avant/après

Même Ryzen 5 7600 (6 cœurs/12 threads), RX 7800 XT, Vulkan/AMD 26.8.1, 1920×1080, caméra et
WorldSketch canoniques, sans kit/prototypes, sans VSync. Aucun build concurrent aux mesures.
Le binaire de référence inclut les mêmes sondes de temps mais conserve la préparation séquentielle.
Il est conservé localement sous `forge_runtime-before.exe`, hors dépôt.

```text
forge_runtime --world-sketch-visual-valley --quality low --adaptive-quality --no-vsync --exit-after-frames 4000
forge_runtime --world-sketch-visual-valley --quality high --no-vsync --metrics-warmup-frames 300 --exit-after-frames 1800 --metrics <fichier-frais.json>
```

Chaque commande est exécutée avec chaque binaire. La première est un scénario adaptatif, pas
un benchmark FPS à profil fixe. La seconde fournit 1500 frames mesurées High, adaptation désactivée.

| Projection terrain CPU | Avant | Après |
|---|---:|---:|
| Low initial | 1176 ms | 322 ms |
| Medium | 1647 ms | 443 ms |
| High | 3816 ms | 737 ms |
| Ultra | 3638 ms | 744 ms |

Les quatre projections reconstruisent toujours 64 chunks. La végétation reste comparable :
Medium 284 → 281 ms, High 556 → 580 ms, Ultra 702 → 702 ms. Aucun gain ne lui est attribué.

| Proposition → acquittement GPU effectif | Avant | Après |
|---|---:|---:|
| Low → Medium | 2,31 s | 1,11 s |
| Medium → High | 5,05 s | 1,99 s |
| High → Ultra | 4,72 s | 1,85 s |

Les trois montées restent automatiques, avec acquittements séparés. Le coût terrain High/Ultra
baisse d'environ 80 % sur cet essai ; le délai complet reste perceptible et n'est pas qualifié fluide.
La préparation scopée attend encore les workers : ce n'est pas une projection asynchrone étalée
sur plusieurs frames. Le pic mémoire des tâches concurrentes n'a pas été profilé.

| High fixe, même protocole | Avant | Après |
|---|---:|---:|
| Moyenne frame | 7,989 ms | 7,889 ms |
| p95 | 8,579 ms | 8,380 ms |
| FPS moyens | 125,17 | 126,75 |
| 1 % low | 66,09 FPS | 111,81 FPS |
| Frames >33,3 ms | 2 | 0 |

Pas de régression observée sur ces runs courts. Les deux hitches de la référence ne sont pas
attribués au calcul initial, exclu par le warmup : ces nombres ne prouvent pas un gain FPS causal
ou statistiquement qualifié. Ils ne qualifient pas non plus le matériel cible RTX 3060/RX 6600.

Source globale et plan effectif High identiques avant/après, y compris les cinq sources liées.
Géométrie terrain identique : 192 meshes, 149789 sommets, 274416 triangles, 20 chunks protégés.
High fixe est Ready/GPU acquitté, adaptation désactivée ; `full_preset_applied` reste false.
Les quatre runs finissent au nombre de frames prévu, code 0, `runtime_stopped.success=true`,
`runtime_errors_observed=false`, aucun événement ERROR. Aucune nouvelle revue artistique ou capture
des frames intermédiaires : les tests de géométrie ne remplacent pas cette qualification.

## Validations et preuves

**535 tests workspace PASS / 1 timing préexistant ignoré**, en plus du probe CPU explicitement
exécuté. Clippy strict workspace all-targets/all-features, Studio Web/WASM, build natif,
fmt et diff PASS. La première passe Clippy a refusé une fonction trop longue ; extraction de
deux helpers sans désactiver le contrôle. Échec conservé dans `clippy.log`.
Warning futur proc-macro-error2 2.0.1 conservé. CI distante **NOT RUN**, ADR-0006.

```text
cargo test -p forge_engine quality_preparation_cpu_probe -- --ignored --nocapture
cargo clippy --workspace --all-targets --all-features -- -D warnings
cargo test --workspace
cargo check -p forge_studio --target wasm32-unknown-unknown --no-default-features --features web
cargo build -p forge_runtime
cargo fmt --all -- --check
git diff --check
```

Environnement build inchangé : Rust épinglé, cache `terrain-water-20260906`, incremental=0,
`RUSTFLAGS=-C debuginfo=0`, TEMP/TMP `terrain-water-temp-20260906`.
Racine de preuve : `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-transition-cost-20260907`.

```text
forge_runtime-before.exe 7A681893A83E5696F5DC5106697E95232EA0FF0BD5F2C52F3C7DF3922C0E26D1
forge_runtime après A543EF4F0BC671D7E0B2FB88D1BFD3FC46A7EA891CCF983F3926989E51529004
preparation-before.log 45BF56E0AEE3562B711A85F4F2373AEE45C6C751A9BF2FF055BF3BB5520DDADF
clippy-pass.log D433B2091B8F5C3B6ADF54C12767D1C2DED0AD4337D7F7B22FCD3301917B7C61
tests.log 4018C1990C05CDF43A46B00FCC203BA99B5C2C12D6F1D5690A612FEFF45DB3C7
wasm.log FE1F90A68D3FE6A6F45878FE7037C75294E1E39D1CAB31DC644A176ED16D07A3
build-after.log BA38D5015B56AB6A11B1A6AB21E8BE91067E3A777D747C03B376945CC2C7AEF0
native-before.jsonl 71F4FC06DA8F7FAAA23F6435EF6539743BA6E1A39719315F5DC77089231E06D8
native-after.jsonl 82F18DCA857FD43B8C9AF1E0CB508085E3549A848AA6A35A49F5C48AA07FACFD
fixed-before.json D6084F2B5C41C9F6FFA35F59A57E2FA810B5A4D891F0B83270587D4713D777D6
fixed-after.json 958F27F458024C4917AACE7CD76E71FEABEA09A95AB3B2B4543676D686822587
fixed-before.jsonl 413FDE588F98CD481B38A8B05843177CEEE67AFABDD4EC01385CA14336A64B01
fixed-after.jsonl 618514A4A2C5FEF45B9CC31538D525F3798CC30ED49983D40EF458E1E9B25FD4
```

Rust, HEAD de base `f786a38aca2023f08d708687f99a47d14f34e6d4`, worktree modifié préservé,
suivi local origin/Rust derrière de 103 commits sans fetch. Pas de qualification sur arbre propre.
Aucune publication, suppression, branche recréée ou asset Cadle. Toutes les sessions sont terminées.
R1-040 reste ouvert : accès/qualification Studio-Web, baisse sous charge réelle et fluidité restent
à compléter. Gate 1 conserve ses critères artistiques, matériels et validations humaines ; aucune
Gate 2 commencée. Le coût principal mesuré est réduit : ne pas poursuivre un micro-audit CPU
général à partir de cette preuve.

## Localisation actuelle des gels — cycle du 7 septembre, sans nouvelle mesure

Lecture du journal existant `r1-cloud-gpu-inventory-cycle-logged-20260907/runtime.log`
sous `E:/OpenRPG_Forge/.gate1-evidence-runtime/`. Cette scène isole les nuages sans
forêt expérimentale : ne pas l'appeler benchmark du candidat complet. Binaire dev,
captures pendant transitions ; aucune comparaison release ni qualification de fluidité.

| Niveau demandé | Terrain, projection CPU totale | Végétation, préparation | Végétation, total |
|---|---:|---:|---:|
| Low | 355,00 ms | 277,43 ms | 293,83 ms |
| Medium | 424,25 ms | 499,27 ms | 508,20 ms |
| Ultra | 775,88 ms | 815,68 ms | 830,45 ms |
| High, retour | 768,73 ms | 612,88 ms | 632,63 ms |

La lecture de `vegetation/quality/transition.rs::apply_quality` confirme que
`prepare_vegetation_projection_with_quality` est appelé synchroniquement par un
système exclusif avant `reconcile`. La préparation représente 612,88/632,63 ms
au retour High et 815,68/830,45 ms en Ultra. Ce n'est pas un micro-écart imperceptible.
Le précédent parallélisme terrain attend ses workers, comme déjà documenté ci-dessus.

**Prochain correctif proposé, pas implémenté** : préparation détachée et bornée,
en conservant l'ancienne projection jusqu'au résultat complet. Il doit respecter :

- une seule préparation en vol, demandes successives regroupées vers la dernière ;
- aucun accès World/Assets/ECS dans le worker, sources copiées ou immuables identifiées ;
- abandon d'un résultat dont demande ou sources ne correspondent plus à l'état courant ;
- application atomique, erreurs explicites et projection précédente conservée ;
- tests de changement rapide, retour au niveau initial, échec et invalidation ;
- stratégie WASM explicite, sans prétendre qu'une future seule crée un worker navigateur.

Ne pas introduire un cache de tous les presets pour masquer ce coût sans mesurer
sa mémoire. Le présent lot ne change pas le code et ne lance ni build ni benchmark.
Le plan demeure soumis à une intégration native/Web réelle ; aucun gain annoncé.

## Préparation végétation détachée — implémentation native

La préparation des changements de niveau utilise maintenant `AsyncComputeTaskPool`
dans les applications natives possédant un RenderApp. La projection initiale, les
appels headless et le Web restent synchrones ; le terrain n'est pas changé dans ce lot.
Le worker reçoit des copies des documents et de la demande, jamais World/Assets/ECS.
Une seule tâche est retenue par projection. Les demandes intermédiaires ne démarrent
pas de tâche supplémentaire : à la fin, seuls niveau, politique et hashes source
courants autorisent le résultat. Sinon il est jeté et la dernière demande est préparée
au tick suivant. Retour au niveau courant : maintien de la scène et retrait du résultat
devenu inutile une fois terminé. Pas de cache des quatre presets.

Les contrôles de bindings vivants précèdent toujours l'application. Pendant l'attente,
statut `preparing_background_previous_projection_retained`, niveau projeté inchangé ;
aucun acquittement global anticipé. Une erreur conserve la projection précédente.
`preparation_ms` mesure le calcul ; `elapsed_ms` est désormais explicitement décrit
par `elapsed_scope=current_main_thread_call_excludes_background_wait`, donc ne pas
le confondre avec le délai demande → acquittement ni avec la durée du worker.

Trois nouveaux tests worker : calcul réel sans mutation des bindings, rejets sur
changement de niveau/politique/terrain/végétation et propagation d'erreur, demandes
rapides sans seconde tâche et abandon après retour au niveau courant. Les tests
headless existants exercent leur chemin synchrone, pas une fausse exécution GPU.
5 tests ciblés PASS / 1 timing ignoré ; Clippy moteur strict PASS ; workspace
605 PASS / 1 ignoré ; check Studio WASM PASS. Erreurs initiales d'import `check_ready`,
usage arithmétique de Revision et lint corrigées sans désactiver les vérifications.
Build natif PASS puis cycle réel terminé. Pas de gain FPS, de fluidité Web ou de
qualification de forêt revendiqué.

### Exécution native après raccordement

Preuve : `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-vegetation-background-cycle-20260907/`.
Même script, scène, kit et paramètres que le cycle nuageux précédent, toujours sans
forêt expérimentale. Les pins incluent maintenant les trois sources végétation ;
inchangés à la sortie. Runtime sortie 0, aucun ERROR, cinq étapes
High/Low/Medium/Ultra/High acquittées GPU et cinq captures. Preset complet non qualifié.
Exécutable SHA-256 `8abc8ab47797b819b259aa80a0c7427f23e2f6cbea139acb542a6c6e3ac53815` ;
journal `dd16914beb6064354afc78572c3586636a3a0009b3c8f7b8ad100ed2e3f23f08`.

| Niveau | Préparation dans le worker | Appel principal final |
|---|---:|---:|
| Low | 380,14 ms | 76,92 ms |
| Medium | 673,71 ms | 66,34 ms |
| Ultra | 991,25 ms | 57,78 ms |
| High, retour | 767,60 ms | 61,70 ms |

Le calcul n'est pas plus rapide sur ces runs : il est sorti de la boucle principale.
L'appel final inclut vérification des sources et réconciliation, et reste supérieur
au budget frame. Coût de lancement/copie et pic mémoire non qualifiés par cette table.
Terrain toujours synchrone : 366,88–857,47 ms pendant les transitions, initial High
2174,40 ms séparé. Aucun gain FPS ou fluidité globale déduit de cette paire dev/captures.

Low `captures/stage-1.png` et retour High `captures/stage-4.png`, 1080p, inspectés :
géométrie/restauration visuellement cohérentes ; pas de contrôle des frames intermédiaires.
Hashes `5ebc905ff81d9fc527afb981d57a69f78c2fbb6effee40890fe9e950f3daf036` et
`761b3f7c0558066f718c212e69499bfa5de98bd8f5e9d353ed1654d53fec6ad5`.
Hitches encore présents au retour High. Suite : terrain avec contraintes de berges
préservées, puis chemin complet ; Web distinct, aucune promotion de gate.

## Préparation terrain détachée — rejeu natif, 7 septembre

Le chemin natif avec RenderApp prépare désormais les empreintes des chunks et les
géométries LOD dans une tâche de fond unique. Les copies possédées ne contiennent
aucun World/Assets/ECS ; les contraintes de berges font partie de l'identité du
résultat, avec source, politique et niveau. Les bindings sont contrôlés avant
application. Les chemins headless et WASM restent synchrones, sans cache des presets.

Pendant l'attente, les meshes précédents restent visibles mais `projected_tier`
reste `None` : une nouvelle berge au même niveau ne peut pas qualifier l'ancien
terrain. Un test ECS vérifie cette attente, la conservation des handles, puis le
retour du niveau qualifié avec une berge contrainte et un hash source inchangé.
Deux tests worker comparent la géométrie au calcul synchrone et rejettent les
résultats périmés (source, politique, niveau ou berges).

Après correction du statut d'attente : **14 tests terrain PASS**, Clippy moteur
strict PASS ; workspace **608 PASS / 1 ignoré**, check Studio WASM PASS
(avertissement futur de la dépendance proc-macro-error2 2.0.1 conservé).
Reconstruction native PASS et rejeu graphique terrain détaché acquis ci-dessous.
Les preuves végétation ci-dessus
restent historiques et ne qualifient pas ce nouveau binaire. Aucun gain FPS,
fluidité globale, physique nouvelle ou passage de gate revendiqué.

Preuve : `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-terrain-background-cycle-20260907/`.
Script `Run-CloudGpuInventoryCycle-20260907.ps1`, même scène/kit/clouds que le run
végétation, toujours sans forêt expérimentale. Sortie 0, pins inchangés, aucun ERROR,
cinq étapes High/Low/Medium/Ultra/High `gpu_ready=true`, `full_preset_applied=false`.
Avertissements wgpu position sans invariant avec comparaison Equal conservés ;
aucune absence d'artefact sur toutes les machines déduite de ces captures.

| Niveau terrain | Calcul worker | Appel principal final |
|---|---:|---:|
| Low | 460,97 ms | 5,98 ms |
| Medium | 588,81 ms | 5,37 ms |
| Ultra | 886,70 ms | 4,60 ms |
| High, retour | 1017,46 ms | 4,67 ms |

Initial High séparé : 819,00 ms de calcul, 2,77 ms d'application. Les anciennes
transitions synchrones mesuraient 366,88–857,47 ms dans l'appel terrain. Le calcul
n'est pas accéléré : l'attente est déplacée hors de cet appel. Copie de lancement,
allocation GPU, pic mémoire et latence bout en bout ne sont pas qualifiés ici.
La végétation conserve 75–85 ms dans son appel final ; des hitches restent visibles
dans l'histogramme. Ce cycle dev avec captures ne constitue pas un benchmark FPS.

Captures 1920×1080 Low et retour High inspectées : terrain, pont et rivière présents,
retour de la densité High, pas de trou évident sur ces deux vues. Les frames
intermédiaires et la continuité physique pendant une édition ne sont pas qualifiées.
Le ciel demeure stylisé/flou et non accepté, conformément au retour humain.

SHA-256 : exécutable `1322b92345cd889c72e69d36cfec76fdafc3913523bf2979959aaa8c2115d3bd` ;
journal `0ef1318685a3747a62d2c2f2c511e2c078cfe8097be3d005596ea878f439b81d` ;
Low `b8d1fa5e7486f686f33a83835611c962d31d2b61af95c1ec0130c6001dc84ff6` ;
High `1eaf5cb4a3edcecc3bb3e179c24f906286045ef42072bd9cb9dfdf3780b4a12b`.

### Smoke du même binaire avec forêt candidate complète

Exécution native ultérieure, sortie 0 à 420 frames, dans
`E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-terrain-background-full-forest-20260907/`.
Même exécutable et paramètres de scène/kit/clouds que ci-dessus, avec catalogue
`r1-conifer-sprays-20260907/runtime/prototype-catalog.v2.json`,
`--prototype-forest-batches`, High fixe, capture demandée à 300 frames et métriques.
Pas de cycle global : son refus de forêt non qualifiée reste volontairement intact.

PNG inspecté : forêt gouvernée, terrain, rivière et pont présents. Diagnostic final :
650 arbres, 7800 primitives de catalogue, 8132 bindings mesh réels, zéro mesh non
comptable ; kit révision 4, 20/20 instances et 36 colliders Rapier prêts. Terrain High,
64 chunks, 20 protégés, hash `18bd933d37f06a6a08a34b19938a78cb8f6fe8d9667b34bd2dcd6355956fd208`.
Aucun ERROR dans le journal. Ce smoke prouve le chargement conjoint, pas la continuité
des changements de qualité de la forêt, une collision parcourue ou l'acceptation artistique.

La fenêtre initiale 1920×1080 est passée à 2560×1369 (trois événements de resize),
PNG final 2560×1369. La cause n'est pas établie. Les 359 échantillons, 45,90 FPS moyens,
p95 25,14 ms et 1 % low 24,75 FPS restent un smoke dev avec capture et résolution
variable : aucune comparaison 1080p ni régression de performance attribuable au correctif.
Pas de nouvelle campagne répétée pour transformer cette observation en benchmark.

SHA-256 : PNG `c4d05439c45f6e407b7ea443b21bc276875becbc81d0f0fe6078e6ee1a48c2da` ;
métriques `dfc46c3d1b37c3352dab59810e40c9935323ac0c155b07410596c6260d527394` ;
journal `f625a4b951811df60726b6c12f70003f84df2682e693a6bf80416fa65a4e0279`.

Inspection du blocage forêt : `global_quality/effective.rs` refuse explicitement
tout prototype preview ; ADR-0011 n'autorise pas d'exception forêt. Les compteurs
réels existent désormais, mais `vegetation/far_batches.rs` regroupe les LOD2 sans
simplifier la géométrie. Ne pas retirer ce refus sur la seule base de ce smoke.
La conformité de la représentation lointaine et ses transitions restent à établir
selon ADR-0010, séparément du chargement maintenant vérifié sur le nouveau binaire.

### Remplacement du profil à niveau constant — 7 septembre

Régression reproduite avant correction : remplacer les paramètres terrain High par
ceux du Medium dans un profil validé, sans changer le niveau demandé, conservait le
hash et la projection du profil précédent. Le test
`replacement_policy_at_same_tier_rebuilds_without_changing_sources_or_handles`
échouait sur le hash de politique (ancien `5ea5f85f…`, demandé `878c9464…`).

Le cache mémorise maintenant le hash du profil effectivement appliqué. Les retours
rapides et les préparations synchrones/détachées invalident sur changement de profil
ou de niveau. Ce hash n'est promu qu'après application complète des résultats ;
l'ancienne géométrie reste présente, non qualifiée, pendant le calcul détaché.
Le profil source de la vallée et ses paramètres ne sont pas modifiés.

`cargo test -p forge_engine --lib terrain_quality --locked --offline` : **15 PASS**,
sortie 0. Le nouveau scénario exerce les deux chemins, exige la reconstruction de
tous les chunks, les nouvelles distances de morphing, les mêmes entités/handles et
le même hash du terrain canonique. Le contrôle Clippy suivant a refusé deux égalités
flottantes du test ; remplacées par égalité des bits, sans tolérance ni exemption.

Il s'agit d'une preuve ECS de cohérence des caches, pas d'un éditeur de profils,
d'un worker Web, d'une mesure de fluidité ou d'une qualification GPU. Les captures
et bundles précédents restent attachés à leur code antérieur ; aucun nouveau rendu
n'est déduit de ce correctif. Gate 1 reste ouverte.

Contrôles après correction des assertions : fmt et Clippy moteur strict (toutes
les cibles natives) PASS. Un premier check WASM lancé à tort avec la feature
desktop par défaut échoue sur Tauri absent de cette cible ; pas un PASS Web.
Le check correct `cargo check -p forge_studio --lib --no-default-features --features web
--target wasm32-unknown-unknown --locked --offline` passe ensuite, sortie 0.
L'avertissement futur `proc-macro-error2 v2.0.1` reste visible.

SHA-256 des sources contrôlées :

- `terrain_quality.rs` : `b77ea4de0e4e77839e0a71a9d14b6022ffb53eb71c4360bec07f5ad242f40def` ;
- `terrain_quality/background.rs` : `be9d883ad4825f8b34490225200ac948a0c68c061c25b8769772536d9663f7f4` ;
- `terrain_quality/tests.rs` : `08d0a3628aa5bfc6752282a4720d23e98f2a20fdc0de29f0db050c9be6a8a637`.

Suite finale `cargo test --workspace --locked --offline -q` : sortie 0,
**611 PASS, 0 FAIL, 1 timing ignoré** (dont 271 PASS/1 ignoré pour le moteur avec
les features unifiées du workspace). Fmt/diff PASS ; branche Rust, aucun commit/push.

### Profil végétal remplacé et reprise d'échec — 7 septembre

Le même défaut a été reproduit dans `vegetation/quality/transition.rs` : le retour
rapide comparait seulement le niveau projeté, tandis que le verrou d'une tentative
échouée retenait seulement le niveau demandé. À High constant, le test conservait
les distances 24/72/180/384 m au lieu des 20/60/160/320 m du profil de remplacement.
Ce scénario échoue avant correction ; aucune source authorée n'a été remplacée.

La projection courante doit maintenant correspondre au niveau **et** au hash de
politique ; la tentative mémorise également ce couple. Un profil corrigé au même
niveau peut être préparé, sans relancer une tentative inchangée. Le hash observé
reste celui de la projection réellement appliquée, pas celui d'un travail en attente.

Le test ECS de cycle existant est étendu (pas un test supplémentaire comptabilisé) :
remplacement High, conservation des hashes de source/collisions des arbres, échec
Low puis reprise au même Low avec une nouvelle politique valide. Ce nouveau scénario
exerce le chemin synchrone ; les tests worker existants vérifient séparément le rejet
des résultats périmés par politique, niveau et sources, sans preuve GPU déduite.

`cargo test -p forge_engine --lib vegetation::quality --locked --offline` :
**5 PASS, 0 FAIL, 1 probe timing ignoré**, sortie 0. Aucun benchmark, capture ou
nouveau bundle lancé pour ce correctif de cohérence. Aucun profil canonique modifié,
aucun éditeur de profil livré, aucune capacité forêt ou Gate 1 promue.

Contrôles finaux : Clippy moteur `--all-targets -- -D warnings` PASS, check Studio
`--lib --no-default-features --features web --target wasm32-unknown-unknown` PASS,
workspace `cargo test --workspace --locked --offline -q` sortie 0 : **611 PASS,
0 FAIL, 1 timing ignoré**. Total inchangé car scénario existant étendu. Warning futur
`proc-macro-error2 v2.0.1` conservé. Fmt/diff PASS, aucune publication Git.

SHA-256 : `vegetation/quality/transition.rs`
`e0198ba265183dc61c44be85f420a80f237bde1d7e70d167795a1bc3c4609a68` ;
`vegetation/quality/tests.rs`
`6d285466a903e0d7f925754141abd49982cf0af53211a40547c54241d16af203`.
