# Rapport Gate 1 — Visual Valley

Date de consolidation : **2026-09-01**

Verdict : **POURSUIVRE GATE 1 — GATE 1 OUVERTE**

Gate suivante : **Gate 2 interdite sans passage de Gate 1 ou nouvelle dérogation explicite acceptée**

## Résumé de décision

Visual Valley conserve une paire native/Browser exacte au commit `d9e2670`, des captures fixes, des
ledgers versionnés et une provenance CC0/Cadle auditée. Le correctif `6a24cabe` supprime le défaut
depth-only majeur de la végétation. La source courante `873618af` conserve cette correction et exerce
le vrai contrôleur gameplay, la caméra `ForgePlayerCamera` et Rapier ; 167 tests
moteur/runtime/intégration, 40 tests runtime Release, Clippy strict, l'audit de provenance et le build
Release exact passent. Sa réacquisition produit 29 PNG et un MP4 avec des logs propres et les neuf
modèles CC0 actifs sans fallback.
Les deux variantes Browser passent chacune
**24/24 contrôles automatisés**, sans échec réseau ni warning/erreur console. Le validateur croisé
natif/Studio passe **46/46** contrôles et les sept hashes communs concordent. La matrice native
`5eadec7f` qualifie 10/10 runs Low/Medium/High/Ultra, adaptatif et benchmark, avec pipelines stables et
tier eau synchronisé. Le commit `afb270c3` ajoute les temps CPU/GPU du pass opaque à l'overlay et les
rend lisibles dans deux captures natives exactes clear/rain. Le lot natif initial `a7c9b1e1` ajoute le
harness visuel exact-commit. Le commit `a4e9824a` qualifie le harnais temporel Browser : 120 RAF,
toutes les ROIs, les quatre bundles exacts et les journaux console/réseau passent ; cette preuve
Browser n'a pas encore été rejouée sur `873618af`.

Ces résultats ne ferment pas Gate 1. Le correctif forward + IBL supprime le warning wgpu enregistré
dans la campagne historique `a34596bd` et corrige les palettes rose/moutarde. Le lot `6a24cabe`
corrige ensuite les silhouettes bleu-noir laissées par la prépasse caméra. Le lot `873618af` acquiert
le déplacement du contrôleur réel sur 173 ticks avec collisions, mais le MP4 8 FPS est assemblé depuis
des checkpoints pausés et ne qualifie pas le stutter temps réel. La présentation GPU Browser reste
`not_observed`, la cible RTX 3060/RX 6600 n'a pas été mesurée et les captures n'ont pas reçu
d'acceptation humaine comparative. Les profils sont fonctionnels, mais cela ne constitue pas une
acceptation artistique.

Le verdict `R1-055` est **poursuivre Gate 1**, corriger et requalifier les preuves manquantes. Il ne
constitue ni un passage administratif de Gate 1, ni une autorisation implicite de commencer Gate 2.

## Identités exactes et frontières de preuve

