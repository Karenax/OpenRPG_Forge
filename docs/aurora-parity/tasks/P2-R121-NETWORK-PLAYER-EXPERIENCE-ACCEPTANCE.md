# P2 R121 — Network player experience acceptance

## Périmètre

Lot `R121 / FC-MULTI-P2-01` : `ORF-MP-003`, `ORF-MP-004`, `ORF-MP-005`,
`ORF-MP-006` et `ORF-MP-008`. Les critères source ont été relus dans
`21_MULTI.md`. Les fondations serveur R102 sont conservées ; ce lot ferme les surfaces
joueur et auteur qui restaient explicitement absentes.

## Matrice de fermeture

| Fonction | Limite réelle auditée | Fermeture et preuve |
|---|---|---|
| `ORF-MP-003` | service direct/LAN présent, mais annuaire optionnel, favoris, récents, invitations et surface auteur absents | `DiscoveryAccessPolicy`, validation localisée, écran Réseau, round-trip, tests et smoke R121 |
| `ORF-MP-004` | session autoritaire présente, mais contrat complet règles/contenu/slots/équipes/permissions et lobby auteur absent | `LobbyAccessPolicy`, édition transactionnelle, audit et test ciblé |
| `ORF-MP-005` | brouillon validé présent, mais constructeur ruleset/templates/preview/import/approbation incomplet | `CharacterBuilderPolicy`, bornes, UI, test serveur et handle typé |
| `ORF-MP-006` | registre local/serveur présent, mais signature, chiffrement, compte et migration non modélisés ensemble | `CharacterAuthorityPolicy` avec quatre modes et politiques de sécurité validées |
| `ORF-MP-008` | refus PvP simple présent, mais couches globale/zone/groupe, consentement, duel, crime et zones sûres incomplètes | `PlayerCombatPolicy`, validation et test couvrant chaque couche |

## Contrat transversal prouvé

- UUID stables, schéma v1, provenance, portée serveur, révision et identité d’audit ;
- validation localisée avant application ;
- prévisualisation, application, rejet, undo/redo compensatoire ;
- sérialisation JSON déterministe et round-trip exact ;
- interface auteur sans édition de fichier généré ;
- ressource et mutation MCP filtrées avec dry-run, rôle, portée et audit ;
- handle ForgeScript typé et comportement runtime optionnel/requis explicite.

## Tests

```powershell
dotnet test tests/Forge.Network.Tests/Forge.Network.Tests.csproj --no-restore
dotnet test tests/Forge.Mcp.Tests/Forge.Mcp.Tests.csproj --no-restore
dotnet build apps/Forge.Toolset/Forge.Toolset.csproj --no-restore
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/Verify.ps1
```

Marqueur attendu : `OPENRPG_FORGE_TOOLSET_R121_NETWORK_PLAYER_EXPERIENCE_PASS`.

## Non-objectifs

Les instances dynamiques, l’authentification distante, la modération, la persistance,
l’observabilité et la reconnexion appartiennent aux lots R122 à R124. P3/P4, AV7–AV10,
commit et push restent exclus.
