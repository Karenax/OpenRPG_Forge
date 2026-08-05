# G11-T — Accessibilité, sécurité et gouvernance

Date de fermeture : 5 août 2026.

## Résultat observable

Le bouton **Qualité** de Creator présente désormais quinze cartes. Aux preuves fonctionnelles,
réseau, visuelles et de performance s’ajoutent l’accessibilité de l’éditeur, l’accessibilité du
runtime, le fuzzing de sécurité et la gouvernance des dépendances. Les profils sont éditables dans
le workspace **Données** ; le registre de distribution expose état d’approbation, risque, rôles de
revue, quorum et référence d’audit sans édition manuelle de JSON.

Le HUD du runtime applique l’échelle de texte, le contraste élevé, les sous-titres et descriptions
sonores. Les actions restent accessibles au clavier et en liste, et l’option de mouvement réduit
supprime la cinématique de démarrage lorsqu’elle est activée. CLI, runtime et MCP exécutent le même
laboratoire en lecture seule des sources.

## Contrats livrés

- `AccessibilityQualityProfile` persiste clavier, libellés, statut indépendant de la couleur,
  raccourcis, contraste, échelles, sous-titres, descriptions, daltonisme, aides d’entrée, mouvement
  réduit et difficulté ;
- `SecurityFuzzProfile` persiste seed, quotas, budget temporel et sept frontières obligatoires ;
- `WorkspaceGovernanceLabService` exécute 112 cas déterministes sur projet JSON, archive de module,
  ForgeScript, configuration réseau, en-têtes image/modèle et confinement des chemins ;
- `ThirdPartyDependencyApproval` porte désormais état, risque, quorum multi-rôle et preuve d’audit,
  projetés dans le manifeste `.orpmod` 1.2 ;
- le gate interroge aussi NuGet pour les vulnérabilités directes et transitives connues ;
- les formats passent à projet `0.6.21` et workspace `1.15.0`, avec migration stable des profils
  absents.

## Preuves automatiques

- `ModuleRuntimeExtensionProfileTests`, `RuntimeExtensionPersistenceTests`,
  `WorkspaceGovernanceLabServiceTests` et `ForgeProjectAgentServiceTests` couvrent validation,
  migration, déterminisme, confinement et lecture seule ;
- markers `OPENRPG_FORGE_TOOLSET_G11T_GOVERNANCE_PASS`,
  `OPENRPG_FORGE_RUNTIME_G11T_GOVERNANCE_PASS` et `OPENRPG_FORGE_GOVERNANCE_LAB_PASS` ;
- le laboratoire produit 7 contrôles éditeur, 7 contrôles runtime, 112 cas de fuzz et zéro crash
  inattendu ou évasion de chemin sur la Crypte des Cendres ;
- `scripts/Verify.ps1` impose ces surfaces et refuse les packages NuGet vulnérables connus.

## Limites explicites

Le fuzzing est déterministe et borné afin de rester utilisable à chaque gate local ; il complète,
mais ne remplace pas, une campagne longue dans une ferme CI. Les libellés lecteur d’écran utilisent
les métadonnées accessibles proposées par la couche UI ; le support vocal final dépend encore des
API d’accessibilité de la plateforme. La qualité artistique de la carte jouable est traitée dans le
lot de contenu suivant.
