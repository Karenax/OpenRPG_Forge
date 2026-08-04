# G10-E — Opérations live

## Exigences

`ORF-DM-024`, `ORF-DM-025`, `ORF-DM-026`, `ORF-DM-027`, `ORF-DM-028`.

## Livré

- objectif d'attaque de groupe avec avertissement de faction et autorité serveur;
- panneau joueur avec identité, personnage, zone, latence, groupe, permissions, voix et modération;
- observation, téléportation GM, invocation de joueur ou de groupe avec confirmation;
- recherche indexée avec suivant, filtres, requêtes sauvegardées et deep links stables;
- exécution exclusive de commandes administratives enregistrées ou snippets signés et bornés, sans shell ni API arbitraire.

## Preuves

- `LiveDirectorOperationsTests`: 4 scénarios;
- `Forge.LiveDirector.Tests`: 18/18;
- tracker G10-E: cinq lignes `Implemented`.