| Portée | Commit | Tree | Usage dans ce rapport |
|---|---|---|---|
| source courante et contrôleur réel | `873618af59eb2e2b6426b91d4fff0ec615fc70d8` | `9ba6455fd632d6532bda27457312c7d229d46e2f` | 167 tests moteur/runtime/intégration, 40 runtime Release, Clippy strict, provenance, build Release, 29 PNG + MP4 ; contrôleur/Rapier/caméra acquis, stutter/humain/GPU en attente |
| correctif prépasse | `6a24cabe69e242ff551b265d44da67ca8d017c98` | `38a6990ce02baa651773d23d1038b3ef9ff9e76d` | défaut depth-only corrigé et réacquisition historique ; qualification humaine/GPU en attente |
| temporel Browser antérieur | `a4e9824abc3d0f5ec824edfa58155207467f3e99` | `25ff4fd075050ae60ad1904a9de00900a2c8a1fc` | acquisition temporelle automatisée PASS, 120 RAF, deux PNG, 3/3 ROIs végétation, témoin terrain, 4/4 bundles et journaux propres ; non rejouée sur la source courante |
| harness visuel natif | `a7c9b1e1d5e32f826b606778434a5fae91cc947d` | `2066fce699e4dd178c3d679cf4b0168d2e727d3d` | acquisition native exact-commit de 29 PNG, séquence clair/pluie/clair et MP4 3 s ; qualification refusée |
| overlay CPU/GPU | `afb270c328b7095a84d452b93b9fdd38e2737f33` | `5eeb9098b8300dead8679de7d478f5096a572fd1` | temps CPU/GPU du pass opaque visibles, fallback indisponible testé, deux ledgers/captures High exacts |
| matrice profils | `5eadec7f6f9f9b5b6734ce62ea3518f0297496f9` | `a2fb4df0b7e2eff4d686dda575d4a2bf18cb12a1` | 10 ledgers/captures natifs, quatre tiers, adaptation High vers Ultra, benchmark High et eau synchronisée |
| campagne Browser exacte | `d9e2670111624aa836ab138afc35cddf441865ef` | `d6e5a40f73b47934444bda4514d5cdcb1dab093d` | Browser clear/rain, manifeste Studio, assets HTTP et captures Browser |
| natif performance/captures exact-commit | `d9e2670111624aa836ab138afc35cddf441865ef` | `d6e5a40f73b47934444bda4514d5cdcb1dab093d` | RX 7800 XT/Vulkan, métriques v8, captures et logs natifs exacts |
| campagne graphique historique | `a34596bd733131699c70028dab86cf2c612b896a` | `8ccdf8cb2cb6a6aabd3a4569f22f1778e164ced6` | ancienne paire exacte conservée byte-for-byte ; warning et dérive visuelle reproduits |
| provenance/indépendance | `92286910a9bab622d5d54905551c0f03172e98e7` | `6d7cd4ab8612686e7f73f01abeb5b8ad0174ebbe` | audit CC0, comparaison Cadle et absence de dépendance produit |
| inventaire conditionnel `R1-033` | `28985328f0d2de1fa79c705d5c3aa1bc522dbe35` | `ec71501d231ea6469eb97dc0af3d0230f4daf6eb` | inventaire automatisé des glTF gouvernés, rôles du manifeste et représentation procédurale du joueur |
| référence Cadle extérieure | `ea0da62549980816a6a27e08b75808b1f56d70e7` | `f0d52e434454fe302052d22eafde203bc361cc1f` | comparaison de provenance uniquement, clone local détaché et propre |

Le natif et le Browser forment une paire exacte d9. Le hash scenery
`6b41ec73efde5b8a40ca0132e1d32462176d6b4810104f6bed69496569f81f5f` est identique aux deux
manifestes Browser. Les preuves `a34596bd` et `92286910` restent historiques et ne sont pas utilisées
pour conclure la parité courante `R1-043`.

## Validation source

Le dernier rejeu workspace complet reste épinglé à la source exacte
`afb270c328b7095a84d452b93b9fdd38e2737f33` :

| Contrôle | Résultat |
|---|---|
| `git diff --check` puis `cargo fmt --all -- --check` | **PASS** |
| `cargo test -p forge_engine --lib --locked -j1` | **122/122 PASS** |
| `cargo test --workspace --all-targets --locked -j1` | **298/298 PASS** — tous les crates et intégrations |
| `cargo fmt --all -- --check` | **PASS** |
| `cargo clippy --workspace --all-targets -- -D warnings` | **PASS**, zéro warning |
| check Studio `wasm32-unknown-unknown`, features `web` | **PASS**, 8,24 s |

Le check WASM conserve uniquement l'avertissement de future incompatibilité transitive connu pour
`proc-macro-error2 v2.0.1`. Cette validation couvre la source et les contrats. Le rebuild Release et
la preuve overlay afb et la matrice native 5e sont documentées ci-dessous ; la paire Browser/native d9
reste une campagne historique exacte distincte.

Au lot natif `a7c9b1e1`, la validation proportionnée du nouveau harness passe 37/37 tests
`forge_runtime`, le formatage, Clippy strict du runtime, la syntaxe PowerShell et l'acquisition native
exacte. Un rejeu workspace a été commencé, puis interrompu pendant la recompilation des dépendances
pour éviter un nouveau cycle d'environ une heure ; il n'a produit aucun verdict et n'est pas présenté
comme PASS. Le résultat 298/298 reste donc attribué uniquement à `afb270c3`.

