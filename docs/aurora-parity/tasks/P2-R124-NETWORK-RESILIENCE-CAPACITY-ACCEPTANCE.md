# P2 R124 — Network resilience and capacity acceptance

## Périmètre

Lot `R124 / FC-MULTI-P2-04` : `ORF-MP-033` et `ORF-MP-034`.

## Matrice de fermeture

| Fonction | Limite réelle | Fermeture et preuve |
|---|---|---|
| `ORF-MP-033` | snapshots et rollback présents, planification durable non configurable | politique snapshots/WAL, répétition de restauration, migrations, PITR, rétention et confirmation destructive |
| `ORF-MP-034` | file FIFO présente, configuration/position/délestage absents de l’auteur | caps sans promesse fixe, slots réservés, réservation, file, position, délestage et métriques |

## Contrat transversal

Le profil `NetworkResilienceCapacityProfile` porte UUID, schéma, provenance, autorité serveur,
révision, audit, validation localisée, preview/apply, compensation undo/redo, round-trip,
ForgeScript typé, runtime optionnel/requis, écran Toolset et ressource MCP filtrée.

## Tests

Les tests `NetworkResilienceCapacityProfileTests` exécutent les vrais services
`InMemoryWorldSnapshotStore`, `RollbackService` et `ConnectionQueue`. Les tests MCP couvrent
recherche bornée, dry-run, apply, revert, portée et plan inconnu. Le marqueur intégré attendu est
`OPENRPG_FORGE_TOOLSET_R124_NETWORK_RESILIENCE_CAPACITY_PASS`.

## Non-objectifs

P3/P4, AV7–AV10, commit et push sont exclus.
