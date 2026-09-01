# Roadmap de migration — OpenRPG Forge Rust/WebGPU

Dernière mise à jour : 2026-09-01
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

Progression consolidée au commit source courant
`5eadec7f6f9f9b5b6734ce62ea3518f0297496f9`, arbre
`a2fb4df0b7e2eff4d686dda575d4a2bf18cb12a1`. La validation workspace passe **298/298 tests**, avec
formatage, Clippy strict et check Studio WASM. La matrice native RX 7800 XT/Vulkan qualifie 10/10
runs Low/Medium/High/Ultra clear/rain, adaptatif clear et benchmark clear : 120 samples chacun,
pipelines stables et tier eau toujours égal au tier appliqué. L'adaptation effectue High vers Ultra.

Le smoke Browser High clear/rain reste épinglé au commit exact `d9e2670` et passe ses **24/24
contrôles pour chaque météo** : huit hashes présents, 9/9 modèles, 26/26 assets HTTP 200, aucune
requête `.meta`, aucun échec réseau et aucune entrée warning/erreur console.

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
et supprime le warning natif observé à `a34596bd`. Les palettes sont corrigées ; des contours sombres
et du stipple/aliasing restent visibles, et l'eau n'est pas isolée dans ces cadrages. `R1-043` reste
satisfaite ; `R1-023`, matériel cible, stutter représentatif et revue humaine
restent dus. L'audit provenance/CC0 reste épinglé à `92286910…`. La CI distante reste `NOT RUN` sous
ADR-0006 ; ce statut n'est pas un PASS. Gate 1 reste **OUVERTE**.

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
- [ ] `R1-014` vent shader — PASS natif et paire temporelle inspectée ; présentation du vent sous
  WebGPU non qualifiée et `gpu_presentation` Browser non observée.
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
- [ ] `R1-023` réflexion/réfraction selon profil.
- [x] `R1-024` pluie — clear/rain High acquis en natif et Browser ; animation GPU non revendiquée.
- [ ] `R1-025` wetness réversible.
- [ ] `R1-026` bloom et color grading bornés.

## R1-D — Personnage et caméra

- [x] `R1-027` contrôleur personnage Rapier — profil v1 strict, commandes séquencées et probe natif.
- [x] `R1-028` pente et marches — seuils, glisse, saut et atterrissage couverts automatiquement.
- [x] `R1-029` caméra troisième personne — entité stable, pose, FOV et suivi profilés.
- [x] `R1-030` occlusion caméra — sphere cast, exclusions, rapprochement et récupération monotone.
- [x] `R1-031` option première personne — même entité, visibilité, hauteur d'yeux et retour 3P vérifiés.
- [x] `R1-032` interaction végétation — 150/150 directions alignées, erreur de centre maximale 0 m.
- [ ] `R1-033` animation minimale si asset disponible.

## R1-E — Performance

- [ ] `R1-034` overlay CPU/GPU.
- [x] `R1-035` frame histogram — natif et boucle WASM Browser mesurés sur 120 frames.
- [x] `R1-036` moyenne, p95, 1 % low — valeurs natives et Browser consignées avec leur portée.
- [ ] `R1-037` draw calls, triangles, instances.
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
- [ ] `R1-046` vue à hauteur humaine.
- [ ] `R1-047` vue lointaine.
- [ ] `R1-048` vidéo de déplacement courte.
- [ ] `R1-049` revue des matériaux.
- [ ] `R1-050` revue de composition et lisibilité.
- [ ] `R1-051` comparaison avec la référence WebGPU ayant motivé le reboot.
- [ ] `R1-052` comparaison A/B avec Cadle épinglé sur même machine, résolution et profil équivalent ; séparer impact visuel et coûts par système, car Cadle couvre 2 km² contre 512 × 512 m pour Visual Valley.
- [x] `R1-053` vérification de provenance : aucun asset, nom, lore ou texte Cadle dans Visual Valley.
- [x] `R1-054` vérification d’indépendance : Visual Valley fonctionne sans clone Cadle produit.
- [x] `R1-055` rapport Gate 1 — verdict : poursuivre Gate 1 ; Gate 1 reste ouverte.

## Conditions de passage Gate 1

- [ ] qualité visuelle jugée clairement supérieure à la base Godot ;
- [ ] impact visuel jugé au moins comparable à la baseline Cadle sans en copier les assets ;
- [ ] 60 FPS moyens en 1080p High sur GPU milieu de gamme ;
- [ ] p95 ≤ 16,7 ms ;
- [ ] 1 % low ≥ 45 FPS ;
- [ ] absence de stutter récurrent ;
- [x] profils qualité fonctionnels — matrice native 10/10 et transition réelle High vers Ultra ;
- [x] parité de données entre natif et Studio — natif et Browser exacts `d9e2670`, 46/46 contrôles
  croisés et sept hashes communs MATCH, dont scenery `6b41ec73…` ;
- [x] code sans dépendance propriétaire obligatoire — audit provenance/indépendance PASS local ;
- [x] rapport et captures versionnés — rapport Gate 1, manifeste/ledgers Browser et captures Forge
  clear/rain ajoutés au lot documentaire avec leurs SHA-256.

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
