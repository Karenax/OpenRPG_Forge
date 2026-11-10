# Progression — passerelle Aurora/NWN (OpenRPG)

**Dernière mise à jour :** 8 novembre 2026.
**Jalon courant :** M5 — campagne avancée et qualification (**partiel, non clos**).
**Verdict global :** documentation de cadrage créée ; baseline ciblée exécutée avec des échecs
préexistants de fmt/Clippy ; audit des capacités multi-zone terminé ; crate `forge_aurora_bridge`
et contrat gelé ; `forge_aurora_runtime` projette le bundle en package Forge, convertit les WOK en
navigation, exécute une session à deux zones et produit une visite headless + un rendu Bevy
simplifié ; `forge_nwn_runtime` exécute le sous-ensemble NCS de la fixture et le scénario
`ab_proving` jusqu'à la reprise idempotente ; `forge_nwn_rules` exécute un combat d20 déterministe,
des sorts, des objets, des factions et une sauvegarde M4 ; M5 ajoute la migration M4 → M5, une trace
reproductible, `aurora-diff-oracle`, `aurora-oracle-log` et des tests de transitions répétées.
**Aucun module NWN hétérogène réel, aucune preuve visuelle Studio, aucun oracle NWN local, aucune
validation des GLB du bundle et aucune archive finale.** **M1, M2 et M3 sont terminés localement ;
M4 est terminé localement avec réserves ; M5 est partiel.**

## 1. Instantané Git

| Élément | Valeur |
| --- | --- |
| Checkout | `E:\OpenRPG_Forge\OpenRPG_Forge-Rust` |
| Branche | `Rust` |
| HEAD observé | `97176c19` |
| Audit du 26 octobre | citait `fc75b8e8` (révision antérieure) |
| Fichiers non suivis hors périmètre | `scripts/hivemind/audit_gladiator_material_migration_v397.py`, `scripts/hivemind/verify_palm_scalar_vertex_v562.py` |
| État des gates | Gate 7 ouverte ; Gate 8/9 non ouvertes ; aucune gate modifiée |
| Commit/tag/push | aucun |

Ne pas nettoyer, réinitialiser ou committer les fichiers non suivis ci-dessus.

## 2. Ce qui est fait

