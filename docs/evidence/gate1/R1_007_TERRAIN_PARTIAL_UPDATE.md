# Gate 1 — Preuve R1-007 mise à jour partielle du terrain

Date de validation locale : **2026-08-31**

Branche : **`Rust`**

Commit code qualifié : **`889f28df46b1a3a66da4e3a0cef075d790e26f6c`**

Tree Git : **`dfe5364246267b9b6423780d0dbd3addbc520361`**

Verdict du lot : **PASS AUTOMATISÉ LOCAL — R1-007 SOUS ADR-0006**

La capacité `R1-007` est démontrée par les tests ciblés, le replay workspace et le probe natif. Gate 1
reste **OUVERTE** : cette preuve ne qualifie ni la revue humaine comparative, ni le benchmark GPU
milieu de gamme, ni les autres lots de Visual Valley.

## Résultat livré

- Une requête versionnée part du `TerrainDocument` et du hash de base exacts, prépare un patch sans
  mutation, puis ne consomme ce patch qu'au moment du commit dans le cache dérivé.
- Le delta canonique de **+5 m** à l'échantillon global **(31, 31)** change exactement **un
  échantillon**, régénère **3 meshes** et **1 collider**, puis produit un cache bit-identique à une
  reconstruction complète du document cible.
- Les trois `Assets<Mesh>` sont modifiés en place : leurs handles et les entités de rendu ne sont pas
  remplacés. Le `Collider` est remplacé sur la même entité avant la synchronisation Rapier.
- Les **64 entités de rendu**, **64 identifiants d'assets mesh** et **64 entités collider** restent
  stables. Le runtime conserve donc 64 projections render et 64 projections physiques, sans
  recréation globale du terrain.
- Le système `FixedUpdate` applique le patch avant `PhysicsSet::SyncBackend`, puis publie le succès
  seulement après `PhysicsSet::Writeback`, après comparaison bit à bit des meshes, du collider ECS et
  du heightfield Rapier avec le cache cible canonique.
- Le handle backend et le `RapierContextEntityLink` attendus sont mémorisés et revérifiés. Un contexte
  changé est refusé comme une erreur de projection, puis le contexte source est restauré.
- Une corruption injectée entre application et vérification déclenche un rollback complet du
  document, du cache, des meshes, des bounds, du collider, du contexte Rapier et des statistiques.
  Le résultat d'échec n'est publié qu'après resynchronisation et preuve du backend restauré.
- Si le rollback ne peut lui-même être prouvé, l'adaptateur conserve un état empoisonné persistant qui
  bloque les transactions suivantes sans consommer leur file. L'échec reste donc visible au lieu de
  laisser avancer silencieusement une projection partielle.

Le lot ne contient aucun asset externe ou Cadle. Toutes les données terrain restent dérivées du
`TerrainDocument` Forge et du cache déterministe `GeneratedTerrain`.

## Empreintes déterministes

| Objet | SHA-256 |
|---|---|
| document source canonique | `9f16780a1de1e9dd4b65931624a8b45281713829434292bada320a578420b2ef` |
| document cible, révision suivante et delta `(31, 31, +5 m)` | `1fe904debcc863cbdb0bf677902fe99081cfc0641405d005f30d5aed3a9b0631` |
| cache cible après commit, égal au rebuild complet | `a64830b94da47776f53133089731e04fc140783acd6662b19dbcb8c1ceca7faa` |

## Validation automatisée locale

Environnement : Windows 11, `rustc 1.98.0 (88d9e12ae 2026-08-18)`, Cargo `1.98.0`, exécution
séquentielle et bornée en mémoire.

| Contrôle | Résultat observé |
|---|---:|
| `cargo fmt --all -- --check` | PASS |
| `git diff --check` | PASS |
| tests ciblés `forge_engine`, `forge_project`, `forge_runtime`, `forge_world` | **75/75 PASS** |
| détail `forge_engine` | **26/26 PASS** |
| détail `forge_project` | **10/10 PASS** |
| détail `forge_runtime` | **15/15 unitaires + 1/1 intégration PASS** |
| détail `forge_world` | **23/23 PASS** |
| filtre transactions terrain `terrain_update::tests` | **7/7 PASS** |
| Clippy strict sur les quatre packages modifiés, `-D warnings` | PASS, zéro avertissement |
| `cargo clippy --workspace --all-targets --locked -j1 -- -D warnings` | PASS en **3 min 40 s**, zéro avertissement |
| checks WASM `forge_engine` et Studio WebGPU | PASS |
| `cargo deny check` | PASS, code de sortie 0 |
| `cargo audit --no-fetch` | PASS, code de sortie 0 ; 20 avertissements de baseline, aucun blocage |
| `cargo test --workspace --all-targets --locked -j1` en profil mémoire réduit | **97/97 PASS** |

Les sept tests transactionnels couvrent le patch nominal, l'absence de binding, l'absence d'asset,
la conservation du handle et le raycast Rapier, la corruption post-commit suivie d'un rollback
intégral, le changement de contexte Rapier, puis l'échec de rollback qui empoisonne l'adaptateur et
préserve la requête suivante. Les 7 tests sont inclus dans les 26 tests `forge_engine` et ne sont pas
additionnés au total de 75.

Le replay workspace a compilé les variantes complètes Bevy/Tauri en **21 min 53 s**, puis les 97 tests
ont réellement tourné en environ **11,77 s** cumulées. Les cibles sans test ont rapporté zéro test et
ne sont pas incluses dans le décompte.

## Probe transactionnel natif

Le binaire exact `target/debug/forge_runtime.exe` a été exécuté avec le mode Visual Valley et le probe
de mise à jour partielle. Le processus a terminé avec un code de sortie **0**.

