# G10-F — Gouvernance live

## Exigences

`ORF-DM-029`, `ORF-DM-031`, `ORF-DM-032`, `ORF-DM-033`, `ORF-DM-034`.

## Livré

- variables typées avec schéma, aperçu, validation, historique et rollback;
- journal immuable chaîné SHA-256 avec acteur, cibles, avant/après, raison et corrélation;
- scènes préparées réunissant spawns, dialogue, musique, météo, triggers et rollback obligatoire;
- vues omnisciente ou joueur sélectionné avec overlays secrets, navigation et rencontres;
- canaux local, groupe, privé, serveur, GM, faction et impersonation avec logs.

## Preuves

- `LiveDirectorGovernanceTests`: 4 scénarios;
- `Forge.LiveDirector.Tests`: 22/22;
- tracker G10-F: cinq lignes `Implemented`.
