# Gate 2 — transactions terrain initiales

12 septembre 2026. Lot démarré sur instruction du propriétaire, workspace Rust
existant. La copie de qualification Gate 1 reste indépendante et intacte.

## Implémenté

Service `forge_project::terrain_edit` : renommer et changer l'élévation de base,
aperçu sans effet, hash/révision attendus, ID de commande, retry idempotent dans
la session, inverse, Undo/Redo, audit en mémoire et limite de 1024 commandes.
Ni document de démonstration, ni renderer, ni asset modifié.

## Exécuté et vérifié

Cache isolé `E:/OpenRPG_Forge/.gate2-build-cache`, deux jobs CPU ; aucun GPU lancé.
Preuves : `E:/OpenRPG_Forge/.gate2-evidence/terrain-edit-20260912/`.

- `cargo test -p forge_core -p forge_project --locked --offline` : **85 PASS**,
  11 core et 74 project, dont **7 nouveaux tests d'édition**, zéro ignoré.
- `cargo clippy -p forge_core -p forge_project --all-targets --all-features --offline -- -D warnings` : PASS.
- `cargo fmt --all --check` : PASS ; `git diff --check` : PASS.
- Refus contrôlés : révision/hash périmés, ID réutilisé avec autre demande,
  nom invalide/trop long, NaN, no-op, historique vide, overflow et limite de session.
- Round-trip JSON du document retouché : égalité exacte, historique volontairement
  vide dans la session réouverte. Cela ne prouve PAS une sauvegarde atomique sur disque.

Premier Clippy refusé pour deux noms de variables de test trop proches ; renommage
ciblé, aucun lint désactivé. `clippy.log` conserve l'échec, `clippy-fixed.log` et
`tests-final.log` portent les passages corrigés.

## Empreintes source du lot vérifié

| Fichier | SHA-256 |
|---|---|
| `crates/forge_core/src/lib.rs` | `c64dbdef8e751b0e076ab4cea1b7857ab080ae78964528972c790e2f85ca1f07` |
| `crates/forge_project/src/lib.rs` | `6defb0e705a49927b742e85ba6d4733dac4edeae7945ef2f10c69377d5d81fcd` |
| `crates/forge_project/src/terrain_edit.rs` | `317374659922dce3ebfabae9a0d66d77469e0ed70855635b1e930e9312552e68` |
| `crates/forge_project/src/terrain_edit/tests.rs` | `2f1bc44f8d117f0de37429404171e6220336dbc597908562990a9a65198309d9` |

## Non livré et suite

Pas encore de boutons Studio, création de zone, sauvegarde disque, audit durable,
reprise idempotente après fermeture, projection/playtest ou verrous de régénération.
Pas de validation visuelle, WASM ou workspace entier revendiquée pour ce lot CPU.
Les critères R2-034 à R2-040 restent partiels, pas cochés comme parcours produit.
Suite : session d'application/persistance puis raccord Studio du parcours minimal,
selon [le plan de tranche](../../design/GATE2_AUTHORING_TRANSACTIONS.md).

Aucun commit/push ni modification des fichiers réservés à la clôture Gate 1.
