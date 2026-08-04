# G10-B — Actions autoritaires sur le monde live

## Exigences

`ORF-DM-006`, `ORF-DM-007`, `ORF-DM-008`, `ORF-DM-009`, `ORF-DM-010`.

## Livré

- focalisation vers un objet avec métadonnées de zone/instance et contrôle de confidentialité;
- relocalisation prévisualisable et atomique avec destination, sécurité navigation, consentement joueur et politique de groupe;
- destruction administrative confirmée avec cause, politique de loot, impact quête et limite de rollback déclarés;
- soin par portée sélectionnable et raison obligatoire;
- gel/reprise d'IA avec mouvement, collision et visibilité configurables sans perdre l'identité de l'entité;
- permissions serveur, révisions, anti-rejeu, diff sémantique et audit communs aux cinq actions.

## Preuves

- `LiveWorldActionTests`: 4 scénarios;
- `Forge.LiveDirector.Tests`: 7/7;
- tracker G10-B: cinq lignes `Implemented` avec limites d'adaptation runtime explicites.
