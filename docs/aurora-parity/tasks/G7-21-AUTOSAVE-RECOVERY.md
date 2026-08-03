# G7-21 — Sauvegarde automatique, récupération et instructions joueur

## Résultat

Le Toolset capture toutes les 60 secondes une révision modifiée dans `.forge/recovery`, sans écrire
le projet canonique. L’écran **Récupération** permet aussi une capture manuelle, affiche le journal,
vérifie le hash avant lecture, restaure explicitement en mémoire et rappelle que le bouton
**Enregistrer** reste la seule confirmation vers les documents canoniques.

Le stockage fournit un journal schéma v1, des écritures atomiques et une rétention de douze copies.
Une copie altérée est refusée. Effacer le journal est une action explicite.

La configuration localisée du module couvre déjà nom, synopsis, description/instructions joueur et
crédits, complétés par licence et compatibilité de publication. L’interface précise désormais la
structure conseillée : installation, règles, contrôles, avertissements et compatibilité. Cette
édition continue de passer par `SetModuleConfigurationHandler`, son dry-run et l’historique.

## Preuves

- `src/Forge.ModuleFormat/Workspaces/RecoverySnapshotStore.cs`
- `apps/Forge.Toolset/addons/openrpg_forge/RecoveryWindow.cs`
- `tests/Forge.ModuleFormat.Tests/Workspaces/RecoverySnapshotStoreTests.cs`
- `tests/Forge.Application.Tests/Projects/SetModuleConfigurationHandlerTests.cs`
- `tests/Forge.ModuleFormat.Tests/Serialization/ProjectJsonSerializerTests.cs`

## Limites explicites

La détection OS d’un arrêt non propre et la fusion sélective entre instantanés ne sont pas incluses.
Les instructions joueur sont des pages éditoriales localisées regroupées dans la description, pas
encore un mini-site Markdown à navigation séparée.

La prochaine tranche unique est **G7-22 — bibliothèque et gouvernance des palettes**.
