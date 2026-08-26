# G11-A — Profils d’import gouvernés

Date de fermeture : 4 août 2026.

## Périmètre

Ce lot couvre `ORF-CONT-013`, `ORF-CONT-014`, `ORF-EE-004`, `ORF-BUILD-023` et
`ORF-QA-007`. Il transforme le catalogue Asset Vault en une entrée d’auteur contrôlée : un profil
texture ou modèle est validé, prévisualisé, persisté dans le projet et exécuté sans modifier le
Vault source.

## Résultat livré

- profils texture et modèle versionnés dans `assets/import-profiles.json`, avec ordre canonique et
  round-trip déterministe ;
- commande `SetAssetProcessingProfile` avec révision stable, dry-run, patch sémantique, inverse et
  historique undo/redo ;
- même commande utilisée par le Content Browser et par le flux CLI ;
- plan d’import avec clé de cache dérivée du profil, du catalogue et de la source ;
- exécuteur confiné au workspace : copie atomique d’un GLB déjà canonique ou staging de la source
  avec reçu de traitement déterministe ;
- conservation de l’identifiant, du hash, de la licence SPDX et de la provenance dans le reçu ;
- commande reproductible `forge assets prepare` pour un asset du catalogue ;
- blocage des chemins hors Vault/workspace et absence d’écriture dans les originaux.

## Preuves

- `tests/Forge.Application.Tests/Assets/AssetProcessingProfileCommandTests.cs` : dry-run, diff,
  révision, validation, remplacement, undo et redo ;
- `tests/Forge.ModuleFormat.Tests/Workspaces/AssetProcessingProfileStoreTests.cs` : absence de
  fichier, round-trip canonique et diagnostic stable ;
- `tests/Forge.ModuleFormat.Tests/Assets/AssetImportExecutorTests.cs` : copie GLB, réutilisation du
  cache, staging texture, tâches futures et confinement des chemins ;
- `scripts/Verify.ps1` : smoke réel optionnel sur `kenney:castle-kit:door` et
  `ambientcg:3DApple001`, avec contrôle des SHA-256 avant/après ;
- marqueurs `OPENRPG_FORGE_ASSET_PREPARE_PASS` et
  `OPENRPG_FORGE_TOOLSET_G716_ASSETS_PASS ... profile=command-dry-run` ;
- revue visuelle interactive : catalogue de 605 entrées, aperçu/provenance et formulaire de profil
  défilable dans le Content Browser.

## Limites explicites

- un GLB source déjà canonique devient immédiatement `CanonicalReady` ;
- une texture PNG ou un modèle OBJ/FBX est mis en staging avec ses tâches KTX2/GLB, mais le worker
  de conversion et le round-trip Blender appartiennent à G11-C ;
- collisions, animations, VFX et LOD deviennent un manifeste distribuable dans G11-B ;
- installation, mise à jour et désinstallation de packs restent dans AV1/AV2.

## Critère de passage

G11-A est fermé lorsque les tests ciblés, le smoke Toolset, le smoke Vault disponible localement,
le tracker Aurora, le graphe d’architecture et `scripts/Verify.ps1` sont verts sur le même état Git.
