# ADR-0053 — Inventaires MCP et distributions qualifiés depuis les sources

Date : 28 août 2026
Statut : accepté
UUID : `4db832e9-8350-4de3-98d3-6966492321ca`
Schéma : v1
Provenance : `author:openrpg-forge`

## Contexte

Le nombre d'outils annoncé par les sources MCP pouvait diverger du binaire Release déjà installé. La
distribution Studio pouvait également embarquer un ancien `Forge.Cli.exe` incomplet ou être assemblée
depuis un arbre de travail différent de celui qualifié. Une réussite de compilation ne prouvait donc
ni l'inventaire réellement exposé, ni le fonctionnement des exécutables redistribués.

## Décision

- `Forge.McpHost` possède un manifeste `mcp-tools.json` versionné. Le démarrage, `--list-tools`,
  `--tool-inventory` et le self-test comparent la réflexion effective au même inventaire canonique.
- Le contrat Release courant contient exactement 271 outils, sans doublon et avec les groupes voix et
  WorldGeneration attendus.
- `Forge.Cli` expose un self-test exécutable. Les scripts de distribution publient l'application
  autonome réelle, puis l'exécutent depuis le staging au lieu de vérifier seulement sa présence.
- Les vérificateurs Studio et Creator exécutent aussi le host MCP redistribué et exigent son inventaire
  exact. Les manifestes de distribution sont recalculés après ces publications.
- Un build isolé par `--artifacts-path` est la voie de qualification lorsque le host MCP canonique est
  déjà ouvert. Un processus utilisateur actif n'est pas arrêté pour contourner un verrou de DLL.

## Preuves

- Tests : `tests/Forge.Mcp.Tests/McpToolInventoryTests.cs`
- Suite : `tests/Forge.Mcp.Tests/Forge.Mcp.Tests.csproj` (180 tests)
- Self-tests : `Forge.McpHost --self-test` et `Forge.Cli --self-test`
- Scripts : `scripts/Build-StudioDistribution.ps1`, `scripts/Verify-StudioDistribution.ps1`
- Scripts : `scripts/Build-CreatorDistribution.ps1`, `scripts/Verify-CreatorDistribution.ps1`

## Conséquences

Le nombre d'outils, le CLI et les exécutables d'une distribution deviennent des preuves exécutées et
non des métadonnées déclaratives. Un host installé plus ancien peut continuer à fonctionner pendant
la qualification isolée ; il doit cependant être redémarré depuis la nouvelle distribution pour
exposer les 271 outils.

Cette décision ne signe pas les binaires et ne rétablit pas la facturation GitHub Actions. Ces gates
externes restent visibles et ne peuvent pas être remplacées par une réussite locale.

## Remplacement

Cette décision ne remplace aucun ADR. Elle renforce les gates de livraison existants.