| ID | Livrable | Fichier(s) | Vérification | Limite |
| --- | --- | --- | --- | --- |
| AB-M0-01 | Instantané du checkout | `docs/aurora-bridge/README.md`, ce fichier | inspection Git locale | instantané daté |
| AB-M0-04 | ADR 0063 | `docs/ADR/ADR-0063-aurora-bridge-import.md` | relecture | M1 livré en lecture seule |
| AB-M0-10 | Campagne et corpus | `Preuves-et-analyses/actives/aurora-bridge/` | création | preuves M1/M2 consignées dans `STATUS.md` |
| AB-M0-14 | Verrou de contrat (gelé) | `docs/aurora-bridge/CONTRACT.lock.json`, `docs/aurora-bridge/contract/` | SHA-256 du miroir vérifié par test | gel lié au checkout `6104b6f` + `sourceDirty` |
| AB-M0-11 | Baseline technique ciblée | ce fichier | fmt/Clippy/tests ciblés | workspace complet non exécuté |
| AB-M0-16 | Audit multi-zone, sauvegarde et runtime | `docs/aurora-bridge/CAPABILITY_AUDIT.md` | 58 tests manifeste + 9 tests sauvegarde + 95 tests outil | aucune visite multi-zone réelle |
| AB-M1-17..20 | Crate `forge_aurora_bridge` | `crates/forge_aurora_bridge/` | 4 tests unitaires + 5 tests d'intégration | import structurel, aucun runtime |
| AB-M1-18 | Contrat gelé et miroir | `CONTRACT.lock.json`, `contract/module-migration-bundle-v1.schema.json` | SHA-256 `342b3316…`, test de verrou | pas de validateur JSON Schema générique |
| AB-M1-21 | Intégration producteur → consommateur | `crates/forge_aurora_bridge/tests/ab_proving_bundle.rs`, harness OpenNever | arbres byte-identiques, import réussi | harness inter-dépôts non commité dans OpenRPG |
| AB-M2-01 | Atelier d'export module | OpenNever desktop | lint TS + test job + check Tauri | pas d'E2E navigateur |
| AB-M2-02 | Assistant d'import | `crates/forge_aurora_runtime`, `tools/forge_cli`, commandes Studio | CLI `aurora-audit/import/visit`, check Studio | écran Leptos non livré |
| AB-M2-03..04 | Projection et assets | `crates/forge_aurora_runtime/src/projection.rs` | package v2 valide, 26 assets | pas de `.orpmod` packé |
| AB-M2-05 | Navigation WOK/PWK/DWK | `walkmesh.rs`, `navmesh.rs` | 81 + 52 polygones `forge_navigation` | pas de corps Rapier dédié |
| AB-M2-06..09 | Objets, session, transitions, gameplay | `projection.rs`, `session.rs` | visite 2 zones, 2 transitions | sauvegarde absente |
| AB-M2-10 | Visite réelle | `forge_aurora_visit`, `forge_aurora_visual` | 285,38 m, captures PNG + SVG | modèles NWN non chargés |
| AB-M2-11..12 | Régressions et matrice | ce fichier, campagne `m2-10/` | tests ciblés, preuves datées | Clippy workspace préexistant |
| AB-M3-01..04 | Événements, parseur, VM et registre | `crates/forge_nwn_runtime/src/{ncs,vm,natives}.rs` | 12 tests unitaires + 5 tests NCS fixture | sous-ensemble de la fixture uniquement |
| AB-M3-05..06 | Ordonnanceur, actions et effets typés | `scheduler.rs`, `vm.rs`, `effects.rs` | ordre stable, reprise sans doublon, actions `StartConversation`/`ExecuteScript` | autres actions NWScript et bus `forge_gameplay` non couverts |
| AB-M3-07..10 | Dialogue, journal, objet, porte, combat, reprise | `scenario.rs`, `tests/m3_scenario.rs` | 4 branches, journal 10/20/30, or 50, 2 transitions, reprise idempotente | pas de suspension mi-VM, pas de JRL/TLK complet |
| AB-M3-11..13 | Diagnostics, preuve, matrice | `M3_RUNTIME.md`, campagne `m3/`, `COMPATIBILITY_MATRIX.md` | rapport JSON, log de tests, hash | preuve headless, aucune capture M3 |
| AB-M4-01..03 | Profil, combat, personnages | `crates/forge_nwn_rules`, `docs/ADR/ADR-0064`, `nwn-rules-profile.v1.json` | 46 tests rules, ADR, profil validé, attaques spéciales | pas de tables 2DA complètes |
| AB-M4-04..06 | Objets, sorts, factions/IA | `inventory.rs`, `spells.rs`, `factions.rs`, `combat.rs`, `ai.rs` | magasin transactionnel, 4 sorts dont zone, perception, spécial | loot tables, sorts complets et pathfinding non couverts |
| AB-M4-07..10 | Quête, RNG, oracle, sauvegarde | `quest.rs`, `rng.rs`, `oracle.rs`, `save.rs`, `m4.rs` | reprise M4 idempotente, trace RNG, corruption refusée | oracle NWN réel non exécuté |
| AB-M4-11..12 | Budgets et matrice | `tests/m4_perf.rs`, `COMPATIBILITY_MATRIX.md` | 11,4 ms de moyenne locale sur 20 essais | pas de FPS/GLB/mémoire |
| AB-M5-01..03 | Session, migration, transitions | `m5.rs`, `session.rs`, `repeated_transitions.rs` | migration M4 → M5, 3 cycles, 6 transitions | registry globale/compagnons absents |
| AB-M5-05..06 | Trace et diff-oracle | `trace.rs`, `aurora-m5`, `aurora-diff-oracle` | 54 entrées, hash stable, mutation détectée | oracle NWN réel non exécuté |
| AB-M5-07..08 | Robustesse et performance | tests NCS/save/profile, `m5_perf.rs`, `m5-module-scale.json` | boucle VM bornée, corruption refusée, audit 42 zones, pic 9,2 Mo | archives non ingérées, FPS absent |
| AB-M5-04, 09..13 | Studio, glTF, SBOM, guide, rapport, archive | `M5_*`, `aurora_m5_ui.rs`, campagne `m5/` | panneau Studio, Khronos, SBOM Cargo, guide et rapport | capture Studio, GLB du bundle et archive non faits |

### Vérifications réellement exécutées

