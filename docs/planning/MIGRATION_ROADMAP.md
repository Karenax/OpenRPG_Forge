# Roadmap de migration — OpenRPG Forge Rust/WebGPU

Dernière mise à jour : 2026-09-03
Branche : `Rust`  
État global : **Gate C qualifiée sous l’exception bornée ADR-0003 ; Gate 0 passée sur preuves
automatisées locales sous ADR-0004, ADR-0005 et ADR-0006 ; Gate 1 ouverte ; Gate 2 interdite sans
nouvelle dérogation explicite**
Gate active : **Gate 1 — Visual Valley (OUVERTE)**

Cette roadmap transforme `PROJECT_CONTEXT.md` en lots exécutables. Elle n’est pas un calendrier et ne
contient aucune promesse de délai. Une gate ne passe qu’avec ses preuves.

## Règles de progression

1. Une seule gate principale est active.
2. Les travaux d’une gate ultérieure sont interdits s’ils détournent la gate active.
3. Chaque lot doit produire un résultat lançable ou vérifiable.
4. Les tâches visuelles nécessitent captures et mesures.
5. Une checkbox n’est cochée qu’après preuve dans le dépôt.
6. Une fonctionnalité historique n’est pas « migrée » parce qu’un type vide porte son nom.
7. Les échecs et limites restent visibles.
8. La roadmap est mise à jour dans le même lot que la preuve.
9. Le clone Cadle reste intact, épinglé et hors Git ; Forge doit fonctionner sans lui.
10. Aucun asset Cadle ne peut devenir une fixture ou un asset Forge.

---

# Gate D — Fondation documentaire

Objectif : rendre la branche compréhensible avant tout code.

- [x] `D-001` Créer la branche orpheline `Rust`.
- [x] `D-002` Confirmer l’absence d’ancêtre commun et l’arbre initial vide.
- [x] `D-003` Ajouter `README.md`.
- [x] `D-004` Ajouter le contexte canonique `PROJECT_CONTEXT.md`.
- [x] `D-005` Ajouter les règles agent `AGENTS.md`.
- [x] `D-006` Accepter l’ADR greenfield Rust/WebGPU.
- [x] `D-007` Ajouter cette roadmap.
- [x] `D-008` Ajouter la matrice de reprise historique.

Preuve attendue : documents présents sur `Rust`, cohérents et sans lien cassé vers un fichier requis.

---

# Gate C — Acquisition et qualification de Cadle

Objectif : transformer Cadle en base de travail exécutable, juridiquement bornée et mesurée avant tout
port Rust.

Référence obligatoire :

```text
repository  https://github.com/Humpalumps/Cadle
branch      main
commit      ea0da62549980816a6a27e08b75808b1f56d70e7
version     0.6.0
local path  upstream/cadle/  (ignored)
```

## C-A — Verrou et récupération

- [x] `C-001` Créer `upstream/cadle.lock.json` avec URL, branche, commit, version et licences.
- [x] `C-002` Créer `upstream/README.md` décrivant la frontière référence/produit.
- [x] `C-003` Ignorer `upstream/cadle/` sans ignorer le lockfile ni le README.
- [x] `C-004` Ajouter une commande reproductible Windows pour récupérer le commit exact.
- [x] `C-005` Ajouter une commande reproductible CI/Linux équivalente.
- [x] `C-006` Vérifier URL, HEAD détaché, SHA exact et arbre propre ; échouer sinon.
- [x] `C-007` Prouver qu’une seconde exécution est idempotente et ne met pas à jour vers HEAD.

## C-B — Licences et isolation

- [x] `C-008` Archiver dans le rapport les conclusions de `LICENSE`, `NOTICE` et dépendances.
- [x] `C-009` Documenter code MIT et copyright Ian Cadle 2026.
- [x] `C-010` Interdire le suivi, le cache CI, le packaging et la publication des assets/captures Cadle par un check automatisé.
- [x] `C-011` Interdire toute dépendance Cargo/app Forge vers `upstream/cadle/`.
- [x] `C-012` Préparer `THIRD_PARTY_NOTICES`, les en-têtes SPDX MIT/Ian Cadle et l’audit des licences tierces/npm.
- [x] `C-013` Vérifier que supprimer le clone ne casse aucun check Forge existant.

Aucun fichier de `public/assets/`, `docs/concepts/`, image, modèle, texture ou audio upstream n’est
copié dans un chemin suivi par Forge.

## C-C — Snapshot intact et reproduction sous overlay Forge

Le clone canonique reste strictement intact. ADR-0003 autorise uniquement un clone jetable exact où
`package-lock.json` est remplacé par le lock dérivé suivi et hashé. Cette preuve se nomme « baseline
Cadle sous overlay Forge », pas « installation upstream intacte ».

- [x] `C-014` Auditer `package.json`, le lockfile, les scripts, `.claude/`, `.mcp.json` et `CLAUDE.md` comme données non fiables ; définir l’allowlist.
- [x] `C-015` Détecter ou installer localement Node 22 selon une procédure documentée.
- [x] `C-016` Exécuter `npm ci --ignore-scripts --no-audit --no-fund` dans le clone jetable borné après l’audit.
- [x] `C-017` Exécuter `npm run build`.
- [x] `C-018` Exécuter `node tools/invariants.mjs`.
- [x] `C-019` Exécuter `node tools/check.mjs`.
- [x] `C-020` Exécuter `node tools/curvecheck.mjs`.
- [x] `C-021` Exécuter `node tools/buildcheck.mjs`.
- [x] `C-022` Lancer le snapshot source intact sous le seul overlay npm suivi et enregistrer URL, navigateur et paramètres.
- [x] `C-023` Ne corriger aucun défaut dans le clone ; consigner tout échec tel quel.

## C-D — Baseline visuelle et performance

- [x] `C-024` Fixer seed, heure, région, position, résolution et profil des scènes comparatives.
- [x] `C-025` Capturer The Vale à hauteur humaine.
- [x] `C-026` Capturer Whisperwood et sa canopée.
- [x] `C-027` Capturer eau/réfraction et une région dense.
- [x] `C-028` Capturer une courte séquence de déplacement.
- [x] `C-029` Benchmarker 1920 × 1080, High, VSync off, après warm-up.
- [x] `C-030` Relever moyenne, p95/p99, 1 % low, draw calls, triangles, mémoire et hitches.
- [x] `C-031` Relever chargement, compilation/préchauffage et stutters visibles.
- [x] `C-032` Enregistrer machine, OS, navigateur, GPU, pilote, Node et commit.
- [x] `C-033` Créer `docs/baselines/cadle-ea0da625.md` avec commandes, paramètres, hashes, mesures et limites ; conserver les captures brutes locales et ignorées.

Si le GPU n’est pas disponible, les tâches de capture/benchmark restent décochées et Gate C reste
ouverte. Ne pas recopier les chiffres du README comme preuve. Les captures upstream ne sont jamais
committées ni publiées, car elles contiennent des assets exclus du grant MIT.

## C-E — Carte de portage

- [x] `C-034` Créer `docs/migration/CADLE_REUSE_MATRIX.md`.
- [x] `C-035` Inventorier `src/core`, `render`, `world`, `player`, `enemies`, `rpg`, `audio`, `ui`, `vfx` et `tools`.
- [x] `C-036` Classer chaque lot : méthode, port, réécriture, différer ou écarter.
- [x] `C-037` Lier chemins source, destination Forge, attribution, tests et écarts.
- [x] `C-038` Sélectionner uniquement harness, terrain/biomes, végétation/LOD, ciel/lumière, eau et contrôleur pour R0/R1.
- [x] `C-039` Marquer combat, ennemis, RPG et audio comme différés jusqu’à la gate qui les nécessite.
- [x] `C-040` Produire le rapport Gate C et mettre l’état global à Gate 0 seulement après passage.

