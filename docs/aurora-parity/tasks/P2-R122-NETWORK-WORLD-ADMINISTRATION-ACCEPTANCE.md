# P2 R122 — Network world administration acceptance

## Périmètre

Lot `R122 / FC-MULTI-P2-02` : `ORF-MP-009`, `ORF-MP-010`, `ORF-MP-016`,
`ORF-MP-018` et `ORF-MP-019`. Les services d’instance, compte, administration et
modération existaient ; le lot ferme leurs contrats auteur, leurs adaptateurs manquants et
leur configuration gouvernée commune.

## Matrice de fermeture

| Fonction | Limite réelle | Fermeture et preuve |
|---|---|---|
| `ORF-MP-009` | runtime scene, persistance et collecte non configurables | politique templates/lifecycle/party/persistence/capacité/GC/scène et intégration `ZoneInstanceService` |
| `ORF-MP-010` | clone et overrides validés non regroupés | politique template/clone, allowlist d’overrides, bornes entités/mémoire et autorité serveur |
| `ORF-MP-016` | fournisseur local sûr présent, fournisseurs distants non modélisés | local, OAuth, OIDC, plateforme, anonyme, identifiant stable, confidentialité et secrets sûrs |
| `ORF-MP-018` | commandes sûres présentes, exposition API/CLI et rate limit non configurés | API/CLI authentifiées, rate limit, audit, santé, joueurs, configuration et allowlist |
| `ORF-MP-019` | mute/kick/ban présents, UI et gouvernance de preuve incomplètes | adaptateurs compte/IP/device, timeout, rapports, preuves, appels et rétention bornée |

## Contrat transversal

Le profil `NetworkWorldAdministrationProfile` fournit UUID, schéma, provenance, portée
serveur, révision, audit, validation localisée, dry-run/apply/rejet, undo/redo, round-trip,
handle typé, runtime optionnel/requis, surface Toolset scrollable et service MCP filtré.

## Tests

```powershell
dotnet test tests/Forge.Network.Tests/Forge.Network.Tests.csproj --no-restore
dotnet test tests/Forge.Mcp.Tests/Forge.Mcp.Tests.csproj --no-restore
dotnet build apps/Forge.Toolset/Forge.Toolset.csproj --no-restore
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/Verify.ps1
```

Marqueur attendu : `OPENRPG_FORGE_TOOLSET_R122_NETWORK_WORLD_ADMIN_PASS`.

## Non-objectifs

Persistance SQL, observabilité, profiler, reconnexion, sauvegarde serveur et files d’attente
restent aux lots R123/R124. P3/P4, AV7–AV10, commit et push sont exclus.