| Contrôle | Résultat | Date |
| --- | --- | --- |
| `git status` / HEAD | HEAD `97176c19` ; travaux M1 non commités + 2 scripts non suivis hors périmètre | 7 nov. 2026 |
| `cargo fmt --check` | **échec préexistant**, 11 fichiers avec diff | 7 nov. 2026 |
| Clippy ciblé `-p forge_project -p forge_gameplay --all-targets -- -D warnings` | **échec préexistant**, 2 diagnostics : `module_package.rs:763` (`case_sensitive_file_extension_comparisons`), `workspace/session.rs:132` (`too_many_lines` 102/100) | 7 nov. 2026 |
| Tests ciblés `-p forge_project -p forge_gameplay --lib --tests` | 517 réussis, 2 ignorés, 0 échec, 73 binaires, ~110 s, exit 0 | 7 nov. 2026 |
| AB-M0-16 : `cargo test -p forge_project --test module_package --test game_save_host` | 58 réussis (manifeste) + 9 réussis (sauvegarde), 0 échec | 7 nov. 2026 |
| AB-M0-16 : `cargo test --manifest-path tools/forge_delivery/Cargo.toml --lib` | 95 réussis, 2 ignorés, 0 échec, 97 tests ; inclut le bornage runtime | 7 nov. 2026 |
| Versions | `rustc 1.98.0`, `cargo 1.98.0` | 7 nov. 2026 |
| AB-M1 : `cargo test -p forge_aurora_bridge` | 4 unitaires + 5 intégration = 9 réussis, 0 échec | 7 nov. 2026 |
| AB-M1 : `cargo clippy -p forge_aurora_bridge --all-targets -- -D warnings` | OK | 7 nov. 2026 |
| AB-M1 : `cargo fmt -p forge_aurora_bridge` | OK (fmt ciblé, pas de fmt global) | 7 nov. 2026 |
| AB-M1 : import du bundle `ab_proving` | 2 zones, 9 objets, 10 blueprints, 14 scripts, 1 dialogue, 3 entrées de journal, 3 factions | 7 nov. 2026 |
| AB-M1 : harness producteur → consommateur | arbres byte-identiques ; manifeste producteur `bfc43867…` | 7 nov. 2026 |
| AB-M2 : `cargo test -p forge_aurora_runtime` | 8 unitaires + 2 intégration = 10 réussis | 7 nov. 2026 |
| AB-M2 : `cargo test -p forge_cli` | 3 + 4 = 7 réussis | 7 nov. 2026 |
| AB-M2 : `cargo test -p forge_aurora_bridge` | 9 réussis | 7 nov. 2026 |
| AB-M2 : `forge_cli aurora-audit` | 1800 + 1000 triangles, 81 + 52 polygones | 7 nov. 2026 |
| AB-M2 : `forge_cli aurora-visit` | 285,38 m, 1175 ticks, 2 transitions, retour `ab_camp` | 7 nov. 2026 |
| AB-M2 : `forge_aurora_visual --auto-visit` | 2 transitions ; captures camp + crypte | 7 nov. 2026 |
| AB-M2 : `cargo test -p forge_project --test module_package --test game_save_host` | 58 + 9 réussis | 7 nov. 2026 |
| AB-M2 : Clippy ciblé `--no-deps` | OK sur les crates bridge/runtime/CLI/visual | 7 nov. 2026 |
| AB-M2 : `cargo check -p forge_studio --bin forge-studio` | OK ; 2 tests `aurora_import` | 7 nov. 2026 |
| AB-M3 : `cargo test -p forge_nwn_runtime` | 12 unitaires + 2 scénario + 7 NCS fixture = 21 réussis, 0 échec | 8 nov. 2026 |
| AB-M3 : `cargo test -p forge_aurora_runtime -p forge_aurora_bridge -p forge_cli` | 27 réussis, 0 échec | 8 nov. 2026 |
| AB-M3 : `forge_cli aurora-scenario` | journal `[10,20,30]`, or 50, 2 transitions, 11 natives, 0 non supporté, reprise idempotente | 8 nov. 2026 |
| AB-M3 : Clippy ciblé `--no-deps` | OK sur `forge_nwn_runtime`, runtime, bridge et CLI | 8 nov. 2026 |
| AB-M3 : `cargo fmt -p … -- --check` | OK sur les quatre crates touchées | 8 nov. 2026 |
| AB-M4 : `cargo test -p forge_nwn_rules` | 40 réussis, 0 échec | 8 nov. 2026 |
| AB-M4 : `cargo test -p forge_nwn_runtime` | 30 unitaires + 2 M3 + 1 M4 perf + 1 M5 perf + 9 NCS = 43 réussis | 8 nov. 2026 |
| AB-M4 : `cargo test -p forge_cli` | 3 + 7 = 10 réussis | 8 nov. 2026 |
| AB-M4 : `forge_cli aurora-gameplay --seed 2026` | gardien vaincu en 2 rounds, 1 dégât subi, 1 PV soigné, quête terminée, reprise idempotente | 8 nov. 2026 |
| AB-M4 : `cargo test -p forge_nwn_runtime --test m4_perf` | 20 essais, moyenne 10,325 ms, p95 13,368 ms | 8 nov. 2026 |
| AB-M4 : Clippy ciblé `--no-deps` | OK sur `forge_nwn_rules`, `forge_nwn_runtime` et `forge_cli` | 8 nov. 2026 |
| AB-M4 : `cargo test -p forge_gameplay --lib` | 21 réussis, 0 échec ; ruleset natif non modifié | 8 nov. 2026 |
| AB-M5 : `cargo test -p forge_nwn_rules` | 40 réussis, 0 échec | 8 nov. 2026 |
| AB-M5 : `cargo test -p forge_nwn_runtime` | 30 unitaires + 2 M3 + 1 M4 perf + 1 M5 perf + 9 NCS = 43 réussis | 8 nov. 2026 |
| AB-M5 : `cargo test -p forge_aurora_runtime` | 8 unitaires + 2 intégration + 1 transitions = 11 réussis | 8 nov. 2026 |
| AB-M5 : `cargo test -p forge_cli` | 3 + 7 = 10 réussis | 8 nov. 2026 |
| AB-M5 : `forge_cli aurora-m5 --seed 2026` | 54 entrées de trace, migration accepted/restored, reprise idempotente | 8 nov. 2026 |
| AB-M5 : `forge_cli aurora-diff-oracle` | replay `matches=true` ; mutation `gold` détectée | 8 nov. 2026 |
| AB-M5 : `cargo test -p forge_nwn_runtime --test m5_perf` | 10 essais, moyenne 11,723 ms, p95 15,526 ms | 8 nov. 2026 |
| AB-M5 : Clippy ciblé `--no-deps` et fmt ciblé | OK sur rules/runtime/CLI/aurora-runtime | 8 nov. 2026 |
| Tests workspace complets | non exécutés (budget) | — |
| Validation JSON Schema générique | non exécutée ; tests structurels des deux côtés | — |