| Champ du probe | Valeur observée |
|---|---:|
| tick fixe de soumission | 3 |
| tick fixe d'application | 4 |
| tick fixe de vérification finale | 6 |
| chunks mesh modifiés / vérifiés | 3 / 3 |
| chunks collider modifiés / composants vérifiés | 1 / 1 |
| backends Rapier vérifiés | 1 |
| bindings complets | 64 |
| entités render préservées | 64 |
| assets mesh préservés | 64 |
| entités collider préservées | 64 |

Le probe a observé deux ticks fixes supplémentaires après l'application avant de conclure. Il a
recomparé le document et le cache runtime aux hashes attendus, ainsi que les 64 identités avant et
après transaction. Le succès ne repose donc ni sur la seule préparation core, ni sur le seul
remplacement du composant ECS : le backend Rapier avait été synchronisé et vérifié.

| Artefact local | SHA-256 | Taille |
|---|---|---:|
| `target/debug/forge_runtime.exe` | `ca0a199c1bc020bbdcfb86b65945142ce47bd517470fe3c5b3a9a841950447f1` | 166 352 896 octets |

## Smoke visuel natif et métriques

Un second lancement natif a produit la capture finale 1920 × 1080 et son ledger métrique. Les deux
processus ont terminé avec un code de sortie **0**. La capture a été inspectée : Visual Valley reste
visible et cohérente après l'intégration de la transaction partielle, sans asset Cadle.

| Artefact local ignoré | SHA-256 | Taille |
|---|---|---:|
| `artifacts/forge-runtime/gate1/r1-007-regression.png` | `d1456780bde6f08a744de0826551baf658f58f8ea988e424c86922e000c46e79` | 1 349 827 octets |
| `artifacts/forge-runtime/gate1/r1-007-regression.metrics.json` | `369a7519071e2613d4e5b6052aec5025879089c2e924719767df8a3c2d65df51` | 4 960 octets |

| Mesure | Valeur observée |
|---|---:|
| GPU / backend | AMD Radeon RX 7800 XT / Vulkan |
| fenêtre | 1920 × 1080 |
| frame moyenne | 15,298783 ms / 65,365 FPS |
| p95 | 12,6004 ms |
| 1 % low | 2,8083 FPS |
| hitch maximal | 693,3253 ms |

Ces timings ne sont **pas** un benchmark Gate 1. Le hitch d'initialisation de 693,3253 ms pollue le
1 % low et interdit d'utiliser cette exécution pour qualifier `R1-041`, `R1-042` ou les conditions de
performance finales. La RX 7800 XT est le GPU de développement observé, pas le GPU milieu de gamme
requis par la gate.

## État Git, indépendance et provenance

Le commit code `889f28df46b1a3a66da4e3a0cef075d790e26f6c` et son tree
`dfe5364246267b9b6423780d0dbd3addbc520361` sont publiés sur `origin/Rust` avec une divergence
locale/distante **0/0** après push.

La copie de travail principale contient encore les modifications utilisateur non liées déjà
inventoriées. Elles ont été préservées et ne font pas partie du commit qualifié. Le commit ne contient
aucun chemin Cadle et aucun asset Cadle n'a été copié dans Forge.

## Exception CI distante ADR-0006

Le push du commit code a déclenché le run GitHub Actions
[`33354077015`](https://github.com/Karenax/OpenRPG_Forge/actions/runs/33354077015). Les quatre jobs
affichent `failure` dans l'interface, mais chacun possède **zéro étape exécutée** :

| Job | ID GitHub | Conclusion d'interface | Étapes exécutées | Qualification Forge |
|---|---:|---|---:|---|
| `supply-chain` | `99372781152` | `failure` | 0 | `NOT RUN — infrastructure billing` |
| `wasm` | `99372781163` | `failure` | 0 | `NOT RUN — infrastructure billing` |
| `native (ubuntu-24.04)` | `99372781181` | `failure` | 0 | `NOT RUN — infrastructure billing` |
| `native (windows-2025)` | `99372781199` | `failure` | 0 | `NOT RUN — infrastructure billing` |

Le run origine de l'exception reste
[`33335683606`](https://github.com/Karenax/OpenRPG_Forge/actions/runs/33335683606). Le run courant n'a
exécuté ni checkout, ni installation de toolchain, ni compilation, ni test du commit R1-007. La cause
reste le refus de démarrage pour facturation ou plafond de dépense documenté par ADR-0006.

État probant actuel :

```text
tests locaux automatisés = PASS
CI distante Windows = NOT RUN — infrastructure billing
CI distante Linux = NOT RUN — infrastructure billing
CI distante WASM = NOT RUN — infrastructure billing
CI distante supply-chain = NOT RUN — infrastructure billing
```

Ce statut n'est ni une CI verte, ni un échec produit. Les risques de portabilité Linux et WASM
distants restent ouverts. L'exception cesse de neutraliser la condition dès qu'un runner exécute une
étape, que la facturation est rétablie ou qu'une distribution est préparée.

## Limites et suite obligatoire

- La CI distante n'a exécuté aucune étape : les risques Linux distant, WASM distant et runner Windows
  distant restent ouverts sous ADR-0006.
- Gate 1 reste ouverte : végétation, atmosphère, eau, contrôleur, caméra, profils qualité, parité
  Studio, performances et rapport final restent à livrer.
- La capture inspectée est une preuve technique de non-régression, pas la revue comparative humaine
  exigée par `R1-049` à `R1-052`.
- Le benchmark RTX 3060/RX 6600 et la qualification du 1 % low restent ouverts.
- La dérogation ADR-0006 ne couvre ni ces revues humaines, ni les critères fonctionnels restants, ni
  une future distribution.