Au commit courant `a4e9824a`, la validation ciblée supplémentaire passe 28/28 tests unitaires Studio,
`cargo fmt --all -- --check`, le check WASM, Clippy strict Studio/WASM, les trois validations
PowerShell du harnais et l'acquisition Browser exacte. L'avertissement de future incompatibilité
transitive `proc-macro-error2 v2.0.1` reste distinct d'un warning projet. Aucun nouveau rejeu workspace
complet n'est revendiqué à ce commit.

Au commit source courant `873618af`, les 6 tests R1-048 ciblés et les 40 tests runtime passent en
Release. La matrice proportionnée moteur/runtime/intégration passe **167/167** en profil de test ;
`cargo check --release --tests`, Clippy strict runtime, le formatage, la syntaxe PowerShell et les
tests positifs/négatifs du watchdog passent également. Le build Release exact et l'acquisition native
schema v2 sont rejoués depuis le worktree propre. Aucun rejeu workspace/all-targets complet ni nouveau
check Studio WASM n'est revendiqué à `873618af`.

## Overlay CPU/GPU exact `afb270c3`

Le binaire Release exact mesure 81 745 408 octets et porte le SHA-256
`9c7a85d9719101e53a25f79d8846462561c81dd9f7e5b1f72743d0964f2caffa`. Deux runs High,
clear puis rain, ont produit 120 samples chacun, des pipelines 69/69 puis 71/71 et des logs vides.
Les captures affichent respectivement CPU 0,010 / GPU 0,674 ms et CPU 0,012 / GPU 0,661 ms pour le
pass opaque. Les ledgers mesurent 0,0101 / 0,68072 ms et 0,0153 / 0,7246 ms à des frames différentes.

Les six artefacts, leurs hashes et le contrôle d'absence de référence Cadle sont versionnés sous
[`native/afb270c328b7095a84d452b93b9fdd38e2737f33/overlay`](../evidence/gate1/native/afb270c328b7095a84d452b93b9fdd38e2737f33/overlay).
Cette preuve ferme `R1-034`. Elle ne fournit ni temps GPU de frame entière, ni compteur global de draw
calls, triangles ou instances ; `R1-037` reste donc ouverte.

## Matrice native courante `5eadec7f`

Le binaire Release courant mesure 81 743 872 octets et porte le SHA-256
`33b3ea91dff8d5d0aa4af054aacf6f575bc1dc449572b914452518acdeb1c3eb`. Dix runs 1920 x 1080,
VSync off, caméra joueur, ont été retenus sur RX 7800 XT/Vulkan : quatre tiers clear/rain,
adaptatif clear et benchmark clear.

Les dix ledgers v8 contiennent chacun 120 samples, 9/9 modèles CC0, des pipelines prêts avant et
pendant la mesure et un tier eau identique au tier appliqué. Les comptes pipelines vont de 59/59 à
74/74. L'adaptation effectue High vers Ultra à la frame 604 ; benchmark reste High. Les moyennes vont
de 5,6130 à 10,2077 ms, les p95 de 6,7886 à 13,9956 ms et les 1 % low de 56,293 à 134,937 FPS.
Low/rain contient deux hitches ; les neuf autres runs zéro.

Les vingt artefacts, leur manifeste SHA-256, les deux séries rejetées et la revue visuelle bornée
sont consignés dans
[`R1_039_040_NATIVE_QUALITY_MATRIX.md`](../evidence/gate1/R1_039_040_NATIVE_QUALITY_MATRIX.md).
Cette preuve ferme `R1-039`, `R1-040` et la condition « profils qualité fonctionnels ». Elle ne ferme
ni le GPU cible, ni le parcours représentatif, ni l'acceptation artistique.

## Acquisition visuelle native initiale `a7c9b1e1`

Le harness courant lie le binaire Release au commit exact
`a7c9b1e1d5e32f826b606778434a5fae91cc947d` et refuse une acquisition depuis un worktree sale. Le
binaire de 81 944 064 octets porte le SHA-256
`5047b8e8016f1897882fc55ef9c78a03bdc25c4aabe4d94a7ebd968b93b07a4c`. Le run 1920 × 1080 High,
VSync désactivé, sur RX 7800 XT/Vulkan a produit 29 PNG, une vidéo H.264 de 24 frames à 8 FPS pendant
trois secondes et deux logs vides.