Les fichiers fmt en échec sont préexistants et hors périmètre :
`apps/forge_studio/src/asset_import_native.rs`, `apps/forge_studio/src/authoring_module_resources.rs`,
`apps/forge_studio/src/workspace_controls/history.rs`,
`apps/forge_studio/tests/module_vegetation_acquisition.rs`,
`apps/forge_studio/tests/module_workspace_bootstrap.rs`, `crates/forge_engine/src/lib.rs`,
`crates/forge_engine/src/render_timing.rs`,
`crates/forge_engine/src/source_vegetation/scene/shadow.rs`,
`crates/forge_tidewater/examples/tidewater_ocean_spectrum_probe/acquired.rs`,
`crates/forge_tidewater/examples/tidewater_plant_probe/camera_perf.rs`,
`crates/forge_tidewater/examples/tidewater_resident_ocean_probe.rs`.

Preuves : `F:\OpenRPG_Forge\Preuves-et-analyses\actives\aurora-bridge\baseline-m0-11-forge-project-gameplay-test.log`
(+ `.receipt.json`) et `...-clippy.log` (+ `.receipt.json`).

## 3. Blocages

| Blocage | Impact | Ce qui le débloque |
| --- | --- | --- |
| `cargo fmt --check` échoue sur 11 fichiers préexistants | le workspace n'est pas vert ; ne pas lancer `cargo fmt` global | correction dédiée ou dérogation explicite hors périmètre bridge |
| Clippy ciblé échoue sur 2 diagnostics préexistants | `forge_project` n'est pas `-D warnings` propre | correction dédiée ou dérogation explicite hors périmètre bridge |
| Tests workspace complets non exécutés | la baseline ne prouve pas tout le workspace | exécution ultérieure selon budget |
| Runtime mono-zone et sauvegarde mono-zone | la visite M2 est locale ; M3/M4/M5 restaurent une session à deux zones, pas une campagne NWN arbitraire | sauvegarde multi-zone avancée et module hétérogène en M5 |
| Bundle structurel seulement | aucune table 2DA/TLK résolue ; les NCS et le gardien de la fixture sont exécutés, pas un module arbitraire | M3/M4/M5 fournissent le sous-ensemble vérifié ; corpus réel requis |
| Rendu M2 simplifié | les modèles NWN réels ne sont pas chargés dans la scène visuelle | M3/M4/M5 |
| Interface Studio M2 partielle | commandes natives et CLI disponibles, écran Leptos non livré | compléter en M5 |
| M3 actions et suspension | `AssignCommand`, `DelayCommand`, `ExecuteScript`, `ActionDoCommand` supportés pour `StartConversation`/`ExecuteScript` ; `VmContinuation` non intégrée à la sauvegarde de scénario | intégration M5 et autres actions |
| M4 sans 2DA ni oracle NWN réel | personnages synthétiques, tables absentes, comparaison oracle non exécutée | collecte oracle locale autorisée |
| M5 partiel | pas de module hétérogène réel, preuve visuelle Studio, oracle NWN, GLB du bundle, textes de licence transitifs ni archive | exécuter les actions listées dans `M5_FINAL_REPORT.md` |

## 4. Prochaines actions

1. **M5** - module hétérogène réel, oracle NWN local, preuve visuelle Studio, GLB du bundle, textes de licence transitifs, archive.
2. Compléter le parcours Studio E2E et vérifier visuellement `AuroraM5Panel`.
3. Ajouter le pack `.orpmod` et l'installation gouvernée.
4. Rejouer les validations workspace selon budget.

Les preuves M2, M3, M4 et M5 restent **locales** ; aucune sauvegarde NWN générale, aucun oracle NWN
réel et aucune parité visuelle ne sont revendiqués.

## 5. État des tâches M0, M1, M2, M3, M4 et M5

