# P2 R123 — Network persistence and observability acceptance

## Périmètre

Lot `R123 / FC-MULTI-P2-03` : `ORF-MP-022`, `ORF-MP-023`, `ORF-MP-028`,
`ORF-MP-030` et `ORF-MP-032`. Les moteurs de snapshot, stockage borné, métriques,
profilage et reconnexion existaient ; le lot ferme leur configuration commune et leurs
adaptateurs auteur.

## Matrice de fermeture

| Fonction | Limite réelle | Fermeture et preuve |
|---|---|---|
| `ORF-MP-022` | fournisseurs, migrations, sauvegardes et bascule non configurables | politique embedded/relationnel, transactions, migrations, sauvegardes, rétention et failover, reliée aux snapshots révisionnés |
| `ORF-MP-023` | allowlist exécutable présente, contrat auteur absent | dépôts serveur, paramètres typés, opérations nommées, services de domaine et interdiction SQL brut |
| `ORF-MP-028` | métriques/santé présentes, export et confidentialité non configurables | Prometheus/OpenTelemetry, métriques opérationnelles, endpoints santé/métriques et filtrage privé |
| `ORF-MP-030` | agrégation présente, politique de capture absente | sampling/instrumentation/traces, breakdown tick et spans script/DB/réseau bornés |
| `ORF-MP-032` | tickets sûrs présents, récupération d’état non contractualisée | grâce, tickets single-use, handoff entité, état groupe, transactions pendantes et repli sûr |

## Contrat transversal

`NetworkPersistenceObservabilityProfile` fournit UUID, schéma, provenance, portée serveur,
révision, audit, validation localisée, dry-run/apply/rejet, undo/redo, round-trip, handle typé,
runtime optionnel/requis, surface Toolset scrollable et MCP filtré.

## Tests

```powershell
dotnet test tests/Forge.Network.Tests/Forge.Network.Tests.csproj --no-restore
dotnet test tests/Forge.Mcp.Tests/Forge.Mcp.Tests.csproj --no-restore
dotnet build apps/Forge.Toolset/Forge.Toolset.csproj --no-restore
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/Verify.ps1
```

Marqueur attendu : `OPENRPG_FORGE_TOOLSET_R123_NETWORK_PERSISTENCE_OBSERVABILITY_PASS`.

## Non-objectifs

Sauvegarde serveur, rollback et files de connexion restent au lot R124. P3/P4,
AV7–AV10, commit et push sont exclus.