Le validateur passe l'identité source, la résolution physique, le present mode, l'identité
GPU/backend/pilote, l'absence d'asset Cadle, la propreté avant/après et l'absence de
warning/erreur/panic. Il observe côté CPU/ECS la séquence clair → pluie → clair, la wetness exacte
0 → 0,78 → 0, les endpoints de grade et la présence du composant Bloom. Il laisse explicitement
`weather_gpu_execution_observed=false`, `human_review_performed=false` et
`gate1_routes_qualified=false`.

L'inspection des PNG originaux refuse la qualification : végétation sombre et fortement crénelée,
silhouettes lointaines en blocs, grands aplats et eau étroite/masquée par les berges. La vidéo utilise
une caméra déterministe de preuve avec `gameplay_controller_exercised=false` ; elle ne qualifie ni un
parcours gameplay ni le stutter. L'acquisition avance donc la preuve sans fermer `R1-023`, `R1-025`,
`R1-026` ou `R1-046` à `R1-048`.

Le protocole, les hashes, la sélection versionnée de 13 artefacts et la décision visuelle sont
consignés dans
[`R1_025_026_046_048_NATIVE_VISUAL_PROOF.md`](../evidence/gate1/R1_025_026_046_048_NATIVE_VISUAL_PROOF.md).

## Réacquisition corrective Release exacte `6a24cabe`

Le matériau de végétation est désormais exclu de la prépasse caméra. Le commit propre
`6a24cabe69e242ff551b265d44da67ca8d017c98`, arbre
`38a6990ce02baa651773d23d1038b3ef9ff9e76d`, passe le formatage, Clippy strict, 127 tests moteur, 36
tests runtime et un test d'intégration. Le binaire Release de 81 970 688 octets porte le SHA-256
`af63ad658557b706159caffbf7e05fb517d72cdf495eed1c234c4243ce187ddf`.

Sur RX 7800 XT/Vulkan en 1920 x 1080 High sans VSync, le harnais acquiert 29 PNG et un MP4 H.264 de
trois secondes. Les logs contiennent zéro warning/erreur/panic ; le décor gouverné est activé exactement
une fois avec neuf modèles, 27 fichiers, 16 652 493 octets et aucun fallback. L'audit strict confirme
un manifeste CC0 et aucun asset Cadle copié.

Le masque bleu-noir baisse de 5 737 à 64 pixels sur la vue lointaine, 4 735 à 6 sur la vue humaine et
1 426 à 0 sur la vue eau ; les trois frames de mouvement retenues baissent de 99,50 à 99,98 %. Cette
mesure et l'inspection technique confirment la correction du symptôme, pas une acceptation artistique.
Le ledger garde l'exécution GPU météo, la revue humaine, le contrôleur gameplay et la qualification
des routes à faux. La preuve est documentée dans
[`R1_046_048_VEGETATION_PREPASS_CORRECTION.md`](../evidence/gate1/R1_046_048_VEGETATION_PREPASS_CORRECTION.md).

## Acquisition contrôleur gameplay réelle `873618af`

Le commit propre `873618af59eb2e2b6426b91d4fff0ec615fc70d8`, arbre
`9ba6455fd632d6532bda27457312c7d229d46e2f`, produit un binaire Release de **82 042 880 octets**,
SHA-256 `ba55fd0c33a3a072d685cd93018e4e638d0f3f725df91978fe9c5ca7a8dec5b4`. Sur RX 7800 XT/Vulkan,
1920 x 1080 High sans VSync, le harness schema v2 acquiert 29 PNG et un MP4 H.264 de 24 frames,
8 FPS et 3 secondes. Les 103 événements runtime sont INFO ; aucun warning, erreur ou panic n'est
présent. L'audit strict passe avec un manifeste, 27 fichiers, neuf modèles CC0 et aucun asset Cadle.

Le ledger lie 24 checkpoints au même contrôleur, au même joueur, à la même caméra `ForgePlayerCamera`
et aux mêmes identités Rapier. À 60 Hz, les 173 ticks de déplacement restent tous grounded et avec
collision. Le joueur avance de 13,544837 m vers -Z, dérive de 0,172029 m en X, franchit la couture
centrale puis s'arrête en 11 ticks après la commande stop immédiatement suivante. Les PNG 000, 012 et
023 montrent le personnage-capsule et un déplacement net de la caméra ; les cinq vues fixes conservent
la correction bleu-noir et le cycle clair → pluie → clair.