| ID | État | Note |
| --- | --- | --- |
| AB-M0-01 | `done` | instantané |
| AB-M0-02 | `in-progress` (côté OpenNever) | MIT décidé ; provenance détaillée par asset à compléter |
| AB-M0-04 | `done` | ADR |
| AB-M0-10 | `done` | campagne |
| AB-M0-11 | `done` (partielle) | fmt échoue sur 11 fichiers, Clippy sur 2 diagnostics ; tests ciblés OK |
| AB-M0-14 | `done` | contrat gelé et miroir SHA-256 |
| AB-M0-16 | `done` (audit) | manifeste multi-zone testé ; runtime mono-zone ; sauvegarde mono-zone |
| AB-M1-17 | `done` | structure de la crate et manifeste consommateur |
| AB-M1-18 | `done` | miroir gelé, `CONTRACT.lock.json` `frozen` |
| AB-M1-19 | `done` | validation stricte des hashes, chemins, limites et identités |
| AB-M1-20 | `done` | `ImportedModule`/`ImportedArea` en lecture seule |
| AB-M1-21 | `done` | intégration producteur → consommateur byte-identique |
| AB-M1-22 | `done` | preuves M1 consignées |
| AB-M2-01 | `done` | atelier desktop producteur |
| AB-M2-02 | `done` (partiel) | commandes natives + CLI ; écran Leptos non livré |
| AB-M2-03..04 | `done` | package Forge v2, 26 assets |
| AB-M2-05 | `done` (partiel) | WOK + `forge_navigation` ; pas de Rapier dédié |
| AB-M2-06..09 | `done` | objets, session, transitions, gameplay |
| AB-M2-10 | `done` (partiel) | headless + rendu simplifié, captures |
| AB-M2-11..12 | `done` | régressions et matrice |
| AB-M3-01 | `done` | `M3_RUNTIME.md` : événements, contexte, ordre, non supportés |
| AB-M3-02 | `done` | parseur NCS borné + corpus de troncatures/mutations |
| AB-M3-03 | `done` | VM déterministe et suspension/reprise `VmContinuation` |
| AB-M3-04 | `done` | registre 11 natives supportées, 4 déclarées non supportées |
| AB-M3-05 | `done` (partiel) | ordonnanceur différé, actions `StartConversation`/`ExecuteScript` ; autres actions hors périmètre |
| AB-M3-06 | `done` (partiel) | effets typés, politique de permission, reçus ; pas de bus `forge_gameplay` |
| AB-M3-07 | `done` | 4 branches conditionnelles prouvées |
| AB-M3-08 | `done` (partiel) | journal 10/20/30 et statut de quête persisté, y compris échec ; pas de JRL complet |
| AB-M3-09 | `done` (partiel) | OnUsed/OnDisturbed/OnDeath et événements de porte OnOpen/OnClose si le script existe |
| AB-M3-10 | `done` (partiel) | reprise idempotente ; continuation VM sérialisable mais non suspendue dans le scénario |
| AB-M3-11 | `done` | diagnostics `unknown-native`, `unsupported-native`, arité |
| AB-M3-12 | `done` (partiel) | rapport + journal + hash ; preuve headless sans capture |
| AB-M3-13 | `done` | matrice requalifiée avec réserves |
| AB-M4-01 | `done` | ADR-0064, profil JSON, tests de non-régression |
| AB-M4-02 | `done` (partiel) | d20, AC, BAB, critiques, concealment, RD, immunités, attaques spéciales |
| AB-M4-03 | `done` (partiel) | templates data-driven ; gardien depuis le bundle ; audit 2DA `aurora-2da` |
| AB-M4-04 | `done` (partiel) | stacks, poids, slots, équipement, transferts atomiques, magasin achat/vente |
| AB-M4-05 | `done` (partiel) | 4 sorts, zone, contrôle avec jet, slots, repos, buffs ; VFX non supportés |
| AB-M4-06 | `done` (partiel) | factions, hostilité, déplacement/attaque, garde de perception ; pas de pathfinding |
| AB-M4-07 | `done` (partiel) | migration journal M3, récompenses uniques |
| AB-M4-08 | `done` | RNG SplitMix64, trace bornée, snapshot/restauration |
| AB-M4-09 | `done` (partiel) | harnais de comparaison et lecture de log externe ; oracle NWN réel non exécuté |
| AB-M4-10 | `done` (partiel) | session + rules + quête + RNG + portes + continuation optionnelle ; corruption refusée |
| AB-M4-11 | `done` (partiel) | 11,4 ms CPU, pic mémoire headless 9,2 Mo ; pas de FPS/GLB |
| AB-M4-12 | `done` | matrice requalifiée avec réserves |
| AB-M5-01 | `done` (partiel) | session/zones/transitions/reprise ; pas de registry globale/compagnons |
| AB-M5-02 | `done` | migration M4 → M5, schéma inconnu refusé |
| AB-M5-03 | `done` | 3 cycles, 6 transitions, restauration sans fuite |
| AB-M5-04 | `done` (partiel) | panneau Studio `AuroraM5Panel` + CLI ; pas de capture visuelle |
| AB-M5-05 | `done` | 54 entrées, hash stable, export expurgé |
| AB-M5-06 | `done` (partiel) | `aurora-diff-oracle` + `aurora-oracle-log` ; oracle NWN réel non exécuté |
| AB-M5-07 | done (partiel) | NCS/save/profil/boucle VM, symlinks refusés ; archives non ingérées |
| AB-M5-08 | `done` (partiel) | M5 CPU 11,723 ms, audit 42 zones, pic 9,2 Mo ; FPS absent |
| AB-M5-09 | `done` (partiel) | 10 GLB Khronos, 0 erreur ; pas de GLB de bundle |
| AB-M5-10 | `done` | SBOM Cargo complète, 909 composants |
| AB-M5-11 | `done` | guide utilisateur/mainteneur |
| AB-M5-12 | `done` | rapport final et matrice |
| AB-M5-13 | `todo` | archive bloquée par M5 partiel |

