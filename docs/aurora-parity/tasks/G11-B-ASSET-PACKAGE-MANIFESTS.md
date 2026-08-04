# G11-B — Manifestes d’assets distribuables

Date de fermeture : 4 août 2026.

## Périmètre

Ce lot couvre `ORF-CONT-008`, `ORF-CONT-015`, `ORF-CONT-016`, `ORF-CONT-017` et
`ORF-EE-010`. Il transforme la sortie d’import G11-A en document distribuable, vérifiable et
consommable sans dépendance à la scène du moteur.

## Résultat livré

- `AssetPackageManifest` versionné avec UUID stable, provenance de modification, licence SPDX,
  hash source, budgets par plateforme et chunks adressés par SHA-256 ;
- variantes de plateforme et compression déclarées par chunk ;
- descripteurs canoniques pour collision/cooking et matériaux, bibliothèque d’animations et clips,
  VFX/provider et budgets, ainsi que LOD, seuils, triangles et crossfade ;
- validation à codes stables des UUID, chemins, références, hashes, budgets et seuils ;
- commande `SetAssetPackageManifest` avec révision, dry-run, diff/inverse et undo/redo ;
- persistance atomique et round-trip déterministe dans `assets/asset-manifests.json` ;
- génération depuis l’artefact réellement préparé par G11-A, via le CLI et le Content Browser ;
- inclusion du manifeste et de ses chunks dans `.orpmod` ;
- résolution runtime par plateforme avec contrôle de présence et SHA-256 des chunks requis ; les
  extensions optionnelles absentes sont ignorées ;
- surface d’auteur visuelle avec état vide, choix VFX, préparation, résumé et historique.

## Preuves

- `tests/Forge.Domain.Tests/Assets/AssetPackageManifestValidatorTests.cs` ;
- `tests/Forge.Application.Tests/Assets/AssetPackageManifestTests.cs` ;
- `tests/Forge.ModuleFormat.Tests/Workspaces/AssetPackageManifestStoreTests.cs` ;
- `tests/Forge.ModuleFormat.Tests/Packages/ModulePackageTests.cs` ;
- smoke Vault réel dans `scripts/Verify.ps1`, avec manifeste modèle comprenant collision,
  animation, VFX et trois LOD ;
- marqueurs `OPENRPG_FORGE_ASSET_PREPARE_PASS ... manifest=...`,
  `OPENRPG_FORGE_RUNTIME_ASSET_MANIFESTS_PASS` et
  `OPENRPG_FORGE_TOOLSET_G716_ASSETS_PASS ... manifest=command-dry-run` ;
- revue interactive du Content Browser à 1242 × 807, sans mutation du projet de démonstration.

## Limites explicites

- le manifeste décrit et vérifie les sorties ; le worker Blender/KTX2/GLB qui fabrique les variantes
  manquantes appartient à G11-C ;
- les collisions générées, le retarget d’animation, le rendu du provider VFX et la génération
  physique des LOD restent des adaptateurs/workers ultérieurs ;
- l’édition manuelle fine des walkmeshes et les benchmarks graphiques appartiennent aux lots
  AREA/QA dédiés ;
- l’exposition MCP/ForgeScript complète du manifeste reste à raccorder dans les lots G11-O/G11-P.

## Critère de passage

G11-B est fermé lorsque tests, build, smoke Toolset, smoke Vault, export/rechargement `.orpmod`,
runtime, tracker Aurora, graphe et portail `scripts/Verify.ps1` sont verts sur le même état Git.
