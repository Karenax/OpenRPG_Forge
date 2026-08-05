# G11-P — Copie runtime, indicateurs, NUI, compilateur et documentation

Date de fermeture : 5 août 2026.

## Résultat observable

La Crypte charge un profil moderne persistant : copie d’entité sous autorité avec nouvel UUID,
prévisualisation AOE colorée et filtrage des cibles, HUD NUI 2.0 responsive avec formulaire/liste/
canvas, compilation ForgeScript incrémentale instrumentée et six catalogues générés vérifiables.

## Contrats livrés

- `RuntimeEntityCopyService` applique quota transactionnel, ownership, remapping des références,
  allowlist des composants d’état et autorité serveur ; le collage sémantique Creator reste la voie
  transactionnelle d’auteur avec dry-run/undo/redo ;
- `AreaEffectPreviewService` traite cercle, cône, rectangle et ligne, hauteur, couverture, cibles
  valides et confirmation serveur ; `RuntimeAreaEffectIndicator` construit le mesh transparent ;
- le toolkit NUI `2.0` ajoute layout responsive, formulaires, listes, canvas, checkbox, slider,
  métadonnées d’accessibilité, résolution de localisation et inspecteur développeur ;
- `ForgeScriptCompilerService` ordonne les dépendances, invalide transitivement, borne les sources,
  trie les diagnostics, mesure les compilations et exécute une suite de conformance ;
- `ForgeDocumentationGenerator` produit feature catalog, API ForgeScript, formats, CLI, package et
  couverture. `forge docs generate|check`, MCP et `Verify.ps1` partagent le mode déterministe.

## Persistance et migrations

- projet agrégé : `0.6.18`, migration depuis `0.6.17` ;
- workspace scindé : `1.12.0`, migration depuis `1.11.0` ;
- trois workspaces du dépôt migrés ; la Crypte contient les widgets NUI 2.0 ;
- UUID de migration dérivés du module et sortie documentaire confinée au dépôt.

## Preuves automatiques

- `ModernRuntimeToolingTests` : copie, autorité, AOE, couverture, hauteur et confirmation ;
- `ForgeScriptCompilerServiceTests` : cache, dépendances, invalidation, stabilité et conformance ;
- `ForgeDocumentationGeneratorTests` : six sorties, déterminisme, dérive et confinement ;
- `RuntimeExtensionPersistenceTests` : round-trip et migration du profil moderne ;
- markers `OPENRPG_FORGE_TOOLSET_G11P_MODERN_PASS`,
  `OPENRPG_FORGE_RUNTIME_G11P_MODERN_PASS` et `OPENRPG_FORGE_DOCUMENTATION_PASS` ;
- capture 1280×720 générée par le runtime et marker `OPENRPG_FORGE_RUNTIME_G11P_VISUAL_PASS` :
  HUD non recouvert, NUI aligné à droite, liste/canvas/checkbox et cercle AOE translucide visibles ;
- gate global `scripts/Verify.ps1` validé le 5 août 2026 : 659 tests .NET, 24 tests Python
  généraux, 11 tests Python du pipeline d’assets, graphe frais, capture runtime 1280×720,
  worker Blender réel et marker final `OPENRPG_FORGE_VERIFICATION=PASS`.

## Limites explicites

Les decals artistiques spécifiques aux sorts, la prédiction réseau sous latence réelle et la
compilation parallèle native sont des adaptateurs ultérieurs. Le lot livre les contrats, quotas,
sélection, mesh fonctionnel, conformance et télémétrie nécessaires sans les simuler comme terminés.