## 6. Journal des preuves

Ajouter une entrée par tranche :

```text
Date :
Tâche :
Fichiers modifiés :
Commande(s) :
Sortie / résultat :
Preuve (chemin, SHA-256) :
Limites :
Prochaine action :
```

Ne jamais convertir un contrôle non exécuté en `PASS`. Ne jamais supprimer une entrée historique.

## 7. Journal

### 2026-11-07 — Baseline technique ciblée

- **Tâche :** `AB-M0-11`.
- **Périmètre :** `forge_project` et `forge_gameplay`, sans Bevy ni rendu.
- **Commandes :** `cargo fmt --check` ; `cargo clippy -p forge_project -p forge_gameplay
  --all-targets -- -D warnings` ; `cargo test -p forge_project -p forge_gameplay --lib --tests`
  via `scripts/Invoke-ForgeCargo.ps1`.
- **Résultat :** fmt échoue sur 11 fichiers préexistants ; Clippy échoue sur 2 diagnostics
  préexistants ; tests ciblés OK (517 réussis, 2 ignorés, 0 échec, 73 binaires).
- **Preuves :** journaux et reçus dans
  `F:\OpenRPG_Forge\Preuves-et-analyses\actives\aurora-bridge\baseline-m0-11-*`.
- **Limites :** tests workspace complets non exécutés ; aucun test d'import de bundle.
- **Prochaine action :** `AB-M0-16`, puis suivre `AB-M0-17` côté OpenNever.

### 2026-11-07 — Audit des capacités multi-zone

- **Tâche :** `AB-M0-16`.
- **Livrable :** `docs/aurora-bridge/CAPABILITY_AUDIT.md`.
- **Constat :** le manifeste valide plusieurs zones (test à deux zones existant) ; le runtime ne
  charge qu'une zone par `area_id` ; `GameSave` lie un seul `module_id` + `area_id` ; les quotas
  natifs sont de 32 scripts, 256 pas, 128 variables et 32 factions.
- **Commandes :** `cargo test -p forge_project --test module_package --test game_save_host` ;
  `cargo test --manifest-path tools/forge_delivery/Cargo.toml --lib`.
- **Résultat :** 58 + 9 tests réussis ; 95 tests outil réussis, 2 ignorés, 0 échec.
- **Preuves :** `F:\OpenRPG_Forge\Preuves-et-analyses\actives\aurora-bridge\m0-16-*`
  (journaux + reçus).
- **Limites :** aucune visite réelle à deux zones, aucune sauvegarde inter-zones.
- **Prochaine action :** suivre `AB-M0-17` côté OpenNever, puis `AB-M1-00`.

### 2026-11-07 — Correctif d'ordre côté producteur

