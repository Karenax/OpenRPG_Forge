# ADR-0057 — Profils d'intelligence individuels des assets

Date : 29 août 2026
Statut : accepté
UUID : `67d84697-ea6a-4a00-a63d-33b7ca1125ae`
Schéma : v1
Provenance : `author:openrpg-forge`

## Contexte

Le catalogue Asset Vault savait gouverner identité, source, licence, disponibilité et métadonnées
techniques partielles. Il ne décrivait pas uniformément le sens d'un modèle, son rôle d'assemblage,
sa surface de placement, son pivot ou les preuves ayant produit ces informations. Studio et les
agents devaient donc réinterpréter les noms de fichiers à chaque usage, sans savoir distinguer une
mesure Blender d'une suggestion automatique.

## Décision

- Chaque asset de catalogue reçoit un `AssetIntelligenceProfile` v1 individuel : descriptions
  localisées, type sémantique, famille, rôle, cas d'usage, bornes et dimensions éventuelles, origine,
  axes, classe de pivot, surface, snap, pivot suggéré, ancres, métriques techniques, état de préparation,
  état de revue, confiance et preuves typées.
- L'identité du profil porte le SHA-256 du modèle et celui de sa fermeture de dépendances. Les
  originaux et les modèles normalisés restent immuables ; l'index dérivé vit sous
  `metadata/asset-intelligence.v1.json` dans le Vault externe.
- Une passe déterministe et indépendante de tout fournisseur IA construit toujours un profil. Une
  analyse glTF mesure ce qui est vérifiable ; les formats non inspectables conservent au minimum
  identité, sémantique, hashes et limites explicites de la preuve.
- L'intégration d'un pack au Vault, sa matérialisation dans un workspace et la finalisation Blender
  construisent ou renforcent automatiquement le profil. Les manifestes et l'état d'installation
  embarquent le profil effectivement utilisé.
- Les overlays gouvernés du dépôt peuvent enrichir un inventaire revu, comme les 365 modèles du pack
  Médiéval. Ils ne remplacent un profil que si leur score de preuve est supérieur.
- Tout pivot ou ancrage déduit automatiquement porte `requiresReview=true`. Une génération ne peut
  jamais déclarer seule un asset `human-reviewed` ou prêt pour la production.
- Studio et le MCP exposent les champs sémantiques en lecture et permettent de filtrer le catalogue.
  Les mutations d'un module restent soumises au bus de commandes, au dry-run et aux révisions.

## Preuves

- Validation du contrat : `tests/Forge.Domain.Tests/Assets/AssetIntelligenceProfileValidatorTests.cs`
- Analyse glTF, hashes et index atomique :
  `tests/Forge.ModuleFormat.Tests/Assets/AssetIntelligenceProfileAnalyzerTests.cs`
- Intégration automatique : `LocalAssetPackageInstallerTests.cs`, `AssetVaultPackageStoreTests.cs`
  et les quatre suites `Curated*InstallerTests.cs`
- Exposition humaine et agentique : `StudioAssetLibraryLoaderTests.cs` et
  `ForgeProjectAgentServiceTests.cs`
- Inventaire Blender : `tests/asset_pipeline/test_generate_asset_intelligence_overlay.py`

## Conséquences

La Forge peut rechercher une porte, un élément mural ou un asset d'assemblage sans dépendre uniquement
de son nom. Un agent reçoit les mêmes dimensions, hashes, descriptions et limites de confiance que
l'interface humaine. Le profil reste un conseil gouverné : collisions, navigation, rendu réel,
qualité artistique et adéquation à une scène doivent encore être vérifiés avant promotion.

L'index JSON v1 privilégie lisibilité et portabilité. Une représentation SQLite ou vectorielle pourra
être ajoutée pour accélérer la recherche de masse, sans devenir la source d'autorité du profil.

## Remplacement

Cette décision complète l'ADR-0035 et les conventions Asset Vault sans les remplacer.