## Conditions de passage Gate C

Toutes sont nécessaires :

- [x] récupération exacte et idempotente depuis un clone Forge propre ;
- [x] commit Cadle et arbre propre vérifiés ;
- [x] licence code et exclusion assets comprises et contrôlées ;
- [x] build et checks non GPU exécutés dans l’environnement dérivé borné ;
- [x] snapshot source intact lancé sous le seul overlay npm suivi ;
- [x] captures comparatives inspectées ;
- [x] benchmark reproductible réalisé sur une machine documentée ;
- [x] matrice de reprise créée ;
- [x] aucun asset Cadle suivi ;
- [x] aucune dépendance produit vers Cadle/Three/Vite ;
- [x] rapport de gate versionné avec limites visibles.

Preuve finale : deux agents sur des clones Forge propres obtiennent le même snapshot et retrouvent les
mêmes scènes de référence avec les commandes documentées.

---

# Gate 0 — Fondation reproductible native + WASM + Studio

Statut au commit `a7c506f099b50100ad49f3ae6d01d541c9b63d49`, arbre
`7ca544c9eac98af558a4d91390c74ae08fa2ff11` : **PASS AUTOMATISÉ LOCAL SOUS ADR-0006**,
avec les décisions bornées ADR-0004 et ADR-0005.

Le replay final Windows dans le clone de preuve propre construit et lance le runtime, WASM/Trunk et le
Studio Tauri sans Cadle ni correction manuelle. Les ledgers Studio finaux valident leurs schémas,
WebGPU, la scène partagée, l'ACL autorisée/refusée, la stabilité du canvas et la capture de la fenêtre
exacte par HWND. Les tests locaux automatisés sont `PASS`.

Le run GitHub Actions du commit final, `33339077298`, a quatre jobs en conclusion d'interface
`failure`, mais zéro étape exécutée pour cause de facturation : CI distante Windows, Linux, WASM et
supply-chain = **NOT RUN — infrastructure billing**. ADR-0006 neutralise administrativement ce blocage
jusqu'à Gate 4 sans le transformer en CI verte. ADR-0005 enregistre la décision « Studio web confirmé »
et diffère les revues humaines : les preuves concernées restent honnêtement `uninspected`,
`pending_human_review` et `qualified = false`.

## But

Prouver que la même base Rust/Bevy peut alimenter un runtime natif et un viewport WebGPU intégré dans
une application de Studio sans architecture cachée ou non reproductible. Le harness et les formats de
preuve doivent permettre une comparaison future avec la baseline Cadle, mais aucun code Cadle ne fait
partie du runtime Forge.

## R0-A — Workspace et outillage

- [x] `R0-001` Créer le workspace Cargo minimal.
- [x] `R0-002` Ajouter `rust-toolchain.toml` avec une version stable épinglée.
- [x] `R0-003` Activer l’édition Rust 2024.
- [x] `R0-004` Définir les profils `dev`, `release` et `wasm-release`.
- [x] `R0-005` Ajouter `.gitignore` pour Cargo, Tauri, Trunk, WASM, caches et preuves locales.
- [x] `R0-006` Ajouter `cargo fmt` et clippy avec warnings projet en erreurs.
- [x] `R0-007` Ajouter `cargo-deny` et la politique de licences.
- [x] `R0-008` Ajouter la journalisation structurée `tracing`.
- [x] `R0-009` Ajouter une commande unique vérifiée pour bootstrapper le développement Windows.
- [x] `R0-010` Documenter uniquement les commandes réellement exécutées.

Critères : clone propre, toolchain automatique, aucune étape non documentée.

## R0-B — Crates minimales

Créer uniquement :

- [x] `R0-011` `crates/forge_core`.
- [x] `R0-012` `crates/forge_project`.
- [x] `R0-013` `crates/forge_world`.
- [x] `R0-014` `crates/forge_engine`.
- [x] `R0-015` `crates/forge_assets`.
- [x] `R0-016` `apps/forge_runtime`.
- [x] `R0-017` `apps/forge_studio`.
- [x] `R0-018` `tools/forge_cli`.

Chaque crate possède un README court, une responsabilité et au moins un test pertinent. Ne pas ajouter
de crate vide de « préparation future ».

## R0-C — Runtime natif

- [x] `R0-019` Ajouter Bevy 0.19.x avec features minimales nécessaires.
- [x] `R0-020` Ouvrir une fenêtre native.
- [x] `R0-021` Initialiser le backend `wgpu` haute performance.
- [x] `R0-022` Afficher une scène partagée simple : sol, objet PBR, lumière et caméra.
- [x] `R0-023` Ajouter redimensionnement, VSync et fermeture propre.
- [x] `R0-024` Afficher adapter GPU, backend et limites utiles.
- [x] `R0-025` Ajouter un overlay FPS/frame time/draw calls — PASS par exception bornée ADR-0004 :
  FPS/frame time réels, `gpu_draw_calls = null` et indisponibilité explicitement affichée.
- [x] `R0-026` Capturer une image automatisable.

## R0-D — WASM/WebGPU

- [x] `R0-027` Ajouter la cible `wasm32-unknown-unknown`.
- [x] `R0-028` Compiler Bevy avec la feature WebGPU appropriée.
- [x] `R0-029` Afficher la même scène partagée dans un canvas navigateur.
- [x] `R0-030` Détecter WebGPU et produire une erreur utilisateur claire s’il est indisponible.
- [x] `R0-031` Vérifier Chrome/Edge desktop.
- [x] `R0-032` Mesurer chargement, mémoire et frame time.
- [x] `R0-033` Documenter les limites WASM observées.

Le fallback WebGL2 n’est pas requis pour la Gate 0.

## R0-E — Studio Tauri + Leptos

- [x] `R0-034` Créer le shell Tauri 2.
- [x] `R0-035` Créer le frontend Leptos/Trunk.
- [x] `R0-036` Ajouter un design system CSS minimal : tokens, surfaces, typographie, focus.
- [x] `R0-037` Intégrer le canvas Bevy/WASM dans la page Studio.
- [x] `R0-038` Garantir que les rerenders UI ne reconstruisent pas le monde 3D.
- [x] `R0-039` Ajouter un panneau hiérarchie factice alimenté par données Rust partagées.
- [x] `R0-040` Ajouter un inspecteur factice lié à la sélection 3D.
- [x] `R0-041` Ajouter un canal de commande typé frontend ↔ backend.
- [x] `R0-042` Borner les permissions Tauri au strict nécessaire.
- [x] `R0-043` Capturer la fenêtre complète du Studio.
- [x] `R0-044` Mesurer latence d’interaction et stabilité du canvas.

## R0-F — Données partagées

- [x] `R0-045` Définir `ProjectId`, `AreaId`, `EntityId`, `AssetId`, `Revision`.
- [x] `R0-046` Définir transform moteur-neutre, mètres, Y-up, droitier.
- [x] `R0-047` Définir un `SceneProofDocument` JSON versionné.
- [x] `R0-048` Charger ce document en natif et en WASM.
- [x] `R0-049` Vérifier round-trip sans perte.
- [x] `R0-050` Vérifier que les types persistants ne dépendent pas de Bevy.

## R0-G — CI et preuves

