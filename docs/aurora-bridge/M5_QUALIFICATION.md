# Qualification M5 - campagne, traces et reprise

**Version :** 8 novembre 2026.
**Statut :** M5 **partiel** : la qualification technique du scénario synthétique est exécutée ; le
module NWN hétérogène réel, la preuve visuelle Studio, l'oracle NWN local et l'archivage final ne
sont pas prouvés. La validation Khronos couvre 10 GLB du Studio et la SBOM couvre le graphe Cargo
complet.
**Code :** `forge_nwn_runtime::m5`, `forge_cli aurora-m5`, `forge_cli aurora-diff-oracle`.
**Guide :** [`M5_USER_GUIDE.md`](M5_USER_GUIDE.md).
**Oracle externe :** [`AURORA_ORACLE_LOG.md`](AURORA_ORACLE_LOG.md).
**Rapport final :** [`M5_FINAL_REPORT.md`](M5_FINAL_REPORT.md).

## 1. Entrées

| Élément | Valeur |
| --- | --- |
| Bundle | `ab_proving.module-migration-v1` |
| Manifeste | SHA-256 `bfc438679c5aec484afc30ff5bcc6b3147fcc3ebae67bb5b31ad506fddbde4af2` |
| Profil | `nwn-ee-vanilla-m4-subset` |
| Graine | `2026` |
| Schéma sauvegarde | `forge.aurora-m5-save@1` |
| Schéma trace | `forge.aurora-m5-trace@1` |
| Outils | `rustc 1.98.0`, `cargo 1.98.0` |

## 2. Commandes exécutées

```powershell
cargo test -p forge_nwn_rules -p forge_nwn_runtime -p forge_cli -p forge_aurora_runtime
cargo test -p forge_gameplay --lib
cargo clippy -p forge_nwn_rules -p forge_nwn_runtime -p forge_cli -p forge_aurora_runtime `
  --all-targets --no-deps -- -D warnings

cargo run -q -p forge_cli -- aurora-m5 `
  crates/forge_aurora_bridge/tests/fixtures/ab_proving.module-migration-v1 `
  --seed 2026 --report m5-report.json --trace m5-trace.json

cargo run -q -p forge_cli -- aurora-diff-oracle `
  m5-report.json m5-report-replay.json --report m5-diff.json
```

## 3. Résultat M5

```text
AURORA_M5 module=Les Épreuves du Pont trace_entries=54
trace_sha256=283a5af7…
save_sha256=c6d4237a…
migration=forge.aurora-m4-save@1 -> forge.aurora-m5-save@1 accepted=true restored=true
resume_idempotent=true transitions=2
```

- La sauvegarde M4 est migrée explicitement vers M5 puis relue.
- La trace contient 54 entrées (`event`, `script`, `spell`, `combat`) et un hash stable.
- La reprise restaure la session, les personnages, l'inventaire, les sorts, les effets, la quête,
  le RNG et la sauvegarde M4 embarquée.
- Le replay M5 produit le même rapport ; `aurora-diff-oracle` retourne `matches=true`.
- Une mutation de `gold` produit une divergence `gold` et un code de sortie 2.

## 4. Couverture des tâches M5

| Tâche | État | Preuve / réserve |
| --- | --- | --- |
| M5-01 `ModuleSession` complet | partiel | accès zones chargées/visitées, transitions répétées, reprise M4/M5 ; pas de registry globale/compagnons |
| M5-02 migration des sauvegardes | fait | M4 → M5, schéma inconnu refusé, aucune sauvegarde native modifiée |
| M5-03 transitions répétées | fait | test 3 cycles camp↔crypte, 6 transitions, restauration après chaque cycle |
| M5-04 diagnostics Studio | partiel | panneau Leptos `AuroraM5Panel` + commande native `run_aurora_gameplay` ; aucune capture visuelle |
| M5-05 traces reproductibles | fait | 54 entrées, hash stable, aucune trace par défaut |
| M5-06 `diff-oracle` | partiel | commande, rapport et harnais `aurora-oracle-log` ; oracle NWN réel non exécuté |
| M5-07 fuzz/robustesse | partiel | NCS tronqué/muté, budget VM, sauvegarde/profil corrompus, symlinks refusés ; archives non ingérées |
| M5-08 échelle/performance | partiel | M5 CPU 11,723 ms ; audit de 3 modules réels (42/25/29 zones), pic 9,2 Mo ; FPS non mesuré |
| M5-09 glTF/navigation | partiel | 10 GLB validés Khronos, 0 erreur, warnings classés ; pas de GLB de bundle |
| M5-10 SBOM/licences | fait | SBOM transitif `m5-sbom-full.json`, 909 composants |
| Tables 2DA locales | outil | `aurora-2da` : 43 classes, 840 sorts, 1116 feats, 30 races |
| M5-11 documentation | fait | guide utilisateur/mainteneur |
| M5-12 rapport final | fait | `M5_FINAL_REPORT.md` + matrice |
| M5-13 clôture/archivage | bloqué | M5 partiel ; aucune archive ni suppression de preuves |

## 5. Robustesse vérifiée

- NCS : troncatures et mutations sans panic, opcode inconnu refusé, budget VM sur boucle infinie ;
- sauvegarde M5 : schéma inconnu, PV hors bornes, personnage dupliqué, objet inconnu refusés ;
- profil : schéma, niveau, seuil d'hostilité, rounds, vitesse et portée validés ;
- trace : séquence, taille, champs vides et chemins absolus refusés ;
- bundle : validation structurelle M1 réutilisée, aucun fichier non listé accepté.

## 6. Limites explicites

- module hétérogène NWN réel non importé/joué ;
- panneau Studio M5 livré dans le code, sans capture ni vérification visuelle ;
- oracle NWN local non exécuté ; `aurora-oracle-log` attend un log externe ;
- validation Khronos faite sur 10 GLB du Studio, pas sur les GLB du bundle ni la scène de navigation ;
- SBOM complète pour le graphe Cargo, textes de licence transitifs non agrégés ;
- audit d'échelle structurel seulement ; pas de FPS ni de chargement GLB mesurés ;
- archives ZIP64 non ingérées ; les preuves restent actives ;
- aucune compatibilité NWN générale n'est revendiquée.

## 7. Preuves

`Preuves-et-analyses/actives/aurora-bridge/m5/` :

- `m5-report.json` ;
- `m5-trace.json` ;
- `m5-report-replay.json` ;
- `m5-diff.json` ;
- `m5-gltf-validation.json`, `m5-2da-audit.json`, `m5-sbom-full.json` ;
- `m5-module-scale.json`, `m5-resource-usage.json` ;
- logs de tests, perf, CLI, Clippy et `README.md`.