Le statut runtime reste `automated_capture_complete_pending_stutter_and_qualification` et le statut
hôte `automated_artifacts_valid_pending_stutter_human_and_gpu_qualification`. Les checkpoints sont
pausés pendant la capture et le MP4 est assemblé ensuite : `real_time_stutter_qualified=false`.
`weather_gpu_execution_observed`, `human_review_performed`, `visual_quality_accepted` et
`gate1_routes_qualified` restent également faux. Le lot borné de 18 artefacts est versionné sous
[`native/873618af59eb2e2b6426b91d4fff0ec615fc70d8/visual-proof`](../evidence/gate1/native/873618af59eb2e2b6426b91d4fff0ec615fc70d8/visual-proof).

## Preuve temporelle Browser exacte `a4e9824a`

Le harnais préenregistré a exécuté Edge headed `152.0.4191.53` sur la route High/clear exacte, depuis
un worktree propre avant et après. La page reste `1920 x 1080` sans scrollbar globale ; le canvas
`1408 x 966` et son backing store sont stables entre les captures. L'identité embarquée correspond au
commit `a4e9824abc3d0f5ec824edfa58155207467f3e99` et à l'arbre
`25ff4fd075050ae60ad1904a9de00900a2c8a1fc`.

Le verdict automatisé est **PASS** : 120/120 callbacks RAF, 3/3 ROIs végétation PASS, témoin terrain
PASS, 4/4 bundles servis byte-for-byte identiques à `dist`, zéro erreur/warning console et zéro échec
réseau. Les dix entrées du manifeste ont été revérifiées contre leurs tailles et SHA-256. Le ledger
principal porte le SHA-256
`df7d375aafbce9dffb817f5def12e1d4b4f9ce64c9706def024dd79828d039a9`.

Les copies versionnées sont sous
[`browser/a4e9824abc3d0f5ec824edfa58155207467f3e99/temporal`](../evidence/gate1/browser/a4e9824abc3d0f5ec824edfa58155207467f3e99/temporal).
Les deux PNG ont été ouverts à leur résolution originale pendant la consolidation technique. Ils
montrent bien une variation temporelle de la végétation, mais aussi le stipple noir, les silhouettes
lointaines crénelées et les aplats déjà refusés côté natif. Le ledger conserve
`human_temporal_inspection=required_not_performed_by_harness` : `R1-014` et Gate 1 restent ouvertes.

## Preuve Browser d9 clear/rain

Les artefacts locaux sont sous :

```text
E:\OpenRPG_Forge\.gate1-browser-smoke-artifacts\d9e2670111624aa836ab138afc35cddf441865ef
```

Le lot documentaire contient leurs copies byte-for-byte sous
`docs/evidence/gate1/browser/d9e2670111624aa836ab138afc35cddf441865ef/` et les deux PNG sous
`docs/evidence/gate1/captures/`. Leur cartographie complète est fournie par
[`R1_BROWSER_WEBGPU_SMOKE.md`](../evidence/gate1/R1_BROWSER_WEBGPU_SMOKE.md).

Le manifeste externe mesure 2 503 octets et porte le SHA-256
`c566fe7d3b8f36ea27e4f428301236e659488d3ed909c9d50ccf2ff26b2b50c7`. Les principaux fichiers :

| Artefact Browser | Taille | SHA-256 |
|---|---:|---|
| `validation.json` | 3 507 octets | `cd6b196e06ca1e02a970c94d47ed64a6aa613f017284cd92616d8040ee6a792f` |
| `visual-valley-clear-high.browser.png` | 1 293 570 octets | `819bd9994fafc8f4694d2e65300528bbf2d2b05f6e8946ae515305b58beb33d4` |
| `visual-valley-rain-high.browser.png` | 1 323 858 octets | `7703f0c4207052010343585d12b4e6bc94ca076114f6e39a4acf4cd10be76e5a` |
| ledger scène clear | 4 043 octets | `83093be5827ff053489a8636ba470b00d437ae928324af7ce06a65f37ba4383f` |
| ledger scène rain | 4 038 octets | `30716870ca853948657bf05f3167e43221cc377ef1d5c06e0322dda3063d32c3` |
| ledger frame clear | 1 771 octets | `1f606f2540a29b5d21c41188256d9464c9e8e6ee9ef565e363fd5cf383fd2e2f` |
| ledger frame rain | 1 796 octets | `d8c8829c6c1297f8d5ed12ad8584869878a9aa5780c987f1c9e07f4370d3d21f` |