- [ ] `R0-051` CI format — NOT RUN — infrastructure billing ; dette ADR-0006.
- [ ] `R0-052` CI clippy — NOT RUN — infrastructure billing ; dette ADR-0006.
- [ ] `R0-053` CI tests natifs — NOT RUN — infrastructure billing ; dette ADR-0006.
- [ ] `R0-054` CI build Windows — NOT RUN — infrastructure billing ; dette ADR-0006.
- [ ] `R0-055` CI build Linux — NOT RUN — infrastructure billing ; dette ADR-0006.
- [ ] `R0-056` CI build WASM — NOT RUN — infrastructure billing ; dette ADR-0006.
- [x] `R0-057` Rapport de licences.
- [x] `R0-058` Capture runtime native.
- [x] `R0-059` Capture Studio.
- [x] `R0-060` Rapport Gate 0.
- [x] `R0-061` Schéma commun des captures et métriques compatible avec la baseline Cadle.
- [x] `R0-062` Test prouvant que build/tests Forge passent sans `upstream/cadle/`.

## Conditions de passage Gate 0

Le passage administratif distingue les résultats satisfaits des deux dettes explicitement différées.
Les cases laissées décochées restent des preuves non acquises ; elles ne deviennent jamais des PASS :

- [x] un clone propre compile sans correction manuelle ;
- [x] runtime natif lancé ;
- [x] build WASM WebGPU lancée ;
- [x] Studio Tauri/Leptos lancé avec canvas ;
- [x] même document source visible sur les deux cibles ;
- [x] sélection simple partagée ;
- [x] tests locaux automatisés = PASS ;
- [ ] CI distante Windows/Linux/WASM/supply-chain = NOT RUN — infrastructure billing ; exception
  administrative ADR-0006, run `33339077298`, zéro étape exécutée ;
- [x] aucune règle métier en JavaScript ;
- [x] aucune dépendance à `main` à l’exécution ;
- [ ] captures et rapports inspectés humainement — revues différées sous ADR-0005 ; les ledgers restent
  `uninspected`, `pending_human_review` et `qualified = false` ;
- [x] décision « Studio web confirmé » enregistrée par ADR-0005.

Ce verdict ouvre Gate 1 sans qualifier la CI distante, sans inventer un compteur global de draw calls
et sans transformer une acquisition automatisée en revue humaine.

---

# Gate 1 — Visual Valley

Statut : **OUVERTE** après le passage automatisé local de Gate 0 sous ADR-0004, ADR-0005 et ADR-0006.

État courant de la chaîne d'intégration : `R1-023` dispose de l'implémentation native bornée
`e7e7d419`, de 14/14 tests eau PASS et d'une campagne exacte `125ba0b` avec 29 PNG uniques, vidéo et
journaux propres. `R1-037` dispose du contrat/modèle `dbe5eb86`, de la route runtime/orchestrateur
`140acbdd`, de 67/67 tests runtime + 1/1 intégration et des durcissements `9bb1d405` → `125ba0b` →
`347d782`. Son préflight réel vérifie le bundle complet puis s'arrête avant build sur l'absence d'une
couche Vulkan RenderDoc déjà correctement enregistrée. Ces routes restent décochées : qualification
humaine/GPU de l'eau et quatre captures RenderDoc indépendantes ne sont pas acquises.

Deux tentatives schema 12 ont été arrêtées sans verdict de performance : la première avant ready et la
seconde avant signal. À `9d19cde5`,
le runtime expose 69/69 pipelines et un contrat qualité 34/34 exact en `5/16/13`, mais le validateur
attend encore l'historique `5/14/15` et bloque avant ready. Le correctif `4b283182` dérive l'attente du
contrat canonique ; 141/141 tests moteur Release, 67/67 tests runtime et 1/1 intégration passent. Son
rebuild gouverné franchit réellement la readiness avec la distribution corrigée, puis PresentMon reste
vivant avec un stdout vide pendant 20 secondes. Aucun signal, host boundary, ledger final ou validation
schema 12 n'est publié. Le nettoyage processus/ETW passe ; un nouveau replay exact après réveil manuel
de l'affichage reste dû. L'écran déjà endormi est l'hypothèse locale prioritaire, pas un fait prouvé par
le reçu.

État des rejeux schema v8 : ADR-0008 et son validateur sont appliqués au commit exact
`49acfd5695a7246062a86fb84f5d933130579187`, arbre
`c6e7f6c5976c6a1b16f32de7f80d4d044bd64ceb`. Le premier rejeu gouverné échoue après une perte de
premier plan ; le second conserve la fenêtre au premier plan et passe tous les objectifs numériques
sauf le contrat strict « zéro `DisplayedTime` > 33,3 ms », avec une valeur unique à 35,3837 ms. Ces
résultats RX 7800 XT restent des preuves locales ; ils ne passent ni `R1-048` ni Gate 1.

La campagne native initiale reste épinglée au commit
`a7c9b1e1d5e32f826b606778434a5fae91cc947d`, arbre
`2066fce699e4dd178c3d679cf4b0168d2e727d3d`. Ce commit ajoute un harness natif exact-commit pour
les vues hauteur humaine/lointaine/eau, la séquence clair → pluie → clair et une vidéo de trois
secondes. Ses 37/37 tests ciblés, le formatage, Clippy strict du runtime et le validateur d'acquisition
passent. La validation workspace 298/298 et le check Studio WASM restent épinglés au commit overlay
`afb270c3` tant que le rejeu workspace du commit courant n'est pas consolidé. La validation
proportionnée du lot schema 8 passe 66/66 tests Release `forge_runtime --all-features` (62 unitaires,
4 intégration), le formatage Rust, l'auto-test A/B schema 2 et le diff-check ; elle n'est pas présentée
comme un replay workspace complet.

Le correctif prépasse `6a24cabe69e242ff551b265d44da67ca8d017c98`, arbre
`38a6990ce02baa651773d23d1038b3ef9ff9e76d`, désactive la prépasse caméra du matériau de végétation
afin d'éviter les silhouettes depth-only après dither/déformation. Le formatage, Clippy strict, 164
tests moteur/runtime/intégration, l'audit de provenance et le build Release passent. La réacquisition
Release produit 29 PNG et un MP4, zéro warning/erreur/panic, active une fois les neuf modèles CC0 sans
fallback et réduit de 98,88 à 100 % le masque bleu-noir sur les six vues diagnostiques.

La campagne visuelle `873618af59eb2e2b6426b91d4fff0ec615fc70d8`, arbre
`9ba6455fd632d6532bda27457312c7d229d46e2f`, remplace la trajectoire synthétique par le contrôleur
gameplay, la caméra `ForgePlayerCamera` et les collisions Rapier réelles. Elle conserve 29 PNG et un
MP4 H.264 de 24 checkpoints. Les checkpoints sont pausés pendant leur capture ; le MP4 à 8 FPS ne
qualifie donc pas le stutter temps réel.

La dernière campagne Release normale publiée `15635b3f85b623654e498b17ed299674928313e6`, arbre
`187ae357e24c47403b9c638c0a0737ed01215e33`, borne le rafraîchissement des preuves et exige des reçus
monotones d'audit de visibilité végétation au démarrage et à la frontière finale. Elle passe 140/140
tests moteur Release, 52/52 tests runtime Release, 1/1 test d'intégration Release, Clippy strict et
l'audit de provenance. Le ledger runtime enregistre sans pause 1 800 ticks en 29,997926 s, reçus 1/1
puis 2/2, 3 672 frames main-world et 3 674 contrôles d'invariants. Séparément, l'hôte retient 719
lignes PresentMon du PID 2864 avec `CPUStartQPC >= measurement_not_before_qpc`, sans borne haute issue
du runtime. Les 680 présentations résolues échouent aux seuils : moyenne 44,2516 ms, p95 62,4861 ms,
p99 76,4441 ms et 1 % low 12,5912 FPS. La frontière hôte n'est ni vérifiée ni ingérée par le runtime.
La campagne est **non qualifiante** — target Cargo partagé, aucun rebuild gouverné en target propre —
et le GPU cible comme la revue humaine restent à faux.

