# AV1 — Packs Asset Vault adressés par contenu

Date : 5 août 2026.

Capacités liées : `ORF-CONT-020`, `ORF-CONT-027`, `ORF-CONT-030`.

## Résultat

- registre d’installation v1 dans le Vault externe ;
- plans secs déterministes liés à une révision, application et reçus ;
- copie des modèles et aperçus dans un stockage SHA-256 dédupliqué ;
- packs multiples partageant un même blob sans copie supplémentaire ;
- backup avant désinstallation et collecte des seuls blobs devenus orphelins ;
- crédits agrégés Markdown/texte depuis les assets effectivement installés ;
- état `Installed` et IDs de packs visibles dans le catalogue, Creator, CLI et lecture MCP ;
- originaux et normalisations du Vault jamais modifiés.

## Preuves

- `tests/Forge.ModuleFormat.Tests/Assets/AssetVaultPackageStoreTests.cs` couvre plan sec, application,
  déduplication inter-packs, crédits, backup, désinstallation, source modifiée, licence refusée,
  traversal et état obsolète ;
- `tests/Forge.ModuleFormat.Tests/Assets/AssetCatalogLoaderTests.cs` couvre la projection persistante
  de l’installation dans le catalogue ;
- `tests/Forge.Application.Tests/Assets/AssetCatalogBrowserTests.cs` couvre la facette installée ;
- marqueurs CLI `OPENRPG_FORGE_ASSET_PACK_PLAN_PASS`,
  `OPENRPG_FORGE_ASSET_PACK_INSTALL_PASS`, `OPENRPG_FORGE_ASSET_PACK_UNINSTALL_PASS`,
  `OPENRPG_FORGE_ASSET_VAULT_REPORT_PASS` et `OPENRPG_FORGE_ASSET_CREDITS_PASS`.

## Limites conservées

La mise à jour d’un pack installé, l’acquisition à la demande depuis une entrée distante et le
glisser-déposer vers le monde appartiennent respectivement à AV2 et AV3. L’état physique de
désinstallation est livré ; l’analyse des dépendants et remplacements reste celle du contrat G11-C.