### Résultats automatisés

- clear : **24/24 PASS**, zéro contrôle échoué ;
- rain : **24/24 PASS**, zéro contrôle échoué ;
- `selected_manifest == runtime_manifest` et `parity_status=source_manifest_matched` dans chaque run ;
- huit hashes de source présents et identiques entre sélection et observation Studio ;
- une caméra 3D active sur une attendue ;
- eau `observed_main_world_components`, météo `observed_cpu_projection` et tier High appliqué ;
- décor `activated`, 9/9 modèles chargés, 0 échec, 125 instances demandées, dont 99 remplacements et
  26 détails ;
- 26/26 requêtes d'assets répondent HTTP 200 pour chaque météo ;
- aucune requête `.meta`, aucun échec réseau, aucun warning/erreur console et aucun message DOM
  d'indisponibilité ou d'erreur runtime.

Les PNG ont une taille de 1920 × 1080 et capturent la page Browser complète. Le canvas Bevy observé
mesure 1 393 × 1 067 ; les chiffres Browser ne doivent donc pas être décrits comme un benchmark GPU
plein écran 1920 × 1080.

### Mesures de boucle WASM

| Mesure après 60 frames de warm-up | Clear | Rain |
|---|---:|---:|
| échantillons | 120 | 120 |
| durée | 1 214,1 ms | 1 147,6 ms |
| moyenne | 10,1175 ms | 9,5633 ms |
| p50 / p95 / p99 | 9,8 / 12,1 / 12,8 ms | 9,3 / 10,9 / 11,6 ms |
| FPS moyen | 98,839 | 104,566 |
| 1 % low | 77,519 FPS | 85,106 FPS |
| hitches `>16,7 ms` / `>33,3 ms` | 0 / 0 | 0 / 0 |
| histogramme `<=16,7` / `16,7..33,3` / `>33,3` | 120 / 0 / 0 | 120 / 0 / 0 |

Ces mesures proviennent de la boucle d'update Bevy WASM, pas d'une timestamp query GPU. Le ledger
conserve `webgpu.status=checking`, `webgpu_ready_ms=null` et `gpu_presentation=not_observed`. Le PASS
automatisé valide le chargement, les invariants CPU/ECS, le réseau, le DOM, les captures et les
métriques de boucle ; il ne doit pas être renommé « présentation GPU WebGPU qualifiée ».

## Preuve native d9 RX 7800 XT exact-commit

Le binaire Release reconstruit et exécuté sur `d9e2670` utilise une AMD Radeon RX 7800 XT, Vulkan
et le pilote AMD `26.8.1 (LLPC)`, en 1920 × 1080 High fixe, VSync off. Le build Release termine en
3 min 24 s ; le binaire de 81 735 680 octets porte le SHA-256
`e44bc941b70eabab8b7e6155a262ab93491d9c2d4763454cb09bfd480e24cc75`. Les deux ledgers
`forge.runtime-metrics` v8 contiennent 120 échantillons après warm-up :

| Mesure native | Clear | Rain |
|---|---:|---:|
| moyenne | 8,3478 ms / 119,792 FPS | 8,2920 ms / 120,598 FPS |
| p95 | 8,7132 ms | 9,0950 ms |
| 1 % low | 113,775 FPS | 96,354 FPS |
| hitches `>16,7 ms` / `>33,3 ms` | 0 / 0 | 0 / 0 |
| mémoire wgpu allouée / réservée | 530 273 124 / 876 609 552 octets | 534 407 108 / 876 609 552 octets |
| pipelines prêts | 69/69 | 71/71 |

La fenêtre de mesure native dure environ 0,94 seconde à caméra fixe. Les valeurs mémoire décrivent
les sous-allocations wgpu, pas la VRAM physique totale. `gpu_draw_calls` reste `null` et aucun total
exhaustif de draw calls/triangles/instances GPU n'est disponible.