La source historique `2fe48485086e925422c667f49c38a80f96e1a81a`, arbre
`378f13f68e7689a6790aa544f1e5b4a1be30ad16`, ajoute l'opt-in strictement diagnostique
`--render-profile-gate1-diagnostic`. Il n'est disponible qu'avec la feature `render-profile`, exige la
route gameplay-stutter et publie un marqueur `qualifying = false`. Le formatage, les checks Cargo
normal/instrumenté, Clippy strict dans les deux configurations, 2/2 tests unitaires, 4/4 tests
d'intégration de garde et la provenance passent. Sur 30 secondes, le ledger enregistre 1 800 ticks,
2 608 frames main-world et 2 609 contrôles. PresentMon retient 2 184 lignes, dont 1 446 présentations
résolues : `DisplayedTime` moyen 20,8579 ms, p95 34,7319 ms, p99 83,4934 ms et 1 % low 4,1709 FPS.
La trace Chrome agrège 2 835 intervalles stables à 11,4451 ms de moyenne, p95 12,6216 ms, p99
13,2567 ms et maximum 27,2177 ms. Elle oriente le diagnostic vers l'acquisition de surface et le
pacing plutôt que les appels Vulkan bruts, mais ne supplante pas PresentMon : horloges et frontières
ne sont pas corrélées, la borne QPC haute manque et la capture reste non qualifiante.

La baseline Release non instrumentée historique `6f0e519afd01a701dd4d772d0d3837dd5cbf9814`, arbre
`cfbe45ecd3464c16f47ef1b2da2bf7fd5b78d9c7`, exécute trois runs `AutoNoVsync` et trois FIFO dans
l'ordre contrebalancé `AB/BA/AB`. Les six acquisitions sont valides côté infrastructure. Les trois
runs Auto échouent : 21,3180 à 21,5263 FPS moyens, p95 de 69,4547 à 69,4584 ms, 1 % low de
6,9036 à 7,0982 FPS, 16 à 27 présentations non affichées, 546 à 556 au-dessus de 33,3 ms et 65 à
116 consécutives au-dessus de 16,7 ms. Les trois FIFO échouent aussi ; l'analyse hashée
`5026e75d…81dd3e` ne propose aucun changement de pacing.

La chaîne source historique menant au schema v7 part de
`2bf5e44f5d06529b26208a1ac7c9a4c158d48aad`, qui ajoute le profil de présentation opt-in, puis
`e1e818fc1ed8655b851e34007039f4b288941377`, qui exige `bevy/trace` et `bevy/debug` afin de publier
les vrais noms de systèmes Bevy, et atteint
`9db35a8e125c6c60cde9de66a7f6aa7585215bfa`, arbre
`656feb155c4a30371210229bf6c5dea5bbf8fcd3`, qui protège les acquisitions PresentMon par un mutex
machine global, des préflights bornés et l'arrêt du seul identifiant de session possédé. Le profil
instrumenté reste diagnostique et non qualifiant ; il ne remplace jamais la Release normale.

La cause des captures sans ligne et des pertes ETW massives n'était pas le renderer : une session
orpheline `OpenRPGForge-Gate1-Diagnostic-85a698103aaf4186a480beabd1840099`, laissée par le run
`cpu-util-vulkan-6f0e519a-20260902T112247353Z`, était encore active. Son arrêt ciblé avec l'exécutable
PresentMon officiel a rendu une acquisition propre : 11 lignes cible, 7 lignes avant signal et zéro
perte ETW. Le guard de `9db35a8e` refuse désormais toute session Gate 1 reconnue avant de démarrer,
ne tue aucune session étrangère et vérifie l'absence de sa session exacte au postflight.

Le run Release normal gouverné schema v7 de `9db35a8e`, construit dans un target Cargo propre, utilise
le binaire exact de 82 333 696 octets, SHA-256
`C3790DA4A7A9F46B36F0837144A75F5AFDD43A63101093D25F402D09E0E80C4A`. Sur RX 7800 XT/Vulkan,
30 secondes de gameplay réel donnent 128,7260 FPS moyens, p95 13,9112 ms, p99 13,9657 ms et 1 % low
58,3489 FPS. La fenêtre reste stable, la résidence d'affichage couvre les deux bornes sans trou,
aucun `DisplayedTime` ne dépasse 33,3 ms, le maximum consécutif au-dessus de 16,7 ms vaut un et les
pertes ETW valent 0/0. Les 17 lignes `DisplayedTime=NA` sont conservées et hashées. Le schema v7
classait encore ce run en échec à cause de son prédicat supplémentaire « zéro frame non affichée ».

Deux répétitions Auto du même binaire confirment 128,9631 à 129,3634 FPS, p95 13,9079 à 13,9130 ms,
1 % low 53,0763 à 54,9590 FPS, zéro valeur au-dessus de 33,3 ms, 16 à 21 lignes `NA` et zéro perte
ETW. La première répétition atteint toutefois deux valeurs consécutives au-dessus de 16,7 ms ; la
seconde reste à une. Le contrôle FIFO reste comparable : 129,6567 FPS, p95 13,9083 ms, 1 % low
57,3937 FPS, 19 lignes `NA`, zéro valeur au-dessus de 33,3 ms, maximum consécutif un et ETW 0/0.
Ces répétitions restent l'historique schema v7 ; elles rendent le nouveau niveau de performance
crédible sans remplacer les deux rejeux gouvernés schema v8 ci-dessous.

Le premier rejeu Release gouverné schema v8,
`vulkan-governed-schema8-49acfd5-20260902T180000000Z`, utilise le binaire SHA-256
`21228A133C5B58025695A68668F10387A7E368C445B3F43155DF6E775CE177F7`. Il conserve 2 257
résidences affichées et 155 lignes `NA`, sans trou de résidence ni perte ETW, mais perd le premier
plan à +6,3577083 s pendant 182 des 275 échantillons de fenêtre. Il échoue avec 75,2425 FPS,
p95 20,9013 ms, p99 34,7203 ms, 1 % low 12,3978 FPS, 496 valeurs > 16,7 ms, 29 valeurs
> 33,3 ms et un maximum consécutif de 14. La validation SHA-256
`36E1EEBF9C4324AB285CBB7DE1C5EA51117237E7BCB88A73C446C4BE1D36D7BD`, le CSV
`6C11232BD6864E195CE50ADA347909608BA8D406CB5F438D87842126583570B3` et le ledger
`7EE6B8C0CE49A3D6A27C2595FD4E755F474CB36C5FAA121025E1A7123CCA037F` prouvent ce FAIL ; les
`NA` restent diagnostiques puisque la résidence couvre toute la fenêtre avec un gap nul.

Le second rejeu Release gouverné schema v8,
`vulkan-governed-schema8-replay2-49acfd5-20260902T192000000Z`, reconstruit le binaire exact SHA-256
`0C220F6980457A427DCF4F48C1204DA07120FDDC265C97E6ED61DA2326A75A85`. La fenêtre reste visible,
non minimisée et au premier plan pendant 274/274 échantillons ; la signature de présentation est
stable, ETW vaut 0/0 et la résidence couvre les deux bornes avec un gap nul. Les 3 900 résidences
affichées et 13 lignes `NA` donnent 129,9968 FPS, p95 13,8805 ms, p99 13,9329 ms et 1 % low
61,2708 FPS. Le maximum consécutif > 16,7 ms vaut un. Tous ces objectifs passent, mais une valeur
unique `DisplayedTime=35,3837 ms` à +0,2253583 s fait échouer le contrat strict « zéro > 33,3 ms »
et maintient le statut `runtime_qpc_display_overlap_thresholds_failed`. La validation SHA-256
`45B87D0D78570C0F6D0E541DF040AA008AE49E2CA47B5D02002F223EA50894D1`, le CSV
`5D810261815FE74D0E556F638BD704AC94119667FA4868AA57716320A124EC22` et le ledger
`88CD702163B3094D22A54FB352DB6C75AA742C4CFDE0919C86E331C76494605B` restent les preuves
gouvernées de ce FAIL unique.

