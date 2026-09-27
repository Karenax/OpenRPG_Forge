# Gate 2 — restauration du journal terrain

12 septembre 2026, branche Rust, workspace existant modifié, sans commit/push.
Complément au lot `R2_FIRST_TERRAIN_TRANSACTIONS.md`, dont les empreintes et
résultats restent historiques, pas ceux de ce nouvel incrément.

## Implémenté

Archive JSON stricte `forge.terrain-edit-session` v1 : document initial, reçus,
empreinte finale. Pas de second document courant sérialisé. Chaque commande est
rejouée via le même service métier ; inverse, révision et hash sont comparés.
Doublons de journal et versions inconnues refusés. Lecture bornée à 4 Mio et
1024 commandes ; aucune perte silencieuse d'historique.

## Vérifié localement

- `cargo test -p forge_core -p forge_project --locked --offline` : 88 PASS,
  11 core + 77 project, zéro ignoré. Dont trois nouveaux tests d'archive.
- `cargo clippy -p forge_core -p forge_project --all-targets --all-features --locked --offline -- -D warnings` : PASS.
- Terrain et audit identiques après désérialisation ; octets canoniques identiques.
- Undo/Redo et retry après réouverture ; branche divergente et session vide.
- Refus des inverses altérés, mauvais hash, schéma/version inconnus, doublons,
  champs inconnus, JSON tronqué et entrée trop volumineuse.

Cache E:/OpenRPG_Forge/.gate2-build-cache, deux jobs CPU, aucun processus graphique.
Ces tests sont des tests de sérialisation/rejeu, **pas une sauvegarde disque**.

## Reste à livrer

Adapter de stockage atomique, conflits de fichiers, reprise après interruption,
acteur/audit durable intégré, branchement Studio, création de zone et playtest.
Limite de 1024 commandes à faire évoluer avant une longue session auteur.
Les hashes ne sont pas des signatures d'authenticité. Validation workspace/WASM
et qualification du parcours produit restent requises ; Gate 2 non validée.