- **Tâche :** suivi de `AB-M0-17` (OpenNever).
- **Résultat :** le producteur applique désormais l'ordre NWN:EE (`HAK utilisateur > HAK
  installation > module > override`), résout `Mod_CustomTlk` dans les HAK et l'override, et exclut
  `development`/`override`/`Standalone` de la fermeture d'export. Les couches NWSync/User Patch/
  Texture Pack/ambient-music produisent un diagnostic explicite.
- **Conséquence pour OpenRPG :** aucun changement de code consommateur ; le prérequis d'import est
  levé. Le bundle reste à produire par `AB-M1-00`.
- **Prochaine action :** `AB-M1-00`, puis préparation de `forge_aurora_bridge`.

### 2026-11-07 — Fixture `ab_proving` disponible

- **Tâche :** suivi de `AB-M1-00` (OpenNever).
- **Livrable :** `fixtures/aurora-bridge/proving-grounds/` avec module à 2 zones, 14 scripts
  NSS/NCS, dialogue conditionnel, quête, coffre, porte et gardien.
- **Conséquence pour OpenRPG :** le corpus d'import M1 existe ; le consommateur peut préparer les
  tests de lecture dès que le contrat est gelé.
- **Limites :** aucune jouabilité NWN revendiquée ; le bundle module reste à produire.
- **Prochaine action :** `AB-M1-01`, puis `AB-M1-17`.

### 2026-11-07 — Licence producteur MIT

- **Décision :** OpenNever applique **MIT** au code, aux artefacts de contrat et aux fixtures
  originales (`AB-M0-12`).
- **Conséquence pour OpenRPG :** la copie inter-dépôts et la publication des artefacts originaux ne
  sont plus bloquées par la licence ; le code OpenRPG reste sous `MIT OR Apache-2.0`.
- **Limite :** les données NWN restent propriétaires ; le gel du contrat reste requis avant
  l'import de production.

### 2026-11-07 — M1 terminé localement : import en lecture seule

- **Tâches :** `AB-M1-17` à `AB-M1-21`.
- **Livrables :** crate `crates/forge_aurora_bridge` (`manifest.rs`, `validate.rs`, `import.rs`,
  `coordinate.rs`, `error.rs`), miroir gelé `docs/aurora-bridge/contract/module-migration-bundle-v1.schema.json`,
  `docs/aurora-bridge/CONTRACT.lock.json` (`frozen`, SHA-256 `342b3316…`), fixture
  `crates/forge_aurora_bridge/tests/fixtures/ab_proving.module-migration-v1/`.
- **Validation :** `validate_bundle` vérifie version, droits, scope d'intégrité, hashes, tailles,
  chemins relatifs sûrs, absence de symlinks, inventaire exact des fichiers module et area,
  manifests area v1, identités, graphe de références et diagnostics. `load_bundle` expose
  `ImportedModule` en lecture seule ; aucun type Bevy/ECS/handle n'est importé.
- **Commandes :** `cargo test -p forge_aurora_bridge` (9 réussis), `cargo clippy -p
  forge_aurora_bridge --all-targets -- -D warnings`, `cargo fmt -p forge_aurora_bridge`, harness
  `scripts/aurora_bridge_m1_integration.ps1` côté OpenNever.
- **Résultat :** bundle producteur byte-identique au fixture consommateur ; 2 zones, 9 objets,
  10 blueprints, 14 scripts NSS/NCS, 1 dialogue (9 nœuds), 3 entrées de journal, 3 factions ;
  `importable=true`, `complete=false`, `playable=false`.
- **Limites :** import structurel uniquement ; aucune navigation, aucun asset NWN, aucun runtime de
  visite ; validation JSON Schema générique non exécutée ; fmt global et Clippy `forge_project`
  restent en échec préexistant.
- **Prochaine action :** M2 — assistant d'import Studio (`AB-M2-02`), projection et première visite.

### 2026-11-07 — M2 terminé localement avec réserves

- **Tâches :** `AB-M2-01` à `AB-M2-12`.
- **Projection :** `crates/forge_aurora_runtime` projette le bundle `ab_proving` en package Forge v2
  (2 zones, 26 assets, 33 fichiers dont `module.json`, manifeste `409dfa52…`) et ingère
  GLB/PNG/WOK/DWK/PWK.
- **Navigation :** walkmesh ASCII 2800 triangles → 81 + 52 polygones `forge_navigation` ; portes
  ouvertes/fermées modélisées ; pas de corps Rapier dédié.
- **Session :** `ModuleSession` charge/décharge les zones, conserve le joueur, ouvre 2 portes et
  effectue 2 transitions ; visite headless de 285,38 m et 1175 ticks.
- **Preuve visuelle :** app Bevy `forge_aurora_visual` ; captures `camp.png` et
  `crypt-after-transition.png`, rapport dans `Preuves-et-analyses/actives/aurora-bridge/m2-10/`.
- **Assistant :** commandes Studio `preview_aurora_bundle`/`import_aurora_bundle` et CLI
  `forge_cli aurora-audit/import/visit` ; écran Leptos non livré.
- **Validations :** `forge_aurora_runtime` 10 tests, `forge_cli` 7, `forge_aurora_bridge` 9,
  `forge_project` 58 + 9, Studio 2 tests `aurora_import`, Clippy ciblé `--no-deps`, check Studio.
- **Limites :** rendu simplifié sans modèles NWN réels ; aucune VM NCS, ruleset D&D, sauvegarde ou
  parité visuelle ; écran Studio partiel ; fmt global et Clippy `forge_project` préexistants.
- **Prochaine action :** M3 - VM NCS bornée, événements, dialogue conditionnel, journal et quête.

### 2026-11-08 - M3 terminé localement avec réserves

- **Tâches :** `AB-M3-01` à `AB-M3-13`.
- **Spécification :** `docs/aurora-bridge/M3_RUNTIME.md` documente les événements réellement
  traités, les événements non traités, le sous-ensemble NCS, les 11 natives supportées et les
  4 natives déclarées non supportées.
- **Runtime :** crate `forge_nwn_runtime` avec parseur NCS borné, VM déterministe, registre de
  natives, scheduler différé, effets typés et scénario `M3Scenario`.
- **Scénario :** `forge_cli aurora-scenario` exécute `ab_mod_load`, `ab_camp_enter`, quatre branches
  de dialogue, la crypte, le trigger différé, le coffre, le gardien, la porte, la récompense et la
  reprise. Résultat : journal `[10, 20, 30]`, or 50, `AB_RELIC`, 2 transitions, 35 effets,
  11 natives, 0 non supporté, reprise idempotente.
- **Preuves :** `Preuves-et-analyses/actives/aurora-bridge/m3/` avec `m3-report.json`
  (SHA-256 `7d4f4c07…`), `m3-tests.log`, `m3-consumer-tests.log`, `m3-cli.log`,
  `m3-clippy.log` et `README.md`.
- **Validations :** `forge_nwn_runtime` 12 unitaires + 2 scénario + 7 NCS = 21 réussis ;
  runtime/bridge/CLI 27 réussis ; Clippy ciblé `--no-deps` OK ; fmt ciblé OK.
- **Limites :** pas de natives d'action (`AssignCommand`, `DelayCommand`, `ExecuteScript`,
  `ActionDoCommand`), pas de suspension mi-VM, pas de JRL/TLK complet, pas de capture M3, pas de
  bus `forge_gameplay`/physique/permissions, événements non listés non supportés.
- **Prochaine action :** M4 - compatibilité gameplay NWN.

### 2026-11-08 - M4 terminé localement avec réserves

- **Tâches :** `AB-M4-01` à `AB-M4-12`.
- **Profil :** `forge.nwn-rules-profile@1`, document
  `docs/aurora-bridge/nwn-rules-profile.v1.json`, ADR-0064 ; `forge_gameplay` inchangé.
- **Moteur :** crate `forge_nwn_rules` avec RNG SplitMix64, combat d20 (initiative, AC, BAB,
  critiques, concealment, RD, immunités, sauvegardes, attaques spéciales), personnages, objets,
  magasin transactionnel, 4 sorts dont un sort de zone, repos, factions/IA, perception et quête.
- **Scénario :** `forge_cli aurora-gameplay --seed 2026` exécute le gardien, les sorts, le repos,
  la quête et la reprise. Résultat : 2 rounds, 4 actions, 2 déplacements, 1 dégât subi, 1 PV
  soignés, journal 10/20/30, or 50, reprise idempotente.
- **Preuves :** `Preuves-et-analyses/actives/aurora-bridge/m4/` avec `m4-report.json`
  (SHA-256 `e6a0caf1…`), logs de tests, perf, CLI, Clippy et `README.md`.
- **Validations :** 46 tests `forge_nwn_rules`, 43 `forge_nwn_runtime`, 10 `forge_cli`,
  21 `forge_gameplay` ; Clippy ciblé `--no-deps` OK ; fmt ciblé OK.
- **Limites :** tables 2DA absentes, attaques spéciales et sorts complets non couverts, pas de
  perception/pathfinding de navigation, oracle NWN réel non exécuté, pas de FPS/GLB/mémoire.
- **Prochaine action :** M5 - campagne hétérogène, oracle et qualification.

### 2026-11-08 - M5 partiel : migration, trace et diff-oracle

- **Tâches :** `AB-M5-01` à `AB-M5-12` (M5-09 et M5-13 restent ouverts).
- **Livrables :** `M5Save` (`forge.aurora-m5-save@1`), migration M4 → M5, `NwnTrace`
  (`forge.aurora-m5-trace@1`), `aurora-m5`, `aurora-diff-oracle`, test de 3 cycles de transitions,
  budget VM, guide utilisateur et rapport final.
- **Résultat :** 54 entrées de trace, hash `283a5af7…`, sauvegarde `c6d4237a…`, migration
  `accepted=true`/`restored=true`, reprise idempotente, diff replay `matches=true`.
- **Validations :** 46 rules, 43 runtime, 11 aurora-runtime, 10 CLI, 21 `forge_gameplay` ;
  Clippy ciblé et fmt ciblé OK ; M5 CPU 11,723 ms de moyenne, p95 15,526 ms.
- **Preuves :** `Preuves-et-analyses/actives/aurora-bridge/m5/` (`m5-report.json` SHA-256
  `4a9d528b…`, `m5-trace.json` `396150e9…`, `m5-diff.json` `b2230998…`).
- **Limites :** module NWN hétérogène réel absent, preuve visuelle Studio absente, oracle NWN non
  exécuté, GLB du bundle non validés, textes de licence transitifs absents, archive non créée.
  **M5 n'est pas clos.**