La série de focus schema v8 A4/A5/A6 au même commit/arbre et sur le binaire partagé SHA-256
`21228A133C5B58025695A68668F10387A7E368C445B3F43155DF6E775CE177F7` est explicitement
**non qualifiante** : elle réutilise le target Cargo gouverné et ne remplace pas un rebuild propre. A4
échoue avec 128,0449 FPS, p95 13,8972 ms, 1 % low 55,3146 FPS, 27 `NA` et une valeur > 33,3 ms ;
son résumé/CSV/ledger vaut
`D339B7528424545EF27DD652FC518CA44E6DBD0A0EA762100F10553B7C8436CC` /
`AC400A4638EDF33223AFBF37CAAF9909CC4BD26EFE2331BB4B9F3C22DB3271C7` /
`FE905D77940487DBE6497807452C6D535E4A8B2CCE430C95309B459E8D409F48`. A5 passe ses seuils avec
129,2931 FPS, p95 13,9101 ms, 1 % low 71,6098 FPS, deux `NA`, zéro valeur > 16,7 ms et zéro valeur
> 33,3 ms ; ses trois hashes valent
`A867339F2545D7B0F2526657CA44C869C4B418455DC96670004857C7200A9248` /
`DCFEAD7518ABEEC3CF9649C3435DB2AD5C061235889132F02008D02B2523FB7F` /
`C6536C40F7A56383D64123895F964FD65C1C7653F879581A36CDCA3809BEC231`. A6 passe aussi avec
128,2562 FPS, p95 13,8968 ms, 1 % low 71,6615 FPS, zéro `NA`, zéro valeur > 16,7 ms et zéro
valeur > 33,3 ms ;
ses trois hashes valent `EBE9F6EF319F77F3B2C5FAE2FC98F1A6A093A69693FA270865A8075CDA582064` /
`F50270BAB9312AAE9EE5FCEDC45AAA524C1B9F1D0AECDC39BAE2F183ACFA838C` /
`5556B2042652572AD2266D2CFF4EBFE02AEA424B71447B3D842A8BC3DADAEBA7`.

L'A/B diagnostique PresentMon `2.5.1` A4 sur le même binaire reste explicitement non qualifiant car il
réutilise le target Cargo et un shim d'identité `2.4.1` dans le signal runtime. Il relève 3 908 lignes
CPU, 3 893 résidences affichées, 16 lignes `NA`, 129,7334 FPS, p95 13,9102 ms, p99 13,9572 ms, 1 %
low 61,4409 FPS, zéro valeur au-dessus de 33,3 ms, maximum consécutif un et ETW 0/0. Son résumé
SHA-256 `AAC662F7177FAEEF2B26E923061517BF6CF4B80A80543E8DE31862FE3DE17B28`, son CSV
`5053DAE9491C5665077D430741258D2CADF1EBCCE627506820E137EA8C5DB2D2` et son ledger
`718270959D9CCB1BD6711CC6BCAE6E1CC3DF70B4F987EBA5A9AE16A6E646E95F` restent externes et hashés ;
l'outil officiel vaut
`9BEC3083069F58F911E6A512F4806DB51A27BD096103087BC1D05EF54C80A191`.

ADR-0008 conserve les lignes `NA` comme diagnostic et autorise un verdict stutter local seulement si
la résidence dérivée couvre sans trou toute la fenêtre, ETW vaut 0/0, la fenêtre et la signature de
présentation restent stables, et les seuils FPS/p95/1 % low/>33,3 ms/consécutifs passent tous. Le code
schema v8, son auto-test et deux rejeux gouvernés hashés existent désormais. Le premier est invalidé par
la perte de premier plan ; le second reste un FAIL strict à cause de son unique 35,3837 ms. Ni les
PASS diagnostiques A5/A6 ni le reste des seuils PASS du rejeu gouverné ne promeuvent `R1-048` ou Gate 1.

Le commit `afb270c3` affiche dans l'overlay les temps CPU et GPU du pass opaque issus des diagnostics
Bevy ; deux captures natives High clear/rain exactes, hashées et lisibles ferment `R1-034` sans
prétendre fournir un temps GPU de frame entière ni fermer `R1-037`.

La matrice native RX 7800 XT/Vulkan reste épinglée à `5eadec7f` et qualifie 10/10 runs
Low/Medium/High/Ultra clear/rain, adaptatif clear et benchmark clear : 120 samples chacun, pipelines
stables et tier eau toujours égal au tier appliqué. L'adaptation effectue High vers Ultra.

Le smoke Browser High clear/rain reste épinglé au commit exact `d9e2670` et passe ses **24/24
contrôles pour chaque météo** : huit hashes présents, 9/9 modèles, 26/26 assets HTTP 200, aucune
requête `.meta`, aucun échec réseau et aucune entrée warning/erreur console.

Le harnais temporel Browser qualifie désormais son acquisition automatisée exacte au commit
`a4e9824abc3d0f5ec824edfa58155207467f3e99`, arbre
`25ff4fd075050ae60ad1904a9de00900a2c8a1fc` : 120/120 callbacks RAF, 3/3 ROIs végétation et témoin
terrain PASS, 4/4 bundles servis identiques à `dist`, zéro warning/erreur console et zéro échec
réseau. Les deux PNG et les dix artefacts hashés sont versionnés. `R1-014` reste ouvert : l'inspection
humaine est en attente et les images montrent encore le stipple/crénelage et les aplats déjà refusés.

La boucle WASM mesure 120 frames après 60 frames de warm-up. Clear relève 10,1175 ms de moyenne,
p95 12,1 ms, 1 % low 77,519 FPS ; rain 9,5633 ms, p95 10,9 ms, 1 % low 85,106 FPS ; zéro hitch dans
les deux runs retenus. Ces nombres ne sont pas des timestamps GPU : `gpu_presentation` reste
`not_observed` et le statut WebGPU du ledger reste `checking`. Un premier passage sous le seuil 1 %
low a été rejeté et conservé hors preuves versionnées ; aucun seuil n'a été abaissé.

La paire native/Studio `d9e2670` conserve **46/46** contrôles croisés PASS et sept hashes communs
concordants. La campagne native courante `5eadec7f` ajoute la preuve de profils : selon le run, la
moyenne varie de 5,6130 à 10,2077 ms, le p95 de 6,7886 à 13,9956 ms et le 1 % low de 56,293 à
134,937 FPS. Low/rain observe deux hitches ; les neuf autres runs zéro. Ces nombres RX 7800 XT ne
remplacent ni le benchmark GPU cible ni le parcours représentatif.

