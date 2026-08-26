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