Les deux logs natifs d9 sont vides : aucun warning `wgpu_core::validation` ni `ERROR` n'y est
enregistré. Cette absence qualifie ces deux exécutions seulement ; elle n'est pas extrapolée à tous
les backends ou GPU.

Les commandes, 46 contrôles, six hashes natifs et limites détaillées sont consignés dans
[`R1_043_NATIVE_STUDIO_EXACT_COMMIT.md`](../evidence/gate1/R1_043_NATIVE_STUDIO_EXACT_COMMIT.md),
complété par
[`R1_034_043_PERFORMANCE_PARITY.md`](../evidence/gate1/R1_034_043_PERFORMANCE_PARITY.md).

## Provenance, CC0 et indépendance

Le rejeu final de provenance sur `92286910` est **PASS automatisé local** :

- sous-ensemble Quaternius Stylized Nature MegaKit Standard 1.0, licence `CC0-1.0` ;
- 27 fichiers suivis, 9 modèles et 16 652 493 octets, tous déclarés et hashés ;
- 27/27 copies byte-for-byte depuis le coffre normalisé ;
- 507 fichiers Cadle hachés, 39 fichiers Forge canoniques comparés ;
- 0 copie exacte, 0 ligne normalisée commune d'au moins 40 caractères, 0 payload Cadle suivi et
  0 dépendance produit vers le clone ;
- Visual Valley fonctionne sans clone Cadle produit ; le clone extérieur n'est qu'une référence de
  comparaison en lecture seule.

Cela qualifie `R1-053`, `R1-054` et la condition « code sans dépendance propriétaire obligatoire ».
Cela ne constitue ni une expertise juridique exhaustive, ni une revue artistique, ni une preuve GPU.
Voir [`R1_053_054_PROVENANCE_INDEPENDENCE.md`](../evidence/gate1/R1_053_054_PROVENANCE_INDEPENDENCE.md)
et [`R1_CC0_SCENERY_OVERLAY.md`](../evidence/gate1/R1_CC0_SCENERY_OVERLAY.md).

## R1-033 — N/A conditionnel au snapshot courant

Le contrôle documentaire automatisé exact `28985328`, arbre `ec71501d`, établit que les neuf glTF
gouvernés correspondent aux neuf modèles du manifeste et totalisent **0 animation**. Les seules
catégories déclarées sont `tree`, `shrub`, `fern`, `rock` et `detail` : aucun personnage ou joueur
n'est disponible. Le joueur courant est construit sans asset avec `Capsule3d::new`, sous le nom
`Forge player capsule`, et son adapter ne référence aucune API d'animation Bevy.

La condition « si asset disponible » de `R1-033` est donc fausse pour cette tranche. La route est
fermée **N/A conditionnel**, et non PASS comme implémentation d'une animation. Elle se rouvre dès
l'introduction d'un asset de personnage animé gouverné ou le remplacement de la capsule procédurale.
La commande, les neuf résultats et les assertions de réouverture sont versionnés dans
[`R1_033_PLAYER_ANIMATION_NA.md`](../evidence/gate1/R1_033_PLAYER_ANIMATION_NA.md).

## État conservateur des routes

Nouvelles cases fermées par les preuves consolidées :

- `R1-019`, `R1-020`, `R1-021`, `R1-022` et `R1-024` ;
- `R1-035`, `R1-036`, `R1-038` et `R1-042` ;
- `R1-039` et `R1-040` par la matrice native exacte 10/10 et la transition High vers Ultra ;
- `R1-034` par l'overlay CPU/GPU exact `afb270c3` et ses deux captures lisibles ;
- `R1-043` pour la paire native/Studio exact-commit et les sept hashes communs concordants ;
- `R1-044` et `R1-045` pour l'acquisition de captures fixes ;
- `R1-053` et `R1-054` pour provenance et indépendance ;
- `R1-033` comme **N/A conditionnel**, faute d'asset joueur animé gouverné dans le snapshot courant ;
- `R1-055` par le présent rapport, avec verdict « poursuivre Gate 1 ».

Restent explicitement ouvertes :

- `R1-014`, `R1-023`, `R1-025` et `R1-026` ;
- `R1-037` et `R1-041` ;
- `R1-046` à `R1-052`.