Le correctif forward + IBL empêche le SSR/différé non qualifié d'être réactivé, borne le grade météo
et supprime le warning natif observé à `a34596bd`. La campagne initiale `a7c9b1e1` refusait
contours/stipple noirs, silhouettes lointaines crénelées, grands aplats et eau masquée. La campagne
corrective `6a24cabe` élimine le principal défaut depth-only et rend `R1-046`/`R1-047` candidates à
revue humaine. La campagne `873618af` acquiert ensuite le vrai contrôleur gameplay, sa caméra et
Rapier. La baseline historique `6f0e519a` échouait sur trois runs Auto, puis la chaîne
`2bf5e44f` → `e1e818fc` → `9db35a8e` a isolé la session ETW orpheline, sécurisé le collecteur et
produit un run normal gouverné qui passe localement les objectifs numériques de la roadmap sous la
sémantique ADR-0008. Une répétition Auto sur deux échoue encore au critère « au plus une valeur
consécutive > 16,7 ms ». Deux runs schema v8 gouvernés existent maintenant : le premier perd le
premier plan et le second passe tous les objectifs sauf une valeur unique à 35,3837 ms. A5/A6 passent
en diagnostic mais restent non qualifiants. La campagne `125ba0b` valide automatiquement l'eau et la
séquence clair/pluie/clair sans revendiquer l'exécution GPU ou la revue humaine. `R1-023`, `R1-025`,
`R1-026` et `R1-046` à `R1-048` restent donc ouverts. `R1-037` reste ouverte après un préflight
fail-closed sur la couche Vulkan non enregistrée, et `R1-041` reste ouverte. `R1-043` reste satisfaite ; un rejeu
gouverné répétable qui passe tous les seuils, le matériel cible et la revue humaine restent dus.
L'audit provenance strict passe sans asset Cadle ; aucun asset Cadle n'entre dans Forge. La CI distante
reste `NOT RUN` sous ADR-0006 et ce statut n'est pas un PASS. Gate 1 reste **OUVERTE** et Gate 2 reste
interdite.

## But

Prouver que le nouveau socle produit un environnement plus convaincant que la base historique et au
moins aussi convaincant que la baseline Cadle sur les critères retenus, tout en tenant 60 FPS sur PC
milieu de gamme.

## Scène de référence

Créer `examples/visual_valley` avec seed et paramètres fixés :

```text
surface                 512 × 512 m
profil                   vallée forestière naturelle
terrain                  relief, chemin, rivière et berge
matériaux                herbe, terre, roche minimum
végétation               arbres, arbustes, fougères, herbe
météo                    clair + pluie de preuve
éclairage                soleil, ciel, atmosphère, brouillard
personnage               capsule ou modèle gouverné, taille humaine
caméra                    troisième personne, option première personne
physique                  sol, rochers, arbres proches, eau bornée
qualité                   Low / Medium / High / Ultra
```

## R1-A — Terrain

- [x] `R1-001` Document de terrain versionné.
- [x] `R1-002` Découpage en chunks.
- [x] `R1-003` Mesh et normales.
- [x] `R1-004` Trois matériaux PBR minimum.
- [x] `R1-005` Blend par masques.
- [x] `R1-006` collisions locales.
- [x] `R1-007` mise à jour partielle — 97/97 tests workspace et probe natif ECS/Rapier PASS local.
- [x] `R1-008` debug chunks/LOD.

## R1-B — Végétation GPU

- [x] `R1-009` Scatter déterministe — 58/58 tests ciblés et goldens document/cache PASS local.
- [x] `R1-010` Instances regroupées — handles partagés, éligibles au batching Bevy sans faux compteur de draws.
- [x] `R1-011` LOD arbres — near/medium distincts et plages complémentaires testées.
- [x] `R1-012` imposteurs ou HLOD lointain — 64 meshes HLOD de chunk observés.
- [x] `R1-013` herbe dense sans entité par brin — 162 176 brins dans 64 lots.
- [ ] `R1-014` vent shader — PASS natif et acquisition temporelle Browser automatisée `a4e9824a`
  PASS : 120 RAF, 3/3 ROIs végétation, témoin terrain, 4/4 bundles exacts, console/réseau propres.
  **ACQUISITION AUTOMATISÉE PASS / INSPECTION HUMAINE EN ATTENTE** ; `gpu_presentation` reste
  `not_observed` et la paire ne ferme pas la route sans revue humaine versionnée.
- [x] `R1-015` interaction locale avec personnage — champ borné aligné sur la pose post-physique.
- [x] `R1-016` culling par chunks — instantané cohérent après 241 observations caméra/range.
- [x] `R1-017` réglage des ombres par distance — CSM 128 m et règles caster/opt-out observés.
- [x] `R1-018` métriques instances visibles/cullées — schéma v4, draw calls explicitement indisponibles.

## R1-C — Atmosphère et eau

- [x] `R1-019` ciel et soleil cohérents — projection native et scène Browser clear/rain acquises.
- [x] `R1-020` brouillard atmosphérique — projection native et scène Browser clear/rain acquises.
- [x] `R1-021` brouillard volumétrique ou approximation mesurée — approximation de distance observée
  en natif et dans la scène Browser ; volumétrie réelle non revendiquée.
- [x] `R1-022` eau de rivière — géométrie/composants observés ; présentation GPU non revendiquée.
- [ ] `R1-023` réflexion/réfraction selon profil — implémentation native bornée `e7e7d419` et 14/14
  tests eau PASS ; la campagne native exacte `125ba0b` passe son contrôle d'artefacts. Les trois PNG
  eau clair/pluie/retour clair sont non vides et cohérents à l'inspection d'agent, mais la campagne ne
  sérialise ni route `R1-023`, ni observation du pass optique GPU ; `human_review_performed=false`.
  Aucun PASS GPU ou humain n'est revendiqué.
- [x] `R1-024` pluie — clear/rain High acquis en natif et Browser ; animation GPU non revendiquée.
- [ ] `R1-025` wetness réversible — endpoints CPU/ECS exacts 0 → 0,78 → 0 dans un même processus et
  trois PNG liés acquis au commit exact `125ba0b` ; exécution GPU instrumentée, qualification Browser
  et acceptation visuelle humaine non acquises.
- [ ] `R1-026` bloom et color grading bornés — endpoints de grade, présence du composant Bloom et
  campagne native exacte `125ba0b` acquis ; exécution GPU instrumentée, matrice Low/High et rendu
  humain accepté non acquis.

## R1-D — Personnage et caméra

- [x] `R1-027` contrôleur personnage Rapier — profil v1 strict, commandes séquencées et probe natif.
- [x] `R1-028` pente et marches — seuils, glisse, saut et atterrissage couverts automatiquement.
- [x] `R1-029` caméra troisième personne — entité stable, pose, FOV et suivi profilés.
- [x] `R1-030` occlusion caméra — sphere cast, exclusions, rapprochement et récupération monotone.
- [x] `R1-031` option première personne — même entité, visibilité, hauteur d'yeux et retour 3P vérifiés.
- [x] `R1-032` interaction végétation — 150/150 directions alignées, erreur de centre maximale 0 m.
- [x] `R1-033` animation minimale si asset disponible — **N/A conditionnel** au snapshot
  `28985328` : les 9 glTF gouvernés totalisent 0 animation, le manifeste ne contient que du décor et
  le joueur est une `Capsule3d` procédurale sans asset animé ;
  [preuve automatisée](../evidence/gate1/R1_033_PLAYER_ANIMATION_NA.md).

## R1-E — Performance

- [x] `R1-034` overlay CPU/GPU — commit exact `afb270c3`, temps CPU/GPU du pass opaque visibles et
  fallback indisponible testé ; ne vaut ni temps GPU frame entière ni instrumentation `R1-037`.
