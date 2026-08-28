# Gouvernance des décisions d'architecture

Les ADRs utilisent `ADR-TEMPLATE.md`, un UUID stable, un schéma v1 et une provenance. Les statuts
autorisés sont `proposé`, `accepté`, `remplacé` et `rejeté` (valeurs canoniques anglaises :
`proposed`, `accepted`, `superseded`, `rejected`).

Une décision acceptée est immuable. Toute évolution structurelle crée un ADR de remplacement et lie
l'ancien au nouveau. Chaque décision référence ses tests ; toute promesse de performance référence
également le benchmark reproductible correspondant. La revue du lot vérifie ces liens avant
intégration.

La génération de monde multi-backend et sa frontière avec les données canoniques sont fixées par
[`ADR-0050`](ADR-0050-governed-world-generation-orchestration.md).

La concurrence entre Studio, MCP et les autres auteurs locaux est régie par les révisions persistantes
et transactions de workspace de
[`ADR-0051`](ADR-0051-persistent-workspace-revisions.md).

La sauvegarde et les transitions de jeu entre plusieurs zones suivent le contrat d'état de session de
[`ADR-0052`](ADR-0052-runtime-session-state-across-areas.md).

Les inventaires MCP, CLI et distributions sont qualifiés depuis les exécutables effectivement
produits selon [`ADR-0053`](ADR-0053-qualified-mcp-and-distribution-inventories.md).

Les expériences et compositions propres aux jeux appartiennent aux modules, selon
[`ADR-0054`](ADR-0054-module-owned-runtime-experience.md).

Le chargement runtime, le packaging et le parcours Studio d'une promotion WorldGeneration réelle
sont fixés par [`ADR-0055`](ADR-0055-runtime-world-generation-vertical-slice.md).

Les nouvelles sessions Internet suivent le transport TLS épinglé, l'authentification par invitation
et l'autorité gameplay bornée de
[`ADR-0056`](ADR-0056-secure-authoritative-internet-transport.md).
