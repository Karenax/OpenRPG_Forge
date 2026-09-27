# Gate 1 — Raccord eau / relief WorldSketch

Date : 2026-09-05. Travail local non publié, branche `Rust`, workspace existant modifié préservé.
Portée : complément R1-022/R1-064 ; aucune ouverture de Gate 2.

**Repère d'état ajouté le 2026-09-06 :** les échecs et candidats ci-dessous sont historiques.
Le relief riverain a ensuite été autorisé et intégré à la source embarquée en révision 2,
conservé en révision 3. Voir `R1_RIVER_VALLEY_REGRADING.md`. L'enfouissement décrit ici ne
représente plus la recette courante ; la finition et la revue artistique restent ouvertes.

## Cause et correction implémentée

Le mode `--world-sketch-visual-valley` chargeait le terrain sculpté WorldSketch mais conservait le
tracé d'eau de l'ancienne vallée. La nouvelle dérivation reprend les 73 contrôles de `river_main`,
leurs hauteurs, largeur et profondeur, avec `piecewise_linear_v1` : aucune seconde courbe Catmull-Rom
ne décale ce tracé du carving. Le contrat historique reste accepté sans conversion.

Les sections qui dépassent les limites de la zone sont réduites le long de leur direction latérale,
en conservant la proportion eau/berge. Le tracé central reste intact et un centre hors zone demeure
une erreur. Cette adaptation de présentation n'écrit ni la recette ni les sources sculptées.
La politique explicite `authored_surface_v1` conserve les hauteurs authorées sans relèvement
automatique. Elle est indépendante de l'algorithme horizontal ; les anciens documents conservent
la politique historique par défaut, omise en sérialisation pour préserver leurs hashes.

## Essais conservés

- Premier lancement `r1-water-carved-20260905-a` : échec de projection de `right_bank_outer` au
  contrôle 0, hors de l'étendue de 512 m. Ce lancement n'est pas une preuve positive.
- Premier test de bornage indépendant des coordonnées : échec `DegenerateTriangle` 1376 ;
  la réduction coordonnée des sections remplace ce bornage qui confondait eau et berge.
- Run `r1-water-carved-20260905-b` : code 0 mais **échec visuel**, eau artificiellement surélevée.
  La projection et la passe monotone historiques produisaient jusqu'à 30,45224 m de hauteur
  au-dessus du terrain. Ne pas réutiliser cette capture comme qualification positive.
- Run final `r1-water-carved-20260905-c` : code 0, capture inspectée. Le relèvement artificiel
  a disparu ; l'eau suit le tracé en amont, mais reste masquée en aval par le terrain insuffisamment
  sculpté. **Le raccord de données est prouvé, pas la continuité visuelle de la rivière.**

## Vérifications finales exécutées

HEAD de base `f786a38aca2023f08d708687f99a47d14f34e6d4`, modifications locales non committées,
103 commits derrière la référence locale `origin/Rust` (aucun fetch/push).
Variables : `CARGO_TARGET_DIR=C:\Users\karen\AppData\Local\Temp\openrpg-forge-r1-064-target`,
`CARGO_INCREMENTAL=0`, `RUSTFLAGS=-C debuginfo=0`.

- `cargo test --workspace --locked -q` : **293 PASS**, code 0 : Assets 14, CLI 7, Core 11,
  Engine 97, Project 44, Runtime 41, Studio 21, World 58.
- `cargo clippy --workspace --all-targets --all-features --locked -- -D warnings` : PASS, code 0.
- `cargo check -p forge_world --target wasm32-unknown-unknown --locked` : PASS, code 0.
  Ce contrôle n'est pas un lancement WebGPU ni un build WASM complet du renderer.
- Les tests couvrent conservation exacte des contrôles et des hauteurs des trois voies du mesh,
  interpolation linéaire, round-trip/hash, rejet d'une route comme rivière, limites de terrain,
  absence de triangles dégénérés sur cette vallée, refus du centre hors carte et chargement runtime.
  Les tests de l'ancien tracé restent verts. Les premiers échecs de Clippy portaient sur trois
  comparaisons flottantes de tests, remplacées par des comparaisons de bits sans assouplissement.

Commande du run final, depuis le workspace Rust :

```powershell
& 'C:\Users\karen\AppData\Local\Temp\openrpg-forge-r1-064-target\debug\forge_runtime.exe' `
  --world-sketch-visual-valley `
  --asset-kit-build E:\OpenRPG_Forge\.gate1-evidence-runtime\r1-063-authored-preservation-20260905-b\rebuild-1\asset-kit-build.v1.json `
  --asset-kit-preview examples\visual_valley\asset-kits\r1-063\runtime-preview-authored.v1.json `
  --capture E:\OpenRPG_Forge\.gate1-evidence-runtime\r1-water-carved-20260905-c\hero.png `
  --capture-frame 300 `
  --metrics E:\OpenRPG_Forge\.gate1-evidence-runtime\r1-water-carved-20260905-c\metrics.json `
  --metrics-warmup-frames 180 --exit-after-frames 420