- [x] `R1-035` frame histogram — natif et boucle WASM Browser mesurés sur 120 frames.
- [x] `R1-036` moyenne, p95, 1 % low — valeurs natives et Browser consignées avec leur portée.
- [ ] `R1-037` draw calls, triangles, instances — contrat/modèle RenderDoc `dbe5eb86`, route
  runtime/orchestrateur `140acbdd` et durcissements jusqu'à `347d782` intégrés ; 22/22 tests Python,
  67/67 tests runtime + 1/1 intégration et inventaire préflight des 72 fichiers PASS. Le préflight
  réel refuse l'absence d'une couche Vulkan enregistrée sans modifier l'hôte ; quatre captures indépendantes
  clear A/B + rain A/B restent `NOT RUN`, donc aucun compteur qualifiant n'est acquis.
- [x] `R1-038` VRAM estimée — sous-allocations wgpu natives observées ; aucune VRAM Browser simulée.
- [x] `R1-039` préchauffage pipelines — 10/10 runs natifs exacts Low/Medium/High/Ultra,
  clear/rain, adaptatif et benchmark prêts avant et pendant les 120 samples.
- [x] `R1-040` profil dynamique — quatre tiers fixes, adaptation High vers Ultra, benchmark High,
  LOD scenery et tier eau concordants au commit `5eadec7f`.
- [ ] `R1-041` benchmark RTX 3060 ou RX 6600.
- [x] `R1-042` benchmark RX 7800 XT de développement — smoke natif High clear/rain exact
  `d9e2670`, Vulkan, 1920 × 1080, zéro hitch.
- [x] `R1-043` comparaison native vs Studio WebGPU — paire exact-commit `d9e2670`, 46/46 contrôles
  croisés et sept hashes de ledger communs MATCH ; présentation GPU Browser toujours non observée.

## R1-F — Revue visuelle

- [x] `R1-044` captures fixes de jour — acquisitions natives et Browser hashées.
- [x] `R1-045` captures sous pluie — acquisitions natives et Browser hashées.
- [ ] `R1-046` vue à hauteur humaine — PNG Release exact `125ba0b` réacquis, non vide et cohérent à
  l'inspection d'agent ; défaut depth-only corrigé, revue humaine comparative encore due.
- [ ] `R1-047` vue lointaine — PNG Release exact `125ba0b` réacquis, correction bleu-noir préservée ;
  revue humaine LOD/composition encore due.
- [ ] `R1-048` vidéo de déplacement courte — contrôleur gameplay/Rapier réel, caméra
  `ForgePlayerCamera`, 24 checkpoints et MP4 H.264 réacquis au commit exact `125ba0b`, vidéo SHA-256
  `29a5db4cd1f86911d3b43662c70147585eafcbee83ed96b4b8b10b60a2f1b226`. Le run normal gouverné
  schema v7 `9db35a8e` sur RX 7800 XT relève 128,7260 FPS, p95 13,9112 ms, 1 % low 58,3489 FPS,
  17 lignes `NA`, zéro perte ETW, zéro valeur > 33,3 ms et maximum consécutif > 16,7 ms égal à un ;
  la résidence couvre toute la fenêtre sans trou. Sous ADR-0008, ces objectifs sont candidats à un
  PASS automatisé local. Au commit/arbre schema v8 `49acfd56`/`c6e7f6c5`, le premier rejeu gouverné
  perd le premier plan et le second, stable, passe FPS/p95/1 % low/consécutifs mais échoue sur une
  valeur unique `DisplayedTime=35,3837 ms`. `R1-048` reste décochée : une répétition Auto atteint
  deux valeurs consécutives > 16,7 ms, les PASS A5/A6 sont non qualifiants, le GPU cible n'est pas
  mesuré et la revue humaine n'est pas réalisée. Le replay schema 11 `1912806` suivant est un
  **ABORT avant signal** sur un gap de sampling fenêtre `>250 ms`. Les deux tentatives schema 12 sont
  également non qualifiantes : `9d19cde5` bloque avant ready sur l'ancien décompte qualité, puis
  `4b283182` corrige et franchit la readiness mais s'arrête avant signal sur stdout PresentMon vide.
  Aucun host boundary, ledger final, validation schema 12 ou verdict performance n'existe ; nouveau
  replay exact après réveil manuel de l'affichage dû.
  `R1-037` reste un bloqueur distinct de Gate 1 ;
  [preuve](../evidence/gate1/R1_048_GAMEPLAY_STUTTER_PRESENTMON.md).
- [ ] `R1-049` revue des matériaux.
- [ ] `R1-050` revue de composition et lisibilité.
- [ ] `R1-051` comparaison avec la référence WebGPU ayant motivé le reboot.
- [ ] `R1-052` comparaison A/B avec Cadle épinglé sur même machine, résolution et profil équivalent ; séparer impact visuel et coûts par système, car Cadle couvre 2 km² contre 512 × 512 m pour Visual Valley.
- [x] `R1-053` vérification de provenance : aucun asset, nom, lore ou texte Cadle dans Visual Valley.
- [x] `R1-054` vérification d’indépendance : Visual Valley fonctionne sans clone Cadle produit.
- [x] `R1-055` rapport Gate 1 — verdict : poursuivre Gate 1 ; Gate 1 reste ouverte.

## Conditions de passage Gate 1

- [ ] qualité visuelle jugée clairement supérieure à la base Godot — défaut depth-only corrigé et
  réacquis à `873618af`, comparaison humaine toujours due ;
- [ ] impact visuel jugé au moins comparable à la baseline Cadle sans en copier les assets ;
- [ ] 60 FPS moyens en 1080p High sur GPU milieu de gamme — candidat PASS local RX 7800 XT au rejeu
  gouverné schema v8 `49acfd56` : 129,9968 FPS ; GPU cible dû ;
- [ ] p95 ≤ 16,7 ms — candidat PASS local RX 7800 XT au rejeu schema v8 `49acfd56` : 13,8805 ms ;
  GPU cible dû ;
- [ ] 1 % low ≥ 45 FPS — candidat PASS local RX 7800 XT au rejeu schema v8 `49acfd56` : 61,2708 FPS ;
  GPU cible dû ;
- [ ] absence de stutter récurrent — candidat PASS local du run gouverné schema v7 sous ADR-0008 :
  résidence sans trou, fenêtre stable, ETW 0/0, 17 lignes `NA` diagnostiques conservées, aucune valeur
  > 33,3 ms et maximum consécutif > 16,7 ms égal à un. Une répétition Auto atteint deux ; le premier
  rejeu schema v8 perd le premier plan et le second échoue sur une valeur unique à 35,3837 ms. Le
  replay schema 11 `1912806` suivant est abandonné avant signal sur un gap fenêtre `>250 ms` et ne
  produit aucun verdict. Les tentatives schema 12 `9d19cde5` et `4b283182` s'arrêtent respectivement
  avant ready sur le validateur qualité périmé, puis avant signal sur un flux PresentMon vide ; la
  readiness `5/16/13` corrigée est prouvée, pas la cadence. Un rejeu gouverné schema 12 strictement
  PASS, la répétabilité et la revue humaine restent dus ;
- [x] profils qualité fonctionnels — matrice native 10/10 et transition réelle High vers Ultra ;
- [x] parité de données entre natif et Studio — natif et Browser exacts `d9e2670`, 46/46 contrôles
  croisés et sept hashes communs MATCH, dont scenery `6b41ec73…` ;
- [x] code sans dépendance propriétaire obligatoire — audit provenance/indépendance PASS local ;
- [x] rapport et captures versionnés — rapport Gate 1, preuves Browser, lot correctif `6a24cabe`,
  lot contrôleur Release `873618af`, preuves historiques, baseline `6f0e519a` et reçu de timeout
  `06b5fd40` ajoutés avec leurs SHA-256 et audit de provenance ; les artefacts bruts des campagnes
  non qualifiantes restent externes et hashés.

---

# Gate 2 — Éditeur de monde