Les fichiers `R1-046` à `R1-048` sont réacquis au commit exact `873618af`. Le principal défaut
depth-only reste corrigé et la vidéo exerce le contrôleur gameplay réel, mais « acquis » ne signifie
pas « qualifié » : la revue humaine comparative et le stutter temps réel restent dus.

`R1-043` est fermée : les deux cibles utilisent `d9e2670`, le scenery hash concorde et
46/46 assertions croisées passent. Le ledger natif n'expose pas le hash autonome du contrôleur
présent dans le manifeste Studio ; cette limite est documentée et ne doit pas être transformée en un
huitième match de ledger.

## Conditions de passage Gate 1

| Condition | État | Motif |
|---|---|---|
| qualité visuelle clairement supérieure à la base Godot | **OUVERTE** | défaut depth-only corrigé et conservé à `873618af`, mais aucune revue humaine comparative acceptée |
| impact au moins comparable à Cadle | **OUVERTE** | pas de comparaison A/B humaine sur protocole équivalent |
| 60 FPS moyens en 1080p High sur GPU milieu de gamme | **OUVERTE** | RX 7800 XT seulement ; Browser canvas et boucle WASM non équivalents |
| p95 `<=16,7 ms` | **OUVERTE** | paire exact-commit dans le budget, mais pas sur GPU cible |
| 1 % low `>=45 FPS` | **OUVERTE** | paire exact-commit dans le budget, mais pas sur GPU cible |
| absence de stutter récurrent | **OUVERTE** | contrôleur réel acquis à `873618af`, mais MP4 8 FPS assemblé depuis des checkpoints pausés |
| profils qualité fonctionnels | **SATISFAITE** | quatre tiers fixes, clear/rain, adaptation High vers Ultra et benchmark High sur 10/10 ledgers natifs |
| parité de données native/Studio | **SATISFAITE** | commit/arbre exacts communs, 46/46 contrôles et sept hashes communs MATCH |
| code sans dépendance propriétaire obligatoire | **SATISFAITE** | audits provenance/indépendance locaux PASS |
| rapport et captures versionnés | **SATISFAITE** | rapports, preuves Browser et lot natif schema v2 `873618af` ajoutés avec hashes/provenance |

## Limites visuelles et qualification humaine

Les captures d9 corrigent nettement la palette rose/moutarde. Le lot initial `a7c9b1e1` rendait les
défauts restants explicites ; le lot `6a24cabe` supprime ensuite les grandes silhouettes/stries
bleu-noir depth-only sur les vues lointaine, humaine, eau et mouvement. Le lot `873618af` conserve la
correction et ajoute le vrai contrôleur/Rapier. La pluie et le changement de
wetness sont visibles, mais la réflexion/réfraction ne sont pas inspectables honnêtement et le retour
CPU/ECS exact au profil clair n'est pas une preuve d'identité GPU.

Aucune affirmation de supériorité sur Godot ou de parité visuelle Cadle n'est acquise. Les vues
hauteur humaine/lointaine et le MP4 corrigés sont acquis mais non qualifiés ; la revue humaine FHD/QHD,
le stutter sur parcours temps réel et les comparaisons matériaux/composition/A-B restent dus.

## CI distante

La CI distante reste **NOT RUN — infrastructure billing**. ADR-0006 autorise une dérogation
administrative bornée jusqu'à Gate 4 lorsque les runners ne démarrent aucune étape. Cette dérogation
ne transforme pas la CI en PASS et n'efface aucune dette locale ou visuelle.

## Verdict et travail suivant

Verdict : **POURSUIVRE GATE 1**.

Ordre minimal avant toute nouvelle décision de passage :

1. faire la revue humaine des vues corrigées, qualifier l'eau par profil et acquérir un parcours
   temps réel représentatif avec qualification du stutter ;
2. acquérir l'instrumentation exhaustive `R1-037` ou documenter une décision Gate 1 dédiée ;
3. mesurer RTX 3060 ou RX 6600 en 1080p High ;
4. effectuer la revue humaine comparative Godot/Cadle et statuer sur les routes visuelles restantes.

Tant que ces éléments ne sont pas acquis, Gate 1 reste **OUVERTE** et Gate 2 est **INTERDITE** sans
nouvelle dérogation explicite acceptée.