```

Run du 2026-09-05 12:10:50 UTC, RX 7800 XT, Vulkan, pilote 26.8.1, 1920 × 1080 :
73 contrôles, 346 échantillons, 2 760 triangles, **zéro relèvement** ; 20/20 instances d'assets,
28/28/28 nœuds LOD, 36/36 colliders, zéro échec de chargement. Les avertissements Vulkan AMD et
shader `@invariant` restent présents. Fenêtre courte de 240 frames : 141,07 FPS moyens,
p95 7,6782 ms, 1 % low 55,63 FPS, un hitch de 36,3012 ms ; VSync demandé. Ce smoke avec capture
ne remplace pas le benchmark Gate 1 et ne qualifie aucune optimisation.

SHA-256 finaux :

- binaire : `8fec5dbf46fa43f5eedd53d93c5926ae6b5a639be0de3122d4f20aa22e9dff36` ;
- capture : `a788c7f356677eaefd5e2c0fb61a00a021752841cc227571e224cfa39338bdcd` ;
- métriques : `8fa207598726837b1f6e3b1d8aadb93b6835795ff53b00c72c06fd15af9d9b27` ;
- eau dérivée : `753ec0398fa88a460b8d59ac77129b6fb92f6e1e9385c8543c4d8aef8945f17e`.

## Limites

Ruisseau secondaire non raccordé, hydrologie et qualité des berges non qualifiées. Le carving
WorldSketch limite la coupe de rivière à 5 m, alors que la surface descend au fil du parcours :
le run final mesure une marge minimale eau/terrain de **−28,309752 m**. L'eau est donc enfouie
sur une partie du parcours, et non une rivière complète acceptable. La palette jaune/rose et la
végétation prototypale restent également à corriger.

Suite prévue : rendre cohérents le profil longitudinal et le budget de carving, puis régénérer et
revalider les preuves terrain/pont/assets affectées, en préservant leurs sources et retouches.
Ne pas augmenter silencieusement les budgets ni modifier des hashes de provenance à la main.
Les sources WorldSketch, le terrain sculpté et le kit Blender restent inchangés dans ce lot.
Aucun parcours d'édition Studio n'est prouvé.
Gate 1 et les revues humaines restent ouvertes ; CI distante `NOT RUN` sous ADR-0006.

## Candidat de creusement explicite — non promu (2026-09-05)

`WorldSketchSpline.maximum_cut_m` est maintenant optionnel, borné à `(0, 64]`, validé et
sérialisé seulement lorsqu'il est présent. L'absence conserve le budget historique. Le runtime
peut lire un fichier strict explicite via `--world-sketch-visual-valley --world-sketch-input fichier`.
Sans ce mode, l'option est refusée ; fichier manquant ou JSON invalide sont refusés avant rendu.
Le pont dérivé peut maintenant être chargé avec WorldSketch et conserve son contrôle de hash terrain.

La rivière est présente dans `locked_feature_ids`. La source courante et ses aperçus n'ont donc
pas été modifiés. Une proposition séparée ajoute uniquement `maximum_cut_m: 40.0` à `river_main`.
La comparaison JSON de la source courante avec la copie avant travail retourne `source_unchanged=true`.
La demande d'autorisation de modifier ce budget verrouillé est en attente ; ce candidat n'est
pas une promotion implicite ni une validation humaine.

Dossier de preuve : `E:\OpenRPG_Forge\.gate1-evidence-runtime\r1-river-profile-20260905`.
Il conserve `world-sketch-before.v1.json`, `world-sketch-candidate.v1.json`, les neuf sorties
`compiled/`, un aperçu candidat séparé, `hero.png`, `metrics.json` et l'événement final du probe
dans `bridge-crossing-completion.jsonl`. Le CLI `compile-world-sketch` a vérifié les images sources
avec `--source-root E:\OpenRPG_Forge\OpenRPG_Forge-Rust`, puis produit un nouveau dossier, code 0.

Le nouvel aperçu conserve exactement les 20 placements, le manifeste de kit et le descripteur
de retouche Blender ; seuls les deux liens WorldSketch/terrain sont remplacés par les hashes
réellement émis par cette compilation. Aucun GLB, Blend ou manifeste d'asset n'est modifié.
L'ancien aperçu face au candidat est refusé pour divergence WorldSketch (`runtime_plugin`,
événement `exit_code=2` ; wrapper de commande observé à 1). Ce refus intervient après initialisation
Vulkan et ne doit pas être décrit comme un refus avant toute initialisation GPU.

Contrôles finaux : **295 tests PASS**, code 0 (`cargo test --workspace --locked -q`) : Assets 14,
CLI 7, Core 11, Engine 97, Project 44, Runtime 42, Studio 21, World 59. Clippy workspace strict,
check WASM `forge_world`, formatage et diff : PASS. Le nouveau test parcourt les 346 centres
échantillonnés pour vérifier au moins 0,14 m au-dessus du terrain, préserve tracé/verrous/source,
vérifie round-trip et rejette cinq budgets invalides. Pas de nouveau lancement WebGPU.

Run natif du 12:20:46 UTC : même kit corrigé et caméra héroïque, avec le pont recompilé ; code 0.
Options : `--world-sketch-visual-valley --world-sketch-input <dossier>/world-sketch-candidate.v1.json`,
`--asset-kit-build` identique au run précédent, `--asset-kit-preview <dossier>/runtime-preview-candidate.v1.json`,
`--bridge-placement <dossier>/compiled/bridge-placements.v1.json`, capture frame 300,
warm-up 180, sortie frame 420. Le premier essai avait été refusé par l'ancien garde d'arguments
qui exigeait `--terrain` pour un pont ; ce garde accepte désormais aussi WorldSketch, sans
supprimer les vérifications d'identité du pont.

- 73 contrôles et 346 sections, aucun relèvement artificiel ; marge eau/terrain entre
  **0,301739 et 2,826935 m** sur les trois voies du mesh, contre −28,309752 m sur la source initiale.
- 8 722 samples de terrain modifiés ; coupe maximale effective **35,96734 m**, budget 40 m.
- 20/20 scènes d'assets, 36/36 colliders UCX, aucun échec de scène.
- Capture inspectée : eau désormais visible jusqu'au pont, mais canyon profond et berges sombres,
  végétation encore prototypale, éléments végétaux dans le cours d'eau et palette jaune/rose.
  Ces défauts interdisent une qualification artistique.
- 239 frames observées : moyenne 141,06 FPS, p95 7,9253 ms, 1 % low 49,94 FPS, un hitch de
  39,3665 ms. VSync demandé, smoke court avec capture, pas un benchmark de qualification.

Probe natif indépendant avec `--terrain <dossier>/compiled/terrain-carved.v2.json`,
`--bridge-placement <dossier>/compiled/bridge-placements.v1.json --bridge-crossing-probe` :
code 0, événement `bridge_crossing_probe_completed` du 12:21:32 UTC. Le vrai contrôleur Rapier
franchit 50,50772 m, **613/613 ticks de traversée au sol**, 477 ticks sur le collider du tablier,
zéro interruption de contact, bindings terrain/joueur/pont conservés. Le pont atteint 33,99 m
au-dessus du terrain au plus haut : cette hauteur découle du canyon et reste à revoir artistiquement.
Le fournisseur de navigation reste `not_selected`.

Hashes canoniques du candidat :

- WorldSketch : `df9eed22681b2f414884890156a5025701d96223a1ff0bc3e553a99e8726e32d` ;
- terrain : `653f14d117a29f628604be76209b13ac572df0c8480d425d690ba55308e88b02` ;
- pont : `fe93462ed664515e9f98f7fcfc2a2e2909c935e51528d7b60bebe40e47045227`.

SHA-256 des artefacts :

- binaire : `b44581d1d86650d08f2736ba2e0823266b7a7ea94c043168dcd4bea835bfb0ea` ;
- capture : `379a400db68759a498efd49517844b3b43164d5a826a86e06824234856e490a4` ;
- métriques : `8fc5badc8b2e812291286438b0e645069a462860a07b1dfdd7b1367d8ea19410` ;
- événements de fin du probe : `afe554f480af9bbdec1f3096faf3a651842b7921d9d488284ac9bddde3bf7e2f`.

Verdict : correction technique disponible et testée en candidat ; source verrouillée inchangée.
Gate 1/R1-022 restent ouvertes. Aucun commit, push ou asset Cadle.

## Précision de périmètre avant correction artistique

Inspection du 2026-09-05 après le raccordement Studio : `WorldSketchSpline` stocke des points
XZ, une largeur et un budget de coupe optionnel, mais aucune altitude authorée par point.
Le compilateur lisse les hauteurs du terrain puis impose de proche en proche une descente
minimale de 0,002 m/m. Une dépression amont contraint ainsi les hauteurs suivantes ; le terrain
aval ne peut ensuite rejoindre ce profil si sa coupe est plafonnée. Augmenter la coupe à 40 m
traite l'enfouissement, pas l'adéquation artistique entre rivière et relief.

La demande de correction altimétrique ne correspond donc pas à la simple édition d'un champ Y
existant : elle implique un choix de génération ou un nouveau contrôle explicite, avec ses
tests et sa provenance. Elle ne doit pas devenir une modification globale implicite de toutes
les splines. Conserver le tracé XZ, la largeur, les IDs et les autres features verrouillées ;
recalculer et requalifier les sorties dépendantes, notamment le pont et ses accès, sans déplacer
silencieusement les landmarks. À cette étape, l'autorisation de retoucher `river_main` était en attente.

Complément du 2026-09-05 : autorisation reçue. Le contrôle d'altitudes explicites et de remblai
borné est implémenté et testé ; un nouveau candidat a été compilé, capturé et traversé nativement.
Il reste non promu : les berges et le remblai central sont encore artificiels. Voir
`R1_WORLDSKETCH_RIVER_AUTHORED_ALTITUDES.md` pour les mesures et limites, sans invalider
les résultats historiques ci-dessus ni présenter l'autorisation comme une revue artistique.