Statut : **INTERDITE tant que Gate 1 reste ouverte**. Aucun travail Gate 2 ne commence sans passage
formel de Gate 1 ou nouvelle dérogation explicite acceptée.

## But

Créer une carte complète sans modifier de JSON à la main, avec une expérience proche d’un constructeur
de monde accessible.

## R2-A — Projet et zones

- [ ] `R2-001` assistant nouveau projet.
- [ ] `R2-002` nouveau module.
- [ ] `R2-003` nouvelle zone extérieure.
- [ ] `R2-004` métadonnées et point de départ.
- [ ] `R2-005` onglets de zones.
- [ ] `R2-006` hiérarchie et sélection.

## R2-B — Brosses de terrain

- [ ] `R2-007` lever/abaisser.
- [ ] `R2-008` lisser.
- [ ] `R2-009` aplatir.
- [ ] `R2-010` bruit.
- [ ] `R2-011` pente.
- [ ] `R2-012` peinture matériaux.
- [ ] `R2-013` visualisation rayon/intensité.
- [ ] `R2-014` recalcul partiel.

## R2-C — Biomes et végétation

- [ ] `R2-015` pinceau densité.
- [ ] `R2-016` catalogues d’espèces.
- [ ] `R2-017` exclusions.
- [ ] `R2-018` seed stable.
- [ ] `R2-019` aperçu temps réel.
- [ ] `R2-020` promotion et retouche.

## R2-D — Splines

- [ ] `R2-021` création/édition de chemin.
- [ ] `R2-022` route avec profil.
- [ ] `R2-023` rivière et carving.
- [ ] `R2-024` intersections.
- [ ] `R2-025` clôture ou mur de preuve.

## R2-E — Placements et bâtiments

- [ ] `R2-026` navigateur d’assets.
- [ ] `R2-027` drag/drop.
- [ ] `R2-028` picking.
- [ ] `R2-029` gizmo translation/rotation/échelle.
- [ ] `R2-030` duplication et suppression.
- [ ] `R2-031` snap surface/grille.
- [ ] `R2-032` bâtiment paramétrique simple.
- [ ] `R2-033` intérieur et porte franchissable.

## R2-F — Commandes, sauvegarde et playtest

- [ ] `R2-034` bus de commandes.
- [ ] `R2-035` dry-run/diff.
- [ ] `R2-036` undo/redo.
- [ ] `R2-037` audit.
- [ ] `R2-038` sauvegarde atomique.
- [ ] `R2-039` récupération interruption.
- [ ] `R2-040` réouverture exacte.
- [ ] `R2-041` bouton Jouer.
- [ ] `R2-042` retour diagnostics.
- [ ] `R2-043` parcours de preuve sans JSON manuel.

## Conditions de passage Gate 2

- [ ] création d’une petite vallée avec forêt, route, rivière et maison ;
- [ ] undo/redo sur chaque opération ;
- [ ] sauvegarde/réouverture exacte ;
- [ ] passage jeu immédiat ;
- [ ] performance interactive ;
- [ ] aucune source de vérité cachée dans Bevy ;
- [ ] capture et procédure reproductible.

---

# Gate 3 — Première boucle RPG environnementale

## R3-A — Fondations

- [ ] joueur, stats minimales et inventaire ;
- [ ] PNJ et navigation ;
- [ ] interaction ciblée ;
- [ ] dialogue ;
- [ ] quête ;
- [ ] ennemi ;
- [ ] combat ;
- [ ] sauvegarde/reprise.

## R3-B — Monde comme arme

- [ ] objet poussable ;
- [ ] objet lançable ;
- [ ] dégâts d’impact ;
- [ ] chute ou piège ;
- [ ] feu ;
- [ ] eau/extinction ;
- [ ] objet destructible ;
- [ ] réaction IA au danger.

## R3-C — Parcours

Le joueur reçoit une quête, parle à un PNJ, traverse la zone, utilise un élément du décor pour vaincre
ou neutraliser un ennemi, récupère un objet, termine l’objectif, sauvegarde et reprend la session.

La gate passe uniquement si le parcours est authoré depuis le Studio.

---

# Gate 4 — Assets gouvernés et `.orpmod` v2

- [ ] catalogue d’assets ;
- [ ] import GLB ;
- [ ] import textures ;
- [ ] import audio ;
- [ ] provenance et licence ;
- [ ] hash et dérivés ;
- [ ] thumbnails ;
- [ ] validation publication ;
- [ ] package `.orpmod` v2 ;
- [ ] protections archive ;
- [ ] installation ;
- [ ] chargement runtime ;
- [ ] second module de régression ;
- [ ] import expérimental de données Solaren sélectionnées.

---

# Gate 5 — Creator RPG étendu

Reprendre progressivement les besoins historiques par parcours :

- dialogues avancés ;
- quêtes et journal ;
- factions ;
- blueprints ;
- objets et conteneurs ;
- créatures ;
- capacités et effets ;
- cinématiques ;
- calendrier ;
- rencontres ;
- profils de règles ;
- scripting sandboxé ;
- validation et diagnostics.

Chaque lot référence les IDs historiques utiles, sans chercher une couverture artificielle.

---

# Gate 6 — IA et MCP gouvernés

- [ ] inspection projet ;
- [ ] recherche d’assets ;
- [ ] proposition de carte ;
- [ ] génération de plan ;
- [ ] dry-run ;
- [ ] approbation ;
- [ ] application transactionnelle ;
- [ ] undo ;
- [ ] tests ;
- [ ] captures ;
- [ ] audit ;
- [ ] quotas ;
- [ ] absence de shell arbitraire.

---

# Gate 7 — Monde étendu et distribution

- [ ] streaming de cellules ;
- [ ] HLOD ;
- [ ] sauvegardes multi-zones ;
- [ ] packaging runtime ;
- [ ] profils graphiques ;
- [ ] lanceur ;
- [ ] builds Windows/Linux ;
- [ ] licences agrégées ;
- [ ] installateur ;
- [ ] qualification sur machine propre.

---

# Gate 8 — Multijoueur autoritaire

- [ ] simulation serveur ;
- [ ] transport ;
- [ ] admission ;
- [ ] présence ;
- [ ] mouvement ;
- [ ] combat ;
- [ ] inventaire ;
- [ ] quêtes ;
- [ ] changements de zone ;
- [ ] sauvegarde campagne ;
- [ ] reconnexion ;
- [ ] sécurité WAN ;
- [ ] tests à plusieurs processus et réseaux distincts.

---

# Scorecard de décision

À remplir à Gate 1 et Gate 2 :

| Critère | Baseline historique | Rust/WebGPU | Verdict |
| --- | ---: | ---: | --- |
| Impact visuel | à mesurer | à mesurer | ouvert |
| FPS moyen 1080p | à mesurer | à mesurer | ouvert |
| p95 | à mesurer | à mesurer | ouvert |
| 1 % low | à mesurer | à mesurer | ouvert |
| VRAM | à mesurer | à mesurer | ouvert |
| Temps d’itération IA | à mesurer | à mesurer | ouvert |
| Complexité dépôt | à mesurer | à mesurer | ouvert |
| Qualité UI | à mesurer | à mesurer | ouvert |
| Stabilité éditeur | à mesurer | à mesurer | ouvert |
| Import d’assets | à mesurer | à mesurer | ouvert |

Décisions possibles :

- **Poursuivre** : les objectifs sont atteints.
- **Réorienter** : remplacer une couche précise.
- **Limiter** : conserver la technologie pour le renderer ou l’éditeur seulement.
- **Arrêter** : la nouvelle pile n’apporte pas le gain attendu.

Aucune décision ne doit être formulée uniquement sur la quantité de code déjà écrite.
